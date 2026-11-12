using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Velopack;
using Velopack.Sources;

namespace TypeForge.App.Services;

/// <summary>
/// Outcome status from checking GitHub Releases for a newer TypeForge build.
/// </summary>
public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    InstallerUpdateAvailable,
    Error
}

/// <summary>
/// Summary returned by an update check against GitHub Releases.
/// </summary>
public sealed record AppUpdateCheckOutcome(
    UpdateCheckStatus Status,
    string CurrentVersion,
    string? AvailableVersion,
    string ReleaseNotes,
    string Message,
    string ReleaseUrl);

/// <summary>
/// Checks for updates via Velopack / GitHub Releases, fetches What's New notes, and applies updates.
/// </summary>
public interface IUpdateService
{
    string CurrentVersion { get; }
    bool IsInstalledViaInstaller { get; }
    bool HasDownloadableUpdate { get; }
    string ReleasePageUrl { get; }
    AppUpdateCheckOutcome? LastOutcome { get; }

    event Action<AppUpdateCheckOutcome>? UpdateStatusChanged;

    Task<AppUpdateCheckOutcome> CheckForUpdatesAsync(CancellationToken ct = default);
    Task<bool> DownloadAndApplyUpdateAsync(Action<int>? onProgress = null, CancellationToken ct = default);
}

