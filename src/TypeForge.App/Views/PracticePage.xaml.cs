using System.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TypeForge.App.ViewModels;
using TypeForge.Core.Engine;
using TypeForge.Core.Models;
using Windows.System;

namespace TypeForge.App.Views;

public sealed partial class PracticePage : Page
{
    private static readonly char[] KeybrLetterOrder =
        "ENIARLTOSUDYCGHPMKBWFZVXQJ".ToCharArray();

    private readonly DispatcherTimer _hudTimer;
    private readonly Dictionary<char, (Border OuterGlow, Border InnerPill, TextBlock Label)> _pillLookup = new();

    public PracticeViewModel ViewModel { get; }

    public PracticePage()
    {
        ViewModel = App.GetService<PracticeViewModel>();
        InitializeComponent();
        BuildKeyPillStrip();

        _hudTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _hudTimer.Tick += (_, _) => ViewModel.TickTimer();

        Loaded += PracticePage_Loaded;
        Unloaded += PracticePage_Unloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.LoadPendingSession();
    }

    private void PracticePage_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.SnapshotUpdated += OnSnapshotUpdated;
        VirtualKeyboard.Visibility = ViewModel.ShowKeyboard ? Visibility.Visible : Visibility.Collapsed;
        IconMute.Glyph = ViewModel.IsMuted ? "\uE74F" : "\uE767";

        if (ViewModel.CurrentSnapshot is not null)
        {
            RenderSnapshot(ViewModel.CurrentSnapshot);
        }

