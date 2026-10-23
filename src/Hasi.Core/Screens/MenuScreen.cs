using Hasi.Core.Input;
using Hasi.Core.Output;

namespace Hasi.Core.Screens;

/// <param name="Label">What is said and shown for the item.</param>
/// <param name="Activate">Runs on Enter. When null, Enter says the item again (reading lists).</param>
public sealed record MenuItem(string Label, Func<IScreenContext, ScreenResult>? Activate = null);

/// <summary>A vertical list of items: the basic building block of HASI.</summary>
/// <param name="initialIndex">The item selected when the screen opens (clamped to the list).</param>
public class MenuScreen(
    string title,
    IReadOnlyList<MenuItem> items,
    string? help = null,
    string countKey = MenuScreen.OptionsCountKey,
    int initialIndex = 0) : IScreen
{
    public const string OptionsCountKey = "menu.count.options";
    public const string LinesCountKey = "menu.count.lines";

    public string Title { get; } = title;

    public IReadOnlyList<MenuItem> Items { get; } = items;

    public int SelectedIndex { get; private set; } = items.Count == 0 ? 0 : Math.Clamp(initialIndex, 0, items.Count - 1);

    public MenuItem? Selected => Items.Count == 0 ? null : Items[SelectedIndex];

    public Utterance Enter(EnterReason reason, IScreenContext context)
    {
        var l = context.Localizer;
        if (Items.Count == 0) return l.Get("menu.empty", ("title", Title));
        return reason == EnterReason.Return
            ? l.Get("menu.return", ("title", Title), ("item", Selected!.Label), ("index", SelectedIndex + 1), ("total", Items.Count))
            : l.Get("menu.enter", ("title", Title), ("count", l.Plural(countKey, Items.Count)),
                ("item", Selected!.Label), ("index", SelectedIndex + 1), ("total", Items.Count));
    }

    public virtual ScreenResult Handle(InputEvent input, IScreenContext context)
    {
        if (Items.Count == 0) return ScreenResult.Unhandled;
        return input.Command switch
        {
            Command.Next => Move(+1, input.IsRepeat, context),
            Command.Previous => Move(-1, input.IsRepeat, context),
            Command.First => MoveTo(0, context),
            Command.Last => MoveTo(Items.Count - 1, context),
            Command.Activate => Selected!.Activate is { } activate
                ? activate(context)
                : ScreenResult.Say(ItemAnnouncement(context)),
            _ => ScreenResult.Unhandled,
        };
    }

    public Utterance Help(IScreenContext context)
    {
        Utterance text = context.Localizer.Get("menu.help", ("title", Title));
        return help is null ? text : text.Then(help);
    }

    private ScreenResult Move(int delta, bool isRepeat, IScreenContext context)
    {
        var target = SelectedIndex + delta;
        if (target >= 0 && target < Items.Count) return MoveTo(target, context);

        // Holding an arrow at the edge: say "end of list" once, not dozens of times per second.
        if (isRepeat) return ScreenResult.Handled;

        if (context.Options.WrapLists && Items.Count > 1)
        {
            SelectedIndex = target < 0 ? Items.Count - 1 : 0;
            return ScreenResult.Say(ItemAnnouncement(context) with { Earcon = Earcon.EdgeOfList });
        }

        var key = delta > 0 ? "menu.endOfList" : "menu.startOfList";
        return ScreenResult.Say(new Utterance(
            context.Localizer.Get(key, ("item", Selected!.Label), ("index", SelectedIndex + 1), ("total", Items.Count)),
            Earcon: Earcon.EdgeOfList));
    }

    private ScreenResult MoveTo(int index, IScreenContext context)
    {
        SelectedIndex = index;
        return ScreenResult.Say(ItemAnnouncement(context));
    }

    private Utterance ItemAnnouncement(IScreenContext context) =>
        context.Localizer.Get("menu.item", ("item", Selected!.Label), ("index", SelectedIndex + 1), ("total", Items.Count));
}
