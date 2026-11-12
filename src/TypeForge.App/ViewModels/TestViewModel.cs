using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TypeForge.App.Controls;
using TypeForge.App.Services;
using TypeForge.Core.Engine;
using TypeForge.Core.Models;
using TypeForge.Core.Services;

namespace TypeForge.App.ViewModels;

public sealed record BookParagraphPreviewItem(int ParagraphNumber, string Prefix, string PreviewText, bool IsCurrent);

public sealed partial class TestViewModel : ObservableObject
{
    private readonly ITypeForgeStateService _state;
    private readonly IKeystrokeAudioService _audio;
    private TypingEngine _engine = null!;

    [ObservableProperty]
    private TestDurationMode _selectedDurationMode = TestDurationMode.Seconds15;

    [ObservableProperty]
    private string _statsBarText = "00:00:00.00 / 0 / 0.00% / 0.0wpm";

    [ObservableProperty]
    private bool _isResultsVisible;

    [ObservableProperty]
    private string _resultWpm = "0.0";

    [ObservableProperty]
    private string _resultAccuracy = "100.00%";

    [ObservableProperty]
    private string _resultChars = "0";

    [ObservableProperty]
    private string _resultElapsed = "00:00:00.00";

    [ObservableProperty]
    private string _resultMistakes = "0";

    [ObservableProperty]
    private string _resultSourceSummary = string.Empty;

    [ObservableProperty]
    private bool _isSettingsDialogOpen;

    [ObservableProperty]
    private int _activeSettingsTabIndex;

    [ObservableProperty]
    private TestTextSource _draftTextSource = TestTextSource.CommonWords;

    [ObservableProperty]
    private string _draftLanguage = "English";

    [ObservableProperty]
    private int _draftCommonWordCount = 500;

    [ObservableProperty]
    private string _draftWordListText = string.Empty;

    [ObservableProperty]
    private string _draftWordStatsText = "500 unique words   Average word length: 4.70";

    [ObservableProperty]
    private int _draftSelectedBookIndex;

    [ObservableProperty]
    private int _draftParagraphNumber = 1;

    [ObservableProperty]
    private int _draftParagraphMax = 1;

    [ObservableProperty]
    private string _draftBookTitle = string.Empty;

    [ObservableProperty]
    private string _draftBookAuthor = string.Empty;

    [ObservableProperty]
    private string _draftBookChaptersText = "0";

    [ObservableProperty]
    private string _draftBookParagraphsText = "0";

    [ObservableProperty]
    private string _draftBookAllWordsText = "0";

    [ObservableProperty]
    private string _draftBookUniqueWordsText = "0";

    [ObservableProperty]
    private string _draftBookCharsText = "0";

    [ObservableProperty]
    private string _draftBookAvgLenText = "0.00";

    [ObservableProperty]
    private string _draftCustomText = string.Empty;

    private bool _isUpdatingDraftWords;

    public IReadOnlyList<string> AvailableLanguages { get; } = new List<string> { "English" };
    public List<string> BookTitles { get; } = [];
    public List<BookParagraphPreviewItem> ParagraphPreviews { get; } = [];

    public TypingSnapshot? CurrentSnapshot { get; private set; }
    public UserSettings CurrentSettings => _state.Settings;

    public string ActiveFontSelection => _state.Settings.TypingFontSelection;
    public int ActiveFontSize => _state.Settings.TypingFontSize;
    public WhitespaceDisplayMode ActiveWhitespaceDisplay => _state.Settings.WhitespaceDisplay;
    public CursorShapeMode ActiveCursorShape => _state.Settings.CursorShape;
    public CursorMovementMode ActiveCursorMovement => _state.Settings.CursorMovement;

    public event Action<TypingSnapshot>? SnapshotUpdated;
    public event Action? AppearanceUpdated;
    public event Action? ParagraphPreviewsUpdated;

