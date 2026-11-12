using TypeForge.Core.Models;

namespace TypeForge.Core.Services;

/// <summary>
/// Summary metrics for an active dictionary word list.
/// </summary>
/// <param name="UniqueWords">Count of distinct non-empty words in the active list.</param>
/// <param name="AverageWordLength">Mean character length across unique words.</param>
public sealed record WordListStats(int UniqueWords, double AverageWordLength);

/// <summary>
/// Builds typing test prompts and computes live statistics for Common Words, Pseudo Words, Books, and Custom text.
/// </summary>
public static class TestTextGenerator
{
    private static readonly string[] PseudoOnsets =
    [
        "b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "n", "p", "r", "s", "t", "v", "w", "z",
        "bl", "br", "ch", "cl", "cr", "dr", "fl", "fr", "gl", "gr", "pl", "pr", "sh", "sl", "sm", "sn", "sp", "st", "sw", "th", "tr", "wh"
    ];

    private static readonly string[] PseudoNuclei = ["a", "e", "i", "o", "u", "ea", "oo", "ai", "ou"];

    private static readonly string[] PseudoCodas =
    [
        "", "d", "k", "l", "m", "n", "p", "r", "s", "t", "ld", "nd", "nk", "nt", "rk", "rt", "st", "th", "ch", "sh"
    ];

