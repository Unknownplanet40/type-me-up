using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TypeForge.App.Services;
using TypeForge.Core.Models;
using TypeForge.Core.Services;

namespace TypeForge.App.ViewModels;

public sealed partial class TestViewModel : ObservableObject
{
    private readonly ITypeForgeStateService _state;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private int _selectedPassageIndex;

    [ObservableProperty]
    private string _customText = string.Empty;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    public IReadOnlyList<TimedPassage> Passages => _state.TimedPassages;

    public TestViewModel(ITypeForgeStateService state, INavigationService navigation)
    {
        _state = state;
        _navigation = navigation;
    }

    [RelayCommand]
    private void StartTimedTest(string minutesStr)
    {
        int minutes = int.TryParse(minutesStr, out int parsed) && parsed is 1 or 3 or 5 ? parsed : 1;
        TimedPassage passage = Passages.Count > 0
            ? Passages[Math.Clamp(SelectedPassageIndex, 0, Passages.Count - 1)]
            : new TimedPassage(
                "fallback_01",
                "Steady Focus",
                "Consistent daily practice builds durable finger pathways far faster than occasional marathon drills.");

        _state.QueuePracticeSession(
            $"{minutes}-Min Test · {passage.Title}",
            passage.Text,
            SessionType.TimedTest,
            timedLimit: TimeSpan.FromMinutes(minutes));

        _navigation.NavigateTo("practice");
    }

    [RelayCommand]
    private void StartCustomTest()
    {
        string trimmed = CustomText.Trim();
        if (trimmed.Length < 10)
        {
            ValidationMessage = "Enter at least 10 characters of custom text to start.";
            return;
        }

        ValidationMessage = string.Empty;
        _state.QueuePracticeSession("Custom Passage", trimmed, SessionType.CustomText);
        _navigation.NavigateTo("practice");
    }
}
