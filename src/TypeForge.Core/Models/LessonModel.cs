namespace TypeForge.Core.Models;

/// <summary>
/// Represents a single ordered lesson inside the structured typing curriculum.
/// </summary>
public sealed class LessonModel
{
    /// <summary>
    /// Initializes a new validated <see cref="LessonModel"/> instance.
    /// </summary>
    public LessonModel(
        string lessonId,
        int levelId,
        int orderIndex,
        string title,
        string description,
        string targetCharacters,
        string practiceText,
        string difficulty,
        double minimumNetWpm,
        double minimumAccuracy,
        bool isUnlocked = false,
        bool isCompleted = false,
        double bestWpm = 0.0,
        double bestAccuracy = 0.0,
        int stars = 0)
    {
        if (string.IsNullOrWhiteSpace(lessonId))
        {
            throw new ArgumentException("Lesson ID cannot be empty.", nameof(lessonId));
        }

        if (levelId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(levelId), "Level ID must be positive.");
        }

        if (orderIndex <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orderIndex), "Order index must be positive.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Lesson title cannot be empty.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(practiceText))
        {
            throw new ArgumentException("Practice text cannot be empty.", nameof(practiceText));
        }

        if (minimumNetWpm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumNetWpm), "Minimum Net WPM cannot be negative.");
        }

        if (minimumAccuracy < 0 || minimumAccuracy > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumAccuracy), "Minimum accuracy must be between 0 and 100.");
        }

        if (stars < 0 || stars > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(stars), "Stars must be between 0 and 3.");
        }

        LessonId = lessonId;
        LevelId = levelId;
        OrderIndex = orderIndex;
        Title = title;
        Description = description ?? string.Empty;
        TargetCharacters = targetCharacters ?? string.Empty;
        PracticeText = practiceText;
        Difficulty = string.IsNullOrWhiteSpace(difficulty) ? "Standard" : difficulty;
        MinimumNetWpm = minimumNetWpm;
        MinimumAccuracy = minimumAccuracy;
        IsUnlocked = isUnlocked;
        IsCompleted = isCompleted;
        BestWpm = bestWpm;
        BestAccuracy = bestAccuracy;
        Stars = stars;
    }

    /// <summary>Gets the unique lesson identifier.</summary>
    public string LessonId { get; }

    /// <summary>Gets the parent course level identifier.</summary>
    public int LevelId { get; }

    /// <summary>Gets the 1-based sequence order within the level.</summary>
    public int OrderIndex { get; }

    /// <summary>Gets the display title of the lesson.</summary>
    public string Title { get; }

    /// <summary>Gets the descriptive summary of the lesson.</summary>
    public string Description { get; }

    /// <summary>Gets the focus characters drilled by this lesson.</summary>
    public string TargetCharacters { get; }

    /// <summary>Gets the exercise prompt text for the typing engine.</summary>
    public string PracticeText { get; }

    /// <summary>Gets the difficulty label.</summary>
    public string Difficulty { get; }

    /// <summary>Gets the minimum Net WPM required to pass.</summary>
    public double MinimumNetWpm { get; }

    /// <summary>Gets the minimum accuracy percentage required to pass.</summary>
    public double MinimumAccuracy { get; }

    /// <summary>Gets or sets a value indicating whether the lesson is unlocked.</summary>
    public bool IsUnlocked { get; set; }

    /// <summary>Gets or sets a value indicating whether the user has passed the lesson.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>Gets or sets the highest recorded Net WPM for this lesson.</summary>
    public double BestWpm { get; set; }

    /// <summary>Gets or sets the highest recorded accuracy percentage for this lesson.</summary>
    public double BestAccuracy { get; set; }

    /// <summary>Gets or sets the highest earned star rating (0–3).</summary>
    public int Stars { get; set; }

    /// <summary>
    /// Evaluates whether a session meets both speed and accuracy pass thresholds.
    /// </summary>
    public bool EvaluatePass(double netWpm, double accuracyPercentage) =>
        netWpm >= MinimumNetWpm && accuracyPercentage >= MinimumAccuracy;

    /// <summary>
    /// Calculates the star rating (0–3) earned for a given performance.
    /// </summary>
    public int CalculateStars(double netWpm, double accuracyPercentage)
    {
        if (!EvaluatePass(netWpm, accuracyPercentage))
        {
            return 0;
        }

        double threeStarWpm = Math.Max(MinimumNetWpm * 2.0, MinimumNetWpm + 20.0);
        double threeStarAcc = Math.Min(100.0, Math.Max(MinimumAccuracy + 5.0, 98.0));
        if (netWpm >= threeStarWpm && accuracyPercentage >= threeStarAcc)
        {
            return 3;
        }

        double twoStarWpm = Math.Max(MinimumNetWpm * 1.5, MinimumNetWpm + 8.0);
        double twoStarAcc = Math.Min(100.0, MinimumAccuracy + 4.0);
        if (netWpm >= twoStarWpm && accuracyPercentage >= twoStarAcc)
        {
            return 2;
        }

        return 1;
    }
}
