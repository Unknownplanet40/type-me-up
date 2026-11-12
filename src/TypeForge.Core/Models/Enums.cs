namespace TypeForge.Core.Models;

/// <summary>
/// Identifies the training mode of a recorded typing session.
/// </summary>
public enum SessionType
{
    /// <summary>A structured lesson from the progressive typing course.</summary>
    CourseLesson = 0,

    /// <summary>An adaptive weak-key drill generated from keystroke statistics.</summary>
    AdaptivePractice = 1,

    /// <summary>A fixed-duration timed benchmark test.</summary>
    TimedTest = 2,

    /// <summary>A custom user-provided passage session.</summary>
    CustomText = 3
}

/// <summary>
/// Controls the visual color theme of the application.
/// </summary>
public enum ThemePreference
{
    /// <summary>Follow the Windows system theme setting.</summary>
    System = 0,

    /// <summary>Light theme appearance.</summary>
    Light = 1,

    /// <summary>Pure-dark default theme appearance.</summary>
    Dark = 2
}

/// <summary>
/// Defines speed and accuracy thresholds required to unlock new letters in adaptive practice.
/// </summary>
public enum AdaptiveDifficultyProfile
{
    /// <summary>Relaxed progression thresholds (650ms latency, 90% accuracy).</summary>
    Casual = 0,

    /// <summary>Balanced progression thresholds (500ms latency, 95% accuracy).</summary>
    Standard = 1,

    /// <summary>Strict proficiency thresholds (350ms latency, 98% accuracy).</summary>
    Pro = 2
}

/// <summary>
/// Selects how adaptive practice exercises generate target text.
/// </summary>
public enum AdaptiveWordMode
{
    /// <summary>Generates pronounceable phonetic pseudo-words restricted to unlocked letters.</summary>
    PhoneticPseudoWords = 0,

    /// <summary>Selects natural English dictionary words restricted to unlocked letters.</summary>
    NaturalDictionaryWords = 1
}

/// <summary>
/// Selects how keys are colored on the virtual QWERTY keyboard.
/// </summary>
public enum KeyboardColorMode
{
    /// <summary>Colors keys by per-key mastery and error intensity.</summary>
    PerformanceHeat = 0,

    /// <summary>Colors keys by touch-typing ergonomic finger assignments.</summary>
    FingerZones = 1
}

/// <summary>
/// Classifies the step role inside a multi-step course lesson unit.
/// </summary>
public enum CourseStepKind
{
    /// <summary>Read-only ergonomic or technique tip card.</summary>
    Tip = 0,

    /// <summary>Introduces newly added key placements before timed drills.</summary>
    NewKeys = 1,

    /// <summary>Focused key-combination drill.</summary>
    KeyDrill = 2,

    /// <summary>Word-building drill using current and earlier keys.</summary>
    WordDrill = 3,

    /// <summary>Sentence or paragraph flow drill.</summary>
    ParagraphDrill = 4
}

/// <summary>Text source selected for the Test view.</summary>
public enum TestTextSource
{
    /// <summary>Random frequency-filtered dictionary words.</summary>
    CommonWords = 0,

    /// <summary>Pronounceable pseudo-words generated from orthographic patterns.</summary>
    PseudoWords = 1,

    /// <summary>Sequential paragraphs from an offline book.</summary>
    BookParagraphs = 2,

    /// <summary>User-provided custom text passage.</summary>
    CustomText = 3
}

/// <summary>Duration or character-count mode on the Test screen.</summary>
public enum TestDurationMode
{
    /// <summary>15-second timed sprint.</summary>
    Seconds15 = 0,

    /// <summary>30-second timed sprint.</summary>
    Seconds30 = 1,

    /// <summary>1-minute timed test.</summary>
    Minute1 = 2,

    /// <summary>Fixed 100-character target.</summary>
    Characters100 = 3,

    /// <summary>Fixed 500-character target.</summary>
    Characters500 = 4,

    /// <summary>Fixed 1000-character target.</summary>
    Characters1000 = 5
}

/// <summary>How spaces are rendered inside the typing stream.</summary>
public enum WhitespaceDisplayMode
{
    /// <summary>Standard space without a visible glyph.</summary>
    None = 0,

    /// <summary>Bottom bracket / bar indicator.</summary>
    Bar = 1,

    /// <summary>Centered middle-dot bullet indicator.</summary>
    Bullet = 2
}

/// <summary>Visual cursor indicator shape.</summary>
public enum CursorShapeMode
{
    /// <summary>Solid filled character block.</summary>
    Block = 0,

    /// <summary>Outlined character box.</summary>
    Box = 1,

    /// <summary>Thin vertical caret line on the left edge.</summary>
    Line = 2,

    /// <summary>Horizontal underline bar beneath the active character.</summary>
    Underline = 3
}

/// <summary>Cursor animation style when advancing between characters.</summary>
public enum CursorMovementMode
{
    /// <summary>Instant discrete jump to the next character.</summary>
    Jumping = 0,

    /// <summary>Smooth eased glide to the next character.</summary>
    Smooth = 1
}

/// <summary>Audio feedback mode during typing.</summary>
public enum SoundFeedbackMode
{
    /// <summary>No keystroke or error sounds.</summary>
    None = 0,

    /// <summary>Play sound only on typing mistakes.</summary>
    ErrorsOnly = 1,

    /// <summary>Play sound only on valid keystrokes.</summary>
    KeysOnly = 2,

    /// <summary>Play both keystroke clicks and error alerts.</summary>
    All = 3
}

/// <summary>Built-in keystroke acoustic theme.</summary>
public enum SoundTheme
{
    /// <summary>Crisp balanced mechanical switch click.</summary>
    Default = 0,

    /// <summary>Dampened tactile thock with warmer low end.</summary>
    Mechanical = 1,

    /// <summary>Classic typewriter strike and muted carriage snap.</summary>
    Typewriter = 2,

    /// <summary>Soft low-profile tap.</summary>
    Soft = 3
}
