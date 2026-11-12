using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TypeForge.App.Services;
using TypeForge.App.ViewModels;
using TypeForge.Core.Models;

namespace TypeForge.App.Views;

public sealed partial class SettingsPage : Page
{
    private readonly ITypeForgeStateService _state;
    private bool _isUpdatingProfileCombo;

    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        _state = App.GetService<ITypeForgeStateService>();
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ViewModel.Attach();
            _state.StateChanged += OnStateChanged;
            RefreshProfileSection();
        };
        Unloaded += (_, _) =>
        {
            _state.StateChanged -= OnStateChanged;
            ViewModel.Detach();
        };
    }

    private void OnStateChanged()
    {
        DispatcherQueue.TryEnqueue(RefreshProfileSection);
    }

    private void RefreshProfileSection()
    {
        _isUpdatingProfileCombo = true;
        try
        {
            UserProfile active = _state.ActiveProfile;
            SettingsProfileName.Text = active.Name;
            SettingsProfileInitials.Text = active.Initials;
            SettingsProfileAvatar.Background = new SolidColorBrush(ProfileDialogs.ParseHexColor(active.AvatarColorHex));

            int count = _state.Profiles.Count;
            SettingsProfileMeta.Text = count == 1 ? "1 profile on this device" : $"{count} profiles on this device";
            BtnDeleteProfile.IsEnabled = count > 1;

            ProfileSwitcherCombo.Items.Clear();
            int selectedIndex = 0;
            for (int i = 0; i < _state.Profiles.Count; i++)
            {
                UserProfile p = _state.Profiles[i];
                ProfileSwitcherCombo.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p.Id });
                if (string.Equals(p.Id, active.Id, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                }
            }

            if (ProfileSwitcherCombo.Items.Count > 0)
            {
                ProfileSwitcherCombo.SelectedIndex = selectedIndex;
            }
        }
        finally
        {
            _isUpdatingProfileCombo = false;
        }
    }

    private void ProfileSwitcherCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingProfileCombo || ProfileSwitcherCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string profileId)
        {
            return;
        }

        if (!string.Equals(profileId, _state.ActiveProfile.Id, StringComparison.OrdinalIgnoreCase))
        {
            _state.SwitchProfile(profileId);
        }
    }

    private async void BtnNewProfile_Click(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is not null)
        {
            await ProfileDialogs.ShowCreateProfileDialogAsync(XamlRoot, _state);
        }
    }

    private async void BtnRenameProfile_Click(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is not null)
        {
            await ProfileDialogs.ShowRenameProfileDialogAsync(XamlRoot, _state, _state.ActiveProfile);
        }
    }

    private async void BtnResetProgress_Click(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is not null)
        {
            await ProfileDialogs.ShowResetProgressDialogAsync(XamlRoot, _state);
        }
    }

    private async void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is not null)
        {
            await ProfileDialogs.ShowDeleteProfileDialogAsync(XamlRoot, _state, _state.ActiveProfile);
        }
    }
}
