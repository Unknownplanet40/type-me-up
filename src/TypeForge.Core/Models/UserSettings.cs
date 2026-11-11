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

    /// <summary>Gets or sets the active structured course track identifier.</summary>
    public string SelectedCourseTrackId { get; set; } = "fast_touch";

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
    }
}
