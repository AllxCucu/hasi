using Hasi.Core.Localization;
using Hasi.Modules.Clock;

namespace Hasi.Transcripts.Tests;

public class LegalHolidaysTests
{
    private static readonly LegalHolidays Shipped = LegalHolidays.Load(LegalHolidays.DefaultPath);

    private static DateOnly D(string date) => DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture);

    // Verified dates only (plan-faza-1.md §0; 2026 against calendar.patriarhia.ro).
    [Theory]
    [InlineData(2024, "2024-05-05")]
    [InlineData(2025, "2025-04-20")]
    [InlineData(2026, "2026-04-12")]
    [InlineData(2027, "2027-05-02")]
    [InlineData(2028, "2028-04-16")]
    [InlineData(2029, "2029-04-08")]
    [InlineData(2030, "2030-04-28")]
    public void Orthodox_easter_matches_the_published_dates(int year, string sunday)
    {
        Assert.Equal(D(sunday), OrthodoxEaster.Sunday(year));
        Assert.Equal(DayOfWeek.Sunday, OrthodoxEaster.Sunday(year).DayOfWeek);
    }

    [Fact]
    public void Orthodox_easter_is_a_spring_sunday_in_every_supported_year_and_refused_outside()
    {
        for (var year = OrthodoxEaster.FirstYear; year <= OrthodoxEaster.LastYear; year++)
        {
            var sunday = OrthodoxEaster.Sunday(year);
            Assert.Equal(DayOfWeek.Sunday, sunday.DayOfWeek);
            Assert.InRange(sunday, new DateOnly(year, 4, 3), new DateOnly(year, 5, 9));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => OrthodoxEaster.Sunday(1899));
        Assert.Throws<ArgumentOutOfRangeException>(() => OrthodoxEaster.Sunday(2100));
    }

    [Fact]
    public void Shipped_file_has_no_errors_and_every_name_is_in_the_resources()
    {
        Assert.Empty(Shipped.Errors);
        Assert.Equal(17, Shipped.Rules.Count);
        var keys = JsonLocalizer.LoadRomanian(Path.Combine(AppContext.BaseDirectory, "Resources", "ro.json")).Keys.ToHashSet();
        Assert.All(Shipped.Rules, r => Assert.Contains(r.Key, keys));
        Assert.Equal(2017, Shipped.FirstYear);
        Assert.Equal(2099, Shipped.LastYear);
    }

    [Fact]
    public void Year_2026_matches_the_patriarchate_list()
    {
        (string Date, string[] Keys)[] expected =
        [
            ("2026-01-01", ["holiday.newYear1"]), ("2026-01-02", ["holiday.newYear2"]),
            ("2026-01-06", ["holiday.epiphany"]), ("2026-01-07", ["holiday.saintJohn"]),
            ("2026-01-24", ["holiday.unification"]), ("2026-04-10", ["holiday.goodFriday"]),
            ("2026-04-12", ["holiday.easter1"]), ("2026-04-13", ["holiday.easter2"]),
            ("2026-05-01", ["holiday.labourDay"]), ("2026-05-31", ["holiday.pentecost1"]),
            ("2026-06-01", ["holiday.childrensDay", "holiday.pentecost2"]), ("2026-08-15", ["holiday.dormition"]),
            ("2026-11-30", ["holiday.saintAndrew"]), ("2026-12-01", ["holiday.nationalDay"]),
            ("2026-12-25", ["holiday.christmas1"]), ("2026-12-26", ["holiday.christmas2"]),
        ];
        var actual = Shipped.In(2026);
        Assert.Equal(expected.Select(e => D(e.Date)), actual.Select(a => a.Date));
        Assert.Equal(expected.Select(e => string.Join(",", e.Keys)), actual.Select(a => string.Join(",", a.Keys)));
    }

    [Theory]
    [InlineData(2024, "2024-05-03", "2024-05-05", "2024-05-06", "2024-06-23", "2024-06-24")]
    [InlineData(2025, "2025-04-18", "2025-04-20", "2025-04-21", "2025-06-08", "2025-06-09")]
    [InlineData(2027, "2027-04-30", "2027-05-02", "2027-05-03", "2027-06-20", "2027-06-21")]
    [InlineData(2028, "2028-04-14", "2028-04-16", "2028-04-17", "2028-06-04", "2028-06-05")]
    [InlineData(2029, "2029-04-06", "2029-04-08", "2029-04-09", "2029-05-27", "2029-05-28")]
    [InlineData(2030, "2030-04-26", "2030-04-28", "2030-04-29", "2030-06-16", "2030-06-17")]
    public void Movable_holidays_follow_orthodox_easter(int year, string goodFriday, string easter1, string easter2,
        string pentecost1, string pentecost2)
    {
        Assert.Equal(["holiday.goodFriday"], Shipped.On(D(goodFriday)));
        Assert.Equal(["holiday.easter1"], Shipped.On(D(easter1)));
        Assert.Equal(["holiday.easter2"], Shipped.On(D(easter2)));
        Assert.Equal(["holiday.pentecost1"], Shipped.On(D(pentecost1)));
        Assert.Equal(["holiday.pentecost2"], Shipped.On(D(pentecost2)));
        Assert.All(Shipped.In(year), h => Assert.Equal(year, h.Date.Year));
    }

    [Fact]
    public void Each_rule_starts_in_its_year()
    {
        Assert.Empty(Shipped.On(D("2023-01-06")));
        Assert.Empty(Shipped.On(D("2023-01-07")));
        Assert.Equal(["holiday.epiphany"], Shipped.On(D("2024-01-06")));
        Assert.Equal(["holiday.saintJohn"], Shipped.On(D("2024-01-07")));
        Assert.Equal(["holiday.childrensDay"], Shipped.On(D("2017-06-01")));
        Assert.DoesNotContain(Shipped.In(2017), h => h.Keys.Contains("holiday.goodFriday"));
        Assert.Contains(Shipped.In(2018), h => h.Keys.Contains("holiday.goodFriday"));
    }

    [Fact]
    public void Years_before_2017_and_after_2099_are_unknown_not_holiday_free()
    {
        Assert.False(Shipped.Knows(2016));
        Assert.True(Shipped.Knows(2017));
        Assert.True(Shipped.Knows(2099));
        Assert.False(Shipped.Knows(2100));
        Assert.Empty(Shipped.In(2016));
        Assert.Empty(Shipped.On(D("2016-12-25")));
        Assert.NotEmpty(Shipped.In(2099));
    }

    [Theory]
    [InlineData("holiday.x | fix 02-30 | 2017")]
    [InlineData("holiday.x | fix 02-29 | 2017")]
    [InlineData("holiday.x | fix 1-1 | 2017")]
    [InlineData("holiday.x | paste +70 | 2017")]
    [InlineData("holiday.x | paste unu | 2017")]
    [InlineData("holiday.x | lunar 05 | 2017")]
    [InlineData("holiday.x | fix 05-01 | 1850")]
    [InlineData("holiday.x | fix 05-01")]
    [InlineData(" | fix 05-01 | 2017")]
    public void Malformed_lines_are_skipped_and_reported(string line)
    {
        var holidays = LegalHolidays.Parse(["# comentariu", "", "holiday.labourDay | fix 05-01 | 2017", line]);
        Assert.Single(holidays.Rules);
        Assert.Equal(["line 4: " + line.Trim()], holidays.Errors);
    }

    [Fact]
    public void A_missing_file_knows_no_year()
    {
        var holidays = LegalHolidays.Load(Path.Combine(AppContext.BaseDirectory, "nu-exista.txt"));
        Assert.Single(holidays.Errors);
        Assert.False(holidays.Knows(2026));
        Assert.Empty(holidays.On(D("2026-05-01")));
    }
}
