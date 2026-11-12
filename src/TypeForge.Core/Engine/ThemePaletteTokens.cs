using System.Globalization;

namespace TypeForge.Core.Engine;

/// <summary>
/// Represents the canonical color tokens for a TypeForge theme (Dark or Light) used for WCAG contrast verification.
/// </summary>
public sealed record ThemeTokenSet(
    string ThemeName,
    string CanvasHex,
    string CardHex,
    string CardInnerHex,
    string KeyNeutralHex,
    string KeyNeutralBorderHex,
    string TextPrimaryHex,
    string TextSecondaryHex,
    string TextMutedHex,
    string TextDisabledHex);

/// <summary>
/// Provides canonical Dark and Light theme color tokens and pure WCAG 2.1 contrast ratio calculations.
/// </summary>
public static class ThemePaletteTokens
{
    /// <summary>Minimum WCAG contrast ratio required for muted / secondary body text (4.5:1).</summary>
    public const double MinMutedTextContrast = 4.5;

    /// <summary>Minimum WCAG contrast ratio required for disabled / locked text and UI indicators (3.0:1).</summary>
    public const double MinDisabledTextContrast = 3.0;

    /// <summary>Gets the canonical Dark theme token table matching <c>App.xaml</c>.</summary>
    public static ThemeTokenSet Dark { get; } = new(
        ThemeName: "Dark",
        CanvasHex: "#C80D0D0D",
        CardHex: "#BF141418",
        CardInnerHex: "#BF19191E",
        KeyNeutralHex: "#18181E",
        KeyNeutralBorderHex: "#2E2E38",
        TextPrimaryHex: "#F4F4F5",
        TextSecondaryHex: "#A1A1AA",
        TextMutedHex: "#8E8E99",
        TextDisabledHex: "#6E6E78");

    /// <summary>Gets the canonical Light theme token table matching <c>App.xaml</c>.</summary>
    public static ThemeTokenSet Light { get; } = new(
        ThemeName: "Light",
        CanvasHex: "#CCFAFAFA",
        CardHex: "#D9FFFFFF",
        CardInnerHex: "#CCF4F4F5",
        KeyNeutralHex: "#F4F4F5",
        KeyNeutralBorderHex: "#D4D4D8",
        TextPrimaryHex: "#18181B",
        TextSecondaryHex: "#52525B",
        TextMutedHex: "#63636E",
        TextDisabledHex: "#7D7D87");

    /// <summary>
    /// Parses a 6-digit (<c>#RRGGBB</c>) or 8-digit (<c>#AARRGGBB</c>) hex color string into RGB bytes.
    /// </summary>
    public static (byte R, byte G, byte B) ParseHexRgb(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);
        string cleaned = hex.Trim().TrimStart('#');

        if (cleaned.Length == 8)
        {
            // Strip leading alpha byte (#AARRGGBB -> RRGGBB)
            cleaned = cleaned[2..];
        }

        if (cleaned.Length != 6)
        {
            throw new FormatException($"Expected 6 or 8 hex digits in color token '{hex}'.");
        }

        byte r = byte.Parse(cleaned[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte g = byte.Parse(cleaned[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte b = byte.Parse(cleaned[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (r, g, b);
    }

    /// <summary>
    /// Computes WCAG 2.1 relative luminance in the range [0.0, 1.0].
    /// </summary>
    public static double CalculateRelativeLuminance(byte r, byte g, byte b)
    {
        static double ChannelLuminance(byte channel)
        {
            double sRgb = channel / 255.0;
            return sRgb <= 0.03928
                ? sRgb / 12.92
                : Math.Pow((sRgb + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * ChannelLuminance(r)) +
               (0.7152 * ChannelLuminance(g)) +
               (0.0722 * ChannelLuminance(b));
    }

    /// <summary>
    /// Computes WCAG 2.1 contrast ratio between two hex colors in the range [1.0, 21.0].
    /// </summary>
    public static double CalculateContrastRatio(string hexA, string hexB)
    {
        (byte r1, byte g1, byte b1) = ParseHexRgb(hexA);
        (byte r2, byte g2, byte b2) = ParseHexRgb(hexB);

        double lum1 = CalculateRelativeLuminance(r1, g1, b1);
        double lum2 = CalculateRelativeLuminance(r2, g2, b2);

        double lighter = Math.Max(lum1, lum2);
        double darker = Math.Min(lum1, lum2);
        return Math.Round((lighter + 0.05) / (darker + 0.05), 2);
    }

    /// <summary>
    /// Validates that primary, secondary, and muted text meet >= 4.5:1 and disabled/locked text meets >= 3.0:1
    /// against all surface backgrounds (Canvas, Card, CardInner, KeyNeutral).
    /// </summary>
    public static IReadOnlyList<string> ValidateContrastThresholds(ThemeTokenSet tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        var violations = new List<string>();
        (string Name, string Hex)[] surfaces =
        [
            ("Canvas", tokens.CanvasHex),
            ("Card", tokens.CardHex),
            ("CardInner", tokens.CardInnerHex),
            ("KeyNeutral", tokens.KeyNeutralHex)
        ];

        (string Name, string Hex, double MinRatio)[] textRoles =
        [
            ("TextPrimary", tokens.TextPrimaryHex, MinMutedTextContrast),
            ("TextSecondary", tokens.TextSecondaryHex, MinMutedTextContrast),
            ("TextMuted", tokens.TextMutedHex, MinMutedTextContrast),
            ("TextDisabled", tokens.TextDisabledHex, MinDisabledTextContrast)
        ];

        foreach ((string surfaceName, string surfaceHex) in surfaces)
        {
            foreach ((string roleName, string textHex, double minRatio) in textRoles)
            {
                double ratio = CalculateContrastRatio(textHex, surfaceHex);
                if (ratio < minRatio)
                {
                    violations.Add($"{tokens.ThemeName}.{roleName} ({textHex}) on {surfaceName} ({surfaceHex}) = {ratio:0.00}:1 (min {minRatio:0.0}:1)");
                }
            }
        }

        return violations;
    }
}
