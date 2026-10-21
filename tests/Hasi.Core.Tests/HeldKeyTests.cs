using Hasi.Core.Input;
using Hasi.Core.Screens;
using Hasi.Testing;

namespace Hasi.Core.Tests;

public class HeldKeyTests
{
    [Fact]
    public void Holding_down_keeps_moving()
    {
        var h = new ShellHarness().Start(TestScreens.Fruits());

        Assert.Equal(["Pară, 2 din 3.", "Prună, 3 din 3.", "Sfârșitul listei. Prună, 3 din 3."], h.Hold(Key.Down, 2));
    }

    [Fact]
    public void Holding_enter_opens_only_once()
    {
        var h = new ShellHarness().Start(TestScreens.Fruits());

        Assert.Equal(["Măr. Un măr."], h.Hold(Key.Enter, 5));
        Assert.Equal(2, h.Shell.Stack.Count);
    }

    [Fact]
    public void Holding_enter_never_confirms_a_question_by_itself()
    {
        var confirmed = false;
        var h = new ShellHarness().Start(TestScreens.Fruits(_ => ScreenResult.Confirm("Ștergeți para?", () =>
        {
            confirmed = true;
            return ScreenResult.Say("Para a fost ștearsă.");
        })));
        h.Press(Key.Down);

        h.Hold(Key.Enter, 10);

        Assert.False(confirmed);
        Assert.IsType<ConfirmScreen>(h.Shell.Current);
    }

    [Fact]
    public void Holding_escape_goes_back_only_one_screen()
    {
        var h = new ShellHarness().Start(TestScreens.Fruits());
        h.Press(Key.Enter);

        Assert.Equal(["Fructe. Selectat: Măr, 1 din 3."], h.Hold(Key.Escape, 5));
    }
}
