using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using TypeForge.App.Services;
using TypeForge.Core.Engine;
using TypeForge.Core.Models;

namespace TypeForge.App.Controls;

/// <summary>
/// Shared Typing Options, Text Appearance (with live animated preview), and Sound settings panel used by Test and Practice dialogs.
/// </summary>
public sealed partial class TypingSettingsControl : UserControl
{
    private const string PreviewSampleText = "Eat more apples and oranges.";

    private static readonly string[] SoundThemeNames = ["Default", "Mechanical", "Typewriter", "Soft"];

    private readonly TypingEngine _previewEngine = new(PreviewSampleText, stopOnError: false);
    private readonly DispatcherTimer _previewTimer;
    private bool _suppressEvents;

    public TypingSettingsControl()
    {
        _suppressEvents = true;
        InitializeComponent();
        PopulateStaticComboBoxes();
        _suppressEvents = false;

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
        _previewTimer.Tick += (_, _) => StepPreview();

        Unloaded += (_, _) => StopPreview();
    }

    public void LoadFromSettings(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _suppressEvents = true;
        try
        {
            ChkStopOnError.IsChecked = settings.StopOnError;
            ChkForgiveErrors.IsChecked = settings.ForgiveErrors;
            ChkSpaceSkipsWords.IsChecked = settings.SpaceSkipsWords;

            RadioPaceOff.IsChecked = settings.PaceCaretMode == PaceCaretMode.Off;
            RadioPaceFixed.IsChecked = settings.PaceCaretMode == PaceCaretMode.FixedWpm;
            RadioPacePersonalBest.IsChecked = settings.PaceCaretMode == PaceCaretMode.PersonalBest;
            RadioPaceLastSession.IsChecked = settings.PaceCaretMode == PaceCaretMode.LastSession;
            PaceFixedWpmRow.Visibility = settings.PaceCaretMode == PaceCaretMode.FixedWpm ? Visibility.Visible : Visibility.Collapsed;

            int paceWpm = Math.Clamp(settings.PaceTargetWpm, UserSettings.MinPaceTargetWpm, UserSettings.MaxPaceTargetWpm);
            SliderPaceTargetWpm.Value = paceWpm;
            TxtPaceTargetWpm.Text = $"{paceWpm} WPM";

            int fontIdx = 0;
            for (int i = 0; i < TypingStreamView.AvailableFonts.Count; i++)
            {
                if (string.Equals(TypingStreamView.AvailableFonts[i], settings.TypingFontSelection, StringComparison.OrdinalIgnoreCase))
                {
                    fontIdx = i;
                    break;
                }
            }

            ComboFont.SelectedIndex = fontIdx;
            int fontSize = Math.Clamp(settings.TypingFontSize, UserSettings.MinFontSize, UserSettings.MaxFontSize);
            SliderFontSize.Value = fontSize;
            TxtFontSize.Text = fontSize.ToString();

            RadioWsNone.IsChecked = settings.WhitespaceDisplay == WhitespaceDisplayMode.None;
            RadioWsBar.IsChecked = settings.WhitespaceDisplay == WhitespaceDisplayMode.Bar;
            RadioWsBullet.IsChecked = settings.WhitespaceDisplay == WhitespaceDisplayMode.Bullet;

            RadioCurBlock.IsChecked = settings.CursorShape == CursorShapeMode.Block;
            RadioCurBox.IsChecked = settings.CursorShape == CursorShapeMode.Box;
            RadioCurLine.IsChecked = settings.CursorShape == CursorShapeMode.Line;
            RadioCurUnderline.IsChecked = settings.CursorShape == CursorShapeMode.Underline;

            RadioMoveJumping.IsChecked = settings.CursorMovement == CursorMovementMode.Jumping;
            RadioMoveSmooth.IsChecked = settings.CursorMovement == CursorMovementMode.Smooth;

            SoundFeedbackMode effectiveMode = settings.SoundEnabled ? settings.SoundMode : SoundFeedbackMode.None;
            RadioSoundNone.IsChecked = effectiveMode == SoundFeedbackMode.None;
            RadioSoundErrors.IsChecked = effectiveMode == SoundFeedbackMode.ErrorsOnly;
            RadioSoundKeys.IsChecked = effectiveMode == SoundFeedbackMode.KeysOnly;
            RadioSoundAll.IsChecked = effectiveMode == SoundFeedbackMode.All;

            int volume = Math.Clamp(settings.SoundVolume, 0, 100);
            SliderSoundVolume.Value = volume;
            TxtSoundVolume.Text = volume.ToString();
            ComboSoundTheme.SelectedIndex = Math.Clamp((int)settings.SoundTheme, 0, SoundThemeNames.Length - 1);
        }
        finally
        {
            _suppressEvents = false;
        }

        _previewEngine.Reset();
        ApplyPreviewAppearance();
    }

