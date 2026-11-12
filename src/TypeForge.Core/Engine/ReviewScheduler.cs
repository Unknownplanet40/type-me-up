using TypeForge.Core.Models;

namespace TypeForge.Core.Engine;

/// <summary>
/// SM-2-lite spaced repetition scheduler for per-key mastery maintenance.
/// </summary>
public sealed class ReviewScheduler
{
    /// <summary>Minimum allowed SM-2 ease factor.</summary>
    public const double MinEaseFactor = 1.3;

    /// <summary>Default initial SM-2 ease factor.</summary>
    public const double DefaultEaseFactor = 2.5;

    /// <summary>Maximum review interval in days.</summary>
    public const double MaxIntervalDays = 60.0;

    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewScheduler"/> class with an injectable clock.
    /// </summary>
    public ReviewScheduler(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Updates the spaced repetition schedule for a single character based on session performance.
    /// Keys that meet accuracy and latency targets grow their interval; failed keys reset to a short interval.
    /// </summary>
    public KeyReviewScheduleRecord EvaluateKeySession(
        KeyReviewScheduleRecord? existing,
        char character,
        KeyMetric sessionMetric,
        double targetAccuracyPercentage = 95.0,
        double maxLatencyMs = 500.0)
    {
        ArgumentNullException.ThrowIfNull(sessionMetric);
        char normalizedChar = char.ToUpperInvariant(character);
        DateTimeOffset now = _timeProvider.GetUtcNow();

        double prevInterval = existing?.IntervalDays ?? 0.0;
        double prevEase = existing?.EaseFactor ?? DefaultEaseFactor;

        bool mastered = sessionMetric.TotalKeystrokes > 0
            && sessionMetric.AccuracyPercentage >= targetAccuracyPercentage
            && (sessionMetric.AverageLatencyMs <= 0.0 || sessionMetric.AverageLatencyMs <= maxLatencyMs);

        double nextInterval;
        double nextEase;

        if (mastered)
        {
            bool highPrecision = sessionMetric.AccuracyPercentage >= Math.Min(100.0, targetAccuracyPercentage + 3.0)
                && (sessionMetric.AverageLatencyMs <= 0.0 || sessionMetric.AverageLatencyMs <= maxLatencyMs * 0.8);

            nextEase = Math.Max(MinEaseFactor, Math.Round(prevEase + (highPrecision ? 0.15 : 0.05), 2));

            if (prevInterval < 1.0)
            {
                nextInterval = 1.0;
            }
            else if (prevInterval < 2.5)
            {
                nextInterval = 3.0;
            }
            else
            {
                nextInterval = Math.Min(MaxIntervalDays, Math.Round(prevInterval * nextEase, 2));
            }
        }
        else
        {
            nextEase = Math.Max(MinEaseFactor, Math.Round(prevEase - 0.20, 2));
            nextInterval = 0.5;
        }

        DateTimeOffset dueAt = now.AddDays(nextInterval);
        return new KeyReviewScheduleRecord(
            Character: normalizedChar,
            LastReviewedAt: now,
            IntervalDays: nextInterval,
            EaseFactor: nextEase,
            DueAt: dueAt);
    }

    /// <summary>
    /// Returns all scheduled keys whose <see cref="KeyReviewScheduleRecord.DueAt"/> is at or before <paramref name="asOf"/> (or the current clock time).
    /// </summary>
    public IReadOnlyList<char> GetDueKeys(
        IEnumerable<KeyReviewScheduleRecord> schedules,
        DateTimeOffset? asOf = null,
        IReadOnlySet<char>? unlockedLetters = null)
    {
        ArgumentNullException.ThrowIfNull(schedules);
        DateTimeOffset referenceTime = asOf ?? _timeProvider.GetUtcNow();

        HashSet<char>? allowedUpper = unlockedLetters?
            .Select(char.ToUpperInvariant)
            .ToHashSet();

        return schedules
            .Where(s => s.DueAt <= referenceTime)
            .Where(s => allowedUpper is null || allowedUpper.Contains(char.ToUpperInvariant(s.Character)))
            .OrderBy(s => s.DueAt)
            .ThenBy(s => char.ToUpperInvariant(s.Character))
            .Select(s => char.ToUpperInvariant(s.Character))
            .Distinct()
            .ToArray();
    }
}
