using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using TypeForge.Core.Engine;

namespace TypeForge.App.Controls;

public sealed partial class FingerGuideControl : UserControl
{
    private readonly Dictionary<FingerId, Rectangle[]> _fingerShapes;
    private char _currentTargetKey = 'F';
    private string? _currentFocusKeys;
    private string? _customHint;

    public FingerGuideControl()
    {
        InitializeComponent();

        _fingerShapes = new Dictionary<FingerId, Rectangle[]>
        {
            [FingerId.LeftPinky] = [Finger_LeftPinky],
            [FingerId.LeftRing] = [Finger_LeftRing],
            [FingerId.LeftMiddle] = [Finger_LeftMiddle],
            [FingerId.LeftIndex] = [Finger_LeftIndex],
            [FingerId.Thumb] = [Finger_LeftThumb, Finger_RightThumb],
            [FingerId.RightIndex] = [Finger_RightIndex],
            [FingerId.RightMiddle] = [Finger_RightMiddle],
            [FingerId.RightRing] = [Finger_RightRing],
            [FingerId.RightPinky] = [Finger_RightPinky]
        };

        Loaded += (_, _) => RefreshVisuals();
        ActualThemeChanged += (_, _) => RefreshVisuals();
    }

    public void UpdateTargetKey(char targetChar, string? customHint = null)
    {
        _currentTargetKey = targetChar == '\0' ? ' ' : targetChar;
        _currentFocusKeys = null;
        _customHint = customHint;
        RefreshVisuals();
    }

    public void UpdateFocusKeys(string? focusKeys)
    {
        _currentFocusKeys = focusKeys;
        _customHint = null;
        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        string themeKey = ActualTheme == ElementTheme.Light ? "Light" : "Dark";
        if (Application.Current.Resources.ThemeDictionaries[themeKey] is not ResourceDictionary dict)
        {
            return;
        }

        var neutralFill = (Brush)dict["ForgeKeyNeutralBrush"];
        var neutralStroke = (Brush)dict["ForgeKeyNeutralBorderBrush"];
        var activeFill = (Brush)dict["ForgePillSelectedBrush"];
        var activeStroke = (Brush)dict["ForgePurpleBrush"];

        var activeFingers = new HashSet<FingerId>();
        string caption;

        if (!string.IsNullOrWhiteSpace(_currentFocusKeys))
        {
            foreach (char ch in _currentFocusKeys.Where(c => !char.IsWhiteSpace(c)))
            {
                activeFingers.Add(FingerMap.GetFinger(ch));
            }

            if (activeFingers.Count == 0)
            {
                activeFingers.Add(FingerId.LeftIndex);
                activeFingers.Add(FingerId.RightIndex);
            }

            string fingerNames = string.Join(", ", activeFingers.OrderBy(f => f).Select(FingerMap.GetDisplayName));
            caption = $"Lesson keys ({_currentFocusKeys.ToUpperInvariant()}): {fingerNames}";
        }
        else
        {
            FingerId targetFinger = FingerMap.GetFinger(_currentTargetKey);
            activeFingers.Add(targetFinger);
            caption = !string.IsNullOrWhiteSpace(_customHint)
                ? _customHint!
                : FingerMap.BuildReachHint(_currentTargetKey);
        }

        foreach (var kvp in _fingerShapes)
        {
            bool isHighlighted = activeFingers.Contains(kvp.Key);
            foreach (Rectangle rect in kvp.Value)
            {
                rect.Fill = isHighlighted ? activeFill : neutralFill;
                rect.Stroke = isHighlighted ? activeStroke : neutralStroke;
                rect.StrokeThickness = isHighlighted ? 2.0 : 1.2;
            }
        }

        HintCaptionText.Text = caption;
        AutomationProperties.SetName(this, $"Ergonomic Finger Guide: {caption}");
    }
}
