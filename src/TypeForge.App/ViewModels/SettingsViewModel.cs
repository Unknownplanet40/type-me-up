using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TypeForge.App.Services;
using TypeForge.Core.Models;
using Windows.System;

namespace TypeForge.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ITypeForgeStateService _state;
    private readonly INavigationService _navigation;
    private readonly IUpdateService _updateService;

    [ObservableProperty]
    private string _updateStatusMessage = "Click Check for Updates to check GitHub Releases and What's New.";

    [ObservableProperty]
    private string _releaseNotesText = string.Empty;

    [ObservableProperty]
    private bool _hasReleaseNotes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCheckUpdates))]
    private bool _isCheckingUpdates;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCheckUpdates))]
    private bool _isDownloadingUpdate;

    public bool CanCheckUpdates => !IsCheckingUpdates && !IsDownloadingUpdate;

    [ObservableProperty]
    private int _downloadProgressPercent;

    [ObservableProperty]
    private bool _canInstallUpdate;

    [ObservableProperty]
    private string _installActionText = "Download & Install";

    public string CurrentVersionDisplay => $"TypeForge v{_updateService.CurrentVersion}";
    public string InstallModeDisplay => _updateService.IsInstalledViaInstaller
        ? "Installed app (1-click delta updates enabled)"
        : "Portable / development build";

    public int ThemeIndex
    {
        get => _state.Settings.Theme switch
        {
            ThemePreference.Light => 0,
            ThemePreference.Dark => 1,
            _ => 2
        };
        set
        {
            ThemePreference next = value switch
            {
                0 => ThemePreference.Light,
                1 => ThemePreference.Dark,
                _ => ThemePreference.System
            };
            if (_state.Settings.Theme != next)
            {
                _state.Settings.Theme = next;
                _navigation.ApplyTheme(next);
                _state.SaveSettings();
                OnPropertyChanged();
            }
        }
    }

    public int TypingFontSize
    {
        get => _state.Settings.TypingFontSize;
        set
        {
            int clamped = Math.Clamp(value, UserSettings.MinFontSize, UserSettings.MaxFontSize);
            if (_state.Settings.TypingFontSize != clamped)
            {
                _state.Settings.TypingFontSize = clamped;
                _state.SaveSettings();
                OnPropertyChanged();
            }
        }
    }

    public int KeyboardColorModeIndex
    {
        get => _state.Settings.KeyboardColorMode == KeyboardColorMode.FingerZones ? 1 : 0;
        set
        {
            KeyboardColorMode next = value == 1 ? KeyboardColorMode.FingerZones : KeyboardColorMode.PerformanceHeat;
            if (_state.Settings.KeyboardColorMode != next)
            {
                _state.Settings.KeyboardColorMode = next;
                _state.SaveSettings();
                OnPropertyChanged();
            }
        }
    }

    public bool ShowVirtualKeyboard
    {
        get => _state.Settings.ShowVirtualKeyboard;
        set
        {
            if (_state.Settings.ShowVirtualKeyboard != value)
            {
                _state.Settings.ShowVirtualKeyboard = value;
                _state.SaveSettings();
                OnPropertyChanged();
            }
        }
    }

    public int DailyGoalIndex
    {
        get => _state.Settings.DailyGoalMinutes switch
        {
            10 => 0,
            15 => 1,
            30 => 2,
            45 => 3,
            60 => 4,
            _ => 2
        };
        set
        {
            int mins = value switch { 0 => 10, 1 => 15, 3 => 45, 4 => 60, _ => 30 };
            if (_state.Settings.DailyGoalMinutes != mins)
            {
                _state.Settings.DailyGoalMinutes = mins;
                _state.SaveSettings();
                OnPropertyChanged();
            }
        }
    }

    public int AdaptiveWordModeIndex
    {
        get => _state.Settings.AdaptiveWordMode == AdaptiveWordMode.NaturalDictionaryWords ? 1 : 0;
        set
        {
            AdaptiveWordMode next = value == 1
                ? AdaptiveWordMode.NaturalDictionaryWords
                : AdaptiveWordMode.PhoneticPseudoWords;
            if (_state.Settings.AdaptiveWordMode != next)
            {
                _state.Settings.AdaptiveWordMode = next;
                _state.SaveSettings();
                if (_state.PendingSessionType == SessionType.AdaptivePractice)
                {
                    _state.QueuePracticeSession(
                        "Adaptive Practice · Keybr Focus",
                        _state.BuildAdaptivePracticeText(),
                        SessionType.AdaptivePractice);
                }

                OnPropertyChanged();
            }
        }
    }

    public int AdaptiveProfileIndex
    {
        get => (int)_state.Settings.AdaptiveProfile;
        set
        {
            var next = (AdaptiveDifficultyProfile)Math.Clamp(value, 0, 2);
            if (_state.Settings.AdaptiveProfile != next)
            {
                _state.Settings.AdaptiveProfile = next;
                _state.SaveSettings();
                OnPropertyChanged();
            }
        }
    }

    public bool SoundEnabled
    {
        get => _state.Settings.SoundEnabled;
        set
        {
            if (_state.Settings.SoundEnabled != value)
            {
                _state.Settings.SoundEnabled = value;
                _state.SaveSettings();
                OnPropertyChanged();
            }
        }
    }

    public bool StopOnError
    {
        get => _state.Settings.StopOnError;
        set
        {
            if (_state.Settings.StopOnError != value)
            {
                _state.Settings.StopOnError = value;
                _state.SaveSettings();
                OnPropertyChanged();
            }
        }
    }

    public bool OptimalDurationEnabled
    {
        get => _state.Settings.OptimalDurationEnabled;
        set
        {
            if (_state.Settings.OptimalDurationEnabled != value)
            {
                _state.Settings.OptimalDurationEnabled = value;
                _state.SaveSettings();
                OnPropertyChanged();
            }
        }
    }

    public SettingsViewModel(
        ITypeForgeStateService state,
        INavigationService navigation,
        IUpdateService updateService)
    {
        _state = state;
        _navigation = navigation;
        _updateService = updateService;

        if (_updateService.LastOutcome is { } cached)
        {
            ApplyOutcome(cached);
        }
    }

    private void ApplyOutcome(AppUpdateCheckOutcome outcome)
    {
        UpdateStatusMessage = outcome.Message;

        if (!string.IsNullOrWhiteSpace(outcome.ReleaseNotes))
        {
            ReleaseNotesText = outcome.ReleaseNotes;
            HasReleaseNotes = true;
        }
        else
        {
            ReleaseNotesText = string.Empty;
            HasReleaseNotes = false;
        }

        if (outcome.Status == UpdateCheckStatus.UpdateAvailable)
        {
            InstallActionText = $"Install v{outcome.AvailableVersion}";
            CanInstallUpdate = true;
        }
        else if (outcome.Status == UpdateCheckStatus.InstallerUpdateAvailable)
        {
            InstallActionText = $"Get Setup v{outcome.AvailableVersion}";
            CanInstallUpdate = true;
        }
        else
        {
            CanInstallUpdate = false;
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingUpdates || IsDownloadingUpdate)
        {
            return;
        }

        IsCheckingUpdates = true;
        CanInstallUpdate = false;
        UpdateStatusMessage = "Checking GitHub Releases...";

        try
        {
            AppUpdateCheckOutcome outcome = await _updateService.CheckForUpdatesAsync();
            ApplyOutcome(outcome);
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (IsDownloadingUpdate || IsCheckingUpdates)
        {
            return;
        }

        if (!_updateService.IsInstalledViaInstaller || !_updateService.HasDownloadableUpdate)
        {
            _ = await Launcher.LaunchUriAsync(new Uri(_updateService.ReleasePageUrl));
            return;
        }

        IsDownloadingUpdate = true;
        DownloadProgressPercent = 0;
        UpdateStatusMessage = "Downloading update package...";

        try
        {
            IProgress<int> progress = new Progress<int>(p => DownloadProgressPercent = Math.Clamp(p, 0, 100));
            await _updateService.DownloadAndApplyUpdateAsync(percent => progress.Report(percent));
            UpdateStatusMessage = "Applying update and restarting...";
        }
        catch (Exception ex)
        {
            UpdateStatusMessage = $"Update failed: {ex.Message}";
            IsDownloadingUpdate = false;
        }
    }
}
