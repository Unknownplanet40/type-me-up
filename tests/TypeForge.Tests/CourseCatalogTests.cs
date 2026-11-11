using TypeForge.Core.Models;
using TypeForge.Core.Services;

namespace TypeForge.Tests;

public sealed class CourseCatalogTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;

    public CourseCatalogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TypeForgeCatalog_{Guid.NewGuid():N}");
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
    public void LoadCourseTracks_EmbeddedResources_ContainsFourTracksAndTwelveFastTouchUnits()
    {
        IReadOnlyList<CourseTrack> tracks = EmbeddedContentLoader.LoadCourseTracks();

        Assert.Equal(4, tracks.Count);
        Assert.Equal("fast_touch", tracks[0].TrackId);
        Assert.Equal(12, tracks[0].Units.Count);
        Assert.Equal("The Home Row", tracks[0].Units[0].Title);
        Assert.Equal("Keys Z and X", tracks[0].Units[11].Title);
        Assert.True(tracks[0].Units[0].Exercises[0].IsUnlocked);
        Assert.False(tracks[0].Units[0].Exercises[1].IsUnlocked);
    }

    [Fact]
    public async Task CourseCatalogService_TipCompletionAndOptimalDuration_PersistAcrossReload()
    {
        var repo = new SqliteTypeForgeRepository(_dbPath);
        await repo.InitializeAsync();
        Assert.True(await repo.GetSchemaVersionAsync() >= 2);

        var catalog = new CourseCatalogService(repo);
        IReadOnlyList<CourseTrack> tracks = await catalog.LoadCatalogAsync();
        CourseTrack fastTouch = tracks[0];
        CourseExercise tipStep = fastTouch.Units[0].Exercises[0]; // 1.1 Touch typing basics (Tip)
        CourseExercise drillStep = fastTouch.Units[0].Exercises[1]; // 1.2 New keys: Home row

        Assert.Equal(CourseStepKind.Tip, tipStep.Kind);
        Assert.True(tipStep.IsUnlocked);
        Assert.False(drillStep.IsUnlocked);

        // Completing a Tip unlocks the next exercise cleanly without requiring typing speed.
        await catalog.MarkTipCompletedAsync(tipStep.ExerciseId);
        Assert.True(tipStep.IsCompleted);
        Assert.True(drillStep.IsUnlocked);

        // Passing with high mastery triggers Optimal Duration reduction on subsequent drill steps.
        var masteryResult = new SessionResult
        {
            SessionType = SessionType.CourseLesson,
            LessonId = drillStep.ExerciseId,
            Duration = TimeSpan.FromSeconds(18),
            NetWpm = 35,
            AccuracyPercentage = 98.5,
            CorrectCharacters = 80,
            TotalKeystrokes = 81,
            Mistakes = 1
        };

        await catalog.ApplyExerciseResultAsync(masteryResult);
        CourseExercise tip2 = fastTouch.Units[0].Exercises[2]; // 1.3 Understanding results (Tip)
        CourseExercise keyDrill = fastTouch.Units[0].Exercises[3]; // 1.4 Key drill
        Assert.True(tip2.IsUnlocked);

        string fullText = keyDrill.GetEffectivePracticeText(optimalDurationEnabled: false, previousExercise: drillStep);
        string optimalText = keyDrill.GetEffectivePracticeText(optimalDurationEnabled: true, previousExercise: drillStep);
        Assert.True(optimalText.Length < fullText.Length);

        // Reload fresh repository + service verifies SQLite persistence
        var reloadedCatalog = new CourseCatalogService(new SqliteTypeForgeRepository(_dbPath));
        IReadOnlyList<CourseTrack> reloadedTracks = await reloadedCatalog.LoadCatalogAsync();
        Assert.True(reloadedTracks[0].Units[0].Exercises[0].IsCompleted);
        Assert.True(reloadedTracks[0].Units[0].Exercises[1].IsCompleted);
        Assert.Equal(35, reloadedTracks[0].Units[0].Exercises[1].BestWpm);
    }

    [Fact]
    public void StatisticsAggregator_DifficultKeysAndStreakCalculator_ComputeCorrectBands()
    {
        var metrics = new Dictionary<char, KeyMetric>
        {
            ['K'] = new KeyMetric('K', 40, 32, 8, 520), // 20% error -> Problematic
            ['L'] = new KeyMetric('L', 40, 37, 3, 410), // 7.5% error -> Difficult
            ['F'] = new KeyMetric('F', 40, 40, 0, 180), // 0% error -> OK
            ['Q'] = new KeyMetric('Q', 2, 1, 1, 700)    // < 5 samples -> filtered out
        };

        IReadOnlyList<DifficultKeyStat> ranked = StatisticsAggregator.GetDifficultKeys(metrics);
        Assert.Equal(3, ranked.Count);
        Assert.Equal('k', ranked[0].Character);
        Assert.Equal(KeyDifficultyBand.Problematic, ranked[0].Band);
        Assert.Equal(KeyDifficultyBand.Difficult, ranked[1].Band);
        Assert.Equal(KeyDifficultyBand.Ok, ranked[2].Band);

        var today = new DateOnly(2026, 11, 12);
        int streak = StreakCalculator.CalculateConsecutiveStreak(
            [today, today.AddDays(-1), today.AddDays(-2), today.AddDays(-5)],
            today);
        Assert.Equal(3, streak);
    }
}
