using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Core.Settings;
using Hasi.Core.Text;
using Hasi.Testing;

namespace Hasi.Transcripts.Tests;

/// <summary>The text editor with the real Romanian resources: keys pressed, text heard.</summary>
public class TextEditScreenTests
{
    private static readonly KeyChord CtrlLeft = new(Key.Left, KeyModifiers.Ctrl);
    private static readonly KeyChord CtrlRight = new(Key.Right, KeyModifiers.Ctrl);
    private static readonly KeyChord CtrlHome = new(Key.Home, KeyModifiers.Ctrl);
    private static readonly KeyChord CtrlEnd = new(Key.End, KeyModifiers.Ctrl);
    private static readonly KeyChord CtrlUp = new(Key.Up, KeyModifiers.Ctrl);
    private static readonly KeyChord CtrlDown = new(Key.Down, KeyModifiers.Ctrl);
    private static readonly KeyChord CtrlZ = new(Key.Z, KeyModifiers.Ctrl);
    private static readonly KeyChord CtrlY = new(Key.Y, KeyModifiers.Ctrl);
    private static readonly KeyChord CtrlS = new(Key.S, KeyModifiers.Ctrl);
    private static readonly KeyChord CtrlM = new(Key.M, KeyModifiers.Ctrl);

    private const string StartOfDocument = "Începutul documentului.";
    private const string EndOfDocument = "Sfârșitul documentului.";

    private static (ShellHarness H, TextEditScreen Editor) Opened(
        string text = "",
        int cursor = 0,
        KeyEcho echo = KeyEcho.Character,
        Func<IScreenContext, ScreenResult>? onMenu = null,
        Func<IScreenContext, ScreenResult>? onSave = null,
        Func<IScreenContext, ScreenResult>? onBack = null,
        TimeProvider? time = null)
    {
        var editor = new TextEditScreen("Scrisoare", new TextBuffer(text, cursor), onMenu, onSave, onBack);
        var h = new ShellHarness(new ShellOptions { KeyEcho = echo }, time: time)
            .Start(new MenuScreen("Editor", [new MenuItem("Document", _ => ScreenResult.Push(editor))]));
        h.Press(Key.Enter);
        Assert.Same(editor, h.Shell.Current);
        return (h, editor);
    }

    [Fact]
    public void Entering_says_the_title_and_the_current_line()
    {
        var (h, _) = Opened("Dragă Ana,\nCe mai faci?", cursor: 12);

        Assert.Equal("Scrisoare. Ce mai faci?", h.LastSaid);
    }

    [Fact]
    public void Entering_an_empty_document_says_it_is_empty()
    {
        var (h, _) = Opened();

        Assert.Equal("Scrisoare. Documentul este gol.", h.LastSaid);
    }

    [Fact]
    public void Entering_on_an_empty_line_says_so()
    {
        var (h, _) = Opened("unu\n\ntrei", cursor: 4);

        Assert.Equal("Scrisoare. Rând gol", h.LastSaid);
    }

    [Fact]
    public void Coming_back_from_a_menu_says_the_line_again()
    {
        var (h, _) = Opened("Bună ziua", onMenu: _ => ScreenResult.Push(new MenuScreen("Meniul editorului", [new MenuItem("Salvează")])));
        h.Press(Key.F10);

        Assert.Equal(["Scrisoare. Bună ziua"], h.Press(Key.Escape));
    }

    [Fact]
    public void Everything_said_is_private_and_the_window_shows_the_whole_text()
    {
        var (h, _) = Opened("unu\ndoi");
        var before = h.Output.Announcements.Count;

        h.Press(Key.Right, Key.Down, Key.End, CtrlHome, Key.Delete, CtrlZ, Key.F1);
        h.Type("x");

        var said = h.Output.Announcements.Skip(before).Select(a => a.Utterance).ToList();
        Assert.Equal(8, said.Count);
        Assert.All(said, u => Assert.True(u.Sensitive));
        Assert.Equal("xunu\ndoi", said[^1].DisplayText);
        Assert.Equal("unu\ndoi", said[0].DisplayText);
    }

