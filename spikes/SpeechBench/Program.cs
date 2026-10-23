// ADR 0004 measurements: the same texts through SAPI5 (Paul) and RHVoice.dll (Paul), with real playback.
// Usage: dotnet run --project spikes/SpeechBench -c Release -- sapi5|rhvoice-dll [rhvoice folder] [volume 0-100]
// Run each engine in its own process, so memory figures are not mixed.

using System.Collections.Concurrent;
using System.Diagnostics;
using Hasi.Core.Speech;
using Hasi.Speech;
using Hasi.Speech.Audio;
using Hasi.Speech.RhVoice;
using Hasi.Text.Ro;

var engineName = args.Length > 0 ? args[0] : "sapi5";
var volume = args.Length > 2 ? int.Parse(args[2]) : 25;
var normalizer = new RomanianNormalizer(AcronymLexicon.Empty, NounGenderLexicon.BuiltIn);

const string Short = "Meniu principal. 4 opțiuni. Selectat: Setări, 1 din 4.";
const string Long = "Bun venit în HASI. Acesta este un text mai lung, pentru a asculta cum sună vocea pe o citire de " +
                    "câteva propoziții. Vremea de mâine va fi însorită în cea mai mare parte a țării, cu temperaturi între " +
                    "15 și 22 de grade. Spre seară, în zonele de munte, sunt posibile averse de scurtă durată. Am plătit " +
                    "1.250,99 lei pentru 21 de facturi, adică 3% din total. Programarea la medic este miercuri, 21.10.2026, " +
                    "la ora 9:30. Vă dorim o zi bună și vă mulțumim că folosiți HASI.";

var process = Process.GetCurrentProcess();
var baselineMemory = Memory();
var metrics = new ConcurrentQueue<SpeechMetric>();
var settings = new SpeechSettings(Volume: volume);

ISpeechEngine engine = engineName switch
{
    "sapi5" => CreateSapi(),
    "rhvoice-dll" => CreateRhVoice(args[1]),
    _ => throw new ArgumentException("engine must be sapi5 or rhvoice-dll"),
};
((ISpeechMetrics)engine).MetricReported += (_, m) => metrics.Enqueue(m);
var afterLoad = Memory();

// Warm-up.
engine.Speak(normalizer.Normalize("Pregătire."));
await engine.WaitUntilIdleAsync(TimeSpan.FromSeconds(10));
metrics.Clear();

// 1. Time to first sound, five short phrases.
var firstSounds = new List<double>();
for (var i = 0; i < 5; i++)
{
    engine.Speak(normalizer.Normalize(Short));
    await engine.WaitUntilIdleAsync(TimeSpan.FromSeconds(20));
    await Task.Delay(300);
    firstSounds.AddRange(Take("first-sound"));
}

// 2. A long reading: CPU and memory sampled every 250 ms while it plays.
var cpuStart = process.TotalProcessorTime;
var wall = Stopwatch.StartNew();
long peakWorkingSet = 0, peakPrivate = 0;
engine.Speak(normalizer.Normalize(Long));
await Task.Delay(200);
while (engine.IsSpeaking && wall.Elapsed < TimeSpan.FromMinutes(2))
{
    process.Refresh();
    peakWorkingSet = Math.Max(peakWorkingSet, process.WorkingSet64);
    peakPrivate = Math.Max(peakPrivate, process.PrivateMemorySize64);
    await Task.Delay(250);
}
var readingSeconds = wall.Elapsed.TotalSeconds;
var cpuPercent = (process.TotalProcessorTime - cpuStart).TotalMilliseconds / wall.Elapsed.TotalMilliseconds / Environment.ProcessorCount * 100;
var cpuOneCore = (process.TotalProcessorTime - cpuStart).TotalMilliseconds / wall.Elapsed.TotalMilliseconds * 100;
await Task.Delay(300);
metrics.Clear();

// 3. Stop in the middle of speech, five times.
var stops = new List<double>();
for (var i = 0; i < 5; i++)
{
    engine.Speak(normalizer.Normalize(Long));
    await Task.Delay(1500);
    engine.Stop();
    await Task.Delay(700);
    stops.AddRange(Take("stop"));
}

engine.Dispose();
Console.WriteLine($"engine={engineName} volume={volume}");
Console.WriteLine($"memory: before engine {baselineMemory}, after loading {afterLoad}");
Console.WriteLine($"first sound (ms): {Describe(firstSounds)}");
Console.WriteLine($"long reading: {readingSeconds:F1} s, CPU {cpuPercent:F2}% of all {Environment.ProcessorCount} cores " +
                  $"(= {cpuOneCore:F1}% of one core), peak working set {peakWorkingSet / 1048576.0:F0} MB, " +
                  $"peak private {peakPrivate / 1048576.0:F0} MB");
Console.WriteLine($"stop (ms): {Describe(stops)}");
return 0;

IEnumerable<double> Take(string kind)
{
    var taken = new List<double>();
    var rest = new List<SpeechMetric>();
    while (metrics.TryDequeue(out var m)) (m.Kind == kind ? taken : null)?.Add(m.Milliseconds);
    return taken;
}

static string Describe(List<double> values) => values.Count == 0
    ? "no measurement"
    : $"median {values.Order().ElementAt(values.Count / 2):F1}, min {values.Min():F1}, max {values.Max():F1} ({values.Count} runs: {string.Join(", ", values.Select(v => v.ToString("F1")))})";

string Memory()
{
    process.Refresh();
    return $"working set {process.WorkingSet64 / 1048576.0:F0} MB, private {process.PrivateMemorySize64 / 1048576.0:F0} MB";
}

ISpeechEngine CreateSapi()
{
    var sapi = new Sapi5Engine();
    sapi.SelectVoice(sapi.Voices.First(v => v.Name.Contains("Paul", StringComparison.OrdinalIgnoreCase)).Name);
    sapi.Settings = settings;
    return sapi;
}

ISpeechEngine CreateRhVoice(string folder)
{
    var installation = RhVoiceInstallation.Find(folder, out var problem) ?? throw new InvalidOperationException(problem);
    var library = new RhVoiceLibrary(installation.DllPath, installation.ResourcePaths,
        Path.Combine(Path.GetTempPath(), "hasi-bench-rhvoice-config"));
    return new RhVoiceDllEngine(library, SpeechAudioOutput.Open(24000, TimeSpan.Zero), "Paul") { Settings = settings };
}
