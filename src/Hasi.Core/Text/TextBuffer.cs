using System.Globalization;

namespace Hasi.Core.Text;

/// <summary>
/// The text of a document being edited, with a cursor, undo and redo. Pure logic: what is said about it is the
/// editor screen's job. Lines are separated only by '\n' (callers normalize "\r\n" before handing the text over).
/// A line is the logical line between two line breaks, never the line as the window wraps it.
/// A "character" is a text element (a grapheme cluster): an emoji written as a surrogate pair, or "ș" written as s plus
/// a combining comma below, moves and is deleted as one unit, and typing never leaves half of a surrogate pair.
/// </summary>
public sealed class TextBuffer
{
    /// <summary>How many undo steps are kept; older ones are forgotten.</summary>
    public const int MaxUndoSteps = 200;

    private readonly List<Snapshot> _undo = [];
    private readonly List<Snapshot> _redo = [];
    private string _text;
    private int _cursor;
    private long _version;
    private long _nextVersion;
    private long _savedVersion;

    /// <summary>The column (0-based) kept while moving up and down across shorter lines; null after any other action.</summary>
    private int? _desiredColumn;

    /// <summary>Typed characters since this was set belong to the same undo step.</summary>
    private bool _typingRun;

    /// <summary>
    /// The first half of a surrogate pair typed alone (the keyboard delivers an emoji as two characters); it is
    /// inserted, with its second half, as one element only when that second half arrives.
    /// </summary>
    private char? _pendingHighSurrogate;

