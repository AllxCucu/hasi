// Builds src/Hasi.Text.Ro/Data/substantive.txt from the MULTEXT-East free lexicons 4.0, Romanian (wfl-ro.txt.gz,
// CC BY-SA 4.0, http://hdl.handle.net/11356/1041). See ADR 0005.
// Usage: dotnet run --project tools/GenderTable -- <wfl-ro.txt.gz> <output substantive.txt>
//
// Input lines: form <TAB> lemma <TAB> MSD. Common nouns have MSDs starting with "Nc"; the third character is the
// agreement gender (m/f). Neuter nouns are tagged masculine in the singular and feminine in the plural, which is
// exactly what counting needs ("un dosar", "două dosare").

using System.IO.Compression;
using System.Text;

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: <wfl-ro.txt.gz> <output substantive.txt>");
    return 2;
}

// Parts of speech that make a form useless as a counted noun when it is also one of them
// (adverbs, adpositions, conjunctions, pronouns, determiners, numerals, particles, interjections).
var functionWordTags = new HashSet<char> { 'R', 'S', 'C', 'P', 'D', 'M', 'Q', 'I' };

var genders = new Dictionary<string, HashSet<char>>(StringComparer.Ordinal);
var functionWords = new HashSet<string>(StringComparer.Ordinal);
var read = 0;

using (var file = File.OpenRead(args[0]))
using (var gzip = new GZipStream(file, CompressionMode.Decompress))
using (var reader = new StreamReader(gzip, Encoding.UTF8))
{
    while (reader.ReadLine() is { } line)
    {
        read++;
        var parts = line.Split('\t');
        if (parts.Length < 3 || parts[2].Length == 0) continue;
        var form = FixDiacritics(parts[0]).ToLowerInvariant();
        var msd = parts[2];
        if (functionWordTags.Contains(msd[0]))
        {
            functionWords.Add(form);
            continue;
        }
        if (!msd.StartsWith("Nc", StringComparison.Ordinal) || msd.Length < 3) continue;
        if (msd[2] is not ('m' or 'f')) continue;
        if (!genders.TryGetValue(form, out var set)) genders[form] = set = [];
        set.Add(msd[2]);
    }
}

var kept = genders
    .Where(g => g.Value.Count == 1 && !functionWords.Contains(g.Key))
    .OrderBy(g => g.Key, StringComparer.Ordinal)
    .ToList();
var ambiguous = genders.Count(g => g.Value.Count > 1);
var dropped = genders.Count(g => g.Value.Count == 1 && functionWords.Contains(g.Key));

using (var output = new StreamWriter(args[1], append: false, new UTF8Encoding(false)))
{
    output.NewLine = "\n";
    output.WriteLine("# Genul de acord al substantivelor comune românești, pentru numărare („un dosar”, „două dosare”).");
    output.WriteLine("# Generat de tools/GenderTable din:");
    output.WriteLine("#   MULTEXT-East free lexicons 4.0 (Erjavec, T., Bruda, Ş., ..., Tufiş, D., Véronis, J., 2010),");
    output.WriteLine("#   Jožef Stefan Institute, http://hdl.handle.net/11356/1041, lexiconul românesc (wfl-ro.txt).");
    output.WriteLine("# Licență: Creative Commons Attribution-ShareAlike 4.0 International,");
    output.WriteLine("#   https://creativecommons.org/licenses/by-sa/4.0/ . Modificări: doar substantivele comune, ş/ţ → ș/ț,");
    output.WriteLine("#   litere mici, fără formele cu gen ambiguu și fără cele care sunt și cuvinte funcționale.");
    output.WriteLine("# Nu edita acest fișier; corecturile tale merg în genuri.txt, care are prioritate.");
    output.WriteLine("# Format: formă => m | f");
    foreach (var (form, set) in kept) output.WriteLine($"{form} => {set.Single()}");
}

Console.WriteLine($"lines read: {read}; noun forms: {genders.Count}; written: {kept.Count}; " +
                  $"ambiguous gender skipped: {ambiguous}; also function words skipped: {dropped}");
return 0;

static string FixDiacritics(string text) => text
    .Replace('ş', 'ș').Replace('Ş', 'Ș')
    .Replace('ţ', 'ț').Replace('Ţ', 'Ț')
    .Normalize(NormalizationForm.FormC);
