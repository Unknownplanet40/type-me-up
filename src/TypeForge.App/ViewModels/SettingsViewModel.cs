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

    public string CurrentVersionDisplay => $"TypeForge v{_updateService.CurrentVersionString}";
    public string InstallModeDisplay => _updateService.IsInstalledCopy
        ? "Installed via TypeForge Setup (silent in-app updates enabled)"
        : "Development / portable build (in-app Setup updater ready)";

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
        SyncUpdateState();
    }

    public void Attach()
    {
        _updateService.StateChanged += OnUpdateStateChanged;
        _updateService.DownloadProgressChanged += OnDownloadProgressChanged;
        SyncUpdateState();
    }

    public void Detach()
    {
        _updateService.StateChanged -= OnUpdateStateChanged;
        _updateService.DownloadProgressChanged -= OnDownloadProgressChanged;
    }

    private void OnUpdateStateChanged(UpdateState state, string message)
    {
        SyncUpdateState();
    }

    private void OnDownloadProgressChanged(double progress)
    {
        DownloadProgressPercent = (int)Math.Round(Math.Clamp(progress * 100.0, 0, 100));
    }

    private void SyncUpdateState()
    {
        UpdateState state = _updateService.State;
        ReleaseInfo? release = _updateService.LatestRelease;

        UpdateStatusMessage = _updateService.StatusMessage;
        IsCheckingUpdates = state == UpdateState.Checking;
        IsDownloadingUpdate = state is UpdateState.Downloading or UpdateState.Downloaded;
        DownloadProgressPercent = (int)Math.Round(Math.Clamp(_updateService.DownloadProgress * 100.0, 0, 100));

        if (release is not null && !string.IsNullOrWhiteSpace(release.Changelog))
        {
            ReleaseNotesText = release.Changelog;
            HasReleaseNotes = true;
        }
        else
        {
            ReleaseNotesText = string.Empty;
            HasReleaseNotes = false;
        }

        if (state == UpdateState.UpdateAvailable && release is not null)
        {
            InstallActionText = string.IsNullOrWhiteSpace(release.DownloadUrl)
                ? $"Open {release.TagName}"
                : $"Download & Install {release.TagName}";
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

        await _updateService.CheckForUpdatesAsync(silentBackground: false);
        SyncUpdateState();
    }

    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (IsDownloadingUpdate || IsCheckingUpdates)
        {
            return;
        }

        if (_updateService.LatestRelease is null || string.IsNullOrWhiteSpace(_updateService.LatestRelease.DownloadUrl))
        {
            _ = await Launcher.LaunchUriAsync(new Uri(_updateService.ReleasePageUrl));
            return;
        }

        CanInstallUpdate = false;
        await _updateService.DownloadAndInstallAsync();
        SyncUpdateState();
    }
}
