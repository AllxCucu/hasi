using System.Text;
using Hasi.Core.Localization;

namespace Hasi.Text.Ro;

/// <summary>Grammatical gender of the noun a number counts. Neuter plurals take feminine forms ("două dosare").</summary>
public enum Gender
{
    Masculine,
    Feminine,
    Neuter,
}

/// <summary>Romanian numbers in words: cardinals with gender, ordinals, digit-by-digit reading.</summary>
public static class RomanianNumbers
{
    private static readonly string[] Teens =
    [
        "zece", "unsprezece", "doisprezece", "treisprezece", "paisprezece", "cincisprezece",
        "șaisprezece", "șaptesprezece", "optsprezece", "nouăsprezece",
    ];

    private static readonly string[] Tens =
        ["", "zece", "douăzeci", "treizeci", "patruzeci", "cincizeci", "șaizeci", "șaptezeci", "optzeci", "nouăzeci"];

    private static readonly string[] Digits =
        ["zero", "unu", "doi", "trei", "patru", "cinci", "șase", "șapte", "opt", "nouă"];

    /// <summary>
    /// The number in words. <paramref name="beforeNoun"/> turns a lone 1 into "un"/"o" ("un leu", "o oră");
    /// in compounds it stays "unu"/"una" ("douăzeci și unu de lei").
    /// </summary>
    public static string Cardinal(long n, Gender gender = Gender.Masculine, bool beforeNoun = false)
    {
        if (n < 0) return "minus " + Cardinal(-n, gender, beforeNoun);
        if (n == 0) return "zero";
        if (n == 1 && beforeNoun) return gender == Gender.Feminine ? "o" : "un";

        var parts = new List<string>();
        var billions = n / 1_000_000_000;
        var millions = n / 1_000_000 % 1000;
        var thousands = n / 1000 % 1000;
        var rest = n % 1000;

        if (billions > 0) parts.Add(Scaled(billions, "un miliard", "miliarde", Gender.Neuter));
        if (millions > 0) parts.Add(Scaled(millions, "un milion", "milioane", Gender.Neuter));
        if (thousands > 0) parts.Add(Scaled(thousands, "o mie", "mii", Gender.Feminine));
        if (rest > 0) parts.Add(Below1000(rest, gender));
        return string.Join(' ', parts);
    }

    /// <summary>"de" goes between a number and its noun from 20 on, except after 1-19 in the last two digits.</summary>
    public static bool NeedsDe(long n) => RomanianPlural.Categorize(n) == PluralCategory.Other;

    /// <summary>Number and noun together: "un leu", "doi lei", "douăzeci de lei".</summary>
    public static string Count(long n, string singular, string plural, Gender gender) =>
        n == 1
            ? $"{Cardinal(1, gender, beforeNoun: true)} {singular}"
            : $"{Cardinal(n, gender)}{(NeedsDe(n) ? " de" : "")} {plural}";

    /// <summary>"al doilea", "al unsprezecelea" (without "al").</summary>
    public static string OrdinalMasculine(long n)
    {
        if (n == 1) return "întâilea";
        var words = Cardinal(n);
        return words switch
        {
            _ when words.EndsWith("unu", StringComparison.Ordinal) => words + "lea",
            _ when words.EndsWith("doi", StringComparison.Ordinal) => words + "lea",
            _ when words.EndsWith("trei", StringComparison.Ordinal) => words + "lea",
            _ when words.EndsWith("patru", StringComparison.Ordinal) => words + "lea",
            _ when words.EndsWith("opt", StringComparison.Ordinal) => words + "ulea",
            _ when words.EndsWith("nouă", StringComparison.Ordinal) => words + "lea",
            _ when words.EndsWith("sută", StringComparison.Ordinal) => words + "lea",
            _ when words.EndsWith("sute", StringComparison.Ordinal) => words + "lea",
            _ when words.EndsWith("mie", StringComparison.Ordinal) => words[..^3] + "miilea",
            _ when words.EndsWith("mii", StringComparison.Ordinal) => words + "lea",
            _ when words.EndsWith("zeci", StringComparison.Ordinal) => words + "lea",
            _ => words + "lea", // cinci, șase, șapte, zece, -sprezece: cincilea, șaselea, zecelea ...
        };
    }