    [Fact]
    public void Typed_characters_are_echoed_by_name()
    {
        var (h, editor) = Opened();

        Assert.Equal(["ș", "a", "spațiu", "1", "virgulă"], h.Type("șa 1,"));
        Assert.Equal("șa 1,", editor.Buffer.Text);
    }

    [Fact]
    public void Word_echo_says_each_finished_word()
    {
        var (h, _) = Opened(echo: KeyEcho.Word);

        Assert.Equal(["Bună", "ziua"], h.Type("Bună ziua.").Where(s => s.Length > 0));
    }

    [Fact]
    public void Word_echo_names_punctuation_that_follows_no_word()
    {
        var (h, _) = Opened(echo: KeyEcho.Word);

        Assert.Equal(["virgulă"], h.Type(",").Where(s => s.Length > 0));
    }

    [Fact]
    public void No_echo_types_and_starts_lines_in_silence()
    {
        var (h, editor) = Opened(echo: KeyEcho.None);

        Assert.All(h.Type("abc"), s => Assert.Equal("", s));
        Assert.Equal([""], h.Press(Key.Enter));
        Assert.Equal("abc\n", editor.Buffer.Text);
        Assert.Equal("abc\n", h.LastAnnouncement!.Utterance.DisplayText);
    }

    [Fact]
    public void Enter_starts_a_new_line()
    {
        var (h, editor) = Opened();
        h.Type("unu");

        Assert.Equal(["Rând nou"], h.Press(Key.Enter));
        h.Type("doi");
        Assert.Equal("unu\ndoi", editor.Buffer.Text);
        Assert.Equal(2, editor.Buffer.LineNumber);
    }

    [Fact]
    public void Left_and_right_say_the_character_at_the_cursor()
    {
        var (h, _) = Opened("aș, b\nc");

        Assert.Equal(["ș", "virgulă", "spațiu", "b", "Sfârșit de rând", "c", EndOfDocument], h.Press(
            Key.Right, Key.Right, Key.Right, Key.Right, Key.Right, Key.Right, Key.Right));
        Assert.Equal(["c", "Sfârșit de rând", "b"], h.Press(Key.Left, Key.Left, Key.Left));
    }

    [Fact]
    public void Left_at_the_start_and_right_at_the_end_do_not_move()
    {
        var (h, editor) = Opened("ab");

        Assert.Equal([StartOfDocument], h.Press(Key.Left));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
        Assert.Equal(0, editor.Buffer.Cursor);

        h.Press(Key.End);
        Assert.Equal([EndOfDocument], h.Press(Key.Right));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
        Assert.Equal(2, editor.Buffer.Cursor);
    }

    [Fact]
    public void Control_with_the_arrows_says_the_word()
    {
        var (h, _) = Opened("Țară, mamă! Știu");

        Assert.Equal(["mamă", "Știu", EndOfDocument], h.Press(CtrlRight, CtrlRight, CtrlRight));
        Assert.Equal(["Știu", "mamă", "Țară"], h.Press(CtrlLeft, CtrlLeft, CtrlLeft));
    }

    [Fact]
    public void Control_with_the_arrows_stops_at_the_edges()
    {
        var (h, editor) = Opened("unu doi");

        Assert.Equal([StartOfDocument], h.Press(CtrlLeft));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
        h.Press(CtrlEnd);
        Assert.Equal([EndOfDocument], h.Press(CtrlRight));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
        Assert.Equal(7, editor.Buffer.Cursor);
    }

    [Fact]
    public void Control_left_from_after_punctuation_says_what_is_there_when_no_word_is_before()
    {
        var (h, _) = Opened("  unu", cursor: 1);

        Assert.Equal(["spațiu"], h.Press(CtrlLeft));
    }

    [Fact]
    public void Up_and_down_say_the_line()
    {
        var (h, _) = Opened("Dragă Ana,\n\nCe mai faci?");

        Assert.Equal(["Rând gol", "Ce mai faci?"], h.Press(Key.Down, Key.Down));
        Assert.Equal(["Rând gol", "Dragă Ana,"], h.Press(Key.Up, Key.Up));
    }

