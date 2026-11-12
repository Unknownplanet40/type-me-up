using TypeForge.Core.Engine;
using TypeForge.Core.Models;

namespace TypeForge.Core.Services;

/// <summary>
/// Identifies the step category within a planned daily typing session.
/// </summary>
public enum PlannedStepKind
{
    /// <summary>60-second rhythm warmup on unlocked keys.</summary>
    Warmup = 0,

    /// <summary>Next unlocked incomplete course drill in the active track.</summary>
    CourseStep = 1,

    /// <summary>Targeted drill focusing on top weak keys and slow bigrams.</summary>
    WeaknessReview = 2,

    /// <summary>Spaced-repetition review drill for keys due today.</summary>
    DueKeyReview = 3
}

/// <summary>
/// Represents a single ordered step inside a <see cref="DailySessionPlan"/>.
/// </summary>
/// <param name="Kind">The step category.</param>
/// <param name="Title">Display title for the step.</param>
/// <param name="Subtitle">Display subtitle describing the step focus.</param>
/// <param name="PromptText">Target typing prompt text for the step.</param>
/// <param name="EstimatedDuration">Estimated time required to complete the step.</param>
/// <param name="TargetCharacters">Focused target characters for the step.</param>
/// <param name="SessionType">Persisted session type when the step completes.</param>
/// <param name="CourseExerciseId">Optional course exercise ID when <paramref name="Kind"/> is <see cref="PlannedStepKind.CourseStep"/>.</param>
/// <param name="CompanionTipExerciseId">Optional preceding Tip exercise ID auto-completed alongside a course drill.</param>
/// <param name="TipBannerText">Optional ergonomic or instructional tip banner text displayed above the typing stream.</param>
/// <param name="MinimumNetWpm">Minimum Net WPM target for the step.</param>
/// <param name="MinimumAccuracy">Minimum accuracy percentage target for the step.</param>
public sealed record PlannedSessionStep(
    PlannedStepKind Kind,
    string Title,
    string Subtitle,
    string PromptText,
    TimeSpan EstimatedDuration,
    string TargetCharacters,
    SessionType SessionType,
    string? CourseExerciseId = null,
    string? CompanionTipExerciseId = null,
    string? TipBannerText = null,
    double MinimumNetWpm = 0,
    double MinimumAccuracy = 90.0);

/// <summary>
/// Represents a complete structured daily practice session capped to the user's time budget.
/// </summary>
/// <param name="Steps">Ordered steps to run back-to-back.</param>
/// <param name="TotalDuration">Total estimated duration across all steps.</param>
/// <param name="MaxBudgetMinutes">Maximum time budget in minutes used when planning.</param>
public sealed record DailySessionPlan(
    IReadOnlyList<PlannedSessionStep> Steps,
    TimeSpan TotalDuration,
    int MaxBudgetMinutes);

/// <summary>
/// Builds a structured "Today's Session" daily practice plan:
/// 1) 60s warmup on unlocked keys
/// 2) Next unlocked incomplete course drill (attaching preceding Tip banner if present; skipped if course is complete)
/// 3) Weighted weak-key &amp; slow-bigram review drill (skipped if no keys/bigrams qualify)
/// 4) Due spaced-repetition key review drill (skipped if no keys are due)
/// Total estimated duration is capped at the user's daily goal budget (default 15 minutes).
/// </summary>
public sealed class SessionPlanner
{
    private readonly ReviewScheduler _reviewScheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionPlanner"/> class.
    /// </summary>
    public SessionPlanner(ReviewScheduler? reviewScheduler = null)
    {
        _reviewScheduler = reviewScheduler ?? new ReviewScheduler();
    }

