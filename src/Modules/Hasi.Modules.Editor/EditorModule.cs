using Hasi.Core.Modules;
using Hasi.Core.Screens;

namespace Hasi.Modules.Editor;

/// <summary>"Editor de text": a new document, opening a .txt or .docx, and the recent documents.</summary>
/// <remarks>Needs the host's document formats and file system (<see cref="IModuleContext.Documents"/>, <see cref="IModuleContext.Files"/>).</remarks>
public sealed class EditorModule : IModule
{
    public static readonly string[] Extensions = [".txt", ".docx"];

    public string Id => "editor";

    public string TitleKey => "editor.title";

    public IScreen CreateRootScreen(IModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var services = new EditorServices(context);
        var l = context.Localizer;
        MenuScreen? root = null;
        root = new MenuScreen(l.Get(TitleKey),
            [
                new MenuItem(l.Get("editor.new"), _ => ScreenResult.Push(EditorScreen.New(services))),
                new MenuItem(l.Get("editor.open"), _ => ScreenResult.Push(new FileBrowserScreen(l.Get("editor.open"), services.Files,
                    Extensions, (entry, _) => services.Open(entry.Path, services.Documents.Load(entry.Path), root!)))),
                new MenuItem(l.Get("editor.recent"), _ => new RecentDocumentsScreen(services, root!) is { IsEmpty: false } recent
                    ? ScreenResult.Push(recent)
                    : ScreenResult.Say(l.Get("editor.recent.empty"))),
            ],
            help: l.Get("editor.help"));
        return root;
    }

    /// <summary>
    /// Documents left unsaved by a serious error or a power cut, offered once HASI has started: one is asked about
    /// directly, several are listed (<see cref="DraftsScreen"/>).
    /// </summary>
    public ScreenResult? OnStartup(IModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var services = new EditorServices(context);
        var drafts = services.Drafts();
        if (drafts.Count == 0) return null;
        if (drafts.Count > 1) return ScreenResult.Push(new DraftsScreen(services, drafts));

        var draft = drafts[0];
        var l = context.Localizer;
        var (date, time) = services.DraftMoment(draft);
        // A document never saved is still called "Document nou": its first words say which one it is.
        var title = draft.Title == l.Get("editor.newTitle") ? EditorServices.DraftWords(draft) : draft.Title;
        var found = l.Get("editor.draft.found", ("title", title), ("date", date), ("time", time));
        return ScreenResult.Push(new QuestionScreen(l.Get("editor.draft.title"), found,
            [
                new MenuItem(l.Get("editor.draft.open"), _ => ScreenResult.Replace(EditorScreen.FromDraft(services, draft))),
                new MenuItem(l.Get("editor.draft.delete"), _ => ScreenResult.Confirm(l.Get("editor.draft.deleteConfirm"), () =>
                {
                    services.DeleteDraft(draft.Id);
                    return ScreenResult.Pop(prefix: l.Get("editor.draft.deleted"));
                })),
                new MenuItem(l.Get("editor.draft.later"), _ => ScreenResult.Pop()),
            ],
            l.Get("editor.draft.help")));
    }
}
