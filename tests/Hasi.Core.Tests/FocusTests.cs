using Hasi.Core.Input;
using Hasi.Testing;

namespace Hasi.Core.Tests;

public class FocusTests
{
    [Fact]
    public void Losing_focus_is_announced_with_how_to_come_back()
    {
        var h = new ShellHarness().Start(TestScreens.Fruits());

        h.Shell.FocusLost();

        Assert.Equal(
            "HASI nu mai este în prim-plan, iar tastele merg către altă fereastră. Pentru a reveni, țineți apăsată tasta Alt și apăsați Tab.",
            h.LastSaid);
    }

    [Fact]
    public void Regaining_focus_says_where_the_user_is()
    {
        var h = new ShellHarness().Start(TestScreens.Fruits());
        h.Press(Key.Down);

        h.Shell.FocusLost();
        h.Shell.FocusRegained();

        Assert.Equal("Fructe. Selectat: Pară, 2 din 3.", h.LastSaid);
    }
}
