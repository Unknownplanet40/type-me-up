using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TypeForge.Core.Models;

namespace TypeForge.App.Services;

/// <summary>
/// Coordinates frame navigation, theme updates, and focus mode transitions across the application shell.
/// </summary>
public interface INavigationService
{
    string CurrentRoute { get; }
    bool IsFocusMode { get; }

    event Action<string>? RouteChanged;
    event Action<bool>? FocusModeChanged;
    event Action<ElementTheme>? ThemeChanged;

    void AttachFrame(Frame frame);
    void RegisterRoute(string routeKey, Type pageType);
    void NavigateTo(string routeKey);
    void SetFocusMode(bool isFocus);
    void ApplyTheme(ThemePreference preference);
}

public sealed class NavigationService : INavigationService
{
    private readonly Dictionary<string, Type> _routes = new(StringComparer.OrdinalIgnoreCase);
    private Frame? _frame;

    public string CurrentRoute { get; private set; } = "practice";
    public bool IsFocusMode { get; private set; }

    public event Action<string>? RouteChanged;
    public event Action<bool>? FocusModeChanged;
    public event Action<ElementTheme>? ThemeChanged;

    public void AttachFrame(Frame frame)
    {
        _frame = frame;
    }

    public void RegisterRoute(string routeKey, Type pageType)
    {
        _routes[routeKey] = pageType;
    }

    public void NavigateTo(string routeKey)
    {
        if (!_routes.TryGetValue(routeKey, out Type? pageType))
        {
            return;
        }

        CurrentRoute = routeKey;
        _frame?.Navigate(pageType);
        RouteChanged?.Invoke(routeKey);
    }

    public void SetFocusMode(bool isFocus)
    {
        if (IsFocusMode == isFocus)
        {
            return;
        }

        IsFocusMode = isFocus;
        FocusModeChanged?.Invoke(isFocus);
    }

    public void ApplyTheme(ThemePreference preference)
    {
        ElementTheme elementTheme = preference switch
        {
            ThemePreference.Light => ElementTheme.Light,
            ThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        ThemeChanged?.Invoke(elementTheme);
    }
}