    /// <summary>
    /// Parses a comma-, newline-, or space-delimited custom word list, or falls back to the top <paramref name="wordCount"/> words.
    /// </summary>
    public static IReadOnlyList<string> ResolveCommonWordPool(
        IReadOnlyList<string> fullDictionary,
        int wordCount,
        string? customWordListCsv)
    {
        ArgumentNullException.ThrowIfNull(fullDictionary);

        if (!string.IsNullOrWhiteSpace(customWordListCsv))
        {
            List<string> parsed = customWordListCsv
                .Split([',', '\r', '\n', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(w => w.Trim())
                .Where(w => !string.IsNullOrEmpty(w))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (parsed.Count > 0)
            {
                return parsed;
            }
        }

        int clamped = Math.Clamp(wordCount, 1, Math.Max(1, fullDictionary.Count));
        return fullDictionary.Take(clamped).ToList();
    }

    /// <summary>
    /// Computes unique word count and average word length for a word pool.
    /// </summary>
    public static WordListStats ComputeWordStats(IReadOnlyList<string> words)
    {
        if (words is null || words.Count == 0)
        {
            return new WordListStats(0, 0.0);
        }

        var unique = words
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (unique.Count == 0)
        {
            return new WordListStats(0, 0.0);
        }

        double avg = unique.Average(w => w.Length);
        return new WordListStats(unique.Count, Math.Round(avg, 2));
    }

    /// <summary>
    /// Generates a test passage sampled uniformly from <paramref name="wordPool"/>.
    /// </summary>
    public static string GenerateCommonWordsPrompt(
        IReadOnlyList<string> wordPool,
        int targetCharCount,
        Random? rng = null)
    {
        ArgumentNullException.ThrowIfNull(wordPool);
        if (wordPool.Count == 0)
        {
            throw new ArgumentException("Word pool cannot be empty.", nameof(wordPool));
        }

        rng ??= Random.Shared;
        int minChars = Math.Clamp(targetCharCount, 40, 6000);
        var words = new List<string>();
        int currentChars = 0;

        while (currentChars < minChars)
        {
            string word = wordPool[rng.Next(wordPool.Count)];
            if (words.Count > 0 && words[^1].Equals(word, StringComparison.OrdinalIgnoreCase) && wordPool.Count > 1)
            {
                word = wordPool[(rng.Next(wordPool.Count - 1) + 1) % wordPool.Count];
            }

            currentChars += word.Length + (words.Count == 0 ? 0 : 1);
            words.Add(word);
        }

        return string.Join(' ', words);
    }

    /// <summary>
    /// Generates pronounceable pseudo-words totaling at least <paramref name="targetCharCount"/> characters.
    /// </summary>
    public static string GeneratePseudoWordsPrompt(int targetCharCount, Random? rng = null)
    {
        rng ??= Random.Shared;
        int minChars = Math.Clamp(targetCharCount, 40, 6000);
        var words = new List<string>();
        int currentChars = 0;

        while (currentChars < minChars)
        {
            int syllables = rng.NextDouble() < 0.4 ? 2 : 1;
            var sb = new System.Text.StringBuilder();
            for (int s = 0; s < syllables; s++)
            {
                sb.Append(PseudoOnsets[rng.Next(PseudoOnsets.Length)]);
                sb.Append(PseudoNuclei[rng.Next(PseudoNuclei.Length)]);
                if (s == syllables - 1 || rng.NextDouble() < 0.35)
                {
                    sb.Append(PseudoCodas[rng.Next(PseudoCodas.Length)]);
                }
            }

            string word = sb.ToString();
            if (word.Length < 3)
            {
                word += "n";
            }

            currentChars += word.Length + (words.Count == 0 ? 0 : 1);
            words.Add(word);
        }

        return string.Join(' ', words);
    }

    /// <summary>
    /// Extracts sequential paragraphs from <paramref name="book"/> starting at <paramref name="startParagraphIndex"/>.
    /// </summary>
    public static string GenerateBookPrompt(BookModel book, int startParagraphIndex, int minimumCharacters)
    {
        ArgumentNullException.ThrowIfNull(book);

        int idx = Math.Clamp(startParagraphIndex, 0, book.Paragraphs.Count - 1);
        int targetChars = Math.Clamp(minimumCharacters, 60, 6000);

        var selected = new List<string>();
        int totalChars = 0;

        while (totalChars < targetChars && idx < book.Paragraphs.Count)
        {
            string para = NormalizeWhitespace(book.Paragraphs[idx]);
            if (!string.IsNullOrEmpty(para))
            {
                totalChars += para.Length + (selected.Count == 0 ? 0 : 1);
                selected.Add(para);
            }

            idx++;
        }

        return selected.Count > 0 ? string.Join(' ', selected) : book.Paragraphs[0];
    }

    /// <summary>
    /// Generates a test passage from a <see cref="RealWorldPoolModel"/> totaling at least <paramref name="targetCharCount"/> characters.
    /// </summary>
    public static string GenerateRealWorldPoolPrompt(
        RealWorldPoolModel pool,
        int targetCharCount,
        Random? rng = null)
    {
        ArgumentNullException.ThrowIfNull(pool);
        if (pool.Items.Count == 0)
        {
            throw new ArgumentException("Real-world pool cannot be empty.", nameof(pool));
        }

        rng ??= Random.Shared;
        int minChars = Math.Clamp(targetCharCount, 60, 6000);
        var segments = new List<string>();
        int currentChars = 0;

        while (currentChars < minChars)
        {
            string item = pool.Items[rng.Next(pool.Items.Count)];
            if (segments.Count > 0 && segments[^1].Equals(item, StringComparison.Ordinal) && pool.Items.Count > 1)
            {
                item = pool.Items[(rng.Next(pool.Items.Count - 1) + 1) % pool.Items.Count];
            }

            currentChars += item.Length + (segments.Count == 0 ? 0 : 1);
            segments.Add(item);
        }

        return string.Join(' ', segments);
    }

    /// <summary>
    /// Estimates an appropriate generated character budget for a selected test mode.
    /// </summary>
    public static int GetTargetCharactersForMode(TestDurationMode mode) => mode switch
    {
        TestDurationMode.Seconds15 => 360,
        TestDurationMode.Seconds30 => 650,
        TestDurationMode.Minute1 => 1200,
        TestDurationMode.Characters100 => 100,
        TestDurationMode.Characters500 => 500,
        TestDurationMode.Characters1000 => 1000,
        _ => 360
    };

    /// <summary>
    /// Returns true if the test stops strictly on exact character completion rather than countdown time.
    /// </summary>
    public static bool IsCharacterCountMode(TestDurationMode mode) => mode is
        TestDurationMode.Characters100 or
        TestDurationMode.Characters500 or
        TestDurationMode.Characters1000;

    /// <summary>
    /// Returns the timed test duration in seconds, or 0 for character-count modes.
    /// </summary>
    public static int GetDurationSeconds(TestDurationMode mode) => mode switch
    {
        TestDurationMode.Seconds15 => 15,
        TestDurationMode.Seconds30 => 30,
        TestDurationMode.Minute1 => 60,
        _ => 0
    };

    private static string NormalizeWhitespace(string text)
    {
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