/// <summary>
/// Concrete updater integrating Velopack delta updates and GitHub release_notes.md retrieval.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    private const string RepoUrl = "https://github.com/Unknownplanet40/-type-me-up";
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/Unknownplanet40/-type-me-up/releases/latest";
    private const string LatestReleaseNotesUrl = "https://github.com/Unknownplanet40/-type-me-up/releases/latest/download/release_notes.md";
    private const string LatestReleasePageUrl = "https://github.com/Unknownplanet40/-type-me-up/releases/latest";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    private readonly UpdateManager _manager;
    private UpdateInfo? _pendingUpdate;

    public UpdateService()
    {
        _manager = new UpdateManager(new GithubSource(RepoUrl, null, false));
    }

    public bool IsInstalledViaInstaller => _manager.IsInstalled;
    public bool HasDownloadableUpdate => _pendingUpdate is not null;
    public string ReleasePageUrl { get; private set; } = LatestReleasePageUrl;
    public AppUpdateCheckOutcome? LastOutcome { get; private set; }

    public event Action<AppUpdateCheckOutcome>? UpdateStatusChanged;

    public string CurrentVersion
    {
        get
        {
            if (_manager.IsInstalled && _manager.CurrentVersion is not null)
            {
                return _manager.CurrentVersion.ToString();
            }

            string? infoVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (!string.IsNullOrWhiteSpace(infoVersion))
            {
                return infoVersion.Split('+')[0].Trim();
            }

            return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
        }
    }

    public async Task<AppUpdateCheckOutcome> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        AppUpdateCheckOutcome outcome = await CheckForUpdatesCoreAsync(ct).ConfigureAwait(false);
        LastOutcome = outcome;
        UpdateStatusChanged?.Invoke(outcome);
        return outcome;
    }

    private async Task<AppUpdateCheckOutcome> CheckForUpdatesCoreAsync(CancellationToken ct)
    {
        string current = CurrentVersion;
        _pendingUpdate = null;
        ReleasePageUrl = LatestReleasePageUrl;

        string releaseNotes = await FetchLatestReleaseNotesAsync(ct).ConfigureAwait(false);

        if (_manager.IsInstalled)
        {
            try
            {
                UpdateInfo? update = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
                if (update is null)
                {
                    return new AppUpdateCheckOutcome(
                        UpdateCheckStatus.UpToDate,
                        current,
                        null,
                        releaseNotes,
                        $"You are up to date on TypeForge v{current}.",
                        LatestReleasePageUrl);
                }

                _pendingUpdate = update;
                string targetVersion = update.TargetFullRelease.Version.ToString();
                string notes = !string.IsNullOrWhiteSpace(releaseNotes)
                    ? releaseNotes
                    : update.TargetFullRelease.NotesMarkdown ?? string.Empty;

                return new AppUpdateCheckOutcome(
                    UpdateCheckStatus.UpdateAvailable,
                    current,
                    targetVersion,
                    notes.Trim(),
                    $"Update v{targetVersion} is ready to download and install.",
                    LatestReleasePageUrl);
            }
            catch (Exception ex)
            {
                return new AppUpdateCheckOutcome(
                    UpdateCheckStatus.Error,
                    current,
                    null,
                    releaseNotes,
                    $"Could not check updates: {ex.Message}",
                    LatestReleasePageUrl);
            }
        }

        // Portable / development run: query GitHub Releases metadata cleanly without reflection.
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, LatestReleaseApiUrl);
            using HttpResponseMessage response = await HttpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new AppUpdateCheckOutcome(
                    UpdateCheckStatus.Error,
                    current,
                    null,
                    releaseNotes,
                    $"GitHub Releases returned HTTP {(int)response.StatusCode}.",
                    LatestReleasePageUrl);
            }

            string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            string tag = root.TryGetProperty("tag_name", out JsonElement tagEl)
                ? (tagEl.GetString() ?? string.Empty)
                : string.Empty;

            if (root.TryGetProperty("html_url", out JsonElement urlEl) && !string.IsNullOrWhiteSpace(urlEl.GetString()))
            {
                ReleasePageUrl = urlEl.GetString()!;
            }

            if (root.TryGetProperty("assets", out JsonElement assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement asset in assetsEl.EnumerateArray())
                {
                    string name = asset.TryGetProperty("name", out JsonElement nameEl)
                        ? (nameEl.GetString() ?? string.Empty)
                        : string.Empty;
                    if (name.EndsWith("Setup.exe", StringComparison.OrdinalIgnoreCase)
                        && asset.TryGetProperty("browser_download_url", out JsonElement dlEl)
                        && !string.IsNullOrWhiteSpace(dlEl.GetString()))
                    {
                        ReleasePageUrl = dlEl.GetString()!;
                        break;
                    }
                }
            }

            string normalizedLatest = tag.TrimStart('v', 'V').Trim();
            if (Version.TryParse(NormalizeSemVer(normalizedLatest), out Version? latestVer)
                && Version.TryParse(NormalizeSemVer(current), out Version? currentVer)
                && latestVer > currentVer)
            {
                return new AppUpdateCheckOutcome(
                    UpdateCheckStatus.InstallerUpdateAvailable,
                    current,
                    normalizedLatest,
                    releaseNotes,
                    $"TypeForge v{normalizedLatest} is available. Download the Windows Setup installer to upgrade.",
                    ReleasePageUrl);
            }

            return new AppUpdateCheckOutcome(
                UpdateCheckStatus.UpToDate,
                current,
                string.IsNullOrWhiteSpace(normalizedLatest) ? null : normalizedLatest,
                releaseNotes,
                $"You are up to date on TypeForge v{current}. Install via TypeForge Setup for 1-click auto-updates.",
                ReleasePageUrl);
        }
        catch (Exception ex)
        {
            return new AppUpdateCheckOutcome(
                UpdateCheckStatus.Error,
                current,
                null,
                releaseNotes,
                $"Unable to check GitHub Releases offline or right now: {ex.Message}",
                LatestReleasePageUrl);
        }
    }

    public async Task<bool> DownloadAndApplyUpdateAsync(Action<int>? onProgress = null, CancellationToken ct = default)
    {
        if (!_manager.IsInstalled || _pendingUpdate is null)
        {
            return false;
        }

        await _manager.DownloadUpdatesAsync(_pendingUpdate, p => onProgress?.Invoke(p), ct).ConfigureAwait(false);
        _manager.ApplyUpdatesAndRestart(_pendingUpdate);
        return true;
    }

    private static async Task<string> FetchLatestReleaseNotesAsync(CancellationToken ct)
    {
        try
        {
            using HttpResponseMessage response = await HttpClient.GetAsync(LatestReleaseNotesUrl, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return string.Empty;
            }

            string text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return text.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string NormalizeSemVer(string raw)
    {
        string core = raw.Split('-')[0].Trim();
        return core.Count(c => c == '.') switch
        {
            0 when int.TryParse(core, out _) => $"{core}.0.0",
            1 => $"{core}.0",
            _ => core
        };
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TypeForge", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
