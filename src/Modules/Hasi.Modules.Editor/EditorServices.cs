using System.Globalization;

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
/// <param name="Id">Stable for the document's whole life in the editor, also after it is recovered from the draft.</param>
internal sealed record EditorDraft(
    string Id, string Title, string? Path, DocumentFormat Format, bool MadeByHasi, string Text, DateTimeOffset SavedAt);

/// <summary>What the editor's screens share: the module context, the recent documents, the drafts and opening a document.</summary>
internal sealed class EditorServices(IModuleContext context)
{
    public const string RecentName = "editor-recente";

    /// <summary>One draft per unsaved document, at most <see cref="MaxDrafts"/>.</summary>
    public const string DraftsName = "editor-ciorne";

    /// <summary>The single draft kept by the first version of the editor; moved into the list on first read.</summary>
    public const string OldDraftName = "editor-ciorna";

    public const int MaxRecent = 10;
    public const int MaxDrafts = 10;

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

    public static string NewDraftId() => Guid.NewGuid().ToString("N");

    /// <summary>The drafts of all unsaved documents, newest first.</summary>
    public IReadOnlyList<EditorDraft> Drafts() => [.. StoredDrafts().OrderByDescending(d => d.SavedAt)];

    /// <summary>
    /// Writes the document's draft over its previous one. A new draft beyond <see cref="MaxDrafts"/> takes the place of
    /// the oldest, which is said (queued, after the current speech) once the write succeeded. False when it could not
    /// be written; the caller tries again later.
    /// </summary>
    public bool WriteDraft(EditorDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (context.UserData is not { } data) return false;
        var drafts = StoredDrafts();
        var index = drafts.FindIndex(d => d.Id == draft.Id);
        List<EditorDraft> dropped = [];
        if (index >= 0)
        {
            drafts[index] = draft;
        }
        else
        {
            while (drafts.Count >= MaxDrafts)
            {
                var oldest = drafts.MinBy(d => d.SavedAt)!;
                drafts.Remove(oldest);
                dropped.Add(oldest);
            }
            drafts.Add(draft);
        }
        if (!data.Write(DraftsName, drafts)) return false;

        // Said only after the write: a failed one is tried again at every tick and must not repeat the sentence.
        foreach (var oldest in dropped)
        {
            var (date, time) = DraftMoment(oldest);
            var said = Localizer.Get("editor.draft.limit",
                ("count", MaxDrafts), ("words", DraftWords(oldest)), ("date", date), ("time", time));
            context.Shell.Show(ScreenResult.Say(new Utterance(said, Sensitive: true)), UtterancePriority.Queue);
        }
        return true;
    }

    /// <summary>Deletes only this document's draft; the others stay.</summary>
    public void DeleteDraft(string id)
    {
        if (context.UserData is not { } data) return;
        var drafts = StoredDrafts();
        if (drafts.RemoveAll(d => d.Id == id) == 0) return;
        if (drafts.Count == 0) data.Delete(DraftsName);
        else data.Write(DraftsName, drafts);
    }

    /// <summary>"Dragă Ana": the first words of the draft's text, or its title when it has none.</summary>
    public static string DraftWords(EditorDraft draft) => DocumentNames.Words(draft.Text, draft.Title);

    /// <summary>"Dragă Ana, din 23 octombrie 2026, ora 21:05".</summary>
    public string DraftLabel(EditorDraft draft)
    {
        var (date, time) = DraftMoment(draft);
        return Localizer.Get("editor.draft.item", ("words", DraftWords(draft)), ("date", date), ("time", time));
    }

    /// <summary>When the draft was written, in the user's time zone: "23 octombrie 2026" and "21:05".</summary>
    public (string Date, string Time) DraftMoment(EditorDraft draft)
    {
        var saved = TimeZoneInfo.ConvertTime(draft.SavedAt, context.Time.LocalTimeZone);
        var l = Localizer;
        return (l.Get("editor.draft.date", ("day", saved.Day), ("month", DateText.Month(saved.Month, l)), ("year", saved.Year)),
            saved.ToString("H:mm", CultureInfo.InvariantCulture));
    }

    /// <summary>The stored drafts, after moving the first version's single draft into the list.</summary>
    private List<EditorDraft> StoredDrafts()
    {
        if (context.UserData is not { } data) return [];
        var drafts = (data.Read<List<EditorDraft>>(DraftsName) ?? [])
            .Where(d => d is { Id: not null, Title: not null, Text: not null })
            .ToList();
        if (data.Read<EditorDraft>(OldDraftName) is { Title: not null, Text: not null } old)
        {
            drafts.Add(old with { Id = NewDraftId() });
            // Deleted only once the list holds it; until then it is offered again from the old entry.
            if (data.Write(DraftsName, drafts)) data.Delete(OldDraftName);
        }
        return drafts;
    }

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
