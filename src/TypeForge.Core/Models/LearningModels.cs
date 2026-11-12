namespace TypeForge.Core.Models;

/// <summary>
/// Aggregated performance metrics for a two-character bigram transition.
/// </summary>
public sealed record BigramMetric(
    string Bigram,
    int TotalCount,
    int ErrorCount,
    double AverageLatencyMs)
{
    /// <summary>Gets the percentage of transitions on this bigram that resulted in an error (0–100).</summary>
    public double ErrorRatePercentage =>
        TotalCount <= 0 ? 0.0 : Math.Round((double)ErrorCount / TotalCount * 100.0, 2);

    /// <summary>Gets the accuracy percentage on this bigram (0–100).</summary>
    public double AccuracyPercentage =>
        TotalCount <= 0 ? 100.0 : Math.Round((double)(TotalCount - ErrorCount) / TotalCount * 100.0, 2);

    /// <summary>Validates invariants on the bigram record.</summary>
    public void Validate()
    {
        if (string.IsNullOrEmpty(Bigram) || Bigram.Length != 2)
        {
            throw new ArgumentException("Bigram must be exactly 2 characters.", nameof(Bigram));
        }

        if (TotalCount < 0 || ErrorCount < 0 || ErrorCount > TotalCount)
        {
            throw new ArgumentOutOfRangeException(nameof(TotalCount), "Bigram counts must be non-negative with ErrorCount <= TotalCount.");
        }

        if (AverageLatencyMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(AverageLatencyMs), "Average latency cannot be negative.");
        }
    }
}

/// <summary>
/// Persisted SM-2-lite spaced repetition schedule state for a single character key.
/// </summary>
public sealed record KeyReviewScheduleRecord(
    char Character,
    DateTimeOffset? LastReviewedAt,
    double IntervalDays,
    double EaseFactor,
    DateTimeOffset DueAt)
{
    /// <summary>Validates invariants on the review schedule record.</summary>
    public void Validate()
    {
        if (char.IsControl(Character))
        {
            throw new ArgumentException("Review character cannot be a control character.", nameof(Character));
        }

        if (IntervalDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(IntervalDays), "IntervalDays cannot be negative.");
        }

        if (EaseFactor < 1.3)
        {
            throw new ArgumentOutOfRangeException(nameof(EaseFactor), "EaseFactor must be at least 1.3.");
        }
    }
}

/// <summary>
/// Persisted result of a 60-second placement test evaluation.
/// </summary>
public sealed record PlacementResultRecord(
    string PlacementId,
    DateTimeOffset TakenAt,
    double NetWpm,
    double AccuracyPercentage,
    string RecommendedTrack)
{
    /// <summary>Validates invariants on the placement result record.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PlacementId))
        {
            throw new ArgumentException("PlacementId cannot be empty.", nameof(PlacementId));
        }

        if (NetWpm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(NetWpm), "NetWpm cannot be negative.");
        }

        if (AccuracyPercentage < 0.0 || AccuracyPercentage > 100.0)
        {
            throw new ArgumentOutOfRangeException(nameof(AccuracyPercentage), "Accuracy must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(RecommendedTrack))
        {
            throw new ArgumentException("RecommendedTrack cannot be empty.", nameof(RecommendedTrack));
        }
    }
}
