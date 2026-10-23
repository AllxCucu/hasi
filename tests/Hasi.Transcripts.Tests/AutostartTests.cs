using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Modules.Settings;
using Hasi.Testing;

namespace Hasi.Transcripts.Tests;

public class AutostartTests
{
    /// <summary>Setări → Pornire automată (item 9).</summary>
    private static ShellHarness OnAutostart(Action<FakeAutostartService>? setup = null)
    {
        var h = new ShellHarness().StartWithModules(new SettingsModule());
        setup?.Invoke(h.Autostart);
        h.Press(Key.Enter, Key.End, Key.Up, Key.Up);
        return h;
    }

    [Fact]
    public void Autostart_is_off_by_default_and_turned_on_with_da()
    {
        var h = OnAutostart();

        Assert.Equal(["Pornire automată. 2 opțiuni. Selectat: Nu, actual, 2 din 2."], h.Press(Key.Enter));
        h.Press(Key.Up);
        Assert.Equal(
            ["Pornire automată: Da: HASI pornește singur după ce intrați în Windows. Setări. Selectat: Pornire automată, 9 din 11."],
            h.Press(Key.Enter));
        Assert.True(h.Autostart.IsEnabled);
    }

    [Fact]
    public void The_current_choice_is_read_from_windows()
    {
        var h = OnAutostart(a => a.SetEnabled(true));

        Assert.Equal(["Pornire automată. 2 opțiuni. Selectat: Da: HASI pornește singur după ce intrați în Windows, actual, 1 din 2."], h.Press(Key.Enter));
    }

    [Fact]
    public void A_refused_change_is_said_and_nothing_changes()
    {
        var h = OnAutostart(a => a.Refuse = true);
        h.Press(Key.Enter, Key.Up);

        Assert.Equal(["Pornirea automată nu a putut fi schimbată."], h.Press(Key.Enter));
        Assert.Equal(Earcon.Error, h.LastAnnouncement!.Utterance.Earcon);
        Assert.False(h.Autostart.IsEnabled);
    }
}
