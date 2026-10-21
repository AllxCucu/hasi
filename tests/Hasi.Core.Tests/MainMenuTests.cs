using Hasi.Core.Input;
using Hasi.Testing;

namespace Hasi.Core.Tests;

public class MainMenuTests
{
    [Fact]
    public void Main_menu_lists_modules_in_registration_order_and_opens_them()
    {
        var h = new ShellHarness().StartWithModules(
            new TestScreens.FakeModule("settings.title", _ => TestScreens.Fruits()),
            new TestScreens.FakeModule("about.title", _ => TestScreens.Fruits()));

        Assert.Equal("Bun venit în HASI. Meniu principal. 2 opțiuni. Selectat: Setări, 1 din 2.", h.LastSaid);
        Assert.Equal(["Despre HASI, 2 din 2.", "Fructe. 3 opțiuni. Selectat: Măr, 1 din 3."], h.Press(Key.Down, Key.Enter));
        Assert.Empty(h.Localizer.MissingKeys);
    }
}
