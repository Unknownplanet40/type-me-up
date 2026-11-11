using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TypeForge.Core.Models;
using Windows.UI;

namespace TypeForge.App.Controls;

public sealed partial class VirtualKeyboardControl : UserControl
{
    private readonly Dictionary<char, (Border KeyBorder, TextBlock Label)> _keyLookup;

    public VirtualKeyboardControl()
    {
        InitializeComponent();

        _keyLookup = new Dictionary<char, (Border, TextBlock)>
        {
            ['Q'] = (Key_Q, Lbl_Q), ['W'] = (Key_W, Lbl_W), ['E'] = (Key_E, Lbl_E), ['R'] = (Key_R, Lbl_R),
            ['T'] = (Key_T, Lbl_T), ['Y'] = (Key_Y, Lbl_Y), ['U'] = (Key_U, Lbl_U), ['I'] = (Key_I, Lbl_I),
            ['O'] = (Key_O, Lbl_O), ['P'] = (Key_P, Lbl_P),
            ['A'] = (Key_A, Lbl_A), ['S'] = (Key_S, Lbl_S), ['D'] = (Key_D, Lbl_D), ['F'] = (Key_F, Lbl_F),
            ['G'] = (Key_G, Lbl_G), ['H'] = (Key_H, Lbl_H), ['J'] = (Key_J, Lbl_J), ['K'] = (Key_K, Lbl_K),
            ['L'] = (Key_L, Lbl_L), [';'] = (Key_Semicolon, Lbl_Semicolon), ['\''] = (Key_Quote, Lbl_Quote),
            ['Z'] = (Key_Z, Lbl_Z), ['X'] = (Key_X, Lbl_X), ['C'] = (Key_C, Lbl_C), ['V'] = (Key_V, Lbl_V),
            ['B'] = (Key_B, Lbl_B), ['N'] = (Key_N, Lbl_N), ['M'] = (Key_M, Lbl_M),
            [','] = (Key_Comma, Lbl_Comma), ['.'] = (Key_Period, Lbl_Period), ['/'] = (Key_Slash, Lbl_Slash),
            [' '] = (Key_Space, Lbl_Space)
        };
    }

    public void UpdateState(
        char activeTargetKey,
        IReadOnlyDictionary<char, KeyMetric> keyMetrics,
        KeyboardColorMode colorMode)
    {
        string themeKey = ActualTheme == ElementTheme.Light ? "Light" : "Dark";
        var dict = Application.Current.Resources.ThemeDictionaries[themeKey] as ResourceDictionary;
        if (dict is null)
        {
            return;
        }

        char upperTarget = char.ToUpperInvariant(activeTargetKey);
        var neutralFill = (Brush)dict["ForgeKeyNeutralBrush"];
        var neutralBorder = (Brush)dict["ForgeKeyNeutralBorderBrush"];
        var mutedText = (Brush)dict["ForgeTextMutedBrush"];
        var purpleBrush = (Brush)dict["ForgePurpleBrush"];
        var purpleFill = (Brush)dict["ForgePillSelectedBrush"];

        foreach (KeyValuePair<char, (Border KeyBorder, TextBlock Label)> kvp in _keyLookup)
        {
            char keyChar = kvp.Key;
            Border border = kvp.Value.KeyBorder;
            TextBlock label = kvp.Value.Label;
            KeyMetric? metric = ResolveCaseCombinedMetric(keyChar, keyMetrics);

            string tipText = metric is not null && metric.TotalKeystrokes > 0
                ? $"{(keyChar == ' ' ? "Space" : keyChar)} · {metric.AccuracyPercentage:0.0}% acc · {metric.AverageLatencyMs:0} ms · {metric.TotalKeystrokes} strokes"
                : $"{(keyChar == ' ' ? "Space" : keyChar)} · No samples recorded yet";
            ToolTipService.SetToolTip(border, tipText);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(border, tipText);

            if (keyChar == upperTarget)
            {
                border.Background = purpleFill;
                border.BorderBrush = purpleBrush;
                border.BorderThickness = new Thickness(1.75);
                label.Foreground = purpleBrush;
                continue;
            }

            border.BorderThickness = new Thickness(1);

            if (colorMode == KeyboardColorMode.FingerZones && char.IsLetter(keyChar))
            {
                (Brush zoneFill, Brush zoneBorder, Brush zoneText) = GetFingerZoneColors(keyChar);
                border.Background = zoneFill;
                border.BorderBrush = zoneBorder;
                label.Foreground = zoneText;
            }
            else if (metric is not null && metric.TotalKeystrokes > 0)
            {
                if (metric.AccuracyPercentage >= 96.0)
                {
                    border.Background = (Brush)dict["ForgeGreenFillBrush"];
                    border.BorderBrush = (Brush)dict["ForgeGreenBorderBrush"];
                    label.Foreground = (Brush)dict["ForgeGreenBrush"];
                }
                else if (metric.AccuracyPercentage >= 90.0)
                {
                    border.Background = (Brush)dict["ForgeYellowFillBrush"];
                    border.BorderBrush = (Brush)dict["ForgeYellowBorderBrush"];
                    label.Foreground = (Brush)dict["ForgeYellowBrush"];
                }
                else
                {
                    border.Background = (Brush)dict["ForgeRedFillBrush"];
                    border.BorderBrush = (Brush)dict["ForgeRedBorderBrush"];
                    label.Foreground = (Brush)dict["ForgeRedBrush"];
                }
            }
            else
            {
                border.Background = neutralFill;
                border.BorderBrush = neutralBorder;
                label.Foreground = mutedText;
            }
        }

        if (colorMode == KeyboardColorMode.FingerZones)
        {
            FingerHintText.Text = $"Finger guide: {GetFingerName(upperTarget)}";
            FingerHintText.Visibility = Visibility.Visible;
        }
        else
        {
            FingerHintText.Visibility = Visibility.Collapsed;
        }
    }

