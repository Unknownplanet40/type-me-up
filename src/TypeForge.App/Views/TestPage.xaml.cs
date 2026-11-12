using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TypeForge.App.ViewModels;
using TypeForge.Core.Engine;
using TypeForge.Core.Models;
using Windows.System;

namespace TypeForge.App.Views;

public sealed partial class TestPage : Page
{
    private readonly DispatcherTimer _hudTimer;
    private bool _suppressDialogEvents;

    public TestViewModel ViewModel { get; }

    public TestPage()
    {
        ViewModel = App.GetService<TestViewModel>();
        InitializeComponent();
        PopulateStaticComboBoxes();

        _hudTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _hudTimer.Tick += (_, _) => ViewModel.TickTimer();

        Loaded += TestPage_Loaded;
        Unloaded += TestPage_Unloaded;
        AddHandler(PointerPressedEvent, new PointerEventHandler(Root_PointerPressed), handledEventsToo: true);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.RefreshFromSettings();
    }

    public void FocusTypingSurface()
    {
        if (!ViewModel.IsSettingsDialogOpen && !ViewModel.IsResultsVisible)
        {
            Focus(FocusState.Programmatic);
        }
    }

    public bool HandleCharacterInput(char ch)
    {
        if (ViewModel.IsSettingsDialogOpen || ViewModel.IsResultsVisible || char.IsControl(ch))
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
                ViewModel.StartNewTest();
                return true;
            default:
                return false;
        }
    }

    private void TestPage_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.SnapshotUpdated += OnSnapshotUpdated;
        ViewModel.AppearanceUpdated += ApplyStreamAppearance;
        ViewModel.ParagraphPreviewsUpdated += RenderParagraphPreviews;

        ApplyStreamAppearance();
        UpdateModeBarVisuals();
        RenderParagraphPreviews();

        if (ViewModel.CurrentSnapshot is not null)
        {
            TestStreamView.RenderSnapshot(ViewModel.CurrentSnapshot);
        }

        _hudTimer.Start();
        FocusTypingSurface();
        DispatcherQueue.TryEnqueue(FocusTypingSurface);
    }

    private void TestPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _hudTimer.Stop();
        TypingSettingsControl.StopPreview();
        ViewModel.SnapshotUpdated -= OnSnapshotUpdated;
        ViewModel.AppearanceUpdated -= ApplyStreamAppearance;
        ViewModel.ParagraphPreviewsUpdated -= RenderParagraphPreviews;
    }

    private void PopulateStaticComboBoxes()
    {
        ComboLanguage.Items.Clear();
        foreach (string lang in ViewModel.AvailableLanguages)
        {
            ComboLanguage.Items.Add(new ComboBoxItem { Content = lang, Tag = lang });
        }

        if (ComboLanguage.Items.Count > 0)
        {
            ComboLanguage.SelectedIndex = 0;
        }

        ComboBook.Items.Clear();
        foreach (string title in ViewModel.BookTitles)
        {
            ComboBook.Items.Add(new ComboBoxItem { Content = title });
        }
    }

    private void RenderParagraphPreviews()
    {
        if (ParagraphPreviewsPanel is null)
        {
            return;
        }

        string themeKey = ActualTheme == ElementTheme.Light ? "Light" : "Dark";
        ResourceDictionary? dict = Application.Current.Resources.ThemeDictionaries[themeKey] as ResourceDictionary;
        Brush? cardInner = dict?["ForgeCardInnerBrush"] as Brush;
        Brush? purpleSoft = dict?["ForgePurpleSoftBrush"] as Brush;
        Brush? hairline = dict?["ForgeHairlineBrush"] as Brush;
        Brush? purple = dict?["ForgePurpleBrush"] as Brush;
        Brush? primary = dict?["ForgeTextPrimaryBrush"] as Brush;
        Brush? secondary = dict?["ForgeTextSecondaryBrush"] as Brush;

        ParagraphPreviewsPanel.Children.Clear();
        foreach (BookParagraphPreviewItem item in ViewModel.ParagraphPreviews)
        {
            var grid = new Grid { ColumnSpacing = 10 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var prefixBlock = new TextBlock
            {
                Text = item.Prefix,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Top
            };
            if (purple is not null && secondary is not null)
            {
                prefixBlock.Foreground = item.IsCurrent ? purple : secondary;
            }

            var previewBlock = new TextBlock
            {
                Text = item.PreviewText,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 2,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            if (primary is not null && secondary is not null)
            {
                previewBlock.Foreground = item.IsCurrent ? primary : secondary;
            }

            Grid.SetColumn(prefixBlock, 0);
            Grid.SetColumn(previewBlock, 1);
            grid.Children.Add(prefixBlock);
            grid.Children.Add(previewBlock);

            var btn = new Button
            {
                Tag = item.ParagraphNumber,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 7, 10, 7),
                CornerRadius = new CornerRadius(6),
                Content = grid
            };
            if (item.IsCurrent && purpleSoft is not null && purple is not null)
            {
                btn.Background = purpleSoft;
                btn.BorderBrush = purple;
            }
            else if (cardInner is not null && hairline is not null)
            {
                btn.Background = cardInner;
                btn.BorderBrush = hairline;
            }

            btn.Click += BtnSelectPreviewParagraph_Click;
            ParagraphPreviewsPanel.Children.Add(btn);
        }
    }

    private void OnSnapshotUpdated(TypingSnapshot snapshot)
    {
        TestStreamView.RenderSnapshot(snapshot);
    }

    private void ApplyStreamAppearance()
    {
        TestStreamView.UpdateAppearance(
            ViewModel.ActiveFontSelection,
            ViewModel.ActiveFontSize,
            ViewModel.ActiveWhitespaceDisplay,
            ViewModel.ActiveCursorShape,
            ViewModel.ActiveCursorMovement);
    }

    private void UpdateModeBarVisuals()
    {
        string themeKey = ActualTheme == ElementTheme.Light ? "Light" : "Dark";
        ResourceDictionary? dict = Application.Current.Resources.ThemeDictionaries[themeKey] as ResourceDictionary;
        Brush? primaryBrush = dict?["ForgeTextPrimaryBrush"] as Brush;
        Brush? secondaryBrush = dict?["ForgeTextSecondaryBrush"] as Brush;

        (TestDurationMode Mode, TextBlock Label, Border Bar)[] items =
        [
            (TestDurationMode.Seconds15, LblSeconds15, BarSeconds15),
            (TestDurationMode.Seconds30, LblSeconds30, BarSeconds30),
            (TestDurationMode.Minute1, LblMinute1, BarMinute1),
            (TestDurationMode.Characters100, LblCharacters100, BarCharacters100),
            (TestDurationMode.Characters500, LblCharacters500, BarCharacters500),
            (TestDurationMode.Characters1000, LblCharacters1000, BarCharacters1000)
        ];

        foreach ((TestDurationMode mode, TextBlock lbl, Border bar) in items)
        {
            bool active = ViewModel.SelectedDurationMode == mode;
            bar.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            lbl.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            if (primaryBrush is not null && secondaryBrush is not null)
            {
                lbl.Foreground = active ? primaryBrush : secondaryBrush;
            }
        }
    }

    private void SyncSettingsDialogControls()
    {
        _suppressDialogEvents = true;
        try
        {
            RadioCommonWords.IsChecked = ViewModel.DraftTextSource == TestTextSource.CommonWords;
            RadioPseudoWords.IsChecked = ViewModel.DraftTextSource == TestTextSource.PseudoWords;
            RadioBookParagraphs.IsChecked = ViewModel.DraftTextSource == TestTextSource.BookParagraphs;
            RadioCustomText.IsChecked = ViewModel.DraftTextSource == TestTextSource.CustomText;
            UpdateTextSourceSubPanels();

            TypingSettingsControl.LoadFromSettings(ViewModel.CurrentSettings);

            SwitchSettingsTab(0);
            RenderParagraphPreviews();
        }
        finally
        {
            _suppressDialogEvents = false;
        }
    }

    private void ComboLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressDialogEvents)
        {
            return;
        }

        if (ComboLanguage.SelectedItem is ComboBoxItem { Tag: string lang })
        {
            ViewModel.DraftLanguage = lang;
        }
    }

    private void SwitchSettingsTab(int index)
    {
        ViewModel.ActiveSettingsTabIndex = index;
        bool isTextTab = index == 0;

        TextSettingsPanel.Visibility = isTextTab ? Visibility.Visible : Visibility.Collapsed;
        TypingSettingsControl.Visibility = isTextTab ? Visibility.Collapsed : Visibility.Visible;

        TabBarText.Visibility = isTextTab ? Visibility.Visible : Visibility.Collapsed;
        TabBarTyping.Visibility = isTextTab ? Visibility.Collapsed : Visibility.Visible;

        DialogHeaderSubtitle.Text = isTextTab
            ? "Text Settings: Choose what text to type in the test"
            : "Typing Settings: Customize behavior, appearance, and sound";

        string themeKey = ActualTheme == ElementTheme.Light ? "Light" : "Dark";
        if (Application.Current.Resources.ThemeDictionaries[themeKey] is ResourceDictionary dict)
        {
            var primary = (Brush)dict["ForgeTextPrimaryBrush"];
            var secondary = (Brush)dict["ForgeTextSecondaryBrush"];
            TabLblText.Foreground = isTextTab ? primary : secondary;
            TabLblTyping.Foreground = isTextTab ? secondary : primary;
        }

        if (isTextTab)
        {
            TypingSettingsControl.StopPreview();
        }
        else
        {
            TypingSettingsControl.StartPreview();
        }
    }

    private void UpdateTextSourceSubPanels()
    {
        CommonWordsPanel.Visibility = ViewModel.DraftTextSource == TestTextSource.CommonWords ? Visibility.Visible : Visibility.Collapsed;
        PseudoWordsPanel.Visibility = ViewModel.DraftTextSource == TestTextSource.PseudoWords ? Visibility.Visible : Visibility.Collapsed;
        BookParagraphsPanel.Visibility = ViewModel.DraftTextSource == TestTextSource.BookParagraphs ? Visibility.Visible : Visibility.Collapsed;
        CustomTextPanel.Visibility = ViewModel.DraftTextSource == TestTextSource.CustomText ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && Enum.TryParse(tag, out TestDurationMode mode))
        {
            ViewModel.SelectDurationMode(mode);
            UpdateModeBarVisuals();
            Focus(FocusState.Programmatic);
        }
    }

    private void BtnRestartTest_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StartNewTest();
        Focus(FocusState.Programmatic);
    }

    private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenSettingsDialog();
        SyncSettingsDialogControls();
    }

    private void BtnDoneSettings_Click(object sender, RoutedEventArgs e)
    {
        TypingSettingsControl.StopPreview();
        TypingSettingsControl.ApplyToSettings(ViewModel.CurrentSettings);
        ViewModel.ApplySettingsAndClose();
        UpdateModeBarVisuals();
        Focus(FocusState.Programmatic);
    }

    private void BtnSettingsTabText_Click(object sender, RoutedEventArgs e) => SwitchSettingsTab(0);

    private void BtnSettingsTabTyping_Click(object sender, RoutedEventArgs e) => SwitchSettingsTab(1);

    private void TextSourceRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressDialogEvents)
        {
            return;
        }

        if (sender is RadioButton { Tag: string tag } && Enum.TryParse(tag, out TestTextSource source))
        {
            ViewModel.DraftTextSource = source;
            UpdateTextSourceSubPanels();
        }
    }

    private void BtnPrevParagraph_Click(object sender, RoutedEventArgs e) => ViewModel.StepBookParagraph(-1);

    private void BtnNextParagraph_Click(object sender, RoutedEventArgs e) => ViewModel.StepBookParagraph(1);

    private void BtnSelectPreviewParagraph_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int paragraphNumber })
        {
            ViewModel.DraftParagraphNumber = paragraphNumber;
        }
    }

    private void Root_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        FocusTypingSurface();
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
        if (HandleKeyDownInput(e.Key))
        {
            e.Handled = true;
        }
    }
}
