using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TypeForge.App.ViewModels;
using TypeForge.Core.Models;

namespace TypeForge.App.Views;

public sealed partial class CoursePage : Page
{
    public CourseViewModel ViewModel { get; }

    public CoursePage()
    {
        ViewModel = App.GetService<CourseViewModel>();
        InitializeComponent();
        ViewModel.StateRefreshed += RenderView;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.RefreshOnNavigate();
    }

    private void RenderView()
    {
        string themeKey = ActualTheme == ElementTheme.Light ? "Light" : "Dark";
        var dict = Application.Current.Resources.ThemeDictionaries[themeKey] as ResourceDictionary;
        if (dict is null)
        {
            return;
        }

        CourseTrack track = ViewModel.ActiveTrack;
        CourseTrackTitle.Text = track.Title;
        CourseTrackDesc.Text = track.Description;
        CourseCatalogPanel.Visibility = ViewModel.IsCoursePickerOpen ? Visibility.Visible : Visibility.Collapsed;
        OptimalBadge.Visibility = ViewModel.OptimalDurationEnabled ? Visibility.Visible : Visibility.Collapsed;

        int completed = track.CompletedExerciseCount;
        int total = Math.Max(1, track.TotalExerciseCount);
        double pct = Math.Round(track.ProgressFraction * 100.0, 0);
        CourseProgressBar.Value = pct;
        CourseProgressSummary.Text = $"{completed} / {total} steps · {pct:0}%";
        CourseEstimateText.Text = $"Est. {track.DurationEstimate}";

        RenderCatalogTracks(dict);
        RenderUnitTabs(track, dict);
        RenderActiveUnit(dict);
        RenderTipPanel();
    }

    private void RenderCatalogTracks(ResourceDictionary dict)
    {
        CatalogTracksHost.Children.Clear();
        foreach (CourseTrack track in ViewModel.Tracks.Where(t => t.TrackId is "fast_touch" or "speed_building"))
        {
            bool isSelected = string.Equals(track.TrackId, ViewModel.ActiveTrack.TrackId, StringComparison.OrdinalIgnoreCase);

            var info = new StackPanel { Spacing = 3 };
            info.Children.Add(new TextBlock
            {
                Text = track.Title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)dict["ForgeTextPrimaryBrush"]
            });
            info.Children.Add(new TextBlock
            {
                Text = $"{track.Description} ({track.Units.Count} lessons · {Math.Round(track.ProgressFraction * 100)}% completed)",
                FontSize = 12,
                Foreground = (Brush)dict["ForgeTextSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap
            });

            var actionBtn = new Button
            {
                Content = isSelected ? "Active Course" : "Select Course",
                Style = (Style)Application.Current.Resources[isSelected ? "ForgePrimaryButtonStyle" : "ForgeSecondaryButtonStyle"],
                VerticalAlignment = VerticalAlignment.Center
            };
            actionBtn.Click += (_, _) => ViewModel.SelectTrackCommand.Execute(track.TrackId);

            var grid = new Grid { ColumnSpacing = 14 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(info, 0);
            Grid.SetColumn(actionBtn, 1);
            grid.Children.Add(info);
            grid.Children.Add(actionBtn);

            CatalogTracksHost.Children.Add(new Border
            {
                Background = (Brush)dict["ForgeCardInnerBrush"],
                BorderBrush = (Brush)dict[isSelected ? "ForgePurpleBrush" : "ForgeHairlineBrush"],
                BorderThickness = new Thickness(isSelected ? 1.5 : 1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 12),
                Child = grid
            });
        }
    }

    private void RenderUnitTabs(CourseTrack track, ResourceDictionary dict)
    {
        UnitTabsPanel.Children.Clear();
        foreach (CourseUnit unit in track.Units)
        {
            bool isCurrent = unit.UnitNumber == ViewModel.SelectedUnitNumber;
            Brush bg;
            Brush fg;
            Brush border;

            if (isCurrent)
            {
                bg = (Brush)dict["ForgePurpleBrush"];
                fg = (Brush)dict["ForgeCanvasBrush"];
                border = (Brush)dict["ForgePurpleBrush"];
            }
            else if (unit.IsCompleted)
            {
                bg = (Brush)dict["ForgeGreenFillBrush"];
                fg = (Brush)dict["ForgeGreenBrush"];
                border = (Brush)dict["ForgeGreenBorderBrush"];
            }
            else
            {
                bg = (Brush)dict["ForgeKeyNeutralBrush"];
                fg = unit.IsUnlocked ? (Brush)dict["ForgeTextPrimaryBrush"] : (Brush)dict["ForgeTextMutedBrush"];
                border = (Brush)dict["ForgeHairlineBrush"];
            }

            var tabBtn = new Button
            {
                Content = unit.UnitNumber.ToString(),
                Width = 34,
                Height = 30,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 6, 0),
                CornerRadius = new CornerRadius(7),
                Background = bg,
                Foreground = fg,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                FontWeight = FontWeights.SemiBold,
                FontSize = 12,
                IsEnabled = unit.IsUnlocked,
                Opacity = unit.IsUnlocked ? 1.0 : 0.45
            };
            ToolTipService.SetToolTip(tabBtn, $"Lesson {unit.UnitNumber}: {unit.Title}");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(tabBtn, $"Lesson {unit.UnitNumber} {unit.Title}");
            tabBtn.Click += (_, _) => ViewModel.SelectUnitCommand.Execute(unit.UnitNumber);
            UnitTabsPanel.Children.Add(tabBtn);
        }
    }

