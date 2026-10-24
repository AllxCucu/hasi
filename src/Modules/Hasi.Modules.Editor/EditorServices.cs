using Hasi.Core.Documents;
using Hasi.Core.Files;
using Hasi.Core.Localization;
using Hasi.Core.Modules;
using Hasi.Core.Output;
using Hasi.Core.Screens;

namespace Hasi.Modules.Editor;

/// <summary>A document opened or saved lately, in the user's data.</summary>
internal sealed record RecentDocument(string Path, string Title, DateTimeOffset LastUsed);

/// <summary>
/// The text of a document with unsaved changes, kept in the user's data so that a serious error or a power cut does
/// not lose it. Deleted when the document is saved, its changes are discarded, or it is closed.
/// </summary>
internal sealed record EditorDraft(string Title, string? Path, DocumentFormat Format, bool MadeByHasi, string Text, DateTimeOffset SavedAt);

/// <summary>What the editor's screens share: the module context, the recent documents, the draft and opening a document.</summary>
internal sealed class EditorServices(IModuleContext context)
{
    public const string RecentName = "editor-recente";
    public const string DraftName = "editor-ciorna";
    public const int MaxRecent = 10;

    public IModuleContext Context => context;

    public ILocalizer Localizer => context.Localizer;

    public IDocumentFormats Documents =>
        context.Documents ?? throw new InvalidOperationException("The editor needs the document formats service.");

    public IFileSystem Files => context.Files ?? throw new InvalidOperationException("The editor needs the file system service.");

    public static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Newest first.</summary>
    public IReadOnlyList<RecentDocument> Recent() =>
        [.. (context.UserData?.Read<List<RecentDocument>>(RecentName) ?? [])
            .Where(r => r is { Path: not null, Title: not null })
            .OrderByDescending(r => r.LastUsed)
            .Take(MaxRecent)];

    /// <summary>Puts the document first in the recent list (once), keeping the last <see cref="MaxRecent"/>.</summary>
    public void AddRecent(string path, string title)
    {
        List<RecentDocument> recent = [new(path, title, context.Time.GetUtcNow()), .. Recent().Where(r => !SamePath(r.Path, path))];
        context.UserData?.Write(RecentName, recent.Take(MaxRecent).ToList());
    }

    public void RemoveRecent(string path) =>
        context.UserData?.Write(RecentName, Recent().Where(r => !SamePath(r.Path, path)).ToList());

    public EditorDraft? Draft() => context.UserData?.Read<EditorDraft>(DraftName) is { Text: not null, Title: not null } draft ? draft : null;

    public bool WriteDraft(EditorDraft draft) => context.UserData?.Write(DraftName, draft) ?? false;

    public void DeleteDraft() => context.UserData?.Delete(DraftName);

    /// <summary>
    /// Opens the document in the editor, above the editor's menu: a problem is said and the user stays where they are.
    /// </summary>
    public ScreenResult Open(string path, DocumentLoadResult loaded, IScreen root)
    {
        if (loaded.Document is not { } document)
        {
            return ScreenResult.Say(new Utterance(OpenProblem(loaded.Problem), Earcon: Earcon.Error));
        }
        var editor = EditorScreen.Opened(this, path, document);
        AddRecent(path, editor.Title);
        return ScreenResult.PopTo(root, then: ScreenResult.Push(editor));
    }

    public string OpenProblem(DocumentProblem problem) => Localizer.Get(problem switch
    {
        DocumentProblem.NotFound => "editor.open.notFound",
        DocumentProblem.TooLarge => "editor.open.tooLarge",
        DocumentProblem.Protected => "editor.open.protected",
        DocumentProblem.NoAccess => "editor.open.noAccess",
        _ => "editor.open.damaged",
    });

    /// <summary>"Documentul nu a putut fi salvat. Discul este plin. Textul este păstrat în HASI."</summary>
    public string SaveProblem(DocumentProblem problem)
    {
        string? reasonKey = problem switch
        {
            DocumentProblem.DiskFull => "editor.saveProblem.diskFull",
            DocumentProblem.NoAccess => "editor.saveProblem.noAccess",
            DocumentProblem.NotFound => "editor.saveProblem.notFound",
            _ => null,
        };
        return reasonKey is null
            ? Localizer.Get("editor.saveFailed.noReason")
            : Localizer.Get("editor.saveFailed", ("reason", Localizer.Get(reasonKey)));
    }
}
