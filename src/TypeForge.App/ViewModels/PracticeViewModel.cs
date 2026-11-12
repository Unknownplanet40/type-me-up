using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TypeForge.App.Services;
using TypeForge.Core.Engine;
using TypeForge.Core.Models;
using TypeForge.Core.Services;

namespace TypeForge.App.ViewModels;

public sealed partial class PracticeViewModel : ObservableObject
{
    private readonly ITypeForgeStateService _state;
    private readonly INavigationService _navigation;
    private readonly IKeystrokeAudioService _audio;
    private TypingEngine _engine = null!;
    private TimeSpan? _timedLimit;
    private bool _engineStopOnError;
    private bool _engineForgiveErrors;
    private bool _engineSpaceSkipsWords;
    private int _consecutiveMissesOnCurrentChar;
    private PlacementRecommendation? _lastPlacementRecommendation;
    private readonly FatigueDetector _fatigueDetector = new();
    private InsightActionKind _lastRecommendedInsightAction = InsightActionKind.NextDrill;

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
    private string _dailyGoalText = "0/15 min";

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
    private KeyboardFadeStage _effectiveKeyboardFadeStage = KeyboardFadeStage.FullLabels;

    [ObservableProperty]
    private bool _showFingerGuide;

    [ObservableProperty]
    private bool _showWpmMetric = true;

    [ObservableProperty]
    private int _typingFontSize = 28;

    [ObservableProperty]
    private string _typingFontSelection = "Cascadia Mono";

    [ObservableProperty]
    private WhitespaceDisplayMode _whitespaceDisplay = WhitespaceDisplayMode.Bullet;

    [ObservableProperty]
    private CursorShapeMode _cursorShape = CursorShapeMode.Underline;

    [ObservableProperty]
    private CursorMovementMode _cursorMovement = CursorMovementMode.Smooth;

    [ObservableProperty]
    private bool _isSettingsDialogOpen;

    [ObservableProperty]
    private bool _canStartTodaysSession = true;

    [ObservableProperty]
    private bool _isDailyPlanActive;

    [ObservableProperty]
    private string _dailyPlanStepLabel = string.Empty;

    [ObservableProperty]
    private double _dailyPlanProgress;

    [ObservableProperty]
    private bool _hasTipBanner;

    [ObservableProperty]
    private string _tipBannerText = string.Empty;

    [ObservableProperty]
    private bool _hasWrongKeyFeedback;

    [ObservableProperty]
    private char _lastWrongPressedKey;

    [ObservableProperty]
    private char _lastExpectedKey;

    [ObservableProperty]
    private bool _hasConsecutiveMissHint;

    [ObservableProperty]
    private string? _wrongKeyFingerHint;

    [ObservableProperty]
    private bool _isPlacementWelcomeVisible;

    [ObservableProperty]
    private bool _isPlacementResultVisible;

    [ObservableProperty]
    private string _placementResultWpm = "0 WPM";

    [ObservableProperty]
    private string _placementResultAccuracy = "100.0%";

    [ObservableProperty]
    private string _placementRecommendedSummary = string.Empty;

    [ObservableProperty]
    private string _resultsHeading = "Session Complete";

    [ObservableProperty]
    private string _resultWpm = "0";

    [ObservableProperty]
    private string _resultAccuracy = "100.0%";

    [ObservableProperty]
    private string _resultMistakes = "0";

    [ObservableProperty]
    private string _resultRawWpm = "0";

    [ObservableProperty]
    private string _resultConsistency = "100%";

    [ObservableProperty]
    private bool _hasMissedWords;

    private IReadOnlyList<string> _lastMissedWords = [];

    [ObservableProperty]
    private string _resultStarsDisplay = "☆☆☆";

    [ObservableProperty]
    private string _resultWeakKeysSummary = "All keys clean this session.";

    [ObservableProperty]
    private string _resultInsightText = string.Empty;

    [ObservableProperty]
    private string _resultInsightActionLabel = "Next Drill";

    [ObservableProperty]
    private int _ghostCaretIndex;

    [ObservableProperty]
    private bool _isPaceCaretVisible;

    [ObservableProperty]
    private bool _isFatigueBannerVisible;

    [ObservableProperty]
    private string _fatigueBannerText = string.Empty;

    [ObservableProperty]
    private bool _isCourseLessonMode;

    [ObservableProperty]
    private bool _isStandardPracticeMode = true;

