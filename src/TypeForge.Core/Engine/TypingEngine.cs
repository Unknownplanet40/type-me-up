using System.Text;
using TypeForge.Core.Models;

namespace TypeForge.Core.Engine;

/// <summary>
/// Deterministic state machine managing character-by-character typing evaluation, timing, and metrics.
/// </summary>
public sealed class TypingEngine
{
    private sealed class KeyAccumulator
    {
        public int Total { get; set; }
        public int Correct { get; set; }
        public int Incorrect { get; set; }
        public double LatencySumMs { get; set; }
        public int LatencySamples { get; set; }

        public KeyMetric ToMetric(char character)
        {
            double avgLatency = LatencySamples == 0 ? 0.0 : Math.Round(LatencySumMs / LatencySamples, 2);
            return new KeyMetric(character, Total, Correct, Incorrect, avgLatency);
        }
    }

    private readonly string _targetText;
    private readonly bool _stopOnError;
    private readonly bool _forgiveErrors;
    private readonly bool _spaceSkipsWords;
    private readonly TimeProvider _timeProvider;
    private readonly SessionType _sessionType;
    private readonly string? _lessonId;
    private readonly StringBuilder _typedText = new();
    private readonly TypingCharacterState[] _slotStates;
    private readonly Dictionary<char, KeyAccumulator> _keyAccumulators = new();
    private readonly BigramTracker _bigramTracker = new();
    private readonly List<double> _keystrokeLatenciesMs = [];
    private readonly HashSet<int> _missedWordIndices = [];
    private readonly List<(int Start, int Length)> _wordSpans = [];

    private int _cursorIndex;
    private int _totalKeystrokes;
    private int _correctKeystrokes;
    private int _mistakes;
    private int _uncorrectedErrors;
    private bool _pendingStopError;
    private bool _hasStarted;
    private bool _isCompleted;
    private DateTimeOffset _startedAt;
    private DateTimeOffset _lastKeystrokeAt;
    private DateTimeOffset _completedAt;

    /// <summary>
    /// Initializes a new <see cref="TypingEngine"/> for the given target prompt.
    /// </summary>
    public TypingEngine(
        string targetText,
        bool stopOnError = false,
        TimeProvider? timeProvider = null,
        SessionType sessionType = SessionType.AdaptivePractice,
        string? lessonId = null,
        bool forgiveErrors = false,
        bool spaceSkipsWords = false)
    {
        if (string.IsNullOrEmpty(targetText))
        {
            throw new ArgumentException("Target typing text cannot be null or empty.", nameof(targetText));
        }

        _targetText = targetText;
        _stopOnError = stopOnError;
        _forgiveErrors = forgiveErrors;
        _spaceSkipsWords = spaceSkipsWords;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _sessionType = sessionType;
        _lessonId = lessonId;
        _slotStates = new TypingCharacterState[targetText.Length];
        BuildWordSpans(targetText, _wordSpans);
        Reset();
    }

