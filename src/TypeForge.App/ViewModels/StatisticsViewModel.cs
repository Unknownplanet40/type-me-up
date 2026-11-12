using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TypeForge.App.Services;
using TypeForge.Core.Models;
using TypeForge.Core.Services;

namespace TypeForge.App.ViewModels;

public sealed partial class StatisticsViewModel : ObservableObject
{
    private readonly ITypeForgeStateService _state;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private string _averageWpmText = "0 WPM";

    [ObservableProperty]
    private string _averageAccuracyText = "100.0%";

    [ObservableProperty]
    private string _todayGoalText = "0 / 30 min";

    [ObservableProperty]
    private string _streakText = "0 Days";

    [ObservableProperty]
    private string _sessionCountSummary = "Complete a session in Practice to populate history.";

    [ObservableProperty]
    private int _selectedChartTabIndex;

    public IReadOnlyDictionary<char, KeyMetric> KeyMetrics => _state.AggregateKeyMetrics;

    /// <summary>Gets recent sessions ordered chronologically (oldest to newest) for chart rendering.</summary>
    public IReadOnlyList<SessionResult> ChronologicalSessions =>
        _state.Sessions.Take(16).Reverse().ToArray();

    /// <summary>Gets ranked difficult keys sorted hardest to easiest.</summary>
    public IReadOnlyList<DifficultKeyStat> DifficultKeys =>
        StatisticsAggregator.GetDifficultKeys(_state.AggregateKeyMetrics, minSampleCount: 3, maxCount: 14);

    public StatisticsViewModel(ITypeForgeStateService state, INavigationService navigation)
    {
        _state = state;
        _navigation = navigation;
        Refresh();
    }

    public void Refresh()
    {
        IReadOnlyList<SessionResult> sessions = _state.Sessions;
        int goalMins = _state.Settings.DailyGoalMinutes;
        int doneMins = (int)Math.Floor(_state.TodayPracticedDuration.TotalMinutes);
        TodayGoalText = $"{doneMins} / {goalMins} min";
        StreakText = _state.StreakDays == 1 ? "1 Day" : $"{_state.StreakDays} Days";

        if (sessions.Count == 0)
        {
            AverageWpmText = "0 WPM";
            AverageAccuracyText = "100.0%";
            SessionCountSummary = "Complete a practice drill to record speed and keystroke data.";
            return;
        }

        double avgWpm = sessions.Average(s => s.NetWpm);
        double avgAcc = sessions.Average(s => s.AccuracyPercentage);

        AverageWpmText = $"{Math.Round(avgWpm):0} WPM";
        AverageAccuracyText = $"{avgAcc:0.0}%";
        SessionCountSummary = sessions.Count == 1 ? "1 session recorded" : $"{sessions.Count} sessions recorded";
    }

    [RelayCommand]
    private void DrillWeakKeys()
    {
        string drillText = _state.BuildWeakKeyDrillText();
        _state.QueuePracticeSession("Weak-Key Drill", drillText, SessionType.AdaptivePractice);
        _navigation.NavigateTo("practice");
    }

    [RelayCommand]
    private void SelectChartTab(string tabIndexStr)
    {
        SelectedChartTabIndex = int.TryParse(tabIndexStr, out int idx) && idx == 1 ? 1 : 0;
    }
}
