using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TypeForge.Core.Models;
using Windows.UI;

namespace TypeForge.App.Services;

internal static class ProfileDialogs
{
    public static Color ParseHexColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return Color.FromArgb(255, 167, 139, 250);
        }

        string clean = hex.Trim().TrimStart('#');
        if (clean.Length == 6 &&
            byte.TryParse(clean[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
            byte.TryParse(clean[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
            byte.TryParse(clean[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            return Color.FromArgb(255, r, g, b);
        }

        return Color.FromArgb(255, 167, 139, 250);
    }

    public static async Task<UserProfile?> ShowCreateProfileDialogAsync(XamlRoot xamlRoot, ITypeForgeStateService state)
    {
        var nameBox = new TextBox
        {
            PlaceholderText = "Enter profile name (e.g., Alex)",
            MaxLength = 32
        };

        string selectedColor = state.ProfileAvatarPalette.FirstOrDefault() ?? "#A78BFA";
        var swatchPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        var swatchButtons = new List<(Button Button, string Hex)>();

        void RefreshSwatches()
        {
            foreach ((Button btn, string hex) in swatchButtons)
            {
                bool active = string.Equals(hex, selectedColor, StringComparison.OrdinalIgnoreCase);
                btn.BorderThickness = new Thickness(active ? 2.5 : 1);
                btn.BorderBrush = active
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            }
        }

        foreach (string hex in state.ProfileAvatarPalette)
        {
            string capturedHex = hex;
            var btn = new Button
            {
                Width = 28,
                Height = 28,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(ParseHexColor(capturedHex))
            };
            AutomationProperties.SetName(btn, $"Avatar color {capturedHex}");
            btn.Click += (_, _) =>
            {
                selectedColor = capturedHex;
                RefreshSwatches();
            };
            swatchButtons.Add((btn, capturedHex));
            swatchPanel.Children.Add(btn);
        }

        RefreshSwatches();

        var errorText = new TextBlock
        {
            Text = "Please enter a profile name.",
            Foreground = new SolidColorBrush(Color.FromArgb(255, 248, 113, 113)),
            FontSize = 12,
            Visibility = Visibility.Collapsed
        };

        var body = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = "Each profile keeps its own typing history, course progress, adaptive key unlocks, and settings.",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13,
                    Opacity = 0.8
                },
                nameBox,
                new TextBlock { Text = "Badge Color", FontSize = 12, FontWeight = FontWeights.SemiBold },
                swatchPanel,
                errorText
            }
        };

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "New Profile",
            Content = body,
            PrimaryButtonText = "Create Profile",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
            }
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text))
        {
            return state.CreateAndSwitchProfile(nameBox.Text, selectedColor);
        }

        return null;
    }

    public static async Task<bool> ShowRenameProfileDialogAsync(
        XamlRoot xamlRoot,
        ITypeForgeStateService state,
        UserProfile profile)
    {
        var nameBox = new TextBox
        {
            Text = profile.Name,
            MaxLength = 32
        };

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Rename Profile",
            Content = nameBox,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                args.Cancel = true;
            }
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text))
        {
            state.RenameProfile(profile.Id, nameBox.Text);
            return true;
        }

        return false;
    }

    public static async Task<bool> ShowResetProgressDialogAsync(XamlRoot xamlRoot, ITypeForgeStateService state)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Reset Progress (Fresh Start)?",
            Content = new TextBlock
            {
                Text = $"This will permanently clear all typing sessions, WPM & accuracy history, key/bigram statistics, course progress, placement results, and streaks for '{state.ActiveProfile.Name}'.\n\nYour app theme, sound, and typing settings will be preserved.",
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "Reset Progress",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            state.ResetCurrentProfileProgress();
            return true;
        }

        return false;
    }

    public static async Task<bool> ShowDeleteProfileDialogAsync(
        XamlRoot xamlRoot,
        ITypeForgeStateService state,
        UserProfile profile)
    {
        if (state.Profiles.Count <= 1)
        {
            return false;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = $"Delete '{profile.Name}'?",
            Content = new TextBlock
            {
                Text = $"This will permanently delete the '{profile.Name}' profile and all of its local typing data. This cannot be undone.",
                TextWrapping = TextWrapping.Wrap
            },
            PrimaryButtonText = "Delete Profile",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            state.DeleteProfile(profile.Id);
            return true;
        }

        return false;
    }
}
