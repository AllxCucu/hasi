using Hasi.Core.Documents;
using Hasi.Core.Files;
using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Core.Text;

namespace Hasi.Modules.Editor;

/// <summary>
/// One document being edited: the core <see cref="TextEditScreen"/> plus saving, the document's menu, leaving it, and
/// the draft kept every 30 seconds while there are unsaved changes.
/// </summary>
/// <remarks>
/// Saving follows plan §3.2: a document with a path is saved over itself when it is text or a .docx made by HASI.
/// A new document, or a .docx written by another program, is saved as a new .docx in Documente, asking only its name;
/// the original is never written.
/// </remarks>
public sealed class EditorScreen : IScreen, ITextEntry, IHasKeyProfile, ISensitiveScreen, IUnsavedWork, ITickingScreen
{
    /// <summary>The least time between two drafts.</summary>
    public static readonly TimeSpan DraftInterval = TimeSpan.FromSeconds(30);

    private readonly EditorServices _services;
    private readonly TextEditScreen _edit;

    /// <summary>Opened from a draft: unsaved until it is saved or its changes are discarded.</summary>
    private bool _recovered;

    /// <summary>The text whose changes the user chose to lose (<see cref="Discard"/>); typing makes it unsaved again.</summary>
    private string? _discardedText;

    /// <summary>This document's draft among the stored ones; a recovered document keeps the id of its draft.</summary>
    private readonly string _draftId;

    /// <summary>A draft with <see cref="_draftId"/> is stored, so saving, discarding or closing deletes it.</summary>
    private bool _ownsDraft;

    private string? _draftText;
    private DateTimeOffset _draftTime;

    private EditorScreen(EditorServices services, string title, string text, string? path, DocumentFormat format, bool madeByHasi,
        string? recoveredDraftId = null)
    {
        _services = services;
        _edit = new TextEditScreen(title, new TextBuffer(text), OpenMenu, SaveKey, Back);
        FilePath = path;
        Format = format;
        MadeByHasi = madeByHasi;
        _draftId = recoveredDraftId ?? EditorServices.NewDraftId();
        _draftTime = services.Context.Time.GetLocalNow();
        if (recoveredDraftId is not null)
        {
            _recovered = true;
            _ownsDraft = true;
            _draftText = text;
        }
    }

    public string Title => _edit.Title;

    /// <summary>Where the document is saved; null for a new document.</summary>
    public string? FilePath { get; private set; }

    public DocumentFormat Format { get; private set; }

    /// <summary>A .docx written by HASI, saved over itself; any other .docx is saved as a new document.</summary>
    public bool MadeByHasi { get; private set; }

    public TextBuffer Buffer => _edit.Buffer;

    public KeyProfile KeyProfile => KeyProfile.TextEditing;

    public bool HasUnsavedChanges => (Buffer.IsModified || _recovered) && Buffer.Text != _discardedText;

    internal static EditorScreen New(EditorServices services) =>
        new(services, services.Localizer.Get("editor.newTitle"), "", null, DocumentFormat.Word, madeByHasi: false);

    internal static EditorScreen Opened(EditorServices services, string path, Document document) =>
        new(services, Path.GetFileNameWithoutExtension(path), string.Join('\n', document.Paragraphs.Select(p => LineOf(p.Text))),
            path, document.Format, document.MadeByHasi);

    internal static EditorScreen FromDraft(EditorServices services, EditorDraft draft) =>
        new(services, draft.Title, draft.Text, draft.Path, draft.Format, draft.MadeByHasi, recoveredDraftId: draft.Id);

    public Utterance Enter(EnterReason reason, IScreenContext context) => _edit.Enter(reason, context);

    public ScreenResult Handle(InputEvent input, IScreenContext context) => _edit.Handle(input, context);

    public Utterance Help(IScreenContext context) => _edit.Help(context);

    public string UnsavedDescription(IScreenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Localizer.Get("editor.unsaved", ("title", Title));
    }

    public ScreenResult Save(IScreenContext context, Func<ScreenResult> then)
    {
        ArgumentNullException.ThrowIfNull(then);
        return SaveFlow(context, _ => then());
    }

