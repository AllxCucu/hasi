using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Hasi.Speech.RhVoice;

/// <summary>RHVoice prosody, already mapped to the library's ranges (-1 to 1, 0 = voice default).</summary>
public readonly record struct RhVoiceProsody(double Rate, double Pitch);

/// <summary>Callbacks of one synthesis. Returning false from <see cref="Samples"/> stops it at once.</summary>
public sealed class SynthesisCallbacks
{
    public Func<ReadOnlySpan<short>, bool> Samples { get; init; } = _ => true;

    public Action<int>? SampleRate { get; init; }

    /// <summary>A word starts: position and length in the message text, as reported by RHVoice.</summary>
    public Action<int, int>? WordStarts { get; init; }
}

/// <summary>The RHVoice synthesis operations HASI uses (fakes in tests).</summary>
public interface IRhVoiceSynthesizer : IDisposable
{
    string Version { get; }

    IReadOnlyList<string> Voices { get; }

    /// <summary>Synthesizes <paramref name="text"/> and blocks until done or stopped through the callbacks.</summary>
    void Synthesize(string text, string voiceProfile, RhVoiceProsody prosody, SynthesisCallbacks callbacks);
}

/// <summary>
/// RHVoice.dll through its C API (RHVoice.h, LGPL-2.1-or-later). The DLL combines GPL-2.0-or-later,
/// LGPL-2.1-or-later and LGPL-3.0-or-later files and is distributed as a whole under GPL-3.0-or-later (ADR 0004). Loaded from an explicit path; function pointers are resolved by name.
/// One synthesis at a time per instance.
/// </summary>
public sealed unsafe class RhVoiceLibrary : IRhVoiceSynthesizer
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Callbacks
    {
        public delegate* unmanaged[Cdecl]<int, IntPtr, int> SetSampleRate;
        public delegate* unmanaged[Cdecl]<short*, uint, IntPtr, int> PlaySpeech;
        public IntPtr ProcessMark;
        public delegate* unmanaged[Cdecl]<uint, uint, IntPtr, int> WordStarts;
        public IntPtr WordEnds, SentenceStarts, SentenceEnds, PlayAudio;
        public delegate* unmanaged[Cdecl]<IntPtr, void> Done;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InitParams
    {
        public IntPtr DataPath, ConfigPath, ResourcePaths;
        public Callbacks Callbacks;
        public uint Options;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SynthParams
    {
        public IntPtr VoiceProfile;
        public double AbsoluteRate, AbsolutePitch, AbsoluteVolume, RelativeRate, RelativePitch, RelativeVolume;
        public int PunctuationMode;
        public IntPtr PunctuationList;
        public int CapitalsMode;
        public int Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct VoiceInfo
    {
        public IntPtr Language, Name;
        public int Gender;
        public IntPtr Country;
    }

    private readonly IntPtr _library;
    private readonly IntPtr _engine;
    private readonly List<IntPtr> _unmanaged = [];
    private readonly delegate* unmanaged[Cdecl]<IntPtr, void> _deleteEngine;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, byte*, uint, int, SynthParams*, IntPtr, IntPtr> _newMessage;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, void> _deleteMessage;
    private readonly delegate* unmanaged[Cdecl]<IntPtr, int> _speak;

    public RhVoiceLibrary(string dllPath, IReadOnlyList<string> resourcePaths, string configPath)
    {
        _library = NativeLibrary.Load(dllPath);
        var getVersion = (delegate* unmanaged[Cdecl]<IntPtr>)Export("RHVoice_get_version");
        var newEngine = (delegate* unmanaged[Cdecl]<InitParams*, IntPtr>)Export("RHVoice_new_tts_engine");
        var voiceCount = (delegate* unmanaged[Cdecl]<IntPtr, uint>)Export("RHVoice_get_number_of_voices");
        var getVoices = (delegate* unmanaged[Cdecl]<IntPtr, VoiceInfo*>)Export("RHVoice_get_voices");
        _deleteEngine = (delegate* unmanaged[Cdecl]<IntPtr, void>)Export("RHVoice_delete_tts_engine");
        _newMessage = (delegate* unmanaged[Cdecl]<IntPtr, byte*, uint, int, SynthParams*, IntPtr, IntPtr>)Export("RHVoice_new_message");
        _deleteMessage = (delegate* unmanaged[Cdecl]<IntPtr, void>)Export("RHVoice_delete_message");
        _speak = (delegate* unmanaged[Cdecl]<IntPtr, int>)Export("RHVoice_speak");

        Version = Marshal.PtrToStringUTF8(getVersion()) ?? "?";
        Directory.CreateDirectory(configPath);
        var paths = (IntPtr*)Keep(Marshal.AllocCoTaskMem(sizeof(IntPtr) * (resourcePaths.Count + 1)));
        for (var i = 0; i < resourcePaths.Count; i++) paths[i] = Utf8(resourcePaths[i]);
        paths[resourcePaths.Count] = IntPtr.Zero;

        var init = new InitParams
        {
            ConfigPath = Utf8(configPath),
            ResourcePaths = (IntPtr)paths,
            Callbacks = new Callbacks
            {
                SetSampleRate = &OnSampleRate,
                PlaySpeech = &OnSpeech,
                WordStarts = &OnWordStarts,
                Done = &OnDone,
            },
        };
        _engine = newEngine(&init);
        if (_engine == IntPtr.Zero)
        {
            Release();
            throw new InvalidOperationException("RHVoice_new_tts_engine failed (check the voice and language data paths).");
        }

        var voices = getVoices(_engine);
        var list = new List<string>();
        for (var i = 0; i < voiceCount(_engine); i++)
        {
            list.Add(Marshal.PtrToStringUTF8(voices[i].Name) ?? "?");
        }
        Voices = list;
    }

    public string Version { get; }

    public IReadOnlyList<string> Voices { get; }

    public void Synthesize(string text, string voiceProfile, RhVoiceProsody prosody, SynthesisCallbacks callbacks)
    {
        ArgumentNullException.ThrowIfNull(callbacks);
        var handle = GCHandle.Alloc(callbacks);
        var profile = Marshal.StringToCoTaskMemUTF8(voiceProfile);
        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var parameters = new SynthParams
            {
                VoiceProfile = profile,
                AbsoluteRate = Math.Clamp(prosody.Rate, -1, 1),
                AbsolutePitch = Math.Clamp(prosody.Pitch, -1, 1),
                RelativeRate = 1,
                RelativePitch = 1,
                RelativeVolume = 1,
            };
            fixed (byte* utf8 = bytes)
            {
                var message = _newMessage(_engine, utf8, (uint)bytes.Length, 0, &parameters, GCHandle.ToIntPtr(handle));
                if (message == IntPtr.Zero) throw new InvalidOperationException("RHVoice_new_message failed.");
                try
                {
                    _speak(message);
                }
                finally
                {
                    _deleteMessage(message);
                }
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(profile);
            handle.Free();
        }
    }

    public void Dispose()
    {
        _deleteEngine(_engine);
        Release();
    }

    private void Release()
    {
        foreach (var pointer in _unmanaged) Marshal.FreeCoTaskMem(pointer);
        _unmanaged.Clear();
    }

    private IntPtr Export(string name) => NativeLibrary.GetExport(_library, name);

    private IntPtr Keep(IntPtr pointer)
    {
        _unmanaged.Add(pointer);
        return pointer;
    }

    private IntPtr Utf8(string text) => Keep(Marshal.StringToCoTaskMemUTF8(text));

    private static SynthesisCallbacks? From(IntPtr user) =>
        user == IntPtr.Zero ? null : GCHandle.FromIntPtr(user).Target as SynthesisCallbacks;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OnSampleRate(int rate, IntPtr user) => Guard(() => From(user)?.SampleRate?.Invoke(rate));

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OnSpeech(short* samples, uint count, IntPtr user)
    {
        try
        {
            var callbacks = From(user);
            return callbacks is null || callbacks.Samples(new ReadOnlySpan<short>(samples, (int)count)) ? 1 : 0;
        }
#pragma warning disable CA1031 // An exception must never unwind into native code; stopping is the safe answer.
        catch
#pragma warning restore CA1031
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int OnWordStarts(uint position, uint length, IntPtr user) =>
        Guard(() => From(user)?.WordStarts?.Invoke((int)position, (int)length));

    /// <summary>Runs a callback; an exception must never unwind into native code.</summary>
    private static int Guard(Action action)
    {
        try
        {
            action();
            return 1;
        }
#pragma warning disable CA1031
        catch
#pragma warning restore CA1031
        {
            return 0;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnDone(IntPtr user)
    {
    }
}
