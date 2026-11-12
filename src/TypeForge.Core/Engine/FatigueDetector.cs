namespace TypeForge.Core.Engine;

/// <summary>
/// Tracks rolling keystroke windows and continuous typing time to detect typing fatigue and suggest a short break at most once per session.
/// </summary>
public sealed class FatigueDetector
{
    /// <summary>
    /// Number of keystrokes per rolling accuracy evaluation window.
    /// </summary>
    public const int WindowSize = 30;

    /// <summary>
    /// Minimum percentage-point drop below session baseline accuracy required to count a window as fatigued.
    /// </summary>
    public const double AccuracyDropThresholdPoints = 10.0;

    /// <summary>
    /// Number of consecutive fatigued windows required to trigger a break suggestion.
    /// </summary>
    public const int RequiredConsecutiveFatiguedWindows = 2;

    /// <summary>
    /// Minimum session elapsed time before window-based accuracy drop can trigger a break suggestion.
    /// </summary>
    public static readonly TimeSpan MinimumElapsedForDropTrigger = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Maximum continuous session duration before an automatic break suggestion is shown.
    /// </summary>
    public static readonly TimeSpan MaxContinuousSessionDuration = TimeSpan.FromMinutes(20);

    private readonly Queue<bool> _currentWindowStrokes = new(WindowSize);
    private double? _baselineAccuracy;
    private int _consecutiveFatiguedWindows;
    private int _strokesSinceLastWindowCheck;
    private bool _hasTriggeredThisSession;
    private bool _isBreakSuggested;
    private string _suggestionReason = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the quiet break suggestion banner should currently be visible.
    /// </summary>
    public bool IsBreakSuggested => _isBreakSuggested;

    /// <summary>
    /// Gets a value indicating whether the fatigue detector has already fired during the current session.
    /// </summary>
    public bool HasTriggeredThisSession => _hasTriggeredThisSession;

    /// <summary>
    /// Gets the baseline accuracy percentage established from the first full window of the session.
    /// </summary>
    public double? BaselineAccuracy => _baselineAccuracy;

    /// <summary>
    /// Gets the human-readable coaching message for the break suggestion.
    /// </summary>
    public string SuggestionReason => _suggestionReason;

    /// <summary>
    /// Records a keystroke and evaluates rolling 30-keystroke windows and elapsed duration.
    /// </summary>
    /// <param name="isCorrect">True if the keystroke matched the expected character.</param>
    /// <param name="elapsed">Current elapsed session time.</param>
    /// <returns>True if a break suggestion is currently active.</returns>
    public bool RecordKeystroke(bool isCorrect, TimeSpan elapsed)
    {
        if (_hasTriggeredThisSession)
        {
            return _isBreakSuggested;
        }

        if (elapsed >= MaxContinuousSessionDuration)
        {
            Trigger("You have been typing continuously for 20 minutes — take a quick 2-minute stretch break.");
            return true;
        }

        if (_currentWindowStrokes.Count == WindowSize)
        {
            _currentWindowStrokes.Dequeue();
        }

        _currentWindowStrokes.Enqueue(isCorrect);
        _strokesSinceLastWindowCheck++;

        if (_currentWindowStrokes.Count == WindowSize && _strokesSinceLastWindowCheck >= WindowSize)
        {
            _strokesSinceLastWindowCheck = 0;
            double windowAccuracy = CalculateWindowAccuracy();

            if (!_baselineAccuracy.HasValue)
            {
                _baselineAccuracy = windowAccuracy;
                _consecutiveFatiguedWindows = 0;
            }
            else if (windowAccuracy <= _baselineAccuracy.Value - AccuracyDropThresholdPoints)
            {
                _consecutiveFatiguedWindows++;
                if (_consecutiveFatiguedWindows >= RequiredConsecutiveFatiguedWindows &&
                    elapsed >= MinimumElapsedForDropTrigger)
                {
                    Trigger("Accuracy dipped 10%+ over the last two 30-key windows — consider a short 2-minute rest break.");
                    return true;
                }
            }
            else
            {
                _consecutiveFatiguedWindows = 0;
            }
        }

        return _isBreakSuggested;
    }

    /// <summary>
    /// Evaluates elapsed session duration on timer ticks so the 20-minute cap triggers even without a new window boundary.
    /// </summary>
    /// <param name="elapsed">Current elapsed session duration.</param>
    /// <returns>True if a break suggestion is active.</returns>
    public bool EvaluateElapsed(TimeSpan elapsed)
    {
        if (!_hasTriggeredThisSession && elapsed >= MaxContinuousSessionDuration)
        {
            Trigger("You have been typing continuously for 20 minutes — take a quick 2-minute stretch break.");
        }

        return _isBreakSuggested;
    }

    /// <summary>
    /// Dismisses the break banner for the remainder of the current session without re-triggering.
    /// </summary>
    public void Dismiss()
    {
        _isBreakSuggested = false;
    }

    /// <summary>
    /// Resets all window state for a fresh typing session.
    /// </summary>
    public void Reset()
    {
        _currentWindowStrokes.Clear();
        _baselineAccuracy = null;
        _consecutiveFatiguedWindows = 0;
        _strokesSinceLastWindowCheck = 0;
        _hasTriggeredThisSession = false;
        _isBreakSuggested = false;
        _suggestionReason = string.Empty;
    }

    private double CalculateWindowAccuracy()
    {
        if (_currentWindowStrokes.Count == 0)
        {
            return 100.0;
        }

        int correctCount = 0;
        foreach (bool ok in _currentWindowStrokes)
        {
            if (ok)
            {
                correctCount++;
            }
        }

        return (double)correctCount / _currentWindowStrokes.Count * 100.0;
    }

    private void Trigger(string reason)
    {
        _hasTriggeredThisSession = true;
        _isBreakSuggested = true;
        _suggestionReason = reason;
    }
}
