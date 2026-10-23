// Phase 0.5 spike (ADR 0004): can HASI drive RHVoice.dll directly, and what do we gain over SAPI?
// Measures, for the Romanian voice Paul:
//   - time to first audio and real-time factor through the C API;
//   - how fast synthesis stops when the audio callback asks it to (our "interrupt");
//   - the same phrase through SAPI5 (System.Speech, synthesized to memory) for comparison.
// Usage: dotnet run --project spikes/RhvoiceDirect -- <RHVoice.dll> <voice data dir> <language data dir> <output folder>
// The binaries and data come from the official RHVoice NVDA add-ons and are not part of the repository.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using System.Text;

if (args.Length < 4)
{
    Console.WriteLine("usage: <RHVoice.dll> <voice data dir> <language data dir> <output folder>");
    return 2;
}

var (dllPath, voiceData, languageData, outputFolder) = (args[0], args[1], args[2], args[3]);
Directory.CreateDirectory(outputFolder);
NativeLibrary.Load(dllPath);

const string Short = "Meniu principal. Patru opțiuni. Selectat: Setări, unu din patru.";
const string Long = "Bun venit în HASI. Acesta este un text lung, folosit pentru a măsura cât de repede se oprește " +
                    "vorbirea atunci când utilizatorul apasă o tastă. Textul continuă cu mai multe propoziții, ca " +
                    "sinteza să aibă de lucru câteva secunde. Plata de doisprezece lei și cincizeci de bani se face " +
                    "pe întâi martie, la ora paisprezece și treizeci de minute.";

Console.WriteLine($"RHVoice version: {Rhv.Version()}");
using var engine = Rhv.Engine.Create([voiceData, languageData], Path.Combine(outputFolder, "config"));
Console.WriteLine($"voices: {string.Join(", ", engine.Voices())}");
Console.WriteLine($"profiles: {string.Join(", ", engine.Profiles())}");
var profile = engine.Profiles().FirstOrDefault(p => p.Contains("Paul", StringComparison.OrdinalIgnoreCase)) ?? engine.Profiles().First();

// Warm-up (first call loads the voice).
var warm = Stopwatch.StartNew();
engine.Speak("Pregătire.", profile, _ => true);
Console.WriteLine($"first synthesis incl. loading: {warm.ElapsedMilliseconds} ms");

for (var run = 1; run <= 3; run++)
{
    var r = engine.Speak(Short, profile, _ => true);
    Console.WriteLine($"[direct] short phrase run {run}: first audio {r.FirstAudioMs:F0} ms, total {r.TotalMs:F0} ms, " +
                      $"audio {r.AudioMs:F0} ms, real-time factor {r.TotalMs / r.AudioMs:F3}");
    if (run == 1) Wav.Write(Path.Combine(outputFolder, "direct-short.wav"), r.Samples, r.SampleRate);
}

// Interrupt: ask the engine to stop as soon as 300 ms of audio have been produced.
var stopAfterMs = 300.0;
var interrupted = engine.Speak(Long, profile, progress => progress.AudioMs < stopAfterMs);
Console.WriteLine($"[direct] interrupt: requested after {stopAfterMs} ms of audio; RHVoice_speak returned " +
                  $"{interrupted.StopLatencyMs:F1} ms after the callback said stop (audio produced {interrupted.AudioMs:F0} ms)");

