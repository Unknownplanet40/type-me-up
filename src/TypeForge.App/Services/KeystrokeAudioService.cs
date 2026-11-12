using System.Buffers.Binary;
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
}

/// <summary>
/// Win32 PCM memory sound player for crisp tactile typing feedback.
/// </summary>
public sealed class KeystrokeAudioService : IKeystrokeAudioService
{
    private const uint SndMemory = 0x0004;
    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint Flags = SndMemory | SndAsync | SndNoDefault;

    private readonly object _lock = new();
    private byte[] _clickWav = [];
    private byte[] _mistakeWav = [];
    private GCHandle _clickHandle;
    private GCHandle _mistakeHandle;
    private SoundFeedbackMode _mode = SoundFeedbackMode.All;
    private SoundTheme _theme = SoundTheme.Default;
    private int _volumePercent = 80;
    private bool _disposed;

    public KeystrokeAudioService()
    {
        RebuildBuffers(_theme, _volumePercent);
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
        byte[] previewWav = BuildClickForTheme(theme, clampedVolume);
        GCHandle handle = GCHandle.Alloc(previewWav, GCHandleType.Pinned);
        try
        {
            _ = NativeMethods.PlaySoundW(handle.AddrOfPinnedObject(), IntPtr.Zero, Flags & ~SndAsync);
        }
        catch
        {
            // Ignore audio device absence on headless runners.
        }
        finally
        {
            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }
    }

    public void PlayClick()
    {
        if (_mode is not (SoundFeedbackMode.KeysOnly or SoundFeedbackMode.All) || _volumePercent <= 0)
        {
            return;
        }

        PlayPinnedBuffer(_clickHandle);
    }

    public void PlayMistake()
    {
        if (_mode is not (SoundFeedbackMode.ErrorsOnly or SoundFeedbackMode.All) || _volumePercent <= 0)
        {
            return;
        }

        PlayPinnedBuffer(_mistakeHandle);
    }

    private void RebuildBuffers(SoundTheme theme, int volumePercent)
    {
        if (_disposed)
        {
            return;
        }

        FreeHandles();
        _clickWav = BuildClickForTheme(theme, volumePercent);
        _mistakeWav = BuildMistakeForTheme(theme, volumePercent);
        _clickHandle = GCHandle.Alloc(_clickWav, GCHandleType.Pinned);
        _mistakeHandle = GCHandle.Alloc(_mistakeWav, GCHandleType.Pinned);
    }

    private static byte[] BuildClickForTheme(SoundTheme theme, int volumePercent)
    {
        double scale = Math.Clamp(volumePercent / 100.0, 0.0, 1.0);
        return theme switch
        {
            SoundTheme.Mechanical => BuildPcmWav(22050, 22, 820, 240, 0.34 * scale, isClick: true),
            SoundTheme.Typewriter => BuildPcmWav(22050, 26, 1650, 580, 0.36 * scale, isClick: true),
            SoundTheme.Soft => BuildPcmWav(22050, 16, 680, 320, 0.16 * scale, isClick: true),
            _ => BuildPcmWav(22050, 18, 1150, 420, 0.28 * scale, isClick: true)
        };
    }

    private static byte[] BuildMistakeForTheme(SoundTheme theme, int volumePercent)
    {
        double scale = Math.Clamp(volumePercent / 100.0, 0.0, 1.0);
        return theme switch
        {
            SoundTheme.Soft => BuildPcmWav(22050, 45, 190, 145, 0.22 * scale, isClick: false),
            _ => BuildPcmWav(22050, 55, 210, 150, 0.32 * scale, isClick: false)
        };
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
    }

    private void PlayPinnedBuffer(GCHandle handle)
    {
        if (_disposed || !handle.IsAllocated)
        {
            return;
        }

        try
        {
            _ = NativeMethods.PlaySoundW(handle.AddrOfPinnedObject(), IntPtr.Zero, Flags);
        }
        catch
        {
            // Ignore audio device absence on headless runners.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _ = NativeMethods.PlaySoundW(IntPtr.Zero, IntPtr.Zero, 0);
        }
        catch
        {
            // Ignore shutdown audio errors.
        }

        FreeHandles();
    }

    private static byte[] BuildPcmWav(
        int sampleRate,
        int durationMs,
        double baseFrequencyHz,
        double sweepEndHz,
        double amplitude,
        bool isClick)
    {
        int sampleCount = Math.Max(1, (sampleRate * durationMs) / 1000);
        int dataSize = sampleCount * sizeof(short);
        byte[] wav = new byte[44 + dataSize];
        Span<byte> span = wav.AsSpan();

        // RIFF header
        wav[0] = (byte)'R';
        wav[1] = (byte)'I';
        wav[2] = (byte)'F';
        wav[3] = (byte)'F';
        BinaryPrimitives.WriteInt32LittleEndian(span[4..8], 36 + dataSize);
        wav[8] = (byte)'W';
        wav[9] = (byte)'A';
        wav[10] = (byte)'V';
        wav[11] = (byte)'E';

        // fmt subchunk
        wav[12] = (byte)'f';
        wav[13] = (byte)'m';
        wav[14] = (byte)'t';
        wav[15] = (byte)' ';
        BinaryPrimitives.WriteInt32LittleEndian(span[16..20], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..22], 1); // PCM
        BinaryPrimitives.WriteInt16LittleEndian(span[22..24], 1); // Mono
        BinaryPrimitives.WriteInt32LittleEndian(span[24..28], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..32], sampleRate * sizeof(short));
        BinaryPrimitives.WriteInt16LittleEndian(span[32..34], sizeof(short));
        BinaryPrimitives.WriteInt16LittleEndian(span[34..36], 16);

        // data subchunk
        wav[36] = (byte)'d';
        wav[37] = (byte)'a';
        wav[38] = (byte)'t';
        wav[39] = (byte)'a';
        BinaryPrimitives.WriteInt32LittleEndian(span[40..44], dataSize);

        double phase = 0.0;
        for (int i = 0; i < sampleCount; i++)
        {
            double progress = (double)i / sampleCount;
            double freq = baseFrequencyHz + ((sweepEndHz - baseFrequencyHz) * progress);
            phase += (2.0 * Math.PI * freq) / sampleRate;

            double envelope = isClick
                ? Math.Exp(-7.5 * progress)
                : Math.Sin(Math.PI * progress) * Math.Exp(-2.2 * progress);

            double wave = isClick
                ? Math.Sin(phase) + (0.35 * Math.Sin(phase * 2.4))
                : Math.Sin(phase) + (0.25 * Math.Sin(phase * 1.5));

            double normalized = Math.Clamp(wave * amplitude * envelope, -1.0, 1.0);
            short sample = (short)(normalized * short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(span[(44 + (i * 2))..(46 + (i * 2))], sample);
        }

        return wav;
    }

    private static class NativeMethods
    {
        [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = false)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PlaySoundW(IntPtr pszSound, IntPtr hmod, uint fdwSound);
    }
}