    public void ApplyToSettings(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        settings.StopOnError = ChkStopOnError.IsChecked == true;
        settings.ForgiveErrors = ChkForgiveErrors.IsChecked == true;
        settings.SpaceSkipsWords = ChkSpaceSkipsWords.IsChecked == true;
        settings.PaceCaretMode = GetSelectedPaceCaretMode();
        settings.PaceTargetWpm = Math.Clamp((int)Math.Round(SliderPaceTargetWpm.Value), UserSettings.MinPaceTargetWpm, UserSettings.MaxPaceTargetWpm);

        int fontIdx = Math.Clamp(ComboFont.SelectedIndex, 0, TypingStreamView.AvailableFonts.Count - 1);
        settings.TypingFontSelection = TypingStreamView.AvailableFonts[fontIdx];
        settings.TypingFontSize = Math.Clamp((int)Math.Round(SliderFontSize.Value), UserSettings.MinFontSize, UserSettings.MaxFontSize);

        settings.WhitespaceDisplay = GetSelectedWhitespace();
        settings.CursorShape = GetSelectedCursorShape();
        settings.CursorMovement = GetSelectedCursorMovement();

        SoundFeedbackMode soundMode = GetSelectedSoundMode();
        settings.SoundMode = soundMode;
        settings.SoundEnabled = soundMode != SoundFeedbackMode.None;
        settings.SoundVolume = Math.Clamp((int)Math.Round(SliderSoundVolume.Value), 0, 100);
        settings.SoundTheme = (SoundTheme)Math.Clamp(ComboSoundTheme.SelectedIndex, 0, SoundThemeNames.Length - 1);
    }

    public void StartPreview()
    {
        ApplyPreviewAppearance();
        if (!_previewTimer.IsEnabled)
        {
            _previewTimer.Start();
        }
    }

    public void StopPreview()
    {
        if (_previewTimer.IsEnabled)
        {
            _previewTimer.Stop();
        }
    }

    private void PopulateStaticComboBoxes()
    {
        ComboFont.Items.Clear();
        foreach (string font in TypingStreamView.AvailableFonts)
        {
            ComboFont.Items.Add(new ComboBoxItem { Content = font });
        }

        ComboSoundTheme.Items.Clear();
        foreach (string theme in SoundThemeNames)
        {
            ComboSoundTheme.Items.Add(new ComboBoxItem { Content = theme });
        }
    }

    private void StepPreview()
    {
        TypingSnapshot snap = _previewEngine.GetSnapshot();
        if (snap.IsCompleted || snap.CursorIndex >= PreviewSampleText.Length)
        {
            _previewEngine.Reset();
        }
        else
        {
            _previewEngine.ProcessKeystroke(PreviewSampleText[snap.CursorIndex]);
        }

        PreviewStreamView.RenderSnapshot(_previewEngine.GetSnapshot());
    }

    private void ApplyPreviewAppearance()
    {
        int fontIdx = Math.Clamp(ComboFont.SelectedIndex, 0, TypingStreamView.AvailableFonts.Count - 1);
        string fontSelection = TypingStreamView.AvailableFonts[fontIdx];
        int fontSize = Math.Clamp((int)Math.Round(SliderFontSize.Value), UserSettings.MinFontSize, UserSettings.MaxFontSize);

        PreviewStreamView.UpdateAppearance(
            fontSelection,
            Math.Min(fontSize, 24),
            GetSelectedWhitespace(),
            GetSelectedCursorShape(),
            GetSelectedCursorMovement());

        PreviewStreamView.RenderSnapshot(_previewEngine.GetSnapshot());
    }

