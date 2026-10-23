using Hasi.Core.Input;
using Hasi.Core.Output;

namespace Hasi.Core.Screens;

/// <summary>
/// Adjusts one number (speech rate, volume …). Right or Up increases, Left or Down decreases; each change is
/// applied at once, so the new value is heard in the new setting. Enter or Escape goes back.
/// </summary>
/// <param name="describe">How a value is said, for example 50 → "50, viteza normală".</param>
public sealed class ValueScreen(
    string title,
    Func<int> get,
    Action<int> set,
    int min,
    int max,
    int step,
    Func<int, string> describe) : IScreen
{
    public string Title { get; } = title;

    public Utterance Enter(EnterReason reason, IScreenContext context) =>
        context.Localizer.Get("value.enter", ("title", Title), ("value", describe(get())));

    public ScreenResult Handle(InputEvent input, IScreenContext context) => input.Command switch
    {
        Command.Increase or Command.Previous => Change(+step, context),
        Command.Decrease or Command.Next => Change(-step, context),
        Command.First => Change(min - get(), context),
        Command.Last => Change(max - get(), context),
        Command.Activate => ScreenResult.Pop(),
        _ => ScreenResult.Unhandled,
    };

    public Utterance Help(IScreenContext context) => context.Localizer.Get("value.help", ("title", Title));

    private ScreenResult Change(int delta, IScreenContext context)
    {
        var current = get();
        var target = Math.Clamp(current + delta, min, max);
        if (target == current)
        {
            var key = delta > 0 ? "value.atMax" : "value.atMin";
            return ScreenResult.Say(new Utterance(context.Localizer.Get(key, ("value", describe(current))), Earcon: Earcon.EdgeOfList));
        }
        set(target);
        return ScreenResult.Say(describe(target));
    }
}

/// <summary>
/// Picks one of several options (key echo, colours …). The list opens on the current option, marked as such;
/// Enter chooses and goes back, saying the new choice.
/// </summary>
public sealed class ChoiceScreen(
    string title,
    IReadOnlyList<string> options,
    int current,
    Func<int, IScreenContext, ScreenResult> choose,
    string currentMarker) : MenuScreen(
        title,
        [.. options.Select((label, index) => new MenuItem(index == current ? $"{label}{currentMarker}" : label))],
        initialIndex: current)
{
    public override ScreenResult Handle(InputEvent input, IScreenContext context) =>
        input.Command == Command.Activate ? choose(SelectedIndex, context) : base.Handle(input, context);
}