    public TestViewModel(ITypeForgeStateService state, IKeystrokeAudioService audio)
    {
        _state = state;
        _audio = audio;

        foreach (BookModel book in _state.Books)
        {
            BookTitles.Add(book.Title);
        }

        SelectedDurationMode = _state.Settings.TestDurationMode;
        LoadDraftFromSettings();
        StartNewTest();
    }

    public void RefreshFromSettings()
    {
        SelectedDurationMode = _state.Settings.TestDurationMode;
        AppearanceUpdated?.Invoke();
        if (CurrentSnapshot is not null)
        {
            SnapshotUpdated?.Invoke(CurrentSnapshot);
        }
    }

    public void SelectDurationMode(TestDurationMode mode)
    {
        SelectedDurationMode = mode;
        _state.Settings.TestDurationMode = mode;
        _state.SaveSettings();
        StartNewTest();
    }

    [RelayCommand]
    public void StartNewTest()
    {
        IsResultsVisible = false;
        string prompt = BuildTestPrompt(SelectedDurationMode);
        _engine = new TypingEngine(
            prompt,
            stopOnError: _state.Settings.StopOnError,
            sessionType: SessionType.TimedTest,
            forgiveErrors: _state.Settings.ForgiveErrors,
            spaceSkipsWords: _state.Settings.SpaceSkipsWords);

        PublishSnapshot();
    }

    public bool HandleCharacter(char inputChar)
    {
        if (IsResultsVisible || IsSettingsDialogOpen)
        {
            return false;
        }

        TypingSnapshot preSnapshot = _engine.GetSnapshot();
        bool isExpectedChar = preSnapshot.CursorIndex < preSnapshot.TargetText.Length
            && preSnapshot.TargetText[preSnapshot.CursorIndex] == inputChar;

        bool accepted = _engine.ProcessKeystroke(inputChar);
        if (!accepted)
        {
            return false;
        }

        if (isExpectedChar)
        {
            _audio.PlayClick();
        }
        else
        {
            _audio.PlayMistake();
        }

        PublishSnapshot();
        return true;
    }

    public bool HandleBackspace()
    {
        if (IsResultsVisible || IsSettingsDialogOpen)
        {
            return false;
        }

        bool moved = _engine.ProcessBackspace();
        if (moved)
        {
            PublishSnapshot();
        }

        return moved;
    }

    public void TickTimer()
    {
        if (IsResultsVisible || IsSettingsDialogOpen || CurrentSnapshot is null || CurrentSnapshot.TotalKeystrokes == 0)
        {
            return;
        }

        TypingSnapshot snapshot = _engine.GetSnapshot();
        int durationSec = TestTextGenerator.GetDurationSeconds(SelectedDurationMode);
        if (durationSec > 0 && snapshot.Elapsed.TotalSeconds >= durationSec)
        {
            FinalizeSession(TimeSpan.FromSeconds(durationSec));
            return;
        }

        UpdateStatsBar(snapshot, snapshot.Elapsed);
    }

    public void OpenSettingsDialog()
    {
        LoadDraftFromSettings();
        ActiveSettingsTabIndex = 0;
        IsSettingsDialogOpen = true;
    }

    public void ApplySettingsAndClose()
    {
        UserSettings s = _state.Settings;
        s.TestTextSource = DraftTextSource;
        s.SelectedLanguage = DraftLanguage;
        s.CommonWordCount = Math.Clamp(DraftCommonWordCount, UserSettings.MinCommonWordCount, UserSettings.MaxCommonWordCount);

        IReadOnlyList<string> sliderDefaultPool = TestTextGenerator.ResolveCommonWordPool(_state.DictionaryWords, s.CommonWordCount, null);
        string sliderCsv = string.Join(", ", sliderDefaultPool);
        s.CustomWordListCsv = string.Equals(DraftWordListText.Trim(), sliderCsv, StringComparison.Ordinal)
            ? string.Empty
            : DraftWordListText.Trim();

        BookModel? selectedBook = GetDraftBook();
        if (selectedBook is not null)
        {
            s.SelectedBookId = selectedBook.Id;
            s.SelectedBookParagraphIndex = Math.Clamp(DraftParagraphNumber - 1, 0, selectedBook.Paragraphs.Count - 1);
        }

        s.CustomTestText = DraftCustomText;

        _state.SaveSettings();
        IsSettingsDialogOpen = false;
        AppearanceUpdated?.Invoke();
        StartNewTest();
    }

