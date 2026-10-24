using Hasi.Core.Input;
using Hasi.Core.Localization;
using Hasi.Core.Output;

namespace Hasi.Core.Screens;

/// <summary>
/// Asked before HASI closes, restarts or shuts down the computer, or installs an update, while a screen holds
/// unsaved work (<see cref="Modules.IShellServices.GuardUnsaved"/>): save and continue, continue without saving
/// (confirmed), or stay.
/// </summary>
public sealed class UnsavedWorkScreen : IScreen
{
    private readonly IUnsavedWork _work;
    private readonly Func<ScreenResult> _next;
    private readonly MenuScreen _menu;

    /// <param name="next">
    /// Once the work is saved or discarded: asks about the remaining unsaved work the same way, then continues with
    /// what the user asked for.
    /// </param>
    public UnsavedWorkScreen(IUnsavedWork work, Func<ScreenResult> next, ILocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        _work = work;
        _next = next;
        _menu = new MenuScreen(
            localizer.Get("unsaved.title"),
            [
                new MenuItem(localizer.Get("unsaved.save"), SaveAndContinue),
                new MenuItem(localizer.Get("unsaved.discard"), DiscardAndContinue),
                new MenuItem(localizer.Get("unsaved.cancel"), Stay),
            ],
            help: localizer.Get("unsaved.help"));
    }

    public string Title => _menu.Title;

    /// <summary>The work this screen asks about.</summary>
    public IUnsavedWork Work => _work;

    public Utterance Enter(EnterReason reason, IScreenContext context)
    {
        if (reason == EnterReason.Return) return _menu.Enter(reason, context);
        var l = context.Localizer;
        return l.Get("unsaved.enter",
            ("description", _work.UnsavedDescription(context)),
            ("count", l.Plural(MenuScreen.OptionsCountKey, _menu.Items.Count)),
            ("item", _menu.Selected!.Label),
            ("index", _menu.SelectedIndex + 1),
            ("total", _menu.Items.Count));
    }

    public ScreenResult Handle(InputEvent input, IScreenContext context) => input.Command == Command.Back
        ? Stay(context)
        : _menu.Handle(input, context);

    public Utterance Help(IScreenContext context) => _menu.Help(context);

    private ScreenResult SaveAndContinue(IScreenContext context) => _work.Save(context, Continue);

    private ScreenResult DiscardAndContinue(IScreenContext context) => ScreenResult.Confirm(
        context.Localizer.Get("unsaved.discardConfirm"),
        () =>
        {
            _work.Discard();
            return Continue();
        });

    private static ScreenResult Stay(IScreenContext context) => ScreenResult.Pop(prefix: context.Localizer.Get("confirm.cancelled"));

    /// <summary>Closes this question (and whatever saving opened above it), then asks about the next unsaved work, or proceeds.</summary>
    private ScreenResult Continue() => new CloseResult(this, _next);
}
