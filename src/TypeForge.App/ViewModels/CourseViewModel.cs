using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TypeForge.App.Services;
using TypeForge.Core.Models;

namespace TypeForge.App.ViewModels;

public sealed partial class CourseViewModel : ObservableObject
{
    private readonly ITypeForgeStateService _state;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private bool _isCoursePickerOpen;

    [ObservableProperty]
    private int _selectedUnitNumber = 1;

    [ObservableProperty]
    private CourseExercise? _activeTipExercise;

    [ObservableProperty]
    private bool _isPlacementWelcomeVisible;

    public IReadOnlyList<CourseTrack> Tracks => _state.CourseTracks;
    public CourseTrack ActiveTrack => _state.SelectedCourseTrack ?? _state.CourseTracks[0];

    public int CompletedLessonsCount => ActiveTrack.Units.Count(u => u.IsCompleted);
    public int TotalLessonsCount => ActiveTrack.Units.Count;
    public int RemainingLessonsCount => Math.Max(0, TotalLessonsCount - CompletedLessonsCount);
    public int TrackCompletionPercent => TotalLessonsCount == 0 ? 0 : (int)Math.Round(100.0 * CompletedLessonsCount / TotalLessonsCount);
    public double TrackLessonProgressFraction => TotalLessonsCount == 0 ? 0.0 : (double)CompletedLessonsCount / TotalLessonsCount;
    public int EarnedStarsCount => ActiveTrack.Units.Sum(u => u.Exercises.Sum(e => e.Stars));
    public int MaxStarsCount => ActiveTrack.Units.Sum(u => u.Exercises.Count(e => e.Kind != CourseStepKind.Tip) * 3);
    public bool IsTrackCompleted => TotalLessonsCount > 0 && CompletedLessonsCount == TotalLessonsCount;

    public string PrimaryActionLabel => ActiveTrack.CompletedExerciseCount == 0
        ? "Start Course"
        : IsTrackCompleted
            ? "Review Course"
            : "Continue Lesson";

    public bool OptimalDurationEnabled
    {
        get => _state.Settings.OptimalDurationEnabled;
        set
        {
            if (_state.Settings.OptimalDurationEnabled != value)
            {
                _state.Settings.OptimalDurationEnabled = value;
                _state.SaveSettings();
                OnPropertyChanged();
                OnPropertyChanged(nameof(OptimalDurationLabel));
                StateRefreshed?.Invoke();
            }
        }
    }

    public string OptimalDurationLabel => OptimalDurationEnabled ? "Skip ahead when mastered: On" : "Skip ahead when mastered: Off";

    public CourseUnit ActiveUnit
    {
        get
        {
            CourseTrack track = ActiveTrack;
            return track.Units.FirstOrDefault(u => u.UnitNumber == SelectedUnitNumber) ?? track.Units[0];
        }
    }

    public event Action? StateRefreshed;

    public CourseViewModel(ITypeForgeStateService state, INavigationService navigation)
    {
        _state = state;
        _navigation = navigation;
        SelectFirstIncompleteUnit();
    }

    public void RefreshOnNavigate()
    {
        SelectFirstIncompleteUnit();
        ActiveTipExercise = null;
        IsPlacementWelcomeVisible = _state.ShouldPromptPlacement;
        NotifyTrackAndUnitChanged();
        StateRefreshed?.Invoke();
    }

    public KeyMetric? GetKeyMetric(char key)
    {
        return _state.AggregateKeyMetrics.TryGetValue(char.ToLowerInvariant(key), out KeyMetric? metric)
            ? metric
            : _state.AggregateKeyMetrics.TryGetValue(key, out KeyMetric? exact)
                ? exact
                : null;
    }

    public string GetUnitStatusLabel(CourseUnit unit)
    {
        if (unit.IsCompleted)
        {
            return "Completed";
        }

        if (!unit.IsUnlocked)
        {
            return "Locked";
        }

        return unit.CompletedCount > 0 ? "In Progress" : "Available";
    }

    public string GetLockedUnitReason(CourseUnit unit)
    {
        return unit.UnitNumber <= 1
            ? "Available now"
            : $"Complete Lesson {unit.UnitNumber - 1} to unlock";
    }

    public string GetLockedExerciseReason(CourseExercise exercise)
    {
        return exercise.OrderIndex <= 1
            ? $"Complete Lesson {exercise.UnitNumber - 1} to unlock"
            : $"Complete step {exercise.OrderIndex - 1} to unlock";
    }

