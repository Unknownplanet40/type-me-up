namespace TypeForge.Core.Engine;

/// <summary>
/// Provides mathematical calculations for typing speed, accuracy, and error metrics.
/// </summary>
public static class MetricsCalculator
{
    /// <summary>Standard average word length in characters used for WPM calculations.</summary>
    public const double CharactersPerWord = 5.0;

    /// <summary>Minimum elapsed duration required before non-zero WPM is reported.</summary>
    public static readonly TimeSpan MinimumValidElapsed = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Calculates gross words per minute from total typed characters and elapsed time.
    /// </summary>
    public static double CalculateGrossWpm(int totalTypedCharacters, TimeSpan elapsed)
    {
        if (totalTypedCharacters <= 0 || elapsed < MinimumValidElapsed)
        {
            return 0.0;
        }

        double gross = (totalTypedCharacters / CharactersPerWord) / elapsed.TotalMinutes;
        return Math.Max(0.0, Math.Round(gross, 2));
    }

    /// <summary>
    /// Calculates net words per minute by deducting uncorrected errors from gross output.
    /// </summary>
    public static double CalculateNetWpm(int totalTypedCharacters, int uncorrectedErrors, TimeSpan elapsed)
    {
        if (totalTypedCharacters <= 0 || elapsed < MinimumValidElapsed)
        {
            return 0.0;
        }

        double net = ((totalTypedCharacters / CharactersPerWord) - Math.Max(0, uncorrectedErrors)) / elapsed.TotalMinutes;
        return Math.Max(0.0, Math.Round(net, 2));
    }

    /// <summary>
    /// Calculates keystroke accuracy percentage in the range 0 to 100.
    /// </summary>
    public static double CalculateAccuracyPercentage(int correctKeystrokes, int totalKeystrokes)
    {
        if (totalKeystrokes <= 0)
        {
            return 100.0;
        }

        double accuracy = (double)Math.Max(0, correctKeystrokes) / totalKeystrokes * 100.0;
        return Math.Round(Math.Clamp(accuracy, 0.0, 100.0), 2);
    }

    /// <summary>
    /// Calculates typing rhythm consistency percentage (0–100) from inter-keystroke intervals.
    /// Higher values indicate steadier cadence; pauses above 2000 ms are excluded as outliers.
    /// </summary>
    public static double CalculateConsistencyPercentage(IReadOnlyList<double> keystrokeLatenciesMs)
    {
        ArgumentNullException.ThrowIfNull(keystrokeLatenciesMs);

        var samples = new List<double>(keystrokeLatenciesMs.Count);
        foreach (double ms in keystrokeLatenciesMs)
        {
            if (ms is > 0.0 and <= 2000.0)
            {
                samples.Add(ms);
            }
        }

        if (samples.Count <= 1)
        {
            return 100.0;
        }

        double mean = samples.Average();
        if (mean <= 0.0)
        {
            return 100.0;
        }

        double variance = 0.0;
        foreach (double ms in samples)
        {
            double diff = ms - mean;
            variance += diff * diff;
        }

        double stdDev = Math.Sqrt(variance / samples.Count);
        double cv = stdDev / mean;
        double consistency = (1.0 - Math.Min(1.0, cv)) * 100.0;
        return Math.Round(Math.Clamp(consistency, 0.0, 100.0), 1);
    }
}
