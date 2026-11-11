using TypeForge.Core.Models;

namespace TypeForge.Tests;

public sealed class DomainModelsTests
{
    [Fact]
    public void UserSettings_Defaults_AreValid()
    {
        var settings = new UserSettings();
        settings.Validate();

        Assert.Equal(ThemePreference.Dark, settings.Theme);
        Assert.Equal(28, settings.TypingFontSize);
        Assert.True(settings.ShowVirtualKeyboard);
        Assert.Equal(KeyboardColorMode.PerformanceHeat, settings.KeyboardColorMode);
        Assert.True(settings.SoundEnabled);
        Assert.False(settings.StopOnError);
        Assert.Equal(AdaptiveDifficultyProfile.Standard, settings.AdaptiveProfile);
        Assert.Equal(AdaptiveWordMode.PhoneticPseudoWords, settings.AdaptiveWordMode);
        Assert.Equal(30, settings.DailyGoalMinutes);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(60)]
    public void UserSettings_InvalidFontSize_ThrowsArgumentOutOfRangeException(int fontSize)
    {
        var settings = new UserSettings { TypingFontSize = fontSize };
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(300)]
    public void UserSettings_InvalidDailyGoal_ThrowsArgumentOutOfRangeException(int minutes)
    {
        var settings = new UserSettings { DailyGoalMinutes = minutes };
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.Validate());
    }

    [Fact]
    public void KeyMetric_ComputesRatesAccurately()
    {
        var metric = new KeyMetric('F', totalKeystrokes: 20, correctKeystrokes: 19, incorrectKeystrokes: 1, averageLatencyMs: 145.5);

        Assert.Equal(95.0, metric.AccuracyPercentage);
        Assert.Equal(5.0, metric.ErrorRatePercentage);
    }

    [Fact]
    public void KeyMetric_ZeroKeystrokes_DefaultsToFullAccuracyAndZeroError()
    {
        var metric = new KeyMetric('J', totalKeystrokes: 0, correctKeystrokes: 0, incorrectKeystrokes: 0, averageLatencyMs: 0);

        Assert.Equal(100.0, metric.AccuracyPercentage);
        Assert.Equal(0.0, metric.ErrorRatePercentage);
    }

    [Fact]
    public void KeyMetric_NegativeCounts_ThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new KeyMetric('A', -1, 0, 0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KeyMetric('A', 5, -1, 0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KeyMetric('A', 5, 4, 2, 100));
    }

    [Theory]
    [InlineData(14.9, 95.0, false, 0)]
    [InlineData(15.0, 89.9, false, 0)]
    [InlineData(15.0, 90.0, true, 1)]
    [InlineData(25.0, 95.0, true, 2)]
    [InlineData(40.0, 98.5, true, 3)]
    public void LessonModel_EvaluateAndCalculateStars_FollowsThresholds(
        double netWpm,
        double accuracy,
        bool expectedPass,
        int expectedStars)
    {
        var lesson = new LessonModel(
            lessonId: "L1_01",
            levelId: 1,
            orderIndex: 1,
            title: "F and J Anchors",
            description: "Anchor index fingers on tactile bumps.",
            targetCharacters: "fj",
            practiceText: "fff jjj fjf jfj",
            difficulty: "Beginner",
            minimumNetWpm: 15.0,
            minimumAccuracy: 90.0,
            isUnlocked: true);

        Assert.Equal(expectedPass, lesson.EvaluatePass(netWpm, accuracy));
        Assert.Equal(expectedStars, lesson.CalculateStars(netWpm, accuracy));
    }

    [Fact]
    public void SessionResult_Validate_RejectsInvalidValues()
    {
        var invalidWpm = new SessionResult
        {
            SessionType = SessionType.TimedTest,
            StartedAt = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromSeconds(30),
            NetWpm = -1
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => invalidWpm.Validate());

        var invalidAcc = new SessionResult
        {
            SessionType = SessionType.TimedTest,
            StartedAt = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromSeconds(30),
            AccuracyPercentage = 101
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => invalidAcc.Validate());
    }

    [Fact]
    public void CourseLevel_TracksCompletionCountsAccurately()
    {
        var l1 = new LessonModel("L1_01", 1, 1, "F & J", "Desc", "fj", "fff jjj", "Beginner", 15, 90, isUnlocked: true, isCompleted: true);
        var l2 = new LessonModel("L1_02", 1, 2, "D & K", "Desc", "dk", "ddd kkk", "Beginner", 15, 90, isUnlocked: true, isCompleted: false);
        var level = new CourseLevel(1, "Home Row", "Anchor keys", "Keep wrists neutral.", new[] { l1, l2 });

        Assert.Equal(2, level.TotalCount);
        Assert.Equal(1, level.CompletedCount);
        Assert.False(level.IsCompleted);

        l2.IsCompleted = true;
        Assert.Equal(2, level.CompletedCount);
        Assert.True(level.IsCompleted);
    }
}