    /// <summary>
    /// Processes a single character keystroke against the current expected character.
    /// </summary>
    /// <returns><c>true</c> if the keystroke was evaluated; <c>false</c> if ignored or session completed.</returns>
    public bool ProcessKeystroke(char inputChar)
    {
        if (_isCompleted)
        {
            return false;
        }

        if (char.IsControl(inputChar) && inputChar != '\t' && inputChar != '\n')
        {
            return false;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        double? latencyMs = null;

        if (!_hasStarted)
        {
            _hasStarted = true;
            _startedAt = now;
            _lastKeystrokeAt = now;
        }
        else
        {
            latencyMs = Math.Max(0.0, (now - _lastKeystrokeAt).TotalMilliseconds);
            _lastKeystrokeAt = now;
        }

        char expectedChar = _targetText[_cursorIndex];

        if (_spaceSkipsWords &&
            inputChar == ' ' &&
            expectedChar != ' ' &&
            _cursorIndex > 0 &&
            _targetText[_cursorIndex - 1] != ' ')
        {
            _pendingStopError = false;
            while (_cursorIndex < _targetText.Length && _targetText[_cursorIndex] != ' ')
            {
                char skippedChar = _targetText[_cursorIndex];
                RecordKeyAccumulator(char.ToUpperInvariant(skippedChar), isMatch: false, latencyMs);
                _totalKeystrokes++;
                _mistakes++;
                _uncorrectedErrors++;
                _slotStates[_cursorIndex] = TypingCharacterState.Incorrect;
                _typedText.Append(skippedChar);
                _cursorIndex++;
                latencyMs = null;
            }

            if (_cursorIndex < _targetText.Length && _targetText[_cursorIndex] == ' ')
            {
                RecordKeyAccumulator(' ', isMatch: true, latencyMs: null);
                _bigramTracker.RecordKeystroke(' ', isCorrect: true, latencyMs: null);
                _totalKeystrokes++;
                _correctKeystrokes++;
                _slotStates[_cursorIndex] = TypingCharacterState.Correct;
                _typedText.Append(' ');
                _cursorIndex++;
            }

            if (_cursorIndex >= _targetText.Length)
            {
                _isCompleted = true;
                _completedAt = now;
            }

            return true;
        }

        char keyBucket = char.ToUpperInvariant(expectedChar);
        bool isMatch = inputChar == expectedChar;

        RecordKeyAccumulator(keyBucket, isMatch, latencyMs);
        _bigramTracker.RecordKeystroke(expectedChar, isMatch, latencyMs);
        if (latencyMs is > 0.0 and <= 2000.0)
        {
            _keystrokeLatenciesMs.Add(latencyMs.Value);
        }

        _totalKeystrokes++;

        if (isMatch)
        {
            _pendingStopError = false;
            _correctKeystrokes++;
            _slotStates[_cursorIndex] = TypingCharacterState.Correct;
            _typedText.Append(inputChar);
            _cursorIndex++;
        }
        else
        {
            RecordMissedWordAt(_cursorIndex);
            _mistakes++;
            if (!_stopOnError)
            {
                _uncorrectedErrors++;
                _slotStates[_cursorIndex] = TypingCharacterState.Incorrect;
                _typedText.Append(inputChar);
                _cursorIndex++;
            }
            else
            {
                _pendingStopError = true;
            }
        }

        if (_cursorIndex >= _targetText.Length)
        {
            _isCompleted = true;
            _completedAt = now;
        }

        return true;
    }

    /// <summary>
    /// Reverts the cursor by one character in free-flow mode without erasing recorded mistake totals.
    /// </summary>
    /// <returns><c>true</c> if the cursor moved back; otherwise <c>false</c>.</returns>
    public bool ProcessBackspace()
    {
        if (_isCompleted || _stopOnError || _cursorIndex == 0)
        {
            return false;
        }

        _cursorIndex--;
        if (_typedText.Length > 0)
        {
            _typedText.Remove(_typedText.Length - 1, 1);
        }

        if (_slotStates[_cursorIndex] == TypingCharacterState.Incorrect)
        {
            _uncorrectedErrors = Math.Max(0, _uncorrectedErrors - 1);
        }

        _slotStates[_cursorIndex] = TypingCharacterState.Pending;
        _bigramTracker.RecordBackspace();
        return true;
    }

    /// <summary>
    /// Resets the engine to its initial pre-session state for repeating the prompt.
    /// </summary>
    public void Reset()
    {
        _typedText.Clear();
        Array.Fill(_slotStates, TypingCharacterState.Pending);
        _keyAccumulators.Clear();
        _bigramTracker.Clear();
        _keystrokeLatenciesMs.Clear();
        _missedWordIndices.Clear();

        _cursorIndex = 0;
        _totalKeystrokes = 0;
        _correctKeystrokes = 0;
        _mistakes = 0;
        _uncorrectedErrors = 0;
        _pendingStopError = false;
        _hasStarted = false;
        _isCompleted = false;
        _startedAt = default;
        _lastKeystrokeAt = default;
        _completedAt = default;
    }

    /// <summary>
    /// Returns an immutable snapshot of current typing progress and live metrics.
    /// </summary>
    public TypingSnapshot GetSnapshot()
    {
        TimeSpan elapsed = GetElapsed();
        int effectiveTypedCount = _typedText.Length;

        var states = (TypingCharacterState[])_slotStates.Clone();
        if (!_isCompleted && _cursorIndex < states.Length)
        {
            states[_cursorIndex] = TypingCharacterState.Current;
        }

        double accuracy = CalculateAccuracy();

        return new TypingSnapshot(
            TargetText: _targetText,
            TypedText: _typedText.ToString(),
            CursorIndex: _cursorIndex,
            TotalKeystrokes: _totalKeystrokes,
            CorrectKeystrokes: _correctKeystrokes,
            Mistakes: _mistakes,
            UncorrectedErrors: _uncorrectedErrors,
            Elapsed: elapsed,
            LiveGrossWpm: MetricsCalculator.CalculateGrossWpm(effectiveTypedCount, elapsed),
            LiveNetWpm: MetricsCalculator.CalculateNetWpm(effectiveTypedCount, _uncorrectedErrors, elapsed),
            LiveAccuracyPercentage: accuracy,
            IsCompleted: _isCompleted,
            CharacterStates: states);
    }

    /// <summary>
    /// Produces a validated <see cref="SessionResult"/> summarizing performance and per-key stats.
    /// </summary>
    public SessionResult BuildResult()
    {
        TimeSpan elapsed = GetElapsed();
        int effectiveTypedCount = _typedText.Length;

        var breakdown = new Dictionary<char, KeyMetric>(_keyAccumulators.Count);
        foreach (var kvp in _keyAccumulators)
        {
            breakdown[kvp.Key] = kvp.Value.ToMetric(kvp.Key);
        }

        var result = new SessionResult
        {
            SessionType = _sessionType,
            LessonId = _lessonId,
            StartedAt = _hasStarted ? _startedAt : _timeProvider.GetUtcNow(),
            Duration = elapsed,
            GrossWpm = MetricsCalculator.CalculateGrossWpm(effectiveTypedCount, elapsed),
            NetWpm = MetricsCalculator.CalculateNetWpm(effectiveTypedCount, _uncorrectedErrors, elapsed),
            AccuracyPercentage = CalculateAccuracy(),
            CorrectCharacters = _correctKeystrokes,
            TotalKeystrokes = _totalKeystrokes,
            Mistakes = _mistakes,
            UncorrectedErrors = _uncorrectedErrors,
            KeyBreakdown = breakdown
        };

        result.Validate();
        return result;
    }

    /// <summary>
    /// Returns the bigram latency and error metrics accumulated during the current session.
    /// </summary>
    public IReadOnlyDictionary<string, BigramMetric> GetBigramBreakdown() => _bigramTracker.GetMetrics();

    /// <summary>
    /// Returns typing rhythm consistency percentage (0–100) for the current session.
    /// </summary>
    public double GetConsistencyPercentage() =>
        MetricsCalculator.CalculateConsistencyPercentage(_keystrokeLatenciesMs);

    /// <summary>
    /// Returns distinct target words in which one or more mistakes occurred during the current session.
    /// </summary>
    public IReadOnlyList<string> GetMissedWords()
    {
        if (_missedWordIndices.Count == 0 || _wordSpans.Count == 0)
        {
            return [];
        }

        var result = new List<string>(_missedWordIndices.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (int idx in _missedWordIndices.OrderBy(i => i))
        {
            if (idx < 0 || idx >= _wordSpans.Count)
            {
                continue;
            }

            (int start, int length) = _wordSpans[idx];
            string word = _targetText.Substring(start, length);
            if (!string.IsNullOrWhiteSpace(word) && seen.Add(word))
            {
                result.Add(word);
            }
        }

        return result;
    }

    /// <summary>
    /// Builds a focused drill prompt cycling through the specified missed words.
    /// </summary>
    public static string BuildMissedWordsDrillText(IReadOnlyList<string> missedWords, int targetWordCount = 24)
    {
        ArgumentNullException.ThrowIfNull(missedWords);
        if (missedWords.Count == 0 || targetWordCount <= 0)
        {
            return string.Empty;
        }

        var words = new List<string>(targetWordCount);
        for (int i = 0; i < targetWordCount; i++)
        {
            words.Add(missedWords[i % missedWords.Count]);
        }

        return string.Join(' ', words);
    }

    private void RecordMissedWordAt(int charIndex)
    {
        for (int i = 0; i < _wordSpans.Count; i++)
        {
            (int start, int length) = _wordSpans[i];
            if (charIndex >= start && charIndex < start + length)
            {
                _missedWordIndices.Add(i);
                return;
            }
        }
    }

    private static void BuildWordSpans(string text, List<(int Start, int Length)> spans)
    {
        spans.Clear();
        int i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            if (i >= text.Length)
            {
                break;
            }

            int start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            spans.Add((start, i - start));
        }
    }

    private double CalculateAccuracy()
    {
        if (!_forgiveErrors)
        {
            return MetricsCalculator.CalculateAccuracyPercentage(_correctKeystrokes, _totalKeystrokes);
        }

        int effectiveErrors = _uncorrectedErrors + (_pendingStopError ? 1 : 0);
        int effectiveTotal = _correctKeystrokes + effectiveErrors;
        return MetricsCalculator.CalculateAccuracyPercentage(_correctKeystrokes, effectiveTotal);
    }

    private TimeSpan GetElapsed()
    {
        if (!_hasStarted)
        {
            return TimeSpan.Zero;
        }

        DateTimeOffset end = _isCompleted ? _completedAt : _timeProvider.GetUtcNow();
        TimeSpan elapsed = end - _startedAt;
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
    }

    private void RecordKeyAccumulator(char keyBucket, bool isMatch, double? latencyMs)
    {
        if (!_keyAccumulators.TryGetValue(keyBucket, out KeyAccumulator? acc))
        {
            acc = new KeyAccumulator();
            _keyAccumulators[keyBucket] = acc;
        }

        acc.Total++;
        if (isMatch)
        {
            acc.Correct++;
        }
        else
        {
            acc.Incorrect++;
        }

        if (latencyMs.HasValue)
        {
            acc.LatencySumMs += latencyMs.Value;
            acc.LatencySamples++;
        }
    }
}