    /// <summary>
    /// Builds a daily practice session plan tailored to the user's unlocked letters, course progress, weaknesses, and due reviews.
    /// </summary>
    public DailySessionPlan BuildPlan(
        IReadOnlyList<AdaptiveKeyState> adaptiveStates,
        CourseTrack? activeTrack,
        IReadOnlySet<string> completedExerciseIds,
        IReadOnlyList<KeyMetric> keyMetrics,
        IReadOnlyList<BigramMetric> bigramMetrics,
        IReadOnlyList<KeyReviewScheduleRecord> reviewSchedules,
        IReadOnlyList<string> dictionaryPool,
        AdaptiveWordMode practiceMode = AdaptiveWordMode.NaturalDictionaryWords,
        int dailyGoalMinutes = 15,
        DateTime? utcNow = null,
        Random? rng = null)
    {
        ArgumentNullException.ThrowIfNull(adaptiveStates);
        ArgumentNullException.ThrowIfNull(completedExerciseIds);
        ArgumentNullException.ThrowIfNull(keyMetrics);
        ArgumentNullException.ThrowIfNull(bigramMetrics);
        ArgumentNullException.ThrowIfNull(reviewSchedules);
        ArgumentNullException.ThrowIfNull(dictionaryPool);

        int budgetMinutes = Math.Clamp(dailyGoalMinutes, 3, 60);
        var maxBudget = TimeSpan.FromMinutes(budgetMinutes);
        var steps = new List<PlannedSessionStep>(4);
        var accumulated = TimeSpan.Zero;

        IReadOnlyList<AdaptiveKeyState> effectiveStates = adaptiveStates.Count > 0
            ? adaptiveStates
            : AdaptiveEngine.CreateInitialStates();

        string unlockedChars = new string(effectiveStates
            .Where(s => s.IsUnlocked)
            .Select(s => char.ToLowerInvariant(s.Character))
            .ToArray());

        if (string.IsNullOrEmpty(unlockedChars))
        {
            unlockedChars = "fj";
        }

        var unlockedUpperSet = effectiveStates
            .Where(s => s.IsUnlocked)
            .Select(s => char.ToUpperInvariant(s.Character))
            .ToHashSet();

        // Step 1: 60s Warmup on unlocked keys
        var warmupDuration = TimeSpan.FromSeconds(60);
        string warmupPrompt = AdaptiveEngine.GeneratePracticeText(
            unlockedUpperSet,
            new Dictionary<char, KeyMetric>(),
            practiceMode,
            dictionaryPool,
            wordCount: 16,
            random: rng);

        var warmupStep = new PlannedSessionStep(
            Kind: PlannedStepKind.Warmup,
            Title: "1. Warmup Drill",
            Subtitle: $"60s rhythm warmup on unlocked keys ({unlockedChars.ToUpperInvariant()})",
            PromptText: warmupPrompt,
            EstimatedDuration: warmupDuration,
            TargetCharacters: unlockedChars,
            SessionType: SessionType.AdaptivePractice,
            MinimumNetWpm: 0,
            MinimumAccuracy: 90.0);

        steps.Add(warmupStep);
        accumulated += warmupDuration;

        // Step 2: Next Course Step (skip Tip-only screen by attaching Tip text as banner on the following drill)
        if (activeTrack is not null)
        {
            var nextCourseStep = ResolveNextCourseStep(activeTrack, completedExerciseIds);
            if (nextCourseStep is not null && accumulated + nextCourseStep.EstimatedDuration <= maxBudget)
            {
                steps.Add(nextCourseStep);
                accumulated += nextCourseStep.EstimatedDuration;
            }
        }

        // Step 3: Weak-Key & Slow-Bigram Review (only if WeaknessScorer finds qualified weak keys or bigrams with Score > 0)
        var rankedKeys = WeaknessScorer.RankKeys(keyMetrics)
            .Where(k => k.Score > 0)
            .Take(4)
            .Select(k => k.Target)
            .ToList();

        var rankedBigrams = WeaknessScorer.RankBigrams(bigramMetrics)
            .Where(b => b.Score > 0)
            .Take(4)
            .Select(b => b.Target)
            .ToList();

        var keyMetricsDict = keyMetrics.ToDictionary(m => char.ToUpperInvariant(m.Character), m => m);
        var bigramMetricsDict = bigramMetrics.ToDictionary(b => b.Bigram.ToUpperInvariant(), b => b, StringComparer.Ordinal);

        if (rankedKeys.Count > 0 || rankedBigrams.Count > 0)
        {
            var weaknessDuration = TimeSpan.FromMinutes(3);
            if (accumulated + weaknessDuration <= maxBudget)
            {
                string weaknessPrompt = AdaptiveEngine.GenerateWeightedPracticeText(
                    unlockedUpperSet,
                    keyMetricsDict,
                    bigramMetricsDict,
                    practiceMode,
                    dictionaryPool,
                    wordCount: 24,
                    0.50,
                    random: rng);

                string focusLabel = BuildWeaknessSubtitle(rankedKeys, rankedBigrams);
                string targetChars = rankedKeys.Count > 0
                    ? new string(rankedKeys.Select(char.ToLowerInvariant).ToArray())
                    : unlockedChars;

                SessionType sessionType = rankedBigrams.Count > 0 && rankedKeys.Count == 0
                    ? SessionType.BigramDrill
                    : SessionType.AdaptivePractice;

                steps.Add(new PlannedSessionStep(
                    Kind: PlannedStepKind.WeaknessReview,
                    Title: $"{steps.Count + 1}. Targeted Weakness Drill",
                    Subtitle: focusLabel,
                    PromptText: weaknessPrompt,
                    EstimatedDuration: weaknessDuration,
                    TargetCharacters: targetChars,
                    SessionType: sessionType,
                    MinimumNetWpm: 0,
                    MinimumAccuracy: 92.0));

                accumulated += weaknessDuration;
            }
        }

        // Step 4: Spaced-Repetition Due Key Review
        DateTimeOffset? asOfOffset = utcNow.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(utcNow.Value, DateTimeKind.Utc))
            : null;

