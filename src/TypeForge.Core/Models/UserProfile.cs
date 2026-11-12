namespace TypeForge.Core.Models;

/// <summary>
/// Represents an isolated local user profile with its own SQLite database.
/// </summary>
public sealed class UserProfile
{
    /// <summary>Unique profile identifier and SQLite filename stem.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name of the profile.</summary>
    public string Name { get; set; } = "Default";

    /// <summary>Hex color string used for the profile avatar badge.</summary>
    public string AvatarColorHex { get; set; } = "#A78BFA";

    /// <summary>UTC timestamp when the profile was created.</summary>
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp when the profile was last activated.</summary>
    public DateTimeOffset LastUsedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>One- or two-letter uppercase initials derived from <see cref="Name"/>.</summary>
    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return "?";
            }

            string[] parts = Name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2
                ? $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}"
                : $"{char.ToUpperInvariant(parts[0][0])}";
        }
    }
}

/// <summary>
/// Persisted registry of local user profiles and the active profile ID.
/// </summary>
public sealed class ProfileRegistry
{
    /// <summary>Identifier of the currently selected profile.</summary>
    public string ActiveProfileId { get; set; } = "default";

    /// <summary>All registered local profiles.</summary>
    public List<UserProfile> Profiles { get; set; } = [];
}

/// <summary>
/// Aggregated leaderboard entry comparing a profile's typing performance against peers on the same device.
/// </summary>
public sealed record ProfileRankingEntry(
    int Rank,
    UserProfile Profile,
    int SessionCount,
    double AverageNetWpm,
    double BestNetWpm,
    double AverageAccuracyPercentage,
    double CourseCompletionPercentage,
    int StreakDays);
