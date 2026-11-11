using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TypeForge.App.Services;
using TypeForge.App.ViewModels;
using TypeForge.App.Views;
using Windows.Graphics;
using Windows.UI;

namespace TypeForge.App;

public sealed partial class MainWindow : Window
{
    private readonly INavigationService _navigation;
    private readonly Dictionary<string, (TextBlock Label, Border Dot)> _routeIndicators;

    public MainWindowViewModel ViewModel { get; }

    public MainWindow(MainWindowViewModel viewModel, INavigationService navigation)
    {
        ViewModel = viewModel;
        _navigation = navigation;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
        SetTitleBar(TitleBarDragRegion);
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Resize(new SizeInt32(1366, 768));

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

        _navigation.RouteChanged += _ => UpdateRouteStyles();
        _navigation.FocusModeChanged += ApplyFocusMode;
        _navigation.ThemeChanged += theme =>
        {
            RootGrid.RequestedTheme = theme;
            UpdateRouteStyles();
        };

        RootGrid.Loaded += (_, _) => AlignTopBarRightInset();
        RootGrid.ActualThemeChanged += (_, _) => UpdateRouteStyles();

        _navigation.NavigateTo("practice");
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
