namespace Hasi.Text.Ro;

/// <summary>What the normalizer knows about the word after a number.</summary>
/// <param name="IsNoun">Whether the word is treated as the counted noun.</param>
/// <param name="Guessed">The gender comes from the word's ending, not from a list: worth reporting.</param>
public readonly record struct NounLookup(bool IsNoun, Gender Gender, bool Guessed)
{
    public static NounLookup NotANoun => default;
}

/// <summary>
/// Agreement gender of counted nouns (ADR 0005), looked up in this order:
/// 1. the tester's <c>Data/genuri.txt</c>; 2. the built-in hand-checked list; 3. the MULTEXT-East table
/// <c>Data/substantive.txt</c>; 4. reliable plural endings; 5. singular endings (-ă, -ie, -ea → feminine),
/// otherwise masculine, marked as guessed.
/// </summary>
public sealed class NounGenderLexicon
{
    public const string DictionaryFileName = "substantive.txt";
    public const string UserFileName = "genuri.txt";

    private readonly Dictionary<string, Gender> _user;
    private readonly Dictionary<string, Gender> _dictionary;

    private NounGenderLexicon(Dictionary<string, Gender> user, Dictionary<string, Gender> dictionary)
    {
        _user = user;
        _dictionary = dictionary;
    }

    /// <summary>Only the built-in list and the endings; used when the data files are absent.</summary>
    public static NounGenderLexicon BuiltIn { get; } = new([], []);

    public int DictionaryCount => _dictionary.Count;

    public int UserCount => _user.Count;

    /// <summary>The data files next to the application (bin\Data).</summary>
    public static NounGenderLexicon LoadDefault()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Data");
        return Load(Path.Combine(folder, DictionaryFileName), Path.Combine(folder, UserFileName));
    }

    public static NounGenderLexicon Load(string dictionaryPath, string userPath) => new(
        File.Exists(userPath) ? Parse(File.ReadLines(userPath)) : [],
        File.Exists(dictionaryPath) ? Parse(File.ReadLines(dictionaryPath)) : []);

    public static NounGenderLexicon FromTables(IEnumerable<string> user, IEnumerable<string> dictionary) =>
        new(Parse(user), Parse(dictionary));

    /// <summary>Lines <c>formă =&gt; m | f | n</c>; # starts a comment; malformed lines are ignored.</summary>
    public static Dictionary<string, Gender> Parse(IEnumerable<string> lines)
    {
        var table = new Dictionary<string, Gender>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var at = line.IndexOf("=>", StringComparison.Ordinal);
            if (at <= 0) continue;
            var word = line[..at].Trim();
            Gender? gender = line[(at + 2)..].Trim().ToLowerInvariant() switch
            {
                "m" => Gender.Masculine,
                "f" => Gender.Feminine,
                "n" => Gender.Neuter,
                _ => null,
            };
            if (word.Length > 0 && gender is { } g) table[word] = g;
        }
        return table;
    }

    /// <param name="plural">The number before the word is not 1, so the word should be a plural form.</param>
    public NounLookup Find(string word, bool plural = false)
    {
        if (word.Length == 0 || Lexicon.IsNeverACountedNoun(word)) return NounLookup.NotANoun;
        if (_user.TryGetValue(word, out var user)) return new(true, user, false);
        if (Lexicon.KnownGender(word) is { } known) return new(true, known, false);
        if (_dictionary.TryGetValue(word, out var listed)) return new(true, listed, false);
        if (Lexicon.PluralEndingGender(word) is { } pluralEnding) return new(true, pluralEnding, false);

        // Unknown word: treated as a noun only if it looks like one (a lower-case word).
        if (!char.IsLower(word[0]) || word.Length < 2) return NounLookup.NotANoun;
        if (plural)
        {
            // Plurals in -e (and -uri, above) take "două": smartfoane, tramvaie (tester's rule, 2026-10-23).
            // Plurals in -i are ambiguous (lei / luni): masculine, reported as guessed.
            return word.EndsWith('e')
                ? new(true, Gender.Neuter, Guessed: false)
                : new(true, Gender.Masculine, Guessed: true);
        }
        var feminine = word.EndsWith('ă') || word.EndsWith("ie", StringComparison.Ordinal) || word.EndsWith("ea", StringComparison.Ordinal);
        return new(true, feminine ? Gender.Feminine : Gender.Masculine, Guessed: true);
    }

    /// <summary>True when the word is a known noun, so a number right after it is a label ("pagina 2").</summary>
    public bool IsKnownNoun(string word) =>
        word.Length > 0 && !Lexicon.IsNeverACountedNoun(word)
        && (Lexicon.IsLabelNoun(word) || _user.ContainsKey(word) || Lexicon.KnownGender(word) is not null || _dictionary.ContainsKey(word));
}
