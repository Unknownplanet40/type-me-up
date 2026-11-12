using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using TypeForge.App.ViewModels;
using TypeForge.Core.Engine;
using TypeForge.Core.Models;

namespace TypeForge.App.Views;

public sealed partial class CoursePage : Page
{
    private bool _isStackedLayout;
    private bool _isSingleColumnCatalog;

    public CourseViewModel ViewModel { get; }

    public CoursePage()
    {
        ViewModel = App.GetService<CourseViewModel>();
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.StateRefreshed += RenderCourseState;
        CourseContentGrid.SizeChanged += CourseContentGrid_SizeChanged;
        CatalogTracksGrid.SizeChanged += CatalogTracksGrid_SizeChanged;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.RefreshOnNavigate();
    }

    private void CourseContentGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool shouldStack = e.NewSize.Width > 0 && e.NewSize.Width < 880;
        if (shouldStack == _isStackedLayout)
        {
            return;
        }

        _isStackedLayout = shouldStack;
        if (shouldStack)
        {
            ChecklistColumn.Width = new GridLength(1, GridUnitType.Star);
            ContextColumn.Width = new GridLength(0);
            Grid.SetColumn(ContextualLearningColumn, 0);
            Grid.SetRow(ContextualLearningColumn, 1);
        }
        else
        {
            ChecklistColumn.Width = new GridLength(1.65, GridUnitType.Star);
            ContextColumn.Width = new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(ContextualLearningColumn, 1);
            Grid.SetRow(ContextualLearningColumn, 0);
        }
    }

    private void CatalogTracksGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool singleColumn = e.NewSize.Width > 0 && e.NewSize.Width < 740;
        if (singleColumn == _isSingleColumnCatalog)
        {
            return;
        }

        _isSingleColumnCatalog = singleColumn;
        RenderCatalogTracks();
    }

    private void CoursePlacementWelcomeOverlay_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        CoursePlacementWelcomeActions.Orientation = e.NewSize.Width is > 0 and < 480
            ? Orientation.Vertical
            : Orientation.Horizontal;
    }

    private void RenderCourseState()
    {
        RenderCatalogTracks();
        RenderProgressOverview();
        RenderUnitTabs();
        RenderLessonDetail();
        RenderExercises();
        RenderContextualPanel();
    }

    private void RenderCatalogTracks()
    {
        CatalogTracksGrid.Children.Clear();
        CatalogTracksGrid.ColumnDefinitions.Clear();
        CatalogTracksGrid.RowDefinitions.Clear();

        int columns = _isSingleColumnCatalog ? 1 : 2;
        for (int c = 0; c < columns; c++)
        {
            CatalogTracksGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        int rows = (int)Math.Ceiling(ViewModel.Tracks.Count / (double)columns);
        for (int r = 0; r < rows; r++)
        {
            CatalogTracksGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (int i = 0; i < ViewModel.Tracks.Count; i++)
        {
            CourseTrack track = ViewModel.Tracks[i];
            bool isSelected = string.Equals(track.TrackId, ViewModel.ActiveTrack.TrackId, StringComparison.OrdinalIgnoreCase);

            int completedLessons = track.Units.Count(u => u.IsCompleted);
            int totalLessons = track.Units.Count;
            int pct = totalLessons == 0 ? 0 : (int)Math.Round(100.0 * completedLessons / totalLessons);
            string statusText = completedLessons == totalLessons && totalLessons > 0
                ? "Completed"
                : track.CompletedExerciseCount > 0
                    ? "In Progress"
                    : "Not Started";

            var card = new Border
            {
                Background = Brush(isSelected ? "ForgePurpleSoftBrush" : "ForgeCardInnerBrush"),
                BorderBrush = Brush(isSelected ? "ForgePurpleBrush" : "ForgeHairlineBrush"),
                BorderThickness = new Thickness(isSelected ? 1.5 : 1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var root = new StackPanel { Spacing = 10 };

            var topRow = new Grid { ColumnSpacing = 12 };
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Spacing = 4 };
            titleStack.Children.Add(new TextBlock
            {
                Text = track.Title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush("ForgeTextPrimaryBrush"),
                TextWrapping = TextWrapping.Wrap
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = track.Description,
                FontSize = 12,
                Foreground = Brush("ForgeTextSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap
            });
            topRow.Children.Add(titleStack);

            var statusPill = new Border
            {
                Background = Brush(completedLessons == totalLessons && totalLessons > 0 ? "ForgeGreenFillBrush" : "ForgeCardBrush"),
                BorderBrush = Brush(completedLessons == totalLessons && totalLessons > 0 ? "ForgeGreenBorderBrush" : "ForgeHairlineBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = statusText,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(completedLessons == totalLessons && totalLessons > 0 ? "ForgeGreenBrush" : "ForgeTextSecondaryBrush")
                }
            };
            Grid.SetColumn(statusPill, 1);
            topRow.Children.Add(statusPill);
            root.Children.Add(topRow);

            var bottomRow = new Grid { ColumnSpacing = 12 };
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var metaPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            metaPanel.Children.Add(new TextBlock
            {
                Text = $"{totalLessons} Lessons",
                FontSize = 12,
                Foreground = Brush("ForgeTextMutedBrush")
            });
            metaPanel.Children.Add(new TextBlock
            {
                Text = $"{pct}% Complete",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(isSelected ? "ForgePurpleBrush" : "ForgeTextSecondaryBrush")
            });
            bottomRow.Children.Add(metaPanel);

            var selectBtn = new Button
            {
                Style = (Style)Application.Current.Resources[isSelected ? "ForgePrimaryButtonStyle" : "ForgeSecondaryButtonStyle"],
                Content = isSelected ? "Selected" : (track.CompletedExerciseCount > 0 ? "Continue" : "Select"),
                Padding = new Thickness(12, 5, 12, 5),
                FontSize = 12
            };
            string capturedId = track.TrackId;
            selectBtn.Click += (_, _) => ViewModel.SelectTrackCommand.Execute(capturedId);
            Grid.SetColumn(selectBtn, 1);
            bottomRow.Children.Add(selectBtn);

            root.Children.Add(bottomRow);
            card.Child = root;

            Grid.SetColumn(card, i % columns);
            Grid.SetRow(card, i / columns);
            CatalogTracksGrid.Children.Add(card);
        }
    }

    private void RenderProgressOverview()
    {
        int completed = ViewModel.CompletedLessonsCount;
        int total = ViewModel.TotalLessonsCount;
        int remaining = ViewModel.RemainingLessonsCount;
        int pct = ViewModel.TrackCompletionPercent;

        ProgressLessonsText.Text = $"{completed} / {total} Lessons Completed";
        ProgressRemainingText.Text = remaining == 1 ? "1 Lesson Remaining" : $"{remaining} Lessons Remaining";
        ProgressStarsText.Text = $"{ViewModel.EarnedStarsCount} / {ViewModel.MaxStarsCount} Stars";
        ProgressPercentText.Text = $"{pct}% Complete";
        CourseProgressBar.Value = pct;
    }

    private void RenderUnitTabs()
    {
        UnitTabsPanel.Children.Clear();
        CourseTrack track = ViewModel.ActiveTrack;

        foreach (CourseUnit unit in track.Units)
        {
            bool isSelected = unit.UnitNumber == ViewModel.ActiveUnit.UnitNumber;
            bool isLocked = !unit.IsUnlocked;
            bool isCompleted = unit.IsCompleted;

            int unitStars = unit.Exercises.Sum(e => e.Stars);
            int unitMaxStars = unit.Exercises.Count(e => e.Kind != CourseStepKind.Tip) * 3;

            var content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (isCompleted)
            {
                content.Children.Add(new FontIcon
                {
                    Glyph = "\uE73E",
                    FontSize = 11,
                    Foreground = Brush("ForgeGreenBrush")
                });
            }
            else if (isLocked)
            {
                content.Children.Add(new FontIcon
                {
                    Glyph = "\uE72E",
                    FontSize = 11,
                    Foreground = Brush("ForgeTextDisabledBrush")
                });
            }

            content.Children.Add(new TextBlock
            {
                Text = unit.UnitNumber.ToString("00"),
                FontSize = 13,
                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = Brush(isLocked
                    ? "ForgeTextDisabledBrush"
                    : isSelected
                        ? "ForgeTextPrimaryBrush"
                        : "ForgeTextSecondaryBrush")
            });

            if (isCompleted && unitMaxStars > 0)
            {
                content.Children.Add(new TextBlock
                {
                    Text = $"★{unitStars}",
                    FontSize = 11,
                    Foreground = Brush("ForgeYellowBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            var btn = new Button
            {
                MinWidth = 52,
                Height = 38,
                Padding = new Thickness(12, 0, 12, 0),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(isSelected ? 1.5 : 1),
                BorderBrush = Brush(isSelected
                    ? "ForgePurpleBrush"
                    : isCompleted
                        ? "ForgeGreenBorderBrush"
                        : "ForgeHairlineBrush"),
                Background = Brush(isSelected
                    ? "ForgePurpleSoftBrush"
                    : isCompleted
                        ? "ForgeGreenFillBrush"
                        : "ForgeCardBrush"),
                Content = content
            };

            string unlockReason = ViewModel.GetLockedUnitReason(unit);
            string accessibleName = isLocked
                ? $"Lesson {unit.UnitNumber:00} — {unit.Title}, Locked. {unlockReason}"
                : isCompleted
                    ? $"Lesson {unit.UnitNumber:00} — {unit.Title}, Completed, {unitStars} of {unitMaxStars} stars"
                    : $"Lesson {unit.UnitNumber:00} — {unit.Title}";

            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(btn, accessibleName);

            if (isLocked)
            {
                ToolTipService.SetToolTip(btn, unlockReason);
                btn.Click += (_, _) => { /* Locked lessons cannot bypass progression rules */ };
            }
            else
            {
                ToolTipService.SetToolTip(btn, $"Lesson {unit.UnitNumber:00} — {unit.Title}");
                int capturedUnit = unit.UnitNumber;
                btn.Click += (_, _) => ViewModel.SelectUnitCommand.Execute(capturedUnit);
            }

            UnitTabsPanel.Children.Add(btn);
        }
    }

    private void RenderLessonDetail()
    {
        CourseUnit unit = ViewModel.ActiveUnit;
        LessonHeaderTitle.Text = $"Lesson {unit.UnitNumber:00} — {unit.Title}";
        LessonHeaderDescription.Text = string.IsNullOrWhiteSpace(unit.ErgonomicTip)
            ? "Follow the sequential steps below to build accuracy and muscle memory."
            : unit.ErgonomicTip;

        string status = ViewModel.GetUnitStatusLabel(unit);
        LessonStatusText.Text = status;
        LessonStatusBadge.Background = Brush(unit.IsCompleted
            ? "ForgeGreenFillBrush"
            : unit.IsUnlocked
                ? "ForgePurpleSoftBrush"
                : "ForgeCardInnerBrush");
        LessonStatusBadge.BorderBrush = Brush(unit.IsCompleted
            ? "ForgeGreenBorderBrush"
            : unit.IsUnlocked
                ? "ForgePurpleBrush"
                : "ForgeHairlineBrush");
        LessonStatusText.Foreground = Brush(unit.IsCompleted
            ? "ForgeGreenBrush"
            : unit.IsUnlocked
                ? "ForgePurpleBrush"
                : "ForgeTextMutedBrush");

        int unitStars = unit.Exercises.Sum(e => e.Stars);
        int unitMaxStars = unit.Exercises.Count(e => e.Kind != CourseStepKind.Tip) * 3;
        LessonStarsSummary.Text = unitMaxStars > 0 ? $"★ {unitStars} / {unitMaxStars}" : string.Empty;

        // Focus Keys colored by persisted mastery
        LessonFocusKeysHost.Children.Clear();
        List<char> focusChars = unit.GetFocusKeyCharacters().Take(8).ToList();
        foreach (char ch in focusChars)
        {
            KeyMetric? metric = ViewModel.GetKeyMetric(ch);
            string fillBrush = "ForgeCardInnerBrush";
            string borderBrush = "ForgeKeyNeutralBorderBrush";
            string textBrush = "ForgeTextPrimaryBrush";

            if (metric is not null && metric.TotalKeystrokes >= 10)
            {
                if (metric.AccuracyPercentage >= 95.0)
                {
                    fillBrush = "ForgeGreenFillBrush";
                    borderBrush = "ForgeGreenBorderBrush";
                    textBrush = "ForgeGreenBrush";
                }
                else if (metric.AccuracyPercentage >= 85.0)
                {
                    fillBrush = "ForgeYellowFillBrush";
                    borderBrush = "ForgeYellowBorderBrush";
                    textBrush = "ForgeYellowBrush";
                }
                else
                {
                    fillBrush = "ForgeRedFillBrush";
                    borderBrush = "ForgeRedBorderBrush";
                    textBrush = "ForgeRedBrush";
                }
            }

            LessonFocusKeysHost.Children.Add(new Border
            {
                Background = Brush(fillBrush),
                BorderBrush = Brush(borderBrush),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                MinWidth = 30,
                Height = 28,
                Padding = new Thickness(8, 0, 8, 0),
                Child = new TextBlock
                {
                    Text = ch.ToString().ToUpperInvariant(),
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush(textBrush),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });
        }

        // Minimum accuracy, WPM, and duration estimate from real unit exercises
        List<CourseExercise> typedSteps = unit.Exercises.Where(e => e.Kind != CourseStepKind.Tip).ToList();
        double minAcc = typedSteps.Count > 0 ? typedSteps.Min(e => e.MinimumAccuracy) : 90.0;
        double minWpm = typedSteps.Count > 0 ? typedSteps.Max(e => e.MinimumNetWpm) : 0.0;
        bool anyShowWpm = typedSteps.Any(e => e.ShowWpm);

        LessonTargetAccuracyText.Text = $"Min Accuracy: {minAcc:0}%";
        LessonTargetWpmText.Text = anyShowWpm && minWpm > 0
            ? $"Min Speed: {minWpm:0} WPM"
            : "Accuracy-first (WPM hidden)";

        string? firstDuration = unit.Exercises
            .Select(e => e.EstimatedMinutes)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
        LessonDurationChip.Visibility = string.IsNullOrWhiteSpace(firstDuration)
            ? Visibility.Collapsed
            : Visibility.Visible;
        LessonDurationText.Text = firstDuration ?? string.Empty;
    }

    private void RenderExercises()
    {
        ExercisesHost.Children.Clear();
        CourseUnit unit = ViewModel.ActiveUnit;
        ExercisesProgressLabel.Text = $"{unit.CompletedCount} of {unit.Exercises.Count} steps complete";

        CourseExercise? activeStep = unit.Exercises.FirstOrDefault(e => e.IsUnlocked && !e.IsCompleted);

        foreach (CourseExercise ex in unit.Exercises)
        {
            bool isActive = activeStep is not null && string.Equals(activeStep.ExerciseId, ex.ExerciseId, StringComparison.OrdinalIgnoreCase);
            bool isLocked = !ex.IsUnlocked;

            var row = new Border
            {
                Background = Brush(isActive ? "ForgePurpleSoftBrush" : "ForgeCardInnerBrush"),
                BorderBrush = Brush(isActive
                    ? "ForgePurpleBrush"
                    : ex.IsCompleted
                        ? "ForgeGreenBorderBrush"
                        : "ForgeHairlineBrush"),
                BorderThickness = new Thickness(isActive ? 1.5 : 1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12, 14, 12)
            };

            var grid = new Grid { ColumnSpacing = 12 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Step number + kind icon badge
            var badgeContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeContent.Children.Add(new TextBlock
            {
                Text = ex.OrderIndex.ToString(),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(ex.IsCompleted
                    ? "ForgeGreenBrush"
                    : isActive
                        ? "ForgePurpleBrush"
                        : isLocked
                            ? "ForgeTextDisabledBrush"
                            : "ForgeTextSecondaryBrush")
            });
            badgeContent.Children.Add(new FontIcon
            {
                Glyph = ex.IsCompleted ? "\uE73E" : isLocked ? "\uE72E" : GetStepIconGlyph(ex.Kind),
                FontSize = 13,
                Foreground = Brush(ex.IsCompleted
                    ? "ForgeGreenBrush"
                    : isActive
                        ? "ForgePurpleBrush"
                        : isLocked
                            ? "ForgeTextDisabledBrush"
                            : "ForgeTextSecondaryBrush")
            });

            var badge = new Border
            {
                Width = 48,
                Height = 32,
                CornerRadius = new CornerRadius(8),
                Background = Brush(ex.IsCompleted
                    ? "ForgeGreenFillBrush"
                    : isActive
                        ? "ForgeCardBrush"
                        : "ForgeCardBrush"),
                BorderBrush = Brush(ex.IsCompleted
                    ? "ForgeGreenBorderBrush"
                    : isActive
                        ? "ForgePurpleBrush"
                        : "ForgeHairlineBrush"),
                BorderThickness = new Thickness(1),
                Child = badgeContent
            };
            grid.Children.Add(badge);

            // Title + 1-line description + performance/status summary
            var textStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 2
            };
            textStack.Children.Add(new TextBlock
            {
                Text = ex.Title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush(isLocked ? "ForgeTextDisabledBrush" : "ForgeTextPrimaryBrush")
            });

            string subLine;
            if (isLocked)
            {
                subLine = ViewModel.GetLockedExerciseReason(ex);
            }
            else if (ex.IsCompleted && ex.Kind != CourseStepKind.Tip && ex.BestAccuracy > 0)
            {
                string stars = new string('★', ex.Stars) + new string('☆', Math.Max(0, 3 - ex.Stars));
                subLine = ex.ShowWpm
                    ? $"Completed · Best {ex.BestAccuracy:0.0}% · {ex.BestWpm:0} WPM · {stars}"
                    : $"Completed · Best {ex.BestAccuracy:0.0}% · {stars}";
            }
            else if (ex.IsCompleted)
            {
                subLine = "Completed";
            }
            else if (unit.IsFinalExercise(ex) && ex.Kind != CourseStepKind.Tip)
            {
                subLine = "Final Check: 100% accuracy required on all lesson focus keys to unlock next lesson.";
            }
            else
            {
                subLine = GetStepOneLineDescription(ex);
            }

            textStack.Children.Add(new TextBlock
            {
                Text = subLine,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = Brush(isLocked
                    ? "ForgeTextDisabledBrush"
                    : ex.IsCompleted
                        ? "ForgeGreenBrush"
                        : "ForgeTextSecondaryBrush")
            });

            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            // Action button
            if (!isLocked)
            {
                string buttonText = ex.Kind == CourseStepKind.Tip
                    ? (ex.IsCompleted ? "Review Tip" : "Read Tip")
                    : ex.IsCompleted
                        ? "Replay"
                        : isActive
                            ? "Continue"
                            : "Start";

                var actionBtn = new Button
                {
                    Style = (Style)Application.Current.Resources[isActive ? "ForgePrimaryButtonStyle" : "ForgeSecondaryButtonStyle"],
                    Content = buttonText,
                    Padding = new Thickness(14, 6, 14, 6),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };
                CourseExercise captured = ex;
                actionBtn.Click += (_, _) => ViewModel.ActivateExerciseCommand.Execute(captured);
                Grid.SetColumn(actionBtn, 2);
                grid.Children.Add(actionBtn);
            }

            row.Child = grid;
            ExercisesHost.Children.Add(row);
        }
    }

    private void RenderContextualPanel()
    {
        CourseUnit unit = ViewModel.ActiveUnit;
        List<char> focusChars = unit.GetFocusKeyCharacters().Take(8).ToList();

        // Section A: Finger Guide
        CourseFingerGuide.UpdateFocusKeys(unit.FocusKeys);

        // Section B: Focus Keys & Finger Assignments
        FocusKeysPillsHost.Children.Clear();
        var fingerMapPairs = new List<string>();
        foreach (char ch in focusChars)
        {
            FocusKeysPillsHost.Children.Add(new Border
            {
                Background = Brush("ForgeCardInnerBrush"),
                BorderBrush = Brush("ForgeKeyNeutralBorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                MinWidth = 30,
                Height = 28,
                Padding = new Thickness(8, 0, 8, 0),
                Child = new TextBlock
                {
                    Text = ch.ToString().ToUpperInvariant(),
                    FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brush("ForgeTextPrimaryBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            });

            if (FingerMap.TryGetFinger(ch, out FingerId finger))
            {
                fingerMapPairs.Add($"{char.ToUpperInvariant(ch)}: {FingerMap.GetDisplayName(finger)}");
            }
        }

        FocusKeysFingerText.Text = fingerMapPairs.Count > 0
            ? string.Join(" · ", fingerMapPairs)
            : "Use home-row posture and return fingers after each reach.";

        // Section C: Active Tip OR Learning Advice
        if (ViewModel.ActiveTipExercise is { } tip)
        {
            TipDetailCard.Visibility = Visibility.Visible;
            LearningAdviceCard.Visibility = Visibility.Collapsed;
            TipTitleText.Text = tip.Title;
            TipBodyText.Text = tip.TipContent;
        }
        else
        {
            TipDetailCard.Visibility = Visibility.Collapsed;
            LearningAdviceCard.Visibility = Visibility.Visible;
            bool accuracyFirst = unit.Exercises.Any(e => e.Kind != CourseStepKind.Tip && !e.ShowWpm);
            LearningAdviceText.Text = !string.IsNullOrWhiteSpace(unit.ErgonomicTip)
                ? unit.ErgonomicTip
                : accuracyFirst
                    ? "Focus on clean accuracy and steady rhythm first. Speed builds naturally once finger reaches feel automatic."
                    : "Keep your wrists neutral, eyes on the typing stream, and maintain an even cadence across letter pairs.";
        }
    }

    private static List<char> ParseFocusKeys(string focusKeys)
    {
        return (focusKeys ?? string.Empty)
            .Split(new[] { ' ', ',', '·' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t[0])
            .Distinct()
            .Take(8)
            .ToList();
    }

    private static string GetStepIconGlyph(CourseStepKind kind) => kind switch
    {
        CourseStepKind.Tip => "\uE946",
        CourseStepKind.NewKeys => "\uE765",
        CourseStepKind.KeyDrill => "\uE8FD",
        CourseStepKind.WordDrill => "\uE8A5",
        CourseStepKind.ParagraphDrill => "\uE8C4",
        _ => "\uE768"
    };

    private static string GetStepOneLineDescription(CourseExercise ex)
    {
        if (!string.IsNullOrWhiteSpace(ex.TipContent))
        {
            return ex.TipContent;
        }

        return ex.Kind switch
        {
            CourseStepKind.Tip => "Read ergonomic guidance before starting the drills.",
            CourseStepKind.NewKeys => "Introduce and practice the new key positions.",
            CourseStepKind.KeyDrill => "Build muscle memory with focused key combinations.",
            CourseStepKind.WordDrill => "Apply your reach accuracy in real words.",
            CourseStepKind.ParagraphDrill => "Sustain flow and accuracy across full sentences.",
            _ => "Complete this exercise step."
        };
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