    private static KeyMetric? ResolveCaseCombinedMetric(char upperOrSymbolKey, IReadOnlyDictionary<char, KeyMetric> metrics)
    {
        metrics.TryGetValue(upperOrSymbolKey, out KeyMetric? upper);
        char lowerChar = char.ToLowerInvariant(upperOrSymbolKey);
        KeyMetric? lower = null;
        if (lowerChar != upperOrSymbolKey)
        {
            metrics.TryGetValue(lowerChar, out lower);
        }

        if (upper is null)
        {
            return lower;
        }

        if (lower is null)
        {
            return upper;
        }

        int total = upper.TotalKeystrokes + lower.TotalKeystrokes;
        if (total == 0)
        {
            return upper;
        }

        double weightedLatency = ((upper.AverageLatencyMs * upper.TotalKeystrokes) + (lower.AverageLatencyMs * lower.TotalKeystrokes)) / total;
        return new KeyMetric(
            upperOrSymbolKey,
            total,
            upper.CorrectKeystrokes + lower.CorrectKeystrokes,
            upper.IncorrectKeystrokes + lower.IncorrectKeystrokes,
            Math.Round(weightedLatency, 2));
    }

    private static (Brush Fill, Brush Border, Brush Text) GetFingerZoneColors(char ch)
    {
        Color tint = ch switch
        {
            'Q' or 'A' or 'Z' or 'P' => Color.FromArgb(0xFF, 0x1E, 0x31, 0x20),
            'W' or 'S' or 'X' or 'O' => Color.FromArgb(0xFF, 0x2F, 0x31, 0x1B),
            'E' or 'D' or 'C' or 'I' or 'K' => Color.FromArgb(0xFF, 0x34, 0x28, 0x17),
            'R' or 'T' or 'F' or 'G' or 'V' or 'B' => Color.FromArgb(0xFF, 0x1B, 0x2D, 0x2F),
            _ => Color.FromArgb(0xFF, 0x31, 0x1E, 0x25)
        };

        return (
            new SolidColorBrush(tint),
            new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            new SolidColorBrush(Color.FromArgb(0xFF, 0xD4, 0xD4, 0xD8)));
    }

    private static string GetFingerName(char ch) => ch switch
    {
        ' ' => "Thumb (Spacebar)",
        'Q' or 'A' or 'Z' => "Left Pinky",
        'W' or 'S' or 'X' => "Left Ring",
        'E' or 'D' or 'C' => "Left Middle",
        'R' or 'T' or 'F' or 'G' or 'V' or 'B' => "Left Index",
        'Y' or 'U' or 'H' or 'J' or 'N' or 'M' => "Right Index",
        'I' or 'K' or ',' => "Right Middle",
        'O' or 'L' or '.' => "Right Ring",
        'P' or ';' or '\'' or '/' => "Right Pinky",
        _ => "Home Row Position"
    };
}
