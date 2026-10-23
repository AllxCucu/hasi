using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Hasi.Core.Output;

namespace Hasi.Text.Ro;

/// <summary>
/// Turns written Romanian into what should be spoken: correct diacritics, abbreviations, amounts in
/// lei, dates, times, percentages, ordinals and numbers in words with the right gender.
/// Only speech is normalized; the screen keeps the original text.
/// </summary>
public sealed partial class RomanianNormalizer : ITextNormalizer
{
    private readonly Func<string, string>[] _stages;
    private readonly NounGenderLexicon _nouns;
    private readonly Action<string>? _onGuessedNoun;

    /// <summary>Without acronyms and noun dictionary: built-in lists only.</summary>
    public RomanianNormalizer()
        : this(AcronymLexicon.Empty)
    {
    }

    /// <param name="nouns">Noun genders; the built-in lists when null.</param>
    /// <param name="onGuessedNoun">
    /// Called with a word whose gender had to be guessed from its ending where it changes what is said
    /// (1, 2, 12, 21, 22 …), so the tester can report it (diagnostic log: <c>LEXICON?</c>).
    /// </param>
    public RomanianNormalizer(AcronymLexicon acronyms, NounGenderLexicon? nouns = null, Action<string>? onGuessedNoun = null)
    {
        ArgumentNullException.ThrowIfNull(acronyms);
        _nouns = nouns ?? NounGenderLexicon.BuiltIn;
        _onGuessedNoun = onGuessedNoun;
        _stages =
        [
            FixDiacritics,
            ExpandEmailsAndWebAddresses,
            acronyms.Expand,
            ExpandAbbreviations,
            ExpandPhoneNumbers,
            ExpandMoney,
            ExpandUnits,
            ExpandDates,
            ExpandTimes,
            ExpandPercentages,
            ExpandOrdinals,
            ExpandVersionNumbers,
            ExpandDecimals,
            ExpandIntegers,
            ExpandSymbols,
            CollapseSpaces,
        ];
    }

