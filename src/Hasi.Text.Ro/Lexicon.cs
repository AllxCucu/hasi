namespace Hasi.Text.Ro;

/// <summary>
/// Linguistic data the normalizer needs: noun genders (for "doi lei" / "două ore"), words that are
/// never counted nouns, abbreviations and month names. Data, not UI text.
/// </summary>
internal static class Lexicon
{
    public static readonly string[] Months =
    [
        "ianuarie", "februarie", "martie", "aprilie", "mai", "iunie",
        "iulie", "august", "septembrie", "octombrie", "noiembrie", "decembrie",
    ];

    private static readonly HashSet<string> Masculine = new(StringComparer.OrdinalIgnoreCase)
    {
        "an", "ani", "leu", "lei", "ban", "bani", "metru", "metri", "kilometru", "kilometri",
        "centimetru", "centimetri", "milimetru", "milimetri", "litru", "litri", "mililitru", "mililitri",
        "om", "oameni", "copil", "copii", "bărbat", "bărbați", "euro", "cent", "cenți", "dolar", "dolari",
        "pas", "pași", "frate", "frați", "nepot", "nepoți", "elev", "elevi", "medic", "medici",
    };

    private static readonly HashSet<string> Feminine = new(StringComparer.OrdinalIgnoreCase)
    {
        "oră", "ore", "zi", "zile", "lună", "luni", "săptămână", "săptămâni", "opțiune", "opțiuni",
        "persoană", "persoane", "pagină", "pagini", "bucată", "bucăți", "rată", "rate", "cameră", "camere",
        "mașină", "mașini", "stație", "stații", "linie", "linii", "întrebare", "întrebări", "scrisoare",
        "scrisori", "factură", "facturi", "carte", "cărți", "secundă", "secunde", "noapte", "nopți",
        "femeie", "femei", "casă", "case", "pastilă", "pastile", "lecție", "lecții", "notificare",
        "notificări", "soră", "surori", "nepoată", "nepoate", "fiică", "fiice", "tabletă", "tablete",
    };

    private static readonly HashSet<string> Neuter = new(StringComparer.OrdinalIgnoreCase)
    {
        "minut", "minute", "dosar", "dosare", "fișier", "fișiere", "mesaj", "mesaje", "rând", "rânduri",
        "kilogram", "kilograme", "gram", "grame", "miligram", "miligrame", "grad", "grade", "etaj", "etaje",
        "apartament", "apartamente", "număr", "numere", "procent", "procente", "bilet", "bilete",
        "tren", "trenuri", "loc", "locuri", "articol", "articole", "punct", "puncte", "e-mail", "e-mailuri",
        "document", "documente", "post", "posturi",
    };

    /// <summary>Words that follow numbers but are never the counted noun ("1 din 4", "2 este").</summary>
    private static readonly HashSet<string> NotNouns = new(StringComparer.OrdinalIgnoreCase)
    {
        "din", "și", "sau", "la", "pe", "în", "cu", "ca", "că", "dar", "nu", "iar", "ori", "până", "după",
        "pentru", "fără", "între", "spre", "către", "decât", "este", "sunt", "era", "a", "al", "ale", "lui",
        "mai", "foarte", "ce", "care", "plus", "minus", "virgulă", "punct", "ora", "orele",
    };

    /// <summary>Gender of the counted noun, or null when the next word is not a counted noun.</summary>
    public static Gender? NounGender(string word)
    {
        if (word.Length == 0 || NotNouns.Contains(word)) return null;
        if (Masculine.Contains(word)) return Gender.Masculine;
        if (Feminine.Contains(word)) return Gender.Feminine;
        if (Neuter.Contains(word)) return Gender.Neuter;
        if (Months.Contains(word, StringComparer.OrdinalIgnoreCase)) return null;

        // Unknown words: typical plural endings.
        if (word.EndsWith("uri", StringComparison.OrdinalIgnoreCase)) return Gender.Neuter;
        if (word.EndsWith("ări", StringComparison.OrdinalIgnoreCase)
            || word.EndsWith("ții", StringComparison.OrdinalIgnoreCase)
            || word.EndsWith("uni", StringComparison.OrdinalIgnoreCase)
            || word.EndsWith("ăți", StringComparison.OrdinalIgnoreCase))
        {
            return Gender.Feminine;
        }
        return null;
    }

    /// <summary>Abbreviation (with its final dot) → full word.</summary>
    public static readonly (string Abbreviation, string Expansion)[] Abbreviations =
    [
        ("ș.a.m.d.", "și așa mai departe"),
        ("ș.a.", "și altele"),
        ("etc.", "etcetera"),
        ("str.", "strada"),
        ("nr.", "numărul"),
        ("bl.", "blocul"),
        ("sc.", "scara"),
        ("et.", "etajul"),
        ("ap.", "apartamentul"),
        ("jud.", "județul"),
        ("mun.", "municipiul"),
        ("sect.", "sectorul"),
        ("bd.", "bulevardul"),
        ("bld.", "bulevardul"),
        ("cal.", "calea"),
        ("șos.", "șoseaua"),
        ("sos.", "șoseaua"),
        ("dl.", "domnul"),
        ("dna.", "doamna"),
        ("dra.", "domnișoara"),
        ("dvs.", "dumneavoastră"),
        ("prof.", "profesor"),
        ("dr.", "doctor"),
        ("ing.", "inginer"),
        ("tel.", "telefon"),
        ("pag.", "pagina"),
        ("vol.", "volumul"),
        ("cca.", "circa"),
        ("aprox.", "aproximativ"),
        ("art.", "articolul"),
        ("alin.", "alineatul"),
        ("lit.", "litera"),
    ];

    /// <summary>Abbreviations written with hyphens or without a dot.</summary>
    public static readonly (string Abbreviation, string Expansion)[] HyphenAbbreviations =
    [
        ("d-l", "domnul"),
        ("d-lui", "domnului"),
        ("d-na", "doamna"),
        ("d-nei", "doamnei"),
        ("d-ra", "domnișoara"),
        ("d-voastră", "dumneavoastră"),
        ("dvs", "dumneavoastră"),
    ];

    /// <summary>Units written after numbers: (symbol, singular, plural, gender).</summary>
    public static readonly (string Symbol, string Singular, string Plural, Gender Gender)[] Units =
    [
        ("km/h", "kilometru pe oră", "kilometri pe oră", Gender.Masculine),
        ("km", "kilometru", "kilometri", Gender.Masculine),
        ("kg", "kilogram", "kilograme", Gender.Neuter),
        ("mg", "miligram", "miligrame", Gender.Neuter),
        ("cm", "centimetru", "centimetri", Gender.Masculine),
        ("mm", "milimetru", "milimetri", Gender.Masculine),
        ("ml", "mililitru", "mililitri", Gender.Masculine),
        ("°C", "grad Celsius", "grade Celsius", Gender.Neuter),
        ("°", "grad", "grade", Gender.Neuter),
    ];
}
