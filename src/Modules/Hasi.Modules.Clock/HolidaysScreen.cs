using Hasi.Core.Input;
using Hasi.Core.Localization;
using Hasi.Core.Output;
using Hasi.Core.Screens;

namespace Hasi.Modules.Clock;

/// <summary>
/// The legal holidays of one year, one item per date. Left and right change the year. For the current year the
/// next holiday is selected first.
/// </summary>
internal sealed class HolidaysScreen : IScreen
{
    public const string CountKey = "clock.holidays.count";

    private readonly LegalHolidays _holidays;
    private readonly ILocalizer _l;
    private readonly DateOnly _today;
    private int _year;
    private MenuScreen _list;

    public HolidaysScreen(LegalHolidays holidays, ILocalizer localizer, DateOnly today)
    {
        _holidays = holidays;
        _l = localizer;
        _today = today;
        _year = holidays.FirstYear > holidays.LastYear
            ? today.Year
            : Math.Clamp(today.Year, holidays.FirstYear, holidays.LastYear);
        _list = List();
    }

    public string Title => _list.Title;

    public Utterance Enter(EnterReason reason, IScreenContext context) => _list.Enter(reason, context);

    public ScreenResult Handle(InputEvent input, IScreenContext context) => input.Command switch
    {
        Command.Increase => ChangeYear(+1, context),
        Command.Decrease => ChangeYear(-1, context),
        _ => _list.Handle(input, context),
    };

    public Utterance Help(IScreenContext context) => _list.Help(context);

    private ScreenResult ChangeYear(int delta, IScreenContext context)
    {
        if (!_holidays.Knows(_year + delta))
        {
            return ScreenResult.Say(new Utterance(
                _l.Get("clock.holidays.limits", ("first", _holidays.FirstYear), ("last", _holidays.LastYear)),
                Earcon: Earcon.EdgeOfList));
        }
        _year += delta;
        _list = List();
        return ScreenResult.Say(_list.Enter(EnterReason.Initial, context));
    }

    private MenuScreen List()
    {
        var dates = _holidays.In(_year);
        var items = dates.Select(d => new MenuItem(_l.Get("clock.holidays.item",
            ("weekday", DateText.Weekday(d.Date, _l, capitalize: true)), ("day", d.Date.Day),
            ("month", DateText.Month(d.Date.Month, _l)), ("names", HolidayNames.Join(d.Keys, _l))))).ToList();
        var selected = 0;
        if (_year == _today.Year && dates.Count > 0)
        {
            selected = dates.ToList().FindIndex(d => d.Date >= _today);
            if (selected < 0) selected = dates.Count - 1;
        }
        return new MenuScreen(_l.Get("clock.holidays.title", ("year", _year)), items,
            help: _l.Get("clock.holidays.help"), countKey: CountKey, initialIndex: selected);
    }
}
