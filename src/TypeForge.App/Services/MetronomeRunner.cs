using Microsoft.UI.Dispatching;
using TypeForge.Core.Engine;
using TypeForge.Core.Models;

namespace TypeForge.App.Services;

/// <summary>
/// Shared UI-thread beat scheduler for the Rhythm Metronome in Practice and Test views.
/// </summary>
internal sealed class MetronomeRunner
{
    private readonly DispatcherQueueTimer _timer;
    private readonly IKeystrokeAudioService _audio;
    private bool _audioEnabled;
    private bool _visualEnabled;
    private Action? _onVisualBeat;

    public MetronomeRunner(DispatcherQueue dispatcherQueue, IKeystrokeAudioService audio)
    {
        _audio = audio ?? throw new ArgumentNullException(nameof(audio));
        _timer = dispatcherQueue.CreateTimer();
        _timer.IsRepeating = true;
        _timer.Tick += OnTick;
    }

    public bool IsRunning => _timer.IsRunning;

    public void Start(
        UserSettings settings,
        double personalAverageNetWpm,
        Action onVisualBeat)
    {
        if (!settings.MetronomeEnabled)
        {
            Stop();
            return;
        }

        int targetWpm = MetronomeCalculator.ResolveTargetWpm(
            settings.MetronomeTargetMode,
            settings.MetronomeTargetWpm,
            personalAverageNetWpm);
        double intervalMs = MetronomeCalculator.GetBeatIntervalMs(targetWpm, settings.MetronomeBeatMode);

        _audioEnabled = settings.MetronomeAudioEnabled;
        _visualEnabled = settings.MetronomeVisualEnabled;
        _onVisualBeat = onVisualBeat;
        _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(50.0, intervalMs));
        if (!_timer.IsRunning)
        {
            _timer.Start();
            OnTick(_timer, null!);
        }
    }

    public void Stop()
    {
        if (_timer.IsRunning)
        {
            _timer.Stop();
        }
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        if (_audioEnabled)
        {
            _audio.PlayMetronomeTick();
        }

        if (_visualEnabled)
        {
            _onVisualBeat?.Invoke();
        }
    }
}
