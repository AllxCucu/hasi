using Hasi.Core.Input;
using Hasi.Core.Screens;
using Hasi.Testing;

namespace Hasi.Core.Tests;

public class GlobalCommandTests
{
    private const KeyModifiers Ctrl = KeyModifiers.Ctrl;

    private static ShellHarness Started() => new ShellHarness().Start(TestScreens.Fruits());

    public static TheoryData<KeyChord> HelpKeys => new(Key.F1, new KeyChord(Key.H, Ctrl), Key.Divide);

    public static TheoryData<KeyChord> RepeatKeys => new(Key.F2, new KeyChord(Key.R, Ctrl), Key.Multiply);

    public static TheoryData<KeyChord> WhereKeys => new(Key.F3, new KeyChord(Key.U, Ctrl), Key.Subtract);

    [Theory]
    [MemberData(nameof(HelpKeys))]
    public void Help_describes_the_current_screen(KeyChord key)
    {
        var h = Started();

        var said = Assert.Single(h.Press(key));
        Assert.StartsWith("Ajutor pentru Fructe. Folosiți săgețile sus și jos", said);
    }

    [Theory]
    [MemberData(nameof(RepeatKeys))]
    public void Repeat_says_the_last_message_again(KeyChord key)
    {
        var h = Started();
        h.Press(Key.Down);

        Assert.Equal(["Pară, 2 din 3."], h.Press(key));
    }

    [Fact]
    public void Repeat_after_help_repeats_the_help()
    {
        var h = Started();
        var help = h.Press(Key.F1);

        Assert.Equal(help, h.Press(Key.F2));
    }

    [Theory]
    [MemberData(nameof(WhereKeys))]
    public void Where_am_i_says_the_menu_path(KeyChord key)
    {
        var h = Started();
        h.Press(Key.Enter);

        Assert.Equal(["Sunteți în: Fructe, Măr."], h.Press(key));
    }

    [Fact]
    public void Help_on_a_message_screen_explains_escape()
    {
        var h = Started();
        h.Press(Key.Enter);

        Assert.Equal(
            ["Ajutor pentru Măr. Apăsați Enter pentru a asculta din nou mesajul și Escape pentru a vă întoarce."],
            h.Press(Key.F1));
    }

    [Fact]
    public void Global_commands_do_not_reach_the_screen()
    {
        var h = Started();
        h.Press(Key.F1, Key.F2, Key.F3);

        Assert.Equal(0, ((MenuScreen)h.Shell.Current).SelectedIndex);
    }
}
