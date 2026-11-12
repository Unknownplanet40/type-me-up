using TypeForge.Core.Engine;
using TypeForge.Core.Models;
using TypeForge.Core.Services;

namespace TypeForge.App.Services;

/// <summary>
/// Coordinates persistent SQLite state, multi-track course catalog progression, adaptive engine state, daily session plans, placement evaluation, and active drill queueing.
/// </summary>
public interface ITypeForgeStateService
{
    UserSettings Settings { get; }
    IReadOnlyList<SessionResult> Sessions { get; }
    IReadOnlyDictionary<char, KeyMetric> AggregateKeyMetrics { get; }
    IReadOnlyDictionary<string, BigramMetric> AggregateBigramMetrics { get; }
    IReadOnlyDictionary<char, KeyReviewScheduleRecord> ReviewSchedules { get; }
    IReadOnlyList<PlacementResultRecord> PlacementResults { get; }
    IReadOnlyList<CourseLevel> CourseLevels { get; }
    IReadOnlyList<CourseTrack> CourseTracks { get; }
    CourseTrack? SelectedCourseTrack { get; }
    IReadOnlyList<AdaptiveKeyState> AdaptiveStates { get; }
    IReadOnlyList<TimedPassage> TimedPassages { get; }
    IReadOnlyList<BookModel> Books { get; }
    IReadOnlyList<RealWorldPoolModel> RealWorldPools { get; }
    IReadOnlyList<string> DictionaryWords { get; }
    TimeSpan TodayPracticedDuration { get; }
    int StreakDays { get; }

    string PendingPromptTitle { get; }
    string PendingPromptText { get; }
    SessionType PendingSessionType { get; }
    string? PendingLessonId { get; }
    TimeSpan? PendingTimedLimit { get; }
    string? PendingTipBannerText { get; }
    string? PendingCompanionTipId { get; }

    bool ShouldPromptPlacement { get; }
    bool ShouldShowProfilePickerOnLaunch { get; }
    UserProfile ActiveProfile { get; }
    IReadOnlyList<UserProfile> Profiles { get; }
    IReadOnlyList<string> ProfileAvatarPalette { get; }
    DailySessionPlan? ActiveDailyPlan { get; }
    int ActiveDailyPlanStepIndex { get; }
    PlannedSessionStep? CurrentPlannedStep { get; }

    event Action? StateChanged;

    void Initialize();
    void SaveSettings();
    void SelectCourseTrack(string trackId);
    void MarkCourseTipCompleted(string exerciseId);
    CourseExercise? GetCourseExercise(string? exerciseId);
    string GetEffectiveExercisePrompt(CourseExercise exercise);

    void QueuePracticeSession(
        string title,
        string promptText,
        SessionType sessionType,
        string? lessonId = null,
        TimeSpan? timedLimit = null,
        string? tipBannerText = null,
        string? companionTipId = null);

    DailySessionPlan StartTodaysSession();
    bool AdvanceDailyPlanStep();
    void CancelDailyPlan();

    void DismissPlacementPrompt();
    void QueuePlacementTest();
    PlacementRecommendation EvaluateAndSavePlacement(SessionResult result);
    void ApplyPlacementRecommendation(PlacementRecommendation recommendation);

    void RecordSession(SessionResult result, IReadOnlyDictionary<string, BigramMetric>? sessionBigrams = null);
    string BuildAdaptivePracticeText(SessionResult? sourceResult = null);
    string BuildWeakKeyDrillText(SessionResult? sourceResult = null);
    string BuildSlowestBigramDrillText();
    void QueueSlowestBigramDrill();
    void QueueNextDrill(SessionResult? sourceResult = null);
    (int Stars, bool Passed) EvaluateCourseOutcome(SessionResult result);
    IReadOnlyList<char> GetMissedFocusKeysForResult(SessionResult result);

    void DismissLaunchProfilePicker();
    void SwitchProfile(string profileId);
    UserProfile CreateAndSwitchProfile(string name, string? avatarColorHex = null);
    void RenameProfile(string profileId, string newName);
    void DeleteProfile(string profileId);
    void ResetCurrentProfileProgress();
    IReadOnlyList<ProfileRankingEntry> GetProfileRankings();
}

public sealed class TypeForgeStateService : ITypeForgeStateService
{
    private sealed class KeyAggregate
    {
        public int Total { get; set; }
        public int Correct { get; set; }
        public int Incorrect { get; set; }
        public double WeightedLatencySum { get; set; }

        public KeyMetric ToMetric(char key)
        {
            double avg = Total == 0 ? 0.0 : Math.Round(WeightedLatencySum / Total, 2);
            return new KeyMetric(key, Total, Correct, Incorrect, avg);
        }
    }

    private readonly ITypeForgeRepository _repository;
    private readonly IProfileService _profileService;
    private readonly CourseProgressionService _courseProgression;
    private readonly CourseCatalogService _courseCatalog;
    private readonly IKeystrokeAudioService _audioService;
    private readonly SessionPlanner _sessionPlanner = new();
    private readonly PlacementEngine _placementEngine = new();
    private readonly ReviewScheduler _reviewScheduler = new();

