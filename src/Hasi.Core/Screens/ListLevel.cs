using Hasi.Core.Input;
using Hasi.Core.Output;

namespace Hasi.Core.Screens;

/// <summary>
/// A list read from outside (the disk) when its screen opens, kept while the user goes deeper and comes back, so the
/// selection stays where it was. When nothing is found, it says so instead of an empty menu.
/// </summary>
internal sealed class ListLevel(
    string title,
    Func<IScreenContext, IReadOnlyList<MenuItem>> build,
    Func<IScreenContext, string> help,
    string emptyKey,
    string emptyHelpKey)
{
    private MenuScreen? _menu;

    public Utterance Enter(EnterReason reason, IScreenContext context)
    {
        if (_menu is null || reason != EnterReason.Return) _menu = Create(context);
        return _menu.Items.Count == 0 ? context.Localizer.Get(emptyKey, ("title", title)) : _menu.Enter(reason, context);
    }

    public ScreenResult Handle(InputEvent input, IScreenContext context)
    {
        _menu ??= Create(context);
        if (_menu.Items.Count > 0) return _menu.Handle(input, context);
        return input.Command is Command.Activate or Command.Next or Command.Previous or Command.First or Command.Last
            ? ScreenResult.Say(context.Localizer.Get(emptyKey, ("title", title)))
            : ScreenResult.Unhandled;
    }

    public Utterance Help(IScreenContext context)
    {
        _menu ??= Create(context);
        return _menu.Items.Count == 0 ? context.Localizer.Get(emptyHelpKey, ("title", title)) : _menu.Help(context);
    }

    private MenuScreen Create(IScreenContext context) => new(title, build(context), help(context));
}