    [ObservableProperty]
    private string _courseBreadcrumbText = string.Empty;

    [ObservableProperty]
    private bool _isLessonCompletedOutcome;

    [ObservableProperty]
    private bool _canContinueCourseExercise = true;

    [ObservableProperty]
    private string _courseResultsPrimaryLabel = "Next Exercise";

    [ObservableProperty]
    private string _courseResultsSecondaryLabel = "Retry Exercise";

    [ObservableProperty]
    private string _courseResultsSummaryText = string.Empty;

    public TypingSnapshot? CurrentSnapshot { get; private set; }
    public SessionResult? LastResult { get; private set; }
    public UserSettings CurrentSettings => _state.Settings;
    public IReadOnlyDictionary<char, KeyMetric> KeyMetrics => _state.AggregateKeyMetrics;
    public IReadOnlySet<char> UnlockedAdaptiveKeys =>
        _state.AdaptiveStates
            .Where(s => s.IsUnlocked)
            .Select(s => char.ToUpperInvariant(s.Character))
            .ToHashSet();
    public bool IsAdaptiveSession => _state.PendingSessionType == SessionType.AdaptivePractice;
    public int ConsecutiveMissesOnCurrentChar => _consecutiveMissesOnCurrentChar;

    public event Action<TypingSnapshot>? SnapshotUpdated;

    public PracticeViewModel(
        ITypeForgeStateService state,
        INavigationService navigation,
        IKeystrokeAudioService audio)
    {
        _state = state;
        _navigation = navigation;
        _audio = audio;
        IsMuted = !_state.Settings.SoundEnabled;
        LoadPendingSession();
    }

    public void LoadPendingSession()
    {
        ShowKeyboard = _state.Settings.ShowVirtualKeyboard;
        KeyboardColorMode = _state.Settings.KeyboardColorMode;
        TypingFontSize = _state.Settings.TypingFontSize;
        TypingFontSelection = _state.Settings.TypingFontSelection;
        WhitespaceDisplay = _state.Settings.WhitespaceDisplay;
        CursorShape = _state.Settings.CursorShape;
        CursorMovement = _state.Settings.CursorMovement;
        IsMuted = !_state.Settings.SoundEnabled || _state.Settings.SoundMode == SoundFeedbackMode.None;

        _timedLimit = _state.PendingTimedLimit;
        PromptTitle = _state.PendingPromptTitle;
        _engineStopOnError = _state.Settings.StopOnError;
        _engineForgiveErrors = _state.Settings.ForgiveErrors;
        _engineSpaceSkipsWords = _state.Settings.SpaceSkipsWords;
        _engine = new TypingEngine(
            _state.PendingPromptText,
            stopOnError: _engineStopOnError,
            sessionType: _state.PendingSessionType,
            lessonId: _state.PendingLessonId,
            forgiveErrors: _engineForgiveErrors,
            spaceSkipsWords: _engineSpaceSkipsWords);

        _fatigueDetector.Reset();
        IsFatigueBannerVisible = false;
        FatigueBannerText = string.Empty;
        ClearMissFeedbackState();
        UpdateCourseAndGuideOptions();
        UpdateDailyPlanStrip();

        IsResultsVisible = false;
        IsPlacementResultVisible = false;
        IsPlacementWelcomeVisible = _state.ShouldPromptPlacement && _state.PendingSessionType != SessionType.PlacementTest;
        LastResult = null;
        RefreshDailyGoal();
        PublishSnapshot();
    }

    public void OpenSettingsDialog()
    {
        IsSettingsDialogOpen = true;
    }

    public void ApplySettingsAndClose()
    {
        _state.SaveSettings();

        bool behaviorChanged = _engineStopOnError != _state.Settings.StopOnError
            || _engineForgiveErrors != _state.Settings.ForgiveErrors
            || _engineSpaceSkipsWords != _state.Settings.SpaceSkipsWords;

        IsSettingsDialogOpen = false;
        if (behaviorChanged)
        {
            LoadPendingSession();
            return;
        }

        ShowKeyboard = _state.Settings.ShowVirtualKeyboard;
        KeyboardColorMode = _state.Settings.KeyboardColorMode;
        TypingFontSize = _state.Settings.TypingFontSize;
        TypingFontSelection = _state.Settings.TypingFontSelection;
        WhitespaceDisplay = _state.Settings.WhitespaceDisplay;
        CursorShape = _state.Settings.CursorShape;
        CursorMovement = _state.Settings.CursorMovement;
        IsMuted = !_state.Settings.SoundEnabled || _state.Settings.SoundMode == SoundFeedbackMode.None;
        UpdateCourseAndGuideOptions();
        RefreshDailyGoal();
        PublishSnapshot();
    }

