using System.Text;
using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Text;

namespace Hasi.Core.Screens;

/// <summary>
/// Moving the cursor in a <see cref="TextBuffer"/> and what is said about it, the same in the editor and in the reader:
/// the character (left, right), the word (Control with left, right), the line (up, down), the start and end of the line
/// and of the document, with the edge sound and "Începutul documentului." / "Sfârșitul documentului." at the edges.
/// </summary>
/// <param name="line">How the current line is said; null: <see cref="PlainLine"/>. The reader names headings.</param>
internal sealed class TextNavigator(TextBuffer buffer, Func<IScreenContext, string>? line = null)
{
    public TextBuffer Buffer { get; } = buffer ?? throw new ArgumentNullException(nameof(buffer));

    /// <summary>Applies a move; returns what to say, or null when <paramref name="command"/> is not a move.</summary>
    public (string Text, Earcon Earcon)? Move(Command command, IScreenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return command switch
        {
            Command.PreviousCharacter => Buffer.MovePreviousCharacter() ? (AtCursor(context), Earcon.None) : StartEdge(context),
            Command.NextCharacter => Buffer.MoveNextCharacter() ? (AtCursor(context), Earcon.None) : EndEdge(context),
            Command.PreviousWord => Buffer.MovePreviousWord() ? (WordOrCharacter(context), Earcon.None) : StartEdge(context),
            Command.NextWord => Buffer.MoveNextWord() ? (WordOrCharacter(context), Earcon.None) : EndEdge(context),
            Command.Previous => Buffer.MoveLineUp() ? (Line(context), Earcon.None) : StartEdge(context),
            Command.Next => Buffer.MoveLineDown() ? (Line(context), Earcon.None) : EndEdge(context),
            Command.LineStart => LineStart(context),
            Command.LineEnd => LineEnd(context),
            Command.DocumentStart => DocumentStart(context),
            Command.DocumentEnd => DocumentEnd(context),
            _ => null,
        };
    }

    /// <summary>The current line as said after a move up or down.</summary>
    public string Line(IScreenContext context) => line is null ? PlainLine(context) : line(context);

    /// <summary>The current line's text, "Rând gol", or "Documentul este gol." when there is no text at all.</summary>
    public string PlainLine(IScreenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Buffer.IsEmpty) return context.Localizer.Get("edit.empty");
        var text = Buffer.CurrentLine;
        return text.Length == 0 ? context.Localizer.Get("edit.emptyLine") : text;
    }

    public (string Text, Earcon Earcon) StartEdge(IScreenContext context) => (context.Localizer.Get("edit.documentStart"), Earcon.EdgeOfList);

    public (string Text, Earcon Earcon) EndEdge(IScreenContext context) => (context.Localizer.Get("edit.documentEnd"), Earcon.EdgeOfList);

    /// <summary>
    /// A text element as said: punctuation by name, a letter written with a combining mark as the letter ("ș"), an
    /// emoji as it is.
    /// </summary>
    public static string ElementName(string element, IScreenContext context)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(context);
        var composed = element.IsNormalized(NormalizationForm.FormC) ? element : element.Normalize(NormalizationForm.FormC);
        return composed.Length == 1 ? KeyNames.Character(composed[0], context.Localizer) : composed;
    }

    private (string, Earcon) LineStart(IScreenContext context)
    {
        Buffer.MoveLineStart();
        return (Buffer.CurrentLine.Length == 0 ? context.Localizer.Get("edit.emptyLine") : AtCursor(context), Earcon.None);
    }

    private (string, Earcon) LineEnd(IScreenContext context)
    {
        Buffer.MoveLineEnd();
        return (context.Localizer.Get("edit.lineEnd"), Earcon.None);
    }

    private (string, Earcon) DocumentStart(IScreenContext context)
    {
        Buffer.MoveDocumentStart();
        return ($"{context.Localizer.Get("edit.documentStart")} {Line(context)}", Earcon.None);
    }

    private (string, Earcon) DocumentEnd(IScreenContext context)
    {
        Buffer.MoveDocumentEnd();
        return (context.Localizer.Get("edit.documentEnd"), Earcon.None);
    }

    /// <summary>The element at the cursor: its name, "Sfârșit de rând" on a line break, "Sfârșitul documentului." at the end.</summary>
    private string AtCursor(IScreenContext context) => Buffer.TextElementAtCursor switch
    {
        null => context.Localizer.Get("edit.documentEnd"),
        "\n" => context.Localizer.Get("edit.lineEnd"),
        { } element => ElementName(element, context),
    };

    private string WordOrCharacter(IScreenContext context) => Buffer.WordAtCursor ?? AtCursor(context);
}