    private void RenderActiveUnit(ResourceDictionary dict)
    {
        CourseUnit unit = ViewModel.ActiveUnit;
        ActiveUnitHeading.Text = $"Lesson {unit.UnitNumber}: {unit.Title}";
        ActiveUnitFocusKeys.Text = string.IsNullOrWhiteSpace(unit.FocusKeys)
            ? string.Empty
            : $"Focus keys: {unit.FocusKeys}";
        ActiveUnitTip.Text = unit.ErgonomicTip;

        ExercisesHost.Children.Clear();
        foreach (CourseExercise exercise in unit.Exercises)
        {
            bool isSelectedTip = ViewModel.ActiveTipExercise?.ExerciseId == exercise.ExerciseId;
            string kindLabel = exercise.Kind switch
            {
                CourseStepKind.Tip => "Tip",
                CourseStepKind.NewKeys => "New Keys",
                CourseStepKind.KeyDrill => "Key Drill",
                CourseStepKind.WordDrill => "Word Drill",
                _ => "Paragraph"
            };

            string iconGlyph = exercise.IsCompleted
                ? "\uE73E"
                : exercise.IsUnlocked
                    ? (exercise.Kind == CourseStepKind.Tip ? "\uE82F" : "\uE768")
                    : "\uE72E";

            Brush iconColor = exercise.IsCompleted
                ? (Brush)dict["ForgeGreenBrush"]
                : exercise.IsUnlocked
                    ? (Brush)dict["ForgePurpleBrush"]
                    : (Brush)dict["ForgeTextMutedBrush"];

            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new FontIcon
            {
                Glyph = iconGlyph,
                FontSize = 14,
                Foreground = iconColor,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(icon, 0);

            var titleStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text = exercise.Title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)dict[exercise.IsUnlocked ? "ForgeTextPrimaryBrush" : "ForgeTextMutedBrush"]
            });

            if (exercise.IsCompleted && exercise.Kind != CourseStepKind.Tip)
            {
                string stars = new string('★', exercise.Stars) + new string('☆', 3 - exercise.Stars);
                titleStack.Children.Add(new TextBlock
                {
                    Text = $"{stars} · Best {Math.Round(exercise.BestWpm):0} WPM · {exercise.BestAccuracy:0.0}%",
                    FontSize = 11,
                    Foreground = (Brush)dict["ForgeGreenBrush"]
                });
            }
            else if (exercise.IsUnlocked && exercise.Kind != CourseStepKind.Tip)
            {
                titleStack.Children.Add(new TextBlock
                {
                    Text = $"Goal: {exercise.MinimumNetWpm:0} WPM · {exercise.MinimumAccuracy:0}% acc",
                    FontSize = 11,
                    Foreground = (Brush)dict["ForgeTextSecondaryBrush"]
                });
            }

            Grid.SetColumn(titleStack, 1);

            var kindBadge = new Border
            {
                Background = (Brush)dict["ForgeKeyNeutralBrush"],
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = kindLabel,
                    FontSize = 11,
                    Foreground = (Brush)dict["ForgeTextSecondaryBrush"]
                }
            };
            Grid.SetColumn(kindBadge, 2);

            var estBlock = new TextBlock
            {
                Text = exercise.EstimatedMinutes,
                FontSize = 12,
                Foreground = (Brush)dict["ForgeTextSecondaryBrush"],
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 62,
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(estBlock, 3);

            row.Children.Add(icon);
            row.Children.Add(titleStack);
            row.Children.Add(kindBadge);
            row.Children.Add(estBlock);

            var stepBtn = new Button
            {
                Content = row,
                IsEnabled = exercise.IsUnlocked,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12, 10, 12, 10),
                CornerRadius = new CornerRadius(9),
                Background = (Brush)dict[isSelectedTip ? "ForgePillSelectedBrush" : "ForgeCardInnerBrush"],
                BorderBrush = (Brush)dict[isSelectedTip ? "ForgePurpleBrush" : "ForgeHairlineBrush"],
                BorderThickness = new Thickness(isSelectedTip ? 1.5 : 1),
                Opacity = exercise.IsUnlocked ? 1.0 : 0.45
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(stepBtn, $"{exercise.Title} {kindLabel}");
            stepBtn.Click += (_, _) => ViewModel.ActivateExerciseCommand.Execute(exercise);
            ExercisesHost.Children.Add(stepBtn);
        }
    }

    private void RenderTipPanel()
    {
        if (ViewModel.ActiveTipExercise is { } tip)
        {
            TipDetailTitle.Text = tip.Title;
            TipDetailBody.Text = tip.TipContent;
            TipDetailCard.Visibility = Visibility.Visible;
        }
        else
        {
            TipDetailCard.Visibility = Visibility.Collapsed;
        }
    }

    private void ChangeCourse_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleCoursePickerCommand.Execute(null);
    }

    private void ContinueLesson_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ContinueCurrentLessonCommand.Execute(null);
    }

    private void StartExtraCourse_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StartSelectedExtraTrackCommand.Execute(null);
    }

    private void CompleteTip_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CompleteActiveTipCommand.Execute(null);
    }

    private void ToggleOptimalDuration_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleOptimalDurationCommand.Execute(null);
    }
}
