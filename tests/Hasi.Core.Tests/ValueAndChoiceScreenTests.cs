using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Core.Settings;
using Hasi.Testing;

namespace Hasi.Core.Tests;

public class ValueAndChoiceScreenTests
{
    private static (ShellHarness Harness, Func<int> Value) Volume(int start = 50)
    {
        var value = start;
        var screen = new ValueScreen("Volum", () => value, v => value = v, 0, 100, 10, v => v.ToString());
        return (new ShellHarness().Start(screen), () => value);
    }

    [Fact]
    public void Value_screen_announces_the_current_value_and_how_to_change_it()
    {
        var (h, _) = Volume();

        Assert.Equal("Bun venit în HASI. Volum. 50. Săgețile stânga și dreapta schimbă valoarea. Enter sau Escape vă întoarce.", h.LastSaid);
    }

    [Fact]
    public void Arrows_change_the_value_at_once_and_say_it()
    {
        var (h, value) = Volume();

        Assert.Equal(["60", "70", "60", "50"], h.Press(Key.Right, Key.Up, Key.Left, Key.Down));
        Assert.Equal(50, value());
    }

    [Fact]
    public void The_limits_are_said_with_a_sound()
    {
        var (h, _) = Volume(start: 100);

        Assert.Equal(["Valoarea maximă. 100."], h.Press(Key.Right));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
        Assert.Equal(["0", "Valoarea minimă. 0."], h.Press(Key.Home, Key.Left));
    }

    [Fact]
    public void Holding_an_arrow_keeps_changing_the_value()
    {
        var (h, value) = Volume(start: 0);

        h.Hold(Key.Right, 4);

        Assert.Equal(50, value());
    }

    [Fact]
    public void Choice_screen_opens_on_the_current_option_marked_as_current()
    {
        var screen = new ChoiceScreen("Culori", ["Galben pe negru", "Alb pe negru"], 1,
            (i, _) => ScreenResult.Say($"ales {i}"), ", actual");

        var h = new ShellHarness().Start(screen);

        Assert.Equal("Bun venit în HASI. Culori. 2 opțiuni. Selectat: Alb pe negru, actual, 2 din 2.", h.LastSaid);
        Assert.Equal(["Galben pe negru, 1 din 2.", "ales 0"], h.Press(Key.Up, Key.Enter));
    }

    [Fact]
    public void Word_echo_says_each_word_when_it_is_finished()
    {
        var h = new ShellHarness(new ShellOptions { KeyEcho = KeyEcho.Word })
            .Start(new TextInputScreen("Test", "Scrieți.", (_, _) => ScreenResult.Handled));

        var said = h.Type("bună ziua.").Where(s => s.Length > 0).ToList();

        Assert.Equal(["bună", "ziua"], said);
        Assert.Equal("bună ziua.", h.LastAnnouncement!.Utterance.DisplayText);
    }

    [Fact]
    public void No_echo_types_in_silence_but_keeps_the_screen_up_to_date()
    {
        var h = new ShellHarness(new ShellOptions { KeyEcho = KeyEcho.None })
            .Start(new TextInputScreen("Test", "Scrieți.", (_, _) => ScreenResult.Handled));

        Assert.All(h.Type("abc"), s => Assert.Equal("", s));
        Assert.Equal("abc", h.LastAnnouncement!.Utterance.DisplayText);
        Assert.Equal(["Bun venit în HASI. Test. Scrieți."], h.Press(Key.F2)); // F2 repeats the last real message
    }
}