    [Fact]
    public void Up_on_the_first_line_and_down_on_the_last_say_the_edge()
    {
        var (h, _) = Opened("unu\ndoi");

        Assert.Equal([StartOfDocument], h.Press(Key.Up));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
        h.Press(Key.Down);
        Assert.Equal([EndOfDocument], h.Press(Key.Down));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
    }

    [Fact]
    public void Home_and_end_go_to_the_start_and_end_of_the_line()
    {
        var (h, editor) = Opened("unu\nșapte", cursor: 6);

        Assert.Equal(["ș"], h.Press(Key.Home));
        Assert.Equal(4, editor.Buffer.Cursor);
        Assert.Equal(["Sfârșit de rând"], h.Press(Key.End));
        Assert.Equal(9, editor.Buffer.Cursor);
    }

    [Fact]
    public void Home_on_an_empty_line_says_it_is_empty()
    {
        var (h, _) = Opened("unu\n\ndoi", cursor: 4);

        Assert.Equal(["Rând gol"], h.Press(Key.Home));
    }

    [Fact]
    public void Control_home_and_control_end_go_to_the_start_and_end_of_the_document()
    {
        var (h, editor) = Opened("unu\ndoi\ntrei", cursor: 5);

        Assert.Equal(["Începutul documentului. unu"], h.Press(CtrlHome));
        Assert.Equal(0, editor.Buffer.Cursor);
        Assert.Equal([EndOfDocument], h.Press(CtrlEnd));
        Assert.Equal(12, editor.Buffer.Cursor);
        Assert.Equal(Earcon.None, h.LastAnnouncement!.Utterance.Earcon);
        Assert.Equal(["Începutul documentului. unu", EndOfDocument], h.Press(CtrlUp, CtrlDown));
    }

    [Fact]
    public void Backspace_says_what_it_deleted_including_a_line_break()
    {
        var (h, editor) = Opened("aș\nb", cursor: 4);

        Assert.Equal(["Șters: b", "Șters: rând nou", "Șters: ș"], h.Press(Key.Backspace, Key.Backspace, Key.Backspace));
        Assert.Equal("a", editor.Buffer.Text);
    }

    [Fact]
    public void A_letter_with_a_combining_mark_moves_is_said_and_deleted_as_one_character()
    {
        var (h, editor) = Opened("așb");

        Assert.Equal(["ș"], h.Press(Key.Right));
        Assert.Equal(["b"], h.Press(Key.Right));
        Assert.Equal(["ș"], h.Press(Key.Left));
        Assert.Equal(["Șters: ș"], h.Press(Key.Delete));
        Assert.Equal("ab", editor.Buffer.Text);
    }

    [Fact]
    public void An_emoji_typed_as_two_halves_is_echoed_once_and_deleted_whole()
    {
        var (h, editor) = Opened();

        Assert.Equal(["", "\U0001F600"], h.Type("\U0001F600"));
        Assert.Equal(["\U0001F600"], h.Press(Key.Left));
        Assert.Equal(["Șters: \U0001F600"], h.Press(Key.Delete));
        Assert.Equal("", editor.Buffer.Text);
    }

    [Fact]
    public void Backspace_at_the_start_says_the_edge()
    {
        var (h, editor) = Opened("ab");

        Assert.Equal([StartOfDocument], h.Press(Key.Backspace));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
        Assert.Equal("ab", editor.Buffer.Text);
    }

    [Fact]
    public void Delete_says_what_it_deleted_and_stops_at_the_end()
    {
        var (h, editor) = Opened("a,\nb", cursor: 1);

        Assert.Equal(["Șters: virgulă", "Șters: rând nou", "Șters: b", EndOfDocument], h.Press(Key.Delete, Key.Delete, Key.Delete, Key.Delete));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
        Assert.Equal("a", editor.Buffer.Text);
    }

    [Fact]
    public void Holding_an_arrow_or_delete_keeps_going()
    {
        var (h, editor) = Opened("abcd");

        Assert.Equal(["b", "c", "d"], h.Hold(Key.Right, 2));
        Assert.Equal(["Șters: d", EndOfDocument], h.Hold(Key.Delete, 1));
        Assert.Equal("abc", editor.Buffer.Text);
    }