    public TextBuffer(string text = "", int cursor = 0)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(cursor);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(cursor, text.Length);
        _text = text;
        _cursor = cursor;
    }

    public string Text => _text;

    public int Length => _text.Length;

    /// <summary>Position between characters: 0 is before the first one, <see cref="Length"/> after the last.</summary>
    public int Cursor => _cursor;

    public bool IsEmpty => _text.Length == 0;

    public bool IsAtStart => _cursor == 0;

    public bool IsAtEnd => _cursor == _text.Length;

    /// <summary>The current line, counted from 1.</summary>
    public int LineNumber
    {
        get
        {
            var line = 1;
            for (var i = 0; i < _cursor; i++)
            {
                if (_text[i] == '\n') line++;
            }
            return line;
        }
    }

    /// <summary>Number of lines; an empty document has one (empty) line, and a final '\n' starts one more.</summary>
    public int LineCount
    {
        get
        {
            var count = 1;
            foreach (var c in _text)
            {
                if (c == '\n') count++;
            }
            return count;
        }
    }

    /// <summary>The cursor's column in its line, counted from 1.</summary>
    public int ColumnNumber => _cursor - LineStartOf(_cursor) + 1;

    /// <summary>The text of the current line, without its line break.</summary>
    public string CurrentLine
    {
        get
        {
            var start = LineStartOf(_cursor);
            return _text[start..LineEndOf(_cursor)];
        }
    }

    /// <summary>The character right after the cursor ('\n' at the end of a line), or null at the end of the text.</summary>
    public char? CharacterAtCursor => _cursor < _text.Length ? _text[_cursor] : null;

    /// <summary>
    /// The text element right after the cursor ("\n" at the end of a line): one character, a surrogate pair, or a letter
    /// with its combining marks; null at the end of the text.
    /// </summary>
    public string? TextElementAtCursor => _cursor < _text.Length ? _text.Substring(_cursor, ElementLengthAt(_cursor)) : null;

    /// <summary>The whole word the character at the cursor belongs to, or null when that character is not part of a word.</summary>
    public string? WordAtCursor
    {
        get
        {
            if (_cursor >= _text.Length || !IsWordCharacter(_text[_cursor])) return null;
            var start = _cursor;
            while (start > 0 && IsWordCharacter(_text[start - 1])) start--;
            var end = _cursor;
            while (end < _text.Length && IsWordCharacter(_text[end])) end++;
            return _text[start..end];
        }
    }

    /// <summary>The word that ends right before <paramref name="position"/>, or "" when none does.</summary>
    public string WordEndingAt(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, _text.Length);
        var start = position;
        while (start > 0 && IsWordCharacter(_text[start - 1])) start--;
        return _text[start..position];
    }

    /// <summary>Whether the text differs from the last <see cref="MarkSaved"/> (or from the text it was created with).</summary>
    public bool IsModified => _version != _savedVersion;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>The current text is what is saved now; undoing back to it later makes the document unmodified again.</summary>
    public void MarkSaved()
    {
        _savedVersion = _version;
        _typingRun = false;
    }

    /// <summary>
    /// Types one character. Consecutive characters form one undo step; a space or other separator typed after a word
    /// closes the step, so undo removes one word at a time. A high surrogate waits for its low surrogate, and the pair
    /// is inserted as one element; half a pair alone is dropped.
    /// </summary>
    public void Insert(char c)
    {
        if (char.IsHighSurrogate(c))
        {
            _pendingHighSurrogate = c;
            return;
        }
        if (char.IsLowSurrogate(c))
        {
            if (_pendingHighSurrogate is { } high) InsertElement(string.Concat(high, c));
            _pendingHighSurrogate = null;
            return;
        }
        if (c == '\n')
        {
            InsertNewLine();
            return;
        }
        InsertElement(c.ToString());
    }

    public void InsertNewLine()
    {
        _pendingHighSurrogate = null;
        BeginStep();
        Change(_text.Insert(_cursor, "\n"), _cursor + 1);
    }

    /// <summary>Deletes the text element before the cursor (Backspace) and returns it, or null at the start.</summary>
    public string? DeletePrevious()
    {
        if (_cursor == 0)
        {
            Moved(false);
            return null;
        }
        var start = ElementStartAtOrBefore(_cursor - 1);
        var deleted = _text[start.._cursor];
        BeginStep();
        Change(_text.Remove(start, deleted.Length), start);
        return deleted;
    }

    /// <summary>Deletes the text element at the cursor (Delete) and returns it, or null at the end.</summary>
    public string? DeleteNext()
    {
        if (_cursor == _text.Length)
        {
            Moved(false);
            return null;
        }
        var deleted = _text.Substring(_cursor, ElementLengthAt(_cursor));
        BeginStep();
        Change(_text.Remove(_cursor, deleted.Length), _cursor);
        return deleted;
    }

    /// <summary>Moves one text element left; false (and no move) at the start.</summary>
    public bool MovePreviousCharacter() => _cursor == 0 ? Moved(false) : MoveTo(ElementStartAtOrBefore(_cursor - 1));

    /// <summary>Moves one text element right; false (and no move) at the end.</summary>
    public bool MoveNextCharacter() => _cursor == _text.Length ? Moved(false) : MoveTo(_cursor + ElementLengthAt(_cursor));

    /// <summary>
    /// Puts the cursor at <paramref name="position"/>, or at the start of the text element it falls inside; false (and
    /// no move) outside the text.
    /// </summary>
    public bool MoveTo(int position)
    {
        if (position < 0 || position > _text.Length) return Moved(false);
        _cursor = ElementStartAtOrBefore(position);
        return Moved(true);
    }

    /// <summary>To the start of the current word, or of the previous one when already there; false at the start.</summary>
    public bool MovePreviousWord()
    {
        if (_cursor == 0) return Moved(false);
        var position = _cursor;
        while (position > 0 && !IsWordCharacter(_text[position - 1])) position--;
        while (position > 0 && IsWordCharacter(_text[position - 1])) position--;
        return MoveTo(position);
    }

    /// <summary>To the start of the next word, or to the end of the text when no word follows; false at the end.</summary>
    public bool MoveNextWord()
    {
        if (_cursor == _text.Length) return Moved(false);
        var position = _cursor;
        while (position < _text.Length && IsWordCharacter(_text[position])) position++;
        while (position < _text.Length && !IsWordCharacter(_text[position])) position++;
        return MoveTo(position);
    }

    /// <summary>To the line above, keeping the column when the line is long enough; false on the first line.</summary>
    public bool MoveLineUp()
    {
        _typingRun = false;
        var start = LineStartOf(_cursor);
        if (start == 0) return false; // the desired column stays, as in Notepad
        var column = _desiredColumn ?? _cursor - start;
        var previousEnd = start - 1;
        var previousStart = LineStartOf(previousEnd);
        _cursor = ElementStartAtOrBefore(previousStart + Math.Min(column, previousEnd - previousStart));
        _pendingHighSurrogate = null;
        _desiredColumn = column;
        return true;
    }

    /// <summary>To the line below, keeping the column when the line is long enough; false on the last line.</summary>
    public bool MoveLineDown()
    {
        _typingRun = false;
        var end = LineEndOf(_cursor);
        if (end == _text.Length) return false;
        var column = _desiredColumn ?? _cursor - LineStartOf(_cursor);
        var nextStart = end + 1;
        var nextEnd = LineEndOf(nextStart);
        _cursor = ElementStartAtOrBefore(nextStart + Math.Min(column, nextEnd - nextStart));
        _pendingHighSurrogate = null;
        _desiredColumn = column;
        return true;
    }

    public void MoveLineStart() => MoveTo(LineStartOf(_cursor));

    public void MoveLineEnd() => MoveTo(LineEndOf(_cursor));

    public void MoveDocumentStart() => MoveTo(0);

    public void MoveDocumentEnd() => MoveTo(_text.Length);

    /// <summary>Goes back one step; false when there is nothing to undo.</summary>
    public bool Undo() => Restore(_undo, _redo);

    /// <summary>Repeats a step undone before; false when there is nothing to redo.</summary>
    public bool Redo() => Restore(_redo, _undo);

    /// <summary>Letters and digits, Romanian diacritics included, and combining marks (ș written as s plus a comma below).</summary>
    public static bool IsWordCharacter(char c) =>
        char.IsLetterOrDigit(c) || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;

    private bool Restore(List<Snapshot> from, List<Snapshot> to)
    {
        _typingRun = false;
        _pendingHighSurrogate = null;
        _desiredColumn = null;
        if (from.Count == 0) return false;
        to.Add(Current());
        var snapshot = from[^1];
        from.RemoveAt(from.Count - 1);
        (_text, _cursor, _version) = snapshot;
        return true;
    }

    /// <summary>Starts a new undo step: remembers the text as it is now and forgets what could be redone.</summary>
    private void BeginStep()
    {
        _undo.Add(Current());
        if (_undo.Count > MaxUndoSteps) _undo.RemoveAt(0);
        _redo.Clear();
        _typingRun = false;
    }

    private void Change(string text, int cursor)
    {
        _text = text;
        _cursor = cursor;
        _version = ++_nextVersion;
        _desiredColumn = null;
    }

    /// <summary>One typed element (a character or a whole surrogate pair) at the cursor, in the current typing run.</summary>
    private void InsertElement(string element)
    {
        _pendingHighSurrogate = null;
        if (!_typingRun) BeginStep();
        var closesWord = !IsWordCharacter(element[0]) && _cursor > 0 && IsWordCharacter(_text[_cursor - 1]);
        Change(_text.Insert(_cursor, element), _cursor + element.Length);
        _typingRun = !closesWord;
    }

    /// <summary>
    /// Any movement, even one that fails at an edge, ends the typing run, forgets the desired column and drops half a
    /// surrogate pair still waiting for its second half.
    /// </summary>
    private bool Moved(bool moved)
    {
        _typingRun = false;
        _desiredColumn = null;
        _pendingHighSurrogate = null;
        return moved;
    }

    /// <summary>Length of the text element that starts at <paramref name="position"/> (a position before the end).</summary>
    private int ElementLengthAt(int position) => Math.Max(1, StringInfo.GetNextTextElementLength(_text.AsSpan(position)));

    /// <summary>
    /// The start of the text element that contains <paramref name="position"/>, or the position itself when an element
    /// starts there. Elements never cross a line break, so the search starts at the start of the line.
    /// </summary>
    private int ElementStartAtOrBefore(int position)
    {
        var boundary = LineStartOf(position);
        while (boundary < position)
        {
            var next = boundary + ElementLengthAt(boundary);
            if (next > position) break;
            boundary = next;
        }
        return boundary;
    }

    private Snapshot Current() => new(_text, _cursor, _version);

    private int LineStartOf(int position) => position == 0 ? 0 : _text.LastIndexOf('\n', position - 1) + 1;

    private int LineEndOf(int position)
    {
        var end = _text.IndexOf('\n', position);
        return end < 0 ? _text.Length : end;
    }

    private sealed record Snapshot(string Text, int Cursor, long Version);
}