    [RelayCommand]
    public void StartTodaysSession()
    {
        _state.StartTodaysSession();
        LoadPendingSession();
    }

    [RelayCommand]
    public void CancelTodaysSession()
    {
        _state.CancelDailyPlan();
        LoadPendingSession();
    }

    [RelayCommand]
    public void StartPlacementCheck()
    {
        _state.QueuePlacementTest();
        IsPlacementWelcomeVisible = false;
        LoadPendingSession();
    }

    [RelayCommand]
    public void SkipPlacementCheck()
    {
        _state.DismissPlacementPrompt();
        IsPlacementWelcomeVisible = false;
        UpdateActionButtonsVisibility(CurrentSnapshot);
    }

    [RelayCommand]
    public void AcceptPlacementRecommendation()
    {
        if (_lastPlacementRecommendation is not null)
        {
            _state.ApplyPlacementRecommendation(_lastPlacementRecommendation);
        }

        IsPlacementResultVisible = false;
        LoadPendingSession();
    }

    [RelayCommand]
    public void PickCourseMyselfAfterPlacement()
    {
        IsPlacementResultVisible = false;
        _navigation.NavigateTo("course");
    }

    public bool HandleCharacter(char inputChar)
    {
        if (IsResultsVisible || IsSettingsDialogOpen || IsPlacementWelcomeVisible || IsPlacementResultVisible)
        {
            return false;
        }

        TypingSnapshot preSnapshot = _engine.GetSnapshot();
        char expectedChar = preSnapshot.CursorIndex < preSnapshot.TargetText.Length
            ? preSnapshot.TargetText[preSnapshot.CursorIndex]
            : ' ';
        bool isExpectedChar = preSnapshot.CursorIndex < preSnapshot.TargetText.Length
            && expectedChar == inputChar;

        bool accepted = _engine.ProcessKeystroke(inputChar);
        if (!accepted)
        {
            return false;
        }

        if (isExpectedChar)
        {
            ClearMissFeedbackState();
        }
        else
        {
            _consecutiveMissesOnCurrentChar++;
            LastWrongPressedKey = inputChar;
            LastExpectedKey = expectedChar;
            HasConsecutiveMissHint = _consecutiveMissesOnCurrentChar >= 2;
            WrongKeyFingerHint = HasConsecutiveMissHint ? FingerMap.BuildReachHint(expectedChar) : null;
            HasWrongKeyFeedback = _engineStopOnError || HasConsecutiveMissHint;
        }

        UpdateCourseAndGuideOptions();
        if (_fatigueDetector.RecordKeystroke(isExpectedChar, _engine.GetSnapshot().Elapsed))
        {
            IsFatigueBannerVisible = _fatigueDetector.IsBreakSuggested;
            FatigueBannerText = _fatigueDetector.SuggestionReason;
        }

        if (!IsMuted)
        {
            if (isExpectedChar)
            {
                _audio.PlayClick();
            }
            else
            {
                _audio.PlayMistake();
            }
        }

        PublishSnapshot();
        return true;
    }

    public bool HandleBackspace()
    {
        if (IsResultsVisible || IsSettingsDialogOpen || IsPlacementWelcomeVisible || IsPlacementResultVisible)
        {
            return false;
        }

        bool moved = _engine.ProcessBackspace();
        if (moved)
        {
            ClearMissFeedbackState();
            UpdateCourseAndGuideOptions();
            PublishSnapshot();
        }

        return moved;
    }

    public void TickTimer()
    {
        if (IsResultsVisible || IsPlacementWelcomeVisible || IsPlacementResultVisible)
        {
            return;
        }

        TypingSnapshot snapshot = _engine.GetSnapshot();
        if (snapshot.TotalKeystrokes == 0)
        {
            return;
        }

        UpdateHudMetrics(snapshot);
        UpdatePaceCaretState(snapshot);
        if (_fatigueDetector.EvaluateElapsed(snapshot.Elapsed))
        {
            IsFatigueBannerVisible = _fatigueDetector.IsBreakSuggested;
            FatigueBannerText = _fatigueDetector.SuggestionReason;
        }

        if (_timedLimit.HasValue && snapshot.Elapsed >= _timedLimit.Value)
        {
            FinalizeCompletedSession(snapshot);
            return;
        }

        SnapshotUpdated?.Invoke(snapshot);
    }

