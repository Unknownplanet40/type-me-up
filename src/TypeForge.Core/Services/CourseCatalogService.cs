using TypeForge.Core.Models;

namespace TypeForge.Core.Services;

/// <summary>
/// Manages multi-track course catalogs, step-by-step progression, Tip completion, and SQLite hydration.
/// </summary>
public sealed class CourseCatalogService
{
    private readonly ITypeForgeRepository _repository;
    private IReadOnlyList<CourseTrack> _tracks = [];
    private Dictionary<string, CourseExercise> _exerciseMap = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, CourseUnit> _unitByExerciseMap = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes a course catalog service backed by <see cref="ITypeForgeRepository"/>.</summary>
    public CourseCatalogService(ITypeForgeRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>Gets all loaded course tracks.</summary>
    public IReadOnlyList<CourseTrack> Tracks => _tracks;

    /// <summary>
    /// Loads embedded course tracks and hydrates persisted step completion and star records from SQLite.
    /// </summary>
    public async Task<IReadOnlyList<CourseTrack>> LoadCatalogAsync(CancellationToken ct = default)
    {
        IReadOnlyList<CourseTrack> tracks = EmbeddedContentLoader.LoadCourseTracksSafe();
        IReadOnlyDictionary<string, LessonProgressRecord> saved = await _repository.LoadExerciseProgressAsync(ct).ConfigureAwait(false);

        var map = new Dictionary<string, CourseExercise>(StringComparer.OrdinalIgnoreCase);
        var unitMap = new Dictionary<string, CourseUnit>(StringComparer.OrdinalIgnoreCase);
        foreach (CourseTrack track in tracks)
        {
            bool firstExercise = true;
            foreach (CourseUnit unit in track.Units.OrderBy(u => u.UnitNumber))
            {
                foreach (CourseExercise ex in unit.Exercises.OrderBy(e => e.OrderIndex))
                {
                    if (saved.TryGetValue(ex.ExerciseId, out LessonProgressRecord? record))
                    {
                        ex.IsUnlocked = record.IsUnlocked;
                        ex.IsCompleted = record.IsCompleted;
                        ex.BestWpm = record.BestWpm;
                        ex.BestAccuracy = record.BestAccuracy;
                        ex.Stars = record.Stars;
                    }
                    else
                    {
                        ex.IsUnlocked = firstExercise;
                    }

                    firstExercise = false;
                    map[ex.ExerciseId] = ex;
                    unitMap[ex.ExerciseId] = unit;
                }
            }
        }

        _tracks = tracks;
        _exerciseMap = map;
        _unitByExerciseMap = unitMap;
        return _tracks;
    }

    /// <summary>Returns a course track by ID, falling back to the first track if unknown.</summary>
    public CourseTrack? GetTrack(string? trackId)
    {
        if (_tracks.Count == 0)
        {
            return null;
        }

        return _tracks.FirstOrDefault(t => string.Equals(t.TrackId, trackId, StringComparison.OrdinalIgnoreCase))
            ?? _tracks[0];
    }

    /// <summary>Marks a read-only Tip step completed and unlocks the following exercise.</summary>
    public async Task<string?> MarkTipCompletedAsync(string exerciseId, CancellationToken ct = default)
    {
        if (_tracks.Count == 0)
        {
            await LoadCatalogAsync(ct).ConfigureAwait(false);
        }

        if (!_exerciseMap.TryGetValue(exerciseId, out CourseExercise? exercise))
        {
            return null;
        }

        exercise.IsUnlocked = true;
        exercise.IsCompleted = true;
        exercise.Stars = Math.Max(exercise.Stars, 3);

        await _repository.SaveExerciseProgressAsync(
            new LessonProgressRecord(
                exercise.ExerciseId,
                true,
                true,
                exercise.BestWpm,
                exercise.BestAccuracy,
                exercise.Stars,
                DateTimeOffset.UtcNow),
            ct).ConfigureAwait(false);

        return await UnlockNextExerciseInTrackAsync(exercise, ct).ConfigureAwait(false);
    }

    /// <summary>Evaluates and persists a typed course exercise run, unlocking the subsequent step when passed.</summary>
    public Task<CourseOutcome> ApplyExerciseResultAsync(SessionResult result, CancellationToken ct = default) =>
        ApplyExerciseResultAsync(result, settings: null, ct);

    /// <summary>Evaluates and persists a typed course exercise run using configurable pass-gate settings.</summary>
    public async Task<CourseOutcome> ApplyExerciseResultAsync(
        SessionResult result,
        UserSettings? settings,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (_tracks.Count == 0)
        {
            await LoadCatalogAsync(ct).ConfigureAwait(false);
        }

        if (result.SessionType != SessionType.CourseLesson || string.IsNullOrWhiteSpace(result.LessonId))
        {
            return new CourseOutcome(false, 0, null);
        }

        if (!_exerciseMap.TryGetValue(result.LessonId, out CourseExercise? exercise))
        {
            return new CourseOutcome(false, 0, null);
        }

        bool gateEnabled = settings?.CoursePassGateEnabled ?? true;
        double minAcc = settings?.CourseMinAccuracy ?? 90.0;
        int maxMistakes = settings?.CourseMaxMistakes ?? 5;

        bool passed = exercise.EvaluatePass(result.NetWpm, result.AccuracyPercentage, result.Mistakes, gateEnabled, minAcc, maxMistakes);
        int earnedStars = exercise.CalculateStars(result.NetWpm, result.AccuracyPercentage, result.Mistakes, gateEnabled, minAcc, maxMistakes);

        if (gateEnabled
            && passed
            && _unitByExerciseMap.TryGetValue(exercise.ExerciseId, out CourseUnit? parentUnit)
            && parentUnit.IsFinalExercise(exercise)
            && CourseUnit.EvaluateMissedFocusKeys(parentUnit, exercise, result.KeyBreakdown).Count > 0)
        {
            passed = false;
            earnedStars = 0;
        }

        exercise.IsUnlocked = true;
        exercise.IsCompleted = exercise.IsCompleted || passed;
        exercise.BestWpm = Math.Max(exercise.BestWpm, result.NetWpm);
        exercise.BestAccuracy = Math.Max(exercise.BestAccuracy, result.AccuracyPercentage);
        exercise.Stars = Math.Max(exercise.Stars, earnedStars);

        await _repository.SaveExerciseProgressAsync(
            new LessonProgressRecord(
                exercise.ExerciseId,
                exercise.IsUnlocked,
                exercise.IsCompleted,
                exercise.BestWpm,
                exercise.BestAccuracy,
                exercise.Stars,
                exercise.IsCompleted ? DateTimeOffset.UtcNow : null),
            ct).ConfigureAwait(false);

        string? unlockedNextId = null;
        if (passed)
        {
            unlockedNextId = await UnlockNextExerciseInTrackAsync(exercise, ct).ConfigureAwait(false);
        }

        return new CourseOutcome(passed, earnedStars, unlockedNextId);
    }

    /// <summary>Finds the previous step before <paramref name="exercise"/> in the same track for Optimal Duration evaluation.</summary>
    public CourseExercise? GetPreviousExercise(CourseExercise exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);
        CourseTrack? track = GetTrack(exercise.TrackId);
        if (track is null)
        {
            return null;
        }

        List<CourseExercise> ordered = track.Units
            .OrderBy(u => u.UnitNumber)
            .SelectMany(u => u.Exercises.OrderBy(e => e.OrderIndex))
            .ToList();

        int idx = ordered.FindIndex(e => string.Equals(e.ExerciseId, exercise.ExerciseId, StringComparison.OrdinalIgnoreCase));
        return idx > 0 ? ordered[idx - 1] : null;
    }

    private async Task<string?> UnlockNextExerciseInTrackAsync(CourseExercise completedExercise, CancellationToken ct)
    {
        CourseTrack? track = GetTrack(completedExercise.TrackId);
        if (track is null)
        {
            return null;
        }

        List<CourseExercise> ordered = track.Units
            .OrderBy(u => u.UnitNumber)
            .SelectMany(u => u.Exercises.OrderBy(e => e.OrderIndex))
            .ToList();

        int idx = ordered.FindIndex(e => string.Equals(e.ExerciseId, completedExercise.ExerciseId, StringComparison.OrdinalIgnoreCase));
        if (idx < 0 || idx + 1 >= ordered.Count)
        {
            return null;
        }

        CourseExercise next = ordered[idx + 1];
        if (next.IsUnlocked)
        {
            return null;
        }

        next.IsUnlocked = true;
        await _repository.SaveExerciseProgressAsync(
            new LessonProgressRecord(
                next.ExerciseId,
                IsUnlocked: true,
                next.IsCompleted,
                next.BestWpm,
                next.BestAccuracy,
                next.Stars,
                null),
            ct).ConfigureAwait(false);

        return next.ExerciseId;
    }
}
