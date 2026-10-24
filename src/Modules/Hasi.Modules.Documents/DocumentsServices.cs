using Hasi.Core.Documents;
using Hasi.Core.Files;
using Hasi.Core.Localization;
using Hasi.Core.Modules;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Core.Text;

namespace Hasi.Modules.Documents;

/// <summary>A document read lately, in the user's data, with the place where the user stopped.</summary>
/// <param name="Page">The PDF page of <paramref name="Position"/>; 0 for other formats.</param>
internal sealed record RecentReading(
    string Path, string Title, DocumentFormat Format, ReadingPosition Position, int Page, DateTimeOffset LastRead);

/// <summary>What the module's screens share: the module context, the recent documents and opening a document.</summary>
internal sealed class DocumentsServices(IModuleContext context)
{
    public const string RecentName = "documente-recente";

    public const int MaxRecent = 10;

    /// <summary>Up to this many scanned pages are named one by one; more are only counted.</summary>
    public const int MaxNamedScannedPages = 5;

    /// <summary>The document being opened in the background, so a second Enter does not open it twice.</summary>
    private BackgroundJob? _opening;

    public IModuleContext Context => context;

    public ILocalizer Localizer => context.Localizer;

    public IDocumentFormats Documents =>
        context.Documents ?? throw new InvalidOperationException("Reading documents needs the document formats service.");

    public IFileSystem Files => context.Files ?? throw new InvalidOperationException("Reading documents needs the file system service.");

    public static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Newest first.</summary>
    public IReadOnlyList<RecentReading> Recent() =>
        [.. (context.UserData?.Read<List<RecentReading>>(RecentName) ?? [])
            .Where(r => r is { Path: not null, Title: not null, Position: not null })
            .OrderByDescending(r => r.LastRead)
            .Take(MaxRecent)];

    /// <summary>Puts the document first in the recent list (once), keeping the last <see cref="MaxRecent"/>. False when it could not be written.</summary>
    public bool SaveRecent(RecentReading reading)
    {
        if (context.UserData is not { } data) return true;
        List<RecentReading> recent = [reading, .. Recent().Where(r => !SamePath(r.Path, reading.Path))];
        return data.Write(RecentName, recent.Take(MaxRecent).ToList());
    }

    public void RemoveRecent(string path) =>
        context.UserData?.Write(RecentName, Recent().Where(r => !SamePath(r.Path, path)).ToList());

    /// <summary>
    /// Loads the document in the background while <paramref name="owner"/> (a level of the file browser, the recent
    /// list) stays current; leaving it drops the result. After a second without an answer: "Se deschide documentul…".
    /// A problem is said and the user stays where they are; an opened document goes above <paramref name="root"/>.
    /// </summary>
    /// <param name="notFound">What to do instead of saying "not found" (the recent list removes the document).</param>
    public ScreenResult Open(string path, IScreen owner, IScreen root, Func<IScreenContext, ScreenResult>? notFound = null)
    {
        var opening = new Utterance(Localizer.Get("documents.opening"));
        if (_opening is { IsRunning: true }) return ScreenResult.Say(opening);
        _opening = context.Shell.RunInBackground(owner, _ => Documents.Load(path),
            (loaded, c) => Opened(path, loaded, root, notFound, c), opening);
        return ScreenResult.Handled;
    }

    private ScreenResult Opened(string path, DocumentLoadResult loaded, IScreen root,
        Func<IScreenContext, ScreenResult>? notFound, IScreenContext screenContext)
    {
        var l = Localizer;
        if (loaded.Document is not { } document)
        {
            if (loaded.Problem == DocumentProblem.NotFound && notFound is not null) return notFound(screenContext);
            return ScreenResult.Say(new Utterance(OpenProblem(loaded.Problem), Earcon: Earcon.Error));
        }

        // Nothing HASI can read: said once, and the document is not kept among the recent ones.
        if (IsScannedOnly(document)) return ScreenResult.Say(new Utterance(l.Get("documents.scannedOnly"), Earcon: Earcon.Error));

        var saved = Recent().FirstOrDefault(r => SamePath(r.Path, path));
        var start = saved is null ? null : ResumeAt(document, saved.Position);
        var reader = new DocumentReaderScreen(this, path, Path.GetFileNameWithoutExtension(path), document, start, Notices(document, start));
        SaveRecent(reader.Reading(context.Time.GetUtcNow()));
        return ScreenResult.PopTo(root, then: ScreenResult.Push(reader));
    }