    public void Discard()
    {
        _discardedText = Buffer.Text;
        ForgetDraft();
    }

    public void SaveDraftNow()
    {
        if (HasUnsavedChanges) WriteDraft(_services.Context.Time.GetLocalNow());
    }

    public void Tick(DateTimeOffset now)
    {
        if (!HasUnsavedChanges || Buffer.Text == _draftText || now - _draftTime < DraftInterval) return;
        WriteDraft(now);
    }

    private void WriteDraft(DateTimeOffset now)
    {
        var text = Buffer.Text;
        if (!_services.WriteDraft(new EditorDraft(_draftId, Title, FilePath, Format, MadeByHasi, text, now))) return; // tried again at the next tick
        _ownsDraft = true;
        _draftText = text;
        _draftTime = now;
    }

    private void ForgetDraft()
    {
        if (_ownsDraft) _services.DeleteDraft(_draftId);
        _ownsDraft = false;
        _draftText = null;
    }

    /// <summary>Ctrl+S: saves and stays in the document.</summary>
    private ScreenResult SaveKey(IScreenContext context) => SaveFlow(context, said => BackToDocument(said));

    /// <summary>
    /// Saves over the document when that loses nothing, otherwise asks for a name. On success <paramref name="next"/>
    /// gets what to say; on failure the user is back in the document, hearing why, and nothing else happens.
    /// </summary>
    private ScreenResult SaveFlow(IScreenContext context, Func<Utterance, ScreenResult> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        var l = context.Localizer;
        if (FilePath is { } path && (Format == DocumentFormat.Text || MadeByHasi))
        {
            var problem = _services.Documents.Save(path, Paragraphs(), Format);
            if (problem != DocumentProblem.None) return Failed(problem);
            Saved(path, Title, Format);
            return next(l.Get("editor.saved"));
        }

        if (_services.Files.Places().FirstOrDefault(p => p.Kind == PlaceKind.Documents) is not { } documents)
        {
            return Failed(DocumentProblem.Unsupported); // no reason worth saying: no Documents folder
        }
        var foreign = FilePath is not null;
        var proposal = foreign
            ? l.Get("editor.copyName", ("name", Title))
            : DocumentNames.Proposal(Buffer.Text, l.Get("editor.defaultName"));
        return ScreenResult.Push(NameField(proposal, foreign ? l.Get("editor.name.foreign") : null, documents.Path,
            DocumentFormat.Word, (target, name) =>
            {
                var problem = _services.Documents.Save(target, Paragraphs(), DocumentFormat.Word);
                if (problem != DocumentProblem.None) return Failed(problem);
                Saved(target, name, DocumentFormat.Word);
                return next(l.Get("editor.savedAs", ("name", name)));
            }));
    }

    /// <summary>"Salvează o copie": the format, the place, then the name; the document itself stays as it is.</summary>
    private ScreenResult SaveCopy(IScreenContext context)
    {
        var l = context.Localizer;
        DocumentFormat[] formats = [DocumentFormat.Word, DocumentFormat.Text];
        return ScreenResult.Push(new ChoiceScreen(l.Get("editor.copy.formatTitle"), [l.Get("editor.copy.word"), l.Get("editor.copy.text")], 0,
            (index, _) => ScreenResult.Push(new PlacePickerScreen(l.Get("editor.copy.placeTitle"), _services.Files,
                (place, _) => ScreenResult.Push(NameField(Title, null, place.Path, formats[index], (target, name) =>
                {
                    var problem = _services.Documents.Save(target, Paragraphs(), formats[index]);
                    return problem == DocumentProblem.None
                        ? BackToDocument(l.Get("editor.copySaved", ("name", name)))
                        : Failed(problem);
                })))),
            currentMarker: "", help: l.Get("editor.copy.formatHelp")));
    }