    [Fact]
    public void Control_z_undoes_and_control_y_redoes_a_word_at_a_time()
    {
        var (h, editor) = Opened();
        h.Type("Bună ziua");

        Assert.Equal(["Anulat. Bună "], h.Press(CtrlZ));
        Assert.Equal(["Anulat. Documentul este gol."], h.Press(CtrlZ));
        Assert.Equal(["Nu este nimic de anulat."], h.Press(CtrlZ));
        Assert.Equal(["Refăcut. Bună ", "Refăcut. Bună ziua"], h.Press(CtrlY, CtrlY));
        Assert.Equal(["Nu este nimic de refăcut."], h.Press(CtrlY));
        Assert.Equal("Bună ziua", editor.Buffer.Text);
    }

    [Fact]
    public void Undo_after_a_new_line_says_the_line_it_returns_to()
    {
        var (h, _) = Opened();
        h.Type("unu");
        h.Press(Key.Enter);

        Assert.Equal(["Anulat. unu"], h.Press(CtrlZ));
    }

    [Fact]
    public void Undo_on_an_empty_line_says_so()
    {
        var (h, _) = Opened("unu\n", cursor: 4);
        h.Type("x");

        Assert.Equal(["Anulat. Rând gol"], h.Press(CtrlZ));
    }

    [Fact]
    public void Position_says_line_and_column()
    {
        var (h, editor) = Opened("unu\ndoi\ntrei", cursor: 6);

        Assert.Equal("Rândul 2 din 3, coloana 3.", editor.Position(h.Shell).Text);
        Assert.True(editor.Position(h.Shell).Sensitive);
    }

    [Fact]
    public void F1_explains_the_editor_keys()
    {
        var (h, _) = Opened("text");
        const string help = "Scrieți cu tastatura. Săgețile stânga și dreapta citesc litera, Control cu săgețile stânga și dreapta "
            + "citește cuvântul, iar săgețile sus și jos citesc rândul. Home și End duc la începutul și la sfârșitul rândului, "
            + "Control Home și Control End la începutul și la sfârșitul documentului. Backspace și Delete șterg. Control Z anulează, "
            + "Control Y reface. Control S salvează. F10 sau Control M deschide meniul. Escape iese din document.";

        Assert.Equal([help], h.Press(Key.F1));
        Assert.Equal([help], h.Press(new KeyChord(Key.H, KeyModifiers.Ctrl)));
    }

