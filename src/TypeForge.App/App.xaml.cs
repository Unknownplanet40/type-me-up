using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using TypeForge.App.Services;
using TypeForge.App.ViewModels;
using TypeForge.Core.Services;

namespace TypeForge.App;

/// <summary>
/// Application entry point configuring dependency injection and launching the primary shell window.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public IServiceProvider Services { get; }

    public static IntPtr MainWindowHandle =>
        Current is App { _window: not null } app
            ? WinRT.Interop.WindowNative.GetWindowHandle(app._window)
            : IntPtr.Zero;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnAppUnhandledException;
        Services = ConfigureServices();
    }

    private static void OnAppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TypeForge");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "last-error.log"), $"{DateTimeOffset.UtcNow:O}\n{e.Message}\n{e.Exception}");
        }
        catch
        {
            // Ignore logging failures during termination.
        }
    }

    public static T GetService<T>() where T : class
    {
        if (Current is not App app)
        {
            throw new InvalidOperationException("Application instance is unavailable.");
        }

        return app.Services.GetRequiredService<T>();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ITypeForgeStateService state = Services.GetRequiredService<ITypeForgeStateService>();
        state.Initialize();

        INavigationService navigation = Services.GetRequiredService<INavigationService>();
        _window = Services.GetRequiredService<MainWindow>();
        navigation.ApplyTheme(state.Settings.Theme);
        _window.Activate();

        IUpdateService updateService = Services.GetRequiredService<IUpdateService>();
        updateService.StartBackgroundMonitoring(_window.DispatcherQueue);
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        var profileService = new ProfileService();
        profileService.Initialize();
        services.AddSingleton<IProfileService>(profileService);
        services.AddSingleton<ITypeForgeRepository>(
            _ => new SwitchableRepository(profileService.CreateRepositoryForActiveProfile()));
        services.AddSingleton<CourseProgressionService>();
        services.AddSingleton<CourseCatalogService>();
        services.AddSingleton<ITypeForgeStateService, TypeForgeStateService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IKeystrokeAudioService, KeystrokeAudioService>();
        services.AddSingleton<IUpdateService, UpdateService>();

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<PracticeViewModel>();
        services.AddTransient<CourseViewModel>();
        services.AddTransient<TestViewModel>();
        services.AddTransient<StatisticsViewModel>();
        services.AddTransient<SettingsViewModel>();

        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
