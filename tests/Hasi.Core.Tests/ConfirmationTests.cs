using Hasi.Core.Input;
using Hasi.Core.Screens;
using Hasi.Testing;

namespace Hasi.Core.Tests;

public class ConfirmationTests
{
    private static ShellHarness StartedWithConfirmOnPear(Action onYes) =>
        new ShellHarness().Start(TestScreens.Fruits(_ => ScreenResult.Confirm("Ștergeți para?", () =>
        {
            onYes();
            return ScreenResult.Say("Para a fost ștearsă.");
        })));

    [Fact]
    public void Confirmation_asks_with_enter_and_escape_instructions()
    {
        var h = StartedWithConfirmOnPear(() => { });
        h.Press(Key.Down);

        Assert.Equal(["Ștergeți para? Apăsați Enter pentru da sau Escape pentru nu."], h.Press(Key.Enter));
    }

    [Fact]
    public void Escape_cancels_and_returns_to_the_screen_below()
    {
        var done = false;
        var h = StartedWithConfirmOnPear(() => done = true);
        h.Press(Key.Down, Key.Enter);

        Assert.Equal(["Anulat. Fructe. Selectat: Pară, 2 din 3."], h.Press(Key.Escape));
        Assert.False(done);
        Assert.Single(h.Shell.Stack);
    }

    [Fact]
    public void Enter_confirms_and_says_only_the_outcome()
    {
        var done = false;
        var h = StartedWithConfirmOnPear(() => done = true);
        h.Press(Key.Down, Key.Enter);

        Assert.Equal(["Para a fost ștearsă."], h.Press(Key.Enter));
        Assert.True(done);
        Assert.Single(h.Shell.Stack);
    }

    [Fact]
    public void Other_keys_repeat_the_question()
    {
        var h = StartedWithConfirmOnPear(() => { });
        h.Press(Key.Down, Key.Enter);

        Assert.Equal(["Ștergeți para? Apăsați Enter pentru da sau Escape pentru nu."], h.Press(Key.Down));
    }

    [Fact]
    public void Alt_F4_asks_before_closing()
    {
        var h = new ShellHarness().Start(TestScreens.Fruits());

        Assert.Equal(["Sigur doriți să închideți HASI? Apăsați Enter pentru da sau Escape pentru nu."],
            h.Press(new KeyChord(Key.F4, KeyModifiers.Alt)));
        Assert.False(h.System.ExitRequested);
    }

    [Fact]
    public void Confirmed_exit_says_goodbye_and_requests_exit()
    {
        var h = new ShellHarness().Start(TestScreens.Fruits());
        h.Press(new KeyChord(Key.F4, KeyModifiers.Alt));

        Assert.Equal(["La revedere."], h.Press(Key.Enter));
        Assert.True(h.System.ExitRequested);
    }

    [Fact]
    public void Alt_F4_while_confirming_repeats_the_question_instead_of_stacking()
    {
        var h = new ShellHarness().Start(TestScreens.Fruits());
        h.Press(new KeyChord(Key.F4, KeyModifiers.Alt));
        h.Press(new KeyChord(Key.F4, KeyModifiers.Alt));

        Assert.Equal(2, h.Shell.Stack.Count);
    }
}
