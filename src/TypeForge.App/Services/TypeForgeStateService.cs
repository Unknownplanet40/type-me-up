using TypeForge.Core.Engine;
using TypeForge.Core.Models;
using TypeForge.Core.Services;

namespace TypeForge.App.Services;

/// <summary>
/// Coordinates persistent SQLite state, multi-track course catalog progression, adaptive engine state, and active drill queueing.
/// </summary>
public interface ITypeForgeStateService
{
    UserSettings Settings { get; }
    IReadOnlyList<SessionResult> Sessions { get; }
    IReadOnlyDictionary<char, KeyMetric> AggregateKeyMetrics { get; }
    IReadOnlyList<CourseLevel> CourseLevels { get; }
    IReadOnlyList<CourseTrack> CourseTracks { get; }
    CourseTrack? SelectedCourseTrack { get; }
    IReadOnlyList<AdaptiveKeyState> AdaptiveStates { get; }
    IReadOnlyList<TimedPassage> TimedPassages { get; }
    TimeSpan TodayPracticedDuration { get; }
    int StreakDays { get; }

    string PendingPromptTitle { get; }
    string PendingPromptText { get; }
    SessionType PendingSessionType { get; }
    string? PendingLessonId { get; }
    TimeSpan? PendingTimedLimit { get; }

    event Action? StateChanged;

    void Initialize();
    void SaveSettings();
    void SelectCourseTrack(string trackId);
    void MarkCourseTipCompleted(string exerciseId);
    string GetEffectiveExercisePrompt(CourseExercise exercise);

    void QueuePracticeSession(
        string title,
        string promptText,
        SessionType sessionType,
        string? lessonId = null,
        TimeSpan? timedLimit = null);

    void RecordSession(SessionResult result);
    string BuildAdaptivePracticeText(SessionResult? sourceResult = null);
    string BuildWeakKeyDrillText(SessionResult? sourceResult = null);
    void QueueNextDrill(SessionResult? sourceResult = null);
    (int Stars, bool Passed) EvaluateCourseOutcome(SessionResult result);
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
    private readonly CourseProgressionService _courseProgression;
    private readonly CourseCatalogService _courseCatalog;
    private readonly List<SessionResult> _sessions = [];
    private readonly Dictionary<char, KeyAggregate> _keyAggregates = [];
    private readonly Dictionary<string, LessonModel> _lessonLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CourseExercise> _exerciseLookup = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<CourseLevel> _courseLevels = [];
    private IReadOnlyList<CourseTrack> _courseTracks = [];
    private IReadOnlyList<AdaptiveKeyState> _adaptiveStates = AdaptiveEngine.CreateInitialStates();
    private IReadOnlyList<TimedPassage> _timedPassages = [];
    private IReadOnlyList<string> _dictionaryWords = [];
    private bool _initialized;