    /// <summary>"a doua", "a unsprezecea" (without "a").</summary>
    public static string OrdinalFeminine(long n)
    {
        if (n == 1) return "întâia";
        var words = Cardinal(n, Gender.Feminine);
        static string Replace(string text, string end, string with) => text[..^end.Length] + with;
        return words switch
        {
            _ when words.EndsWith("două", StringComparison.Ordinal) => Replace(words, "două", "doua"),
            _ when words.EndsWith("una", StringComparison.Ordinal) => words,
            _ when words.EndsWith("trei", StringComparison.Ordinal) => words + "a",
            _ when words.EndsWith("patru", StringComparison.Ordinal) => Replace(words, "patru", "patra"),
            _ when words.EndsWith("cinci", StringComparison.Ordinal) => Replace(words, "cinci", "cincea"),
            _ when words.EndsWith("șase", StringComparison.Ordinal) => Replace(words, "șase", "șasea"),
            _ when words.EndsWith("șapte", StringComparison.Ordinal) => Replace(words, "șapte", "șaptea"),
            _ when words.EndsWith("opt", StringComparison.Ordinal) => words + "a",
            _ when words.EndsWith("nouă", StringComparison.Ordinal) => Replace(words, "nouă", "noua"),
            _ when words.EndsWith("zece", StringComparison.Ordinal) => Replace(words, "zece", "zecea"),
            _ when words.EndsWith("zeci", StringComparison.Ordinal) => Replace(words, "zeci", "zecea"),
            _ when words.EndsWith("sută", StringComparison.Ordinal) => Replace(words, "sută", "suta"),
            _ when words.EndsWith("sute", StringComparison.Ordinal) => Replace(words, "sute", "suta"),
            _ when words.EndsWith("mie", StringComparison.Ordinal) => Replace(words, "mie", "mia"),
            _ when words.EndsWith("mii", StringComparison.Ordinal) => words + "a",
            _ => words + "a",
        };
    }

    /// <summary>Phone numbers, codes: "0722" → "zero șapte doi doi".</summary>
    public static string DigitByDigit(string digits)
    {
        var text = new StringBuilder();
        foreach (var c in digits)
        {
            if (!char.IsAsciiDigit(c)) continue;
            if (text.Length > 0) text.Append(' ');
            text.Append(Digits[c - '0']);
        }
        return text.ToString();
    }

    private static string Scaled(long count, string one, string plural, Gender gender) =>
        count == 1 ? one : $"{Below1000(count, gender)}{(NeedsDe(count) ? " de" : "")} {plural}";

    private static string Below1000(long n, Gender gender)
    {
        var hundreds = n / 100;
        var rest = n % 100;
        var parts = new List<string>(2);
        if (hundreds == 1) parts.Add("o sută");
        else if (hundreds > 1) parts.Add($"{Below100(hundreds, Gender.Feminine)} sute");
        if (rest > 0) parts.Add(Below100(rest, gender));
        return string.Join(' ', parts);
    }

    private static string Below100(long n, Gender gender)
    {
        var feminine = gender != Gender.Masculine;
        if (n < 10) return Unit(n, gender);
        if (n < 20) return n == 12 && feminine ? "douăsprezece" : Teens[n - 10];
        var tens = Tens[n / 10];
        var unit = n % 10;
        return unit == 0 ? tens : $"{tens} și {Unit(unit, gender)}";
    }

    private static string Unit(long n, Gender gender) => (n, gender) switch
    {
        (1, Gender.Feminine) => "una",
        (2, Gender.Feminine or Gender.Neuter) => "două",
        _ => Digits[n],
    };
}
