using System.Runtime.InteropServices;
using TypeForge.Core.Models;

namespace TypeForge.App.Services;

/// <summary>
/// Plays synthesized low-latency offline mechanical click and error alert sounds without external asset files.
/// </summary>
public interface IKeystrokeAudioService : IDisposable
{
    void Configure(SoundFeedbackMode mode, SoundTheme theme, int volumePercent);
    void Preview(SoundTheme theme, int volumePercent);
    void PlayClick();
    void PlayMistake();
    void PlayMetronomeTick();
}

/// <summary>
/// Persistent Win32 waveOut PCM sound player for crisp, low-latency, polyphonic typing feedback.
/// </summary>
public sealed class KeystrokeAudioService : IKeystrokeAudioService
{
    private const uint WaveMapper = 0xFFFFFFFF;
    private const int SampleRate = 22050;
    private const int RingSize = 8;

    private readonly object _lock = new();
    private readonly WaveSlot[] _slots = new WaveSlot[RingSize];
    private IntPtr _hWaveOut;
    private int _nextSlot;

    private byte[] _clickPcm = [];
    private byte[] _mistakePcm = [];
    private byte[] _metronomePcm = [];
    private GCHandle _clickHandle;
    private GCHandle _mistakeHandle;
    private GCHandle _metronomeHandle;

    private SoundFeedbackMode _mode = SoundFeedbackMode.All;
    private SoundTheme _theme = SoundTheme.Default;
    private int _volumePercent = 80;
    private bool _disposed;

    private sealed class WaveSlot
    {
        public IntPtr HeaderPtr;
        public bool IsPrepared;
    }

    public KeystrokeAudioService()
    {
        for (int i = 0; i < RingSize; i++)
        {
            _slots[i] = new WaveSlot
            {
                HeaderPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WaveHdr>())
            };
        }

