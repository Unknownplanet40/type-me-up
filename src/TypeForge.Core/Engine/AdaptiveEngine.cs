using System.Text;
using TypeForge.Core.Models;

namespace TypeForge.Core.Engine;

/// <summary>
/// Represents the unlock state and ordered stage index of a single letter in adaptive practice.
/// </summary>
/// <param name="Character">Uppercase letter A–Z.</param>
/// <param name="IsUnlocked">Whether the letter is currently unlocked for drills.</param>
/// <param name="StageIndex">1-based unlock stage (1–13).</param>
public sealed record AdaptiveKeyState(char Character, bool IsUnlocked, int StageIndex);

/// <summary>
/// Implements staged letter unlocking and dual-mode text synthesis strictly omitting locked letters.
/// </summary>
public static class AdaptiveEngine
{
    private static readonly string[] UnlockStages =
    [
        "FJ",
        "DK",
        "SL",
        "AG",
        "HE",
        "IR",
        "TU",
        "NO",
        "CM",
        "PW",
        "VB",
        "YQ",
        "XZ"
    ];

    private static readonly HashSet<char> Vowels = ['a', 'e', 'i', 'o', 'u', 'y'];

    /// <summary>
    /// Creates the initial 26-letter adaptive state with only Stage 1 keys (<c>F</c>, <c>J</c>) unlocked.
    /// </summary>
    public static IReadOnlyList<AdaptiveKeyState> CreateInitialStates()
    {
        var list = new List<AdaptiveKeyState>(26);
        for (int stageIndex = 0; stageIndex < UnlockStages.Length; stageIndex++)
        {
            bool unlocked = stageIndex == 0;
            foreach (char ch in UnlockStages[stageIndex])
            {
                list.Add(new AdaptiveKeyState(ch, unlocked, stageIndex + 1));
            }
        }

        return list;
    }

    /// <summary>
    /// Returns the speed, accuracy, and sample-count thresholds required by the chosen difficulty profile.
    /// </summary>
    public static (double MaxLatencyMs, double MinAccuracyPercentage, int MinKeystrokes) GetThresholds(
        AdaptiveDifficultyProfile profile) => profile switch
        {
            AdaptiveDifficultyProfile.Casual => (650.0, 90.0, 10),
            AdaptiveDifficultyProfile.Pro => (350.0, 98.0, 15),
            _ => (500.0, 95.0, 10)
        };

    /// <summary>
    /// Advances adaptive progression by unlocking the next stage when all currently unlocked keys satisfy profile thresholds.
    /// </summary>
    public static IReadOnlyList<AdaptiveKeyState> EvaluateUnlockProgression(
        IReadOnlyList<AdaptiveKeyState> currentStates,
        IReadOnlyDictionary<char, KeyMetric> metrics,
        AdaptiveDifficultyProfile profile)
    {
        ArgumentNullException.ThrowIfNull(currentStates);
        ArgumentNullException.ThrowIfNull(metrics);

        if (currentStates.Count == 0)
        {
            currentStates = CreateInitialStates();
        }

        (double maxLatencyMs, double minAccuracy, int minKeystrokes) = GetThresholds(profile);

        var unlocked = currentStates.Where(s => s.IsUnlocked).ToList();
        if (unlocked.Count == 0)
        {
            return CreateInitialStates();
        }

        bool allReady = unlocked.All(state =>
        {
            char upper = char.ToUpperInvariant(state.Character);
            if (!TryGetMetricCaseInsensitive(metrics, upper, out KeyMetric? metric))
            {
                return false;
            }

            return metric.TotalKeystrokes >= minKeystrokes
                && metric.AccuracyPercentage >= minAccuracy
                && metric.AverageLatencyMs > 0
                && metric.AverageLatencyMs <= maxLatencyMs;
        });

        if (!allReady)
        {
            return currentStates;
        }

        int currentMaxStage = unlocked.Max(s => s.StageIndex);
        int nextStage = currentStates
            .Where(s => !s.IsUnlocked && s.StageIndex > currentMaxStage)
            .Select(s => s.StageIndex)
            .DefaultIfEmpty(-1)
            .Min();

        if (nextStage < 0)
        {
            return currentStates;
        }

        return currentStates
            .Select(s => s.StageIndex <= nextStage ? s with { IsUnlocked = true } : s)
            .ToArray();
    }

    /// <summary>
    /// Generates an adaptive drill prompt in either phonetic or natural dictionary mode while strictly omitting locked letters.
    /// </summary>
    public static string GeneratePracticeText(
        IReadOnlySet<char> unlockedLetters,
        IReadOnlyDictionary<char, KeyMetric> metrics,
        AdaptiveWordMode wordMode,
        IReadOnlyList<string> dictionaryWords,
        int wordCount = 18,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(unlockedLetters);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(dictionaryWords);

        random ??= Random.Shared;
        wordCount = Math.Max(1, wordCount);

        var allowedUpper = unlockedLetters
            .Where(char.IsLetter)
            .Select(char.ToUpperInvariant)
            .ToHashSet();

        if (allowedUpper.Count == 0)
        {
            allowedUpper.Add('F');
            allowedUpper.Add('J');
        }

        char[] allowedLower = allowedUpper.Select(char.ToLowerInvariant).OrderBy(c => c).ToArray();
        List<char> focusPool = BuildWeightedCharacterPool(allowedLower, metrics);

        if (wordMode == AdaptiveWordMode.NaturalDictionaryWords)
        {
            List<string> validDictionary = dictionaryWords
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(w => w.Trim().ToLowerInvariant())
                .Where(w => w.All(c => allowedUpper.Contains(char.ToUpperInvariant(c))))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (validDictionary.Count > 0)
            {
                return BuildFromDictionary(validDictionary, focusPool, metrics, wordCount, random);
            }
        }

        return BuildPhoneticPseudoWords(allowedLower, focusPool, wordCount, random);
    }

