using TypeForge.Core.Models;

namespace TypeForge.Core.Services;

/// <summary>
/// Difficulty band classifying a character's error and latency performance.
/// </summary>
public enum KeyDifficultyBand
{
    /// <summary>Key meets accuracy and speed expectations.</summary>
    Ok = 0,

    /// <summary>Key shows moderate error rate or elevated latency.</summary>
    Difficult = 1,

    /// <summary>Key exhibits high error rate or severe latency requiring targeted drill focus.</summary>
    Problematic = 2
}

/// <summary>
/// Ranked key difficulty item used by Difficult Keys charts and heatmaps.
/// </summary>
public sealed record DifficultKeyStat(
    char Character,
    double ErrorRatePercentage,
    double AverageLatencyMs,
    int TotalKeystrokes,
    double DifficultyScore,
    KeyDifficultyBand Band);

/// <summary>
/// Provides chart-ready benchmark classifications and difficult-key rankings.
/// </summary>
public static class StatisticsAggregator
{
    /// <summary>
    /// Classifies key difficulty severity from error rate and mean latency.
    /// </summary>
    public static KeyDifficultyBand ClassifyKeyDifficulty(KeyMetric metric)
    {
        ArgumentNullException.ThrowIfNull(metric);

        if (metric.ErrorRatePercentage >= 10.0 || metric.AverageLatencyMs >= 550.0)
        {
            return KeyDifficultyBand.Problematic;
        }

        if (metric.ErrorRatePercentage >= 5.0 || metric.AverageLatencyMs >= 400.0)
        {
            return KeyDifficultyBand.Difficult;
        }

        return KeyDifficultyBand.Ok;
    }

    /// <summary>
    /// Returns visible character metrics sorted from hardest to easiest, filtering out ultra-low sample noise.
    /// </summary>
    public static IReadOnlyList<DifficultKeyStat> GetDifficultKeys(
        IReadOnlyDictionary<char, KeyMetric> metrics,
        int minSampleCount = 5,
        int maxCount = 14)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        return metrics.Values
            .Where(m => m.TotalKeystrokes >= minSampleCount && !char.IsWhiteSpace(m.Character))
            .Select(m =>
            {
                double normalizedLatency = Math.Clamp(m.AverageLatencyMs / 650.0, 0.0, 1.5);
                double score = Math.Round((m.ErrorRatePercentage * 3.0) + (normalizedLatency * 25.0), 2);
                return new DifficultKeyStat(
                    char.ToLowerInvariant(m.Character),
                    m.ErrorRatePercentage,
                    m.AverageLatencyMs,
                    m.TotalKeystrokes,
                    score,
                    ClassifyKeyDifficulty(m));
            })
            .OrderByDescending(stat => stat.DifficultyScore)
            .ThenByDescending(stat => stat.ErrorRatePercentage)
            .Take(Math.Max(1, maxCount))
            .ToArray();
    }
}

/// <summary>
/// Calculates consecutive calendar-day practice streaks from local history timestamps.
/// </summary>
public static class StreakCalculator
{
    /// <summary>
    /// Computes consecutive days practiced ending on <paramref name="today"/> or yesterday.
    /// </summary>
    public static int CalculateConsecutiveStreak(IReadOnlyList<DateOnly> practiceDatesDescending, DateOnly? today = null)
    {
        ArgumentNullException.ThrowIfNull(practiceDatesDescending);
        if (practiceDatesDescending.Count == 0)
        {
            return 0;
        }

        DateOnly referenceToday = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly latest = practiceDatesDescending.Max();
        if (latest < referenceToday.AddDays(-1))
        {
            return 0;
        }

        var dateSet = practiceDatesDescending.ToHashSet();
        int streak = 0;
        DateOnly cursor = latest;
        while (dateSet.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }

        return streak;
    }
}
