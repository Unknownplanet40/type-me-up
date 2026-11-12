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

    /// <summary>Gets or sets the daily active practice target in minutes (defaults to 15 minutes for new users).</summary>
    public int DailyGoalMinutes { get; set; } = 15;

    /// <summary>Gets or sets a value indicating whether course drills automatically adapt duration to user mastery.</summary>
    public bool OptimalDurationEnabled { get; set; } = true;

    /// <summary>Minimum allowed common-words pool size.</summary>
    public const int MinCommonWordCount = 100;

    /// <summary>Maximum allowed common-words pool size.</summary>
    public const int MaxCommonWordCount = 1000;

    /// <summary>Minimum allowed pace caret target WPM.</summary>
    public const int MinPaceTargetWpm = 10;

    /// <summary>Maximum allowed pace caret target WPM.</summary>
    public const int MaxPaceTargetWpm = 250;

    /// <summary>Minimum allowed rhythm metronome target WPM.</summary>
    public const int MinMetronomeWpm = 20;

    /// <summary>Maximum allowed rhythm metronome target WPM.</summary>
    public const int MaxMetronomeWpm = 120;

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

    /// <summary>Gets or sets the selected real-world text pool identifier in Test mode (e.g. "quotes", "code_snippets", "numbers", "punctuation").</summary>
    public string SelectedRealWorldPoolId { get; set; } = "quotes";

    /// <summary>Gets or sets the active pace caret mode.</summary>
    public PaceCaretMode PaceCaretMode { get; set; } = PaceCaretMode.Off;

    /// <summary>Gets or sets the target WPM for FixedWpm pace caret mode.</summary>
    public int PaceTargetWpm { get; set; } = 60;

    /// <summary>Gets or sets the virtual keyboard label fade stage.</summary>
    public KeyboardFadeStage KeyboardFadeStage { get; set; } = KeyboardFadeStage.FullLabels;

    /// <summary>Gets or sets when the finger-guide hand diagram is displayed.</summary>
    public FingerGuideMode FingerGuideMode { get; set; } = FingerGuideMode.CourseDefault;

    /// <summary>Gets or sets whether the rhythm metronome is enabled during typing sessions.</summary>
    public bool MetronomeEnabled { get; set; } = false;

    /// <summary>Gets or sets whether the metronome beats per keystroke or per 5-character word.</summary>
    public MetronomeBeatMode MetronomeBeatMode { get; set; } = MetronomeBeatMode.PerKeystroke;

    /// <summary>Gets or sets whether the metronome uses a fixed WPM or auto (personal average + 5 WPM).</summary>
    public MetronomeTargetMode MetronomeTargetMode { get; set; } = MetronomeTargetMode.Fixed;

    /// <summary>Gets or sets the fixed rhythm metronome target WPM (20–120).</summary>
    public int MetronomeTargetWpm { get; set; } = 40;

    /// <summary>Gets or sets whether the metronome plays an audio tick on each beat.</summary>
    public bool MetronomeAudioEnabled { get; set; } = true;

    /// <summary>Gets or sets whether the metronome pulses a visual indicator in the HUD on each beat.</summary>
    public bool MetronomeVisualEnabled { get; set; } = true;

    /// <summary>Gets or sets whether WPM is always shown during early accuracy-first course lessons.</summary>
    public bool ShowWpmInCourse { get; set; } = false;

    /// <summary>Minimum allowed configurable course pass accuracy percentage.</summary>
    public const double MinCourseAccuracy = 50.0;

    /// <summary>Maximum allowed configurable course pass accuracy percentage.</summary>
    public const double MaxCourseAccuracy = 100.0;

    /// <summary>Minimum allowed configurable course mistake limit.</summary>
    public const int MinCourseMaxMistakes = 0;

    /// <summary>Maximum allowed configurable course mistake limit.</summary>
    public const int MaxCourseMaxMistakes = 50;

    /// <summary>Gets or sets whether course exercises require meeting minimum accuracy and maximum mistakes to proceed.</summary>
    public bool CoursePassGateEnabled { get; set; } = true;

    /// <summary>Gets or sets the minimum accuracy percentage required to pass a course exercise (default 90%).</summary>
    public double CourseMinAccuracy { get; set; } = 90.0;

    /// <summary>Gets or sets the maximum mistakes allowed to pass a course exercise (default 5).</summary>
    public int CourseMaxMistakes { get; set; } = 5;

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

        if (PaceTargetWpm < MinPaceTargetWpm || PaceTargetWpm > MaxPaceTargetWpm)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PaceTargetWpm),
                $"Pace target WPM must be between {MinPaceTargetWpm} and {MaxPaceTargetWpm}.");
        }

        if (CourseMinAccuracy < MinCourseAccuracy || CourseMinAccuracy > MaxCourseAccuracy)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CourseMinAccuracy),
                $"Course minimum accuracy must be between {MinCourseAccuracy} and {MaxCourseAccuracy}.");
        }

        if (CourseMaxMistakes < MinCourseMaxMistakes || CourseMaxMistakes > MaxCourseMaxMistakes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CourseMaxMistakes),
                $"Course maximum mistakes must be between {MinCourseMaxMistakes} and {MaxCourseMaxMistakes}.");
        }

        if (MetronomeTargetWpm < MinMetronomeWpm || MetronomeTargetWpm > MaxMetronomeWpm)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MetronomeTargetWpm),
                $"Metronome target WPM must be between {MinMetronomeWpm} and {MaxMetronomeWpm}.");
        }
    }
}
