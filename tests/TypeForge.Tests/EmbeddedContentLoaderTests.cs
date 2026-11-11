using TypeForge.Core.Models;
using TypeForge.Core.Services;

namespace TypeForge.Tests;

public sealed class EmbeddedContentLoaderTests
{
    [Fact]
    public void LoadCurriculum_EmbeddedResources_ContainsAllSevenLevelsOrdered()
    {
        IReadOnlyList<CourseLevel> levels = EmbeddedContentLoader.LoadCurriculum();

        Assert.Equal(7, levels.Count);
        Assert.Equal(1, levels[0].LevelId);
        Assert.Equal(7, levels[6].LevelId);
        Assert.All(levels, l => Assert.NotEmpty(l.Lessons));
        Assert.All(levels, l => Assert.False(string.IsNullOrWhiteSpace(l.ErgonomicTip)));
    }

    [Fact]
    public void LoadTimedPassages_AndDictionaryWords_LoadSuccessfully()
    {
        IReadOnlyList<TimedPassage> passages = EmbeddedContentLoader.LoadTimedPassages();
        IReadOnlyList<string> words = EmbeddedContentLoader.LoadDictionaryWords();

        Assert.NotEmpty(passages);
        Assert.True(words.Count >= 100);
        Assert.All(words, w => Assert.True(w.All(char.IsLetter)));
    }

    [Theory]
    [InlineData("{ not valid json")]
    [InlineData("{ \"levels\": [] }")]
    [InlineData("{ \"levels\": [{ \"levelId\": 0, \"title\": \"\", \"lessons\": [] }] }")]
    public void ParseCurriculum_MalformedOrInvalid_ThrowsInvalidDataException(string badJson)
    {
        Assert.Throws<InvalidDataException>(() => EmbeddedContentLoader.ParseCurriculum(badJson));
    }

    [Fact]
    public void ParseCurriculumSafe_MalformedJson_FallsBackWithoutCrashing()
    {
        IReadOnlyList<CourseLevel> levels = EmbeddedContentLoader.ParseCurriculumSafe("{ corrupted json !!!");

        Assert.NotEmpty(levels);
        Assert.True(levels[0].Lessons[0].IsUnlocked);
    }

    [Fact]
    public void ParseDictionaryWordsSafe_FiltersInvalidWordsWithoutCrashing()
    {
        IReadOnlyList<string> parsed = EmbeddedContentLoader.ParseDictionaryWordsSafe("{ invalid");
        Assert.NotEmpty(parsed);

        IReadOnlyList<string> mixed = EmbeddedContentLoader.ParseDictionaryWordsSafe(
            "{ \"words\": [\"valid\", \"123\", \"\", \"bad-word\", \"CLEAN\"] }");
        Assert.Contains("valid", mixed);
        Assert.Contains("clean", mixed);
        Assert.DoesNotContain("123", mixed);
        Assert.DoesNotContain("bad-word", mixed);
    }
}