    private void NotifyTrackAndUnitChanged()
    {
        OnPropertyChanged(nameof(ActiveTrack));
        OnPropertyChanged(nameof(ActiveUnit));
        OnPropertyChanged(nameof(CompletedLessonsCount));
        OnPropertyChanged(nameof(TotalLessonsCount));
        OnPropertyChanged(nameof(RemainingLessonsCount));
        OnPropertyChanged(nameof(TrackCompletionPercent));
        OnPropertyChanged(nameof(TrackLessonProgressFraction));
        OnPropertyChanged(nameof(EarnedStarsCount));
        OnPropertyChanged(nameof(MaxStarsCount));
        OnPropertyChanged(nameof(IsTrackCompleted));
        OnPropertyChanged(nameof(PrimaryActionLabel));
    }

    [RelayCommand]
    public void StartPlacementFromCourse()
    {
        IsPlacementWelcomeVisible = false;
        _state.QueuePlacementTest();
        _navigation.NavigateTo("practice");
    }

    [RelayCommand]
    public void SkipPlacementFromCourse()
    {
        _state.DismissPlacementPrompt();
        IsPlacementWelcomeVisible = false;
    }

    [RelayCommand]
    private void SelectUnit(int unitNumber)
    {
        SelectedUnitNumber = unitNumber;
        ActiveTipExercise = ActiveUnit.Exercises.FirstOrDefault(e => e.Kind == CourseStepKind.Tip && e.IsUnlocked && !e.IsCompleted);
        OnPropertyChanged(nameof(ActiveUnit));
        StateRefreshed?.Invoke();
    }

    [RelayCommand]
    private void ToggleCoursePicker()
    {
        IsCoursePickerOpen = !IsCoursePickerOpen;
        StateRefreshed?.Invoke();
    }

    [RelayCommand]
    private void SelectTrack(string? trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
        {
            return;
        }

        _state.SelectCourseTrack(trackId);
        IsCoursePickerOpen = false;
        SelectFirstIncompleteUnit();
        ActiveTipExercise = null;
        NotifyTrackAndUnitChanged();
        StateRefreshed?.Invoke();
    }

    [RelayCommand]
    private void ToggleOptimalDuration()
    {
        OptimalDurationEnabled = !OptimalDurationEnabled;
    }

    [RelayCommand]
    private void ActivateExercise(CourseExercise? exercise)
    {
        if (exercise is null || !exercise.IsUnlocked)
        {
            return;
        }

        if (exercise.Kind == CourseStepKind.Tip)
        {
            ActiveTipExercise = exercise;
            StateRefreshed?.Invoke();
            return;
        }

        string effectivePrompt = _state.GetEffectiveExercisePrompt(exercise);
        CourseUnit? parentUnit = ActiveTrack.Units.FirstOrDefault(u => u.UnitNumber == exercise.UnitNumber);
        CourseExercise? precedingTip = parentUnit?.Exercises
            .Where(e => e.OrderIndex < exercise.OrderIndex && e.Kind == CourseStepKind.Tip)
            .OrderByDescending(e => e.OrderIndex)
            .FirstOrDefault();

        string? tipBanner = !string.IsNullOrWhiteSpace(exercise.TipContent)
            ? exercise.TipContent
            : precedingTip?.TipContent;

        _state.QueuePracticeSession(
            $"{ActiveTrack.Title} · {exercise.Title}",
            effectivePrompt,
            SessionType.CourseLesson,
            lessonId: exercise.ExerciseId,
            tipBannerText: tipBanner);

        _navigation.NavigateTo("practice");
    }

    [RelayCommand]
    private void CompleteActiveTip()
    {
        if (ActiveTipExercise is null)
        {
            return;
        }

        string completedId = ActiveTipExercise.ExerciseId;
        _state.MarkCourseTipCompleted(completedId);
        ActiveTipExercise = null;

        CourseExercise? next = ActiveUnit.Exercises.FirstOrDefault(e => e.IsUnlocked && !e.IsCompleted);
        if (next is { Kind: CourseStepKind.Tip })
        {
            ActiveTipExercise = next;
        }

        NotifyTrackAndUnitChanged();
        StateRefreshed?.Invoke();
    }

    [RelayCommand]
    private void ContinueCurrentLesson()
    {
        CourseExercise? next = ActiveTrack.Units
            .SelectMany(u => u.Exercises)
            .FirstOrDefault(e => e.IsUnlocked && !e.IsCompleted)
            ?? ActiveTrack.Units.SelectMany(u => u.Exercises).FirstOrDefault(e => e.IsUnlocked);

        if (next is null)
        {
            return;
        }

        SelectedUnitNumber = next.UnitNumber;
        ActivateExercise(next);
    }

    private void SelectFirstIncompleteUnit()
    {
        CourseTrack track = ActiveTrack;
        CourseUnit? target = track.Units.FirstOrDefault(u => u.IsUnlocked && !u.IsCompleted)
            ?? track.Units.LastOrDefault(u => u.IsUnlocked)
            ?? track.Units[0];
        SelectedUnitNumber = target.UnitNumber;
    }
}
