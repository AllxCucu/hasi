using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Text;

namespace Hasi.Core.Screens;

/// <summary>
/// A multi-line text editor. Typing follows the key echo setting like <see cref="TextInputScreen"/>; the arrows read
/// the character, the word (with Control) or the line; Backspace and Delete say what they deleted. Saving, the
/// editor's menu and leaving belong to whoever opened the screen, through the callbacks.
/// Everything it says is private content: masked in the diagnostic log, with the whole text shown on screen.
/// </summary>
/// <param name="onMenu">F10 / Ctrl+M; null: the key is unavailable.</param>
/// <param name="onSave">Ctrl+S; null: the key is unavailable.</param>
/// <param name="onBack">Escape; null: the screen closes.</param>
public sealed class TextEditScreen(
    string title,
    TextBuffer buffer,
    Func<IScreenContext, ScreenResult>? onMenu = null,
    Func<IScreenContext, ScreenResult>? onSave = null,
    Func<IScreenContext, ScreenResult>? onBack = null) : IScreen, ITextEntry, IHasKeyProfile, ISensitiveScreen
{
    /// <summary>The document's name; it changes when a new document is saved under a name.</summary>
    public string Title { get; set; } = title;

    public TextBuffer Buffer { get; } = buffer ?? throw new ArgumentNullException(nameof(buffer));

    public KeyProfile KeyProfile => KeyProfile.TextEditing;

    public Utterance Enter(EnterReason reason, IScreenContext context) =>
        Private(context.Localizer.Get("edit.enter", ("title", Title), ("line", LineDescription(context))));

    public ScreenResult Handle(InputEvent input, IScreenContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);
        if (input.Character is { } c && !char.IsControl(c)) return Typed(c, context);

        var l = context.Localizer;
        return input.Command switch
        {
            Command.NewLine => NewLine(context),
            Command.PreviousCharacter => Buffer.MovePreviousCharacter() ? Say(AtCursor(context)) : StartEdge(context),
            Command.NextCharacter => Buffer.MoveNextCharacter() ? Say(AtCursor(context)) : EndEdge(context),
            Command.PreviousWord => Buffer.MovePreviousWord() ? Say(WordOrCharacter(context)) : StartEdge(context),
            Command.NextWord => Buffer.MoveNextWord() ? Say(WordOrCharacter(context)) : EndEdge(context),
            Command.Previous => Buffer.MoveLineUp() ? Say(LineDescription(context)) : StartEdge(context),
            Command.Next => Buffer.MoveLineDown() ? Say(LineDescription(context)) : EndEdge(context),
            Command.LineStart => LineStart(context),
            Command.LineEnd => LineEnd(context),
            Command.DocumentStart => DocumentStart(context),
            Command.DocumentEnd => DocumentEnd(context),
            Command.DeletePrevious => Buffer.DeletePrevious() is { } deleted ? Deleted(deleted, context) : StartEdge(context),
            Command.DeleteNext => Buffer.DeleteNext() is { } deleted ? Deleted(deleted, context) : EndEdge(context),
            Command.Undo => Buffer.Undo()
                ? Say(l.Get("edit.undone", ("line", LineDescription(context))))
                : Say(l.Get("edit.nothingToUndo")),
            Command.Redo => Buffer.Redo()
                ? Say(l.Get("edit.redone", ("line", LineDescription(context))))
                : Say(l.Get("edit.nothingToRedo")),
            Command.Save => onSave?.Invoke(context) ?? ScreenResult.Unhandled,
            Command.ScreenMenu => onMenu?.Invoke(context) ?? ScreenResult.Unhandled,
            Command.Back => onBack?.Invoke(context) ?? ScreenResult.Pop(),
            _ => ScreenResult.Unhandled,
        };
    }

    public Utterance Help(IScreenContext context) => Private(context.Localizer.Get("edit.help"));

    /// <summary>"Rândul 3 din 10, coloana 5.", for the editor's menu.</summary>
    public Utterance Position(IScreenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Private(context.Localizer.Get("edit.position",
            ("line", Buffer.LineNumber), ("lines", Buffer.LineCount), ("column", Buffer.ColumnNumber)));
    }

    private ScreenResult Typed(char c, IScreenContext context)
    {
        Buffer.Insert(c);
        return context.Options.KeyEcho switch
        {
            Settings.KeyEcho.None => Say(""),
            // Word echo: letters are silent; a space or punctuation says the word just finished.
            Settings.KeyEcho.Word when TextBuffer.IsWordCharacter(c) => Say(""),
            Settings.KeyEcho.Word => Say(Buffer.WordEndingAt(Buffer.Cursor - 1) is { Length: > 0 } word ? word : CharacterName(c, context)),
            _ => Say(CharacterName(c, context)),
        };
    }

    private ScreenResult NewLine(IScreenContext context)
    {
        Buffer.InsertNewLine();
        return Say(context.Options.KeyEcho == Settings.KeyEcho.None ? "" : context.Localizer.Get("edit.newLine"));
    }

    private ScreenResult LineStart(IScreenContext context)
    {
        Buffer.MoveLineStart();
        return Say(Buffer.CurrentLine.Length == 0 ? context.Localizer.Get("edit.emptyLine") : AtCursor(context));
    }

    private ScreenResult LineEnd(IScreenContext context)
    {
        Buffer.MoveLineEnd();
        return Say(context.Localizer.Get("edit.lineEnd"));
    }

    private ScreenResult DocumentStart(IScreenContext context)
    {
        Buffer.MoveDocumentStart();
        return Say($"{context.Localizer.Get("edit.documentStart")} {LineDescription(context)}");
    }

    private ScreenResult DocumentEnd(IScreenContext context)
    {
        Buffer.MoveDocumentEnd();
        return Say(context.Localizer.Get("edit.documentEnd"));
    }

    private ScreenResult Deleted(char deleted, IScreenContext context)
    {
        var name = deleted == '\n' ? context.Localizer.Get("edit.lineBreakName") : CharacterName(deleted, context);
        return Say(context.Localizer.Get("textInput.deleted", ("char", name)));
    }

    private ScreenResult StartEdge(IScreenContext context) => Say(context.Localizer.Get("edit.documentStart"), Earcon.EdgeOfList);

    private ScreenResult EndEdge(IScreenContext context) => Say(context.Localizer.Get("edit.documentEnd"), Earcon.EdgeOfList);

    /// <summary>The character at the cursor: its name, "Sfârșit de rând" on a line break, "Sfârșitul documentului." at the end.</summary>
    private string AtCursor(IScreenContext context) => Buffer.CharacterAtCursor switch
    {
        null => context.Localizer.Get("edit.documentEnd"),
        '\n' => context.Localizer.Get("edit.lineEnd"),
        { } c => CharacterName(c, context),
    };

    private string WordOrCharacter(IScreenContext context) => Buffer.WordAtCursor ?? AtCursor(context);

    /// <summary>The current line's text, "Rând gol", or "Documentul este gol." when there is no text at all.</summary>
    private string LineDescription(IScreenContext context)
    {
        if (Buffer.IsEmpty) return context.Localizer.Get("edit.empty");
        var line = Buffer.CurrentLine;
        return line.Length == 0 ? context.Localizer.Get("edit.emptyLine") : line;
    }

    private ScreenResult Say(string text, Earcon earcon = Earcon.None) => ScreenResult.Say(Private(text, earcon));

    /// <summary>Masked in the diagnostic log; the window shows the whole document.</summary>
    private Utterance Private(string text, Earcon earcon = Earcon.None) =>
        new(text, Earcon: earcon, DisplayText: Buffer.Text, Sensitive: true);

    private static string CharacterName(char c, IScreenContext context) => KeyNames.Character(c, context.Localizer);
}
