using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;

namespace Hasi.Modules.Editor;

/// <summary>
/// A menu that first says why it is asked: "Documentul are modificări nesalvate. Ce doriți să faceți? 3 opțiuni. …".
/// It names a document, so it is private. Escape closes it.
/// </summary>
internal sealed class QuestionScreen(string title, string description, IReadOnlyList<MenuItem> items, string help)
    : IScreen, ISensitiveScreen
{
    private readonly MenuScreen _menu = new(title, items, help);

    public string Title => _menu.Title;

    public Utterance Enter(EnterReason reason, IScreenContext context)
    {
        if (reason == EnterReason.Return) return _menu.Enter(reason, context);
        var l = context.Localizer;
        return l.Get("editor.question",
            ("description", description),
            ("count", l.Plural(MenuScreen.OptionsCountKey, _menu.Items.Count)),
            ("item", _menu.Selected!.Label),
            ("index", _menu.SelectedIndex + 1),
            ("total", _menu.Items.Count));
    }

    public ScreenResult Handle(InputEvent input, IScreenContext context) => _menu.Handle(input, context);

    public Utterance Help(IScreenContext context) => _menu.Help(context);
}

/// <summary>
/// "Există deja un document cu numele …. Îl înlocuiți?" Enter replaces it; Escape goes back to the name field to write
/// another name. Any other key says the question again.
/// </summary>
internal sealed class ReplaceQuestionScreen(string question, Func<ScreenResult> onYes, string title, string help) : IScreen
{
    public string Title { get; } = title;

    public Utterance Enter(EnterReason reason, IScreenContext context) => question;

    public ScreenResult Handle(InputEvent input, IScreenContext context) => input.Command switch
    {
        Command.Activate => ScreenResult.Pop(announce: false, then: onYes()),
        Command.Back => ScreenResult.Pop(),
        _ => ScreenResult.Say(question),
    };

    public Utterance Help(IScreenContext context) => help;
}

/// <summary>The field for a document's name; the first time it opens it can say something first (why HASI saves a copy).</summary>
internal sealed class NameFieldScreen(TextInputScreen textInput, string? introduction) : IScreen, ITextEntry
{
    public string Title => textInput.Title;

    public Utterance Enter(EnterReason reason, IScreenContext context)
    {
        var entry = textInput.Enter(reason, context);
        return introduction is not null && reason != EnterReason.Return ? new Utterance(introduction).Then(entry) : entry;
    }

    public ScreenResult Handle(InputEvent input, IScreenContext context) => textInput.Handle(input, context);

    public Utterance Help(IScreenContext context) => textInput.Help(context);
}

/// <summary>
/// Several documents left unsaved, offered at startup, newest first: "Am găsit 3 documente nesalvate. Selectat: …".
/// Enter on one asks whether to open it or delete its draft; an opened or deleted one leaves the list, which closes
/// when it is empty. Escape ("Mai târziu") keeps every draft for the next start. Their words are private.
/// </summary>
internal sealed class DraftsScreen : IScreen, ISensitiveScreen
{
    public const string CountKey = "editor.draft.count";

    private readonly EditorServices _services;
    private readonly List<EditorDraft> _drafts;
    private MenuScreen _menu;

    public DraftsScreen(EditorServices services, IReadOnlyList<EditorDraft> drafts)
    {
        _services = services;
        _drafts = [.. drafts];
        _menu = Build(0);
    }

    public string Title => _menu.Title;

    public Utterance Enter(EnterReason reason, IScreenContext context)
    {
        if (reason == EnterReason.Return) return _menu.Enter(reason, context);
        var l = context.Localizer;
        return l.Get("editor.draft.foundMany",
            ("count", l.Plural(CountKey, _menu.Items.Count)),
            ("item", _menu.Selected!.Label),
            ("index", _menu.SelectedIndex + 1),
            ("total", _menu.Items.Count));
    }

    public ScreenResult Handle(InputEvent input, IScreenContext context) => _menu.Handle(input, context);

    public Utterance Help(IScreenContext context) => _menu.Help(context);

    private MenuScreen Build(int selected)
    {
        var l = _services.Localizer;
        return new MenuScreen(l.Get("editor.draft.listTitle"),
            [.. _drafts.Select(draft => new MenuItem(_services.DraftLabel(draft), _ => Choose(draft)))],
            l.Get("editor.draft.listHelp"), CountKey, selected);
    }

    private ScreenResult Choose(EditorDraft draft)
    {
        var l = _services.Localizer;
        return ScreenResult.Push(new QuestionScreen(EditorServices.DraftWords(draft),
            l.Get("editor.draft.chosen", ("item", _services.DraftLabel(draft))),
            [
                new MenuItem(l.Get("editor.draft.open"), _ => Open(draft)),
                new MenuItem(l.Get("editor.draft.delete"), _ => ScreenResult.Confirm(l.Get("editor.draft.deleteConfirm"), () => Delete(draft))),
            ],
            l.Get("editor.draft.itemHelp")));
    }

    /// <summary>The document opens above the list (or in its place, when it was the last one); its draft stays stored.</summary>
    private ScreenResult Open(EditorDraft draft)
    {
        Remove(draft);
        var editor = EditorScreen.FromDraft(_services, draft);
        return ScreenResult.PopTo(this, then: _drafts.Count == 0 ? ScreenResult.Replace(editor) : ScreenResult.Push(editor));
    }

    private ScreenResult Delete(EditorDraft draft)
    {
        _services.DeleteDraft(draft.Id);
        Remove(draft);
        var deleted = _services.Localizer.Get("editor.draft.deleted");
        return _drafts.Count == 0
            ? ScreenResult.PopTo(this, then: ScreenResult.Pop(prefix: deleted))
            : ScreenResult.PopTo(this, prefix: deleted);
    }

    private void Remove(EditorDraft draft)
    {
        var index = _drafts.IndexOf(draft);
        _drafts.Remove(draft);
        _menu = Build(Math.Min(index, _drafts.Count - 1));
    }
}

/// <summary>The documents opened or saved lately, newest first. Their names are private.</summary>
internal sealed class RecentDocumentsScreen : IScreen, ISensitiveScreen
{
    private readonly EditorServices _services;
    private readonly IScreen _root;
    private MenuScreen _menu;

    /// <param name="root">The editor's menu: an opened document goes above it.</param>
    public RecentDocumentsScreen(EditorServices services, IScreen root)
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
        return new MenuScreen(l.Get("editor.recent"),
            [.. _services.Recent().Select(recent => new MenuItem(recent.Title, _ => Open(recent)))],
            l.Get("editor.recent.help"), "editor.recent.count", selected);
    }

    private ScreenResult Open(RecentDocument recent)
    {
        var loaded = _services.Documents.Load(recent.Path);
        if (loaded.Problem != Hasi.Core.Documents.DocumentProblem.NotFound) return _services.Open(recent.Path, loaded, _root);

        // The file was moved, deleted, or was on a stick that is not plugged in: it leaves the list.
        _services.RemoveRecent(recent.Path);
        var l = _services.Localizer;
        var missing = new Utterance(l.Get("editor.recent.missing"), Earcon: Earcon.Error);
        _menu = Build(_menu.SelectedIndex);
        if (_menu.Items.Count == 0) return ScreenResult.Pop(prefix: missing);
        return ScreenResult.Say(missing.Then(l.Get("menu.item",
            ("item", _menu.Selected!.Label), ("index", _menu.SelectedIndex + 1), ("total", _menu.Items.Count))));
    }
}
