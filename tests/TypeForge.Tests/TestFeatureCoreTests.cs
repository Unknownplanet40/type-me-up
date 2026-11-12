using TypeForge.Core.Engine;
using TypeForge.Core.Models;
using TypeForge.Core.Services;

namespace TypeForge.Tests;

[Collection("SqliteTests")]
public sealed class TestFeatureCoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;

    public TestFeatureCoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TypeForgeTestFeature_{Guid.NewGuid():N}");
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
    public void LoadBooksSafe_LoadsFullAliceWonderlandBook()
    {
        IReadOnlyList<BookModel> books = EmbeddedContentLoader.LoadBooksSafe();

        Assert.NotEmpty(books);
        BookModel alice = books[0];
        Assert.Equal("alice_wonderland", alice.Id);
        Assert.Equal("Alice's Adventures in Wonderland", alice.Title);
        Assert.Equal("Lewis Carroll", alice.Author);
        Assert.Equal(12, alice.Chapters);
        Assert.True(alice.Paragraphs.Count >= 700);
        Assert.Contains("Alice was beginning", alice.Paragraphs[0]);

        string prompt = TestTextGenerator.GenerateBookPrompt(alice, 0, 200);
        Assert.StartsWith("Alice was beginning", prompt);
        Assert.True(prompt.Length >= 200);
    }

    [Fact]
    public void CommonWordPoolAndStats_RespectCountAndCustomWords()
    {
        IReadOnlyList<string> full = EmbeddedContentLoader.LoadDictionaryWords();
        Assert.Equal(1000, full.Count);

        IReadOnlyList<string> subset = TestTextGenerator.ResolveCommonWordPool(full, 250, null);
        Assert.Equal(250, subset.Count);
        WordListStats stats = TestTextGenerator.ComputeWordStats(subset);
        Assert.Equal(250, stats.UniqueWords);
        Assert.True(stats.AverageWordLength > 2.0);

        IReadOnlyList<string> custom = TestTextGenerator.ResolveCommonWordPool(full, 250, "alpha beta gamma, alpha");
        Assert.Equal(3, custom.Count);
        WordListStats customStats = TestTextGenerator.ComputeWordStats(custom);
        Assert.Equal(3, customStats.UniqueWords);
        Assert.Equal(4.67, customStats.AverageWordLength);

        string generated = TestTextGenerator.GenerateCommonWordsPrompt(subset, 180, new Random(42));
        Assert.True(generated.Length >= 180);
    }

    [Fact]
    public void GeneratePseudoWordsPrompt_ProducesMultipleWordsUpToBudget()
    {
        string prompt = TestTextGenerator.GeneratePseudoWordsPrompt(160, new Random(7));
        Assert.True(prompt.Length >= 160);
        Assert.Contains(' ', prompt);
    }

    [Fact]
    public void TypingEngine_ForgiveErrors_RestoresAccuracyWhenFixed()
    {
        var clock = new FakeTimeProvider();
        var engine = new TypingEngine("ab", stopOnError: false, timeProvider: clock, forgiveErrors: true);

        engine.ProcessKeystroke('x');
        clock.Advance(TimeSpan.FromMilliseconds(150));
        engine.ProcessBackspace();
        clock.Advance(TimeSpan.FromMilliseconds(150));
        engine.ProcessKeystroke('a');
        clock.Advance(TimeSpan.FromMilliseconds(150));
        engine.ProcessKeystroke('b');

        SessionResult result = engine.BuildResult();
        Assert.Equal(100.0, result.AccuracyPercentage);
        Assert.Equal(1, result.Mistakes);
        Assert.Equal(0, result.UncorrectedErrors);
    }

    [Fact]
    public void TypingEngine_StopOnErrorWithForgiveErrors_RestoresAccuracyOnCorrectChar()
    {
        var clock = new FakeTimeProvider();
        var engine = new TypingEngine("ok", stopOnError: true, timeProvider: clock, forgiveErrors: true);

        engine.ProcessKeystroke('z');
        Assert.True(engine.GetSnapshot().LiveAccuracyPercentage < 100.0);

        clock.Advance(TimeSpan.FromMilliseconds(150));
        engine.ProcessKeystroke('o');
        clock.Advance(TimeSpan.FromMilliseconds(150));
        engine.ProcessKeystroke('k');

        SessionResult result = engine.BuildResult();
        Assert.Equal(100.0, result.AccuracyPercentage);
    }

    [Fact]
    public void TypingEngine_SpaceSkipsWords_AdvancesPastCurrentWord()
    {
        var clock = new FakeTimeProvider();
        var engine = new TypingEngine("apple pear", stopOnError: false, timeProvider: clock, spaceSkipsWords: true);

        engine.ProcessKeystroke('a');
        clock.Advance(TimeSpan.FromMilliseconds(100));
        engine.ProcessKeystroke(' '); // skips "pple" + consumes space

        TypingSnapshot snap = engine.GetSnapshot();
        Assert.Equal(6, snap.CursorIndex); // points at 'p' of "pear"
        Assert.Equal(4, snap.UncorrectedErrors);
        Assert.Equal('p', snap.TargetText[snap.CursorIndex]);
    }

    [Fact]
    public async Task SqliteRepository_RoundTripsNewTestAndAppearanceSettings()
    {
        var repo = new SqliteTypeForgeRepository(_dbPath);
        await repo.InitializeAsync();

        var expected = new UserSettings
        {
            ForgiveErrors = true,
            SpaceSkipsWords = true,
            TypingFontSelection = "Consolas Bold",
            WhitespaceDisplay = WhitespaceDisplayMode.Bar,
            CursorShape = CursorShapeMode.Block,
            CursorMovement = CursorMovementMode.Jumping,
            SoundMode = SoundFeedbackMode.ErrorsOnly,
            SoundEnabled = true,
            SoundVolume = 45,
            SoundTheme = SoundTheme.Typewriter,
            TestTextSource = TestTextSource.BookParagraphs,
            TestDurationMode = TestDurationMode.Characters500,
            CommonWordCount = 750,
            SelectedLanguage = "English",
            CustomWordListCsv = "test words",
            SelectedBookId = "alice_wonderland",
            SelectedBookParagraphIndex = 14,
            CustomTestText = "Custom passage body"
        };

        await repo.SaveSettingsAsync(expected);
        UserSettings loaded = await repo.GetSettingsAsync();

        Assert.True(loaded.ForgiveErrors);
        Assert.True(loaded.SpaceSkipsWords);
        Assert.Equal("Consolas Bold", loaded.TypingFontSelection);
        Assert.Equal(WhitespaceDisplayMode.Bar, loaded.WhitespaceDisplay);
        Assert.Equal(CursorShapeMode.Block, loaded.CursorShape);
        Assert.Equal(CursorMovementMode.Jumping, loaded.CursorMovement);
        Assert.Equal(SoundFeedbackMode.ErrorsOnly, loaded.SoundMode);
        Assert.Equal(45, loaded.SoundVolume);
        Assert.Equal(SoundTheme.Typewriter, loaded.SoundTheme);
        Assert.Equal(TestTextSource.BookParagraphs, loaded.TestTextSource);
        Assert.Equal(TestDurationMode.Characters500, loaded.TestDurationMode);
        Assert.Equal(750, loaded.CommonWordCount);
        Assert.Equal("test words", loaded.CustomWordListCsv);
        Assert.Equal(14, loaded.SelectedBookParagraphIndex);
        Assert.Equal("Custom passage body", loaded.CustomTestText);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }
}