        RebuildBuffers(_theme, _volumePercent);
        EnsureDeviceOpen();
    }

    public void Configure(SoundFeedbackMode mode, SoundTheme theme, int volumePercent)
    {
        int clampedVolume = Math.Clamp(volumePercent, 0, 100);
        lock (_lock)
        {
            _mode = mode;
            if (_theme != theme || _volumePercent != clampedVolume)
            {
                _theme = theme;
                _volumePercent = clampedVolume;
                RebuildBuffers(theme, clampedVolume);
            }
        }
    }

    public void Preview(SoundTheme theme, int volumePercent)
    {
        int clampedVolume = Math.Clamp(volumePercent, 0, 100);
        byte[] previewPcm = BuildClickForTheme(theme, clampedVolume);
        GCHandle handle = GCHandle.Alloc(previewPcm, GCHandleType.Pinned);
        lock (_lock)
        {
            try
            {
                PlayBufferLocked(handle, previewPcm.Length);
            }
            finally
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
        }
    }

    public void PlayClick()
    {
        lock (_lock)
        {
            if (_mode is not (SoundFeedbackMode.KeysOnly or SoundFeedbackMode.All) || _volumePercent <= 0)
            {
                return;
            }

            PlayBufferLocked(_clickHandle, _clickPcm.Length);
        }
    }

    public void PlayMistake()
    {
        lock (_lock)
        {
            if (_mode is not (SoundFeedbackMode.ErrorsOnly or SoundFeedbackMode.All) || _volumePercent <= 0)
            {
                return;
            }

            PlayBufferLocked(_mistakeHandle, _mistakePcm.Length);
        }
    }

    public void PlayMetronomeTick()
    {
        lock (_lock)
        {
            if (_mode == SoundFeedbackMode.None || _volumePercent <= 0)
            {
                return;
            }

            PlayBufferLocked(_metronomeHandle, _metronomePcm.Length);
        }
    }

    private void EnsureDeviceOpen()
    {
        if (_disposed || _hWaveOut != IntPtr.Zero)
        {
            return;
        }

        var format = new WaveFormatEx
        {
            wFormatTag = 1, // WAVE_FORMAT_PCM
            nChannels = 1,
            nSamplesPerSec = SampleRate,
            nAvgBytesPerSec = SampleRate * sizeof(short),
            nBlockAlign = sizeof(short),
            wBitsPerSample = 16,
            cbSize = 0
        };

        try
        {
            int result = NativeMethods.waveOutOpen(out _hWaveOut, WaveMapper, ref format, IntPtr.Zero, IntPtr.Zero, 0);
            if (result != 0)
            {
                _hWaveOut = IntPtr.Zero;
            }
        }
        catch
        {
            _hWaveOut = IntPtr.Zero;
        }
    }

    private void PlayBufferLocked(GCHandle pcmHandle, int byteLength)
    {
        if (_disposed || !pcmHandle.IsAllocated || byteLength <= 0)
        {
            return;
        }

        EnsureDeviceOpen();
        if (_hWaveOut == IntPtr.Zero)
        {
            return;
        }

        try
        {
            WaveSlot slot = _slots[_nextSlot];
            _nextSlot = (_nextSlot + 1) % RingSize;

            int hdrSize = Marshal.SizeOf<WaveHdr>();
            if (slot.IsPrepared)
            {
                _ = NativeMethods.waveOutUnprepareHeader(_hWaveOut, slot.HeaderPtr, hdrSize);
                slot.IsPrepared = false;
            }

            var hdr = new WaveHdr
            {
                lpData = pcmHandle.AddrOfPinnedObject(),
                dwBufferLength = (uint)byteLength,
                dwBytesRecorded = 0,
                dwUser = IntPtr.Zero,
                dwFlags = 0,
                dwLoops = 0,
                lpNext = IntPtr.Zero,
                reserved = IntPtr.Zero
            };

            Marshal.StructureToPtr(hdr, slot.HeaderPtr, fDeleteOld: false);

            if (NativeMethods.waveOutPrepareHeader(_hWaveOut, slot.HeaderPtr, hdrSize) == 0)
            {
                slot.IsPrepared = true;
                _ = NativeMethods.waveOutWrite(_hWaveOut, slot.HeaderPtr, hdrSize);
            }
        }
        catch
        {
            // Ignore audio device absence or driver errors on headless runners.
        }
    }

    private void RebuildBuffers(SoundTheme theme, int volumePercent)
    {
        if (_disposed)
        {
            return;
        }

        if (_hWaveOut != IntPtr.Zero)
        {
            try
            {
                _ = NativeMethods.waveOutReset(_hWaveOut);
                int hdrSize = Marshal.SizeOf<WaveHdr>();
                foreach (WaveSlot slot in _slots)
                {
                    if (slot.IsPrepared)
                    {
                        _ = NativeMethods.waveOutUnprepareHeader(_hWaveOut, slot.HeaderPtr, hdrSize);
                        slot.IsPrepared = false;
                    }
                }
            }
            catch
            {
                // Ignore reset failures during buffer rebuild.
            }
        }

        FreeHandles();
        _clickPcm = BuildClickForTheme(theme, volumePercent);
        _mistakePcm = BuildMistakeForTheme(theme, volumePercent);
        double scale = Math.Clamp(volumePercent / 100.0, 0.0, 1.0);
        _metronomePcm = BuildPcmSamples(24, 1520, 620, 0.38 * scale, isClick: true);
        _clickHandle = GCHandle.Alloc(_clickPcm, GCHandleType.Pinned);
        _mistakeHandle = GCHandle.Alloc(_mistakePcm, GCHandleType.Pinned);
        _metronomeHandle = GCHandle.Alloc(_metronomePcm, GCHandleType.Pinned);
    }

    private static byte[] BuildClickForTheme(SoundTheme theme, int volumePercent)
    {
        double scale = Math.Clamp(volumePercent / 100.0, 0.0, 1.0);
        return theme switch
        {
            SoundTheme.Mechanical => BuildPcmSamples(38, 920, 260, 0.65 * scale, isClick: true),
            SoundTheme.Typewriter => BuildPcmSamples(42, 1750, 520, 0.68 * scale, isClick: true),
            SoundTheme.Soft => BuildPcmSamples(32, 680, 290, 0.42 * scale, isClick: true),
            _ => BuildPcmSamples(36, 1250, 380, 0.60 * scale, isClick: true)
        };
    }

    private static byte[] BuildMistakeForTheme(SoundTheme theme, int volumePercent)
    {
        double scale = Math.Clamp(volumePercent / 100.0, 0.0, 1.0);
        return theme switch
        {
            SoundTheme.Soft => BuildPcmSamples(70, 210, 145, 0.48 * scale, isClick: false),
            _ => BuildPcmSamples(80, 230, 140, 0.65 * scale, isClick: false)
        };
    }

    private static byte[] BuildPcmSamples(
        int durationMs,
        double baseFrequencyHz,
        double sweepEndHz,
        double amplitude,
        bool isClick)
    {
        int sampleCount = Math.Max(1, (SampleRate * durationMs) / 1000);
        byte[] pcm = new byte[sampleCount * sizeof(short)];

        double phase = 0.0;
        for (int i = 0; i < sampleCount; i++)
        {
            double progress = (double)i / sampleCount;
            double freq = baseFrequencyHz + ((sweepEndHz - baseFrequencyHz) * progress);
            phase += (2.0 * Math.PI * freq) / SampleRate;

            double attack = Math.Min(1.0, i / 24.0);
            double envelope = isClick
                ? attack * Math.Exp(-4.5 * progress)
                : attack * Math.Sin(Math.PI * progress) * Math.Exp(-1.6 * progress);

            double wave = isClick
                ? Math.Sin(phase) + (0.4 * Math.Sin(phase * 2.3))
                : Math.Sin(phase) + (0.3 * Math.Sin(phase * 1.5));

            double normalized = Math.Clamp(wave * amplitude * envelope, -1.0, 1.0);
            short sample = (short)(normalized * short.MaxValue);
            pcm[i * 2] = (byte)(sample & 0xFF);
            pcm[(i * 2) + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return pcm;
    }

    private void FreeHandles()
    {
        if (_clickHandle.IsAllocated)
        {
            _clickHandle.Free();
        }

        if (_mistakeHandle.IsAllocated)
        {
            _mistakeHandle.Free();
        }

        if (_metronomeHandle.IsAllocated)
        {
            _metronomeHandle.Free();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_hWaveOut != IntPtr.Zero)
            {
                try
                {
                    _ = NativeMethods.waveOutReset(_hWaveOut);
                    int hdrSize = Marshal.SizeOf<WaveHdr>();
                    foreach (WaveSlot slot in _slots)
                    {
                        if (slot.IsPrepared)
                        {
                            _ = NativeMethods.waveOutUnprepareHeader(_hWaveOut, slot.HeaderPtr, hdrSize);
                            slot.IsPrepared = false;
                        }
                    }

                    _ = NativeMethods.waveOutClose(_hWaveOut);
                }
                catch
                {
                    // Ignore shutdown audio errors.
                }

                _hWaveOut = IntPtr.Zero;
            }

            foreach (WaveSlot slot in _slots)
            {
                if (slot.HeaderPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(slot.HeaderPtr);
                    slot.HeaderPtr = IntPtr.Zero;
                }
            }

            FreeHandles();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormatEx
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHdr
    {
        public IntPtr lpData;
        public uint dwBufferLength;
        public uint dwBytesRecorded;
        public IntPtr dwUser;
        public uint dwFlags;
        public uint dwLoops;
        public IntPtr lpNext;
        public IntPtr reserved;
    }

    private static class NativeMethods
    {
        [DllImport("winmm.dll")]
        internal static extern int waveOutOpen(
            out IntPtr phwo,
            uint uDeviceID,
            ref WaveFormatEx pwfx,
            IntPtr dwCallback,
            IntPtr dwInstance,
            uint fdwOpen);

        [DllImport("winmm.dll")]
        internal static extern int waveOutPrepareHeader(IntPtr hwo, IntPtr pwh, int cbwh);

        [DllImport("winmm.dll")]
        internal static extern int waveOutUnprepareHeader(IntPtr hwo, IntPtr pwh, int cbwh);

        [DllImport("winmm.dll")]
        internal static extern int waveOutWrite(IntPtr hwo, IntPtr pwh, int cbwh);

        [DllImport("winmm.dll")]
        internal static extern int waveOutReset(IntPtr hwo);

        [DllImport("winmm.dll")]
        internal static extern int waveOutClose(IntPtr hwo);
    }
}
