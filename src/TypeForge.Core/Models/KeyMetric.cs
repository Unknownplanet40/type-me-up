namespace TypeForge.Core.Models;

/// <summary>
/// Represents aggregated typing accuracy and latency metrics for a single target key.
/// </summary>
public sealed class KeyMetric
{
    /// <summary>
    /// Initializes a new validated <see cref="KeyMetric"/> instance.
    /// </summary>
    /// <param name="character">The target key character.</param>
    /// <param name="totalKeystrokes">Total keystroke attempts targeting this key.</param>
    /// <param name="correctKeystrokes">Count of matching keystrokes.</param>
    /// <param name="incorrectKeystrokes">Count of mismatched keystrokes.</param>
    /// <param name="averageLatencyMs">Mean inter-key latency in milliseconds.</param>
    public KeyMetric(
        char character,
        int totalKeystrokes,
        int correctKeystrokes,
        int incorrectKeystrokes,
        double averageLatencyMs)
    {
        if (totalKeystrokes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalKeystrokes), "Total keystrokes cannot be negative.");
        }

        if (correctKeystrokes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(correctKeystrokes), "Correct keystrokes cannot be negative.");
        }

        if (incorrectKeystrokes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(incorrectKeystrokes), "Incorrect keystrokes cannot be negative.");
        }

        if (correctKeystrokes + incorrectKeystrokes > totalKeystrokes)
        {
            throw new ArgumentOutOfRangeException(nameof(totalKeystrokes), "Correct and incorrect keystrokes cannot exceed total keystrokes.");
        }

        if (averageLatencyMs < 0 || double.IsNaN(averageLatencyMs) || double.IsInfinity(averageLatencyMs))
        {
            throw new ArgumentOutOfRangeException(nameof(averageLatencyMs), "Average latency must be a finite non-negative number.");
        }

        Character = character;
        TotalKeystrokes = totalKeystrokes;
        CorrectKeystrokes = correctKeystrokes;
        IncorrectKeystrokes = incorrectKeystrokes;
        AverageLatencyMs = averageLatencyMs;
    }

    /// <summary>Gets the target key character.</summary>
    public char Character { get; }

    /// <summary>Gets total keystroke attempts recorded for this key.</summary>
    public int TotalKeystrokes { get; }

    /// <summary>Gets the number of accurate keystrokes recorded for this key.</summary>
    public int CorrectKeystrokes { get; }

    /// <summary>Gets the number of mistaken keystrokes recorded for this key.</summary>
    public int IncorrectKeystrokes { get; }

    /// <summary>Gets the average typing latency in milliseconds.</summary>
    public double AverageLatencyMs { get; }

    /// <summary>Gets the percentage of correct keystrokes (0–100).</summary>
    public double AccuracyPercentage =>
        TotalKeystrokes == 0
            ? 100.0
            : Math.Round((double)CorrectKeystrokes / TotalKeystrokes * 100.0, 2);

    /// <summary>Gets the percentage of mistaken keystrokes (0–100).</summary>
    public double ErrorRatePercentage =>
        TotalKeystrokes == 0
            ? 0.0
            : Math.Round((double)IncorrectKeystrokes / TotalKeystrokes * 100.0, 2);
}
