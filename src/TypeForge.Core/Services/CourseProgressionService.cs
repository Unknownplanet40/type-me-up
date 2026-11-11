using TypeForge.Core.Models;

namespace TypeForge.Core.Services;

/// <summary>
/// Captures the evaluation outcome of completing a structured course lesson.
/// </summary>
/// <param name="Passed">True when the session satisfied minimum Net WPM and accuracy thresholds.</param>
/// <param name="EarnedStars">Stars earned for this attempt (0–3).</param>
/// <param name="NewlyUnlockedLessonId">Identifier of the next sequential lesson unlocked by this attempt, if any.</param>
public sealed record CourseOutcome(bool Passed, int EarnedStars, string? NewlyUnlockedLessonId);

/// <summary>
/// Manages structured 7-level curriculum loading, sequential unlocks, star ratings, and persistent lesson history.
/// </summary>
public sealed class CourseProgressionService
{
    private readonly ITypeForgeRepository _repository;
    private IReadOnlyList<CourseLevel> _levels = [];
    private List<LessonModel> _orderedLessons = [];

    /// <summary>
    /// Initializes a progression service backed by the given persistence repository.
    /// </summary>
    public CourseProgressionService(ITypeForgeRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>Gets the currently loaded course levels with hydrated progression state.</summary>
    public IReadOnlyList<CourseLevel> Levels => _levels;

    /// <summary>
    /// Seeds embedded curriculum if needed and hydrates every lesson with persisted progress from SQLite.
    /// </summary>
    public async Task<IReadOnlyList<CourseLevel>> LoadCourseAsync(CancellationToken ct = default)
    {
        IReadOnlyList<CourseLevel> baseLevels = EmbeddedContentLoader.ParseCurriculumSafe(
            string.Empty);

        // Load real embedded curriculum; fallback only on missing/invalid embedded resource.
        try
        {
            baseLevels = EmbeddedContentLoader.LoadCurriculum();
        }
        catch (Exception ex) when (ex is InvalidDataException or FileNotFoundException)
        {
            // Retained fallback starter curriculum from ParseCurriculumSafe above.
        }

        await _repository.SeedCurriculumIfEmptyAsync(baseLevels, ct).ConfigureAwait(false);
        IReadOnlyDictionary<string, LessonProgressRecord> saved = await _repository.LoadLessonProgressAsync(ct).ConfigureAwait(false);

        var ordered = new List<LessonModel>();
        bool firstSeen = false;
        foreach (CourseLevel level in baseLevels.OrderBy(l => l.LevelId))
        {
            foreach (LessonModel lesson in level.Lessons.OrderBy(l => l.OrderIndex))
            {
                if (saved.TryGetValue(lesson.LessonId, out LessonProgressRecord? record))
                {
                    lesson.IsUnlocked = record.IsUnlocked;
                    lesson.IsCompleted = record.IsCompleted;
                    lesson.BestWpm = record.BestWpm;
                    lesson.BestAccuracy = record.BestAccuracy;
                    lesson.Stars = record.Stars;
                }
                else
                {
                    lesson.IsUnlocked = !firstSeen;
                }

                firstSeen = true;
                ordered.Add(lesson);
            }
        }

        if (ordered.Count > 0 && !ordered[0].IsUnlocked)
        {
            ordered[0].IsUnlocked = true;
        }

        _levels = baseLevels;
        _orderedLessons = ordered;
        return _levels;
    }

    /// <summary>
    /// Returns true if the lesson exists in the loaded curriculum and is unlocked.
    /// </summary>
    public bool CanStartLesson(string lessonId)
    {
        if (string.IsNullOrWhiteSpace(lessonId))
        {
            return false;
        }

        LessonModel? lesson = _orderedLessons.FirstOrDefault(
            l => string.Equals(l.LessonId, lessonId, StringComparison.OrdinalIgnoreCase));

        return lesson?.IsUnlocked == true;
    }

    /// <summary>
    /// Evaluates a completed course lesson attempt, updates best scores without overwriting higher records, and unlocks the next lesson if passed.
    /// </summary>
    public async Task<CourseOutcome> ApplySessionResultAsync(SessionResult result, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (_orderedLessons.Count == 0)
        {
            await LoadCourseAsync(ct).ConfigureAwait(false);
        }

        if (result.SessionType != SessionType.CourseLesson || string.IsNullOrWhiteSpace(result.LessonId))
        {
            return new CourseOutcome(false, 0, null);
        }

        int index = _orderedLessons.FindIndex(
            l => string.Equals(l.LessonId, result.LessonId, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            return new CourseOutcome(false, 0, null);
        }

        LessonModel lesson = _orderedLessons[index];
        bool passed = lesson.EvaluatePass(result.NetWpm, result.AccuracyPercentage);
        int earnedStars = lesson.CalculateStars(result.NetWpm, result.AccuracyPercentage);

        lesson.IsUnlocked = true;
        lesson.IsCompleted = lesson.IsCompleted || passed;
        lesson.BestWpm = Math.Max(lesson.BestWpm, result.NetWpm);
        lesson.BestAccuracy = Math.Max(lesson.BestAccuracy, result.AccuracyPercentage);
        lesson.Stars = Math.Max(lesson.Stars, earnedStars);

        await _repository.SaveLessonProgressAsync(
            new LessonProgressRecord(
                lesson.LessonId,
                lesson.IsUnlocked,
                lesson.IsCompleted,
                lesson.BestWpm,
                lesson.BestAccuracy,
                lesson.Stars,
                lesson.IsCompleted ? DateTimeOffset.UtcNow : null),
            ct).ConfigureAwait(false);

        string? newlyUnlockedId = null;
        if (passed && index + 1 < _orderedLessons.Count)
        {
            LessonModel next = _orderedLessons[index + 1];
            if (!next.IsUnlocked)
            {
                next.IsUnlocked = true;
                newlyUnlockedId = next.LessonId;

                await _repository.SaveLessonProgressAsync(
                    new LessonProgressRecord(
                        next.LessonId,
                        IsUnlocked: true,
                        next.IsCompleted,
                        next.BestWpm,
                        next.BestAccuracy,
                        next.Stars,
                        null),
                    ct).ConfigureAwait(false);
            }
        }

        return new CourseOutcome(passed, earnedStars, newlyUnlockedId);
    }
}
