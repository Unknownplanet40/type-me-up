namespace TypeForge.Core.Models;

/// <summary>
/// Represents an entire selectable typing course track (e.g., Fast Touch, Speed Building, Numbers).
/// </summary>
public sealed class CourseTrack
{
    /// <summary>Initializes a new <see cref="CourseTrack"/>.</summary>
    public CourseTrack(
        string trackId,
        string title,
        string description,
        string durationEstimate,
        IReadOnlyList<CourseUnit> units)
    {
        if (string.IsNullOrWhiteSpace(trackId))
        {
            throw new ArgumentException("Track ID cannot be empty.", nameof(trackId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Track title cannot be empty.", nameof(title));
        }

        ArgumentNullException.ThrowIfNull(units);

        TrackId = trackId;
        Title = title;
        Description = description ?? string.Empty;
        DurationEstimate = durationEstimate ?? string.Empty;
        Units = units;
    }

    /// <summary>Gets the stable course track identifier.</summary>
    public string TrackId { get; }

    /// <summary>Gets the display title of the course track.</summary>
    public string Title { get; }

    /// <summary>Gets the course summary description.</summary>
    public string Description { get; }

    /// <summary>Gets the estimated total completion time range.</summary>
    public string DurationEstimate { get; }

    /// <summary>Gets the ordered lesson units in this course track.</summary>
    public IReadOnlyList<CourseUnit> Units { get; }

    /// <summary>Gets total steps across all units in this track.</summary>
    public int TotalExerciseCount => Units.Sum(u => u.Exercises.Count);

    /// <summary>Gets completed steps across all units in this track.</summary>
    public int CompletedExerciseCount => Units.Sum(u => u.CompletedCount);

    /// <summary>Gets completion fraction in [0, 1].</summary>
    public double ProgressFraction =>
        TotalExerciseCount == 0 ? 0.0 : (double)CompletedExerciseCount / TotalExerciseCount;
}

/// <summary>
/// Represents a numbered lesson unit inside a <see cref="CourseTrack"/> containing ordered steps.
/// </summary>
public sealed class CourseUnit
{
    /// <summary>Initializes a new <see cref="CourseUnit"/>.</summary>
    public CourseUnit(
        int unitNumber,
        string title,
        string focusKeys,
        string ergonomicTip,
        IReadOnlyList<CourseExercise> exercises)
    {
        if (unitNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitNumber), "Unit number must be positive.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Unit title cannot be empty.", nameof(title));
        }

        ArgumentNullException.ThrowIfNull(exercises);

        UnitNumber = unitNumber;
        Title = title;
        FocusKeys = focusKeys ?? string.Empty;
        ErgonomicTip = ergonomicTip ?? string.Empty;
        Exercises = exercises;
    }

    /// <summary>Gets the 1-based lesson tab number.</summary>
    public int UnitNumber { get; }

    /// <summary>Gets the unit title (e.g., "The Home Row").</summary>
    public string Title { get; }

    /// <summary>Gets focus characters taught in this unit.</summary>
    public string FocusKeys { get; }

    /// <summary>Gets unit-level posture and reach tip.</summary>
    public string ErgonomicTip { get; }

    /// <summary>Gets the ordered checklist of exercises within this unit.</summary>
    public IReadOnlyList<CourseExercise> Exercises { get; }

    /// <summary>Gets whether any exercise in this unit is unlocked.</summary>
    public bool IsUnlocked => Exercises.Any(e => e.IsUnlocked);

    /// <summary>Gets whether all exercises in this unit are completed.</summary>
    public bool IsCompleted => Exercises.Count > 0 && Exercises.All(e => e.IsCompleted);

    /// <summary>Gets the count of completed exercises in this unit.</summary>
    public int CompletedCount => Exercises.Count(e => e.IsCompleted);
}

/// <summary>
/// Represents one step inside a course unit (either a read-only Tip or a typed exercise).
/// </summary>
public sealed class CourseExercise
{
    /// <summary>Initializes a new <see cref="CourseExercise"/>.</summary>
    public CourseExercise(
        string exerciseId,
        string trackId,
        int unitNumber,
        int orderIndex,
        string title,
        CourseStepKind kind,
        string estimatedMinutes,
        string tipContent,
        string practiceText,
        double minimumNetWpm,
        double minimumAccuracy,
        bool isUnlocked = false,
        bool isCompleted = false,
        double bestWpm = 0.0,
        double bestAccuracy = 0.0,
        int stars = 0)
    {
        if (string.IsNullOrWhiteSpace(exerciseId))
        {
            throw new ArgumentException("Exercise ID cannot be empty.", nameof(exerciseId));
        }

        ExerciseId = exerciseId;
        TrackId = trackId;
        UnitNumber = unitNumber;
        OrderIndex = orderIndex;
        Title = title;
        Kind = kind;
        EstimatedMinutes = estimatedMinutes ?? string.Empty;
        TipContent = tipContent ?? string.Empty;
        PracticeText = practiceText ?? string.Empty;
        MinimumNetWpm = minimumNetWpm;
        MinimumAccuracy = minimumAccuracy;
        IsUnlocked = isUnlocked;
        IsCompleted = isCompleted;
        BestWpm = bestWpm;
        BestAccuracy = bestAccuracy;
        Stars = stars;
    }