    public string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var stage in _stages) text = stage(text);
        return text;
    }

    // ---- Diacritics -------------------------------------------------------------------------

    private static readonly (string Wrong, string Right)[] Mojibake =
    [
        ("Äƒ", "ă"), ("Ä‚", "Ă"), ("Ã¢", "â"), ("Ã‚", "Â"), ("Ã®", "î"), ("ÃŽ", "Î"),
        ("È™", "ș"), ("È˜", "Ș"), ("È›", "ț"), ("Èš", "Ț"),
        ("ÅŸ", "ș"), ("Åž", "Ș"), ("Å£", "ț"), ("Å¢", "Ț"),
    ];

    /// <summary>ş ţ (cedilla) → ș ț (comma below); common UTF-8 mojibake; ã used for ă.</summary>
    internal static string FixDiacritics(string text)
    {
        text = text.Normalize(NormalizationForm.FormC);
        if (text.AsSpan().IndexOfAny("ÃÄÈÅ") >= 0)
        {
            foreach (var (wrong, right) in Mojibake) text = text.Replace(wrong, right, StringComparison.Ordinal);
        }
        return text
            .Replace('ş', 'ș').Replace('Ş', 'Ș')
            .Replace('ţ', 'ț').Replace('Ţ', 'Ț')
            .Replace('ã', 'ă').Replace('Ã', 'Ă');
    }

    // ---- E-mail and web addresses -------------------------------------------------------------

    [GeneratedRegex(@"(?<![\w.])([\w.+-]+)@([\w-]+(?:\.[\w-]+)+)", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?<![\w@])(?:https?://)?(?:www\.)?[\w-]+(?:\.[\w-]+)*\.(?:ro|com|net|org|eu|md|info)\b(?:/\S*)?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex WebPattern();

    private static string ExpandEmailsAndWebAddresses(string text)
    {
        text = EmailPattern().Replace(text, m =>
            $"{SpellDots(m.Groups[1].Value)} a rond {SpellDots(m.Groups[2].Value)}");
        return WebPattern().Replace(text, m => SpellDots(Regex.Replace(m.Value, "^https?://", "", RegexOptions.IgnoreCase)));
    }

    private static string SpellDots(string address) =>
        address.Replace(".", " punct ", StringComparison.Ordinal)
               .Replace("/", " bară ", StringComparison.Ordinal)
               .Trim();

    // ---- Abbreviations ----------------------------------------------------------------------

    private static readonly Regex AbbreviationPattern = new(
        @"(?<![\p{L}.])(" + string.Join('|', Lexicon.Abbreviations.Select(a => Regex.Escape(a.Abbreviation))) + @")(?=\s|\d|$|[,;:)])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex HyphenAbbreviationPattern = new(
        @"(?<![\p{L}-])(" + string.Join('|', Lexicon.HyphenAbbreviations.Select(a => Regex.Escape(a.Abbreviation))) + @")(?![\p{L}-])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static string ExpandAbbreviations(string text)
    {
        text = AbbreviationPattern.Replace(text, m => Lookup(Lexicon.Abbreviations, m.Value));
        return HyphenAbbreviationPattern.Replace(text, m => Lookup(Lexicon.HyphenAbbreviations, m.Value));
    }

    private static string Lookup((string Abbreviation, string Expansion)[] table, string found) =>
        table.First(a => a.Abbreviation.Equals(found, StringComparison.OrdinalIgnoreCase)).Expansion;

    // ---- Phone numbers ----------------------------------------------------------------------

    [GeneratedRegex(@"(?<![\d+])(\+40[ .-]?|0)(\d{2,3})[ .-]?(\d{3})[ .-]?(\d{3})(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

    /// <summary>Phone numbers are read digit by digit, with a pause between groups.</summary>
    private static string ExpandPhoneNumbers(string text) => PhonePattern().Replace(text, m =>
    {
        var prefix = m.Groups[1].Value.StartsWith('+') ? "plus patru zero" : "zero";
        return $"{prefix} {RomanianNumbers.DigitByDigit(m.Groups[2].Value)}, " +
               $"{RomanianNumbers.DigitByDigit(m.Groups[3].Value)}, {RomanianNumbers.DigitByDigit(m.Groups[4].Value)}";
    });

    // ---- Money ------------------------------------------------------------------------------

    private const string Amount = @"(?<int>\d{1,3}(?:\.\d{3})+|\d+)(?:[,.](?<frac>\d{1,2}))?(?!\d)";

    [GeneratedRegex(@"(?<![\d.,])" + Amount + @"\s*(?<cur>lei|leu|ron|euro|eur|€)(?![\p{L}])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex MoneyPattern();

    [GeneratedRegex(@"€\s*" + Amount, RegexOptions.CultureInvariant)]
    private static partial Regex EuroFirstPattern();

    private static string ExpandMoney(string text)
    {
        text = EuroFirstPattern().Replace(text, m => SayAmount(m, euro: true));
        return MoneyPattern().Replace(text, m =>
        {
            var currency = m.Groups["cur"].Value.ToLowerInvariant();
            return SayAmount(m, euro: currency is "euro" or "eur" or "€");
        });
    }

    private static string SayAmount(Match m, bool euro)
    {
        var whole = ParseInteger(m.Groups["int"].Value);
        var fraction = m.Groups["frac"].Success ? m.Groups["frac"].Value : "";
        var cents = fraction.Length == 0 ? 0 : int.Parse(fraction.PadRight(2, '0'), CultureInfo.InvariantCulture);

        var main = euro
            ? RomanianNumbers.Count(whole, "euro", "euro", Gender.Masculine)
            : RomanianNumbers.Count(whole, "leu", "lei", Gender.Masculine);
        if (cents == 0) return main;
        var small = euro
            ? RomanianNumbers.Count(cents, "cent", "cenți", Gender.Masculine)
            : RomanianNumbers.Count(cents, "ban", "bani", Gender.Masculine);
        return whole == 0 ? small : $"{main} și {small}";
    }

    // ---- Units ------------------------------------------------------------------------------

    private static readonly Regex UnitPattern = new(
        @"(?<neg>(?<=^|\s)-)?(?<![\d.,])(?<int>\d+)(?:,(?<frac>\d+))?\s*(?<unit>" +
        string.Join('|', Lexicon.Units.Select(u => Regex.Escape(u.Symbol))) + @")(?![\p{L}/])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static string ExpandUnits(string text) => UnitPattern.Replace(text, m =>
    {
        var unit = Lexicon.Units.First(u => u.Symbol == m.Groups["unit"].Value);
        var whole = ParseInteger(m.Groups["int"].Value);
        var sign = m.Groups["neg"].Success ? "minus " : "";
        if (m.Groups["frac"].Success)
        {
            return $"{sign}{RomanianNumbers.Cardinal(whole)} virgulă {Fraction(m.Groups["frac"].Value)} {unit.Plural}";
        }
        return sign + RomanianNumbers.Count(whole, unit.Singular, unit.Plural, unit.Gender);
    });

    // ---- Dates ------------------------------------------------------------------------------

    [GeneratedRegex(@"(?<![\d.])(\d{1,2})[./](\d{1,2})[./](\d{4})(?![\d.]\d)", RegexOptions.CultureInvariant)]
    private static partial Regex NumericDatePattern();

    [GeneratedRegex(@"(?<!\d)(\d{4})-(\d{2})-(\d{2})(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex IsoDatePattern();

    [GeneratedRegex(@"(?<![\d.,])(\d{1,2})\s+(ianuarie|februarie|martie|aprilie|mai|iunie|iulie|august|septembrie|octombrie|noiembrie|decembrie)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TextDatePattern();

    private static string ExpandDates(string text)
    {
        text = NumericDatePattern().Replace(text, m =>
            SayDate(m.Value, m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value));
        text = IsoDatePattern().Replace(text, m =>
            SayDate(m.Value, m.Groups[3].Value, m.Groups[2].Value, m.Groups[1].Value));
        return TextDatePattern().Replace(text, m =>
        {
            var day = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            return day is >= 1 and <= 31 ? $"{DayWords(day)} {m.Groups[2].Value.ToLowerInvariant()}" : m.Value;
        });
    }

    private static string SayDate(string original, string day, string month, string year)
    {
        var d = int.Parse(day, CultureInfo.InvariantCulture);
        var mo = int.Parse(month, CultureInfo.InvariantCulture);
        if (d is < 1 or > 31 || mo is < 1 or > 12) return original;
        return $"{DayWords(d)} {Lexicon.Months[mo - 1]} {RomanianNumbers.Cardinal(long.Parse(year, CultureInfo.InvariantCulture))}";
    }

    /// <summary>"1 martie" is read "întâi martie"; other days use the plain cardinal.</summary>
    private static string DayWords(int day) => day == 1 ? "întâi" : RomanianNumbers.Cardinal(day);

    // ---- Times ------------------------------------------------------------------------------

    [GeneratedRegex(@"(?<![\d:.,])(?<ora>\b(?:ora|orele)\s+)?(?<h>[01]?\d|2[0-3]):(?<m>[0-5]\d)(?![\d:])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ColonTimePattern();

    [GeneratedRegex(@"(?<ora>\b(?:ora|orele)\s+)(?<h>[01]?\d|2[0-3])\.(?<m>[0-5]\d)(?![\d.])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex DotTimePattern();

    private static string ExpandTimes(string text)
    {
        text = ColonTimePattern().Replace(text, SayTime);
        return DotTimePattern().Replace(text, SayTime);
    }

    private static string SayTime(Match m)
    {
        var hour = int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture);
        var lead = m.Groups["ora"].Success ? m.Groups["ora"].Value : "ora ";
        var hourWords = hour switch
        {
            1 or 21 => RomanianNumbers.Cardinal(hour),
            _ => RomanianNumbers.Cardinal(hour, Gender.Feminine),
        };
        // Full hours are said without "fix" (tester's choice, 2026-10-23): "ora paisprezece".
        return minute == 0
            ? $"{lead}{hourWords}"
            : $"{lead}{hourWords} și {RomanianNumbers.Count(minute, "minut", "minute", Gender.Neuter)}";
    }

    // ---- Percentages ------------------------------------------------------------------------

    [GeneratedRegex(@"(?<![\d.,])(?<int>\d+)(?:,(?<frac>\d+))?\s*%", RegexOptions.CultureInvariant)]
    private static partial Regex PercentPattern();

    private static string ExpandPercentages(string text) => PercentPattern().Replace(text, m =>
    {
        var whole = ParseInteger(m.Groups["int"].Value);
        var words = m.Groups["frac"].Success
            ? $"{RomanianNumbers.Cardinal(whole)} virgulă {Fraction(m.Groups["frac"].Value)}"
            : whole == 1 ? "unu" : RomanianNumbers.Cardinal(whole, Gender.Feminine);
        return $"{words} la sută";
    });

    // ---- Ordinals ---------------------------------------------------------------------------

    [GeneratedRegex(@"\b(?<art>al|a)\s+(?<n>\d+)-(?<suf>lea|a)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex OrdinalPattern();

    private static string ExpandOrdinals(string text) => OrdinalPattern().Replace(text, m =>
    {
        var n = ParseInteger(m.Groups["n"].Value);
        var article = m.Groups["art"].Value;
        return article.Equals("al", StringComparison.OrdinalIgnoreCase)
            ? $"{article} {RomanianNumbers.OrdinalMasculine(n)}"
            : $"{article} {RomanianNumbers.OrdinalFeminine(n)}";
    });

    // ---- Version-like numbers (0.1.0, 2.1) ---------------------------------------------------

    [GeneratedRegex(@"(?<![\d.,])\d+(?:\.\d+)+(?!\d|,\d|\.\d)", RegexOptions.CultureInvariant)]
    private static partial Regex DottedPattern();

    /// <summary>"0.1.0" → "zero punct unu punct zero". Thousands ("12.500") are left for integers.</summary>
    private static string ExpandVersionNumbers(string text) => DottedPattern().Replace(text, m =>
    {
        var parts = m.Value.Split('.');
        if (parts.Skip(1).All(p => p.Length == 3) && parts[0].Length <= 3 && parts[0][0] != '0') return m.Value;
        return string.Join(" punct ", parts.Select(p => RomanianNumbers.Cardinal(ParseInteger(p))));
    });

    // ---- Decimals ---------------------------------------------------------------------------

    [GeneratedRegex(@"(?<![\d.,])(?<int>\d+),(?<frac>\d+)(?!\d|,\d)", RegexOptions.CultureInvariant)]
    private static partial Regex DecimalPattern();

    private static string ExpandDecimals(string text) => DecimalPattern().Replace(text, m =>
        $"{RomanianNumbers.Cardinal(ParseInteger(m.Groups["int"].Value))} virgulă {Fraction(m.Groups["frac"].Value)}");

    /// <summary>Digits after the comma: leading zeros are read one by one ("0,05" → "zero virgulă zero cinci").</summary>
    private static string Fraction(string digits)
    {
        var zeros = digits.Length - digits.TrimStart('0').Length;
        var rest = digits.TrimStart('0');
        var words = Enumerable.Repeat("zero", zeros).ToList();
        if (rest.Length > 0) words.Add(RomanianNumbers.Cardinal(ParseInteger(rest)));
        return string.Join(' ', words);
    }

    // ---- Integers ---------------------------------------------------------------------------

    [GeneratedRegex(@"(?<![\d\p{L}.,])(-)?(\d{1,3}(?:\.\d{3})+|\d+)(?![\d\p{L}])", RegexOptions.CultureInvariant)]
    private static partial Regex IntegerPattern();

    [GeneratedRegex(@"^\s*(\p{L}[\p{L}-]*)", RegexOptions.CultureInvariant)]
    private static partial Regex NextWordPattern();

    private string ExpandIntegers(string text) => IntegerPattern().Replace(text, m =>
    {
        var digits = m.Groups[2].Value;
        var negative = m.Groups[1].Success && (m.Index == 0 || char.IsWhiteSpace(text[m.Index - 1]));
        var sign = negative ? "minus " : m.Groups[1].Value;

        // Codes, account numbers and numbers with leading zeros are read digit by digit.
        var grouped = digits.Contains('.', StringComparison.Ordinal);
        if (!grouped && ((digits.Length >= 2 && digits[0] == '0') || digits.Length >= 10))
        {
            return sign + RomanianNumbers.DigitByDigit(digits);
        }

        var n = ParseInteger(digits);
        var after = text[(m.Index + m.Length)..];
        var (word, noun, hasDe) = CountedNoun(after);
        if (!noun.IsNoun) return sign + RomanianNumbers.Cardinal(n);
        if (noun.Guessed && GenderChangesWords(n)) _onGuessedNoun?.Invoke(word);
        var words = RomanianNumbers.Cardinal(n, noun.Gender, beforeNoun: true);
        return sign + words + (RomanianNumbers.NeedsDe(n) && !hasDe ? " de" : "");
    });

    /// <summary>The noun after a number ("2 ore", "20 de lei").</summary>
    private (string Word, NounLookup Noun, bool HasDe) CountedNoun(string after)
    {
        var first = NextWordPattern().Match(after);
        if (!first.Success) return ("", NounLookup.NotANoun, false);
        var word = first.Groups[1].Value;
        if (word.Equals("de", StringComparison.OrdinalIgnoreCase))
        {
            var second = NextWordPattern().Match(after[first.Length..]);
            return second.Success
                ? (second.Groups[1].Value, _nouns.Find(second.Groups[1].Value), true)
                : ("", NounLookup.NotANoun, true);
        }
        return (word, _nouns.Find(word), false);
    }

    /// <summary>Only 1, 2 and numbers ending in them (except 11) have masculine and feminine forms.</summary>
    private static bool GenderChangesWords(long n) => (n % 10 == 1 && n % 100 != 11) || n % 10 == 2;

    // ---- Symbols ----------------------------------------------------------------------------

    [GeneratedRegex(@"\s*&\s*", RegexOptions.CultureInvariant)]
    private static partial Regex AmpersandPattern();

    [GeneratedRegex(@"(?<=\s|^)\+(?=\s)|(?<=\p{L}|\d)\s*\+\s*(?=\p{L}|\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PlusPattern();

    [GeneratedRegex(@"\s*=\s*", RegexOptions.CultureInvariant)]
    private static partial Regex EqualsPattern();

    private static string ExpandSymbols(string text)
    {
        text = AmpersandPattern().Replace(text, " și ");
        text = PlusPattern().Replace(text, " plus ");
        text = EqualsPattern().Replace(text, " egal ");
        return text.Replace("#", " diez ", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"[ \t]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex SpacesPattern();

    private static string CollapseSpaces(string text) => SpacesPattern().Replace(text, " ").Trim();

    private static long ParseInteger(string digits) =>
        long.Parse(digits.Replace(".", "", StringComparison.Ordinal), CultureInfo.InvariantCulture);
}
