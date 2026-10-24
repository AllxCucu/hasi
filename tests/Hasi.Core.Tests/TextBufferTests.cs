using Hasi.Core.Text;

namespace Hasi.Core.Tests;

public class TextBufferTests
{
    private static TextBuffer Typed(string text)
    {
        var buffer = new TextBuffer();
        foreach (var c in text)
        {
            if (c == '\n') buffer.InsertNewLine();
            else buffer.Insert(c);
        }
        return buffer;
    }

    [Fact]
    public void An_empty_document_has_one_empty_line()
    {
        var buffer = new TextBuffer();

        Assert.True(buffer.IsEmpty);
        Assert.Equal(1, buffer.LineNumber);
        Assert.Equal(1, buffer.LineCount);
        Assert.Equal(1, buffer.ColumnNumber);
        Assert.Equal("", buffer.CurrentLine);
        Assert.Null(buffer.CharacterAtCursor);
        Assert.Null(buffer.WordAtCursor);
        Assert.False(buffer.IsModified);
    }

    [Fact]
    public void Nothing_moves_or_deletes_in_an_empty_document()
    {
        var buffer = new TextBuffer();

        Assert.False(buffer.MovePreviousCharacter());
        Assert.False(buffer.MoveNextCharacter());
        Assert.False(buffer.MovePreviousWord());
        Assert.False(buffer.MoveNextWord());
        Assert.False(buffer.MoveLineUp());
        Assert.False(buffer.MoveLineDown());
        Assert.Null(buffer.DeletePrevious());
        Assert.Null(buffer.DeleteNext());
        Assert.False(buffer.CanUndo);
        Assert.False(buffer.IsModified);
    }

    [Fact]
    public void Typing_inserts_at_the_cursor_with_diacritics()
    {
        var buffer = Typed("ăâîșț");

        Assert.Equal("ăâîșț", buffer.Text);
        Assert.Equal(5, buffer.Cursor);
        buffer.MoveDocumentStart();
        buffer.MoveNextCharacter();
        buffer.Insert('x');
        Assert.Equal("ăxâîșț", buffer.Text);
        Assert.Equal(2, buffer.Cursor);
    }

    [Fact]
    public void Lines_columns_and_the_current_line_are_counted_from_one()
    {
        var buffer = new TextBuffer("Bună ziua\nCe faci?\nBine", cursor: 13);

        Assert.Equal(2, buffer.LineNumber);
        Assert.Equal(3, buffer.LineCount);
        Assert.Equal(4, buffer.ColumnNumber);
        Assert.Equal("Ce faci?", buffer.CurrentLine);
        Assert.Equal('f', buffer.CharacterAtCursor);
    }

    [Fact]
    public void A_final_line_break_starts_one_more_empty_line()
    {
        var buffer = new TextBuffer("abc\n");
        buffer.MoveDocumentEnd();

        Assert.Equal(2, buffer.LineCount);
        Assert.Equal(2, buffer.LineNumber);
        Assert.Equal("", buffer.CurrentLine);
    }

    [Fact]
    public void The_last_line_without_a_line_break_is_a_line()
    {
        var buffer = new TextBuffer("unu\ndoi");
        buffer.MoveDocumentEnd();

        Assert.Equal(2, buffer.LineCount);
        Assert.Equal("doi", buffer.CurrentLine);
        Assert.False(buffer.MoveLineDown());
        Assert.True(buffer.MoveLineUp());
        Assert.Equal("unu", buffer.CurrentLine);
        Assert.Equal(3, buffer.Cursor);
    }

    [Fact]
    public void Characters_move_across_line_breaks_and_stop_at_the_edges()
    {
        var buffer = new TextBuffer("a\nb");

        Assert.False(buffer.MovePreviousCharacter());
        Assert.True(buffer.MoveNextCharacter());
        Assert.Equal('\n', buffer.CharacterAtCursor);
        Assert.True(buffer.MoveNextCharacter());
        Assert.Equal('b', buffer.CharacterAtCursor);
        Assert.True(buffer.MoveNextCharacter());
        Assert.True(buffer.IsAtEnd);
        Assert.False(buffer.MoveNextCharacter());
        Assert.Equal(3, buffer.Cursor);
    }

    [Fact]
    public void Next_word_goes_to_the_start_of_the_next_word_skipping_punctuation()
    {
        var buffer = new TextBuffer("Țară, mamă! Știu.");

        Assert.True(buffer.MoveNextWord());
        Assert.Equal("mamă", buffer.WordAtCursor);
        Assert.Equal(6, buffer.Cursor);
        Assert.True(buffer.MoveNextWord());
        Assert.Equal("Știu", buffer.WordAtCursor);
        Assert.True(buffer.MoveNextWord()); // no word follows: the end of the text
        Assert.True(buffer.IsAtEnd);
        Assert.Null(buffer.WordAtCursor);
        Assert.False(buffer.MoveNextWord());
    }