// SAPI5 comparison: same phrase, Paul through the SAPI server, synthesized to memory.
using (var sapi = new SpeechSynthesizer())
{
    var paul = sapi.GetInstalledVoices().FirstOrDefault(v => v.VoiceInfo.Name.Contains("Paul", StringComparison.OrdinalIgnoreCase));
    if (paul is null)
    {
        Console.WriteLine("[sapi] Paul is not installed for SAPI5");
    }
    else
    {
        sapi.SelectVoice(paul.VoiceInfo.Name);
        for (var run = 1; run <= 3; run++)
        {
            using var memory = new MemoryStream();
            sapi.SetOutputToAudioStream(memory, new SpeechAudioFormatInfo(24000, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
            var clock = Stopwatch.StartNew();
            sapi.Speak(Short);
            var total = clock.Elapsed.TotalMilliseconds;
            var audioMs = memory.Length / 2.0 / 24000 * 1000;
            Console.WriteLine($"[sapi]   short phrase run {run}: total {total:F0} ms, audio {audioMs:F0} ms, " +
                              $"real-time factor {total / audioMs:F3} (no per-chunk callback: first audio not measurable)");
        }
    }
}
return 0;

internal static unsafe class Rhv
{
    private const string Lib = "RHVoice";

    [StructLayout(LayoutKind.Sequential)]
    private struct Callbacks
    {
        public delegate* unmanaged[Cdecl]<int, IntPtr, int> SetSampleRate;
        public delegate* unmanaged[Cdecl]<short*, uint, IntPtr, int> PlaySpeech;
        public IntPtr ProcessMark, WordStarts, WordEnds, SentenceStarts, SentenceEnds, PlayAudio;
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

    [DllImport(Lib)] private static extern IntPtr RHVoice_get_version();
    [DllImport(Lib)] private static extern IntPtr RHVoice_new_tts_engine(InitParams* p);
    [DllImport(Lib)] private static extern void RHVoice_delete_tts_engine(IntPtr e);
    [DllImport(Lib)] private static extern uint RHVoice_get_number_of_voices(IntPtr e);
    [DllImport(Lib)] private static extern VoiceInfo* RHVoice_get_voices(IntPtr e);
    [DllImport(Lib)] private static extern uint RHVoice_get_number_of_voice_profiles(IntPtr e);
    [DllImport(Lib)] private static extern IntPtr* RHVoice_get_voice_profiles(IntPtr e);
    [DllImport(Lib)] private static extern IntPtr RHVoice_new_message(IntPtr e, byte* text, uint length, int type, SynthParams* p, IntPtr userData);
    [DllImport(Lib)] private static extern void RHVoice_delete_message(IntPtr m);
    [DllImport(Lib)] private static extern int RHVoice_speak(IntPtr m);

    public static string Version() => Marshal.PtrToStringUTF8(RHVoice_get_version()) ?? "?";

    public readonly record struct Progress(double AudioMs);

    public sealed record Result(short[] Samples, int SampleRate, double FirstAudioMs, double TotalMs, double AudioMs, double StopLatencyMs);

    // One synthesis at a time in this spike, so the callback state can be static.
    private static List<short> s_samples = [];
    private static int s_rate = 24000;
    private static Stopwatch s_clock = new();
    private static double s_first = -1, s_stopRequestedAt = -1;
    private static Func<Progress, bool> s_keepGoing = _ => true;

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static int OnSampleRate(int rate, IntPtr user) { s_rate = rate; return 1; }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static int OnSpeech(short* samples, uint count, IntPtr user)
    {
        if (s_first < 0) s_first = s_clock.Elapsed.TotalMilliseconds;
        for (var i = 0; i < count; i++) s_samples.Add(samples[i]);
        if (s_keepGoing(new Progress(s_samples.Count * 1000.0 / s_rate))) return 1;
        s_stopRequestedAt = s_clock.Elapsed.TotalMilliseconds;
        return 0; // tells RHVoice to stop: this is how HASI would interrupt speech
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static void OnDone(IntPtr user) { }

    public sealed class Engine : IDisposable
    {
        private readonly IntPtr _handle;
        private readonly List<IntPtr> _strings = [];

        private Engine(IntPtr handle, List<IntPtr> strings) { _handle = handle; _strings = strings; }

        public static Engine Create(string[] resourcePaths, string configPath)
        {
            Directory.CreateDirectory(configPath);
            var strings = new List<IntPtr>();
            IntPtr Utf8(string s) { var p = Marshal.StringToCoTaskMemUTF8(s); strings.Add(p); return p; }
            var paths = (IntPtr*)Marshal.AllocCoTaskMem(sizeof(IntPtr) * (resourcePaths.Length + 1));
            strings.Add((IntPtr)paths);
            for (var i = 0; i < resourcePaths.Length; i++) paths[i] = Utf8(resourcePaths[i]);
            paths[resourcePaths.Length] = IntPtr.Zero;

            var init = new InitParams
            {
                DataPath = IntPtr.Zero,
                ConfigPath = Utf8(configPath),
                ResourcePaths = (IntPtr)paths,
                Callbacks = new Callbacks
                {
                    SetSampleRate = &OnSampleRate,
                    PlaySpeech = &OnSpeech,
                    Done = &OnDone,
                },
            };
            var handle = RHVoice_new_tts_engine(&init);
            if (handle == IntPtr.Zero) throw new InvalidOperationException("RHVoice_new_tts_engine failed");
            return new Engine(handle, strings);
        }

        public List<string> Voices()
        {
            var list = new List<string>();
            var voices = RHVoice_get_voices(_handle);
            for (var i = 0; i < RHVoice_get_number_of_voices(_handle); i++)
                list.Add($"{Marshal.PtrToStringUTF8(voices[i].Name)} ({Marshal.PtrToStringUTF8(voices[i].Language)})");
            return list;
        }

        public List<string> Profiles()
        {
            var list = new List<string>();
            var profiles = RHVoice_get_voice_profiles(_handle);
            for (var i = 0; i < RHVoice_get_number_of_voice_profiles(_handle); i++) list.Add(Marshal.PtrToStringUTF8(profiles[i])!);
            return list;
        }

        public Result Speak(string text, string profile, Func<Progress, bool> keepGoing)
        {
            s_samples = [];
            s_first = -1;
            s_stopRequestedAt = -1;
            s_keepGoing = keepGoing;
            var bytes = Encoding.UTF8.GetBytes(text);
            var profilePtr = Marshal.StringToCoTaskMemUTF8(profile);
            try
            {
                var p = new SynthParams { VoiceProfile = profilePtr, RelativeRate = 1, RelativePitch = 1, RelativeVolume = 1 };
                fixed (byte* t = bytes)
                {
                    s_clock = Stopwatch.StartNew();
                    var message = RHVoice_new_message(_handle, t, (uint)bytes.Length, 0, &p, IntPtr.Zero);
                    RHVoice_speak(message);
                    var total = s_clock.Elapsed.TotalMilliseconds;
                    RHVoice_delete_message(message);
                    var audio = s_samples.Count * 1000.0 / s_rate;
                    var stopLatency = s_stopRequestedAt < 0 ? 0 : total - s_stopRequestedAt;
                    return new Result([.. s_samples], s_rate, s_first, total, audio, stopLatency);
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(profilePtr);
            }
        }

        public void Dispose()
        {
            RHVoice_delete_tts_engine(_handle);
            foreach (var p in _strings) Marshal.FreeCoTaskMem(p);
        }
    }
}

internal static class Wav
{
    public static void Write(string path, short[] samples, int rate)
    {
        using var w = new BinaryWriter(File.Create(path));
        w.Write("RIFF"u8); w.Write(36 + samples.Length * 2); w.Write("WAVEfmt "u8); w.Write(16);
        w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(samples.Length * 2);
        foreach (var s in samples) w.Write(s);
    }
}
