using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI.Dispatching;

namespace TypeForge.App.Services;

/// <summary>
/// Lifecycle states for GitHub Release checking and Inno Setup installation.
/// </summary>
public enum UpdateState
{
    Idle,
    Checking,
    UpdateAvailable,
    UpToDate,
    Downloading,
    Downloaded,
    Failed
}

/// <summary>
/// Metadata for the latest TypeForge GitHub Release and its Windows Setup asset.
/// </summary>
public sealed record ReleaseInfo(
    string TagName,
    Version Version,
    string Name,
    string Changelog,
    string DownloadUrl,
    long DownloadSize,
    string HtmlUrl,
    DateTimeOffset? PublishedAt);

internal sealed class GitHubReleaseAssetDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

internal sealed class GitHubReleaseDto
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public GitHubReleaseAssetDto[]? Assets { get; set; }
}

[JsonSerializable(typeof(GitHubReleaseDto))]
internal sealed partial class UpdateJsonContext : JsonSerializerContext;

/// <summary>
/// Checks GitHub Releases, downloads the Inno Setup installer with progress, and performs a silent self-update handoff.
/// </summary>
public interface IUpdateService
{
    UpdateState State { get; }
    ReleaseInfo? LatestRelease { get; }
    string StatusMessage { get; }
    double DownloadProgress { get; }
    string CurrentVersionString { get; }
    bool IsInstalledCopy { get; }
    string ReleasePageUrl { get; }

    event Action<UpdateState, string>? StateChanged;
    event Action<double>? DownloadProgressChanged;

    void StartBackgroundMonitoring(DispatcherQueue dispatcherQueue);
    void StopBackgroundMonitoring();
    Task<ReleaseInfo?> CheckForUpdatesAsync(bool silentBackground = false, CancellationToken cancellationToken = default);
    Task<bool> DownloadAndInstallAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Inno Setup + GitHub Releases updater following the LivelyScreen silent installer handoff flow.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private const string RepoOwner = "Unknownplanet40";
    private const string RepoName = "-type-me-up";
    private const string LatestReleaseUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
    private const string DefaultReleasePageUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/latest";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    private DispatcherQueue? _dispatcherQueue;
    private DispatcherQueueTimer? _startupTimer;
    private DispatcherQueueTimer? _periodicTimer;
    private int _isChecking;

    public UpdateState State { get; private set; } = UpdateState.Idle;
    public ReleaseInfo? LatestRelease { get; private set; }
    public string StatusMessage { get; private set; } = "Click Check for Updates to check GitHub Releases.";
    public double DownloadProgress { get; private set; }
    public string ReleasePageUrl => LatestRelease?.HtmlUrl is { Length: > 0 } url ? url : DefaultReleasePageUrl;

    public bool IsInstalledCopy => File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    public string CurrentVersionString => GetCurrentVersionString();

    public static Version CurrentVersion
    {
        get
        {
            return ParseVersion(GetCurrentVersionString())
                ?? Assembly.GetExecutingAssembly().GetName().Version
                ?? new Version(0, 1, 0);
        }
    }

    public event Action<UpdateState, string>? StateChanged;
    public event Action<double>? DownloadProgressChanged;

    public void StartBackgroundMonitoring(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        if (_startupTimer is not null)
        {
            return;
        }

        _startupTimer = dispatcherQueue.CreateTimer();
        _startupTimer.Interval = TimeSpan.FromSeconds(12);
        _startupTimer.IsRepeating = false;
        _startupTimer.Tick += async (_, _) =>
        {
            _startupTimer?.Stop();
            await CheckForUpdatesAsync(silentBackground: true);
        };
        _startupTimer.Start();

        _periodicTimer = dispatcherQueue.CreateTimer();
        _periodicTimer.Interval = TimeSpan.FromHours(24);
        _periodicTimer.IsRepeating = true;
        _periodicTimer.Tick += async (_, _) => await CheckForUpdatesAsync(silentBackground: true);
        _periodicTimer.Start();
    }

    public void StopBackgroundMonitoring()
    {
        _startupTimer?.Stop();
        _periodicTimer?.Stop();
    }

