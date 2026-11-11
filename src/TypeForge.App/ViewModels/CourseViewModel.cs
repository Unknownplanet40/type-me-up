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
    private int _selectedExtraTrackIndex;

    public IReadOnlyList<CourseTrack> Tracks => _state.CourseTracks;
    public CourseTrack ActiveTrack => _state.SelectedCourseTrack ?? _state.CourseTracks[0];

    public IReadOnlyList<CourseTrack> ExtraTracks =>
        _state.CourseTracks.Where(t => t.TrackId is not "fast_touch" and not "speed_building").ToList();

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

    public string OptimalDurationLabel => OptimalDurationEnabled ? "Optimal Duration: On" : "Optimal Duration: Off";

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
        OnPropertyChanged(nameof(ActiveTrack));
        OnPropertyChanged(nameof(ActiveUnit));
        StateRefreshed?.Invoke();
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
        OnPropertyChanged(nameof(ActiveTrack));
        OnPropertyChanged(nameof(ActiveUnit));
        StateRefreshed?.Invoke();
    }

    [RelayCommand]
    private void StartSelectedExtraTrack()
    {
        IReadOnlyList<CourseTrack> extras = ExtraTracks;
        if (extras.Count == 0)
        {
            return;
        }

        int idx = Math.Clamp(SelectedExtraTrackIndex, 0, extras.Count - 1);
        SelectTrack(extras[idx].TrackId);
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
        _state.QueuePracticeSession(
            $"{ActiveTrack.Title} · {exercise.Title}",
            effectivePrompt,
            SessionType.CourseLesson,
            lessonId: exercise.ExerciseId);

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
