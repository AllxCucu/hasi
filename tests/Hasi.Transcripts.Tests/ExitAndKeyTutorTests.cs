using Hasi.Core.Input;
using Hasi.Modules.About;
using Hasi.Modules.Exit;
using Hasi.Modules.KeyTutor;
using Hasi.Modules.Settings;
using Hasi.Testing;

namespace Hasi.Transcripts.Tests;

public class ExitAndKeyTutorTests
{
    private static ShellHarness Started() =>
        new ShellHarness().StartWithModules(new SettingsModule(), new KeyTutorModule(), new AboutModule(), new ExitModule());

    [Theory]
    [InlineData(1, "restart", "Calculatorul se repornește. La revedere.")]
    [InlineData(2, "shutdown", "Calculatorul se oprește. La revedere.")]
    public void A_confirmed_restart_or_shutdown_says_goodbye_and_asks_the_host(int item, string action, string goodbye)
    {
        var h = Started();
        h.Press(Key.End, Key.Enter);
        for (var i = 0; i < item; i++) h.Press(Key.Down);
        h.Press(Key.Enter);

        Assert.Equal([goodbye], h.Press(Key.Enter));
        Assert.Equal(action, h.System.PowerRequested);
        Assert.False(h.System.ExitRequested);
    }

    [Fact]
    public void Holding_enter_never_restarts_the_computer()
    {
        var h = Started();
        h.Press(Key.End, Key.Enter, Key.Down);

        h.Hold(Key.Enter, 20);

        Assert.Null(h.System.PowerRequested);
    }

    [Fact]
    public void Closing_the_window_from_outside_still_asks_even_while_learning_keys()
    {
        var h = Started();
        h.Press(Key.Down, Key.Enter);

        h.Shell.RequestExitConfirmation();

        Assert.Equal("Sigur doriți să închideți HASI? Apăsați Enter pentru da sau Escape pentru nu.", h.LastSaid);
    }

    [Fact]
    public void Typed_letters_digits_and_signs_are_named()
    {
        var h = Started();
        h.Press(Key.Down, Key.Enter);

        Assert.Equal(
            [
                "Litera ș: când scrieți, scrie acest caracter. În meniuri nu are nicio funcție.",
                "Cifra 7: când scrieți, scrie acest caracter. În meniuri nu are nicio funcție.",
                "virgulă: când scrieți, scrie acest caracter. În meniuri nu are nicio funcție.",
            ],
            h.Type("ș7,"));
    }

    [Fact]
    public void Holding_a_key_in_the_tutor_says_it_once()
    {
        var h = Started();
        h.Press(Key.Down, Key.Enter);

        Assert.Equal(["Săgeata jos: trece la opțiunea următoare."], h.Hold(Key.Down, 5));
    }

    [Fact]
    public void Main_menu_help_points_to_the_key_tutor()
    {
        var h = Started();

        Assert.EndsWith("Ca să aflați ce face fiecare tastă, alegeți Învață tastele.", h.Press(Key.F1)[0]);
    }
}
