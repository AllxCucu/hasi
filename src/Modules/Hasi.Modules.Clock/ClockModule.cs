using Hasi.Core.Localization;
using Hasi.Core.Modules;
using Hasi.Core.Screens;

namespace Hasi.Modules.Clock;

/// <summary>"Ceas și calendar": the time, today's date, a calendar and the legal holidays of a year.</summary>
public sealed class ClockModule(LegalHolidays? holidays = null) : IModule
{
    private static readonly DateOnly CalendarStart = new(OrthodoxEaster.FirstYear, 1, 1);
    private static readonly DateOnly CalendarEnd = new(OrthodoxEaster.LastYear, 12, 31);

    private LegalHolidays? _holidays = holidays;

    public string Id => "clock";

    public string TitleKey => "clock.title";

    private LegalHolidays Holidays => _holidays ??= LegalHolidays.Load(LegalHolidays.DefaultPath);

    public IScreen CreateRootScreen(IModuleContext context)
    {
        var l = context.Localizer;
        MenuItem[] items =
        [
            new(l.Get("clock.time"), _ => ScreenResult.Say(DateText.Time(context.Time.GetLocalNow(), l))),
            new(l.Get("clock.date"), _ => ScreenResult.Say(TodayText(context))),
            new(l.Get("clock.calendar"), _ => ScreenResult.Push(Calendar(context))),
            new(l.Get("clock.holidays"), _ => ScreenResult.Push(new HolidaysScreen(Holidays, l, Today(context)))),
        ];
        return new MenuScreen(l.Get(TitleKey), items, help: l.Get("clock.help"));
    }

    private static DateOnly Today(IModuleContext context) => DateOnly.FromDateTime(context.Time.GetLocalNow().DateTime);

    private string TodayText(IModuleContext context)
    {
        var l = context.Localizer;
        var today = Today(context);
        var text = l.Get("clock.today", ("date", DateText.Date(today, l)));
        return HolidayNames.Sentence(Holidays.On(today), l) is { } holiday ? $"{text} {holiday}" : text;
    }

    private DatePickerScreen Calendar(IModuleContext context)
    {
        var l = context.Localizer;
        var today = Today(context);
        var start = today < CalendarStart ? CalendarStart : today > CalendarEnd ? CalendarEnd : today;
        return new DatePickerScreen(l.Get("clock.calendar"), start, CalendarStart, CalendarEnd,
            day => HolidayNames.Sentence(Holidays.On(day), l), today,
            help: l.Get("clock.calendar.help", ("first", Holidays.FirstYear)));
    }
}

/// <summary>How the names of one day's holidays are said.</summary>
internal static class HolidayNames
{
    /// <summary>"Ziua Muncii", or "Ziua Copilului și Rusaliile ortodoxe, a doua zi".</summary>
    public static string Join(IReadOnlyList<string> keys, ILocalizer l)
    {
        var names = keys.Select(key => l.Get(key)).ToList();
        return names.Count == 1
            ? names[0]
            : $"{string.Join(", ", names[..^1])} {l.Get("clock.and")} {names[^1]}";
    }

    /// <summary>"Sărbătoare legală: …." or "Sărbători legale: …."; null when the day is not a holiday.</summary>
    public static string? Sentence(IReadOnlyList<string> keys, ILocalizer l) => keys.Count switch
    {
        0 => null,
        1 => l.Get("clock.holiday.one", ("names", Join(keys, l))),
        _ => l.Get("clock.holiday.many", ("names", Join(keys, l))),
    };
}
