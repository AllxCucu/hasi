using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Testing;

namespace Hasi.Core.Tests;

public class ShellNavigationTests
{
    private static ShellHarness Started(ShellOptions? options = null) =>
        new ShellHarness(options).Start(TestScreens.Fruits());

    [Fact]
    public void Startup_announces_welcome_place_count_and_selection()
    {
        var h = Started();

        Assert.Equal("Bun venit în HASI. Fructe. 3 opțiuni. Selectat: Măr, 1 din 3.", h.LastSaid);
    }

    [Fact]
    public void Arrows_move_and_announce_position()
    {
        var h = Started();

        Assert.Equal(["Pară, 2 din 3.", "Prună, 3 din 3.", "Pară, 2 din 3."], h.Press(Key.Down, Key.Down, Key.Up));
    }

    [Fact]
    public void Numeric_keypad_navigates_like_arrows()
    {
        var h = Started();

        Assert.Equal(["Pară, 2 din 3.", "Măr, 1 din 3."], h.Press(Key.NumPad2, Key.NumPad8));
    }

    [Fact]
    public void End_of_list_does_not_wrap_by_default_and_plays_earcon()
    {
        var h = Started();
        h.Press(Key.End);

        Assert.Equal(["Sfârșitul listei. Prună, 3 din 3."], h.Press(Key.Down));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
    }

    [Fact]
    public void Start_of_list_does_not_wrap_by_default()
    {
        var h = Started();

        Assert.Equal(["Începutul listei. Măr, 1 din 3."], h.Press(Key.Up));
    }

    [Fact]
    public void Wrapping_can_be_enabled()
    {
        var h = Started(new ShellOptions { WrapLists = true });

        Assert.Equal(["Prună, 3 din 3.", "Măr, 1 din 3."], h.Press(Key.Up, Key.Down));
        Assert.Equal(Earcon.EdgeOfList, h.LastAnnouncement!.Utterance.Earcon);
    }

    [Theory]
    [InlineData(Key.Home, Key.End)]
    [InlineData(Key.NumPad7, Key.NumPad1)]
    public void First_and_last(Key first, Key last)
    {
        var h = Started();

        Assert.Equal(["Prună, 3 din 3.", "Măr, 1 din 3."], h.Press(last, first));
    }

    [Fact]
    public void Laptop_alternatives_for_first_and_last()
    {
        var h = Started();

        Assert.Equal(["Prună, 3 din 3.", "Măr, 1 din 3."],
            h.Press(new KeyChord(Key.Down, KeyModifiers.Ctrl), new KeyChord(Key.Up, KeyModifiers.Ctrl)));
    }

    [Fact]
    public void Enter_opens_a_screen_and_escape_returns_with_selection()
    {
        var h = Started();

        Assert.Equal(["Măr. Un măr."], h.Press(Key.Enter));
        Assert.Equal(["Fructe. Selectat: Măr, 1 din 3."], h.Press(Key.Escape));
    }

    [Fact]
    public void Escape_on_the_root_screen_explains_how_to_exit_and_stays()
    {
        var h = Started();

        Assert.Equal(["Sunteți în meniul principal. Pentru a închide HASI, alegeți Ieșire."], h.Press(Key.Escape));
        Assert.Single(h.Shell.Stack);
        Assert.False(h.System.ExitRequested);
    }

    [Fact]
    public void Enter_on_an_item_without_action_says_it_again()
    {
        var h = Started();
        h.Press(Key.End);

        Assert.Equal(["Prună, 3 din 3."], h.Press(Key.Enter));
    }

    [Fact]
    public void Unbound_key_says_it_has_no_function_with_earcon()
    {
        var h = Started();

        Assert.Equal(["Această tastă nu are nicio funcție aici. Pentru ajutor, apăsați F1 sau Control H."], h.Press(Key.Q));
        Assert.Equal(Earcon.Unavailable, h.LastAnnouncement!.Utterance.Earcon);
    }

    [Fact]
    public void Modifier_alone_says_nothing()
    {
        var h = Started();

        Assert.Empty(h.Press(Key.LeftShift, Key.LeftAlt));
    }

    [Fact]
    public void Every_key_interrupts_speech()
    {
        var h = Started();
        var before = h.Output.CancelCount;

        h.Press(Key.LeftShift);

        Assert.True(h.Output.CancelCount > before);
    }

    [Theory]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.RightCtrl)]
    public void Ctrl_stops_speech_and_says_nothing(Key ctrl)
    {
        var h = Started();
        var before = h.Output.CancelCount;

        Assert.Empty(h.Press(ctrl));
        Assert.True(h.Output.CancelCount > before);
    }
}
