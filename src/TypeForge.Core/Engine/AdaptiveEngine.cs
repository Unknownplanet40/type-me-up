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
    public static IReadOnlyList<AdaptiveKeyState> CreateInitialStates() =>
        CreateStatesWithUnlockedStage(1);

    /// <summary>
    /// Creates a 26-letter adaptive state list with all stages up to <paramref name="maxUnlockedStage"/> (1–13) unlocked.
    /// </summary>
    public static IReadOnlyList<AdaptiveKeyState> CreateStatesWithUnlockedStage(int maxUnlockedStage)
    {
        int clampedStage = Math.Clamp(maxUnlockedStage, 1, UnlockStages.Length);
        var list = new List<AdaptiveKeyState>(26);
        for (int stageIndex = 0; stageIndex < UnlockStages.Length; stageIndex++)
        {
            bool unlocked = (stageIndex + 1) <= clampedStage;
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

    /// <summary>
    /// Generates a weighted practice drill where approximately <paramref name="targetWeakRatio"/> (default 50%)
    /// of generated words contain a top weak key or weak bigram while strictly omitting locked letters.
    /// </summary>
    public static string GenerateWeightedPracticeText(
        IReadOnlySet<char> unlockedLetters,
        IReadOnlyDictionary<char, KeyMetric> keyMetrics,
        IReadOnlyDictionary<string, BigramMetric> bigramMetrics,
        AdaptiveWordMode wordMode,
        IReadOnlyList<string> dictionaryWords,
        int wordCount = 18,
        double targetWeakRatio = 0.50,
        int minWeaknessSamples = WeaknessScorer.DefaultMinSamples,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(unlockedLetters);
        ArgumentNullException.ThrowIfNull(keyMetrics);
        ArgumentNullException.ThrowIfNull(bigramMetrics);
        ArgumentNullException.ThrowIfNull(dictionaryWords);

        random ??= Random.Shared;
        wordCount = Math.Max(1, wordCount);
        targetWeakRatio = Math.Clamp(targetWeakRatio, 0.0, 1.0);

        HashSet<char> allowedUpper = unlockedLetters
            .Where(char.IsLetter)
            .Select(char.ToUpperInvariant)
            .ToHashSet();

        if (allowedUpper.Count == 0)
        {
            allowedUpper.Add('F');
            allowedUpper.Add('J');
        }

        char[] allowedLower = allowedUpper.Select(char.ToLowerInvariant).OrderBy(c => c).ToArray();

        char[] weakKeysLower = WeaknessScorer.RankKeys(keyMetrics.Values, minWeaknessSamples)
            .Where(r => allowedUpper.Contains(r.Target))
            .Take(5)
            .Select(r => char.ToLowerInvariant(r.Target))
            .ToArray();

        string[] weakBigramsLower = WeaknessScorer.RankBigrams(bigramMetrics.Values, minWeaknessSamples)
            .Where(r => r.Target.Length == 2 && allowedUpper.Contains(r.Target[0]) && allowedUpper.Contains(r.Target[1]))
            .Take(5)
            .Select(r => r.Target.ToLowerInvariant())
            .ToArray();

        if (weakKeysLower.Length == 0 && weakBigramsLower.Length == 0)
        {
            return GeneratePracticeText(unlockedLetters, keyMetrics, wordMode, dictionaryWords, wordCount, random);
        }

        HashSet<char> weakKeySet = weakKeysLower.ToHashSet();

        bool WordContainsWeakness(string word)
        {
            for (int i = 0; i < word.Length; i++)
            {
                if (weakKeySet.Contains(word[i]))
                {
                    return true;
                }
            }

            for (int b = 0; b < weakBigramsLower.Length; b++)
            {
                if (word.Contains(weakBigramsLower[b], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        if (wordMode == AdaptiveWordMode.NaturalDictionaryWords)
        {
            List<string> validDictionary = dictionaryWords
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(w => w.Trim().ToLowerInvariant())
                .Where(w => w.All(c => allowedUpper.Contains(char.ToUpperInvariant(c))))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            List<string> weakWords = validDictionary.Where(WordContainsWeakness).ToList();
            List<string> neutralWords = validDictionary.Where(w => !WordContainsWeakness(w)).ToList();

            if (weakWords.Count > 0 && neutralWords.Count > 0)
            {
                var selected = new List<string>(wordCount);
                for (int i = 0; i < wordCount; i++)
                {
                    bool pickWeak = random.NextDouble() < targetWeakRatio;
                    List<string> bucket = pickWeak ? weakWords : neutralWords;
                    selected.Add(bucket[random.Next(bucket.Count)]);
                }

                return string.Join(' ', selected);
            }
        }

        char[] nonWeakLower = allowedLower.Where(c => !weakKeySet.Contains(c)).ToArray();
        if (nonWeakLower.Length == 0)
        {
            nonWeakLower = allowedLower;
        }

        var pseudoWords = new List<string>(wordCount);
        for (int w = 0; w < wordCount; w++)
        {
            bool makeWeak = random.NextDouble() < targetWeakRatio;
            int length = allowedLower.Length <= 2 ? random.Next(3, 5) : random.Next(4, 7);

            if (makeWeak)
            {
                var sb = new StringBuilder(length);
                for (int i = 0; i < length; i++)
                {
                    sb.Append(allowedLower[random.Next(allowedLower.Length)]);
                }

                if (weakBigramsLower.Length > 0 && (weakKeysLower.Length == 0 || random.NextDouble() < 0.5))
                {
                    string bg = weakBigramsLower[random.Next(weakBigramsLower.Length)];
                    int pos = random.Next(0, Math.Max(1, length - 1));
                    sb[pos] = bg[0];
                    if (pos + 1 < sb.Length)
                    {
                        sb[pos + 1] = bg[1];
                    }
                    else
                    {
                        sb.Append(bg[1]);
                    }
                }
                else if (weakKeysLower.Length > 0)
                {
                    char wk = weakKeysLower[random.Next(weakKeysLower.Length)];
                    int pos = random.Next(0, sb.Length);
                    sb[pos] = wk;
                }

                pseudoWords.Add(sb.ToString());
            }
            else
            {
                var sb = new StringBuilder(length);
                for (int i = 0; i < length; i++)
                {
                    char candidate = nonWeakLower[random.Next(nonWeakLower.Length)];
                    if (sb.Length > 0 && weakBigramsLower.Length > 0)
                    {
                        char prev = sb[^1];
                        for (int attempt = 0; attempt < 6; attempt++)
                        {
                            string pair = string.Concat(prev, candidate);
                            if (!weakBigramsLower.Contains(pair, StringComparer.Ordinal))
                            {
                                break;
                            }

                            candidate = nonWeakLower[random.Next(nonWeakLower.Length)];
                        }
                    }

                    sb.Append(candidate);
                }

                pseudoWords.Add(sb.ToString());
            }
        }

        return string.Join(' ', pseudoWords);
    }

    /// <summary>
    /// Generates a targeted 2-minute drill (~80 words) focused on the user's slowest and most error-prone letter pairs (bigrams).
    /// </summary>
    /// <param name="unlockedKeys">Currently unlocked letter set.</param>
    /// <param name="bigramMetrics">Recorded bigram metrics.</param>
    /// <param name="dictionaryPool">Embedded dictionary word pool.</param>
    /// <param name="wordCount">Target word count (default 80 for a 2-minute drill).</param>
    /// <param name="random">Optional RNG for deterministic testing.</param>
    /// <returns>Space-separated bigram drill text.</returns>
    public static string GenerateBigramDrillText(
        IReadOnlySet<char> unlockedKeys,
        IReadOnlyDictionary<string, BigramMetric> bigramMetrics,
        IReadOnlyList<string>? dictionaryPool = null,
        int wordCount = 80,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(unlockedKeys);
        ArgumentNullException.ThrowIfNull(bigramMetrics);
        random ??= Random.Shared;

        char[] allowedLower = unlockedKeys
            .Select(char.ToLowerInvariant)
            .Distinct()
            .ToArray();

        if (allowedLower.Length == 0)
        {
            allowedLower = ['f', 'j'];
        }

        HashSet<char> allowedSet = [.. allowedLower];

        IReadOnlyList<RankedWeakness<string>> ranked = WeaknessScorer.RankBigrams(
            bigramMetrics.Values,
            minSamples: WeaknessScorer.DefaultMinSamples)
            .Take(6)
            .ToArray();

        if (ranked.Count == 0)
        {
            ranked = WeaknessScorer.RankBigrams(
                bigramMetrics.Values,
                minSamples: 3)
                .Take(6)
                .ToArray();
        }

        string[] targetBigrams = ranked
            .Select(b => b.Target.ToLowerInvariant())
            .Where(b => b.Length == 2 && allowedSet.Contains(b[0]) && allowedSet.Contains(b[1]))
            .ToArray();

        if (targetBigrams.Length == 0)
        {
            // Fallback pairs formed from unlocked letters
            var fallback = new List<string>();
            for (int i = 0; i < Math.Min(allowedLower.Length, 4); i++)
            {
                char first = allowedLower[i];
                char second = allowedLower[(i + 1) % allowedLower.Length];
                fallback.Add(string.Concat(first, second));
            }

            targetBigrams = fallback.ToArray();
        }

        if (dictionaryPool is { Count: > 0 })
        {
            List<string> validWords = dictionaryPool
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(w => w.Trim().ToLowerInvariant())
                .Where(w => w.All(c => allowedSet.Contains(c)))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            List<string> matchingWords = validWords
                .Where(w => targetBigrams.Any(bg => w.Contains(bg, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (matchingWords.Count >= 4)
            {
                var chosen = new List<string>(wordCount);
                for (int i = 0; i < wordCount; i++)
                {
                    bool pickBigramWord = random.NextDouble() < 0.75 || validWords.Count == 0;
                    List<string> pool = pickBigramWord ? matchingWords : validWords;
                    chosen.Add(pool[random.Next(pool.Count)]);
                }

                return string.Join(' ', chosen);
            }
        }

        var words = new List<string>(wordCount);
        for (int w = 0; w < wordCount; w++)
        {
            string bg = targetBigrams[w % targetBigrams.Length];
            int len = random.Next(4, 7);
            var sb = new StringBuilder(len);
            for (int i = 0; i < len; i++)
            {
                sb.Append(allowedLower[random.Next(allowedLower.Length)]);
            }

            int pos = random.Next(0, Math.Max(1, len - 1));
            sb[pos] = bg[0];
            if (pos + 1 < sb.Length)
            {
                sb[pos + 1] = bg[1];
            }
            else
            {
                sb.Append(bg[1]);
            }

            words.Add(sb.ToString());
        }

        return string.Join(' ', words);
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
