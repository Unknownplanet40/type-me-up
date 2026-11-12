using TypeForge.Core.Models;

namespace TypeForge.Core.Engine;

/// <summary>
/// Action kind recommended alongside a post-session coaching insight.
/// </summary>
public enum InsightActionKind
{
    /// <summary>Advance to the next drill or lesson.</summary>
    NextDrill = 0,

    /// <summary>Launch a targeted weak-key practice drill.</summary>
    ReviewWeakKeys = 1,

    /// <summary>Launch a 2-minute drill targeting the user's slowest letter pairs.</summary>
    DrillSlowestBigrams = 2,

    /// <summary>Repeat the current drill to solidify accuracy.</summary>
    RepeatDrill = 3
}

/// <summary>
/// Post-session coaching insight pairing one primary observation with one recommended next action.
/// </summary>
/// <param name="PrimaryInsight">Concise coaching headline summarizing what stood out in the session.</param>
/// <param name="ActionLabel">Button label for the recommended next step.</param>
/// <param name="RecommendedAction">Action kind to execute when the user activates the recommendation.</param>
/// <param name="IsNeutralFallback">True when session history or sample counts were too sparse for comparative ranking.</param>
public sealed record SessionInsight(
    string PrimaryInsight,
    string ActionLabel,
    InsightActionKind RecommendedAction,
    bool IsNeutralFallback);

/// <summary>
/// Pure post-session diagnostic engine that surfaces one primary insight and one action on the Results screen.
/// </summary>
public static class InsightEngine
{
    /// <summary>
    /// Evaluates a completed <paramref name="currentSession"/> against recent history and aggregate key/bigram statistics.
    /// </summary>
    /// <param name="currentSession">The session just completed.</param>
    /// <param name="previousSessions">Prior recorded sessions (excluding or including current; filtered by SessionId).</param>
    /// <param name="aggregateKeys">Aggregate per-key metrics.</param>
    /// <param name="aggregateBigrams">Aggregate per-bigram metrics.</param>
    /// <returns>A single <see cref="SessionInsight"/> with primary message and recommended action.</returns>
    public static SessionInsight Evaluate(
        SessionResult currentSession,
        IReadOnlyList<SessionResult>? previousSessions = null,
        IReadOnlyDictionary<char, KeyMetric>? aggregateKeys = null,
        IReadOnlyDictionary<string, BigramMetric>? aggregateBigrams = null)
    {
        ArgumentNullException.ThrowIfNull(currentSession);

        // 1. High-error run (< 90% accuracy): coach accuracy first and recommend repeating
        if (currentSession.TotalKeystrokes >= 20 && currentSession.AccuracyPercentage < 90.0)
        {
            KeyMetric? topSessionErrorKey = currentSession.KeyBreakdown.Values
                .Where(k => k.IncorrectKeystrokes > 0)
                .OrderByDescending(k => k.IncorrectKeystrokes)
                .ThenByDescending(k => k.ErrorRatePercentage)
                .FirstOrDefault();

            if (topSessionErrorKey is not null)
            {
                return new SessionInsight(
                    $"Accuracy dipped to {currentSession.AccuracyPercentage:0.0}% with '{topSessionErrorKey.Character}' causing {topSessionErrorKey.IncorrectKeystrokes} mistakes — slow down slightly for clean strokes.",
                    "Review Weak Keys",
                    InsightActionKind.ReviewWeakKeys,
                    IsNeutralFallback: false);
            }

            return new SessionInsight(
                $"Accuracy landed at {currentSession.AccuracyPercentage:0.0}% — aim for 95%+ precision before pushing raw speed.",
                "Repeat Drill",
                InsightActionKind.RepeatDrill,
                IsNeutralFallback: false);
        }

        // 2. Check ranked bigram bottleneck from aggregate or current session
        if (aggregateBigrams is { Count: > 0 })
        {
            IReadOnlyList<RankedWeakness<string>> weakBigrams = WeaknessScorer.RankBigrams(
                aggregateBigrams.Values,
                minSamples: WeaknessScorer.DefaultMinSamples)
                .Take(3)
                .ToArray();

            if (weakBigrams.Count > 0 && (weakBigrams[0].ErrorRatePercentage >= 6.0 || weakBigrams[0].AverageLatencyMs >= 260.0))
            {
                RankedWeakness<string> topPair = weakBigrams[0];
                return new SessionInsight(
                    $"Letter pair '{topPair.Target}' averages {topPair.AverageLatencyMs:0} ms ({topPair.ErrorRatePercentage:0.#}% err) — a 2-minute pair drill will smooth out your rhythm.",
                    "Drill Slowest Pairs",
                    InsightActionKind.DrillSlowestBigrams,
                    IsNeutralFallback: false);
            }
        }

        // 3. Check ranked single-key weakness from aggregate stats
        if (aggregateKeys is { Count: > 0 })
        {
            IReadOnlyList<RankedWeakness<char>> weakKeys = WeaknessScorer.RankKeys(
                aggregateKeys.Values,
                minSamples: WeaknessScorer.DefaultMinSamples)
                .Take(3)
                .ToArray();

            if (weakKeys.Count > 0 && weakKeys[0].ErrorRatePercentage >= 5.0)
            {
                RankedWeakness<char> topKey = weakKeys[0];
                return new SessionInsight(
                    $"Key '{topKey.Target}' has a {topKey.ErrorRatePercentage:0.#}% error rate ({topKey.AverageLatencyMs:0} ms avg) — targeted key practice will lock in muscle memory.",
                    "Review Weak Keys",
                    InsightActionKind.ReviewWeakKeys,
                    IsNeutralFallback: false);
            }
        }

        // 4. Compare speed/accuracy against recent session baseline when >= 2 prior sessions exist
        List<SessionResult> history = previousSessions?
            .Where(s => s.SessionId != currentSession.SessionId && s.TotalKeystrokes >= 15)
            .Take(10)
            .ToList() ?? [];

        if (history.Count >= 2)
        {
            double recentAvgWpm = history.Average(s => s.NetWpm);
            double deltaWpm = Math.Round(currentSession.NetWpm - recentAvgWpm, 1);

            if (deltaWpm >= 2.0 && currentSession.AccuracyPercentage >= 95.0)
            {
                return new SessionInsight(
                    $"Net speed climbed +{deltaWpm:0.0} WPM above your recent {recentAvgWpm:0.0} WPM average while holding {currentSession.AccuracyPercentage:0.0}% accuracy.",
                    "Next Drill",
                    InsightActionKind.NextDrill,
                    IsNeutralFallback: false);
            }

            return new SessionInsight(
                $"Steady pace at {currentSession.NetWpm:0.0} WPM and {currentSession.AccuracyPercentage:0.0}% accuracy compared to your {recentAvgWpm:0.0} WPM recent average.",
                "Next Drill",
                InsightActionKind.NextDrill,
                IsNeutralFallback: false);
        }

        // 5. Sparse-data neutral summary fallback
        return new SessionInsight(
            $"Baseline recorded at {currentSession.NetWpm:0.0} WPM and {currentSession.AccuracyPercentage:0.0}% accuracy — complete a few more sessions to unlock personalized trend insights.",
            "Next Drill",
            InsightActionKind.NextDrill,
            IsNeutralFallback: true);
    }
}
