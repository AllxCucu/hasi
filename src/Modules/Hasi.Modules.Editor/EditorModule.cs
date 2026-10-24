using System.Globalization;

using Hasi.Core.Localization;
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

    /// <summary>A document left unsaved by a serious error or a power cut: offered once HASI has started.</summary>
    public ScreenResult? OnStartup(IModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var services = new EditorServices(context);
        if (services.Draft() is not { } draft) return null;

        var l = context.Localizer;
        var saved = TimeZoneInfo.ConvertTime(draft.SavedAt, context.Time.LocalTimeZone);
        var date = l.Get("editor.draft.date", ("day", saved.Day), ("month", DateText.Month(saved.Month, l)), ("year", saved.Year));
        var found = l.Get("editor.draft.found",
            ("title", draft.Title), ("date", date), ("time", saved.ToString("H:mm", CultureInfo.InvariantCulture)));
        return ScreenResult.Push(new QuestionScreen(l.Get("editor.draft.title"), found,
            [
                new MenuItem(l.Get("editor.draft.open"), _ => ScreenResult.Replace(EditorScreen.FromDraft(services, draft))),
                new MenuItem(l.Get("editor.draft.delete"), _ => ScreenResult.Confirm(l.Get("editor.draft.deleteConfirm"), () =>
                {
                    services.DeleteDraft();
                    return ScreenResult.Pop(prefix: l.Get("editor.draft.deleted"));
                })),
                new MenuItem(l.Get("editor.draft.later"), _ => ScreenResult.Pop()),
            ],
            l.Get("editor.draft.help")));
    }
}