    public void StepBookParagraph(int delta)
    {
        DraftParagraphNumber = Math.Clamp(DraftParagraphNumber + delta, 1, Math.Max(1, DraftParagraphMax));
    }

    partial void OnDraftCommonWordCountChanged(int value)
    {
        if (_isUpdatingDraftWords)
        {
            return;
        }

        IReadOnlyList<string> pool = TestTextGenerator.ResolveCommonWordPool(_state.DictionaryWords, value, null);
        _isUpdatingDraftWords = true;
        DraftWordListText = string.Join(", ", pool);
        _isUpdatingDraftWords = false;
        UpdateDraftWordStats(pool);
    }

    partial void OnDraftWordListTextChanged(string value)
    {
        if (_isUpdatingDraftWords)
        {
            return;
        }

        IReadOnlyList<string> pool = TestTextGenerator.ResolveCommonWordPool(
            _state.DictionaryWords,
            DraftCommonWordCount,
            value);
        UpdateDraftWordStats(pool);
    }

    partial void OnDraftSelectedBookIndexChanged(int value)
    {
        RefreshDraftBookMetadata(preserveParagraphIndex: false);
    }

    partial void OnDraftParagraphNumberChanged(int value)
    {
        RefreshBookParagraphPreviews();
    }

    private void LoadDraftFromSettings()
    {
        UserSettings s = _state.Settings;
        DraftTextSource = s.TestTextSource;
        DraftLanguage = string.IsNullOrWhiteSpace(s.SelectedLanguage) ? "English" : s.SelectedLanguage;

        _isUpdatingDraftWords = true;
        DraftCommonWordCount = Math.Clamp(s.CommonWordCount, UserSettings.MinCommonWordCount, UserSettings.MaxCommonWordCount);
        IReadOnlyList<string> pool = TestTextGenerator.ResolveCommonWordPool(
            _state.DictionaryWords,
            DraftCommonWordCount,
            s.CustomWordListCsv);
        DraftWordListText = string.IsNullOrWhiteSpace(s.CustomWordListCsv)
            ? string.Join(", ", pool)
            : s.CustomWordListCsv;
        _isUpdatingDraftWords = false;
        UpdateDraftWordStats(pool);

        int bookIdx = 0;
        for (int i = 0; i < _state.Books.Count; i++)
        {
            if (string.Equals(_state.Books[i].Id, s.SelectedBookId, StringComparison.OrdinalIgnoreCase))
            {
                bookIdx = i;
                break;
            }
        }

        DraftSelectedBookIndex = bookIdx;
        RefreshDraftBookMetadata(preserveParagraphIndex: true);

        DraftCustomText = s.CustomTestText;
    }

    private void UpdateDraftWordStats(IReadOnlyList<string> pool)
    {
        WordListStats stats = TestTextGenerator.ComputeWordStats(pool);
        DraftWordStatsText = $"{stats.UniqueWords:N0} unique words   Average word length: {stats.AverageWordLength:0.00}";
    }

    private BookModel? GetDraftBook()
    {
        if (_state.Books.Count == 0)
        {
            return null;
        }

        int idx = Math.Clamp(DraftSelectedBookIndex, 0, _state.Books.Count - 1);
        return _state.Books[idx];
    }