        var dueKeys = _reviewScheduler.GetDueKeys(reviewSchedules, asOfOffset, unlockedUpperSet)
            .Select(char.ToLowerInvariant)
            .Distinct()
            .Take(5)
            .ToList();

        if (dueKeys.Count > 0)
        {
            var reviewDuration = TimeSpan.FromMinutes(2);
            if (accumulated + reviewDuration <= maxBudget)
            {
                var dueKeySyntheticMetrics = dueKeys.ToDictionary(
                    c => char.ToUpperInvariant(c),
                    c => new KeyMetric(char.ToUpperInvariant(c), 30, 20, 10, 450));

                string duePrompt = AdaptiveEngine.GenerateWeightedPracticeText(
                    unlockedUpperSet,
                    dueKeySyntheticMetrics,
                    new Dictionary<string, BigramMetric>(),
                    practiceMode,
                    dictionaryPool,
                    wordCount: 20,
                    0.50,
                    random: rng);

                string dueLabel = string.Join(", ", dueKeys.Select(c => char.ToUpperInvariant(c)));
                steps.Add(new PlannedSessionStep(
                    Kind: PlannedStepKind.DueKeyReview,
                    Title: $"{steps.Count + 1}. Spaced Review ({dueLabel})",
                    Subtitle: $"Scheduled retention check for due keys: {dueLabel}",
                    PromptText: duePrompt,
                    EstimatedDuration: reviewDuration,
                    TargetCharacters: new string(dueKeys.ToArray()),
                    SessionType: SessionType.AdaptivePractice,
                    MinimumNetWpm: 0,
                    MinimumAccuracy: 94.0));

                accumulated += reviewDuration;
            }
        }

        return new DailySessionPlan(steps, accumulated, budgetMinutes);
    }

    private static PlannedSessionStep? ResolveNextCourseStep(
        CourseTrack track,
        IReadOnlySet<string> completedExerciseIds)
    {
        foreach (var unit in track.Units.OrderBy(u => u.UnitNumber))
        {
            string? pendingTipId = null;
            string? pendingTipText = null;

            foreach (var exercise in unit.Exercises.OrderBy(e => e.OrderIndex))
            {
                bool isDone = exercise.IsCompleted || completedExerciseIds.Contains(exercise.ExerciseId);
                if (isDone)
                {
                    continue;
                }

                if (exercise.Kind == CourseStepKind.Tip)
                {
                    pendingTipId ??= exercise.ExerciseId;
                    pendingTipText = !string.IsNullOrWhiteSpace(exercise.TipContent)
                        ? exercise.TipContent
                        : unit.ErgonomicTip;
                    continue;
                }

                var duration = ParseEstimatedMinutes(exercise.EstimatedMinutes);
                string banner = !string.IsNullOrWhiteSpace(pendingTipText)
                    ? pendingTipText!
                    : (!string.IsNullOrWhiteSpace(exercise.TipContent) ? exercise.TipContent : unit.ErgonomicTip);

                return new PlannedSessionStep(
                    Kind: PlannedStepKind.CourseStep,
                    Title: $"2. Course: {exercise.Title}",
                    Subtitle: $"{track.Title} - Lesson {unit.UnitNumber}: {unit.Title}",
                    PromptText: exercise.PracticeText,
                    EstimatedDuration: duration,
                    TargetCharacters: string.IsNullOrWhiteSpace(unit.FocusKeys) ? "fj" : unit.FocusKeys,
                    SessionType: SessionType.CourseLesson,
                    CourseExerciseId: exercise.ExerciseId,
                    CompanionTipExerciseId: pendingTipId,
                    TipBannerText: string.IsNullOrWhiteSpace(banner) ? null : banner,
                    MinimumNetWpm: exercise.MinimumNetWpm,
                    MinimumAccuracy: exercise.MinimumAccuracy);
            }
        }

        return null;
    }

    private static TimeSpan ParseEstimatedMinutes(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            foreach (var part in text.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part, out int mins) && mins is >= 1 and <= 15)
                {
                    return TimeSpan.FromMinutes(mins);
                }
            }
        }

        return TimeSpan.FromMinutes(3);
    }

    private static string BuildWeaknessSubtitle(IReadOnlyList<char> weakKeys, IReadOnlyList<string> weakBigrams)
    {
        var parts = new List<string>(2);
        if (weakKeys.Count > 0)
        {
            parts.Add("Weak keys: " + string.Join(", ", weakKeys.Select(c => char.ToUpperInvariant(c))));
        }

        if (weakBigrams.Count > 0)
        {
            parts.Add("Slow bigrams: " + string.Join(", ", weakBigrams.Select(b => b.ToUpperInvariant())));
        }

        return string.Join(" | ", parts);
    }
}
