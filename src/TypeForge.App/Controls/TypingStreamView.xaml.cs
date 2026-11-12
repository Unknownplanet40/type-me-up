using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using TypeForge.Core.Engine;
using TypeForge.Core.Models;
using Windows.Foundation;
using Windows.UI.Text;

namespace TypeForge.App.Controls;

/// <summary>
/// Reusable static start-aligned typing stream renderer with configurable whitespace glyphs, font styles, cursor shapes, and smooth/jumping cursor tracking.
/// </summary>
public sealed partial class TypingStreamView : UserControl
{
    public static readonly IReadOnlyList<string> AvailableFonts =
    [
        "Cascadia Mono",
        "Cascadia Mono Bold",
        "Cascadia Mono Italic",
        "Consolas",
        "Consolas Bold",
        "Consolas Italic",
        "Courier New",
        "Courier New Bold",
        "Courier New Italic",
        "Segoe UI",
        "Segoe UI Bold",
        "Segoe UI Italic",
        "Georgia",
        "Georgia Bold",
        "Georgia Italic"
    ];

    private readonly List<Run> _charRuns = [];
    private string _renderedTargetText = string.Empty;
    private string _renderedFontSelection = string.Empty;
    private int _renderedFontSize = 28;
    private WhitespaceDisplayMode _whitespaceMode = WhitespaceDisplayMode.Bullet;
    private CursorShapeMode _cursorShape = CursorShapeMode.Underline;
    private CursorMovementMode _cursorMovement = CursorMovementMode.Smooth;
    private int _activeCursorIndex = -1;
    private int _ghostCursorIndex = -1;
    private int _lastPositionedGhostIndex = -1;
    private bool _isGhostVisible;
    private int _maxVisibleLines;

    private double _targetLeft;
    private double _targetTop;
    private double _targetWidth;
    private double _targetHeight;
    private bool _hasCursorTarget;
    private bool _isRenderingHooked;

    public int MaxVisibleLines
    {
        get => _maxVisibleLines;
        set
        {
            _maxVisibleLines = Math.Max(0, value);
            UpdateViewportMaxHeight();
        }
    }

    public TypingStreamView()
    {
        InitializeComponent();
        Unloaded += (_, _) => UnhookRendering();
    }

    public void UpdateAppearance(
        string fontSelection,
        int fontSize,
        WhitespaceDisplayMode whitespaceMode,
        CursorShapeMode cursorShape,
        CursorMovementMode cursorMovement)
    {
        string normalizedFont = string.IsNullOrWhiteSpace(fontSelection) ? "Cascadia Mono" : fontSelection;
        bool textDirty = !string.Equals(_renderedFontSelection, normalizedFont, StringComparison.Ordinal)
            || _renderedFontSize != fontSize
            || _whitespaceMode != whitespaceMode;

        _renderedFontSelection = normalizedFont;
        _renderedFontSize = Math.Clamp(fontSize, UserSettings.MinFontSize, UserSettings.MaxFontSize);
        _whitespaceMode = whitespaceMode;
        _cursorShape = cursorShape;
        _cursorMovement = cursorMovement;

        (string familyName, FontWeight weight, FontStyle style) = ParseFontSelection(normalizedFont);
        TypingStreamBlock.FontFamily = new FontFamily($"{familyName}, Cascadia Mono, Consolas");
        TypingStreamBlock.FontSize = _renderedFontSize;
        TypingStreamBlock.FontWeight = weight;
        TypingStreamBlock.FontStyle = style;
        TypingStreamBlock.LineHeight = Math.Round(_renderedFontSize * 1.72);
        UpdateViewportMaxHeight();

        if (textDirty && _charRuns.Count == _renderedTargetText.Length && _renderedTargetText.Length > 0)
        {
            for (int i = 0; i < _renderedTargetText.Length; i++)
            {
                _charRuns[i].Text = FormatChar(_renderedTargetText[i], _whitespaceMode);
            }
        }

        ApplyCursorVisualStyle();
        DispatcherQueue.TryEnqueue(PositionCursorIndicator);
    }

