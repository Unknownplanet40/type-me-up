using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using TypeForge.Core.Engine;
using TypeForge.Core.Models;

namespace TypeForge.Core.Services;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ProfileRegistry))]
internal sealed partial class ProfileJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Manages local user profiles, per-profile SQLite database paths, legacy DB migration, and cross-profile rankings.
/// </summary>
public interface IProfileService
{
    /// <summary>All registered local profiles.</summary>
    IReadOnlyList<UserProfile> Profiles { get; }

    /// <summary>Currently active user profile.</summary>
    UserProfile ActiveProfile { get; }

    /// <summary>Curated avatar badge hex colors.</summary>
    IReadOnlyList<string> AvatarPalette { get; }

    /// <summary>Loads or initializes the profile registry and migrates any legacy database.</summary>
    void Initialize();

    /// <summary>Returns the SQLite database file path for the specified profile ID.</summary>
    string GetDatabasePath(string profileId);

    /// <summary>Creates a repository targeting the active profile's SQLite database.</summary>
    ITypeForgeRepository CreateRepositoryForActiveProfile();

    /// <summary>Creates a new profile and persists it to the registry.</summary>
    UserProfile CreateProfile(string name, string? avatarColorHex = null);

    /// <summary>Renames an existing profile and persists the registry.</summary>
    void RenameProfile(string profileId, string newName);

    /// <summary>Marks the specified profile as active and updates its last-used timestamp.</summary>
    void SetActiveProfile(string profileId);

    /// <summary>Deletes a profile and its SQLite database file; throws if it is the last remaining profile.</summary>
    void DeleteProfile(string profileId);

    /// <summary>Computes ranked leaderboard entries across all profiles ordered by average net WPM.</summary>
    Task<IReadOnlyList<ProfileRankingEntry>> BuildRankingsAsync(int totalCourseExercises, CancellationToken ct = default);
}

/// <summary>
/// File-backed implementation of <see cref="IProfileService"/> storing <c>profiles.json</c> and per-profile SQLite files.
/// </summary>
public sealed class ProfileService : IProfileService
{
    private static readonly string[] DefaultPalette =
    [
        "#A78BFA", // Purple
        "#34D399", // Emerald
        "#60A5FA", // Blue
        "#FBBF24", // Amber
        "#F472B6", // Pink
        "#FB923C"  // Orange
    ];

    private readonly string _rootDirectory;
    private readonly string _registryPath;
    private readonly string _profilesDirectory;
    private readonly TimeProvider _timeProvider;
    private ProfileRegistry _registry = new();
    private bool _initialized;