    [Fact]
    public void Next_word_from_the_middle_of_a_word_goes_to_the_next_one()
    {
        var buffer = new TextBuffer("învățătoare școală", cursor: 3);

        Assert.True(buffer.MoveNextWord());
        Assert.Equal(12, buffer.Cursor);
        Assert.Equal("școală", buffer.WordAtCursor);
    }

    [Fact]
    public void Previous_word_goes_to_the_start_of_the_current_word_then_the_previous_one()
    {
        var buffer = new TextBuffer("într-o zi, 12 mere", cursor: 16);

        Assert.True(buffer.MovePreviousWord());
        Assert.Equal(14, buffer.Cursor);
        Assert.Equal("mere", buffer.WordAtCursor);
        Assert.True(buffer.MovePreviousWord());
        Assert.Equal("12", buffer.WordAtCursor);
        Assert.True(buffer.MovePreviousWord());
        Assert.Equal("zi", buffer.WordAtCursor);
        Assert.True(buffer.MovePreviousWord());
        Assert.Equal("o", buffer.WordAtCursor); // the hyphen separates words
        Assert.True(buffer.MovePreviousWord());
        Assert.Equal("într", buffer.WordAtCursor);
        Assert.True(buffer.IsAtStart);
        Assert.False(buffer.MovePreviousWord());
    }

    [Fact]
    public void Words_continue_over_line_breaks()
    {
        var buffer = new TextBuffer("sus\njos");

        Assert.True(buffer.MoveNextWord());
        Assert.Equal("jos", buffer.WordAtCursor);
        Assert.Equal(2, buffer.LineNumber);
        Assert.True(buffer.MovePreviousWord());
        Assert.Equal("sus", buffer.WordAtCursor);
    }

    [Fact]
    public void Decomposed_diacritics_belong_to_the_word()
    {
        var buffer = new TextBuffer("șase ani");

        Assert.Equal("șase", buffer.WordAtCursor);
    }

    [Fact]
    public void The_word_ending_before_a_position_is_found()
    {
        var buffer = new TextBuffer("Bună ziua,");

        Assert.Equal("ziua", buffer.WordEndingAt(9));
        Assert.Equal("Bună", buffer.WordEndingAt(4));
        Assert.Equal("", buffer.WordEndingAt(10));
        Assert.Equal("", buffer.WordEndingAt(0));
    }

    [Fact]
    public void Up_and_down_keep_the_desired_column_across_shorter_lines()
    {
        var buffer = new TextBuffer("primul rând\nscurt\n\nal patrulea rând", cursor: 9);

        Assert.True(buffer.MoveLineDown());
        Assert.Equal(17, buffer.Cursor); // end of "scurt"
        Assert.True(buffer.MoveLineDown());
        Assert.Equal("", buffer.CurrentLine);
        Assert.True(buffer.MoveLineDown());
        Assert.Equal(10, buffer.ColumnNumber); // back to column 10
        Assert.False(buffer.MoveLineDown());
        Assert.True(buffer.MoveLineUp());
        Assert.True(buffer.MoveLineUp());
        Assert.True(buffer.MoveLineUp());
        Assert.Equal(9, buffer.Cursor);
        Assert.False(buffer.MoveLineUp());
    }

    [Fact]
    public void Moving_sideways_forgets_the_desired_column()
    {
        var buffer = new TextBuffer("lung de tot\nab\nlung de tot", cursor: 8);
        buffer.MoveLineDown();
        buffer.MovePreviousCharacter();
        buffer.MoveLineDown();

        Assert.Equal(2, buffer.ColumnNumber);
    }

    [Fact]
    public void Line_start_and_end_and_document_start_and_end()
    {
        var buffer = new TextBuffer("unu\ndoi\ntrei", cursor: 5);

        buffer.MoveLineStart();
        Assert.Equal(4, buffer.Cursor);
        buffer.MoveLineEnd();
        Assert.Equal(7, buffer.Cursor);
        Assert.Equal('\n', buffer.CharacterAtCursor);
        buffer.MoveDocumentEnd();
        Assert.Equal(12, buffer.Cursor);
        buffer.MoveDocumentStart();
        Assert.Equal(0, buffer.Cursor);
    }