    public void RenderSnapshot(TypingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        string themeKey = ActualTheme == ElementTheme.Light ? "Light" : "Dark";
        if (Application.Current.Resources.ThemeDictionaries[themeKey] is not ResourceDictionary dict)
        {
            return;
        }

        var greenBrush = (Brush)dict["ForgeGreenBrush"];
        var redBrush = (Brush)dict["ForgeRedBrush"];
        var purpleBrush = (Brush)dict["ForgePurpleBrush"];
        var pendingBrush = (Brush)dict["ForgeTextSecondaryBrush"];
        var mutedDotBrush = (Brush)dict["ForgeTextMutedBrush"];

        string text = snapshot.TargetText;
        IReadOnlyList<TypingCharacterState> states = snapshot.CharacterStates;

        bool rebuildRuns = !string.Equals(_renderedTargetText, text, StringComparison.Ordinal)
            || _charRuns.Count != text.Length;

        if (rebuildRuns)
        {
            StreamParagraph.Inlines.Clear();
            _charRuns.Clear();
            _renderedTargetText = text;
            _hasCursorTarget = false;
            _lastPositionedGhostIndex = -1;

            for (int i = 0; i < text.Length; i++)
            {
                var run = new Run
                {
                    Text = FormatChar(text[i], _whitespaceMode),
                    Foreground = text[i] == ' ' ? mutedDotBrush : pendingBrush
                };
                _charRuns.Add(run);
                StreamParagraph.Inlines.Add(run);
            }
        }

        for (int i = 0; i < text.Length; i++)
        {
            _charRuns[i].Foreground = states[i] switch
            {
                TypingCharacterState.Correct => greenBrush,
                TypingCharacterState.Incorrect => redBrush,
                TypingCharacterState.Current => _cursorShape == CursorShapeMode.Block ? (Brush)dict["ForgeTextPrimaryBrush"] : purpleBrush,
                _ => text[i] == ' ' ? mutedDotBrush : pendingBrush
            };
        }

        _activeCursorIndex = snapshot.CursorIndex;
        ApplyCursorVisualStyle();
        if (rebuildRuns)
        {
            DispatcherQueue.TryEnqueue(PositionCursorIndicator);
        }
        else
        {
            PositionCursorIndicator();
        }
    }

