using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using TypeForge.App.ViewModels;
using TypeForge.Core.Models;
using TypeForge.Core.Services;
using Windows.UI;

namespace TypeForge.App.Views;

public sealed partial class StatisticsPage : Page
{
    public StatisticsViewModel ViewModel { get; }

    public StatisticsPage()
    {
        ViewModel = App.GetService<StatisticsViewModel>();
        InitializeComponent();
        ActualThemeChanged += (_, _) => RenderAllCharts();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Refresh();
        StatsHeatmapKeyboard.UpdateState('\0', ViewModel.KeyMetrics, KeyboardColorMode.PerformanceHeat);
        ApplyActiveTabVisuals();
        RenderAllCharts();
    }

    private void ChartTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
        {
            ViewModel.SelectChartTabCommand.Execute(tag);
            ApplyActiveTabVisuals();
            RenderAllCharts();
        }
    }

    private void ApplyActiveTabVisuals()
    {
        bool speedActive = ViewModel.SelectedChartTabIndex == 0;
        SpeedChartsPanel.Visibility = speedActive ? Visibility.Visible : Visibility.Collapsed;
        AccuracyChartsPanel.Visibility = speedActive ? Visibility.Collapsed : Visibility.Visible;

        BtnTabSpeed.Style = (Style)Application.Current.Resources[speedActive ? "ForgePrimaryButtonStyle" : "ForgeSecondaryButtonStyle"];
        BtnTabAccuracy.Style = (Style)Application.Current.Resources[speedActive ? "ForgeSecondaryButtonStyle" : "ForgePrimaryButtonStyle"];
    }

    private void ChartsCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderAllCharts();
    }

    private void RenderAllCharts()
    {
        string themeKey = ActualTheme == ElementTheme.Light ? "Light" : "Dark";
        var dict = Application.Current.Resources.ThemeDictionaries[themeKey] as ResourceDictionary;
        if (dict is null)
        {
            return;
        }

        IReadOnlyList<SessionResult> sessions = ViewModel.ChronologicalSessions;
        RenderSpeedChart(
            GrossSpeedCanvas,
            sessions,
            s => s.GrossWpm,
            (Brush)dict["ForgePurpleBrush"],
            dict,
            showVeryGoodLine: true);

        RenderSpeedChart(
            NetSpeedCanvas,
            sessions,
            s => s.NetWpm,
            (Brush)dict["ForgeGreenBrush"],
            dict,
            showVeryGoodLine: false);

        RenderAccuracyChart(AccuracyCanvas, sessions, dict);
        RenderDifficultKeysChart(DifficultKeysCanvas, ViewModel.DifficultKeys, dict);
    }

    private static void RenderSpeedChart(
        Canvas canvas,
        IReadOnlyList<SessionResult> sessions,
        Func<SessionResult, double> selector,
        Brush barBrush,
        ResourceDictionary dict,
        bool showVeryGoodLine)
    {
        canvas.Children.Clear();
        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;
        if (width < 40 || height < 40)
        {
            return;
        }

        const double padLeft = 32;
        const double padRight = 10;
        const double padTop = 14;
        const double padBottom = 24;
        double plotW = Math.Max(10, width - padLeft - padRight);
        double plotH = Math.Max(10, height - padTop - padBottom);

        double maxData = sessions.Count > 0 ? sessions.Max(selector) : 40;
        double maxY = Math.Max(60, Math.Ceiling((maxData + 10) / 10.0) * 10.0);

        DrawHorizontalGuide(canvas, padLeft, plotW, padTop, plotH, maxY, 15, "OK", (Brush)dict["ForgeHairlineBrush"], (Brush)dict["ForgeTextMutedBrush"]);
        DrawHorizontalGuide(canvas, padLeft, plotW, padTop, plotH, maxY, 30, "Good", (Brush)dict["ForgeGreenBorderBrush"], (Brush)dict["ForgeGreenBrush"]);
        if (showVeryGoodLine)
        {
            DrawHorizontalGuide(canvas, padLeft, plotW, padTop, plotH, maxY, 50, "Very good", (Brush)dict["ForgePurpleBrush"], (Brush)dict["ForgePurpleBrush"]);
        }

        if (sessions.Count == 0)
        {
            DrawEmptyChartHint(canvas, width, height, "Complete a typing run to chart WPM history", (Brush)dict["ForgeTextMutedBrush"]);
            return;
        }

        double slotW = plotW / sessions.Count;
        double barW = Math.Clamp(slotW * 0.56, 8, 28);

        for (int i = 0; i < sessions.Count; i++)
        {
            double val = Math.Clamp(selector(sessions[i]), 0, maxY);
            double barH = Math.Max(2, (val / maxY) * plotH);
            double x = padLeft + (i * slotW) + ((slotW - barW) * 0.5);
            double y = padTop + plotH - barH;

            var rect = new Rectangle
            {
                Width = barW,
                Height = barH,
                RadiusX = 3,
                RadiusY = 3,
                Fill = barBrush
            };
            ToolTipService.SetToolTip(rect, $"{val:0} WPM · {sessions[i].StartedAt.LocalDateTime:g}");
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            canvas.Children.Add(rect);

            var lbl = new TextBlock
            {
                Text = $"#{i + 1}",
                FontSize = 10,
                Foreground = (Brush)dict["ForgeTextMutedBrush"]
            };
            Canvas.SetLeft(lbl, x + (barW * 0.1));
            Canvas.SetTop(lbl, padTop + plotH + 4);
            canvas.Children.Add(lbl);
        }
    }

    private static void RenderAccuracyChart(Canvas canvas, IReadOnlyList<SessionResult> sessions, ResourceDictionary dict)
    {
        canvas.Children.Clear();
        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;
        if (width < 40 || height < 40)
        {
            return;
        }

        const double padLeft = 34;
        const double padRight = 10;
        const double padTop = 14;
        const double padBottom = 24;
        double plotW = Math.Max(10, width - padLeft - padRight);
        double plotH = Math.Max(10, height - padTop - padBottom);

        // Scale cleanly across 70% - 100% band like TypingMaster Accuracy view
        double minAcc = 70.0;
        double range = 30.0;

        double goalY = padTop + plotH * (1.0 - ((94.0 - minAcc) / range));
        var goalLine = new Line
        {
            X1 = padLeft,
            X2 = padLeft + plotW,
            Y1 = goalY,
            Y2 = goalY,
            Stroke = (Brush)dict["ForgeGreenBrush"],
            StrokeThickness = 1.25
        };
        canvas.Children.Add(goalLine);

        var goalLabel = new TextBlock
        {
            Text = "Accuracy Goal (94%)",
            FontSize = 10,
            Foreground = (Brush)dict["ForgeGreenBrush"]
        };
        Canvas.SetLeft(goalLabel, padLeft + plotW - 110);
        Canvas.SetTop(goalLabel, goalY - 15);
        canvas.Children.Add(goalLabel);

        if (sessions.Count == 0)
        {
            DrawEmptyChartHint(canvas, width, height, "Complete a typing run to chart Accuracy history", (Brush)dict["ForgeTextMutedBrush"]);
            return;
        }

        double slotW = plotW / sessions.Count;
        double barW = Math.Clamp(slotW * 0.56, 8, 28);

        for (int i = 0; i < sessions.Count; i++)
        {
            double acc = Math.Clamp(sessions[i].AccuracyPercentage, minAcc, 100.0);
            double barH = Math.Max(3, ((acc - minAcc) / range) * plotH);
            double x = padLeft + (i * slotW) + ((slotW - barW) * 0.5);
            double y = padTop + plotH - barH;

            Brush fill = sessions[i].AccuracyPercentage >= 94.0
                ? (Brush)dict["ForgeGreenBrush"]
                : sessions[i].AccuracyPercentage >= 88.0
                    ? (Brush)dict["ForgeYellowBrush"]
                    : (Brush)dict["ForgeRedBrush"];

            var rect = new Rectangle
            {
                Width = barW,
                Height = barH,
                RadiusX = 3,
                RadiusY = 3,
                Fill = fill
            };
            ToolTipService.SetToolTip(rect, $"{sessions[i].AccuracyPercentage:0.0}% accuracy");
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            canvas.Children.Add(rect);
        }
    }

    private static void RenderDifficultKeysChart(Canvas canvas, IReadOnlyList<DifficultKeyStat> difficultKeys, ResourceDictionary dict)
    {
        canvas.Children.Clear();
        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;
        if (width < 40 || height < 40)
        {
            return;
        }

        const double padLeft = 20;
        const double padRight = 10;
        const double padTop = 14;
        const double padBottom = 24;
        double plotW = Math.Max(10, width - padLeft - padRight);
        double plotH = Math.Max(10, height - padTop - padBottom);

        double maxScore = Math.Max(60.0, difficultKeys.Count > 0 ? difficultKeys.Max(k => k.DifficultyScore) + 10.0 : 60.0);
        DrawHorizontalGuide(canvas, padLeft, plotW, padTop, plotH, maxScore, 18, "OK", (Brush)dict["ForgeHairlineBrush"], (Brush)dict["ForgeTextMutedBrush"]);
        DrawHorizontalGuide(canvas, padLeft, plotW, padTop, plotH, maxScore, 35, "Difficult", (Brush)dict["ForgeYellowBorderBrush"], (Brush)dict["ForgeYellowBrush"]);
        DrawHorizontalGuide(canvas, padLeft, plotW, padTop, plotH, maxScore, 52, "Problematic", (Brush)dict["ForgeRedBorderBrush"], (Brush)dict["ForgeRedBrush"]);

        if (difficultKeys.Count == 0)
        {
            DrawEmptyChartHint(canvas, width, height, "Type more sessions to identify difficult keys", (Brush)dict["ForgeTextMutedBrush"]);
            return;
        }

        double slotW = plotW / difficultKeys.Count;
        double barW = Math.Clamp(slotW * 0.56, 10, 26);

        for (int i = 0; i < difficultKeys.Count; i++)
        {
            DifficultKeyStat item = difficultKeys[i];
            double barH = Math.Max(4, (Math.Clamp(item.DifficultyScore, 2, maxScore) / maxScore) * plotH);
            double x = padLeft + (i * slotW) + ((slotW - barW) * 0.5);
            double y = padTop + plotH - barH;

            Brush fill = item.Band switch
            {
                KeyDifficultyBand.Problematic => (Brush)dict["ForgeRedBrush"],
                KeyDifficultyBand.Difficult => (Brush)dict["ForgeYellowBrush"],
                _ => (Brush)dict["ForgePurpleBrush"]
            };

            var rect = new Rectangle
            {
                Width = barW,
                Height = barH,
                RadiusX = 3,
                RadiusY = 3,
                Fill = fill
            };
            ToolTipService.SetToolTip(rect, $"'{item.Character}' · {item.ErrorRatePercentage:0.0}% err · {item.AverageLatencyMs:0}ms avg");
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            canvas.Children.Add(rect);

            var keyLabel = new TextBlock
            {
                Text = item.Character.ToString(),
                FontSize = 11,
                Foreground = (Brush)dict["ForgeTextPrimaryBrush"]
            };
            Canvas.SetLeft(keyLabel, x + (barW * 0.25));
            Canvas.SetTop(keyLabel, padTop + plotH + 4);
            canvas.Children.Add(keyLabel);
        }
    }

    private static void DrawHorizontalGuide(
        Canvas canvas,
        double padLeft,
        double plotW,
        double padTop,
        double plotH,
        double maxValue,
        double thresholdValue,
        string label,
        Brush lineBrush,
        Brush textBrush)
    {
        if (thresholdValue > maxValue)
        {
            return;
        }

        double y = padTop + plotH * (1.0 - (thresholdValue / maxValue));
        var line = new Line
        {
            X1 = padLeft,
            X2 = padLeft + plotW,
            Y1 = y,
            Y2 = y,
            Stroke = lineBrush,
            StrokeThickness = 1,
            StrokeDashArray = [4, 3]
        };
        canvas.Children.Add(line);

        var valTxt = new TextBlock
        {
            Text = $"{thresholdValue:0}",
            FontSize = 10,
            Foreground = textBrush
        };
        Canvas.SetLeft(valTxt, 2);
        Canvas.SetTop(valTxt, y - 7);
        canvas.Children.Add(valTxt);

        var bandTxt = new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = textBrush
        };
        Canvas.SetLeft(bandTxt, padLeft + plotW - 62);
        Canvas.SetTop(bandTxt, y - 14);
        canvas.Children.Add(bandTxt);
    }

    private static void DrawEmptyChartHint(Canvas canvas, double width, double height, string message, Brush textBrush)
    {
        var txt = new TextBlock
        {
            Text = message,
            FontSize = 12,
            Foreground = textBrush
        };
        Canvas.SetLeft(txt, Math.Max(16, (width - 240) * 0.5));
        Canvas.SetTop(txt, Math.Max(16, height * 0.45));
        canvas.Children.Add(txt);
    }

    private void DrillWeakKeys_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DrillWeakKeysCommand.Execute(null);
    }
}
