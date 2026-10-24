using System.Globalization;

namespace Hasi.Modules.Clock;

/// <summary>One line of <c>sarbatori-legale.txt</c>: a fixed day, or a number of days from Orthodox Easter.</summary>
/// <param name="Key">Resource key of the spoken name.</param>
public sealed record HolidayRule(string Key, int FromYear, int? Month = null, int? Day = null, int? DaysFromEaster = null)
{
    public DateOnly DateIn(int year) => DaysFromEaster is { } days
        ? OrthodoxEaster.Sunday(year).AddDays(days)
        : new DateOnly(year, Month!.Value, Day!.Value);
}

/// <summary>The days of one year that are legal holidays, with the keys of their names in file order.</summary>
public sealed record HolidayDate(DateOnly Date, IReadOnlyList<string> Keys);

/// <summary>
/// Romanian legal holidays (Codul muncii, art. 139), from the editable rules in <c>Data/sarbatori-legale.txt</c>.
/// Years before the earliest rule are unknown, not holiday-free.
/// </summary>
public sealed class LegalHolidays
{
    public const string DefaultFileName = "sarbatori-legale.txt";

    private const string FixedRule = "fix";
    private const string EasterRule = "paste";

    private LegalHolidays(IReadOnlyList<HolidayRule> rules, IReadOnlyList<string> errors)
    {
        Rules = rules;
        Errors = errors;
    }

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "Data", DefaultFileName);

    public IReadOnlyList<HolidayRule> Rules { get; }

    /// <summary>Lines that were skipped, with their line numbers.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>The first year every rule is known for; later than <see cref="LastYear"/> when there are no rules.</summary>
    public int FirstYear => Rules.Count == 0 ? LastYear + 1 : Math.Max(OrthodoxEaster.FirstYear, Rules.Min(r => r.FromYear));

    public int LastYear => OrthodoxEaster.LastYear;

    public bool Knows(int year) => year >= FirstYear && year <= LastYear;

    public static LegalHolidays Load(string path) => File.Exists(path)
        ? Parse(File.ReadAllLines(path))
        : new LegalHolidays([], [$"{path}: missing"]);

    public static LegalHolidays Parse(IEnumerable<string> lines)
    {
        var rules = new List<HolidayRule>();
        var errors = new List<string>();
        var number = 0;
        foreach (var raw in lines)
        {
            number++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (TryParse(line, out var rule)) rules.Add(rule);
            else errors.Add($"line {number}: {line}");
        }
        return new LegalHolidays(rules, errors);
    }

    /// <summary>The names' keys of the holidays on <paramref name="date"/>; empty when there are none or the year is unknown.</summary>
    public IReadOnlyList<string> On(DateOnly date) =>
        Knows(date.Year) ? [.. Rules.Where(r => r.FromYear <= date.Year && r.DateIn(date.Year) == date).Select(r => r.Key)] : [];

    /// <summary>Every holiday date of <paramref name="year"/>, in calendar order; empty when the year is unknown.</summary>
    public IReadOnlyList<HolidayDate> In(int year) => !Knows(year)
        ? []
        : [.. Rules.Where(r => r.FromYear <= year)
            .GroupBy(r => r.DateIn(year))
            .OrderBy(g => g.Key)
            .Select(g => new HolidayDate(g.Key, [.. g.Select(r => r.Key)]))];

    private static bool TryParse(string line, out HolidayRule rule)
    {
        rule = null!;
        var fields = line.Split('|', StringSplitOptions.TrimEntries);
        if (fields.Length < 3 || fields[0].Length == 0) return false;
        if (!int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var from)
            || from < OrthodoxEaster.FirstYear || from > OrthodoxEaster.LastYear)
        {
            return false;
        }

        var parts = fields[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;
        switch (parts[0])
        {
            case FixedRule when DateOnly.TryParseExact($"2000-{parts[1]}", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var day) && !(day.Month == 2 && day.Day == 29):
                rule = new HolidayRule(fields[0], from, Month: day.Month, Day: day.Day);
                return true;
            case EasterRule when int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var days)
                && Math.Abs(days) <= 60:
                rule = new HolidayRule(fields[0], from, DaysFromEaster: days);
                return true;
            default:
                return false;
        }
    }
}
