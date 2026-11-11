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
    private readonly TimeProvider _timeProvider;
    private readonly SessionType _sessionType;
    private readonly string? _lessonId;
    private readonly StringBuilder _typedText = new();
    private readonly TypingCharacterState[] _slotStates;
    private readonly Dictionary<char, KeyAccumulator> _keyAccumulators = new();

    private int _cursorIndex;
    private int _totalKeystrokes;
    private int _correctKeystrokes;
    private int _mistakes;
    private int _uncorrectedErrors;
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
        string? lessonId = null)
    {
        if (string.IsNullOrEmpty(targetText))
        {
            throw new ArgumentException("Target typing text cannot be null or empty.", nameof(targetText));
        }

        _targetText = targetText;
        _stopOnError = stopOnError;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _sessionType = sessionType;
        _lessonId = lessonId;
        _slotStates = new TypingCharacterState[targetText.Length];
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
        char keyBucket = char.ToUpperInvariant(expectedChar);
        bool isMatch = inputChar == expectedChar;

        RecordKeyAccumulator(keyBucket, isMatch, latencyMs);
        _totalKeystrokes++;

        if (isMatch)
        {
            _correctKeystrokes++;
            _slotStates[_cursorIndex] = TypingCharacterState.Correct;
            _typedText.Append(inputChar);
            _cursorIndex++;
        }
        else
        {
            _mistakes++;
            if (!_stopOnError)
            {
                _uncorrectedErrors++;
                _slotStates[_cursorIndex] = TypingCharacterState.Incorrect;
                _typedText.Append(inputChar);
                _cursorIndex++;
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

        _cursorIndex = 0;
        _totalKeystrokes = 0;
        _correctKeystrokes = 0;
        _mistakes = 0;
        _uncorrectedErrors = 0;
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
            LiveAccuracyPercentage: MetricsCalculator.CalculateAccuracyPercentage(_correctKeystrokes, _totalKeystrokes),
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
            AccuracyPercentage = MetricsCalculator.CalculateAccuracyPercentage(_correctKeystrokes, _totalKeystrokes),
            CorrectCharacters = _correctKeystrokes,
            TotalKeystrokes = _totalKeystrokes,
            Mistakes = _mistakes,
            UncorrectedErrors = _uncorrectedErrors,
            KeyBreakdown = breakdown
        };

        result.Validate();
        return result;
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
