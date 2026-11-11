using TypeForge.Core.Engine;

namespace TypeForge.Tests;

public sealed class MetricsCalculatorTests
{
    [Fact]
    public void CalculateGrossWpm_StandardFiveCharWord_ReturnsExpectedWpm()
    {
        // 300 typed characters = 60 words in 1 minute => 60 WPM
        double wpm = MetricsCalculator.CalculateGrossWpm(300, TimeSpan.FromMinutes(1));
        Assert.Equal(60.0, wpm);
    }

    [Fact]
    public void CalculateNetWpm_SubtractsUncorrectedErrorsFromWordsPerMinute()
    {
        // 300 chars (60 words) - 5 uncorrected errors in 1 minute => 55 Net WPM
        double netWpm = MetricsCalculator.CalculateNetWpm(300, uncorrectedErrors: 5, TimeSpan.FromMinutes(1));
        Assert.Equal(55.0, netWpm);
    }

    [Fact]
    public void CalculateNetWpm_NeverReturnsNegative()
    {
        double netWpm = MetricsCalculator.CalculateNetWpm(10, uncorrectedErrors: 20, TimeSpan.FromMinutes(1));
        Assert.Equal(0.0, netWpm);
    }

    [Fact]
    public void CalculateWpm_ZeroOrSubOneSecondDuration_ReturnsZeroWithoutDivisionByZero()
    {
        Assert.Equal(0.0, MetricsCalculator.CalculateGrossWpm(50, TimeSpan.Zero));
        Assert.Equal(0.0, MetricsCalculator.CalculateNetWpm(50, 0, TimeSpan.Zero));
        Assert.Equal(0.0, MetricsCalculator.CalculateGrossWpm(5, TimeSpan.FromMilliseconds(400)));
        Assert.Equal(0.0, MetricsCalculator.CalculateNetWpm(5, 0, TimeSpan.FromMilliseconds(400)));
    }

    [Fact]
    public void CalculateAccuracyPercentage_HandlesZeroAndMixedKeystrokes()
    {
        Assert.Equal(100.0, MetricsCalculator.CalculateAccuracyPercentage(0, 0));
        Assert.Equal(95.0, MetricsCalculator.CalculateAccuracyPercentage(19, 20));
        Assert.Equal(0.0, MetricsCalculator.CalculateAccuracyPercentage(0, 10));
    }
}
