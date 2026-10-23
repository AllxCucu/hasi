using Hasi.Core.Input;
using Hasi.Core.Screens;
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
    public void With_the_return_key_registered_the_notice_names_control_alt_h()
    {
        var h = new ShellHarness(new ShellOptions { ReturnHotkey = true }).Start(TestScreens.Fruits());

        h.Shell.FocusLost();

        Assert.Equal(
            "HASI nu mai este în prim-plan, iar tastele merg către altă fereastră. Pentru a reveni, apăsați împreună Control, Alt și H.",
            h.LastSaid);
    }

    [Fact]
    public void When_windows_keeps_another_window_in_front_hasi_says_how_to_come_back_by_hand()
    {
        var h = new ShellHarness(new ShellOptions { ReturnHotkey = true }).Start(TestScreens.Fruits());

        h.Shell.CannotComeForward();

        Assert.Equal("Windows nu a lăsat HASI să vină în prim-plan. Țineți apăsată tasta Alt și apăsați Tab până ajungeți la HASI.", h.LastSaid);
    }

    [Fact]
    public void Coming_forward_while_already_in_front_says_where_the_user_is()
    {
        var h = new ShellHarness().Start(TestScreens.Fruits());
        h.Press(Key.Down);

        h.Shell.FocusRegained();

        Assert.Equal("Fructe. Selectat: Pară, 2 din 3.", h.LastSaid);
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
