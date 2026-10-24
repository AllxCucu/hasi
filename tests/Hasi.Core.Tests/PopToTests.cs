using Hasi.Core.Screens;
using Hasi.Testing;

namespace Hasi.Core.Tests;

public class PopToTests
{
    [Fact]
    public void Closes_every_screen_above_the_target_and_announces_it_with_the_prefix()
    {
        var root = TestScreens.Fruits();
        var h = new ShellHarness().Start(root);
        h.Shell.Show(ScreenResult.Push(new MenuScreen("Unu", [new MenuItem("a")])));
        h.Shell.Show(ScreenResult.Push(new MenuScreen("Doi", [new MenuItem("b")])));

        h.Shell.Show(ScreenResult.PopTo(root, prefix: "Gata."));

        Assert.Same(root, h.Shell.Current);
        Assert.Single(h.Shell.Stack);
        Assert.Equal("Gata. Fructe. Selectat: Măr, 1 din 3.", h.LastSaid);
    }

    [Fact]
    public void Applies_then_instead_of_the_announcement()
    {
        var root = TestScreens.Fruits();
        var h = new ShellHarness().Start(root);
        h.Shell.Show(ScreenResult.Push(new MenuScreen("Unu", [new MenuItem("a")])));

        h.Shell.Show(ScreenResult.PopTo(root, then: ScreenResult.Push(new MenuScreen("Trei", [new MenuItem("c")]))));

        Assert.Equal(["Fructe", "Trei"], h.Shell.Stack.Select(s => s.Title));
        Assert.Equal("Trei. 1 opțiune. Selectat: c, 1 din 1.", h.LastSaid);
    }

    [Fact]
    public void A_screen_no_longer_open_closes_nothing()
    {
        var h = new ShellHarness().Start(TestScreens.Fruits());
        h.Shell.Show(ScreenResult.Push(new MenuScreen("Unu", [new MenuItem("a")])));

        h.Shell.Show(ScreenResult.PopTo(new MenuScreen("Altul", [])));

        Assert.Equal(2, h.Shell.Stack.Count);
    }
}