    private void RefreshDraftBookMetadata(bool preserveParagraphIndex)
    {
        BookModel? book = GetDraftBook();
        if (book is null)
        {
            DraftBookTitle = string.Empty;
            DraftBookAuthor = string.Empty;
            DraftParagraphMax = 1;
            DraftParagraphNumber = 1;
            ParagraphPreviews.Clear();
            return;
        }

        DraftBookTitle = book.Title;
        DraftBookAuthor = $"by {book.Author}";
        DraftBookChaptersText = book.Chapters.ToString("N0");
        DraftBookParagraphsText = book.Paragraphs.Count.ToString("N0");
        DraftBookAllWordsText = book.AllWords.ToString("N0");
        DraftBookUniqueWordsText = book.UniqueWords.ToString("N0");
        DraftBookCharsText = book.Characters.ToString("N0");
        DraftBookAvgLenText = book.AverageWordLength.ToString("0.00");
        DraftParagraphMax = book.Paragraphs.Count;

        int targetNum = preserveParagraphIndex
            ? Math.Clamp(_state.Settings.SelectedBookParagraphIndex + 1, 1, book.Paragraphs.Count)
            : 1;
        DraftParagraphNumber = targetNum;
        RefreshBookParagraphPreviews();
    }

    private void RefreshBookParagraphPreviews()
    {
        ParagraphPreviews.Clear();
        BookModel? book = GetDraftBook();
        if (book is null || book.Paragraphs.Count == 0)
        {
            return;
        }

        int zeroBased = Math.Clamp(DraftParagraphNumber - 1, 0, book.Paragraphs.Count - 1);
        int start = Math.Clamp(zeroBased - 1, 0, Math.Max(0, book.Paragraphs.Count - 3));
        int end = Math.Min(book.Paragraphs.Count - 1, start + 2);

        for (int i = start; i <= end; i++)
        {
            bool isCurrent = i == zeroBased;
            string raw = book.Paragraphs[i].Trim();
            string snippet = raw.Length > 135 ? $"{raw[..135]}..." : raw;
            ParagraphPreviews.Add(new BookParagraphPreviewItem(
                ParagraphNumber: i + 1,
                Prefix: isCurrent ? $"➤ #{i + 1}" : $"   #{i + 1}",
                PreviewText: snippet,
                IsCurrent: isCurrent));
        }

        ParagraphPreviewsUpdated?.Invoke();
    }

    private string BuildTestPrompt(TestDurationMode mode)
    {
        int targetChars = TestTextGenerator.GetTargetCharactersForMode(mode);
        UserSettings s = _state.Settings;

        string raw = s.TestTextSource switch
        {
            TestTextSource.PseudoWords => TestTextGenerator.GeneratePseudoWordsPrompt(Math.Max(targetChars, 140)),
            TestTextSource.BookParagraphs => BuildBookPrompt(targetChars),
            TestTextSource.CustomText => BuildCustomPrompt(),
            _ => BuildCommonWordsPrompt(Math.Max(targetChars, 140))
        };

        if (TestTextGenerator.IsCharacterCountMode(mode) && raw.Length > targetChars)
        {
            raw = raw[..targetChars].TrimEnd();
            if (raw.Length < targetChars)
            {
                raw = raw.PadRight(targetChars, '.');
            }
        }

        return raw;
    }

    private string BuildCommonWordsPrompt(int targetChars)
    {
        IReadOnlyList<string> pool = TestTextGenerator.ResolveCommonWordPool(
            _state.DictionaryWords,
            _state.Settings.CommonWordCount,
            _state.Settings.CustomWordListCsv);
        return TestTextGenerator.GenerateCommonWordsPrompt(pool, targetChars);
    }

    private string BuildBookPrompt(int targetChars)
    {
        BookModel? book = _state.Books.FirstOrDefault(
            b => string.Equals(b.Id, _state.Settings.SelectedBookId, StringComparison.OrdinalIgnoreCase))
            ?? _state.Books.FirstOrDefault();

        if (book is null)
        {
            return BuildCommonWordsPrompt(targetChars);
        }

        return TestTextGenerator.GenerateBookPrompt(
            book,
            _state.Settings.SelectedBookParagraphIndex,
            Math.Max(targetChars, 220));
    }

