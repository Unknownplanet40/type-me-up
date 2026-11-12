namespace TypeForge.Core.Models;

/// <summary>
/// Stores user preferences persisted locally across sessions.
/// </summary>
public sealed class UserSettings
{
    /// <summary>Minimum allowed typing stream font size in pixels.</summary>
    public const int MinFontSize = 16;

    /// <summary>Maximum allowed typing stream font size in pixels.</summary>
    public const int MaxFontSize = 48;

    /// <summary>Minimum allowed daily practice goal in minutes.</summary>
    public const int MinDailyGoalMinutes = 5;

    /// <summary>Maximum allowed daily practice goal in minutes.</summary>
    public const int MaxDailyGoalMinutes = 180;

    /// <summary>Gets or sets the preferred application color theme.</summary>
    public ThemePreference Theme { get; set; } = ThemePreference.Dark;

    /// <summary>Gets or sets the typing text stream font size in pixels.</summary>
    public int TypingFontSize { get; set; } = 28;

    /// <summary>Gets or sets a value indicating whether the virtual keyboard is shown during practice.</summary>
    public bool ShowVirtualKeyboard { get; set; } = true;

    /// <summary>Gets or sets the virtual keyboard coloring scheme.</summary>
    public KeyboardColorMode KeyboardColorMode { get; set; } = KeyboardColorMode.PerformanceHeat;

    /// <summary>Gets or sets a value indicating whether keystroke audio feedback is enabled.</summary>
    public bool SoundEnabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether typing halts on mistakes until corrected.</summary>
    public bool StopOnError { get; set; } = false;

    /// <summary>Gets or sets the difficulty thresholds for adaptive letter unlocks.</summary>
    public AdaptiveDifficultyProfile AdaptiveProfile { get; set; } = AdaptiveDifficultyProfile.Standard;

    /// <summary>Gets or sets the word generation mode for adaptive drills.</summary>
    public AdaptiveWordMode AdaptiveWordMode { get; set; } = AdaptiveWordMode.PhoneticPseudoWords;

    /// <summary>Gets or sets the daily active practice target in minutes.</summary>
    public int DailyGoalMinutes { get; set; } = 30;

    /// <summary>Gets or sets a value indicating whether course drills automatically adapt duration to user mastery.</summary>
    public bool OptimalDurationEnabled { get; set; } = true;

    /// <summary>Minimum allowed common-words pool size.</summary>
    public const int MinCommonWordCount = 100;

    /// <summary>Maximum allowed common-words pool size.</summary>
    public const int MaxCommonWordCount = 1000;

    /// <summary>Gets or sets the active structured course track identifier.</summary>
    public string SelectedCourseTrackId { get; set; } = "fast_touch";

    /// <summary>Gets or sets whether corrected mistakes are forgiven in accuracy calculation.</summary>
    public bool ForgiveErrors { get; set; } = false;

    /// <summary>Gets or sets whether pressing space inside a word skips the rest of that word as missed.</summary>
    public bool SpaceSkipsWords { get; set; } = false;

    /// <summary>Gets or sets the selected typing font family and optional style suffix (e.g. "Cascadia Mono Bold").</summary>
    public string TypingFontSelection { get; set; } = "Cascadia Mono";

    /// <summary>Gets or sets how spaces appear in the typing stream.</summary>
    public WhitespaceDisplayMode WhitespaceDisplay { get; set; } = WhitespaceDisplayMode.Bullet;

    /// <summary>Gets or sets the visual cursor indicator shape.</summary>
    public CursorShapeMode CursorShape { get; set; } = CursorShapeMode.Underline;

    /// <summary>Gets or sets how the cursor moves between characters.</summary>
    public CursorMovementMode CursorMovement { get; set; } = CursorMovementMode.Smooth;

    /// <summary>Gets or sets which typing events trigger audio feedback.</summary>
    public SoundFeedbackMode SoundMode { get; set; } = SoundFeedbackMode.All;

    /// <summary>Gets or sets the keystroke audio volume from 0 to 100.</summary>
    public int SoundVolume { get; set; } = 80;

    /// <summary>Gets or sets the selected keystroke acoustic profile.</summary>
    public SoundTheme SoundTheme { get; set; } = SoundTheme.Default;

    /// <summary>Gets or sets the active text source in Test mode.</summary>
    public TestTextSource TestTextSource { get; set; } = TestTextSource.CommonWords;

    /// <summary>Gets or sets the active duration or character length target in Test mode.</summary>
    public TestDurationMode TestDurationMode { get; set; } = TestDurationMode.Seconds15;

    /// <summary>Gets or sets how many frequent dictionary words are included in Test mode.</summary>
    public int CommonWordCount { get; set; } = 500;

    /// <summary>Gets or sets the selected common-words language.</summary>
    public string SelectedLanguage { get; set; } = "English";

    /// <summary>Gets or sets optional user-edited word list override for Common Words mode.</summary>
    public string CustomWordListCsv { get; set; } = string.Empty;

    /// <summary>Gets or sets the selected book identifier for Book Paragraphs mode.</summary>
    public string SelectedBookId { get; set; } = "alice_wonderland";

    /// <summary>Gets or sets the zero-based paragraph index in Book Paragraphs mode.</summary>
    public int SelectedBookParagraphIndex { get; set; } = 0;

    /// <summary>Gets or sets custom passage text for Custom Text mode.</summary>
    public string CustomTestText { get; set; } = string.Empty;

    /// <summary>
    /// Validates user preference bounds and throws if out of range.
    /// </summary>
    public void Validate()
    {
        if (TypingFontSize < MinFontSize || TypingFontSize > MaxFontSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(TypingFontSize),
                $"Font size must be between {MinFontSize} and {MaxFontSize}.");
        }

        if (DailyGoalMinutes < MinDailyGoalMinutes || DailyGoalMinutes > MaxDailyGoalMinutes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DailyGoalMinutes),
                $"Daily goal must be between {MinDailyGoalMinutes} and {MaxDailyGoalMinutes} minutes.");
        }

        if (CommonWordCount < MinCommonWordCount || CommonWordCount > MaxCommonWordCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CommonWordCount),
                $"Common word count must be between {MinCommonWordCount} and {MaxCommonWordCount}.");
        }

        if (SoundVolume < 0 || SoundVolume > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SoundVolume),
                "Sound volume must be between 0 and 100.");
        }

        if (SelectedBookParagraphIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SelectedBookParagraphIndex),
                "Paragraph index cannot be negative.");
        }
    }
}
