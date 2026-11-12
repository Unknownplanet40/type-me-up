using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        AddHandler(PointerPressedEvent, new PointerEventHandler(Root_PointerPressed), handledEventsToo: true);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.LoadPendingSession();
    }

    public void FocusTypingSurface()
    {
        if (!ViewModel.IsSettingsDialogOpen
            && !ViewModel.IsResultsVisible
            && !ViewModel.IsPlacementWelcomeVisible
            && !ViewModel.IsPlacementResultVisible)
        {
            Focus(FocusState.Programmatic);
        }
    }

    public bool HandleCharacterInput(char ch)
    {
        if (ViewModel.IsSettingsDialogOpen
            || ViewModel.IsResultsVisible
            || ViewModel.IsPlacementWelcomeVisible
            || ViewModel.IsPlacementResultVisible
            || char.IsControl(ch))
        {
            return false;
        }

        return ViewModel.HandleCharacter(ch);
    }

    public bool HandleKeyDownInput(VirtualKey key)
    {
        if (ViewModel.IsSettingsDialogOpen)
        {
            return false;
        }

        switch (key)
        {
            case VirtualKey.Back:
                return !ViewModel.IsResultsVisible && ViewModel.HandleBackspace();
            case VirtualKey.Tab:
                ViewModel.RestartSession();
                return true;
            case VirtualKey.Escape:
                ViewModel.ToggleFocusMode();
                return true;
            default:
                return false;
        }
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
        FocusTypingSurface();
        DispatcherQueue.TryEnqueue(FocusTypingSurface);
    }

    private void PracticePage_Unloaded(object sender, RoutedEventArgs e)
    {
        _hudTimer.Stop();
        PracticeSettingsControl.StopPreview();
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
        StreamView.UpdateAppearance(
            ViewModel.TypingFontSelection,
            ViewModel.TypingFontSize,
            ViewModel.WhitespaceDisplay,
            ViewModel.CursorShape,
            ViewModel.CursorMovement);
        StreamView.RenderSnapshot(snapshot);
        StreamView.UpdatePaceCaret(ViewModel.GhostCaretIndex, ViewModel.IsPaceCaretVisible);
        UpdateCapsLockWarning(snapshot.TargetText);
        RenderKeyPills(dict);

        VirtualKeyboard.Visibility = ViewModel.ShowKeyboard ? Visibility.Visible : Visibility.Collapsed;
        if (ViewModel.HasWrongKeyFeedback && ViewModel.LastWrongPressedKey != '\0')
        {
            VirtualKeyboard.ShowWrongKeyFeedback(
                ViewModel.LastWrongPressedKey,
                ViewModel.LastExpectedKey,
                ViewModel.WrongKeyFingerHint);
        }
        else
        {
            VirtualKeyboard.ClearWrongKeyFeedback();
        }

        VirtualKeyboard.UpdateState(
            ViewModel.ActiveTargetKey,
            ViewModel.KeyMetrics,
            ViewModel.KeyboardColorMode,
            ViewModel.EffectiveKeyboardFadeStage);

        PracticeFingerGuide.UpdateTargetKey(ViewModel.ActiveTargetKey, ViewModel.WrongKeyFingerHint);

        string targetLabel = ViewModel.ActiveTargetKey == ' ' ? "Space" : char.ToUpperInvariant(ViewModel.ActiveTargetKey).ToString();
        string fingerName = TypeForge.Core.Engine.FingerMap.GetDisplayName(TypeForge.Core.Engine.FingerMap.GetFinger(ViewModel.ActiveTargetKey));
        string announcement = !string.IsNullOrWhiteSpace(ViewModel.WrongKeyFingerHint)
            ? $"Target key {targetLabel}, {fingerName}. Hint: {ViewModel.WrongKeyFingerHint}"
            : $"Target key {targetLabel}, {fingerName}";
        TargetKeyLiveAnnouncer.Text = announcement;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(TargetKeyLiveAnnouncer, announcement);
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
            (Border outerGlow, Border InnerPill, TextBlock label) = kvp.Value;
            Border innerPill = InnerPill;
            bool isLocked = checkAdaptiveLocks && !unlocked.Contains(key);
            innerPill.Opacity = 1.0;

            if (key == activeUpper)
            {
                outerGlow.Background = (Brush)dict["ForgePurpleGlowBrush"];
                innerPill.Background = (Brush)dict["ForgePillSelectedBrush"];
                innerPill.BorderBrush = (Brush)dict["ForgePurpleBrush"];
                innerPill.BorderThickness = new Thickness(1.5);
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
                innerPill.BorderBrush = (Brush)dict[isLocked ? "ForgeHairlineBrush" : "ForgeKeyNeutralBorderBrush"];
                label.Foreground = (Brush)dict[isLocked ? "ForgeTextDisabledBrush" : "ForgeTextMutedBrush"];
                label.FontWeight = isLocked ? FontWeights.Normal : FontWeights.Medium;
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
        if (HandleCharacterInput(ch))
        {
            args.Handled = true;
        }
    }

    private void Page_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.CapitalLock)
        {
            DispatcherQueue.TryEnqueue(() => UpdateCapsLockWarning(ViewModel.CurrentSnapshot?.TargetText));
        }

        if (HandleKeyDownInput(e.Key))
        {
            e.Handled = true;
        }
    }

    private void UpdateCapsLockWarning(string? targetText)
    {
        bool isCapsLockOn = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.CapitalLock)
            & Windows.UI.Core.CoreVirtualKeyStates.Locked) != 0;
        CapsLockWarningBanner.Visibility = CapsLockGuard.ShouldShowWarning(isCapsLockOn, targetText)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Root_PointerEntered(object sender, PointerRoutedEventArgs e) => HoverControls.Opacity = 1.0;

    private void Root_PointerExited(object sender, PointerRoutedEventArgs e) => HoverControls.Opacity = 0.6;

    private void Root_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        FocusTypingSurface();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenSettingsDialog();
        PracticeSettingsControl.LoadFromSettings(ViewModel.CurrentSettings);
        PracticeSettingsControl.StartPreview();
    }

    private void BtnDoneSettings_Click(object sender, RoutedEventArgs e)
    {
        PracticeSettingsControl.StopPreview();
        PracticeSettingsControl.ApplyToSettings(ViewModel.CurrentSettings);
        ViewModel.ApplySettingsAndClose();
        IconMute.Glyph = ViewModel.IsMuted ? "\uE74F" : "\uE767";
        Focus(FocusState.Programmatic);
    }

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

    private void BtnStartTodaysSession_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StartTodaysSession();
        Focus(FocusState.Programmatic);
    }

    private void BtnCancelDailyPlan_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelTodaysSession();
        Focus(FocusState.Programmatic);
    }

    private void BtnStartPlacement_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StartPlacementCheck();
        Focus(FocusState.Programmatic);
    }

    private void BtnSkipPlacement_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SkipPlacementCheck();
        Focus(FocusState.Programmatic);
    }

    private void BtnAcceptPlacement_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AcceptPlacementRecommendation();
        Focus(FocusState.Programmatic);
    }

    private void BtnPickCourseMyself_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.PickCourseMyselfAfterPlacement();
    }

    private void BtnDismissFatigue_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DismissFatigueBanner();
        Focus(FocusState.Programmatic);
    }

    private void BtnDrillSlowestPairs_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DrillSlowestPairs();
        Focus(FocusState.Programmatic);
    }

    private void BtnInsightAction_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ExecuteInsightAction();
        Focus(FocusState.Programmatic);
    }

    private void PlacementWelcomeOverlay_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool isNarrow = e.NewSize.Width < 900;
        if (isNarrow)
        {
            PracticeHeroTextCol.Width = new GridLength(1, GridUnitType.Star);
            PracticeHeroArtCol.Width = new GridLength(0);
            Grid.SetColumn(PracticeHeroArtPanel, 0);
            Grid.SetRow(PracticeHeroArtPanel, 0);
            Grid.SetColumn(PracticeHeroTextStack, 0);
            Grid.SetRow(PracticeHeroTextStack, 1);
        }
        else
        {
            PracticeHeroTextCol.Width = new GridLength(320);
            PracticeHeroArtCol.Width = new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(PracticeHeroTextStack, 0);
            Grid.SetRow(PracticeHeroTextStack, 0);
            Grid.SetColumn(PracticeHeroArtPanel, 1);
            Grid.SetRow(PracticeHeroArtPanel, 0);
        }
    }
}
