using Hasi.Core.Input;
using Hasi.Core.Localization;
using Hasi.Core.Output;

namespace Hasi.Core.Screens;

/// <summary>A bounded calendar shared by modules; callers supply any extra information about a day.</summary>
public sealed class DatePickerScreen : IScreen
{
    private readonly DateOnly _min;
    private readonly DateOnly _max;
    private readonly DateOnly _today;
    private readonly Func<DateOnly, string?> _describeDay;
    private readonly Func<DateOnly, IScreenContext, ScreenResult>? _onChoose;
    private readonly string? _help;

    public DatePickerScreen(string title, DateOnly initialDate, DateOnly min, DateOnly max,
        Func<DateOnly, string?> describeDay, DateOnly today,
        Func<DateOnly, IScreenContext, ScreenResult>? onChoose = null, string? help = null)
    {
        if (max < min) throw new ArgumentOutOfRangeException(nameof(max));
        if (initialDate < min || initialDate > max) throw new ArgumentOutOfRangeException(nameof(initialDate));
        ArgumentNullException.ThrowIfNull(describeDay);
        Title = title;
        SelectedDate = initialDate;
        _min = min;
        _max = max;
        _describeDay = describeDay;
        _today = today;
        _onChoose = onChoose;
        _help = help;
    }

    public string Title { get; }

    public DateOnly SelectedDate { get; private set; }

    public Utterance Enter(EnterReason reason, IScreenContext context) =>
        context.Localizer.Get("datePicker.enter", ("title", Title), ("day", DayLine(context)));

    public ScreenResult Handle(InputEvent input, IScreenContext context) => input.Command switch
    {
        Command.Next => MoveTo(SelectedDate.DayNumber + 1, context),
        Command.Previous => MoveTo(SelectedDate.DayNumber - 1, context),
        Command.NextPage or Command.Increase => MoveMonth(1, context),
        Command.PreviousPage or Command.Decrease => MoveMonth(-1, context),
        Command.First => MoveTo(new DateOnly(SelectedDate.Year, SelectedDate.Month, 1).DayNumber, context),
        Command.Last => MoveTo(new DateOnly(SelectedDate.Year, SelectedDate.Month,
            DateTime.DaysInMonth(SelectedDate.Year, SelectedDate.Month)).DayNumber, context),
        Command.Activate => _onChoose?.Invoke(SelectedDate, context) ?? ScreenResult.Say(DayLine(context)),
        _ => ScreenResult.Unhandled,
    };

    public Utterance Help(IScreenContext context)
    {
        Utterance text = context.Localizer.Get("datePicker.help");
        return _help is null ? text : text.Then(_help);
    }

    private string DayLine(IScreenContext context)
    {
        var l = context.Localizer;
        var text = l.Get("datePicker.day", ("date", DateText.Date(SelectedDate, l, capitalize: true)),
            ("today", SelectedDate == _today ? l.Get("datePicker.today") : ""));
        var extra = _describeDay(SelectedDate);
        return string.IsNullOrWhiteSpace(extra) ? text : $"{text} {extra}";
    }

    private ScreenResult MoveMonth(int delta, IScreenContext context)
    {
        var month = (SelectedDate.Year - 1) * 12 + SelectedDate.Month - 1 + delta;
        if (month < 0) return MoveTo(_min.DayNumber - 1, context);
        if (month >= 9999 * 12) return MoveTo(_max.DayNumber + 1, context);
        return MoveTo(SelectedDate.AddMonths(delta).DayNumber, context);
    }

    private ScreenResult MoveTo(int dayNumber, IScreenContext context)
    {
        SelectedDate = DateOnly.FromDayNumber(Math.Clamp(dayNumber, _min.DayNumber, _max.DayNumber));
        if (dayNumber < _min.DayNumber || dayNumber > _max.DayNumber)
        {
            var key = dayNumber < _min.DayNumber ? "datePicker.start" : "datePicker.end";
            Utterance edge = new(context.Localizer.Get(key), Earcon: Earcon.EdgeOfList);
            return ScreenResult.Say(edge.Then(DayLine(context)));
        }
        return ScreenResult.Say(DayLine(context));
    }
}
