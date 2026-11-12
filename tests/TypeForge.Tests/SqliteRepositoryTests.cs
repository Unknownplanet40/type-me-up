using TypeForge.Core.Engine;
using TypeForge.Core.Models;
using TypeForge.Core.Services;

namespace TypeForge.Tests;

[Collection("SqliteTests")]
public sealed class SqliteRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;

    public SqliteRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TypeForgeTests_{Guid.NewGuid():N}");
        _dbPath = Path.Combine(_tempDir, "nested", "typeforge.db");
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
    public async Task InitializeAsync_CreatesDirectorySchemaAndVersion()
    {
        var repo = new SqliteTypeForgeRepository(_dbPath);
        await repo.InitializeAsync();

        Assert.True(File.Exists(_dbPath));
        int version = await repo.GetSchemaVersionAsync();
        Assert.True(version >= 1);
    }

    [Fact]
    public async Task RecordSessionAndSettings_PersistAcrossReadsWithParameterizedSql()
    {
        var repo = new SqliteTypeForgeRepository(_dbPath);
        await repo.InitializeAsync();

        var settings = new UserSettings
        {
            Theme = ThemePreference.Light,
            TypingFontSize = 32,
            ShowVirtualKeyboard = false,
            KeyboardColorMode = KeyboardColorMode.FingerZones,
            SoundEnabled = false,
            StopOnError = true,
            AdaptiveProfile = AdaptiveDifficultyProfile.Pro,
            AdaptiveWordMode = AdaptiveWordMode.NaturalDictionaryWords,
            DailyGoalMinutes = 45
        };

        await repo.SaveSettingsAsync(settings);
        UserSettings loadedSettings = await repo.GetSettingsAsync();

        Assert.Equal(ThemePreference.Light, loadedSettings.Theme);
        Assert.Equal(32, loadedSettings.TypingFontSize);
        Assert.False(loadedSettings.ShowVirtualKeyboard);
        Assert.Equal(KeyboardColorMode.FingerZones, loadedSettings.KeyboardColorMode);
        Assert.False(loadedSettings.SoundEnabled);
        Assert.True(loadedSettings.StopOnError);
        Assert.Equal(AdaptiveDifficultyProfile.Pro, loadedSettings.AdaptiveProfile);
        Assert.Equal(AdaptiveWordMode.NaturalDictionaryWords, loadedSettings.AdaptiveWordMode);
        Assert.Equal(45, loadedSettings.DailyGoalMinutes);

        var session = new SessionResult
        {
            SessionType = SessionType.TimedTest,
            StartedAt = new DateTimeOffset(2026, 11, 12, 8, 30, 0, TimeSpan.Zero),
            Duration = TimeSpan.FromSeconds(60),
            GrossWpm = 66,
            NetWpm = 64,
            AccuracyPercentage = 97.5,
            CorrectCharacters = 320,
            TotalKeystrokes = 328,
            Mistakes = 8,
            UncorrectedErrors = 2,
            KeyBreakdown = new Dictionary<char, KeyMetric>
            {
                ['F'] = new KeyMetric('F', 40, 39, 1, 180.0),
                ['J'] = new KeyMetric('J', 40, 38, 2, 220.0)
            }
        };

        await repo.RecordPracticeSessionAsync(session);

        IReadOnlyList<SessionResult> sessions = await repo.GetRecentSessionsAsync();
        Assert.Single(sessions);
        Assert.Equal(64, sessions[0].NetWpm);
        Assert.Equal(SessionType.TimedTest, sessions[0].SessionType);

        IReadOnlyDictionary<char, KeyMetric> keys = await repo.GetKeyStatisticsAsync();
        Assert.Equal(40, keys['F'].TotalKeystrokes);
        Assert.Equal(2, keys['J'].IncorrectKeystrokes);

        TimeSpan dayDuration = await repo.GetTotalDurationForDateAsync(new DateOnly(2026, 11, 12));
        Assert.Equal(TimeSpan.FromSeconds(60), dayDuration);
    }

    [Fact]
    public async Task TimedAndCustomSessions_DoNotModifyCourseOrAdaptiveLocks()
    {
        var repo = new SqliteTypeForgeRepository(_dbPath);
        await repo.InitializeAsync();
        IReadOnlyList<CourseLevel> levels = EmbeddedContentLoader.LoadCurriculum();
        await repo.SeedCurriculumIfEmptyAsync(levels);

        IReadOnlyList<AdaptiveKeyState> initialAdaptive = AdaptiveEngine.CreateInitialStates();
        await repo.SaveAdaptiveProgressAsync(initialAdaptive);

        var timedSession = new SessionResult
        {
            SessionType = SessionType.TimedTest,
            StartedAt = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromMinutes(1),
            NetWpm = 90,
            AccuracyPercentage = 99.5,
            CorrectCharacters = 450,
            TotalKeystrokes = 452,
            Mistakes = 2
        };

        await repo.RecordPracticeSessionAsync(timedSession);

        IReadOnlyDictionary<string, LessonProgressRecord> progress = await repo.LoadLessonProgressAsync();
        Assert.True(progress["L1_01"].IsUnlocked);
        Assert.False(progress["L1_01"].IsCompleted);
        Assert.False(progress["L1_02"].IsUnlocked);

        IReadOnlyList<AdaptiveKeyState> adaptive = await repo.GetAdaptiveProgressAsync();
        Assert.Equal(2, adaptive.Count(a => a.IsUnlocked));
    }
}
