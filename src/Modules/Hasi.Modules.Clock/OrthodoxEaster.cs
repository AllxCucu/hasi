namespace Hasi.Modules.Clock;

/// <summary>Orthodox Easter Sunday as a civil (Gregorian) date.</summary>
public static class OrthodoxEaster
{
    public const int FirstYear = 1900;
    public const int LastYear = 2099;

    /// <summary>
    /// Meeus' algorithm for the Julian calendar, then the 13 days between the Julian and Gregorian calendars,
    /// which is constant only from 1 March 1900 to 28 February 2100.
    /// </summary>
    public static DateOnly Sunday(int year)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, FirstYear);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, LastYear);
        var a = year % 4;
        var b = year % 7;
        var c = year % 19;
        var d = (19 * c + 15) % 30;
        var e = (2 * a + 4 * b - d + 34) % 7;
        var month = (d + e + 114) / 31;
        var day = (d + e + 114) % 31 + 1;
        return new DateOnly(year, month, day).AddDays(13);
    }
}