    [RelayCommand]
    public void RestartSession()
    {
        _engine.Reset();
        _fatigueDetector.Reset();
        IsFatigueBannerVisible = false;
        FatigueBannerText = string.Empty;
        ClearMissFeedbackState();
        UpdateCourseAndGuideOptions();
        IsResultsVisible = false;
        IsPlacementResultVisible = false;
        PublishSnapshot();
    }

    [RelayCommand]
    public void DismissFatigueBanner()
    {
        _fatigueDetector.Dismiss();
        IsFatigueBannerVisible = false;
    }

    [RelayCommand]
    public void DrillSlowestPairs()
    {
        _state.QueueSlowestBigramDrill();
        LoadPendingSession();
    }

    [RelayCommand]
    public void ExecuteInsightAction()
    {
        switch (_lastRecommendedInsightAction)
        {
            case InsightActionKind.ReviewWeakKeys:
                ReviewWeakKeys();
                break;
            case InsightActionKind.DrillSlowestBigrams:
                DrillSlowestPairs();
                break;
            case InsightActionKind.RepeatDrill:
                RestartSession();
                break;
            default:
                NextDrill();
                break;
        }
    }

    [RelayCommand]
    public void PracticeMissedWords()
    {
        if (_lastMissedWords.Count == 0)
        {
            return;
        }

        _state.CancelDailyPlan();
        string drillText = TypingEngine.BuildMissedWordsDrillText(_lastMissedWords);
        _state.QueuePracticeSession("Missed Words Drill", drillText, SessionType.AdaptivePractice);
        LoadPendingSession();
    }

    [RelayCommand]
    public void ReviewWeakKeys()
    {
        _state.CancelDailyPlan();
        string drillText = _state.BuildWeakKeyDrillText(LastResult);
        _state.QueuePracticeSession("Weak-Key Drill", drillText, SessionType.AdaptivePractice);
        LoadPendingSession();
    }

    [RelayCommand]
    public void NextDrill()
    {
        _state.QueueNextDrill(LastResult);
        LoadPendingSession();
    }

    [RelayCommand]
    public void ToggleMute()
    {
        IsMuted = !IsMuted;
        _state.Settings.SoundEnabled = !IsMuted;
        if (!IsMuted && _state.Settings.SoundMode == SoundFeedbackMode.None)
        {
            _state.Settings.SoundMode = SoundFeedbackMode.All;
        }

        _state.SaveSettings();
    }

    [RelayCommand]
    public void BackToCourse()
    {
        IsResultsVisible = false;
        _navigation.NavigateTo("course");
    }

    [RelayCommand]
    public void ContinueCourseFlow()
    {
        if (!CanContinueCourseExercise)
        {
            return;
        }

        if (IsLessonCompletedOutcome)
        {
            CourseExercise? current = _state.GetCourseExercise(_state.PendingLessonId);
            CourseTrack? track = current is not null
                ? _state.CourseTracks.FirstOrDefault(t => string.Equals(t.TrackId, current.TrackId, StringComparison.OrdinalIgnoreCase))
                : _state.SelectedCourseTrack;

            CourseUnit? nextUnit = current is not null && track is not null
                ? track.Units.FirstOrDefault(u => u.UnitNumber > current.UnitNumber && u.IsUnlocked && !u.IsCompleted)
                : null;

            CourseExercise? nextEx = nextUnit?.Exercises.FirstOrDefault(e => e.Kind != CourseStepKind.Tip && e.IsUnlocked && !e.IsCompleted)
                ?? nextUnit?.Exercises.FirstOrDefault(e => e.Kind != CourseStepKind.Tip && e.IsUnlocked);

            if (nextEx is null || track is null)
            {
                IsResultsVisible = false;
                _navigation.NavigateTo("course");
                return;
            }

            CourseExercise? tip = nextUnit?.Exercises
                .Where(e => e.OrderIndex < nextEx.OrderIndex && e.Kind == CourseStepKind.Tip)
                .OrderByDescending(e => e.OrderIndex)
                .FirstOrDefault();

            _state.QueuePracticeSession(
                $"{track.Title} · {nextEx.Title}",
                _state.GetEffectiveExercisePrompt(nextEx),
                SessionType.CourseLesson,
                lessonId: nextEx.ExerciseId,
                tipBannerText: !string.IsNullOrWhiteSpace(nextEx.TipContent) ? nextEx.TipContent : tip?.TipContent);
            LoadPendingSession();
            return;
        }

        NextDrill();
    }