    /// <summary>
    /// Asks for a name, proposing <paramref name="proposal"/> (Enter on the empty field takes it); a name already used in
    /// <paramref name="folder"/> is replaced only after a question.
    /// </summary>
    private NameFieldScreen NameField(string proposal, string? introduction, string folder, DocumentFormat format,
        Func<string, string, ScreenResult> write)
    {
        var l = _services.Localizer;
        var extension = _services.Documents.Extension(format);
        return new NameFieldScreen(new TextInputScreen(l.Get("editor.name.title"), l.Get("editor.name.prompt", ("proposal", proposal)),
            (typed, _) =>
            {
                var name = DocumentNames.Clean(typed, extension) ?? proposal;
                var target = Path.Combine(folder, name + extension);
                if (IsProtectedOriginal(target)) return ScreenResult.Say(l.Get("editor.name.original"));
                if (!_services.Files.Exists(target)) return write(target, name);
                return ScreenResult.Push(new ReplaceQuestionScreen(l.Get("editor.name.replace", ("name", name)), () => write(target, name),
                    l.Get("editor.name.replaceTitle"), l.Get("editor.name.replaceHelp")));
            },
            defaultValue: proposal), introduction);
    }

    /// <summary>A .docx written by another program: HASI never writes over it, not even when asked to replace it.</summary>
    private bool IsProtectedOriginal(string target) =>
        FilePath is { } original && Format == DocumentFormat.Word && !MadeByHasi && EditorServices.SamePath(original, target);

    private void Saved(string path, string title, DocumentFormat format)
    {
        FilePath = path;
        _edit.Title = title;
        Format = format;
        if (format == DocumentFormat.Word) MadeByHasi = true;
        Buffer.MarkSaved();
        _recovered = false;
        _discardedText = null;
        ForgetDraft();
        _services.AddRecent(path, title);
    }

    private ScreenResult Failed(DocumentProblem problem) =>
        BackToDocument(new Utterance(_services.SaveProblem(problem), Earcon: Earcon.Error));

    /// <summary>Closes whatever is open above the document and says <paramref name="said"/>.</summary>
    private ScreenResult BackToDocument(Utterance said) => ScreenResult.PopTo(this, then: ScreenResult.Say(said));

    private ScreenResult OpenMenu(IScreenContext context)
    {
        var l = context.Localizer;
        return ScreenResult.Push(new MenuScreen(l.Get("editor.menu.title"),
            [
                new MenuItem(l.Get("editor.menu.save"), c => SaveFlow(c, said => BackToDocument(said))),
                new MenuItem(l.Get("editor.menu.saveCopy"), SaveCopy),
                new MenuItem(l.Get("editor.menu.position"), c => BackToDocument(_edit.Position(c))),
                new MenuItem(l.Get("editor.menu.close"), c => ScreenResult.PopTo(this, then: Back(c))),
            ],
            help: l.Get("editor.menu.help")));
    }

    /// <summary>Escape: leaves an unchanged document; with unsaved changes, asks what to do first.</summary>
    private ScreenResult Back(IScreenContext context)
    {
        if (!HasUnsavedChanges) return Leave();
        var l = context.Localizer;
        return ScreenResult.Push(new QuestionScreen(l.Get("editor.leave.title"), l.Get("editor.leave.description"),
            [
                new MenuItem(l.Get("editor.leave.save"), c => SaveFlow(c, said => ScreenResult.PopTo(this, then: Leave(said)))),
                new MenuItem(l.Get("editor.leave.discard"), _ => ScreenResult.Confirm(l.Get("editor.leave.discardConfirm"), () =>
                {
                    Discard();
                    return ScreenResult.PopTo(this, then: Leave());
                })),
                new MenuItem(l.Get("editor.leave.stay"), _ => ScreenResult.Pop()),
            ],
            l.Get("editor.leave.help")));
    }

    /// <summary>Closes the document (it must be the current screen); its draft is no longer needed.</summary>
    private ScreenResult Leave(Utterance? said = null)
    {
        ForgetDraft();
        return ScreenResult.Pop(prefix: said);
    }

    private IReadOnlyList<DocumentParagraph> Paragraphs() => [.. Buffer.Text.Split('\n').Select(line => new DocumentParagraph(line))];

    /// <summary>The buffer separates lines only with '\n'.</summary>
    private static string LineOf(string paragraph) => paragraph.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\r', ' ').Replace('\n', ' ');
}
