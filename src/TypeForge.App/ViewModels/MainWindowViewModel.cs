using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TypeForge.App.Services;

namespace TypeForge.App.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly ITypeForgeStateService _state;

    [ObservableProperty]
    private string _currentRoute = "practice";

    [ObservableProperty]
    private bool _isFocusMode;

    [ObservableProperty]
    private int _streakDays;

    public MainWindowViewModel(INavigationService navigation, ITypeForgeStateService state)
    {
        _navigation = navigation;
        _state = state;
        _streakDays = _state.StreakDays;

        _navigation.RouteChanged += route => CurrentRoute = route;
        _navigation.FocusModeChanged += isFocus => IsFocusMode = isFocus;
        _state.StateChanged += () => StreakDays = _state.StreakDays;
    }

    [RelayCommand]
    private void Navigate(string route)
    {
        _navigation.NavigateTo(route);
    }

    [RelayCommand]
    private void ExitFocusMode()
    {
        _navigation.SetFocusMode(false);
    }
}
