using System.Globalization;

namespace Hasi.Core.Localization;

/// <summary>Date and time text shared by the shell and modules; names come from the resources.</summary>
public static class DateText
{
    public static string Time(DateTimeOffset time, ILocalizer localizer) =>
        localizer.Get("date.time", ("time", time.ToString("H:mm", CultureInfo.InvariantCulture)));

    public static string Date(DateOnly date, ILocalizer localizer, bool capitalize = false)
    {
        var weekday = localizer.Get($"date.weekday.{date.DayOfWeek.ToString().ToLowerInvariant()}");
        if (capitalize) weekday = char.ToUpper(weekday[0], localizer.Culture) + weekday[1..];
        return localizer.Get("date.full", ("weekday", weekday), ("day", date.Day),
            ("month", localizer.Get($"date.month.{date.Month}")), ("year", date.Year));
    }
}
