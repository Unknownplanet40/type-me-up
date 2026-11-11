using TypeForge.Core.Models;
using TypeForge.Core.Services;

namespace TypeForge.Tests;

public sealed class CourseProgressionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;

    public CourseProgressionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TypeForgeCourse_{Guid.NewGuid():N}");
        _dbPath = Path.Combine(_tempDir, "typeforge.db");
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task CourseProgression_EnforcesSequentialUnlockAndPreservesBestScores()
    {
        var repo = new SqliteTypeForgeRepository(_dbPath);
        await repo.InitializeAsync();

        var service = new CourseProgressionService(repo);
        IReadOnlyList<CourseLevel> levels = await service.LoadCourseAsync();

        LessonModel first = levels[0].Lessons[0];
        LessonModel second = levels[0].Lessons[1];
        Assert.True(first.IsUnlocked);
        Assert.False(second.IsUnlocked);
        Assert.False(service.CanStartLesson(second.LessonId));

        // Failing attempt does not unlock next lesson
        var failResult = new SessionResult
        {
            SessionType = SessionType.CourseLesson,
            LessonId = first.LessonId,
            Duration = TimeSpan.FromSeconds(20),
            NetWpm = 10,
            AccuracyPercentage = 85,
            CorrectCharacters = 20,
            TotalKeystrokes = 24,
            Mistakes = 4
        };

        CourseOutcome outcomeFail = await service.ApplySessionResultAsync(failResult);
        Assert.False(outcomeFail.Passed);
        Assert.Equal(0, outcomeFail.EarnedStars);
        Assert.False(second.IsUnlocked);

        // Passing 3-star attempt completes Lesson 1 and unlocks Lesson 2
        var passResult = new SessionResult
        {
            SessionType = SessionType.CourseLesson,
            LessonId = first.LessonId,
            Duration = TimeSpan.FromSeconds(20),
            NetWpm = 42,
            AccuracyPercentage = 99,
            CorrectCharacters = 70,
            TotalKeystrokes = 71,
            Mistakes = 1
        };

        CourseOutcome outcomePass = await service.ApplySessionResultAsync(passResult);
        Assert.True(outcomePass.Passed);
        Assert.Equal(3, outcomePass.EarnedStars);
        Assert.True(first.IsCompleted);
        Assert.True(second.IsUnlocked);

        // Slower repeat attempt keeps best WPM and stars intact
        var slowerRepeat = new SessionResult
        {
            SessionType = SessionType.CourseLesson,
            LessonId = first.LessonId,
            Duration = TimeSpan.FromSeconds(25),
            NetWpm = 16,
            AccuracyPercentage = 91,
            CorrectCharacters = 40,
            TotalKeystrokes = 44,
            Mistakes = 4
        };

        await service.ApplySessionResultAsync(slowerRepeat);
        Assert.Equal(42, first.BestWpm);
        Assert.Equal(99, first.BestAccuracy);
        Assert.Equal(3, first.Stars);
    }
}