    private static string BuildFromDictionary(
        List<string> validDictionary,
        List<char> focusPool,
        IReadOnlyDictionary<char, KeyMetric> metrics,
        int wordCount,
        Random random)
    {
        // Prefer words that contain weak keys so drills naturally target lagging fingers.
        var weakKeys = metrics.Values
            .Where(m => m.TotalKeystrokes > 0 && (m.ErrorRatePercentage >= 8.0 || m.AverageLatencyMs >= 450.0))
            .Select(m => char.ToLowerInvariant(m.Character))
            .ToHashSet();

        List<string> weightedWords = new(validDictionary.Count * 2);
        foreach (string word in validDictionary)
        {
            weightedWords.Add(word);
            if (word.Any(c => weakKeys.Contains(c)))
            {
                weightedWords.Add(word);
                weightedWords.Add(word);
            }
        }

        var selected = new List<string>(wordCount);
        for (int i = 0; i < wordCount; i++)
        {
            selected.Add(weightedWords[random.Next(weightedWords.Count)]);
        }

        // Guard against unused parameter warning while reinforcing focus pool when dictionary is tiny.
        if (selected.Count == 0 && focusPool.Count > 0)
        {
            selected.Add(new string(focusPool.Take(3).ToArray()));
        }

        return string.Join(' ', selected);
    }

    private static string BuildPhoneticPseudoWords(
        char[] allowedLower,
        List<char> focusPool,
        int wordCount,
        Random random)
    {
        char[] unlockedVowels = allowedLower.Where(c => Vowels.Contains(c)).ToArray();
        char[] unlockedConsonants = allowedLower.Where(c => !Vowels.Contains(c)).ToArray();

        var words = new List<string>(wordCount);
        for (int w = 0; w < wordCount; w++)
        {
            int length = allowedLower.Length <= 2
                ? random.Next(2, 5)
                : random.Next(3, 7);

            var sb = new StringBuilder(length);
            bool expectVowel = unlockedVowels.Length > 0 && random.NextDouble() < 0.35;

            for (int i = 0; i < length; i++)
            {
                char next;
                if (unlockedVowels.Length == 0 || unlockedConsonants.Length == 0)
                {
                    next = focusPool[random.Next(focusPool.Count)];
                }
                else if (expectVowel)
                {
                    next = PickWeightedFromSubset(focusPool, unlockedVowels, random);
                }
                else
                {
                    next = PickWeightedFromSubset(focusPool, unlockedConsonants, random);
                }

                // Avoid awkward triple identical characters in synthetic drills.
                if (sb.Length >= 2 && sb[^1] == next && sb[^2] == next)
                {
                    char alternate = allowedLower[random.Next(allowedLower.Length)];
                    next = alternate;
                }

                sb.Append(next);
                expectVowel = !expectVowel;
            }

            words.Add(sb.ToString());
        }

        return string.Join(' ', words);
    }

    private static char PickWeightedFromSubset(List<char> focusPool, char[] subset, Random random)
    {
        if (random.NextDouble() < 0.7)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                char candidate = focusPool[random.Next(focusPool.Count)];
                if (Array.IndexOf(subset, candidate) >= 0)
                {
                    return candidate;
                }
            }
        }

        return subset[random.Next(subset.Length)];
    }

    private static List<char> BuildWeightedCharacterPool(
        char[] allowedLower,
        IReadOnlyDictionary<char, KeyMetric> metrics)
    {
        var pool = new List<char>(allowedLower.Length * 4);
        foreach (char ch in allowedLower)
        {
            pool.Add(ch);
            if (!TryGetMetricCaseInsensitive(metrics, char.ToUpperInvariant(ch), out KeyMetric? metric)
                || metric.TotalKeystrokes < 10)
            {
                pool.Add(ch);
                pool.Add(ch);
                continue;
            }

            if (metric.ErrorRatePercentage >= 8.0 || metric.AverageLatencyMs >= 450.0)
            {
                pool.Add(ch);
                pool.Add(ch);
                pool.Add(ch);
            }
        }

        return pool;
    }

    private static bool TryGetMetricCaseInsensitive(
        IReadOnlyDictionary<char, KeyMetric> metrics,
        char upperKey,
        out KeyMetric metric)
    {
        if (metrics.TryGetValue(upperKey, out KeyMetric? exact) && exact is not null)
        {
            metric = exact;
            return true;
        }

        char lowerKey = char.ToLowerInvariant(upperKey);
        if (metrics.TryGetValue(lowerKey, out KeyMetric? lower) && lower is not null)
        {
            metric = lower;
            return true;
        }

        metric = null!;
        return false;
    }
}
