using TypeForge.Core.Engine;
using TypeForge.Core.Models;
using TypeForge.Core.Services;

namespace TypeForge.Tests;

public sealed class AdaptiveEngineTests
{
    [Fact]
    public void CreateInitialStates_UnlocksOnlyFAndJInStageOne()
    {
        IReadOnlyList<AdaptiveKeyState> states = AdaptiveEngine.CreateInitialStates();

        Assert.Equal(26, states.Count);
        Assert.Equal(new[] { 'F', 'J' }, states.Where(s => s.IsUnlocked).Select(s => s.Character).OrderBy(c => c).ToArray());
    }

    [Fact]
    public void EvaluateUnlockProgression_AdvancesWhenAllUnlockedKeysMeetProfileThresholds()
    {
        IReadOnlyList<AdaptiveKeyState> initial = AdaptiveEngine.CreateInitialStates();

        // Not enough samples or too slow -> stays at Stage 1
        var weakMetrics = new Dictionary<char, KeyMetric>
        {
            ['F'] = new KeyMetric('F', 12, 12, 0, 400),
            ['J'] = new KeyMetric('J', 12, 10, 2, 620) // fails Standard (500ms / 95%)
        };

        IReadOnlyList<AdaptiveKeyState> unchanged = AdaptiveEngine.EvaluateUnlockProgression(
            initial,
            weakMetrics,
            AdaptiveDifficultyProfile.Standard);
        Assert.Equal(2, unchanged.Count(s => s.IsUnlocked));

        // Passing samples -> Stage 2 (D, K) unlocks
        var passingMetrics = new Dictionary<char, KeyMetric>
        {
            ['F'] = new KeyMetric('F', 15, 15, 0, 320),
            ['J'] = new KeyMetric('J', 15, 15, 0, 340)
        };

        IReadOnlyList<AdaptiveKeyState> advanced = AdaptiveEngine.EvaluateUnlockProgression(
            initial,
            passingMetrics,
            AdaptiveDifficultyProfile.Standard);

        char[] unlocked = advanced.Where(s => s.IsUnlocked).Select(s => s.Character).OrderBy(c => c).ToArray();
        Assert.Equal(new[] { 'D', 'F', 'J', 'K' }, unlocked);
    }

    [Theory]
    [InlineData(AdaptiveWordMode.PhoneticPseudoWords)]
    [InlineData(AdaptiveWordMode.NaturalDictionaryWords)]
    public void GeneratePracticeText_StrictlyOmitsLockedLettersInBothModes(AdaptiveWordMode mode)
    {
        IReadOnlyList<string> words = EmbeddedContentLoader.LoadDictionaryWords();
        var unlocked = new HashSet<char>(new[] { 'F', 'J', 'D', 'K', 'S', 'L', 'A', 'E' });

        string prompt = AdaptiveEngine.GeneratePracticeText(
            unlocked,
            new Dictionary<char, KeyMetric>
            {
                ['L'] = new KeyMetric('L', 20, 16, 4, 480)
            },
            mode,
            words,
            wordCount: 20,
            random: new Random(42));

        Assert.False(string.IsNullOrWhiteSpace(prompt));

        foreach (char ch in prompt)
        {
            if (ch == ' ')
            {
                continue;
            }

            Assert.Contains(char.ToUpperInvariant(ch), unlocked);
        }
    }

    [Fact]
    public void GeneratePracticeText_StageOneOnlyFAndJ_NeverEmitsLockedLetters()
    {
        IReadOnlyList<string> words = EmbeddedContentLoader.LoadDictionaryWords();
        var unlocked = new HashSet<char>(new[] { 'F', 'J' });

        foreach (AdaptiveWordMode mode in new[] { AdaptiveWordMode.PhoneticPseudoWords, AdaptiveWordMode.NaturalDictionaryWords })
        {
            string prompt = AdaptiveEngine.GeneratePracticeText(
                unlocked,
                new Dictionary<char, KeyMetric>(),
                mode,
                words,
                wordCount: 15,
                random: new Random(7));

            Assert.All(prompt.Where(c => c != ' '), c => Assert.Contains(char.ToUpperInvariant(c), unlocked));
        }
    }
}
