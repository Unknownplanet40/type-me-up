using TypeForge.Core.Models;

namespace TypeForge.Core.Engine;

/// <summary>
/// Represents a ranked key or bigram weakness item with its blended weakness score and underlying metrics.
/// </summary>
public sealed record RankedWeakness<T>(
    T Target,
    double Score,
    double ErrorRatePercentage,
    double AverageLatencyMs,
    double LatencyZScore,
    int SampleCount);

/// <summary>
/// Pure scoring functions that rank keys and bigrams by a blend of error rate and latency z-score relative to the user's own baseline.
/// </summary>
public static class WeaknessScorer
{
    /// <summary>Default minimum sample threshold required before an item is eligible for weakness ranking.</summary>
    public const int DefaultMinSamples = 20;

    /// <summary>
    /// Ranks single-character keys from weakest (highest score) to strongest among keys meeting <paramref name="minSamples"/>.
    /// </summary>
    public static IReadOnlyList<RankedWeakness<char>> RankKeys(
        IEnumerable<KeyMetric> metrics,
        int minSamples = DefaultMinSamples)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        int threshold = Math.Max(1, minSamples);

        List<KeyMetric> eligible = metrics
            .Where(m => !char.IsWhiteSpace(m.Character) && !char.IsControl(m.Character) && m.TotalKeystrokes >= threshold)
            .ToList();

        if (eligible.Count == 0)
        {
            return [];
        }

        (double meanLatency, double stdDevLatency) = ComputeLatencyStats(eligible.Select(m => m.AverageLatencyMs));

        return eligible
            .Select(m =>
            {
                double z = stdDevLatency <= 1e-6 ? 0.0 : (m.AverageLatencyMs - meanLatency) / stdDevLatency;
                double positiveZ = Math.Max(0.0, z);
                double score = Math.Round((0.65 * m.ErrorRatePercentage) + (0.35 * positiveZ * 20.0), 4);
                return new RankedWeakness<char>(
                    Target: char.ToUpperInvariant(m.Character),
                    Score: score,
                    ErrorRatePercentage: m.ErrorRatePercentage,
                    AverageLatencyMs: m.AverageLatencyMs,
                    LatencyZScore: Math.Round(z, 4),
                    SampleCount: m.TotalKeystrokes);
            })
            .Where(r => r.Score > 0.0 || r.ErrorRatePercentage > 0.0 || r.LatencyZScore > 0.0)
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.ErrorRatePercentage)
            .ThenByDescending(r => r.AverageLatencyMs)
            .ThenBy(r => r.Target)
            .ToArray();
    }

    /// <summary>
    /// Ranks two-character bigrams from weakest (highest score) to strongest among bigrams meeting <paramref name="minSamples"/>.
    /// </summary>
    public static IReadOnlyList<RankedWeakness<string>> RankBigrams(
        IEnumerable<BigramMetric> metrics,
        int minSamples = DefaultMinSamples)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        int threshold = Math.Max(1, minSamples);

        List<BigramMetric> eligible = metrics
            .Where(m => !string.IsNullOrWhiteSpace(m.Bigram) && m.Bigram.Length == 2 && m.TotalCount >= threshold)
            .ToList();

        if (eligible.Count == 0)
        {
            return [];
        }

        (double meanLatency, double stdDevLatency) = ComputeLatencyStats(eligible.Select(m => m.AverageLatencyMs));

        return eligible
            .Select(m =>
            {
                double z = stdDevLatency <= 1e-6 ? 0.0 : (m.AverageLatencyMs - meanLatency) / stdDevLatency;
                double positiveZ = Math.Max(0.0, z);
                double score = Math.Round((0.65 * m.ErrorRatePercentage) + (0.35 * positiveZ * 20.0), 4);
                return new RankedWeakness<string>(
                    Target: m.Bigram.ToUpperInvariant(),
                    Score: score,
                    ErrorRatePercentage: m.ErrorRatePercentage,
                    AverageLatencyMs: m.AverageLatencyMs,
                    LatencyZScore: Math.Round(z, 4),
                    SampleCount: m.TotalCount);
            })
            .Where(r => r.Score > 0.0 || r.ErrorRatePercentage > 0.0 || r.LatencyZScore > 0.0)
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.ErrorRatePercentage)
            .ThenByDescending(r => r.AverageLatencyMs)
            .ThenBy(r => r.Target, StringComparer.Ordinal)
            .ToArray();
    }

    private static (double Mean, double StdDev) ComputeLatencyStats(IEnumerable<double> values)
    {
        double[] arr = values.Where(v => v >= 0.0 && !double.IsNaN(v) && !double.IsInfinity(v)).ToArray();
        if (arr.Length == 0)
        {
            return (0.0, 0.0);
        }

        double mean = arr.Average();
        if (arr.Length == 1)
        {
            return (mean, 0.0);
        }

        double sumSq = 0.0;
        for (int i = 0; i < arr.Length; i++)
        {
            double diff = arr[i] - mean;
            sumSq += diff * diff;
        }

        double stdDev = Math.Sqrt(sumSq / arr.Length);
        return (mean, stdDev);
    }
}
