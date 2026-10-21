using System.Runtime.InteropServices;
using Hasi.Core.Output;
using Hasi.Core.Speech;

namespace Hasi.Speech;

/// <summary>Plays short generated tones through the Windows wave mapper, without blocking.</summary>
public sealed class ToneEarconPlayer : IEarconPlayer, IDisposable
{
    private const int SampleRate = 22050;
    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint SndMemory = 0x0004;

    // PlaySound with SND_MEMORY|SND_ASYNC reads the buffer while playing, so buffers stay pinned.
    private readonly Dictionary<Earcon, GCHandle> _sounds = new()
    {
        [Earcon.EdgeOfList] = Pin(Wave((660, 60))),
        [Earcon.Unavailable] = Pin(Wave((330, 90))),
        [Earcon.Error] = Pin(Wave((440, 90), (330, 120))),
    };

    public void Play(Earcon earcon)
    {
        if (_sounds.TryGetValue(earcon, out var sound))
        {
            PlaySound(sound.AddrOfPinnedObject(), IntPtr.Zero, SndMemory | SndAsync | SndNoDefault);
        }
    }

    public void Dispose()
    {
        PlaySound(IntPtr.Zero, IntPtr.Zero, 0);
        foreach (var sound in _sounds.Values) sound.Free();
        _sounds.Clear();
    }

    private static GCHandle Pin(byte[] data) => GCHandle.Alloc(data, GCHandleType.Pinned);

    /// <summary>16-bit mono PCM WAV with short fades so tones do not click.</summary>
    private static byte[] Wave(params (int Hz, int Ms)[] tones)
    {
        var samples = new List<short>();
        foreach (var (hz, ms) in tones)
        {
            var count = SampleRate * ms / 1000;
            var fade = Math.Min(count / 4, SampleRate / 200);
            for (var i = 0; i < count; i++)
            {
                var envelope = Math.Min(1.0, Math.Min(i, count - 1 - i) / (double)fade);
                samples.Add((short)(Math.Sin(2 * Math.PI * hz * i / SampleRate) * envelope * short.MaxValue * 0.3));
            }
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var dataBytes = samples.Count * 2;
        writer.Write("RIFF"u8);
        writer.Write(36 + dataBytes);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(dataBytes);
        foreach (var sample in samples) writer.Write(sample);
        writer.Flush();
        return stream.ToArray();
    }

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = false)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(IntPtr sound, IntPtr module, uint flags);
}