        _hudTimer.Start();
        Focus(FocusState.Programmatic);
    }

    private void PracticePage_Unloaded(object sender, RoutedEventArgs e)
    {
        _hudTimer.Stop();
        ViewModel.SnapshotUpdated -= OnSnapshotUpdated;
    }

    private void OnSnapshotUpdated(TypingSnapshot snapshot)
    {
        RenderSnapshot(snapshot);
    }

    private void BuildKeyPillStrip()
    {
        KeyPillsPanel.Children.Clear();
        _pillLookup.Clear();

        foreach (char letter in KeybrLetterOrder)
        {
            var label = new TextBlock
            {
                Text = letter.ToString(),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var innerPill = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                Child = label
            };

            var outerGlow = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(2),
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Child = innerPill
            };

            _pillLookup[letter] = (outerGlow, innerPill, label);
            KeyPillsPanel.Children.Add(outerGlow);
        }
    }

    private void RenderSnapshot(TypingSnapshot snapshot)
    {
        string themeKey = ActualTheme == ElementTheme.Light ? "Light" : "Dark";
        var dict = Application.Current.Resources.ThemeDictionaries[themeKey] as ResourceDictionary;
        if (dict is null)
        {
            return;
        }

        DailyGoalFill.Width = 96.0 * ViewModel.DailyGoalProgress;
        RenderStream(snapshot, dict);
        RenderKeyPills(dict);
        VirtualKeyboard.UpdateState(ViewModel.ActiveTargetKey, ViewModel.KeyMetrics, ViewModel.KeyboardColorMode);
    }

    private void RenderStream(TypingSnapshot snapshot, ResourceDictionary dict)
    {
        StreamParagraph.Inlines.Clear();

        var greenBrush = (Brush)dict["ForgeGreenBrush"];
        var redBrush = (Brush)dict["ForgeRedBrush"];
        var purpleBrush = (Brush)dict["ForgePurpleBrush"];
        var purpleSoftBrush = (Brush)dict["ForgePurpleSoftBrush"];
        var pendingBrush = (Brush)dict["ForgeTextSecondaryBrush"];
        var mutedDotBrush = (Brush)dict["ForgeTextMutedBrush"];

        string text = snapshot.TargetText;
        IReadOnlyList<TypingCharacterState> states = snapshot.CharacterStates;

        int i = 0;
        while (i < text.Length)
        {
            TypingCharacterState state = states[i];

            if (state == TypingCharacterState.Current)
            {
                string displayChar = text[i] == ' ' ? "·" : text[i].ToString();
                var activeBlock = new TextBlock
                {
                    Text = displayChar,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontSize = ViewModel.TypingFontSize,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = purpleBrush
                };

                var cursorBorder = new Border
                {
                    Background = purpleSoftBrush,
                    BorderBrush = purpleBrush,
                    BorderThickness = new Thickness(0, 0, 0, 2.5),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(3, 0, 3, 1),
                    Margin = new Thickness(0, 0, 1, -5),
                    Child = activeBlock
                };

                StreamParagraph.Inlines.Add(new InlineUIContainer { Child = cursorBorder });
                i++;
                continue;
            }

            var chunk = new StringBuilder();
            while (i < text.Length && states[i] == state)
            {
                chunk.Append(text[i] == ' ' ? '·' : text[i]);
                i++;
            }

            Brush runBrush = state switch
            {
                TypingCharacterState.Correct => greenBrush,
                TypingCharacterState.Incorrect => redBrush,
                _ => pendingBrush
            };

            // Slightly dim middle dots cleanly when pending
            if (state == TypingCharacterState.Pending && chunk.ToString() == "·")
            {
                runBrush = mutedDotBrush;
            }

            StreamParagraph.Inlines.Add(new Run
            {
                Text = chunk.ToString(),
                Foreground = runBrush
            });
        }
    }

    private void RenderKeyPills(ResourceDictionary dict)
    {
        char activeUpper = char.ToUpperInvariant(ViewModel.ActiveTargetKey);
        IReadOnlyDictionary<char, KeyMetric> metrics = ViewModel.KeyMetrics;
        IReadOnlySet<char> unlocked = ViewModel.UnlockedAdaptiveKeys;
        bool checkAdaptiveLocks = ViewModel.IsAdaptiveSession;

        foreach (KeyValuePair<char, (Border OuterGlow, Border InnerPill, TextBlock Label)> kvp in _pillLookup)
        {
            char key = kvp.Key;
            (Border outerGlow, Border innerPill, TextBlock label) = kvp.Value;
            bool isLocked = checkAdaptiveLocks && !unlocked.Contains(key);
            innerPill.Opacity = isLocked ? 0.38 : 1.0;

            if (key == activeUpper)
            {
                outerGlow.Background = (Brush)dict["ForgePurpleGlowBrush"];
                innerPill.Background = (Brush)dict["ForgePillSelectedBrush"];
                innerPill.BorderBrush = (Brush)dict["ForgePurpleBrush"];
                innerPill.BorderThickness = new Thickness(1.5);
                innerPill.Opacity = 1.0;
                label.Foreground = (Brush)dict["ForgePurpleBrush"];
                label.FontWeight = FontWeights.Bold;
                continue;
            }

            outerGlow.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            innerPill.BorderThickness = new Thickness(1);
            label.FontWeight = FontWeights.SemiBold;

            if (TryGetCombinedMetric(metrics, key, out KeyMetric? metric) && metric.TotalKeystrokes > 0)
            {
                if (metric.AccuracyPercentage >= 96.0)
                {
                    innerPill.Background = (Brush)dict["ForgeGreenFillBrush"];
                    innerPill.BorderBrush = (Brush)dict["ForgeGreenBorderBrush"];
                    label.Foreground = (Brush)dict["ForgeGreenBrush"];
                }
                else if (metric.AccuracyPercentage >= 90.0)
                {
                    innerPill.Background = (Brush)dict["ForgeYellowFillBrush"];
                    innerPill.BorderBrush = (Brush)dict["ForgeYellowBorderBrush"];
                    label.Foreground = (Brush)dict["ForgeYellowBrush"];
                }
                else
                {
                    innerPill.Background = (Brush)dict["ForgeRedFillBrush"];
                    innerPill.BorderBrush = (Brush)dict["ForgeRedBorderBrush"];
                    label.Foreground = (Brush)dict["ForgeRedBrush"];
                }
            }
            else
            {
                innerPill.Background = (Brush)dict["ForgeKeyNeutralBrush"];
                innerPill.BorderBrush = (Brush)dict["ForgeKeyNeutralBorderBrush"];
                label.Foreground = (Brush)dict["ForgeTextMutedBrush"];
                label.FontWeight = FontWeights.Normal;
            }
        }
    }

    private static bool TryGetCombinedMetric(
        IReadOnlyDictionary<char, KeyMetric> metrics,
        char upperKey,
        out KeyMetric combined)
    {
        metrics.TryGetValue(upperKey, out KeyMetric? upper);
        char lower = char.ToLowerInvariant(upperKey);
        KeyMetric? lowerMetric = null;
        if (lower != upperKey)
        {
            metrics.TryGetValue(lower, out lowerMetric);
        }

        if (upper is null && lowerMetric is null)
        {
            combined = null!;
            return false;
        }

        if (upper is null)
        {
            combined = lowerMetric!;
            return true;
        }

        if (lowerMetric is null)
        {
            combined = upper;
            return true;
        }

        int total = upper.TotalKeystrokes + lowerMetric.TotalKeystrokes;
        double avgLatency = total == 0
            ? 0.0
            : ((upper.AverageLatencyMs * upper.TotalKeystrokes) + (lowerMetric.AverageLatencyMs * lowerMetric.TotalKeystrokes)) / total;

        combined = new KeyMetric(
            upperKey,
            total,
            upper.CorrectKeystrokes + lowerMetric.CorrectKeystrokes,
            upper.IncorrectKeystrokes + lowerMetric.IncorrectKeystrokes,
            Math.Round(avgLatency, 2));
        return true;
    }

    private void Page_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        char ch = Convert.ToChar(args.Character);
        if (char.IsControl(ch))
        {
            return;
        }

        if (ViewModel.HandleCharacter(ch))
        {
            args.Handled = true;
        }
    }

    private void Page_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Back:
                if (ViewModel.HandleBackspace())
                {
                    e.Handled = true;
                }
                break;

            case VirtualKey.Tab:
                ViewModel.RestartSession();
                e.Handled = true;
                break;

            case VirtualKey.Escape:
                ViewModel.ToggleFocusMode();
                e.Handled = true;
                break;
        }
    }

    private void Root_PointerEntered(object sender, PointerRoutedEventArgs e) => HoverControls.Opacity = 0.9;

    private void Root_PointerExited(object sender, PointerRoutedEventArgs e) => HoverControls.Opacity = 0;

    private void Root_PointerPressed(object sender, PointerRoutedEventArgs e) => Focus(FocusState.Programmatic);

    private void BtnMute_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleMute();
        IconMute.Glyph = ViewModel.IsMuted ? "\uE74F" : "\uE767";
        Focus(FocusState.Programmatic);
    }

    private void BtnRestart_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RestartSession();
        Focus(FocusState.Programmatic);
    }

    private void BtnFocus_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleFocusMode();
        Focus(FocusState.Programmatic);
    }

    private void BtnReviewWeakKeys_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ReviewWeakKeys();
        Focus(FocusState.Programmatic);
    }

    private void BtnNextDrill_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.NextDrill();
        Focus(FocusState.Programmatic);
    }

    private void BtnRepeat_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RestartSession();
        Focus(FocusState.Programmatic);
    }
}