    [RelayCommand]
    public void RepeatCourseLesson()
    {
        if (IsLessonCompletedOutcome)
        {
            CourseExercise? current = _state.GetCourseExercise(_state.PendingLessonId);
            CourseTrack? track = current is not null
                ? _state.CourseTracks.FirstOrDefault(t => string.Equals(t.TrackId, current.TrackId, StringComparison.OrdinalIgnoreCase))
                : _state.SelectedCourseTrack;
            CourseUnit? unit = current is not null && track is not null
                ? track.Units.FirstOrDefault(u => u.UnitNumber == current.UnitNumber)
                : null;
            CourseExercise? firstTyped = unit?.Exercises.FirstOrDefault(e => e.Kind != CourseStepKind.Tip && e.IsUnlocked);

            if (firstTyped is not null && track is not null)
            {
                _state.QueuePracticeSession(
                    $"{track.Title} · {firstTyped.Title}",
                    _state.GetEffectiveExercisePrompt(firstTyped),
                    SessionType.CourseLesson,
                    lessonId: firstTyped.ExerciseId,
                    tipBannerText: firstTyped.TipContent);
                LoadPendingSession();
                return;
            }
        }

        RestartSession();
    }

    [RelayCommand]
    public void ToggleFocusMode()
    {
        _navigation.SetFocusMode(!_navigation.IsFocusMode);
    }

    private void ClearMissFeedbackState()
    {
        _consecutiveMissesOnCurrentChar = 0;
        HasWrongKeyFeedback = false;
        HasConsecutiveMissHint = false;
        WrongKeyFingerHint = null;
        LastWrongPressedKey = '\0';
        LastExpectedKey = '\0';
    }

    private void UpdateCourseAndGuideOptions()
    {
        CourseExercise? exercise = _state.PendingSessionType == SessionType.CourseLesson
            ? _state.GetCourseExercise(_state.PendingLessonId)
            : null;

        ShowWpmMetric = exercise is null || exercise.ShouldDisplayWpm(_state.Settings.ShowWpmInCourse);
        EffectiveKeyboardFadeStage = exercise is not null
            ? exercise.GetEffectiveKeyboardFadeStage(_state.Settings.KeyboardFadeStage)
            : _state.Settings.KeyboardFadeStage;

        ShowFingerGuide = _state.Settings.FingerGuideMode == FingerGuideMode.AlwaysOn
            || (_state.Settings.FingerGuideMode == FingerGuideMode.CourseDefault && _state.PendingSessionType == SessionType.CourseLesson)
            || HasConsecutiveMissHint;

        TipBannerText = _state.PendingTipBannerText ?? string.Empty;
        HasTipBanner = !string.IsNullOrWhiteSpace(TipBannerText);

        IsCourseLessonMode = _state.PendingSessionType == SessionType.CourseLesson && exercise is not null;
        IsStandardPracticeMode = !IsCourseLessonMode;
        if (exercise is not null)
        {
            CourseTrack? track = _state.CourseTracks.FirstOrDefault(t => string.Equals(t.TrackId, exercise.TrackId, StringComparison.OrdinalIgnoreCase));
            string trackName = track?.Title ?? "Course";
            CourseBreadcrumbText = $"{trackName} / Lesson {exercise.UnitNumber:00} / {exercise.Title}";
        }
        else
        {
            CourseBreadcrumbText = string.Empty;
        }
    }

    private void UpdateDailyPlanStrip()
    {
        DailySessionPlan? plan = _state.ActiveDailyPlan;
        if (plan is null || plan.Steps.Count == 0)
        {
            IsDailyPlanActive = false;
            DailyPlanStepLabel = string.Empty;
            DailyPlanProgress = 0.0;
            return;
        }

        int stepIdx = Math.Clamp(_state.ActiveDailyPlanStepIndex, 0, plan.Steps.Count - 1);
        PlannedSessionStep current = plan.Steps[stepIdx];
        IsDailyPlanActive = true;
        DailyPlanStepLabel = $"Today's Session · Step {stepIdx + 1} of {plan.Steps.Count} · {current.Title} ({current.Subtitle})";
        DailyPlanProgress = (double)(stepIdx + 1) / plan.Steps.Count;
    }