    private string BuildCustomPrompt()
    {
        string normalized = string.Join(' ', _state.Settings.CustomTestText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(normalized)
            ? "Paste or write your own custom passage in Test Settings to begin."
            : normalized;
    }

    private void PublishSnapshot()
    {
        TypingSnapshot snapshot = _engine.GetSnapshot();
        CurrentSnapshot = snapshot;
        UpdateStatsBar(snapshot, snapshot.Elapsed);
        SnapshotUpdated?.Invoke(snapshot);

        if (snapshot.IsCompleted && !IsResultsVisible)
        {
            FinalizeSession(snapshot.Elapsed);
        }
    }

    private void UpdateStatsBar(TypingSnapshot snapshot, TimeSpan elapsed)
    {
        string clock = $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds / 10:00}";
        int charsTyped = snapshot.TypedText.Length;
        double acc = snapshot.TotalKeystrokes == 0 ? 0.0 : snapshot.LiveAccuracyPercentage;
        StatsBarText = $"{clock} / {charsTyped} / {acc:0.00}% / {snapshot.LiveNetWpm:0.0}wpm";
    }

    private void FinalizeSession(TimeSpan? forcedDuration = null)
    {
        SessionResult built = _engine.BuildResult();
        if (forcedDuration.HasValue && forcedDuration.Value > TimeSpan.Zero)
        {
            TimeSpan clampedDuration = forcedDuration.Value;
            int typedLen = _engine.GetSnapshot().TypedText.Length;
            built = new SessionResult
            {
                SessionId = built.SessionId,
                SessionType = built.SessionType,
                LessonId = built.LessonId,
                StartedAt = built.StartedAt,
                Duration = clampedDuration,
                GrossWpm = MetricsCalculator.CalculateGrossWpm(typedLen, clampedDuration),
                NetWpm = MetricsCalculator.CalculateNetWpm(typedLen, built.UncorrectedErrors, clampedDuration),
                AccuracyPercentage = built.AccuracyPercentage,
                CorrectCharacters = built.CorrectCharacters,
                TotalKeystrokes = built.TotalKeystrokes,
                Mistakes = built.Mistakes,
                UncorrectedErrors = built.UncorrectedErrors,
                KeyBreakdown = built.KeyBreakdown
            };
        }

        _state.RecordSession(built);

        TimeSpan elapsed = built.Duration;
        ResultWpm = $"{built.NetWpm:0.0}";
        ResultAccuracy = $"{built.AccuracyPercentage:0.00}%";
        ResultChars = $"{built.CorrectCharacters + built.UncorrectedErrors}";
        ResultElapsed = $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds / 10:00}";
        ResultMistakes = $"{built.Mistakes}";
        ResultSourceSummary = $"{SelectedDurationModeLabel(SelectedDurationMode)} · {SourceLabel(_state.Settings.TestTextSource)}";
        UpdateStatsBar(_engine.GetSnapshot(), elapsed);

        IsResultsVisible = true;
    }

    private static string SelectedDurationModeLabel(TestDurationMode mode) => mode switch
    {
        TestDurationMode.Seconds15 => "15 seconds",
        TestDurationMode.Seconds30 => "30 seconds",
        TestDurationMode.Minute1 => "one minute",
        TestDurationMode.Characters100 => "100 characters",
        TestDurationMode.Characters500 => "500 characters",
        TestDurationMode.Characters1000 => "1000 characters",
        _ => "15 seconds"
    };

    private static string SourceLabel(TestTextSource source) => source switch
    {
        TestTextSource.PseudoWords => "Pseudo words",
        TestTextSource.BookParagraphs => "Book paragraphs",
        TestTextSource.CustomText => "Custom text",
        _ => "Common words"
    };
}
