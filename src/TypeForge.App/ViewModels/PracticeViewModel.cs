using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TypeForge.App.Services;
using TypeForge.Core.Engine;
using TypeForge.Core.Models;

namespace TypeForge.App.ViewModels;

public sealed partial class PracticeViewModel : ObservableObject
{
    private readonly ITypeForgeStateService _state;
    private readonly INavigationService _navigation;
    private TypingEngine _engine = null!;
    private TimeSpan? _timedLimit;

    [ObservableProperty]
    private string _promptTitle = string.Empty;

    [ObservableProperty]
    private string _wpmText = "0 WPM";

    [ObservableProperty]
    private string _accuracyText = "100.0%";

    [ObservableProperty]
    private string _mistakesText = "0 mistakes";

    [ObservableProperty]
    private string _elapsedText = "00:00";

    [ObservableProperty]
    private string _dailyGoalText = "0/30 min";

    [ObservableProperty]
    private double _dailyGoalProgress;

    [ObservableProperty]
    private char _activeTargetKey = ' ';

    [ObservableProperty]
    private bool _isResultsVisible;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _showKeyboard = true;

    [ObservableProperty]
    private KeyboardColorMode _keyboardColorMode = KeyboardColorMode.PerformanceHeat;

    [ObservableProperty]
    private int _typingFontSize = 28;

    [ObservableProperty]
    private string _resultWpm = "0";

    [ObservableProperty]
    private string _resultAccuracy = "100.0%";

    [ObservableProperty]
    private string _resultMistakes = "0";

    [ObservableProperty]
    private string _resultStarsDisplay = "☆☆☆";

    [ObservableProperty]
    private string _resultWeakKeysSummary = "All keys clean this session.";

    public TypingSnapshot? CurrentSnapshot { get; private set; }
    public SessionResult? LastResult { get; private set; }
    public IReadOnlyDictionary<char, KeyMetric> KeyMetrics => _state.AggregateKeyMetrics;
    public IReadOnlySet<char> UnlockedAdaptiveKeys =>
        _state.AdaptiveStates
            .Where(s => s.IsUnlocked)
            .Select(s => char.ToUpperInvariant(s.Character))
            .ToHashSet();
    public bool IsAdaptiveSession => _state.PendingSessionType == SessionType.AdaptivePractice;

    public event Action<TypingSnapshot>? SnapshotUpdated;

    public PracticeViewModel(ITypeForgeStateService state, INavigationService navigation)
    {
        _state = state;
        _navigation = navigation;
        IsMuted = !_state.Settings.SoundEnabled;
        LoadPendingSession();
    }

    public void LoadPendingSession()
    {
        ShowKeyboard = _state.Settings.ShowVirtualKeyboard;
        KeyboardColorMode = _state.Settings.KeyboardColorMode;
        TypingFontSize = _state.Settings.TypingFontSize;
        IsMuted = !_state.Settings.SoundEnabled;

        _timedLimit = _state.PendingTimedLimit;
        PromptTitle = _state.PendingPromptTitle;
        _engine = new TypingEngine(
            _state.PendingPromptText,
            stopOnError: _state.Settings.StopOnError,
            sessionType: _state.PendingSessionType,
            lessonId: _state.PendingLessonId);

        IsResultsVisible = false;
        LastResult = null;
        RefreshDailyGoal();
        PublishSnapshot();
    }

    public bool HandleCharacter(char inputChar)
    {
        if (IsResultsVisible)
        {
            return false;
        }

        bool accepted = _engine.ProcessKeystroke(inputChar);
        if (!accepted)
        {
            return false;
        }

        PublishSnapshot();
        return true;
    }

    public bool HandleBackspace()
    {
        if (IsResultsVisible)
        {
            return false;
        }

        bool moved = _engine.ProcessBackspace();
        if (moved)
        {
            PublishSnapshot();
        }

        return moved;
    }

    public void TickTimer()
    {
        if (IsResultsVisible || CurrentSnapshot is null || CurrentSnapshot.TotalKeystrokes == 0)
        {
            return;
        }

        TypingSnapshot snapshot = _engine.GetSnapshot();
        if (_timedLimit.HasValue && snapshot.Elapsed >= _timedLimit.Value)
        {
            FinalizeCompletedSession(snapshot);
            return;
        }

        UpdateHudMetrics(snapshot);
    }

