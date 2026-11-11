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