    [Fact]
    public void Control_r_repeats_control_u_says_where_and_control_t_says_the_time_inside_the_editor()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-24T11:32:00Z"), TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest"));
        var (h, editor) = Opened("abc", time: time);
        h.Press(Key.Right);

        Assert.Equal(["b"], h.Press(new KeyChord(Key.R, KeyModifiers.Ctrl)));
        Assert.Equal(["b"], h.Press(Key.F2));
        Assert.Equal(["Sunteți în: Editor, Scrisoare."], h.Press(new KeyChord(Key.U, KeyModifiers.Ctrl)));
        Assert.Equal(["Este ora 14:32."], h.Press(new KeyChord(Key.T, KeyModifiers.Ctrl)));
        Assert.Equal("abc", editor.Buffer.Text);
        Assert.Equal(1, editor.Buffer.Cursor);
        Assert.Same(editor, h.Shell.Current);
    }

    [Fact]
    public void Control_s_and_the_menu_keys_call_the_callbacks()
    {
        var saves = 0;
        var menus = 0;
        var (h, _) = Opened("text",
            onMenu: _ => { menus++; return ScreenResult.Say("Meniu"); },
            onSave: _ => { saves++; return ScreenResult.Say("Salvat"); });

        Assert.Equal(["Salvat", "Meniu", "Meniu"], h.Press(CtrlS, Key.F10, CtrlM));
        Assert.Equal(1, saves);
        Assert.Equal(2, menus);
    }

    [Fact]
    public void Without_callbacks_save_and_menu_are_unavailable()
    {
        var (h, _) = Opened("text");

        Assert.Equal(["Această tastă nu are nicio funcție aici. Pentru ajutor, apăsați F1 sau Control H."], h.Press(CtrlS));
        Assert.Equal(Earcon.Unavailable, h.LastAnnouncement!.Utterance.Earcon);
        Assert.Equal(["Această tastă nu are nicio funcție aici. Pentru ajutor, apăsați F1 sau Control H."], h.Press(Key.F10));
    }

    [Fact]
    public void Escape_without_a_callback_closes_the_editor()
    {
        var (h, _) = Opened("text");

        h.Press(Key.Escape);

        Assert.IsType<MenuScreen>(h.Shell.Current);
    }

    [Fact]
    public void Escape_with_a_callback_lets_the_owner_decide()
    {
        var (h, editor) = Opened("text", onBack: _ => ScreenResult.Say("Documentul are modificări nesalvate."));

        Assert.Equal(["Documentul are modificări nesalvate."], h.Press(Key.Escape));
        Assert.Same(editor, h.Shell.Current);
    }

    [Fact]
    public void A_question_over_the_editor_uses_the_default_keys()
    {
        var closed = false;
        var (h, editor) = Opened("text", onBack: _ => ScreenResult.Confirm("Închideți documentul?", () =>
        {
            closed = true;
            return ScreenResult.Pop();
        }));
        h.Press(Key.Escape);

        Assert.Equal(KeyProfile.Default, h.Shell.ActiveKeyProfile);
        h.Press(Key.Enter); // Activate (yes), not a new line

        Assert.True(closed);
        Assert.Equal("text", editor.Buffer.Text);
        Assert.IsType<MenuScreen>(h.Shell.Current);
    }

    [Fact]
    public void A_menu_over_the_editor_uses_the_default_keys()
    {
        var (h, _) = Opened("text", onMenu: _ => ScreenResult.Push(new MenuScreen("Meniul editorului",
            [new MenuItem("Salvează"), new MenuItem("Spune poziția"), new MenuItem("Închide documentul")])));
        Assert.Equal(KeyProfile.TextEditing, h.Shell.ActiveKeyProfile);

        h.Press(CtrlM);
        Assert.Equal(KeyProfile.Default, h.Shell.ActiveKeyProfile);
        h.Press(Key.End);

        Assert.Equal(2, ((MenuScreen)h.Shell.Current).SelectedIndex);
    }

    [Fact]
    public void Keys_the_editor_does_not_use_are_unavailable()
    {
        var (h, _) = Opened("text");

        Assert.Equal(["Această tastă nu are nicio funcție aici. Pentru ajutor, apăsați F1 sau Control H."], h.Press(Key.PageDown));
    }

    [Fact]
    public void With_num_lock_on_the_keypad_types_digits_in_the_editor()
    {
        var (h, _) = Opened();
        var map = h.KeyMap;

        Assert.True(h.Shell.IsTextEntryActive);
        Assert.True(TextRouting.ProducesText(Key.NumPad4, map, h.Shell.IsTextEntryActive, numLockOn: true, h.Shell.ActiveKeyProfile));
        Assert.True(TextRouting.ProducesText(Key.NumPad7, map, h.Shell.IsTextEntryActive, numLockOn: true, h.Shell.ActiveKeyProfile));
        Assert.False(TextRouting.ProducesText(Key.Enter, map, h.Shell.IsTextEntryActive, numLockOn: true, h.Shell.ActiveKeyProfile));
        Assert.False(TextRouting.ProducesText(Key.Multiply, map, h.Shell.IsTextEntryActive, numLockOn: true, h.Shell.ActiveKeyProfile));
    }

    [Fact]
    public void Every_command_has_a_description_for_learning_the_keys()
    {
        var h = new ShellHarness();

        foreach (var command in Enum.GetValues<Command>())
        {
            var description = h.Localizer.Get($"command.{command}");
            Assert.False(string.IsNullOrWhiteSpace(description), command.ToString());
            Assert.NotEqual($"command.{command}", description);
        }
    }
}