    private void UpdateActionButtonsVisibility(TypingSnapshot? snapshot)
    {
        bool notStarted = snapshot is null || (snapshot.TotalKeystrokes == 0 && !snapshot.IsCompleted);
        CanStartTodaysSession = notStarted
            && !IsResultsVisible
            && !IsPlacementWelcomeVisible
            && !IsPlacementResultVisible
            && _state.ActiveDailyPlan is null
            && _state.PendingSessionType != SessionType.PlacementTest;
    }

    private void PublishSnapshot()
    {
        TypingSnapshot snapshot = _engine.GetSnapshot();
        CurrentSnapshot = snapshot;
        UpdateHudMetrics(snapshot);
        UpdatePaceCaretState(snapshot);
        UpdateActionButtonsVisibility(snapshot);

        ActiveTargetKey = !snapshot.IsCompleted && snapshot.CursorIndex < snapshot.TargetText.Length
            ? snapshot.TargetText[snapshot.CursorIndex]
            : ' ';

        SnapshotUpdated?.Invoke(snapshot);

        if (snapshot.IsCompleted && !IsResultsVisible && !IsPlacementResultVisible)
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
        IReadOnlyDictionary<string, BigramMetric> bigrams = _engine.GetBigramBreakdown();
        _state.RecordSession(result, bigrams);
        LastResult = result;
        RefreshDailyGoal();

        if (result.SessionType == SessionType.PlacementTest)
        {
            PlacementRecommendation rec = _state.EvaluateAndSavePlacement(result);
            _lastPlacementRecommendation = rec;
            PlacementResultWpm = $"{Math.Round(result.NetWpm):0}";
            PlacementResultAccuracy = $"{result.AccuracyPercentage:0.0}%";
            PlacementRecommendedSummary = rec.Summary;
            IsPlacementResultVisible = true;
            UpdateActionButtonsVisibility(snapshot);
            SnapshotUpdated?.Invoke(snapshot);
            return;
        }

        if (_state.ActiveDailyPlan is not null)
        {
            bool hasMoreSteps = _state.AdvanceDailyPlanStep();
            if (hasMoreSteps)
            {
                LoadPendingSession();
                return;
            }

            ResultsHeading = "Today's Session Complete";
        }
        else
        {
            ResultsHeading = "Session Complete";
        }

        (int stars, bool passed) = _state.EvaluateCourseOutcome(result);
        if (result.SessionType == SessionType.CourseLesson)
        {
            CourseExercise? completedExercise = _state.GetCourseExercise(result.LessonId);
            CourseTrack? parentTrack = completedExercise is not null
                ? _state.CourseTracks.FirstOrDefault(t => string.Equals(t.TrackId, completedExercise.TrackId, StringComparison.OrdinalIgnoreCase))
                : null;
            CourseUnit? parentUnit = completedExercise is not null && parentTrack is not null
                ? parentTrack.Units.FirstOrDefault(u => u.UnitNumber == completedExercise.UnitNumber)
                : null;

            bool unitCompleted = parentUnit is { IsCompleted: true };
            IsLessonCompletedOutcome = unitCompleted;
            CanContinueCourseExercise = passed;

            if (unitCompleted && parentUnit is not null && passed)
            {
                int unitStars = parentUnit.Exercises.Sum(e => e.Stars);
                int unitMaxStars = parentUnit.Exercises.Count(e => e.Kind != CourseStepKind.Tip) * 3;
                bool hasNextUnit = parentTrack is not null && parentTrack.Units.Any(u => u.UnitNumber > parentUnit.UnitNumber && u.IsUnlocked);

                ResultsHeading = $"Lesson {parentUnit.UnitNumber:00} Complete";
                CourseResultsSummaryText = $"{parentUnit.Title} mastered · {unitStars} / {unitMaxStars} stars earned";
                CourseResultsPrimaryLabel = hasNextUnit ? "Continue to Next Lesson" : "Return to Course";
                CourseResultsSecondaryLabel = "Repeat Lesson";
            }
            else if (passed)
            {
                ResultsHeading = "Exercise Completed";
                CourseResultsSummaryText = "Target met — next exercise is unlocked.";
                CourseResultsPrimaryLabel = "Next Exercise";
                CourseResultsSecondaryLabel = "Retry Exercise";
            }
            else
            {
                IReadOnlyList<char> missedFocus = _state.Settings.CoursePassGateEnabled
                    ? _state.GetMissedFocusKeysForResult(result)
                    : [];

                double requiredAcc = _state.Settings.CoursePassGateEnabled
                    ? Math.Max(completedExercise?.MinimumAccuracy ?? 90.0, _state.Settings.CourseMinAccuracy)
                    : (completedExercise?.MinimumAccuracy ?? 90.0);

                ResultsHeading = "Exercise Not Passed";
                if (missedFocus.Count > 0)
                {
                    string missedList = string.Join(", ", missedFocus.Select(char.ToUpperInvariant));
                    CourseResultsSummaryText = $"Final lesson check: 100% accuracy required on all lesson focus keys (missed: {missedList}).";
                }
                else
                {
                    CourseResultsSummaryText = _state.Settings.CoursePassGateEnabled
                        ? $"Need ≥ {requiredAcc:0}% accuracy (got {result.AccuracyPercentage:0.0}%) and ≤ {_state.Settings.CourseMaxMistakes} mistakes (got {result.Mistakes}) to proceed."
                        : $"Need ≥ {requiredAcc:0}% accuracy (got {result.AccuracyPercentage:0.0}%) to proceed.";
                }

                CourseResultsPrimaryLabel = "Next Exercise";
                CourseResultsSecondaryLabel = "Retry Exercise";
            }
        }
        else
        {
            IsLessonCompletedOutcome = false;
            CanContinueCourseExercise = true;
            CourseResultsSummaryText = string.Empty;
        }
        ResultWpm = $"{Math.Round(result.NetWpm):0}";
        ResultRawWpm = $"{Math.Round(result.GrossWpm):0}";
        ResultAccuracy = $"{result.AccuracyPercentage:0.0}%";
        ResultMistakes = $"{result.Mistakes}";
        ResultConsistency = $"{_engine.GetConsistencyPercentage():0.0}%";
        _lastMissedWords = _engine.GetMissedWords();
        HasMissedWords = _lastMissedWords.Count > 0;
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

        SessionInsight insight = InsightEngine.Evaluate(
            result,
            _state.Sessions,
            _state.AggregateKeyMetrics,
            _state.AggregateBigramMetrics);
        ResultInsightText = insight.PrimaryInsight;
        ResultInsightActionLabel = insight.ActionLabel;
        _lastRecommendedInsightAction = insight.RecommendedAction;

        IsResultsVisible = true;
        UpdateCourseAndGuideOptions();
        UpdatePaceCaretState(snapshot);
        UpdateActionButtonsVisibility(snapshot);
        SnapshotUpdated?.Invoke(snapshot);
    }

