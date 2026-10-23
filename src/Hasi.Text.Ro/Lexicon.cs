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

    /// <summary>
    /// Words that follow numbers but are never the counted noun ("1 din 4", "2 este"): prepositions,
    /// conjunctions, pronouns, common verbs and adverbs. Needed because unknown lower-case words are
    /// otherwise treated as nouns (ADR 0005, fallback steps 3-5).
    /// </summary>
    private static readonly HashSet<string> NotNouns = new(StringComparer.OrdinalIgnoreCase)
    {
        // prepositions and conjunctions
        "din", "dintre", "de", "la", "pe", "în", "cu", "fără", "pentru", "prin", "spre", "către", "despre",
        "până", "după", "între", "sub", "peste", "lângă", "și", "sau", "ori", "dar", "iar", "ci", "ca", "că",
        "decât", "dacă", "deci", "când", "unde", "cum", "nici", "a", "al", "ale", "ai", "lui",
        // pronouns and determiners
        "eu", "tu", "el", "ea", "noi", "voi", "ei", "ele", "mă", "te", "se", "îl", "o", "îi", "le", "ne", "vă",
        "un", "una", "ce", "care", "cine", "acest", "această", "acești", "aceste", "fiecare", "tot", "toți",
        "toate", "alt", "altă", "alți", "alte",
        // verbs and adverbs that often follow a number
        "este", "e", "sunt", "era", "erau", "au", "am", "are", "avem", "aveți", "fost", "va", "vor", "vom",
        "poate", "pot", "trebuie", "mai", "foarte", "doar", "numai", "aici", "acolo", "acum", "atunci", "ori",
        "nu", "da", "bine", "plus", "minus", "virgulă", "punct", "ora", "orele",
    };

    /// <summary>
    /// Nouns usually followed by a number that is a label, not a quantity ("pagina 2", "etajul 1"),
    /// in case the dictionary lacks a form. The number is then read in its base form.
    /// </summary>
    private static readonly HashSet<string> LabelNouns = new(StringComparer.OrdinalIgnoreCase)
    {
        "pagina", "camera", "linia", "etajul", "sala", "numărul", "capitolul", "articolul", "punctul", "anexa",
        "blocul", "scara", "apartamentul", "sectorul", "cabinetul", "biroul", "ghișeul", "peronul", "poarta", "ușa",
        "clasa", "grupa", "lecția", "exercițiul", "întrebarea", "varianta", "nivelul", "canalul", "postul",
        "tabelul", "figura", "pasul", "autobuzul", "tramvaiul", "troleibuzul", "ruta", "terminalul", "intrarea",
        "rândul", "locul", "vagonul", "compartimentul", "anul", "secția", "salonul", "patul", "fereastra",
    };

    public static bool IsLabelNoun(string word) => LabelNouns.Contains(word);

    /// <summary>True for words that can never be the counted noun after a number.</summary>
    public static bool IsNeverACountedNoun(string word) =>
        NotNouns.Contains(word) || Months.Contains(word, StringComparer.OrdinalIgnoreCase);

    /// <summary>Gender from the built-in, hand-checked lists, or null.</summary>
    public static Gender? KnownGender(string word)
    {
        if (Masculine.Contains(word)) return Gender.Masculine;
        if (Feminine.Contains(word)) return Gender.Feminine;
        if (Neuter.Contains(word)) return Gender.Neuter;
        return null;
    }

    /// <summary>Gender from plural endings that are reliable in Romanian, or null.</summary>
    public static Gender? PluralEndingGender(string word)
    {
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
