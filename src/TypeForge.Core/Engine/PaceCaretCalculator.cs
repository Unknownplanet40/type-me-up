using TypeForge.Core.Models;

namespace TypeForge.Core.Engine;

/// <summary>
/// Pure pace-caret (ghost cursor) index and target WPM calculator.
/// </summary>
public static class PaceCaretCalculator
{
    /// <summary>
    /// Standard milliseconds per character at 1 WPM (<c>60,000 ms / 5 chars per word = 12,000</c>).
    /// </summary>
    public const double MillisecondsPerCharAtOneWpm = 12000.0;

    /// <summary>
    /// Calculates the zero-based character index of the ghost pace caret for a given elapsed time and target WPM.
    /// Formula: <c>ms per char = 12000 / WPM</c>.
    /// </summary>
    /// <param name="elapsed">Elapsed active typing duration.</param>
    /// <param name="targetWpm">Target words per minute.</param>
    /// <param name="textLength">Total length of the target prompt text.</param>
    /// <returns>Clamped character index in <c>[0, textLength - 1]</c>, or <c>0</c> if inactive.</returns>
    public static int GetGhostIndex(TimeSpan elapsed, double targetWpm, int textLength)
    {
        if (textLength <= 0 || elapsed <= TimeSpan.Zero || targetWpm <= 0.0 || double.IsNaN(targetWpm) || double.IsInfinity(targetWpm))
        {
            return 0;
        }

        double msPerChar = MillisecondsPerCharAtOneWpm / targetWpm;
        int rawIndex = (int)Math.Floor(elapsed.TotalMilliseconds / msPerChar);
        return Math.Clamp(rawIndex, 0, textLength - 1);
    }

    /// <summary>
    /// Resolves the effective target WPM for the configured <see cref="PaceCaretMode"/>.
    /// </summary>
    /// <param name="mode">Selected pace caret mode.</param>
    /// <param name="fixedTargetWpm">User-configured fixed WPM target.</param>
    /// <param name="sessions">Recorded session history (newest first).</param>
    /// <returns>Effective target WPM, or <c>0</c> when <see cref="PaceCaretMode.Off"/>.</returns>
    public static double ResolveTargetWpm(
        PaceCaretMode mode,
        int fixedTargetWpm,
        IReadOnlyList<SessionResult>? sessions)
    {
        int clampedFixed = Math.Clamp(fixedTargetWpm, UserSettings.MinPaceTargetWpm, UserSettings.MaxPaceTargetWpm);

        return mode switch
        {
            PaceCaretMode.Off => 0.0,
            PaceCaretMode.FixedWpm => clampedFixed,
            PaceCaretMode.PersonalBest => sessions is { Count: > 0 }
                ? Math.Max(UserSettings.MinPaceTargetWpm, Math.Round(sessions.Max(s => s.NetWpm), 1))
                : clampedFixed,
            PaceCaretMode.LastSession => sessions is { Count: > 0 }
                ? Math.Max(UserSettings.MinPaceTargetWpm, Math.Round(sessions[0].NetWpm, 1))
                : clampedFixed,
            _ => 0.0
        };
    }
}