    [Fact]
    public void Deleting_returns_what_was_deleted_including_line_breaks()
    {
        var buffer = new TextBuffer("aș\nb", cursor: 3);

        Assert.Equal('\n', buffer.DeletePrevious());
        Assert.Equal("așb", buffer.Text);
        Assert.Equal(2, buffer.Cursor);
        Assert.Equal('b', buffer.DeleteNext());
        Assert.Null(buffer.DeleteNext());
        Assert.Equal('ș', buffer.DeletePrevious());
        Assert.Equal('a', buffer.DeletePrevious());
        Assert.Null(buffer.DeletePrevious());
        Assert.True(buffer.IsEmpty);
    }

    [Fact]
    public void A_typed_word_with_its_space_is_one_undo_step()
    {
        var buffer = Typed("Bună ziua");

        Assert.True(buffer.Undo());
        Assert.Equal("Bună ", buffer.Text);
        Assert.Equal(5, buffer.Cursor);
        Assert.True(buffer.Undo());
        Assert.Equal("", buffer.Text);
        Assert.False(buffer.Undo());
    }

    [Fact]
    public void Movement_ends_the_typing_step()
    {
        var buffer = Typed("ab");
        buffer.MovePreviousCharacter();
        buffer.MoveNextCharacter();
        buffer.Insert('c');

        Assert.True(buffer.Undo());
        Assert.Equal("ab", buffer.Text);
        Assert.Equal(2, buffer.Cursor);
    }

    [Fact]
    public void Each_deletion_and_new_line_is_its_own_step()
    {
        var buffer = Typed("abc");
        buffer.DeletePrevious();
        buffer.DeletePrevious();
        buffer.InsertNewLine();

        Assert.True(buffer.Undo());
        Assert.Equal("a", buffer.Text);
        Assert.True(buffer.Undo());
        Assert.Equal("ab", buffer.Text);
        Assert.True(buffer.Undo());
        Assert.Equal("abc", buffer.Text);
        Assert.True(buffer.Undo());
        Assert.Equal("", buffer.Text);
    }

    [Fact]
    public void Typing_after_a_deletion_starts_a_new_step()
    {
        var buffer = Typed("ab");
        buffer.DeletePrevious();
        buffer.Insert('c');
        buffer.Insert('d');

        Assert.True(buffer.Undo());
        Assert.Equal("a", buffer.Text);
    }

    [Fact]
    public void Redo_repeats_what_was_undone_and_a_new_edit_clears_it()
    {
        var buffer = Typed("unu doi");
        buffer.Undo();

        Assert.True(buffer.CanRedo);
        Assert.True(buffer.Redo());
        Assert.Equal("unu doi", buffer.Text);
        Assert.Equal(7, buffer.Cursor);
        Assert.False(buffer.Redo());

        buffer.Undo();
        buffer.Insert('x');
        Assert.False(buffer.CanRedo);
        Assert.False(buffer.Redo());
    }

    [Fact]
    public void At_most_200_steps_are_kept()
    {
        var buffer = new TextBuffer();
        for (var i = 0; i < 250; i++) buffer.InsertNewLine();

        var undone = 0;
        while (buffer.Undo()) undone++;

        Assert.Equal(TextBuffer.MaxUndoSteps, undone);
        Assert.Equal(50, buffer.Length);
    }

    [Fact]
    public void Editing_marks_the_document_modified_until_saved()
    {
        var buffer = new TextBuffer("text");
        Assert.False(buffer.IsModified);

        buffer.MoveDocumentEnd();
        Assert.False(buffer.IsModified); // moving changes nothing
        buffer.Insert('e');
        Assert.True(buffer.IsModified);
        buffer.MarkSaved();
        Assert.False(buffer.IsModified);
    }

    [Fact]
    public void Undoing_back_to_the_saved_text_makes_it_unmodified_again()
    {
        var buffer = Typed("unu ");
        buffer.MarkSaved();
        buffer.Insert('d'); // continues no step: saving ended the typing run

        Assert.True(buffer.IsModified);
        buffer.Undo();
        Assert.Equal("unu ", buffer.Text);
        Assert.False(buffer.IsModified);
        buffer.Undo();
        Assert.True(buffer.IsModified);
        buffer.Redo();
        Assert.False(buffer.IsModified);
    }

    [Fact]
    public void Saving_in_the_middle_of_a_word_lets_undo_return_to_the_saved_text()
    {
        var buffer = Typed("ab");
        buffer.MarkSaved();
        buffer.Insert('c');
        buffer.Undo();

        Assert.Equal("ab", buffer.Text);
        Assert.False(buffer.IsModified);
    }

    [Fact]
    public void Undoing_all_the_typing_of_a_new_document_leaves_it_unmodified()
    {
        var buffer = Typed("ceva");
        buffer.Undo();

        Assert.True(buffer.IsEmpty);
        Assert.False(buffer.IsModified);
    }

    [Fact]
    public void A_cursor_outside_the_text_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextBuffer("ab", cursor: 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextBuffer("ab", cursor: -1));
    }
}
