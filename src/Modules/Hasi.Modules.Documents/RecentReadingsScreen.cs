using Hasi.Core.Documents;
using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;

namespace Hasi.Modules.Documents;

/// <summary>
/// The documents read lately, newest first; a PDF also says its page: "Manual, pagina 12". Enter reopens the document
/// where the user stopped. A document no longer on the disk leaves the list. Their names are private.
/// </summary>
internal sealed class RecentReadingsScreen : IScreen, ISensitiveScreen
{
    private readonly DocumentsServices _services;
    private readonly IScreen _root;
    private MenuScreen _menu;

    /// <param name="root">The module's menu: an opened document goes above it.</param>
    public RecentReadingsScreen(DocumentsServices services, IScreen root)
    {
        _services = services;
        _root = root;
        _menu = Build(0);
    }

    public bool IsEmpty => _menu.Items.Count == 0;

    public string Title => _menu.Title;

    public Utterance Enter(EnterReason reason, IScreenContext context) => _menu.Enter(reason, context);

    public ScreenResult Handle(InputEvent input, IScreenContext context) => _menu.Handle(input, context);

    public Utterance Help(IScreenContext context) => _menu.Help(context);

    private MenuScreen Build(int selected)
    {
        var l = _services.Localizer;
        return new MenuScreen(l.Get("documents.recent"),
            [.. _services.Recent().Select(recent => new MenuItem(Label(recent), _ => _services.Open(recent.Path, this, _root, c => Missing(recent, c))))],
            l.Get("documents.recent.help"), "documents.recent.count", selected);
    }

    private string Label(RecentReading recent) => recent.Format == DocumentFormat.Pdf && recent.Page > 0
        ? _services.Localizer.Get("documents.recent.page", ("title", recent.Title), ("page", recent.Page))
        : recent.Title;

    /// <summary>The file was moved, deleted, or was on a stick that is not plugged in: it leaves the list.</summary>
    private ScreenResult Missing(RecentReading recent, IScreenContext context)
    {
        _services.RemoveRecent(recent.Path);
        var l = context.Localizer;
        var missing = new Utterance(l.Get("documents.recent.missing"), Earcon: Earcon.Error);
        _menu = Build(_menu.SelectedIndex);
        if (_menu.Items.Count == 0) return ScreenResult.Pop(prefix: missing);
        return ScreenResult.Say(missing.Then(l.Get("menu.item",
            ("item", _menu.Selected!.Label), ("index", _menu.SelectedIndex + 1), ("total", _menu.Items.Count))));
    }
}