    [RelayCommand]
    public void RestartSession()
    {
        _engine.Reset();
        IsResultsVisible = false;
        LastResult = null;
        RefreshDailyGoal();
        PublishSnapshot();
    }

    [RelayCommand]
    public void ReviewWeakKeys()
    {
        string drillText = _state.BuildWeakKeyDrillText(LastResult);
        _state.QueuePracticeSession("Weak-Key Drill", drillText, SessionType.AdaptivePractice);
        LoadPendingSession();
    }

    [RelayCommand]
    public void NextDrill()
    {
        string drillText = _state.BuildAdaptivePracticeText(LastResult);
        _state.QueuePracticeSession("Adaptive Practice · Keybr Focus", drillText, SessionType.AdaptivePractice);
        LoadPendingSession();
    }

    [RelayCommand]
    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        _state.Settings.SoundEnabled = !IsMuted;
        _state.SaveSettings();
    }

    [RelayCommand]
    public void ToggleFocusMode()
    {
        _navigation.SetFocusMode(!_navigation.IsFocusMode);
    }

    private void PublishSnapshot()
    {
        TypingSnapshot snapshot = _engine.GetSnapshot();
        CurrentSnapshot = snapshot;
        UpdateHudMetrics(snapshot);

        ActiveTargetKey = !snapshot.IsCompleted && snapshot.CursorIndex < snapshot.TargetText.Length
            ? snapshot.TargetText[snapshot.CursorIndex]
            : ' ';

        SnapshotUpdated?.Invoke(snapshot);

        if (snapshot.IsCompleted && !IsResultsVisible)
        {
            FinalizeCompletedSession(snapshot);
        }
    }

    private void UpdateHudMetrics(TypingSnapshot snapshot)
    {
        WpmText = $"{Math.Round(snapshot.LiveNetWpm):0} WPM";
        AccuracyText = $"{snapshot.LiveAccuracyPercentage:0.0}%";
        MistakesText = snapshot.Mistakes == 1 ? "1 mistake" : $"{snapshot.Mistakes} mistakes";

        TimeSpan displayTime = _timedLimit.HasValue
            ? (_timedLimit.Value - snapshot.Elapsed < TimeSpan.Zero ? TimeSpan.Zero : _timedLimit.Value - snapshot.Elapsed)
            : snapshot.Elapsed;
        ElapsedText = $"{(int)displayTime.TotalMinutes:00}:{displayTime.Seconds:00}";
    }

    private void FinalizeCompletedSession(TypingSnapshot snapshot)
    {
        SessionResult result = _engine.BuildResult();
        _state.RecordSession(result);
        LastResult = result;
        RefreshDailyGoal();

        (int stars, _) = _state.EvaluateCourseOutcome(result);
        ResultWpm = $"{Math.Round(result.NetWpm):0}";
        ResultAccuracy = $"{result.AccuracyPercentage:0.0}%";
        ResultMistakes = $"{result.Mistakes}";
        ResultStarsDisplay = new string('★', stars) + new string('☆', 3 - stars);

        List<string> weakKeys = result.KeyBreakdown.Values
            .Where(k => k.IncorrectKeystrokes > 0)
            .OrderByDescending(k => k.ErrorRatePercentage)
            .ThenByDescending(k => k.AverageLatencyMs)
            .Take(3)
            .Select(k => $"{k.Character} ({k.ErrorRatePercentage:0.#}% err)")
            .ToList();

        ResultWeakKeysSummary = weakKeys.Count > 0
            ? $"Weak keys: {string.Join(" · ", weakKeys)}"
            : "Zero errors — clean precision run.";

        IsResultsVisible = true;
        SnapshotUpdated?.Invoke(snapshot);
    }

    private void RefreshDailyGoal()
    {
        int goalMins = _state.Settings.DailyGoalMinutes;
        int doneMins = (int)Math.Floor(_state.TodayPracticedDuration.TotalMinutes);
        DailyGoalText = $"{doneMins}/{goalMins} min";
        DailyGoalProgress = goalMins <= 0 ? 0.0 : Math.Clamp(_state.TodayPracticedDuration.TotalMinutes / goalMins, 0.0, 1.0);
    }
}
