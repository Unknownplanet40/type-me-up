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
using Windows.Graphics;
using Windows.System;
using Windows.UI;

namespace TypeForge.App;

public sealed partial class MainWindow : Window
{
    private readonly INavigationService _navigation;
    private readonly IUpdateService _updateService;
    private readonly Dictionary<string, (TextBlock Label, Border Dot)> _routeIndicators;

    public MainWindowViewModel ViewModel { get; }

    public MainWindow(
        MainWindowViewModel viewModel,
        INavigationService navigation,
        IUpdateService updateService)
    {
        ViewModel = viewModel;
        _navigation = navigation;
        _updateService = updateService;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
        SetTitleBar(TitleBarDragRegion);
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.SetIcon("Assets/AppIcon.ico");
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

        ApplyUpdateBadge();

        _navigation.NavigateTo("course");
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
            TopBarRightGroup.Margin = new Thickness(0, 0, rightInsetDips + 6, 0);
        }
    }

    private void ApplyFocusMode(bool isFocus)
    {
        if (isFocus)
        {
            TopBarRow.Height = new GridLength(0);
            TopBarContainer.Visibility = Visibility.Collapsed;
            BtnFocusExit.Visibility = Visibility.Visible;
        }
        else
        {
            TopBarRow.Height = new GridLength(32);
            TopBarContainer.Visibility = Visibility.Visible;
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
}
