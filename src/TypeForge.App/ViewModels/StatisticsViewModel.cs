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

    [ObservableProperty]
    private bool _isCertificateOpen;

    [ObservableProperty]
    private string _recipientName = Environment.UserName;

    [ObservableProperty]
    private string _certificateCourseTitle = "Fast Touch Typing Course";

    [ObservableProperty]
    private string _certificatePeakWpmText = "0 WPM";

    [ObservableProperty]
    private string _certificateAccuracyText = "100.0%";

    [ObservableProperty]
    private string _certificateStarsText = "0 / 0 Stars";

    [ObservableProperty]
    private string _certificateDateText = DateTime.Now.ToString("MMMM d, yyyy");

    [ObservableProperty]
    private string _exportStatusText = string.Empty;

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

        CourseTrack? track = _state.SelectedCourseTrack;
        if (track is not null)
        {
            CertificateCourseTitle = $"{track.Title} ({Math.Round(track.ProgressFraction * 100):0}% complete)";
            int earnedStars = track.Units.SelectMany(u => u.Exercises).Where(e => e.Kind != CourseStepKind.Tip).Sum(e => e.Stars);
            int maxStars = track.Units.SelectMany(u => u.Exercises).Count(e => e.Kind != CourseStepKind.Tip) * 3;
            CertificateStarsText = $"{earnedStars} / {maxStars} ★";
        }

        CertificateDateText = DateTime.Now.ToString("MMMM d, yyyy");

        if (sessions.Count == 0)
        {
            AverageWpmText = "0 WPM";
            AverageAccuracyText = "100.0%";
            CertificatePeakWpmText = "0 WPM";
            CertificateAccuracyText = "100.0%";
            SessionCountSummary = "Complete a practice drill to record speed and keystroke data.";
            return;
        }

        double avgWpm = sessions.Average(s => s.NetWpm);
        double avgAcc = sessions.Average(s => s.AccuracyPercentage);
        double peakWpm = sessions.Max(s => s.NetWpm);

        AverageWpmText = $"{Math.Round(avgWpm):0} WPM";
        AverageAccuracyText = $"{avgAcc:0.0}%";
        CertificatePeakWpmText = $"{Math.Round(peakWpm):0} WPM Peak";
        CertificateAccuracyText = $"{avgAcc:0.0}% Avg Accuracy";
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

    [RelayCommand]
    private void OpenCertificate()
    {
        ExportStatusText = string.Empty;
        Refresh();
        IsCertificateOpen = true;
    }

    [RelayCommand]
    private void CloseCertificate()
    {
        IsCertificateOpen = false;
    }
}