    private WhitespaceDisplayMode GetSelectedWhitespace()
    {
        if (RadioWsNone.IsChecked == true)
        {
            return WhitespaceDisplayMode.None;
        }

        if (RadioWsBar.IsChecked == true)
        {
            return WhitespaceDisplayMode.Bar;
        }

        return WhitespaceDisplayMode.Bullet;
    }

    private CursorShapeMode GetSelectedCursorShape()
    {
        if (RadioCurBlock.IsChecked == true)
        {
            return CursorShapeMode.Block;
        }

        if (RadioCurBox.IsChecked == true)
        {
            return CursorShapeMode.Box;
        }

        if (RadioCurLine.IsChecked == true)
        {
            return CursorShapeMode.Line;
        }

        return CursorShapeMode.Underline;
    }

    private CursorMovementMode GetSelectedCursorMovement() =>
        RadioMoveJumping.IsChecked == true ? CursorMovementMode.Jumping : CursorMovementMode.Smooth;

    private SoundFeedbackMode GetSelectedSoundMode()
    {
        if (RadioSoundNone.IsChecked == true)
        {
            return SoundFeedbackMode.None;
        }

        if (RadioSoundErrors.IsChecked == true)
        {
            return SoundFeedbackMode.ErrorsOnly;
        }

        if (RadioSoundKeys.IsChecked == true)
        {
            return SoundFeedbackMode.KeysOnly;
        }

        return SoundFeedbackMode.All;
    }

    private void AppearanceCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_suppressEvents)
        {
            ApplyPreviewAppearance();
        }
    }

    private void FontSizeSlider_Changed(object sender, RangeBaseValueChangedEventArgs e)
    {
        int size = Math.Clamp((int)Math.Round(e.NewValue), UserSettings.MinFontSize, UserSettings.MaxFontSize);
        if (TxtFontSize is not null)
        {
            TxtFontSize.Text = size.ToString();
        }

        if (!_suppressEvents && PreviewStreamView is not null)
        {
            ApplyPreviewAppearance();
        }
    }

    private void AppearanceRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (!_suppressEvents && PreviewStreamView is not null)
        {
            ApplyPreviewAppearance();
        }
    }

    private void VolumeSlider_Changed(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (TxtSoundVolume is not null)
        {
            TxtSoundVolume.Text = Math.Clamp((int)Math.Round(e.NewValue), 0, 100).ToString();
        }
    }

    private void BtnPreviewSound_Click(object sender, RoutedEventArgs e)
    {
        var theme = (SoundTheme)Math.Clamp(ComboSoundTheme.SelectedIndex, 0, SoundThemeNames.Length - 1);
        int volume = Math.Clamp((int)Math.Round(SliderSoundVolume.Value), 0, 100);
        App.GetService<IKeystrokeAudioService>().Preview(theme, volume);
    }

    private PaceCaretMode GetSelectedPaceCaretMode()
    {
        if (RadioPaceFixed.IsChecked == true)
        {
            return PaceCaretMode.FixedWpm;
        }

        if (RadioPacePersonalBest.IsChecked == true)
        {
            return PaceCaretMode.PersonalBest;
        }

        if (RadioPaceLastSession.IsChecked == true)
        {
            return PaceCaretMode.LastSession;
        }

        return PaceCaretMode.Off;
    }

    private void PaceModeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (PaceFixedWpmRow is not null && RadioPaceFixed is not null)
        {
            PaceFixedWpmRow.Visibility = RadioPaceFixed.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void PaceWpmSlider_Changed(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (TxtPaceTargetWpm is not null)
        {
            int wpm = Math.Clamp((int)Math.Round(e.NewValue), UserSettings.MinPaceTargetWpm, UserSettings.MaxPaceTargetWpm);
            TxtPaceTargetWpm.Text = $"{wpm} WPM";
        }
    }
}