    public UserSettings Settings { get; private set; } = new();
    public IReadOnlyList<SessionResult> Sessions => _sessions;
    public IReadOnlyDictionary<char, KeyMetric> AggregateKeyMetrics =>
        _keyAggregates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToMetric(kvp.Key));
    public IReadOnlyList<CourseLevel> CourseLevels => _courseLevels;
    public IReadOnlyList<CourseTrack> CourseTracks => _courseTracks;
    public CourseTrack? SelectedCourseTrack => _courseCatalog.GetTrack(Settings.SelectedCourseTrackId);
    public IReadOnlyList<AdaptiveKeyState> AdaptiveStates => _adaptiveStates;
    public IReadOnlyList<TimedPassage> TimedPassages => _timedPassages;
    public TimeSpan TodayPracticedDuration { get; private set; } = TimeSpan.Zero;
    public int StreakDays { get; private set; }

    public string PendingPromptTitle { get; private set; } = "Adaptive Practice · Keybr Focus";
    public string PendingPromptText { get; private set; } = "fjf jfj fff jjj fjfj jfjf ffjj jjff";
    public SessionType PendingSessionType { get; private set; } = SessionType.AdaptivePractice;
    public string? PendingLessonId { get; private set; }
    public TimeSpan? PendingTimedLimit { get; private set; }

    public event Action? StateChanged;

    public TypeForgeStateService(
        ITypeForgeRepository repository,
        CourseProgressionService courseProgression,
        CourseCatalogService courseCatalog)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _courseProgression = courseProgression ?? throw new ArgumentNullException(nameof(courseProgression));
        _courseCatalog = courseCatalog ?? throw new ArgumentNullException(nameof(courseCatalog));
    }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        Task.Run(InitializeAsync).GetAwaiter().GetResult();
        _initialized = true;
    }

    public void SaveSettings()
    {
        Settings.Validate();
        Task.Run(() => _repository.SaveSettingsAsync(Settings)).GetAwaiter().GetResult();
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
        TimeSpan? timedLimit = null)
    {
        PendingPromptTitle = title;
        PendingPromptText = promptText;
        PendingSessionType = sessionType;
        PendingLessonId = lessonId;
        PendingTimedLimit = timedLimit;
    }

    public void RecordSession(SessionResult result)
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

        Task.Run(async () =>
        {
            await _repository.RecordPracticeSessionAsync(result).ConfigureAwait(false);

            if (result.SessionType == SessionType.CourseLesson)
            {
                await _courseCatalog.ApplyExerciseResultAsync(result).ConfigureAwait(false);
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
                bool exPassed = exercise.EvaluatePass(result.NetWpm, result.AccuracyPercentage);
                int exStars = exercise.CalculateStars(result.NetWpm, result.AccuracyPercentage);
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

    public string BuildAdaptivePracticeText(SessionResult? sourceResult = null)
    {
        HashSet<char> unlocked = _adaptiveStates
            .Where(s => s.IsUnlocked)
            .Select(s => char.ToUpperInvariant(s.Character))
            .ToHashSet();

        IReadOnlyDictionary<char, KeyMetric> metrics = sourceResult is { KeyBreakdown.Count: > 0 }
            ? sourceResult.KeyBreakdown
            : AggregateKeyMetrics;

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

        if (weakChars.Count == 0)
        {
            return BuildAdaptivePracticeText(sourceResult);
        }

        HashSet<char> allowedLetters = _adaptiveStates
            .Where(s => s.IsUnlocked)
            .Select(s => char.ToUpperInvariant(s.Character))
            .ToHashSet();

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

        return AdaptiveEngine.GeneratePracticeText(
            allowedLetters,
            metrics,
            Settings.AdaptiveWordMode,
            _dictionaryWords,
            wordCount: 16);
    }

    public void QueueNextDrill(SessionResult? sourceResult = null)
    {
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
                for (int i = currentIndex + 1; i >= 1 && i < ordered.Count; i++)
                {
                    CourseExercise next = ordered[i];
                    if (!next.IsUnlocked)
                    {
                        break;
                    }

                    if (next.Kind == CourseStepKind.Tip)
                    {
                        MarkCourseTipCompleted(next.ExerciseId);
                        continue;
                    }

                    string prompt = GetEffectiveExercisePrompt(next);
                    QueuePracticeSession(
                        $"{track.Title} · {next.Title}",
                        prompt,
                        SessionType.CourseLesson,
                        next.ExerciseId);
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

        TodayPracticedDuration = await _repository
            .GetTotalDurationForDateAsync(DateOnly.FromDateTime(DateTime.UtcNow))
            .ConfigureAwait(false);

        IReadOnlyList<DateOnly> practiceDates = await _repository.GetPracticeDatesAsync().ConfigureAwait(false);
        StreakDays = StreakCalculator.CalculateConsecutiveStreak(practiceDates);

        _timedPassages = EmbeddedContentLoader.LoadTimedPassagesSafe();
        _dictionaryWords = EmbeddedContentLoader.LoadDictionaryWords();

        PendingPromptTitle = "Adaptive Practice · Keybr Focus";
        PendingPromptText = BuildAdaptivePracticeText();
    }
}