    /// <summary>Gets the unique step identifier.</summary>
    public string ExerciseId { get; }

    /// <summary>Gets the parent track identifier.</summary>
    public string TrackId { get; }

    /// <summary>Gets the parent lesson unit number.</summary>
    public int UnitNumber { get; }

    /// <summary>Gets the 1-based step index within the unit.</summary>
    public int OrderIndex { get; }

    /// <summary>Gets the step title (e.g., "1.1 Touch typing basics").</summary>
    public string Title { get; }

    /// <summary>Gets the step kind (Tip card vs typed drill).</summary>
    public CourseStepKind Kind { get; }

    /// <summary>Gets the estimated duration label (e.g., "3 - 5 min.").</summary>
    public string EstimatedMinutes { get; }

    /// <summary>Gets the instructional tip text displayed for Tip or NewKeys steps.</summary>
    public string TipContent { get; }

    /// <summary>Gets the full practice prompt text (empty for Tip steps).</summary>
    public string PracticeText { get; }

    /// <summary>Gets minimum Net WPM required to pass typed exercises.</summary>
    public double MinimumNetWpm { get; }

    /// <summary>Gets minimum accuracy required to pass typed exercises.</summary>
    public double MinimumAccuracy { get; }

    /// <summary>Gets or sets whether this step can currently be started.</summary>
    public bool IsUnlocked { get; set; }

    /// <summary>Gets or sets whether this step has been completed.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>Gets or sets highest recorded Net WPM for typed steps.</summary>
    public double BestWpm { get; set; }

    /// <summary>Gets or sets highest recorded accuracy percentage for typed steps.</summary>
    public double BestAccuracy { get; set; }

    /// <summary>Gets or sets highest earned stars (0–3).</summary>
    public int Stars { get; set; }

    /// <summary>Evaluates whether a typed session passes this step's minimum requirements.</summary>
    public bool EvaluatePass(double netWpm, double accuracyPercentage) =>
        Kind == CourseStepKind.Tip || (netWpm >= MinimumNetWpm && accuracyPercentage >= MinimumAccuracy);

    /// <summary>Calculates star rating (0–3) for a typed exercise attempt.</summary>
    public int CalculateStars(double netWpm, double accuracyPercentage)
    {
        if (Kind == CourseStepKind.Tip)
        {
            return 3;
        }

        if (!EvaluatePass(netWpm, accuracyPercentage))
        {
            return 0;
        }

        double threeStarWpm = Math.Max(MinimumNetWpm * 1.8, MinimumNetWpm + 16.0);
        double threeStarAcc = Math.Min(100.0, Math.Max(MinimumAccuracy + 4.0, 97.5));
        if (netWpm >= threeStarWpm && accuracyPercentage >= threeStarAcc)
        {
            return 3;
        }

        double twoStarWpm = Math.Max(MinimumNetWpm * 1.35, MinimumNetWpm + 7.0);
        double twoStarAcc = Math.Min(100.0, MinimumAccuracy + 2.0);
        if (netWpm >= twoStarWpm && accuracyPercentage >= twoStarAcc)
        {
            return 2;
        }

        return 1;
    }

    /// <summary>
    /// Returns the drill text shortened (~50%) when Optimal Duration is active and the prior exercise shows high mastery (>= 96% accuracy).
    /// </summary>
    public string GetEffectivePracticeText(bool optimalDurationEnabled, CourseExercise? previousExercise)
    {
        if (Kind == CourseStepKind.Tip || string.IsNullOrWhiteSpace(PracticeText))
        {
            return PracticeText;
        }

        bool priorMastered = previousExercise is not null
            && previousExercise.IsCompleted
            && previousExercise.BestAccuracy >= 96.0
            && (previousExercise.Kind == CourseStepKind.Tip || previousExercise.BestWpm >= previousExercise.MinimumNetWpm);

        if (!optimalDurationEnabled || !priorMastered)
        {
            return PracticeText;
        }

        string[] tokens = PracticeText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length <= 6)
        {
            return PracticeText;
        }

        int targetTokens = Math.Max(6, (int)Math.Ceiling(tokens.Length * 0.55));
        return string.Join(' ', tokens.Take(targetTokens));
    }
}
