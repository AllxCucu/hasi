using System.Globalization;

namespace Hasi.Core.Localization;

/// <summary>Date and time text shared by the shell and modules; names come from the resources.</summary>
public static class DateText
{
    public static string Time(DateTimeOffset time, ILocalizer localizer) =>
        localizer.Get("date.time", ("time", time.ToString("H:mm", CultureInfo.InvariantCulture)));

    public static string Date(DateOnly date, ILocalizer localizer, bool capitalize = false) =>
        localizer.Get("date.full", ("weekday", Weekday(date, localizer, capitalize)), ("day", date.Day),
            ("month", Month(date.Month, localizer)), ("year", date.Year));

    public static string Weekday(DateOnly date, ILocalizer localizer, bool capitalize = false)
    {
        var weekday = localizer.Get($"date.weekday.{date.DayOfWeek.ToString().ToLowerInvariant()}");
        return capitalize ? char.ToUpper(weekday[0], localizer.Culture) + weekday[1..] : weekday;
    }

    public static string Month(int month, ILocalizer localizer) => localizer.Get($"date.month.{month}");
}
