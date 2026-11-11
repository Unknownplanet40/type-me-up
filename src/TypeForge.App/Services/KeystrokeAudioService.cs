using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace TypeForge.App.Services;

/// <summary>
/// Plays synthesized low-latency offline mechanical click and error alert sounds without external asset files.
/// </summary>
public interface IKeystrokeAudioService : IDisposable
{
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

    private readonly byte[] _clickWav;
    private readonly byte[] _mistakeWav;
    private GCHandle _clickHandle;
    private GCHandle _mistakeHandle;
    private bool _disposed;

    public KeystrokeAudioService()
    {
        _clickWav = BuildPcmWav(sampleRate: 22050, durationMs: 18, baseFrequencyHz: 1150, sweepEndHz: 420, amplitude: 0.24, isClick: true);
        _mistakeWav = BuildPcmWav(sampleRate: 22050, durationMs: 55, baseFrequencyHz: 210, sweepEndHz: 150, amplitude: 0.30, isClick: false);

        _clickHandle = GCHandle.Alloc(_clickWav, GCHandleType.Pinned);
        _mistakeHandle = GCHandle.Alloc(_mistakeWav, GCHandleType.Pinned);
    }

    public void PlayClick() => PlayPinnedBuffer(_clickHandle);

    public void PlayMistake() => PlayPinnedBuffer(_mistakeHandle);

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

        if (_clickHandle.IsAllocated)
        {
            _clickHandle.Free();
        }

        if (_mistakeHandle.IsAllocated)
        {
            _mistakeHandle.Free();
        }
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
