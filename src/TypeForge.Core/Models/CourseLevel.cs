namespace TypeForge.Core.Models;

/// <summary>
/// Represents a progressive curriculum level grouping ordered lessons and ergonomic guidance.
/// </summary>
public sealed class CourseLevel
{
    /// <summary>
    /// Initializes a new validated <see cref="CourseLevel"/> instance.
    /// </summary>
    public CourseLevel(
        int levelId,
        string title,
        string description,
        string ergonomicTip,
        IReadOnlyList<LessonModel> lessons)
    {
        if (levelId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(levelId), "Level ID must be positive.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Level title cannot be empty.", nameof(title));
        }

        ArgumentNullException.ThrowIfNull(lessons);

        LevelId = levelId;
        Title = title;
        Description = description ?? string.Empty;
        ErgonomicTip = ergonomicTip ?? string.Empty;
        Lessons = lessons;
    }

    /// <summary>Gets the 1-based level identifier.</summary>
    public int LevelId { get; }

    /// <summary>Gets the display title of the level.</summary>
    public string Title { get; }

    /// <summary>Gets the level summary description.</summary>
    public string Description { get; }

    /// <summary>Gets ergonomic posture guidance specific to this level.</summary>
    public string ErgonomicTip { get; }

    /// <summary>Gets the ordered list of lessons in this level.</summary>
    public IReadOnlyList<LessonModel> Lessons { get; }

    /// <summary>Gets the count of completed lessons in this level.</summary>
    public int CompletedCount => Lessons.Count(lesson => lesson.IsCompleted);

    /// <summary>Gets the total count of lessons in this level.</summary>
    public int TotalCount => Lessons.Count;

    /// <summary>Gets a value indicating whether all lessons in this level are completed.</summary>
    public bool IsCompleted => TotalCount > 0 && CompletedCount == TotalCount;
}