    public string OpenProblem(DocumentProblem problem) => Localizer.Get(problem switch
    {
        DocumentProblem.NotFound => "documents.open.notFound",
        DocumentProblem.TooLarge => "documents.open.tooLarge",
        DocumentProblem.Protected => "documents.open.protected",
        DocumentProblem.NoAccess => "documents.open.noAccess",
        _ => "documents.open.damaged",
    });

    /// <summary>A PDF whose every page is a scanned image.</summary>
    private static bool IsScannedOnly(Document document) =>
        document.Format == DocumentFormat.Pdf && document.ScannedPages.Count > 0
        && document.ScannedPages.Distinct().Count() >= document.PageCount;

    /// <summary>
    /// Where a saved position resumes: the start of the sentence that contains it, in the same paragraph. Null for the
    /// beginning of the document, or when the document has no text.
    /// </summary>
    private static ReadingPosition? ResumeAt(Document document, ReadingPosition saved)
    {
        if (document.Paragraphs.Count == 0 || saved == ReadingPosition.Beginning) return null;
        var paragraph = Math.Clamp(saved.Paragraph, 0, document.Paragraphs.Count - 1);
        var text = document.Paragraphs[paragraph].Text.Replace('\r', ' ').Replace('\n', ' ');
        var offset = Math.Clamp(saved.Offset, 0, text.Length);
        var sentence = Sentences.Split(text).LastOrDefault(s => s.Start <= offset);
        var start = new ReadingPosition(paragraph, sentence == default ? 0 : sentence.Start);
        return start == ReadingPosition.Beginning ? null : start;
    }

    /// <summary>
    /// What is special about the document, said once after its title, before the first line: the scanned pages, text
    /// that looks broken, and where reading resumes. Null when there is nothing to say.
    /// </summary>
    private string? Notices(Document document, ReadingPosition? start)
    {
        var l = Localizer;
        List<string> notices = [];
        if (document.Format == DocumentFormat.Pdf && document.ScannedPages.Count > 0)
        {
            notices.Add(ScannedNotice([.. document.ScannedPages.Distinct().Order()]));
        }
        if (document.LooksGarbled) notices.Add(l.Get("documents.garbled"));
        if (start is not null)
        {
            var paragraph = document.Paragraphs[start.Paragraph];
            notices.Add(document.Format switch
            {
                DocumentFormat.Pdf when paragraph.Page > 0 => l.Get("documents.resume.page", ("page", paragraph.Page)),
                DocumentFormat.Epub when paragraph.Chapter > 0 => l.Get("documents.resume.chapter", ("chapter", paragraph.Chapter)),
                _ => l.Get("documents.resume.other"),
            });
        }
        return notices.Count == 0 ? null : string.Join(' ', notices);
    }

    /// <summary>"Pagina 3 este o imagine scanată.", "Paginile 3, 4 și 7 sunt imagini scanate." or "8 pagini sunt imagini scanate."</summary>
    private string ScannedNotice(IReadOnlyList<int> pages)
    {
        var l = Localizer;
        if (pages.Count == 1) return l.Get("documents.scanned.one", ("page", pages[0]));
        if (pages.Count > MaxNamedScannedPages) return l.Plural("documents.scanned.count", pages.Count);
        var list = l.Get("documents.list.and", ("items", string.Join(", ", pages.Take(pages.Count - 1))), ("last", pages[^1]));
        return l.Get("documents.scanned.some", ("pages", list));
    }
}
