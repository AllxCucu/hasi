using Hasi.Core.Modules;
using Hasi.Core.Screens;

namespace Hasi.Modules.Documents;

/// <summary>
/// "Citire documente": opening a .txt, .docx, .pdf or .epub document to read it, and the documents read lately, each
/// reopened where the user stopped.
/// </summary>
/// <remarks>Needs the host's document formats and file system (<see cref="IModuleContext.Documents"/>, <see cref="IModuleContext.Files"/>).</remarks>
public sealed class DocumentsModule : IModule
{
    public static readonly string[] Extensions = [".txt", ".docx", ".pdf", ".epub"];

    public string Id => "documents";

    public string TitleKey => "documents.title";

    public IScreen CreateRootScreen(IModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var services = new DocumentsServices(context);
        var l = context.Localizer;
        MenuScreen? root = null;
        root = new MenuScreen(l.Get(TitleKey),
            [
                new MenuItem(l.Get("documents.open"), _ => ScreenResult.Push(new FileBrowserScreen(l.Get("documents.open"), services.Files,
                    Extensions, (entry, level, _) => services.Open(entry.Path, level, root!)))),
                new MenuItem(l.Get("documents.recent"), _ => new RecentReadingsScreen(services, root!) is { IsEmpty: false } recent
                    ? ScreenResult.Push(recent)
                    : ScreenResult.Say(l.Get("documents.recent.empty"))),
            ],
            help: l.Get("documents.help"));
        return root;
    }
}
