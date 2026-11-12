using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TypeForge.App.Services;
using TypeForge.App.ViewModels;
using TypeForge.App.Views;
using TypeForge.Core.Models;
using Windows.Graphics;
using Windows.System;
using Windows.UI;

namespace TypeForge.App;

public sealed partial class MainWindow : Window
{
    private readonly INavigationService _navigation;
    private readonly IUpdateService _updateService;
    private readonly ITypeForgeStateService _state;
    private readonly Dictionary<string, (TextBlock Label, Border Dot)> _routeIndicators;

    public MainWindowViewModel ViewModel { get; }

    public MainWindow(
        MainWindowViewModel viewModel,
        INavigationService navigation,
        IUpdateService updateService,
        ITypeForgeStateService state)
    {
        ViewModel = viewModel;
        _navigation = navigation;
        _updateService = updateService;
        _state = state;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
        SetTitleBar(TitleBarDragRegion);
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        ApplyWindowIcon();
        ConfigureInitialWindowBounds();

        _routeIndicators = new Dictionary<string, (TextBlock, Border)>
        {
            ["practice"] = (LblPractice, DotPractice),
            ["course"] = (LblCourse, DotCourse),
            ["test"] = (LblTest, DotTest),
            ["statistics"] = (LblStatistics, DotStatistics)
        };

        _navigation.AttachFrame(ContentFrame);
        _navigation.RegisterRoute("practice", typeof(PracticePage));
        _navigation.RegisterRoute("course", typeof(CoursePage));
        _navigation.RegisterRoute("test", typeof(TestPage));
        _navigation.RegisterRoute("statistics", typeof(StatisticsPage));
        _navigation.RegisterRoute("settings", typeof(SettingsPage));

        _navigation.RouteChanged += _ =>
        {
            UpdateRouteStyles();
            DispatcherQueue.TryEnqueue(FocusActiveTypingPage);
        };
        _navigation.FocusModeChanged += ApplyFocusMode;
        _navigation.ThemeChanged += theme =>
        {
            RootGrid.RequestedTheme = theme;
            UpdateRouteStyles();
        };

        Activated += (_, args) =>
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                DispatcherQueue.TryEnqueue(FocusActiveTypingPage);
            }
        };

        RootGrid.Loaded += (_, _) => AlignTopBarRightInset();
        RootGrid.SizeChanged += (_, _) => AlignTopBarRightInset();
        RootGrid.ActualThemeChanged += (_, _) => UpdateRouteStyles();
        _updateService.StateChanged += (_, _) =>
            DispatcherQueue.TryEnqueue(ApplyUpdateBadge);
        _state.StateChanged += () =>
            DispatcherQueue.TryEnqueue(() =>
            {
                _navigation.ApplyTheme(_state.Settings.Theme);
                UpdateProfileUi();
            });

        ApplyUpdateBadge();
        UpdateProfileUi();

        _navigation.NavigateTo("course");
    }

    private void ApplyWindowIcon()
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "TypeForge.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }
    }

    private void ConfigureInitialWindowBounds()
    {
        DisplayArea? displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        if (displayArea is not null)
        {
            RectInt32 workArea = displayArea.WorkArea;
            int width = Math.Min(1366, Math.Max(800, workArea.Width - 64));
            int height = Math.Min(768, Math.Max(600, workArea.Height - 64));
            int x = workArea.X + Math.Max(0, (workArea.Width - width) / 2);
            int y = workArea.Y + Math.Max(0, (workArea.Height - height) / 2);
            AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
        }
        else
        {
            AppWindow.Resize(new SizeInt32(1280, 720));
        }

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
    }

    private void ApplyUpdateBadge()
    {
        bool available = _updateService.State == UpdateState.UpdateAvailable
            && _updateService.LatestRelease is not null;
        BtnUpdateBadge.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        if (available && _updateService.LatestRelease is { } release)
        {
            LblUpdateBadge.Text = $"Update {release.TagName}";
        }
    }

    private void AlignTopBarRightInset()
    {
        if (RootGrid.XamlRoot is null)
        {
            return;
        }

        double scale = RootGrid.XamlRoot.RasterizationScale;
        if (scale > 0 && AppWindow.TitleBar.RightInset > 0)
        {
            double rightInsetDips = AppWindow.TitleBar.RightInset / scale;
            Thickness rightMargin = new(0, 0, rightInsetDips + 6, 0);
            TopBarRightGroup.Margin = rightMargin;
            BtnFocusExit.Margin = rightMargin;
        }
    }

    private void ApplyFocusMode(bool isFocus)
    {
        if (isFocus)
        {
            TopBarLeftGroup.Visibility = Visibility.Collapsed;
            TopBarRightGroup.Visibility = Visibility.Collapsed;
            BtnFocusExit.Visibility = Visibility.Visible;
        }
        else
        {
            TopBarLeftGroup.Visibility = Visibility.Visible;
            TopBarRightGroup.Visibility = Visibility.Visible;
            BtnFocusExit.Visibility = Visibility.Collapsed;
        }
    }

    private void NavItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string route })
        {
            ViewModel.NavigateCommand.Execute(route);
        }
    }

    private void BtnFocusExit_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ExitFocusModeCommand.Execute(null);
    }

    private void FocusActiveTypingPage()
    {
        if (ContentFrame.Content is PracticePage practicePage)
        {
            practicePage.FocusTypingSurface();
        }
        else if (ContentFrame.Content is TestPage testPage)
        {
            testPage.FocusTypingSurface();
        }
    }

    private bool IsEditableOrDialogControlFocused()
    {
        if (RootGrid.XamlRoot is null)
        {
            return false;
        }

        object? focused = FocusManager.GetFocusedElement(RootGrid.XamlRoot);
        return focused is TextBox or RichEditBox or PasswordBox or AutoSuggestBox or ComboBox or Slider;
    }

    private void RootGrid_CharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        if (args.Handled || IsEditableOrDialogControlFocused())
        {
            return;
        }

        char ch = Convert.ToChar(args.Character);
        if (char.IsControl(ch))
        {
            return;
        }

        if (ContentFrame.Content is PracticePage practicePage && practicePage.HandleCharacterInput(ch))
        {
            args.Handled = true;
            practicePage.FocusTypingSurface();
        }
        else if (ContentFrame.Content is TestPage testPage && testPage.HandleCharacterInput(ch))
        {
            args.Handled = true;
            testPage.FocusTypingSurface();
        }
    }

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled || IsEditableOrDialogControlFocused())
        {
            return;
        }

        if (e.Key is not (VirtualKey.Back or VirtualKey.Escape))
        {
            return;
        }

        if (ContentFrame.Content is PracticePage practicePage && practicePage.HandleKeyDownInput(e.Key))
        {
            e.Handled = true;
        }
        else if (ContentFrame.Content is TestPage testPage && testPage.HandleKeyDownInput(e.Key))
        {
            e.Handled = true;
        }
    }

    private void UpdateRouteStyles()
    {
        bool isLight = RootGrid.ActualTheme == ElementTheme.Light;
        string themeKey = isLight ? "Light" : "Dark";
        var dict = Application.Current.Resources.ThemeDictionaries[themeKey] as ResourceDictionary;
        if (dict is null)
        {
            return;
        }

        var primaryBrush = (Brush)dict["ForgeTextPrimaryBrush"];
        var secondaryBrush = (Brush)dict["ForgeTextSecondaryBrush"];
        var purpleBrush = (Brush)dict["ForgePurpleBrush"];

        foreach (KeyValuePair<string, (TextBlock Label, Border Dot)> kvp in _routeIndicators)
        {
            bool isActive = string.Equals(kvp.Key, _navigation.CurrentRoute, StringComparison.OrdinalIgnoreCase);
            kvp.Value.Label.Foreground = isActive ? primaryBrush : secondaryBrush;
            kvp.Value.Label.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal;
            kvp.Value.Dot.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        }

        IconSettings.Foreground = string.Equals(_navigation.CurrentRoute, "settings", StringComparison.OrdinalIgnoreCase)
            ? purpleBrush
            : secondaryBrush;

        if (isLight)
        {
            AppWindow.TitleBar.ButtonForegroundColor = Color.FromArgb(0xFF, 0x18, 0x18, 0x1B);
            AppWindow.TitleBar.ButtonHoverForegroundColor = Color.FromArgb(0xFF, 0x18, 0x18, 0x1B);
            AppWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x14, 0x00, 0x00, 0x00);
            AppWindow.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x22, 0x00, 0x00, 0x00);
            AppWindow.TitleBar.ButtonInactiveForegroundColor = Color.FromArgb(0xFF, 0x71, 0x71, 0x7A);
        }
        else
        {
            AppWindow.TitleBar.ButtonForegroundColor = Color.FromArgb(0xFF, 0xA1, 0xA1, 0xAA);
            AppWindow.TitleBar.ButtonHoverForegroundColor = Color.FromArgb(0xFF, 0xF4, 0xF4, 0xF5);
            AppWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF);
            AppWindow.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF);
            AppWindow.TitleBar.ButtonInactiveForegroundColor = Color.FromArgb(0xFF, 0x52, 0x52, 0x5B);
        }
    }

    private void UpdateProfileUi()
    {
        UserProfile active = _state.ActiveProfile;
        ProfilePillName.Text = active.Name;
        ProfilePillInitials.Text = active.Initials;
        ProfilePillAvatar.Background = new SolidColorBrush(ProfileDialogs.ParseHexColor(active.AvatarColorHex));

        bool showPicker = _state.ShouldShowProfilePickerOnLaunch;
        ProfilePickerOverlay.Visibility = showPicker ? Visibility.Visible : Visibility.Collapsed;
        if (showPicker)
        {
            RefreshProfilePickerOverlay();
        }
    }

    private static Brush ResolveThemeBrush(ResourceDictionary? dict, string key, Color fallback)
    {
        if (dict is not null && dict.TryGetValue(key, out object? val) && val is Brush b)
        {
            return b;
        }

        if (Application.Current.Resources.TryGetValue(key, out object? appVal) && appVal is Brush appBrush)
        {
            return appBrush;
        }

        return new SolidColorBrush(fallback);
    }

    private void RefreshProfilePickerOverlay()
    {
        ProfilePickerCardsHost.Children.Clear();
        IReadOnlyList<ProfileRankingEntry> rankings = _state.GetProfileRankings();
        bool showRank = rankings.Count >= 2;

        string themeKey = RootGrid.ActualTheme == ElementTheme.Light ? "Light" : "Dark";
        ResourceDictionary? dict = Application.Current.Resources.ThemeDictionaries.TryGetValue(themeKey, out object? td)
            ? td as ResourceDictionary
            : null;

        Brush cardBrush = ResolveThemeBrush(dict, "ForgeCardBrush", Color.FromArgb(0xBF, 0x14, 0x14, 0x18));
        Brush cardInnerBrush = ResolveThemeBrush(dict, "ForgeCardInnerBrush", Color.FromArgb(0xBF, 0x19, 0x19, 0x1E));
        Brush hairlineBrush = ResolveThemeBrush(dict, "ForgeHairlineBrush", Color.FromArgb(0xFF, 0x24, 0x24, 0x29));
        Brush purpleBrush = ResolveThemeBrush(dict, "ForgePurpleBrush", Color.FromArgb(0xFF, 0xA7, 0x8B, 0xFA));
        Brush textPrimaryBrush = ResolveThemeBrush(dict, "ForgeTextPrimaryBrush", Color.FromArgb(0xFF, 0xF4, 0xF4, 0xF5));
        Brush textSecondaryBrush = ResolveThemeBrush(dict, "ForgeTextSecondaryBrush", Color.FromArgb(0xFF, 0xA1, 0xA1, 0xAA));
        Brush textMutedBrush = ResolveThemeBrush(dict, "ForgeTextMutedBrush", Color.FromArgb(0xFF, 0x8E, 0x8E, 0x99));

        foreach (ProfileRankingEntry entry in rankings)
        {
            UserProfile profile = entry.Profile;
            bool isActive = string.Equals(profile.Id, _state.ActiveProfile.Id, StringComparison.OrdinalIgnoreCase);

            var cardContent = new StackPanel
            {
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 172
            };

            var topRow = new Grid();
            if (showRank)
            {
                var rankBadge = new Border
                {
                    Background = entry.Rank == 1
                        ? new SolidColorBrush(Color.FromArgb(36, 167, 139, 250))
                        : cardInnerBrush,
                    BorderBrush = entry.Rank == 1 ? purpleBrush : hairlineBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2, 8, 2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Child = new TextBlock
                    {
                        Text = $"#{entry.Rank}",
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = entry.Rank == 1 ? purpleBrush : textSecondaryBrush
                    }
                };
                topRow.Children.Add(rankBadge);
            }

            if (isActive)
            {
                var lastUsedBadge = new TextBlock
                {
                    Text = "Last used",
                    FontSize = 11,
                    Foreground = textMutedBrush,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                topRow.Children.Add(lastUsedBadge);
            }

            cardContent.Children.Add(topRow);

            var avatar = new Border
            {
                Width = 56,
                Height = 56,
                CornerRadius = new CornerRadius(28),
                Background = new SolidColorBrush(ProfileDialogs.ParseHexColor(profile.AvatarColorHex)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    Text = profile.Initials,
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            cardContent.Children.Add(avatar);

            cardContent.Children.Add(new TextBlock
            {
                Text = profile.Name,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = textPrimaryBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            string statsLine = entry.SessionCount > 0
                ? $"{entry.AverageNetWpm:0} WPM · {entry.CourseCompletionPercentage:0}% course"
                : "New profile";
            cardContent.Children.Add(new TextBlock
            {
                Text = statsLine,
                FontSize = 12,
                Foreground = textSecondaryBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            string capturedId = profile.Id;
            var cardBtn = new Button
            {
                Content = cardContent,
                Padding = new Thickness(16),
                CornerRadius = new CornerRadius(12),
                Background = cardBrush,
                BorderBrush = isActive ? purpleBrush : hairlineBrush,
                BorderThickness = new Thickness(isActive ? 1.5 : 1)
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(cardBtn, $"Continue as {profile.Name}");
            cardBtn.Click += (_, _) => _state.SwitchProfile(capturedId);
            ProfilePickerCardsHost.Children.Add(cardBtn);
        }

        var addCardContent = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 156,
            Height = 148,
            Children =
            {
                new Border
                {
                    Width = 56,
                    Height = 56,
                    CornerRadius = new CornerRadius(28),
                    BorderBrush = hairlineBrush,
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Child = new FontIcon
                    {
                        Glyph = "\uE710",
                        FontSize = 20,
                        Foreground = textSecondaryBrush
                    }
                },
                new TextBlock
                {
                    Text = "New Profile",
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = textSecondaryBrush,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            }
        };

        var addBtn = new Button
        {
            Content = addCardContent,
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = hairlineBrush,
            BorderThickness = new Thickness(1)
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(addBtn, "Create new profile");
        addBtn.Click += async (_, _) =>
        {
            if (RootGrid.XamlRoot is not null)
            {
                await ProfileDialogs.ShowCreateProfileDialogAsync(RootGrid.XamlRoot, _state);
            }
        };
        ProfilePickerCardsHost.Children.Add(addBtn);
    }

    private void BtnProfilePill_Click(object sender, RoutedEventArgs e)
    {
        var flyout = new MenuFlyout();
        IReadOnlyList<ProfileRankingEntry> rankings = _state.GetProfileRankings();
        bool showRank = rankings.Count >= 2;

        foreach (ProfileRankingEntry entry in rankings)
        {
            UserProfile profile = entry.Profile;
            bool isActive = string.Equals(profile.Id, _state.ActiveProfile.Id, StringComparison.OrdinalIgnoreCase);
            string rankPrefix = showRank ? $"#{entry.Rank}  " : string.Empty;
            string activeSuffix = isActive ? " (Active)" : string.Empty;
            string capturedId = profile.Id;

            var item = new MenuFlyoutItem
            {
                Text = $"{rankPrefix}{profile.Name}{activeSuffix}",
                Icon = isActive ? new FontIcon { Glyph = "\uE73E" } : new FontIcon { Glyph = "\uE77B" }
            };
            item.Click += (_, _) => _state.SwitchProfile(capturedId);
            flyout.Items.Add(item);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());

        var newItem = new MenuFlyoutItem
        {
            Text = "New Profile...",
            Icon = new FontIcon { Glyph = "\uE710" }
        };
        newItem.Click += async (_, _) =>
        {
            if (RootGrid.XamlRoot is not null)
            {
                await ProfileDialogs.ShowCreateProfileDialogAsync(RootGrid.XamlRoot, _state);
            }
        };
        flyout.Items.Add(newItem);

        var resetItem = new MenuFlyoutItem
        {
            Text = "Reset Progress (Fresh Start)...",
            Icon = new FontIcon { Glyph = "\uE72C" }
        };
        resetItem.Click += async (_, _) =>
        {
            if (RootGrid.XamlRoot is not null)
            {
                await ProfileDialogs.ShowResetProgressDialogAsync(RootGrid.XamlRoot, _state);
            }
        };
        flyout.Items.Add(resetItem);

        var manageItem = new MenuFlyoutItem
        {
            Text = "Manage Profiles",
            Icon = new FontIcon { Glyph = "\uE713" }
        };
        manageItem.Click += (_, _) => _navigation.NavigateTo("settings");
        flyout.Items.Add(manageItem);

        flyout.ShowAt(BtnProfilePill);
    }
}