    /// <summary>Initializes the profile service with an optional root directory and time provider.</summary>
    public ProfileService(string? rootDirectory = null, TimeProvider? timeProvider = null)
    {
        _rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? GetDefaultRootDirectory()
            : rootDirectory;
        _registryPath = Path.Combine(_rootDirectory, "profiles.json");
        _profilesDirectory = Path.Combine(_rootDirectory, "profiles");
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Returns the default per-user root directory under <c>%LOCALAPPDATA%\TypeForge</c>.</summary>
    public static string GetDefaultRootDirectory()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "TypeForge");
    }

    /// <inheritdoc />
    public IReadOnlyList<UserProfile> Profiles => _registry.Profiles;

    /// <inheritdoc />
    public UserProfile ActiveProfile =>
        _registry.Profiles.FirstOrDefault(p => string.Equals(p.Id, _registry.ActiveProfileId, StringComparison.OrdinalIgnoreCase))
        ?? _registry.Profiles.First();

    /// <inheritdoc />
    public IReadOnlyList<string> AvatarPalette => DefaultPalette;

    /// <inheritdoc />
    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        Directory.CreateDirectory(_rootDirectory);
        Directory.CreateDirectory(_profilesDirectory);

        if (File.Exists(_registryPath))
        {
            try
            {
                string json = File.ReadAllText(_registryPath);
                ProfileRegistry? loaded = JsonSerializer.Deserialize(json, ProfileJsonContext.Default.ProfileRegistry);
                if (loaded is { Profiles.Count: > 0 })
                {
                    _registry = loaded;
                }
            }
            catch
            {
                // Fall through to default initialization if registry file is corrupted.
            }
        }

        if (_registry.Profiles.Count == 0)
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            var defaultProfile = new UserProfile
            {
                Id = "default",
                Name = "Default",
                AvatarColorHex = DefaultPalette[0],
                CreatedAtUtc = now,
                LastUsedAtUtc = now
            };

            _registry = new ProfileRegistry
            {
                ActiveProfileId = defaultProfile.Id,
                Profiles = [defaultProfile]
            };

            MigrateLegacyDatabaseIfPresent(defaultProfile.Id);
            SaveRegistry();
        }
        else if (_registry.Profiles.All(p => !string.Equals(p.Id, _registry.ActiveProfileId, StringComparison.OrdinalIgnoreCase)))
        {
            _registry.ActiveProfileId = _registry.Profiles[0].Id;
            SaveRegistry();
        }

        _initialized = true;
    }

    /// <inheritdoc />
    public string GetDatabasePath(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            throw new ArgumentException("Profile ID cannot be empty.", nameof(profileId));
        }

        string safeId = SanitizeFileSegment(profileId);
        return Path.Combine(_profilesDirectory, $"{safeId}.db");
    }

    /// <inheritdoc />
    public ITypeForgeRepository CreateRepositoryForActiveProfile()
    {
        EnsureInitialized();
        return new SqliteTypeForgeRepository(GetDatabasePath(ActiveProfile.Id));
    }

    /// <inheritdoc />
    public UserProfile CreateProfile(string name, string? avatarColorHex = null)
    {
        EnsureInitialized();
        string normalizedName = NormalizeName(name);
        string id = GenerateUniqueId(normalizedName);
        string color = string.IsNullOrWhiteSpace(avatarColorHex)
            ? DefaultPalette[_registry.Profiles.Count % DefaultPalette.Length]
            : avatarColorHex;
        DateTimeOffset now = _timeProvider.GetUtcNow();

        var profile = new UserProfile
        {
            Id = id,
            Name = normalizedName,
            AvatarColorHex = color,
            CreatedAtUtc = now,
            LastUsedAtUtc = now
        };

        _registry.Profiles.Add(profile);
        SaveRegistry();
        return profile;
    }

    /// <inheritdoc />
    public void RenameProfile(string profileId, string newName)
    {
        EnsureInitialized();
        UserProfile profile = FindProfile(profileId);
        profile.Name = NormalizeName(newName);
        SaveRegistry();
    }

    /// <inheritdoc />
    public void SetActiveProfile(string profileId)
    {
        EnsureInitialized();
        UserProfile profile = FindProfile(profileId);
        profile.LastUsedAtUtc = _timeProvider.GetUtcNow();
        _registry.ActiveProfileId = profile.Id;
        SaveRegistry();
    }

    /// <inheritdoc />
    public void DeleteProfile(string profileId)
    {
        EnsureInitialized();
        if (_registry.Profiles.Count <= 1)
        {
            throw new InvalidOperationException("Cannot delete the last remaining profile.");
        }

        UserProfile profile = FindProfile(profileId);
        _registry.Profiles.Remove(profile);

        if (string.Equals(_registry.ActiveProfileId, profile.Id, StringComparison.OrdinalIgnoreCase))
        {
            _registry.ActiveProfileId = _registry.Profiles[0].Id;
        }

        SaveRegistry();

        SqliteConnection.ClearAllPools();
        string dbPath = GetDatabasePath(profile.Id);
        TryDeleteFile(dbPath);
        TryDeleteFile(dbPath + "-wal");
        TryDeleteFile(dbPath + "-shm");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileRankingEntry>> BuildRankingsAsync(
        int totalCourseExercises,
        CancellationToken ct = default)
    {
        EnsureInitialized();
        var raw = new List<(UserProfile Profile, int SessionCount, double AvgWpm, double BestWpm, double AvgAcc, double CoursePct, int Streak)>();

        foreach (UserProfile profile in _registry.Profiles)
        {
            ct.ThrowIfCancellationRequested();
            string dbPath = GetDatabasePath(profile.Id);
            if (!File.Exists(dbPath))
            {
                raw.Add((profile, 0, 0.0, 0.0, 0.0, 0.0, 0));
                continue;
            }

            try
            {
                var repo = new SqliteTypeForgeRepository(dbPath);
                await repo.InitializeAsync(ct).ConfigureAwait(false);

                IReadOnlyList<SessionResult> sessions = await repo.GetRecentSessionsAsync(500, ct).ConfigureAwait(false);
                IReadOnlyDictionary<string, LessonProgressRecord> exercises = await repo.LoadExerciseProgressAsync(ct).ConfigureAwait(false);
                IReadOnlyList<DateOnly> practiceDates = await repo.GetPracticeDatesAsync(ct).ConfigureAwait(false);

                int count = sessions.Count;
                double avgWpm = count == 0 ? 0.0 : Math.Round(sessions.Average(s => s.NetWpm), 1);
                double bestWpm = count == 0 ? 0.0 : Math.Round(sessions.Max(s => s.NetWpm), 1);
                double avgAcc = count == 0 ? 0.0 : Math.Round(sessions.Average(s => s.AccuracyPercentage), 1);
                int completedExercises = exercises.Values.Count(e => e.IsCompleted);
                double coursePct = totalCourseExercises <= 0
                    ? 0.0
                    : Math.Round(Math.Clamp((double)completedExercises / totalCourseExercises * 100.0, 0.0, 100.0), 1);
                int streak = StreakCalculator.CalculateConsecutiveStreak(practiceDates);

                raw.Add((profile, count, avgWpm, bestWpm, avgAcc, coursePct, streak));
            }
            catch
            {
                raw.Add((profile, 0, 0.0, 0.0, 0.0, 0.0, 0));
            }
        }

        List<ProfileRankingEntry> ranked = raw
            .OrderByDescending(x => x.SessionCount > 0 ? 1 : 0)
            .ThenByDescending(x => x.AvgWpm)
            .ThenByDescending(x => x.AvgAcc)
            .ThenByDescending(x => x.BestWpm)
            .ThenBy(x => x.Profile.Name, StringComparer.OrdinalIgnoreCase)
            .Select((x, index) => new ProfileRankingEntry(
                index + 1,
                x.Profile,
                x.SessionCount,
                x.AvgWpm,
                x.BestWpm,
                x.AvgAcc,
                x.CoursePct,
                x.Streak))
            .ToList();

        return ranked;
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }

    private UserProfile FindProfile(string profileId)
    {
        UserProfile? profile = _registry.Profiles.FirstOrDefault(
            p => string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase));

        return profile ?? throw new KeyNotFoundException($"Profile '{profileId}' was not found.");
    }

    private void MigrateLegacyDatabaseIfPresent(string defaultProfileId)
    {
        string legacyPath = Path.Combine(_rootDirectory, "typeforge.db");
        string targetPath = GetDatabasePath(defaultProfileId);

        if (File.Exists(legacyPath) && !File.Exists(targetPath))
        {
            SqliteConnection.ClearAllPools();
            File.Move(legacyPath, targetPath);

            string legacyWal = legacyPath + "-wal";
            string legacyShm = legacyPath + "-shm";
            if (File.Exists(legacyWal))
            {
                File.Move(legacyWal, targetPath + "-wal", overwrite: true);
            }

            if (File.Exists(legacyShm))
            {
                File.Move(legacyShm, targetPath + "-shm", overwrite: true);
            }
        }
    }

    private void SaveRegistry()
    {
        Directory.CreateDirectory(_rootDirectory);
        string json = JsonSerializer.Serialize(_registry, ProfileJsonContext.Default.ProfileRegistry);
        File.WriteAllText(_registryPath, json);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Profile name cannot be empty.", nameof(name));
        }

        string trimmed = name.Trim();
        return trimmed.Length > 32 ? trimmed[..32].TrimEnd() : trimmed;
    }

    private string GenerateUniqueId(string name)
    {
        string slug = new string(name.ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_')
            .ToArray());

        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "user";
        }

        if (_registry.Profiles.All(p => !string.Equals(p.Id, slug, StringComparison.OrdinalIgnoreCase)))
        {
            return slug;
        }

        string suffix = Guid.NewGuid().ToString("N")[..6];
        return $"{slug}-{suffix}";
    }

    private static string SanitizeFileSegment(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string cleaned = new string(value.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "profile" : cleaned;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
