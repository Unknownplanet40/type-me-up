using TypeForge.Core.Models;

namespace TypeForge.Core.Engine;

/// <summary>
/// Pure cadence math helpers for the Rhythm Metronome &amp; Cadence Trainer.
/// </summary>
public static class MetronomeCalculator
{
    /// <summary>Resolves the effective metronome target WPM (clamped to 20–120 WPM).</summary>
    public static int ResolveTargetWpm(MetronomeTargetMode mode, int fixedWpm, double personalAverageNetWpm)
    {
        int clampedFixed = Math.Clamp(fixedWpm, UserSettings.MinMetronomeWpm, UserSettings.MaxMetronomeWpm);
        if (mode == MetronomeTargetMode.Auto && personalAverageNetWpm > 0.0)
        {
            int autoWpm = (int)Math.Round(personalAverageNetWpm + 5.0);
            return Math.Clamp(autoWpm, UserSettings.MinMetronomeWpm, UserSettings.MaxMetronomeWpm);
        }

        return clampedFixed;
    }

    /// <summary>Computes the beat interval in milliseconds for the given target WPM and subdivision mode.</summary>
    public static double GetBeatIntervalMs(int targetWpm, MetronomeBeatMode beatMode)
    {
        int clampedWpm = Math.Clamp(targetWpm, UserSettings.MinMetronomeWpm, UserSettings.MaxMetronomeWpm);
        return beatMode == MetronomeBeatMode.PerWord
            ? 60_000.0 / clampedWpm
            : 12_000.0 / clampedWpm;
    }
}
