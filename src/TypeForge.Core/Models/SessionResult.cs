namespace TypeForge.Core.Models;

/// <summary>
/// Captures completed typing session performance and per-key breakdown metrics.
/// </summary>
public sealed class SessionResult
{
    /// <summary>Gets or sets the unique identifier for this session.</summary>
    public Guid SessionId { get; init; } = Guid.NewGuid();

    /// <summary>Gets or sets the training mode of the session.</summary>
    public SessionType SessionType { get; init; }

    /// <summary>Gets or sets the optional course lesson identifier associated with the session.</summary>
    public string? LessonId { get; init; }

    /// <summary>Gets or sets the UTC timestamp when the first keystroke occurred.</summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the active duration of the session.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Gets or sets the calculated net words per minute.</summary>
    public double NetWpm { get; init; }

    /// <summary>Gets or sets the calculated gross words per minute.</summary>
    public double GrossWpm { get; init; }

    /// <summary>Gets or sets the keystroke accuracy percentage (0–100).</summary>
    public double AccuracyPercentage { get; init; } = 100.0;

    /// <summary>Gets or sets the number of accurate keystrokes recorded.</summary>
    public int CorrectCharacters { get; init; }

    /// <summary>Gets or sets the total number of typed keystrokes (excluding backspaces).</summary>
    public int TotalKeystrokes { get; init; }

    /// <summary>Gets or sets the total number of recorded typing mistakes.</summary>
    public int Mistakes { get; init; }

    /// <summary>Gets or sets the remaining uncorrected errors at session end.</summary>
    public int UncorrectedErrors { get; init; }

    /// <summary>Gets or sets per-key performance metrics collected during the session.</summary>
    public IReadOnlyDictionary<char, KeyMetric> KeyBreakdown { get; init; } =
        new Dictionary<char, KeyMetric>();

    /// <summary>
    /// Validates all numeric invariants on this session result.
    /// </summary>
    public void Validate()
    {
        if (Duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Duration), "Session duration cannot be negative.");
        }

        if (NetWpm < 0 || double.IsNaN(NetWpm) || double.IsInfinity(NetWpm))
        {
            throw new ArgumentOutOfRangeException(nameof(NetWpm), "Net WPM must be finite and non-negative.");
        }

        if (GrossWpm < 0 || double.IsNaN(GrossWpm) || double.IsInfinity(GrossWpm))
        {
            throw new ArgumentOutOfRangeException(nameof(GrossWpm), "Gross WPM must be finite and non-negative.");
        }

        if (AccuracyPercentage < 0 || AccuracyPercentage > 100 || double.IsNaN(AccuracyPercentage))
        {
            throw new ArgumentOutOfRangeException(nameof(AccuracyPercentage), "Accuracy must be between 0 and 100.");
        }

        if (CorrectCharacters < 0 || TotalKeystrokes < 0 || Mistakes < 0 || UncorrectedErrors < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(TotalKeystrokes), "Keystroke counts cannot be negative.");
        }
    }
}