    private void UpdatePaceCaretState(TypingSnapshot snapshot)
    {
        if (!ShowWpmMetric
            || _state.Settings.PaceCaretMode == PaceCaretMode.Off
            || snapshot.TotalKeystrokes == 0
            || snapshot.IsCompleted)
        {
            IsPaceCaretVisible = false;
            GhostCaretIndex = 0;
            return;
        }

        double targetWpm = PaceCaretCalculator.ResolveTargetWpm(
            _state.Settings.PaceCaretMode,
            _state.Settings.PaceTargetWpm,
            _state.Sessions);

        if (targetWpm <= 0.0)
        {
            IsPaceCaretVisible = false;
            GhostCaretIndex = 0;
            return;
        }

        GhostCaretIndex = PaceCaretCalculator.GetGhostIndex(snapshot.Elapsed, targetWpm, snapshot.TargetText.Length);
        IsPaceCaretVisible = true;
    }

    private void RefreshDailyGoal()
    {
        int goalMins = _state.Settings.DailyGoalMinutes;
        int doneMins = (int)Math.Floor(_state.TodayPracticedDuration.TotalMinutes);
        DailyGoalText = $"{doneMins}/{goalMins} min";
        DailyGoalProgress = goalMins <= 0 ? 0.0 : Math.Clamp(_state.TodayPracticedDuration.TotalMinutes / goalMins, 0.0, 1.0);
    }
}