    private readonly List<SessionResult> _sessions = [];
    private readonly Dictionary<char, KeyAggregate> _keyAggregates = [];
    private readonly Dictionary<string, BigramMetric> _bigramAggregates = new(StringComparer.Ordinal);
    private readonly Dictionary<char, KeyReviewScheduleRecord> _reviewSchedules = [];
    private readonly List<PlacementResultRecord> _placementResults = [];
    private readonly Dictionary<string, LessonModel> _lessonLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CourseExercise> _exerciseLookup = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<CourseLevel> _courseLevels = [];
    private IReadOnlyList<CourseTrack> _courseTracks = [];
    private IReadOnlyList<AdaptiveKeyState> _adaptiveStates = AdaptiveEngine.CreateInitialStates();
    private IReadOnlyList<TimedPassage> _timedPassages = [];
    private IReadOnlyList<BookModel> _books = [];
    private IReadOnlyList<RealWorldPoolModel> _realWorldPools = [];
    private IReadOnlyList<string> _dictionaryWords = [];
    private bool _initialized;
    private bool _placementPromptDismissed;

    public UserSettings Settings { get; private set; } = new();
    public IReadOnlyList<SessionResult> Sessions => _sessions;
    public IReadOnlyDictionary<char, KeyMetric> AggregateKeyMetrics =>
        _keyAggregates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToMetric(kvp.Key));
    public IReadOnlyDictionary<string, BigramMetric> AggregateBigramMetrics => _bigramAggregates;
    public IReadOnlyDictionary<char, KeyReviewScheduleRecord> ReviewSchedules => _reviewSchedules;
    public IReadOnlyList<PlacementResultRecord> PlacementResults => _placementResults;
    public IReadOnlyList<CourseLevel> CourseLevels => _courseLevels;
    public IReadOnlyList<CourseTrack> CourseTracks => _courseTracks;
    public CourseTrack? SelectedCourseTrack => _courseCatalog.GetTrack(Settings.SelectedCourseTrackId);
    public IReadOnlyList<AdaptiveKeyState> AdaptiveStates => _adaptiveStates;
    public IReadOnlyList<TimedPassage> TimedPassages => _timedPassages;
    public IReadOnlyList<BookModel> Books => _books;
    public IReadOnlyList<RealWorldPoolModel> RealWorldPools => _realWorldPools;
    public IReadOnlyList<string> DictionaryWords => _dictionaryWords;
    public TimeSpan TodayPracticedDuration { get; private set; } = TimeSpan.Zero;
    public int StreakDays { get; private set; }

    public string PendingPromptTitle { get; private set; } = "Adaptive Practice · Keybr Focus";
    public string PendingPromptText { get; private set; } = "fjf jfj fff jjj fjfj jfjf ffjj jjff";
    public SessionType PendingSessionType { get; private set; } = SessionType.AdaptivePractice;
    public string? PendingLessonId { get; private set; }
    public TimeSpan? PendingTimedLimit { get; private set; }
    public string? PendingTipBannerText { get; private set; }
    public string? PendingCompanionTipId { get; private set; }

    public bool ShouldPromptPlacement =>
        _sessions.Count == 0 && _placementResults.Count == 0 && !_placementPromptDismissed;

    public bool ShouldShowProfilePickerOnLaunch { get; private set; }
    public UserProfile ActiveProfile => _profileService.ActiveProfile;
    public IReadOnlyList<UserProfile> Profiles => _profileService.Profiles;
    public IReadOnlyList<string> ProfileAvatarPalette => _profileService.AvatarPalette;

    public DailySessionPlan? ActiveDailyPlan { get; private set; }
    public int ActiveDailyPlanStepIndex { get; private set; }
    public PlannedSessionStep? CurrentPlannedStep =>
        ActiveDailyPlan is not null && ActiveDailyPlanStepIndex >= 0 && ActiveDailyPlanStepIndex < ActiveDailyPlan.Steps.Count
            ? ActiveDailyPlan.Steps[ActiveDailyPlanStepIndex]
            : null;

    public event Action? StateChanged;

    public TypeForgeStateService(
        ITypeForgeRepository repository,
        IProfileService profileService,
        CourseProgressionService courseProgression,
        CourseCatalogService courseCatalog,
        IKeystrokeAudioService audioService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _courseProgression = courseProgression ?? throw new ArgumentNullException(nameof(courseProgression));
        _courseCatalog = courseCatalog ?? throw new ArgumentNullException(nameof(courseCatalog));
        _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
    }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _profileService.Initialize();
        if (_repository is SwitchableRepository switchable)
        {
            switchable.SetRepository(_profileService.CreateRepositoryForActiveProfile());
        }

        Task.Run(InitializeAsync).GetAwaiter().GetResult();
        ShouldShowProfilePickerOnLaunch = _profileService.Profiles.Count >= 2;
        _initialized = true;
    }

    public void DismissLaunchProfilePicker()
    {
        if (!ShouldShowProfilePickerOnLaunch)
        {
            return;
        }

        ShouldShowProfilePickerOnLaunch = false;
        StateChanged?.Invoke();
    }

    public void SwitchProfile(string profileId)
    {
        _profileService.SetActiveProfile(profileId);
        if (_repository is SwitchableRepository switchable)
        {
            switchable.SetRepository(_profileService.CreateRepositoryForActiveProfile());
        }

        ActiveDailyPlan = null;
        ActiveDailyPlanStepIndex = 0;
        _placementPromptDismissed = false;
        ShouldShowProfilePickerOnLaunch = false;

        Task.Run(InitializeAsync).GetAwaiter().GetResult();
        StateChanged?.Invoke();
    }

    public UserProfile CreateAndSwitchProfile(string name, string? avatarColorHex = null)
    {
        UserProfile created = _profileService.CreateProfile(name, avatarColorHex);
        SwitchProfile(created.Id);
        return created;
    }

    public void RenameProfile(string profileId, string newName)
    {
        _profileService.RenameProfile(profileId, newName);
        StateChanged?.Invoke();
    }

    public void DeleteProfile(string profileId)
    {
        string activeBefore = _profileService.ActiveProfile.Id;
        _profileService.DeleteProfile(profileId);

        if (string.Equals(activeBefore, profileId, StringComparison.OrdinalIgnoreCase))
        {
            SwitchProfile(_profileService.ActiveProfile.Id);
            return;
        }

        StateChanged?.Invoke();
    }

    public void ResetCurrentProfileProgress()
    {
        Task.Run(async () =>
        {
            await _repository.ResetProgressAsync().ConfigureAwait(false);
            await InitializeAsync().ConfigureAwait(false);
        }).GetAwaiter().GetResult();

        ActiveDailyPlan = null;
        ActiveDailyPlanStepIndex = 0;
        _placementPromptDismissed = false;
        StateChanged?.Invoke();
    }

    public IReadOnlyList<ProfileRankingEntry> GetProfileRankings()
    {
        int totalExercises = _exerciseLookup.Count;
        return Task.Run(() => _profileService.BuildRankingsAsync(totalExercises)).GetAwaiter().GetResult();
    }

    public void SaveSettings()
    {
        Settings.Validate();
        ApplyAudioSettings();
        Task.Run(() => _repository.SaveSettingsAsync(Settings)).GetAwaiter().GetResult();
        StateChanged?.Invoke();
    }

    private void ApplyAudioSettings()
    {
        SoundFeedbackMode effectiveMode = Settings.SoundEnabled ? Settings.SoundMode : SoundFeedbackMode.None;
        _audioService.Configure(effectiveMode, Settings.SoundTheme, Settings.SoundVolume);
    }

    public void SelectCourseTrack(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
        {
            return;
        }

        Settings.SelectedCourseTrackId = trackId;
        SaveSettings();
        StateChanged?.Invoke();
    }

    public void MarkCourseTipCompleted(string exerciseId)
    {
        Task.Run(() => _courseCatalog.MarkTipCompletedAsync(exerciseId)).GetAwaiter().GetResult();
        StateChanged?.Invoke();
    }

    public CourseExercise? GetCourseExercise(string? exerciseId)
    {
        if (string.IsNullOrWhiteSpace(exerciseId))
        {
            return null;
        }

        return _exerciseLookup.TryGetValue(exerciseId, out CourseExercise? ex) ? ex : null;
    }

    public string GetEffectiveExercisePrompt(CourseExercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        CourseExercise? previous = _courseCatalog.GetPreviousExercise(exercise);
        return exercise.GetEffectivePracticeText(Settings.OptimalDurationEnabled, previous);
    }

    public void QueuePracticeSession(
        string title,
        string promptText,
        SessionType sessionType,
        string? lessonId = null,
        TimeSpan? timedLimit = null,
        string? tipBannerText = null,
        string? companionTipId = null)
    {
        PendingPromptTitle = title;
        PendingPromptText = promptText;
        PendingSessionType = sessionType;
        PendingLessonId = lessonId;
        PendingTimedLimit = timedLimit;
        PendingTipBannerText = tipBannerText;
        PendingCompanionTipId = companionTipId;
    }

    public DailySessionPlan StartTodaysSession()
    {
        CourseTrack? track = SelectedCourseTrack;
        HashSet<string> completedIds = track is null
            ? []
            : track.Units
                .SelectMany(u => u.Exercises)
                .Where(e => e.IsCompleted)
                .Select(e => e.ExerciseId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        DailySessionPlan plan = _sessionPlanner.BuildPlan(
            adaptiveStates: _adaptiveStates,
            activeTrack: track,
            completedExerciseIds: completedIds,
            keyMetrics: AggregateKeyMetrics.Values.ToList(),
            bigramMetrics: _bigramAggregates.Values.ToList(),
            reviewSchedules: _reviewSchedules.Values.ToList(),
            dictionaryPool: _dictionaryWords,
            practiceMode: Settings.AdaptiveWordMode,
            dailyGoalMinutes: Settings.DailyGoalMinutes);

        ActiveDailyPlan = plan;
        ActiveDailyPlanStepIndex = 0;

        if (plan.Steps.Count > 0)
        {
            QueuePlannedStep(plan.Steps[0], 0, plan.Steps.Count);
        }

        StateChanged?.Invoke();
        return plan;
    }

    public bool AdvanceDailyPlanStep()
    {
        if (ActiveDailyPlan is null)
        {
            return false;
        }

        int nextIndex = ActiveDailyPlanStepIndex + 1;
        if (nextIndex < ActiveDailyPlan.Steps.Count)
        {
            ActiveDailyPlanStepIndex = nextIndex;
            QueuePlannedStep(ActiveDailyPlan.Steps[nextIndex], nextIndex, ActiveDailyPlan.Steps.Count);
            StateChanged?.Invoke();
            return true;
        }

        ActiveDailyPlan = null;
        ActiveDailyPlanStepIndex = 0;
        PendingTipBannerText = null;
        PendingCompanionTipId = null;
        StateChanged?.Invoke();
        return false;
    }

    public void CancelDailyPlan()
    {
        ActiveDailyPlan = null;
        ActiveDailyPlanStepIndex = 0;
        PendingTipBannerText = null;
        PendingCompanionTipId = null;
        QueuePracticeSession("Adaptive Practice · Keybr Focus", BuildAdaptivePracticeText(), SessionType.AdaptivePractice);
        StateChanged?.Invoke();
    }

    private void QueuePlannedStep(PlannedSessionStep step, int index, int total)
    {
        string stepPrefix = $"Today's Session ({index + 1}/{total}) · ";
        QueuePracticeSession(
            title: stepPrefix + step.Title,
            promptText: step.PromptText,
            sessionType: step.SessionType,
            lessonId: step.CourseExerciseId,
            timedLimit: step.Kind == PlannedStepKind.Warmup ? step.EstimatedDuration : null,
            tipBannerText: step.TipBannerText,
            companionTipId: step.CompanionTipExerciseId);
    }

    public void DismissPlacementPrompt()
    {
        _placementPromptDismissed = true;
        StateChanged?.Invoke();
    }

    public void QueuePlacementTest()
    {
        _placementPromptDismissed = true;
        ActiveDailyPlan = null;
        ActiveDailyPlanStepIndex = 0;
        QueuePracticeSession(
            title: "Placement Diagnostic · 60s Check",
            promptText: _placementEngine.DiagnosticPrompt,
            sessionType: SessionType.PlacementTest,
            timedLimit: TimeSpan.FromSeconds(_placementEngine.DurationSeconds));
        StateChanged?.Invoke();
    }

    public PlacementRecommendation EvaluateAndSavePlacement(SessionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        PlacementRecommendation rec = _placementEngine.Evaluate(result.NetWpm, result.AccuracyPercentage);
        PlacementResultRecord record = _placementEngine.CreateRecord(result.NetWpm, result.AccuracyPercentage, result.StartedAt);
        _placementResults.Insert(0, record);
        _placementPromptDismissed = true;

        Task.Run(() => _repository.SavePlacementResultAsync(record)).GetAwaiter().GetResult();
        StateChanged?.Invoke();
        return rec;
    }

    public void ApplyPlacementRecommendation(PlacementRecommendation recommendation)
    {
        ArgumentNullException.ThrowIfNull(recommendation);
        _placementPromptDismissed = true;

        Settings.SelectedCourseTrackId = recommendation.RecommendedCourseId;
        SaveSettings();

        IReadOnlyList<AdaptiveKeyState> unlockedStates = AdaptiveEngine.CreateStatesWithUnlockedStage(recommendation.UnlockedAdaptiveStage);
        _adaptiveStates = unlockedStates;

        Task.Run(async () =>
        {
            await _repository.SaveAdaptiveProgressAsync(unlockedStates).ConfigureAwait(false);

            CourseTrack? track = _courseCatalog.GetTrack(recommendation.RecommendedCourseId);
            if (track is not null)
            {
                foreach (CourseUnit unit in track.Units.OrderBy(u => u.UnitNumber))
                {
                    foreach (CourseExercise ex in unit.Exercises.OrderBy(e => e.OrderIndex))
                    {
                        bool beforeStartingUnit = unit.UnitNumber < recommendation.StartingUnitNumber;
                        bool isTargetExercise = string.Equals(ex.ExerciseId, recommendation.StartingExerciseId, StringComparison.OrdinalIgnoreCase);

                        if (beforeStartingUnit || isTargetExercise || (unit.UnitNumber == recommendation.StartingUnitNumber && ex.Kind == CourseStepKind.Tip))
                        {
                            ex.IsUnlocked = true;
                            if (beforeStartingUnit || (unit.UnitNumber == recommendation.StartingUnitNumber && ex.Kind == CourseStepKind.Tip && !isTargetExercise))
                            {
                                ex.IsCompleted = true;
                                ex.Stars = Math.Max(ex.Stars, 3);
                            }

                            await _repository.SaveExerciseProgressAsync(new LessonProgressRecord(
                                ex.ExerciseId,
                                ex.IsUnlocked,
                                ex.IsCompleted,
                                ex.BestWpm,
                                ex.BestAccuracy,
                                ex.Stars,
                                ex.IsCompleted ? DateTimeOffset.UtcNow : null)).ConfigureAwait(false);
                        }

                        if (isTargetExercise)
                        {
                            break;
                        }
                    }
                }
            }
        }).GetAwaiter().GetResult();

        if (_exerciseLookup.TryGetValue(recommendation.StartingExerciseId, out CourseExercise? startEx))
        {
            CourseTrack? track = _courseCatalog.GetTrack(recommendation.RecommendedCourseId);
            string trackTitle = track?.Title ?? "Course";
            QueuePracticeSession(
                $"{trackTitle} · {startEx.Title}",
                GetEffectiveExercisePrompt(startEx),
                SessionType.CourseLesson,
                startEx.ExerciseId,
                tipBannerText: startEx.TipContent);
        }

        StateChanged?.Invoke();
    }

    public void RecordSession(SessionResult result, IReadOnlyDictionary<string, BigramMetric>? sessionBigrams = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.Validate();

        _sessions.Insert(0, result);
        TodayPracticedDuration += result.Duration;

        foreach (KeyValuePair<char, KeyMetric> kvp in result.KeyBreakdown)
        {
            if (!_keyAggregates.TryGetValue(kvp.Key, out KeyAggregate? agg))
            {
                agg = new KeyAggregate();
                _keyAggregates[kvp.Key] = agg;
            }

            agg.Total += kvp.Value.TotalKeystrokes;
            agg.Correct += kvp.Value.CorrectKeystrokes;
            agg.Incorrect += kvp.Value.IncorrectKeystrokes;
            agg.WeightedLatencySum += kvp.Value.AverageLatencyMs * kvp.Value.TotalKeystrokes;
        }

        var updatedBigrams = new List<BigramMetric>();
        if (sessionBigrams is { Count: > 0 })
        {
            foreach (KeyValuePair<string, BigramMetric> kvp in sessionBigrams)
            {
                string key = kvp.Key.ToUpperInvariant();
                if (_bigramAggregates.TryGetValue(key, out BigramMetric? existing))
                {
                    int total = existing.TotalCount + kvp.Value.TotalCount;
                    int errors = existing.ErrorCount + kvp.Value.ErrorCount;
                    double latency = existing.AverageLatencyMs <= 0
                        ? kvp.Value.AverageLatencyMs
                        : kvp.Value.AverageLatencyMs <= 0
                            ? existing.AverageLatencyMs
                            : Math.Round((0.25 * kvp.Value.AverageLatencyMs) + (0.75 * existing.AverageLatencyMs), 2);
                    var merged = new BigramMetric(key, total, errors, latency);
                    _bigramAggregates[key] = merged;
                    updatedBigrams.Add(merged);
                }
                else
                {
                    _bigramAggregates[key] = kvp.Value;
                    updatedBigrams.Add(kvp.Value);
                }
            }
        }

        (double targetLatencyMs, double targetAccuracy, _) = AdaptiveEngine.GetThresholds(Settings.AdaptiveProfile);
        var updatedSchedules = new List<KeyReviewScheduleRecord>();
        foreach (KeyValuePair<char, KeyMetric> kvp in result.KeyBreakdown)
        {
            char upper = char.ToUpperInvariant(kvp.Key);
            if (!char.IsLetter(upper) || kvp.Value.TotalKeystrokes < 1)
            {
                continue;
            }

            _reviewSchedules.TryGetValue(upper, out KeyReviewScheduleRecord? existingSchedule);
            KeyReviewScheduleRecord nextSchedule = _reviewScheduler.EvaluateKeySession(
                existingSchedule,
                upper,
                kvp.Value,
                targetAccuracyPercentage: targetAccuracy,
                maxLatencyMs: targetLatencyMs);
            _reviewSchedules[upper] = nextSchedule;
            updatedSchedules.Add(nextSchedule);
        }

        string? companionTipId = PendingCompanionTipId;

        Task.Run(async () =>
        {
            await _repository.RecordPracticeSessionAsync(result).ConfigureAwait(false);

            if (updatedBigrams.Count > 0)
            {
                await _repository.SaveBigramStatisticsAsync(updatedBigrams).ConfigureAwait(false);
            }

            if (updatedSchedules.Count > 0)
            {
                await _repository.SaveKeyReviewSchedulesAsync(updatedSchedules).ConfigureAwait(false);
            }

            if (result.SessionType == SessionType.CourseLesson)
            {
                if (!string.IsNullOrWhiteSpace(companionTipId))
                {
                    await _courseCatalog.MarkTipCompletedAsync(companionTipId).ConfigureAwait(false);
                }

                await _courseCatalog.ApplyExerciseResultAsync(result, Settings).ConfigureAwait(false);
                await _courseProgression.ApplySessionResultAsync(result).ConfigureAwait(false);
            }
            else if (result.SessionType == SessionType.AdaptivePractice)
            {
                IReadOnlyList<AdaptiveKeyState> updated = AdaptiveEngine.EvaluateUnlockProgression(
                    _adaptiveStates,
                    AggregateKeyMetrics,
                    Settings.AdaptiveProfile);

                if (!updated.SequenceEqual(_adaptiveStates))
                {
                    _adaptiveStates = updated;
                    await _repository.SaveAdaptiveProgressAsync(updated).ConfigureAwait(false);
                }
            }

            IReadOnlyList<DateOnly> practiceDates = await _repository.GetPracticeDatesAsync().ConfigureAwait(false);
            StreakDays = StreakCalculator.CalculateConsecutiveStreak(practiceDates);
        }).GetAwaiter().GetResult();

        StateChanged?.Invoke();
    }

    public (int Stars, bool Passed) EvaluateCourseOutcome(SessionResult result)
    {
        if (result.SessionType == SessionType.CourseLesson && !string.IsNullOrWhiteSpace(result.LessonId))
        {
            if (_exerciseLookup.TryGetValue(result.LessonId, out CourseExercise? exercise))
            {
                bool exPassed = exercise.EvaluatePass(
                    result.NetWpm,
                    result.AccuracyPercentage,
                    result.Mistakes,
                    Settings.CoursePassGateEnabled,
                    Settings.CourseMinAccuracy,
                    Settings.CourseMaxMistakes);
                int exStars = exercise.CalculateStars(
                    result.NetWpm,
                    result.AccuracyPercentage,
                    result.Mistakes,
                    Settings.CoursePassGateEnabled,
                    Settings.CourseMinAccuracy,
                    Settings.CourseMaxMistakes);

                if (exPassed
                    && Settings.CoursePassGateEnabled
                    && GetMissedFocusKeysForResult(result).Count > 0)
                {
                    exPassed = false;
                    exStars = 0;
                }

                return (exStars, exPassed);
            }

            if (_lessonLookup.TryGetValue(result.LessonId, out LessonModel? lesson))
            {
                bool passed = lesson.EvaluatePass(result.NetWpm, result.AccuracyPercentage);
                int stars = lesson.CalculateStars(result.NetWpm, result.AccuracyPercentage);
                return (stars, passed);
            }
        }

        int generalStars = result.AccuracyPercentage >= 97 && result.NetWpm >= 50 ? 3
            : result.AccuracyPercentage >= 92 && result.NetWpm >= 30 ? 2
            : result.AccuracyPercentage >= 85 ? 1 : 0;
        return (generalStars, result.AccuracyPercentage >= 85);
    }

    public IReadOnlyList<char> GetMissedFocusKeysForResult(SessionResult result)
    {
        if (result.SessionType != SessionType.CourseLesson || string.IsNullOrWhiteSpace(result.LessonId))
        {
            return [];
        }

        if (!_exerciseLookup.TryGetValue(result.LessonId, out CourseExercise? exercise))
        {
            return [];
        }

        CourseTrack? track = CourseTracks.FirstOrDefault(t => string.Equals(t.TrackId, exercise.TrackId, StringComparison.OrdinalIgnoreCase));
        CourseUnit? unit = track?.Units.FirstOrDefault(u => u.UnitNumber == exercise.UnitNumber);
        if (unit is null || !unit.IsFinalExercise(exercise))
        {
            return [];
        }

        return CourseUnit.EvaluateMissedFocusKeys(unit, exercise, result.KeyBreakdown);
    }

    private HashSet<char> GetLearnedAllowedLetters()
    {
        HashSet<char> allowed = _adaptiveStates
            .Where(s => s.IsUnlocked && char.IsLetter(s.Character))
            .Select(s => char.ToUpperInvariant(s.Character))
            .ToHashSet();

        CourseTrack? activeTrack = SelectedCourseTrack ?? CourseTracks.FirstOrDefault();
        if (activeTrack is not null)
        {
            foreach (CourseUnit unit in activeTrack.Units.Where(u => u.IsUnlocked))
            {
                foreach (char ch in unit.GetFocusKeyCharacters())
                {
                    if (char.IsLetter(ch))
                    {
                        allowed.Add(char.ToUpperInvariant(ch));
                    }
                }
            }
        }

        if (allowed.Count == 0)
        {
            allowed.Add('F');
            allowed.Add('J');
        }

        return allowed;
    }

    public string BuildAdaptivePracticeText(SessionResult? sourceResult = null)
    {
        HashSet<char> unlocked = GetLearnedAllowedLetters();

        IReadOnlyDictionary<char, KeyMetric> metrics = sourceResult is { KeyBreakdown.Count: > 0 }
            ? sourceResult.KeyBreakdown
            : AggregateKeyMetrics;

        if (metrics.Count > 0 || _bigramAggregates.Count > 0)
        {
            return AdaptiveEngine.GenerateWeightedPracticeText(
                unlocked,
                metrics,
                _bigramAggregates,
                Settings.AdaptiveWordMode,
                _dictionaryWords,
                wordCount: 16);
        }

        return AdaptiveEngine.GeneratePracticeText(
            unlocked,
            metrics,
            Settings.AdaptiveWordMode,
            _dictionaryWords,
            wordCount: 16);
    }

    public string BuildWeakKeyDrillText(SessionResult? sourceResult = null)
    {
        IReadOnlyDictionary<char, KeyMetric> metrics = sourceResult is { KeyBreakdown.Count: > 0 }
            ? sourceResult.KeyBreakdown
            : AggregateKeyMetrics;

        List<char> weakChars = metrics.Values
            .Where(m => !char.IsWhiteSpace(m.Character) && (m.IncorrectKeystrokes > 0 || m.AverageLatencyMs >= 420.0))
            .OrderByDescending(m => m.ErrorRatePercentage)
            .ThenByDescending(m => m.AverageLatencyMs)
            .Take(4)
            .Select(m => m.Character)
            .ToList();

        if (weakChars.Count == 0 && _bigramAggregates.Count == 0)
        {
            return BuildAdaptivePracticeText(sourceResult);
        }

        HashSet<char> allowedLetters = GetLearnedAllowedLetters();

        foreach (char weak in weakChars.Where(char.IsLetter))
        {
            allowedLetters.Add(char.ToUpperInvariant(weak));
        }

        List<char> nonLetters = weakChars.Where(c => !char.IsLetter(c)).ToList();
        if (nonLetters.Count > 0)
        {
            List<string> chunks = new(14);
            List<string> baseWords = _dictionaryWords.Where(w => w.Length is >= 3 and <= 6).Take(40).ToList();
            for (int i = 0; i < 12; i++)
            {
                char target = nonLetters[i % nonLetters.Count];
                if (char.IsDigit(target))
                {
                    char alt = nonLetters[(i + 1) % nonLetters.Count];
                    chunks.Add($"{target}{target}{alt}{target}");
                }
                else
                {
                    string word = baseWords.Count > 0 ? baseWords[(i * 7) % baseWords.Count] : "code";
                    chunks.Add($"{word}{target}");
                }
            }

            return string.Join(' ', chunks);
        }

        return AdaptiveEngine.GenerateWeightedPracticeText(
            allowedLetters,
            metrics,
            _bigramAggregates,
            Settings.AdaptiveWordMode,
            _dictionaryWords,
            wordCount: 16);
    }

    public string BuildSlowestBigramDrillText()
    {
        HashSet<char> unlocked = GetLearnedAllowedLetters();

        return AdaptiveEngine.GenerateBigramDrillText(
            unlocked,
            _bigramAggregates,
            _dictionaryWords,
            wordCount: 80);
    }

    public void QueueSlowestBigramDrill()
    {
        ActiveDailyPlan = null;
        ActiveDailyPlanStepIndex = 0;

        string drillText = BuildSlowestBigramDrillText();
        QueuePracticeSession(
            "Slowest Letter Pairs · 2m Drill",
            drillText,
            SessionType.BigramDrill,
            timedLimit: TimeSpan.FromMinutes(2));
    }

    public void QueueNextDrill(SessionResult? sourceResult = null)
    {
        if (ActiveDailyPlan is not null && AdvanceDailyPlanStep())
        {
            return;
        }

        if (PendingSessionType == SessionType.CourseLesson
            && !string.IsNullOrWhiteSpace(PendingLessonId)
            && _exerciseLookup.TryGetValue(PendingLessonId, out CourseExercise? currentExercise))
        {
            CourseTrack? track = _courseCatalog.GetTrack(currentExercise.TrackId);
            if (track is not null)
            {
                List<CourseExercise> ordered = track.Units
                    .OrderBy(u => u.UnitNumber)
                    .SelectMany(u => u.Exercises.OrderBy(e => e.OrderIndex))
                    .ToList();

                int currentIndex = ordered.FindIndex(e => string.Equals(e.ExerciseId, currentExercise.ExerciseId, StringComparison.OrdinalIgnoreCase));
                string? pendingTipBanner = null;
                for (int i = currentIndex + 1; i >= 1 && i < ordered.Count; i++)
                {
                    CourseExercise next = ordered[i];
                    if (!next.IsUnlocked)
                    {
                        break;
                    }

                    if (next.Kind == CourseStepKind.Tip)
                    {
                        pendingTipBanner ??= next.TipContent;
                        MarkCourseTipCompleted(next.ExerciseId);
                        continue;
                    }

                    string prompt = GetEffectiveExercisePrompt(next);
                    QueuePracticeSession(
                        $"{track.Title} · {next.Title}",
                        prompt,
                        SessionType.CourseLesson,
                        next.ExerciseId,
                        tipBannerText: pendingTipBanner ?? (!string.IsNullOrWhiteSpace(next.TipContent) ? next.TipContent : null));
                    return;
                }
            }
        }

        if (PendingSessionType == SessionType.TimedTest && _timedPassages.Count > 0)
        {
            int currentIdx = _timedPassages.ToList().FindIndex(p => PendingPromptTitle.Contains(p.Title, StringComparison.OrdinalIgnoreCase));
            TimedPassage nextPassage = _timedPassages[(currentIdx + 1) % _timedPassages.Count];
            TimeSpan limit = PendingTimedLimit ?? TimeSpan.FromMinutes(1);
            QueuePracticeSession(
                $"Timed Test ({(int)limit.TotalMinutes}m) · {nextPassage.Title}",
                nextPassage.Text,
                SessionType.TimedTest,
                timedLimit: limit);
            return;
        }

        string drillText = BuildAdaptivePracticeText(sourceResult);
        QueuePracticeSession("Adaptive Practice · Keybr Focus", drillText, SessionType.AdaptivePractice);
    }

    private async Task InitializeAsync()
    {
        await _repository.InitializeAsync().ConfigureAwait(false);

        Settings = await _repository.GetSettingsAsync().ConfigureAwait(false);
        _courseLevels = await _courseProgression.LoadCourseAsync().ConfigureAwait(false);
        _courseTracks = await _courseCatalog.LoadCatalogAsync().ConfigureAwait(false);

        _lessonLookup.Clear();
        foreach (CourseLevel level in _courseLevels)
        {
            foreach (LessonModel lesson in level.Lessons)
            {
                _lessonLookup[lesson.LessonId] = lesson;
            }
        }

        _exerciseLookup.Clear();
        foreach (CourseTrack track in _courseTracks)
        {
            foreach (CourseUnit unit in track.Units)
            {
                foreach (CourseExercise exercise in unit.Exercises)
                {
                    _exerciseLookup[exercise.ExerciseId] = exercise;
                }
            }
        }

        _adaptiveStates = await _repository.GetAdaptiveProgressAsync().ConfigureAwait(false);
        await _repository.SaveAdaptiveProgressAsync(_adaptiveStates).ConfigureAwait(false);

        IReadOnlyList<SessionResult> recent = await _repository.GetRecentSessionsAsync(200).ConfigureAwait(false);
        _sessions.Clear();
        _sessions.AddRange(recent);

        IReadOnlyDictionary<char, KeyMetric> keyStats = await _repository.GetKeyStatisticsAsync().ConfigureAwait(false);
        _keyAggregates.Clear();
        foreach (KeyValuePair<char, KeyMetric> kvp in keyStats)
        {
            _keyAggregates[kvp.Key] = new KeyAggregate
            {
                Total = kvp.Value.TotalKeystrokes,
                Correct = kvp.Value.CorrectKeystrokes,
                Incorrect = kvp.Value.IncorrectKeystrokes,
                WeightedLatencySum = kvp.Value.AverageLatencyMs * kvp.Value.TotalKeystrokes
            };
        }

        IReadOnlyDictionary<string, BigramMetric> bigramStats = await _repository.GetBigramStatisticsAsync().ConfigureAwait(false);
        _bigramAggregates.Clear();
        foreach (KeyValuePair<string, BigramMetric> kvp in bigramStats)
        {
            _bigramAggregates[kvp.Key] = kvp.Value;
        }

        IReadOnlyDictionary<char, KeyReviewScheduleRecord> schedules = await _repository.GetKeyReviewSchedulesAsync().ConfigureAwait(false);
        _reviewSchedules.Clear();
        foreach (KeyValuePair<char, KeyReviewScheduleRecord> kvp in schedules)
        {
            _reviewSchedules[kvp.Key] = kvp.Value;
        }

        IReadOnlyList<PlacementResultRecord> placements = await _repository.GetPlacementResultsAsync().ConfigureAwait(false);
        _placementResults.Clear();
        _placementResults.AddRange(placements);

        TodayPracticedDuration = await _repository
            .GetTotalDurationForDateAsync(DateOnly.FromDateTime(DateTime.UtcNow))
            .ConfigureAwait(false);

        IReadOnlyList<DateOnly> practiceDates = await _repository.GetPracticeDatesAsync().ConfigureAwait(false);
        StreakDays = StreakCalculator.CalculateConsecutiveStreak(practiceDates);

        _timedPassages = EmbeddedContentLoader.LoadTimedPassagesSafe();
        _books = EmbeddedContentLoader.LoadBooksSafe();
        _realWorldPools = EmbeddedContentLoader.LoadRealWorldPoolsSafe();
        _dictionaryWords = EmbeddedContentLoader.LoadDictionaryWords();
        ApplyAudioSettings();

        PendingPromptTitle = "Adaptive Practice · Keybr Focus";
        PendingPromptText = BuildAdaptivePracticeText();
    }
}
