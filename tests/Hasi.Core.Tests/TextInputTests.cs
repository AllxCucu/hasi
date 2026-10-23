using Hasi.Core.Input;
using Hasi.Core.Screens;
using Hasi.Testing;

namespace Hasi.Core.Tests;

public class TextInputTests
{
    private static (ShellHarness Harness, List<string> Submitted) Started()
    {
        var submitted = new List<string>();
        var screen = new TextInputScreen("Test", "Scrieți ceva.", (text, _) =>
        {
            submitted.Add(text);
            return ScreenResult.Say($"Trimis: {text}");
        });
        return (new ShellHarness().Start(screen), submitted);
    }

    [Fact]
    public void Entering_announces_title_and_prompt()
    {
        var (h, _) = Started();

        Assert.Equal("Bun venit în HASI. Test. Scrieți ceva.", h.LastSaid);
    }

    [Fact]
    public void Every_typed_character_is_echoed()
    {
        var (h, _) = Started();

        Assert.Equal(["ș", "a", "spațiu", "1", "virgulă"], h.Type("șa 1,"));
    }

    [Fact]
    public void The_screen_shows_the_text_typed_so_far()
    {
        var (h, _) = Started();
        h.Type("ăî");

        Assert.Equal("ăî", h.LastAnnouncement!.Utterance.DisplayText);
    }

    [Fact]
    public void Backspace_deletes_and_says_what_was_deleted()
    {
        var (h, _) = Started();
        h.Type("ab");

        Assert.Equal(["Șters: b"], h.Press(Key.Backspace));
        Assert.Equal(["Textul scris: a"], h.Press(Key.Down));
    }

    [Fact]
    public void Backspace_on_an_empty_field_says_it_is_empty()
    {
        var (h, _) = Started();

        Assert.Equal(["Câmpul este gol."], h.Press(Key.Backspace));
    }

    [Fact]
    public void Arrows_read_the_whole_text()
    {
        var (h, _) = Started();
        h.Type("Bună ziua");

        Assert.Equal(["Textul scris: Bună ziua"], h.Press(Key.Up));
    }

    [Fact]
    public void Enter_submits_and_keeps_the_text_for_corrections()
    {
        var (h, submitted) = Started();
        h.Type("12 lei");

        Assert.Equal(["Trimis: 12 lei"], h.Press(Key.Enter));
        h.Type("!");
        h.Press(Key.Enter);
        Assert.Equal(["12 lei", "12 lei!"], submitted);
    }

    [Fact]
    public void Enter_on_an_empty_field_asks_for_text()
    {
        var (h, submitted) = Started();

        Assert.Equal(["Câmpul este gol. Scrieți mai întâi un text."], h.Press(Key.Enter));
        Assert.Empty(submitted);
    }

    [Fact]
    public void Global_keys_still_work_in_a_text_field()
    {
        var (field, _) = Started();

        Assert.StartsWith("Ajutor pentru Test.", field.Press(Key.F1)[0]);
        Assert.Equal(["Sunteți în: Test."], field.Press(Key.F3));
    }

    [Fact]
    public void Letters_in_a_menu_still_say_the_key_has_no_function()
    {
        var h = new ShellHarness().Start(TestScreens.Fruits());

        Assert.Equal(["Această tastă nu are nicio funcție aici. Pentru ajutor, apăsați F1 sau Control H."], h.Type("q"));
    }

    [Fact]
    public void Holding_backspace_keeps_deleting()
    {
        var (h, _) = Started();
        h.Type("abc");

        Assert.Equal(["Șters: c", "Șters: b", "Șters: a"], h.Hold(Key.Backspace, 2));
    }
}
