// Checks an RHVoice installation (ADR 0004): the DLL is x64, has the expected version and the voice Paul, and
// speaks the five "Comparare voci" phrases. Each phrase is written as a WAV file, so two builds can be compared
// by ear (for example 1.18.2 and 1.18.4). Used by .github/workflows/rhvoice.yml and by hand.
// Usage: dotnet run --project tools/RhVoiceCheck -- <rhvoice folder> <ro.json> <output folder> [--expect-version 1.18.4]
// Exit code 0 = every check passed.

using System.Text;
using Hasi.Core.Localization;
using Hasi.Speech.RhVoice;
using Hasi.Text.Ro;

if (args.Length is not (3 or 5) || (args.Length == 5 && args[3] != "--expect-version"))
{
    Console.Error.WriteLine("usage: <rhvoice folder> <ro.json> <output folder> [--expect-version X.Y.Z]");
    return 2;
}
var (root, resources, output) = (args[0], args[1], args[2]);
var expectedVersion = args.Length == 5 ? args[4] : null;
string[] phraseKeys = ["compare.numbers", "compare.diacritics", "compare.long", "compare.question", "compare.dateTime"];
const string Voice = "Paul";
var failures = new List<string>();

var installation = RhVoiceInstallation.Find(root, out var problem);
if (installation is null)
{
    Console.Error.WriteLine($"FAIL no usable RHVoice in {root}: {problem}");
    return 1;
}
Console.WriteLine($"dll      {installation.DllPath} ({installation.Architecture})");
if (installation.Architecture != BinaryArchitecture.X64) failures.Add($"architecture {installation.Architecture}, expected X64");

var localizer = JsonLocalizer.LoadRomanian(resources);
var normalizer = new RomanianNormalizer(AcronymLexicon.Load(AcronymLexicon.DefaultPath), NounGenderLexicon.LoadDefault());
Directory.CreateDirectory(output);
using var library = new RhVoiceLibrary(installation.DllPath, installation.ResourcePaths, Path.Combine(output, "config"));
Console.WriteLine($"version  {library.Version}");
Console.WriteLine($"voices   {string.Join(", ", library.Voices)}");
if (expectedVersion is not null && library.Version != expectedVersion) failures.Add($"version {library.Version}, expected {expectedVersion}");
if (!library.Voices.Contains(Voice, StringComparer.OrdinalIgnoreCase)) failures.Add($"no voice {Voice}");

if (failures.Count == 0)
{
    for (var i = 0; i < phraseKeys.Length; i++)
    {
        var text = normalizer.Normalize(localizer.Get(phraseKeys[i] + ".text"));
        var samples = new List<short>();
        var rate = 0;
        library.Synthesize(text, Voice, default, new SynthesisCallbacks
        {
            SampleRate = r => rate = r,
            Samples = chunk =>
            {
                samples.AddRange(chunk.ToArray());
                return true;
            },
        });
        var seconds = rate == 0 ? 0 : samples.Count / (double)rate;
        var peak = samples.Count == 0 ? 0 : samples.Max(s => Math.Abs((int)s));
        var file = Path.Combine(output, $"{i + 1}-{phraseKeys[i]["compare.".Length..]}-{library.Version}.wav");
        WriteWav(file, samples, rate);
        Console.WriteLine($"phrase   {i + 1} {seconds,6:0.00} s  peak {peak,5}  {Path.GetFileName(file)}");
        // Silence or a few clicks would mean a broken build, not a voice.
        if (seconds < 1 || peak < 1000) failures.Add($"phrase {i + 1}: {seconds:0.00} s, peak {peak}");
    }
}

foreach (var failure in failures) Console.Error.WriteLine($"FAIL {failure}");
Console.WriteLine(failures.Count == 0 ? "OK" : $"{failures.Count} check(s) failed");
return failures.Count == 0 ? 0 : 1;

static void WriteWav(string path, List<short> samples, int rate)
{
    using var writer = new BinaryWriter(File.Create(path));
    var bytes = samples.Count * 2;
    writer.Write(Encoding.ASCII.GetBytes("RIFF"));
    writer.Write(36 + bytes);
    writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
    writer.Write(16);
    writer.Write((short)1); // PCM
    writer.Write((short)1); // mono
    writer.Write(rate);
    writer.Write(rate * 2);
    writer.Write((short)2);
    writer.Write((short)16);
    writer.Write(Encoding.ASCII.GetBytes("data"));
    writer.Write(bytes);
    foreach (var sample in samples) writer.Write(sample);
}