    private void TypingStreamBlock_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _lastPositionedGhostIndex = -1;
        PositionCursorIndicator();
        PositionGhostIndicator();
    }

    public void UpdatePaceCaret(int ghostIndex, bool visible)
    {
        if (!visible || ghostIndex < 0 || _charRuns.Count == 0)
        {
            _isGhostVisible = false;
            _ghostCursorIndex = -1;
            _lastPositionedGhostIndex = -1;
            PaceGhostIndicator.Visibility = Visibility.Collapsed;
            return;
        }

        int clamped = Math.Clamp(ghostIndex, 0, _charRuns.Count - 1);
        bool changed = !_isGhostVisible || _ghostCursorIndex != clamped;
        _isGhostVisible = true;
        _ghostCursorIndex = clamped;

        if (changed)
        {
            PositionGhostIndicator();
        }
    }

    private void PositionGhostIndicator()
    {
        if (!_isGhostVisible || _ghostCursorIndex < 0 || _ghostCursorIndex >= _charRuns.Count || TypingStreamBlock.ActualWidth <= 0)
        {
            PaceGhostIndicator.Visibility = Visibility.Collapsed;
            return;
        }

        if (_lastPositionedGhostIndex == _ghostCursorIndex && PaceGhostIndicator.Visibility == Visibility.Visible)
        {
            return;
        }

        try
        {
            Run ghostRun = _charRuns[_ghostCursorIndex];
            Rect startRect = ghostRun.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            double charHeight = Math.Max(startRect.Height, _renderedFontSize * 1.25);

            Canvas.SetLeft(PaceGhostIndicator, startRect.X - 1);
            Canvas.SetTop(PaceGhostIndicator, startRect.Y + 1);
            PaceGhostIndicator.Height = charHeight;
            PaceGhostIndicator.Visibility = Visibility.Visible;
            _lastPositionedGhostIndex = _ghostCursorIndex;
        }
        catch
        {
            PaceGhostIndicator.Visibility = Visibility.Collapsed;
        }
    }

    private void PositionCursorIndicator()
    {
        if (_activeCursorIndex < 0 || _activeCursorIndex >= _charRuns.Count || TypingStreamBlock.ActualWidth <= 0)
        {
            CursorIndicator.Visibility = Visibility.Collapsed;
            UnhookRendering();
            return;
        }

        try
        {
            Run activeRun = _charRuns[_activeCursorIndex];
            Rect startRect = activeRun.ContentStart.GetCharacterRect(LogicalDirection.Forward);
            Rect endRect = activeRun.ContentEnd.GetCharacterRect(LogicalDirection.Forward);

            double left = startRect.X;
            double top = startRect.Y;
            double charWidth = endRect.X - startRect.X;
            if (Math.Abs(endRect.Y - startRect.Y) > 2 || charWidth <= 0)
            {
                charWidth = Math.Max(startRect.Width, _renderedFontSize * 0.62);
            }

            double charHeight = Math.Max(startRect.Height, _renderedFontSize * 1.25);

            double desiredLeft;
            double desiredWidth;
            switch (_cursorShape)
            {
                case CursorShapeMode.Line:
                    desiredLeft = left - 1;
                    desiredWidth = 2.5;
                    break;
                case CursorShapeMode.Underline:
                    desiredLeft = left - 1;
                    desiredWidth = Math.Max(8, charWidth + 2);
                    break;
                default:
                    desiredLeft = left - 2;
                    desiredWidth = Math.Max(8, charWidth + 4);
                    break;
            }

            double desiredTop = top + 1;
            double desiredHeight = charHeight;

            bool shouldSnap = _cursorMovement == CursorMovementMode.Jumping
                || !_hasCursorTarget
                || CursorIndicator.Visibility != Visibility.Visible
                || Math.Abs(desiredTop - _targetTop) > 4.0;

            _targetLeft = desiredLeft;
            _targetTop = desiredTop;
            _targetWidth = desiredWidth;
            _targetHeight = desiredHeight;
            _hasCursorTarget = true;

            CursorIndicator.Visibility = Visibility.Visible;

            if (shouldSnap)
            {
                Canvas.SetLeft(CursorIndicator, _targetLeft);
                Canvas.SetTop(CursorIndicator, _targetTop);
                CursorIndicator.Width = _targetWidth;
                CursorIndicator.Height = _targetHeight;
            }
            else
            {
                HookRendering();
            }

            if (_maxVisibleLines > 0 && StreamScrollViewer.ScrollableHeight > 0)
            {
                double lineH = TypingStreamBlock.LineHeight > 0 ? TypingStreamBlock.LineHeight : _renderedFontSize * 1.72;
                double offset = Math.Clamp(desiredTop - (lineH * 1.4), 0, StreamScrollViewer.ScrollableHeight);
                StreamScrollViewer.ChangeView(null, offset, null, disableAnimation: false);
            }
        }
        catch
        {
            CursorIndicator.Visibility = Visibility.Collapsed;
            UnhookRendering();
        }
    }

    private void ApplyCursorVisualStyle()
    {
        string themeKey = ActualTheme == ElementTheme.Light ? "Light" : "Dark";
        if (Application.Current.Resources.ThemeDictionaries[themeKey] is not ResourceDictionary dict)
        {
            return;
        }

        var purple = (Brush)dict["ForgePurpleBrush"];
        var purpleSoft = (Brush)dict["ForgePurpleSoftBrush"];
        var transparent = new SolidColorBrush(Colors.Transparent);

        switch (_cursorShape)
        {
            case CursorShapeMode.Block:
                CursorIndicator.Background = purpleSoft;
                CursorIndicator.BorderBrush = purple;
                CursorIndicator.BorderThickness = new Thickness(0);
                CursorIndicator.CornerRadius = new CornerRadius(4);
                break;

            case CursorShapeMode.Box:
                CursorIndicator.Background = transparent;
                CursorIndicator.BorderBrush = purple;
                CursorIndicator.BorderThickness = new Thickness(1.5);
                CursorIndicator.CornerRadius = new CornerRadius(4);
                break;

            case CursorShapeMode.Line:
                CursorIndicator.Background = purple;
                CursorIndicator.BorderBrush = transparent;
                CursorIndicator.BorderThickness = new Thickness(0);
                CursorIndicator.CornerRadius = new CornerRadius(1);
                break;

            default:
                CursorIndicator.Background = transparent;
                CursorIndicator.BorderBrush = purple;
                CursorIndicator.BorderThickness = new Thickness(0, 0, 0, 2.5);
                CursorIndicator.CornerRadius = new CornerRadius(0);
                break;
        }
    }

    private void OnCompositionRendering(object? sender, object e)
    {
        if (!_hasCursorTarget || CursorIndicator.Visibility != Visibility.Visible)
        {
            UnhookRendering();
            return;
        }

        double currentLeft = Canvas.GetLeft(CursorIndicator);
        double currentTop = Canvas.GetTop(CursorIndicator);
        double currentWidth = double.IsNaN(CursorIndicator.Width) ? _targetWidth : CursorIndicator.Width;
        double currentHeight = double.IsNaN(CursorIndicator.Height) ? _targetHeight : CursorIndicator.Height;

        const double alpha = 0.34;
        double nextLeft = currentLeft + ((_targetLeft - currentLeft) * alpha);
        double nextTop = currentTop + ((_targetTop - currentTop) * alpha);
        double nextWidth = currentWidth + ((_targetWidth - currentWidth) * alpha);
        double nextHeight = currentHeight + ((_targetHeight - currentHeight) * alpha);

        bool settled = Math.Abs(_targetLeft - nextLeft) < 0.35
            && Math.Abs(_targetTop - nextTop) < 0.35
            && Math.Abs(_targetWidth - nextWidth) < 0.35;

        if (settled)
        {
            Canvas.SetLeft(CursorIndicator, _targetLeft);
            Canvas.SetTop(CursorIndicator, _targetTop);
            CursorIndicator.Width = _targetWidth;
            CursorIndicator.Height = _targetHeight;
            UnhookRendering();
            return;
        }

        Canvas.SetLeft(CursorIndicator, nextLeft);
        Canvas.SetTop(CursorIndicator, nextTop);
        CursorIndicator.Width = nextWidth;
        CursorIndicator.Height = nextHeight;
    }

    private void HookRendering()
    {
        if (!_isRenderingHooked)
        {
            CompositionTarget.Rendering += OnCompositionRendering;
            _isRenderingHooked = true;
        }
    }

    private void UnhookRendering()
    {
        if (_isRenderingHooked)
        {
            CompositionTarget.Rendering -= OnCompositionRendering;
            _isRenderingHooked = false;
        }
    }

    private void UpdateViewportMaxHeight()
    {
        if (_maxVisibleLines <= 0)
        {
            StreamScrollViewer.MaxHeight = double.PositiveInfinity;
            return;
        }

        double lineH = TypingStreamBlock.LineHeight > 0 ? TypingStreamBlock.LineHeight : _renderedFontSize * 1.72;
        StreamScrollViewer.MaxHeight = (lineH * _maxVisibleLines) + 12;
    }

    private static string FormatChar(char ch, WhitespaceDisplayMode mode)
    {
        if (ch != ' ')
        {
            return ch.ToString();
        }

        return mode switch
        {
            WhitespaceDisplayMode.None => " ",
            WhitespaceDisplayMode.Bar => "␣\u200B",
            _ => "·\u200B"
        };
    }

    private static (string FamilyName, FontWeight Weight, FontStyle Style) ParseFontSelection(string selection)
    {
        FontWeight weight = FontWeights.Normal;
        FontStyle style = FontStyle.Normal;
        string family = selection.Trim();

        if (family.EndsWith(" Bold Italic", StringComparison.OrdinalIgnoreCase))
        {
            weight = FontWeights.Bold;
            style = FontStyle.Italic;
            family = family[..^" Bold Italic".Length].Trim();
        }
        else if (family.EndsWith(" Bold", StringComparison.OrdinalIgnoreCase))
        {
            weight = FontWeights.Bold;
            family = family[..^" Bold".Length].Trim();
        }
        else if (family.EndsWith(" Italic", StringComparison.OrdinalIgnoreCase))
        {
            style = FontStyle.Italic;
            family = family[..^" Italic".Length].Trim();
        }

        return (family, weight, style);
    }
}
