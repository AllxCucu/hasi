using System.Globalization;
using System.Text.RegularExpressions;

namespace Hasi.Text.Ro;

/// <summary>
/// How acronyms are spoken, from an editable text file (<c>Data/acronime.txt</c>):
/// <c>CFR =&gt; litere</c>, <c>ANAF =&gt; cuvânt</c> or an explicit pronunciation.
/// </summary>
public sealed partial class AcronymLexicon
{
    public const string DefaultFileName = "acronime.txt";
    private const string Separator = "=>";
    private const string ByLetters = "litere";
    private const string AsWord = "cuvânt";

    private static readonly Dictionary<char, string> LetterNames = new()
    {
        ['A'] = "a", ['Ă'] = "ă", ['Â'] = "â", ['B'] = "be", ['C'] = "ce", ['D'] = "de", ['E'] = "e",
        ['F'] = "fe", ['G'] = "ge", ['H'] = "haș", ['I'] = "i", ['Î'] = "î", ['J'] = "je", ['K'] = "ca",
        ['L'] = "le", ['M'] = "me", ['N'] = "ne", ['O'] = "o", ['P'] = "pe", ['Q'] = "chiu", ['R'] = "re",
        ['S'] = "se", ['Ș'] = "șe", ['T'] = "te", ['Ț'] = "țe", ['U'] = "u", ['V'] = "ve", ['W'] = "dublu ve",
        ['X'] = "ics", ['Y'] = "i grec", ['Z'] = "ze",
    };

    private readonly Dictionary<string, string> _spoken = new(StringComparer.Ordinal);

    public static AcronymLexicon Empty { get; } = new();

    public int Count => _spoken.Count;

    /// <summary>Path of the default file next to the application, for example bin\Data\acronime.txt.</summary>
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Data", DefaultFileName);

    public static AcronymLexicon Load(string path) =>
        File.Exists(path) ? Parse(File.ReadAllLines(path)) : Empty;

    public static AcronymLexicon Parse(IEnumerable<string> lines)
    {
        var lexicon = new AcronymLexicon();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var at = line.IndexOf(Separator, StringComparison.Ordinal);
            if (at <= 0) continue;
            var acronym = line[..at].Trim();
            var how = line[(at + Separator.Length)..].Trim();
            if (acronym.Length == 0 || how.Length == 0) continue;
            lexicon._spoken[acronym] = how switch
            {
                ByLetters => SpellLetters(acronym),
                AsWord => CultureInfo.GetCultureInfo("ro-RO").TextInfo.ToTitleCase(acronym.ToLowerInvariant()),
                _ => how,
            };
        }
        return lexicon;
    }

    public static string SpellLetters(string acronym) =>
        string.Join(' ', acronym.Select(c => LetterNames.TryGetValue(char.ToUpperInvariant(c), out var name) ? name : c.ToString()));

    [GeneratedRegex(@"(?<![\p{L}\d])(?<word>\p{Lu}[\p{Lu}\d]+)(?<suffix>-\p{Ll}+)?(?![\p{L}\d])", RegexOptions.CultureInvariant)]
    private static partial Regex UppercaseWordPattern();

    /// <summary>Replaces known acronyms; unknown ones are left to the voice.</summary>
    public string Expand(string text)
    {
        if (_spoken.Count == 0) return text;
        return UppercaseWordPattern().Replace(text, m =>
            _spoken.TryGetValue(m.Groups["word"].Value, out var spoken)
                ? spoken + m.Groups["suffix"].Value
                : m.Value);
    }
}
