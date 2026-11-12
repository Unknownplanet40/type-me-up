using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using TypeForge.App.Services;
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
        RenderProfileLeaderboard();
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
        int tab = ViewModel.SelectedChartTabIndex;
        SpeedChartsPanel.Visibility = tab == 0 ? Visibility.Visible : Visibility.Collapsed;
        AccuracyChartsPanel.Visibility = tab == 1 ? Visibility.Visible : Visibility.Collapsed;
        WeeklyChartsPanel.Visibility = tab == 2 ? Visibility.Visible : Visibility.Collapsed;

        BtnTabSpeed.Style = (Style)Application.Current.Resources[tab == 0 ? "ForgePrimaryButtonStyle" : "ForgeSecondaryButtonStyle"];
        BtnTabAccuracy.Style = (Style)Application.Current.Resources[tab == 1 ? "ForgePrimaryButtonStyle" : "ForgeSecondaryButtonStyle"];
        BtnTabWeekly.Style = (Style)Application.Current.Resources[tab == 2 ? "ForgePrimaryButtonStyle" : "ForgeSecondaryButtonStyle"];
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

        IReadOnlyList<DailyTrendPoint> weekly = ViewModel.WeeklyTrend;
        RenderWeeklyTrendChart(
            WeeklyWpmCanvas,
            weekly,
            p => p.AverageNetWpm,
            maxYFallback: 60.0,
            unitSuffix: "WPM",
            lineBrush: (Brush)dict["ForgePurpleBrush"],
            dict);

        RenderWeeklyTrendChart(
            WeeklyAccuracyCanvas,
            weekly,
            p => p.AverageAccuracyPercentage,
            maxYFallback: 100.0,
            unitSuffix: "%",
            lineBrush: (Brush)dict["ForgeGreenBrush"],
            dict);

        RenderWeakBigramsCanvas(WeakBigramsCanvas, ViewModel.WeakBigrams, dict);
        RenderDueReviewKeysCanvas(DueReviewKeysCanvas, ViewModel.DueReviewKeys, ViewModel.UpcomingReviewSchedules, dict);
        RenderTestHistoryList(TestHistoryListHost, ViewModel.TimedTestSessions, dict);
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

    private static void RenderWeeklyTrendChart(
        Canvas canvas,
        IReadOnlyList<DailyTrendPoint> points,
        Func<DailyTrendPoint, double> selector,
        double maxYFallback,
        string unitSuffix,
        Brush lineBrush,
        ResourceDictionary dict)
    {
        canvas.Children.Clear();
        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;
        if (width < 40 || height < 40)
        {
            return;
        }

        const double padLeft = 34;
        const double padRight = 12;
        const double padTop = 16;
        const double padBottom = 26;
        double plotW = Math.Max(10, width - padLeft - padRight);
        double plotH = Math.Max(10, height - padTop - padBottom);

        bool anyActiveDay = points.Any(p => p.SessionCount > 0);
        if (!anyActiveDay)
        {
            DrawEmptyChartHint(canvas, width, height, "Complete sessions this week to plot 7-day trend", (Brush)dict["ForgeTextMutedBrush"]);
            return;
        }

        double maxData = points.Max(selector);
        double maxY = Math.Max(maxYFallback, Math.Ceiling((maxData + 5.0) / 10.0) * 10.0);

        DrawHorizontalGuide(canvas, padLeft, plotW, padTop, plotH, maxY, maxY * 0.5, $"{maxY * 0.5:0}", (Brush)dict["ForgeHairlineBrush"], (Brush)dict["ForgeTextMutedBrush"]);
        DrawHorizontalGuide(canvas, padLeft, plotW, padTop, plotH, maxY, maxY * 0.85, $"{maxY * 0.85:0}", (Brush)dict["ForgeHairlineBrush"], (Brush)dict["ForgeTextSecondaryBrush"]);

        double slotW = plotW / Math.Max(1, points.Count);
        var polyline = new Polyline
        {
            Stroke = lineBrush,
            StrokeThickness = 2.25
        };

        for (int i = 0; i < points.Count; i++)
        {
            DailyTrendPoint pt = points[i];
            double val = Math.Clamp(selector(pt), 0.0, maxY);
            double cx = padLeft + (i * slotW) + (slotW * 0.5);
            double cy = padTop + plotH - ((val / maxY) * plotH);

            if (pt.SessionCount > 0)
            {
                polyline.Points.Add(new Windows.Foundation.Point(cx, cy));

                var dot = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = lineBrush
                };
                ToolTipService.SetToolTip(dot, $"{pt.Date:MMM d} ({pt.DayLabel}): {val:0.0} {unitSuffix} · {pt.SessionCount} session(s)");
                Canvas.SetLeft(dot, cx - 4);
                Canvas.SetTop(dot, cy - 4);
                canvas.Children.Add(dot);
            }

            var dayLbl = new TextBlock
            {
                Text = pt.DayLabel,
                FontSize = 10,
                Foreground = (Brush)dict[pt.SessionCount > 0 ? "ForgeTextSecondaryBrush" : "ForgeTextDisabledBrush"]
            };
            Canvas.SetLeft(dayLbl, cx - 11);
            Canvas.SetTop(dayLbl, padTop + plotH + 5);
            canvas.Children.Add(dayLbl);
        }

        if (polyline.Points.Count >= 2)
        {
            canvas.Children.Insert(0, polyline);
        }
    }

    private static void RenderWeakBigramsCanvas(
        Canvas canvas,
        IReadOnlyList<TypeForge.Core.Engine.RankedWeakness<string>> bigrams,
        ResourceDictionary dict)
    {
        canvas.Children.Clear();
        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;
        if (width < 40 || height < 40)
        {
            return;
        }

        if (bigrams.Count == 0)
        {
            DrawEmptyChartHint(canvas, width, height, "Type more 2-letter transitions to rank weak pairs", (Brush)dict["ForgeTextMutedBrush"]);
            return;
        }

        int count = Math.Min(6, bigrams.Count);
        double rowH = Math.Max(22, (height - 8) / count);
        double maxLatency = Math.Max(350.0, bigrams.Take(count).Max(b => b.AverageLatencyMs));
        double barLeft = 48;
        double barMaxW = Math.Max(40, width - barLeft - 118);

        for (int i = 0; i < count; i++)
        {
            var item = bigrams[i];
            double y = 4 + (i * rowH);

            var pairLabel = new TextBlock
            {
                Text = item.Target.ToLowerInvariant(),
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = (Brush)dict["ForgeTextPrimaryBrush"]
            };
            Canvas.SetLeft(pairLabel, 6);
            Canvas.SetTop(pairLabel, y + 2);
            canvas.Children.Add(pairLabel);

            double ratio = Math.Clamp(item.AverageLatencyMs / maxLatency, 0.12, 1.0);
            double barW = barMaxW * ratio;
            Brush fill = item.ErrorRatePercentage >= 8.0 || item.AverageLatencyMs >= 320.0
                ? (Brush)dict["ForgeRedBrush"]
                : item.ErrorRatePercentage >= 4.0 || item.AverageLatencyMs >= 220.0
                    ? (Brush)dict["ForgeYellowBrush"]
                    : (Brush)dict["ForgePurpleBrush"];

            var bar = new Rectangle
            {
                Width = barW,
                Height = 12,
                RadiusX = 3,
                RadiusY = 3,
                Fill = fill
            };
            ToolTipService.SetToolTip(bar, $"Pair '{item.Target}' · {item.AverageLatencyMs:0} ms · {item.ErrorRatePercentage:0.0}% err · {item.SampleCount} samples");
            Canvas.SetLeft(bar, barLeft);
            Canvas.SetTop(bar, y + 5);
            canvas.Children.Add(bar);

            var metricTxt = new TextBlock
            {
                Text = $"{item.AverageLatencyMs:0}ms · {item.ErrorRatePercentage:0.#}% err",
                FontSize = 11,
                Foreground = (Brush)dict["ForgeTextSecondaryBrush"]
            };
            Canvas.SetLeft(metricTxt, barLeft + barW + 8);
            Canvas.SetTop(metricTxt, y + 3);
            canvas.Children.Add(metricTxt);
        }
    }

    private static void RenderDueReviewKeysCanvas(
        Canvas canvas,
        IReadOnlyList<char> dueKeys,
        IReadOnlyList<KeyReviewScheduleRecord> upcomingSchedules,
        ResourceDictionary dict)
    {
        canvas.Children.Clear();
        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;
        if (width < 40 || height < 40)
        {
            return;
        }

        if (upcomingSchedules.Count == 0)
        {
            DrawEmptyChartHint(canvas, width, height, "Complete a session to schedule spaced key reviews", (Brush)dict["ForgeTextMutedBrush"]);
            return;
        }

        HashSet<char> dueSet = dueKeys.Select(char.ToUpperInvariant).ToHashSet();
        int count = Math.Min(10, upcomingSchedules.Count);
        const int cols = 5;
        double cellW = Math.Max(56, (width - 16) / cols);
        double cellH = 68;

        for (int i = 0; i < count; i++)
        {
            KeyReviewScheduleRecord item = upcomingSchedules[i];
            char keyUpper = char.ToUpperInvariant(item.Character);
            bool isDue = dueSet.Contains(keyUpper);

            int col = i % cols;
            int row = i / cols;
            double x = 4 + (col * cellW);
            double y = 8 + (row * (cellH + 10));

            var badgeRect = new Rectangle
            {
                Width = cellW - 10,
                Height = cellH,
                RadiusX = 8,
                RadiusY = 8,
                Fill = (Brush)dict[isDue ? "ForgePillSelectedBrush" : "ForgeKeyNeutralBrush"],
                Stroke = (Brush)dict[isDue ? "ForgePurpleBrush" : "ForgeKeyNeutralBorderBrush"],
                StrokeThickness = isDue ? 1.5 : 1.0
            };
            Canvas.SetLeft(badgeRect, x);
            Canvas.SetTop(badgeRect, y);
            canvas.Children.Add(badgeRect);

            var keyTxt = new TextBlock
            {
                Text = keyUpper.ToString(),
                FontSize = 16,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = (Brush)dict[isDue ? "ForgePurpleBrush" : "ForgeTextPrimaryBrush"]
            };
            Canvas.SetLeft(keyTxt, x + ((cellW - 10) * 0.5) - 6);
            Canvas.SetTop(keyTxt, y + 10);
            canvas.Children.Add(keyTxt);

            string statusLabel = isDue ? "Due now" : $"In {Math.Max(0.5, item.IntervalDays):0.#}d";
            var subTxt = new TextBlock
            {
                Text = statusLabel,
                FontSize = 10,
                Foreground = (Brush)dict[isDue ? "ForgePurpleBrush" : "ForgeTextSecondaryBrush"]
            };
            Canvas.SetLeft(subTxt, x + 8);
            Canvas.SetTop(subTxt, y + 40);
            canvas.Children.Add(subTxt);
        }
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

    private void DrillSlowestPairs_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DrillSlowestPairsCommand.Execute(null);
    }

    private void EmptyState_ActionRequested(object? sender, TypeForge.Core.Branding.EmptyStateKind kind)
    {
        if (kind == TypeForge.Core.Branding.EmptyStateKind.NoStats)
        {
            ViewModel.NavigateToRoute("practice");
        }
        else if (kind == TypeForge.Core.Branding.EmptyStateKind.NoTestHistory)
        {
            ViewModel.NavigateToRoute("test");
        }
    }

    private static void RenderTestHistoryList(
        StackPanel host,
        IReadOnlyList<SessionResult> tests,
        ResourceDictionary dict)
    {
        host.Children.Clear();
        foreach (SessionResult test in tests)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(new TextBlock
            {
                Text = test.StartedAt.LocalDateTime.ToString("g"),
                FontSize = 12,
                Foreground = (Brush)dict["ForgeTextSecondaryBrush"],
                VerticalAlignment = VerticalAlignment.Center
            });

            var wpmTxt = new TextBlock
            {
                Text = $"{test.NetWpm:0.0} WPM",
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)dict["ForgePurpleBrush"],
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(wpmTxt, 1);
            row.Children.Add(wpmTxt);

            var accTxt = new TextBlock
            {
                Text = $"{test.AccuracyPercentage:0.0}% · {test.TotalKeystrokes} keys",
                FontSize = 12,
                Foreground = (Brush)dict["ForgeTextPrimaryBrush"],
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(accTxt, 2);
            row.Children.Add(accTxt);

            host.Children.Add(new Border
            {
                Background = (Brush)dict["ForgeCardInnerBrush"],
                BorderBrush = (Brush)dict["ForgeHairlineBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Child = row
            });
        }
    }

    private void RenderProfileLeaderboard()
    {
        ITypeForgeStateService state = App.GetService<ITypeForgeStateService>();
        IReadOnlyList<ProfileRankingEntry> rankings = state.GetProfileRankings();
        if (rankings.Count < 2)
        {
            ProfileLeaderboardCard.Visibility = Visibility.Collapsed;
            return;
        }

        ProfileLeaderboardCard.Visibility = Visibility.Visible;
        ProfileLeaderboardHost.Children.Clear();

        string themeKey = ActualTheme == ElementTheme.Light ? "Light" : "Dark";
        if (Application.Current.Resources.ThemeDictionaries[themeKey] is not ResourceDictionary dict)
        {
            return;
        }

        foreach (ProfileRankingEntry entry in rankings)
        {
            bool isActive = string.Equals(entry.Profile.Id, state.ActiveProfile.Id, StringComparison.OrdinalIgnoreCase);

            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(new TextBlock
            {
                Text = $"#{entry.Rank}",
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = entry.Rank == 1
                    ? (Brush)dict["ForgePurpleBrush"]
                    : (Brush)dict["ForgeTextSecondaryBrush"],
                VerticalAlignment = VerticalAlignment.Center
            });

            var identityPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                VerticalAlignment = VerticalAlignment.Center
            };

            identityPanel.Children.Add(new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = new SolidColorBrush(ProfileDialogs.ParseHexColor(entry.Profile.AvatarColorHex)),
                Child = new TextBlock
                {
                    Text = entry.Profile.Initials,
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });

            identityPanel.Children.Add(new TextBlock
            {
                Text = isActive ? $"{entry.Profile.Name} (Active)" : entry.Profile.Name,
                FontSize = 13,
                FontWeight = isActive ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                Foreground = (Brush)dict["ForgeTextPrimaryBrush"],
                VerticalAlignment = VerticalAlignment.Center
            });

            Grid.SetColumn(identityPanel, 1);
            row.Children.Add(identityPanel);

            var avgWpmTxt = new TextBlock
            {
                Text = entry.SessionCount > 0 ? $"{entry.AverageNetWpm:0.0} avg WPM" : "—",
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)dict["ForgePurpleBrush"],
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(avgWpmTxt, 2);
            row.Children.Add(avgWpmTxt);

            var bestWpmTxt = new TextBlock
            {
                Text = entry.SessionCount > 0 ? $"Best {entry.BestNetWpm:0}" : "",
                FontSize = 12,
                Foreground = (Brush)dict["ForgeTextSecondaryBrush"],
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(bestWpmTxt, 3);
            row.Children.Add(bestWpmTxt);

            var accTxt = new TextBlock
            {
                Text = entry.SessionCount > 0 ? $"{entry.AverageAccuracyPercentage:0.0}% acc" : "0 sessions",
                FontSize = 12,
                Foreground = (Brush)dict["ForgeTextPrimaryBrush"],
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(accTxt, 4);
            row.Children.Add(accTxt);

            var courseTxt = new TextBlock
            {
                Text = $"{entry.CourseCompletionPercentage:0}% course · {entry.StreakDays}d",
                FontSize = 12,
                Foreground = (Brush)dict["ForgeTextSecondaryBrush"],
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(courseTxt, 5);
            row.Children.Add(courseTxt);

            ProfileLeaderboardHost.Children.Add(new Border
            {
                Background = (Brush)dict["ForgeCardInnerBrush"],
                BorderBrush = isActive
                    ? (Brush)dict["ForgePurpleBrush"]
                    : (Brush)dict["ForgeHairlineBrush"],
                BorderThickness = new Thickness(isActive ? 1.5 : 1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Child = row
            });
        }
    }
}