    public async Task<ReleaseInfo?> CheckForUpdatesAsync(bool silentBackground = false, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _isChecking, 1) == 1)
        {
            return LatestRelease;
        }

        try
        {
            if (!silentBackground)
            {
                SetState(UpdateState.Checking, "Checking GitHub Releases for updates...");
            }

            using HttpResponseMessage response = await HttpClient.GetAsync(LatestReleaseUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                string error = response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "No public GitHub release found (HTTP 404 — repository may be private or have no published release)."
                    : $"Update check failed (HTTP {(int)response.StatusCode}).";
                if (!silentBackground)
                {
                    SetState(UpdateState.Failed, error);
                }

                return null;
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            GitHubReleaseDto? dto = JsonSerializer.Deserialize(json, UpdateJsonContext.Default.GitHubReleaseDto);
            if (dto is null || string.IsNullOrWhiteSpace(dto.TagName))
            {
                if (!silentBackground)
                {
                    SetState(UpdateState.Failed, "Invalid release metadata returned by GitHub.");
                }

                return null;
            }

            Version? latestVersion = ParseVersion(dto.TagName);
            if (latestVersion is null)
            {
                if (!silentBackground)
                {
                    SetState(UpdateState.Failed, $"Unrecognized version tag: {dto.TagName}");
                }

                return null;
            }

            GitHubReleaseAssetDto? installerAsset = FindInstallerAsset(dto.Assets);
            string changelog = await ResolveReleaseNotesAsync(dto, cancellationToken).ConfigureAwait(false);

            var info = new ReleaseInfo(
                dto.TagName,
                latestVersion,
                string.IsNullOrWhiteSpace(dto.Name) ? dto.TagName : dto.Name,
                changelog,
                installerAsset?.BrowserDownloadUrl ?? string.Empty,
                installerAsset?.Size ?? 0,
                dto.HtmlUrl ?? DefaultReleasePageUrl,
                dto.PublishedAt);

            LatestRelease = info;

            if (IsNewerVersion(latestVersion, CurrentVersion))
            {
                SetState(UpdateState.UpdateAvailable, $"Update available: {info.TagName} (Current: v{CurrentVersionString})");
                return info;
            }

            if (!silentBackground)
            {
                SetState(UpdateState.UpToDate, $"You are up to date on TypeForge v{CurrentVersionString}.");
            }
            else if (State == UpdateState.Idle)
            {
                StatusMessage = $"You are up to date on TypeForge v{CurrentVersionString}.";
            }

            return info;
        }
        catch (OperationCanceledException)
        {
            if (!silentBackground)
            {
                SetState(UpdateState.Idle, "Update check cancelled.");
            }

            return null;
        }
        catch (Exception ex)
        {
            if (!silentBackground)
            {
                SetState(UpdateState.Failed, $"Could not check updates: {ex.Message}");
            }

            return null;
        }
        finally
        {
            Interlocked.Exchange(ref _isChecking, 0);
        }
    }

    public async Task<bool> DownloadAndInstallAsync(CancellationToken cancellationToken = default)
    {
        if (LatestRelease is null || string.IsNullOrWhiteSpace(LatestRelease.DownloadUrl))
        {
            SetState(UpdateState.Failed, "No Windows Setup installer asset was found on the latest GitHub release.");
            return false;
        }

        try
        {
            DownloadProgress = 0;
            ReportProgress(0);
            SetState(UpdateState.Downloading, $"Downloading {LatestRelease.TagName} installer...");

            string tempDir = Path.Combine(Path.GetTempPath(), "TypeForgeUpdates");
            Directory.CreateDirectory(tempDir);

            string fileName = Path.GetFileName(new Uri(LatestRelease.DownloadUrl).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                fileName = $"TypeForge-Setup-{LatestRelease.TagName}.exe";
            }

            string installerPath = Path.Combine(tempDir, fileName);
            if (File.Exists(installerPath))
            {
                File.Delete(installerPath);
            }

            using (HttpResponseMessage response = await HttpClient
                .GetAsync(LatestRelease.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                long totalBytes = response.Content.Headers.ContentLength ?? LatestRelease.DownloadSize;

                await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var fs = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        double progress = Math.Clamp((double)totalRead / totalBytes, 0, 1);
                        DownloadProgress = progress;
                        ReportProgress(progress);
                    }
                }

                await fs.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            DownloadProgress = 1;
            ReportProgress(1);
            SetState(UpdateState.Downloaded, "Download complete. Launching silent installer...");
            InstallSilentAndRestart(installerPath);
            return true;
        }
        catch (OperationCanceledException)
        {
            SetState(UpdateState.Idle, "Download cancelled.");
            return false;
        }
        catch (Exception ex)
        {
            SetState(UpdateState.Failed, $"Download failed: {ex.Message}");
            return false;
        }
    }

    public static void InstallSilentAndRestart(string installerPath)
    {
        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException("Downloaded installer could not be found.", installerPath);
        }

        string? currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        string? restartExe = ResolveRestartExecutablePath(currentExe);

        string arguments = string.IsNullOrWhiteSpace(restartExe)
            ? $"/c ping 127.0.0.1 -n 3 > nul & \"{installerPath}\" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART"
            : $"/c ping 127.0.0.1 -n 3 > nul & \"{installerPath}\" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART & start \"\" \"{restartExe}\"";

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false
        };

        Process.Start(psi);
        Environment.Exit(0);
    }

    public static Version? ParseVersion(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        string clean = tag.Trim().TrimStart('v', 'V').Split('-')[0].Trim();
        if (Version.TryParse(clean, out Version? parsed))
        {
            return NormalizeThreePart(parsed);
        }

        string[] parts = clean.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1 && int.TryParse(parts[0], out int major))
        {
            return new Version(major, 0, 0);
        }

        return null;
    }

    private static bool IsNewerVersion(Version candidate, Version current)
    {
        return NormalizeThreePart(candidate) > NormalizeThreePart(current);
    }

    private static Version NormalizeThreePart(Version version)
    {
        int major = Math.Max(0, version.Major);
        int minor = Math.Max(0, version.Minor);
        int build = Math.Max(0, version.Build);
        return new Version(major, minor, build);
    }

    private static GitHubReleaseAssetDto? FindInstallerAsset(GitHubReleaseAssetDto[]? assets)
    {
        if (assets is null || assets.Length == 0)
        {
            return null;
        }

        return assets.FirstOrDefault(a =>
                !string.IsNullOrWhiteSpace(a.Name)
                && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && a.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl))
            ?? assets.FirstOrDefault(a =>
                !string.IsNullOrWhiteSpace(a.Name)
                && a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl));
    }

    private static async Task<string> ResolveReleaseNotesAsync(GitHubReleaseDto dto, CancellationToken cancellationToken)
    {
        GitHubReleaseAssetDto? notesAsset = dto.Assets?.FirstOrDefault(a =>
            string.Equals(a.Name, "release_notes.md", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl));

        if (notesAsset?.BrowserDownloadUrl is { Length: > 0 } url)
        {
            try
            {
                using HttpResponseMessage response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    string markdown = (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Trim();
                    if (!string.IsNullOrWhiteSpace(markdown))
                    {
                        return markdown;
                    }
                }
            }
            catch
            {
                // Fall back to the release body if downloading release_notes.md fails.
            }
        }

        return dto.Body?.Trim() ?? string.Empty;
    }

    private static string? ResolveRestartExecutablePath(string? currentExe)
    {
        if (string.IsNullOrWhiteSpace(currentExe))
        {
            return null;
        }

        // Dev build output isn't overwritten by Inno Setup; launch the installed executable instead.
        if (currentExe.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase)
            || currentExe.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "TypeForge",
                "TypeForge.App.exe");
        }

        return currentExe;
    }

    private static string GetCurrentVersionString()
    {
        string? infoVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            return infoVersion.Split('+')[0].Trim();
        }

        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
    }

    private void SetState(UpdateState state, string message)
    {
        State = state;
        StatusMessage = message;
        if (_dispatcherQueue is not null)
        {
            _dispatcherQueue.TryEnqueue(() => StateChanged?.Invoke(state, message));
        }
        else
        {
            StateChanged?.Invoke(state, message);
        }
    }

    private void ReportProgress(double progress)
    {
        if (_dispatcherQueue is not null)
        {
            _dispatcherQueue.TryEnqueue(() => DownloadProgressChanged?.Invoke(progress));
        }
        else
        {
            DownloadProgressChanged?.Invoke(progress);
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        string version = GetCurrentVersionString();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TypeForge-WinUI-Updater", version));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
