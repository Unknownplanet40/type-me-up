using TypeForge.Core.Models;

namespace TypeForge.Core.Engine;

/// <summary>
/// Tracks letter-pair (bigram) transition latency and error counts using an exponential moving average and pause cutoff.
/// </summary>
public sealed class BigramTracker
{
    private sealed class BigramAccumulator
    {
        public int TotalCount { get; set; }
        public int ErrorCount { get; set; }
        public double AverageLatencyMs { get; set; }
        public bool HasLatencySample { get; set; }

        public BigramMetric ToMetric(string bigram) =>
            new(bigram, TotalCount, ErrorCount, Math.Round(AverageLatencyMs, 2));
    }

    private readonly Dictionary<string, BigramAccumulator> _accumulators = new(StringComparer.Ordinal);
    private bool _hasValidPrevious;
    private char _previousUpperChar;

    /// <summary>Gets the EMA smoothing factor in <c>(0, 1]</c>.</summary>
    public double Alpha { get; }

    /// <summary>Gets the maximum transition pause in milliseconds before a sample is treated as an idle break outlier.</summary>
    public double MaxPauseCutoffMs { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BigramTracker"/> class.
    /// </summary>
    public BigramTracker(
        double alpha = 0.25,
        double maxPauseCutoffMs = 2000.0,
        IReadOnlyDictionary<string, BigramMetric>? initialMetrics = null)
    {
        if (alpha <= 0.0 || alpha > 1.0 || double.IsNaN(alpha))
        {
            throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be in (0, 1].");
        }

        if (maxPauseCutoffMs <= 0.0 || double.IsNaN(maxPauseCutoffMs))
        {
            throw new ArgumentOutOfRangeException(nameof(maxPauseCutoffMs), "MaxPauseCutoffMs must be positive.");
        }

        Alpha = alpha;
        MaxPauseCutoffMs = maxPauseCutoffMs;

        if (initialMetrics is not null)
        {
            foreach (KeyValuePair<string, BigramMetric> kvp in initialMetrics)
            {
                if (kvp.Key.Length == 2 && char.IsLetter(kvp.Key[0]) && char.IsLetter(kvp.Key[1]))
                {
                    string key = kvp.Key.ToUpperInvariant();
                    _accumulators[key] = new BigramAccumulator
                    {
                        TotalCount = Math.Max(0, kvp.Value.TotalCount),
                        ErrorCount = Math.Clamp(kvp.Value.ErrorCount, 0, Math.Max(0, kvp.Value.TotalCount)),
                        AverageLatencyMs = Math.Max(0.0, kvp.Value.AverageLatencyMs),
                        HasLatencySample = kvp.Value.AverageLatencyMs > 0.0
                    };
                }
            }
        }
    }

    /// <summary>
    /// Records a single evaluated keystroke against the expected target character.
    /// Latency is incorporated only when both letters in the pair were typed correctly in sequence and within <see cref="MaxPauseCutoffMs"/>.
    /// </summary>
    public void RecordKeystroke(char expectedChar, bool isCorrect, double? latencyMs)
    {
        if (!char.IsLetter(expectedChar))
        {
            _hasValidPrevious = false;
            return;
        }

        char upperCurr = char.ToUpperInvariant(expectedChar);

        if (_hasValidPrevious)
        {
            string bigram = string.Concat(_previousUpperChar, upperCurr);
            if (!_accumulators.TryGetValue(bigram, out BigramAccumulator? acc))
            {
                acc = new BigramAccumulator();
                _accumulators[bigram] = acc;
            }

            acc.TotalCount++;
            if (isCorrect)
            {
                if (latencyMs is > 0.0 && latencyMs.Value <= MaxPauseCutoffMs)
                {
                    if (!acc.HasLatencySample)
                    {
                        acc.AverageLatencyMs = latencyMs.Value;
                        acc.HasLatencySample = true;
                    }
                    else
                    {
                        acc.AverageLatencyMs = (Alpha * latencyMs.Value) + ((1.0 - Alpha) * acc.AverageLatencyMs);
                    }
                }

                _previousUpperChar = upperCurr;
                _hasValidPrevious = true;
            }
            else
            {
                acc.ErrorCount++;
                _hasValidPrevious = false;
            }
        }
        else
        {
            if (isCorrect)
            {
                _previousUpperChar = upperCurr;
                _hasValidPrevious = true;
            }
            else
            {
                _hasValidPrevious = false;
            }
        }
    }

    /// <summary>
    /// Clears the active predecessor state when the user presses backspace so retyped characters do not form synthetic transitions.
    /// </summary>
    public void RecordBackspace()
    {
        _hasValidPrevious = false;
    }

    /// <summary>
    /// Resets transient sequence state at the start or end of a session while keeping accumulated metrics.
    /// </summary>
    public void ResetSequence()
    {
        _hasValidPrevious = false;
    }

    /// <summary>
    /// Clears all accumulated bigram metrics and sequence state.
    /// </summary>
    public void Clear()
    {
        _accumulators.Clear();
        _hasValidPrevious = false;
    }

    /// <summary>
    /// Exports a snapshot of all recorded bigram statistics.
    /// </summary>
    public IReadOnlyDictionary<string, BigramMetric> GetMetrics()
    {
        var dict = new Dictionary<string, BigramMetric>(_accumulators.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, BigramAccumulator> kvp in _accumulators)
        {
            dict[kvp.Key] = kvp.Value.ToMetric(kvp.Key);
        }

        return dict;
    }
}
