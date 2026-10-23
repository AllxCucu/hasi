using Hasi.Core.Input;
using Hasi.Core.Power;
using Hasi.Core.Settings;
using Hasi.Modules.About;
using Hasi.Modules.Exit;
using Hasi.Modules.KeyTutor;
using Hasi.Modules.Settings;
using Hasi.Testing;

namespace Hasi.Transcripts.Tests;

/// <summary>Restart and shutdown: open programs named first, test mode, and what HASI says when Windows does not follow.</summary>
public class PowerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 23, 10, 0, 0, TimeSpan.Zero);

    private static ShellHarness Started(bool simulate = false, params OpenProgram[] programs)
    {
        var h = new ShellHarness().StartWithModules(new SettingsModule(), new KeyTutorModule(), new AboutModule(), new ExitModule());
        h.Settings.SetSimulatePower(simulate);
        h.System.Programs.AddRange(programs);
        return h;
    }

    /// <summary>Ieșire → Repornește (item 1) or Oprește (item 2), up to the question.</summary>
    private static string Ask(ShellHarness h, PowerAction action)
    {
        h.Press(Key.End, Key.Enter);
        h.Press(Key.Down);
        if (action == PowerAction.Shutdown) h.Press(Key.Down);
        return h.Press(Key.Enter)[0];
    }

    private static void Confirmed(ShellHarness h, PowerAction action)
    {
        Ask(h, action);
        h.Press(Key.Enter);
        h.Shell.PowerStarted(action, T0);
    }

    [Theory]
    [InlineData(PowerAction.Restart, "Sigur doriți să reporniți calculatorul? Apăsați Enter pentru da sau Escape pentru nu.")]
    [InlineData(PowerAction.Shutdown, "Sigur doriți să opriți calculatorul? Apăsați Enter pentru da sau Escape pentru nu.")]
    public void Without_other_programs_the_question_is_short(PowerAction action, string question)
    {
        Assert.Equal(question, Ask(Started(), action));
    }

    [Fact]
    public void Open_programs_are_named_before_confirming()
    {
        var h = Started(false, new OpenProgram("Notepad"), new OpenProgram("Microsoft Edge", 2));

        Assert.Equal(
            "Sigur doriți să reporniți calculatorul? Sunt deschise 2 programe: Notepad, Microsoft Edge, 2 ferestre. "
            + "Dacă aveți modificări nesalvate, se pot pierde. Apăsați Enter pentru da sau Escape pentru nu.",
            Ask(h, PowerAction.Restart));
    }

    [Fact]
    public void One_open_program_is_said_in_the_singular()
    {
        var h = Started(false, new OpenProgram("Notepad"));

        Assert.Contains("Este deschis un program: Notepad.", Ask(h, PowerAction.Shutdown), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PowerAction.Restart, "Sigur doriți să reporniți calculatorul? Mod de test: calculatorul nu va reporni.")]
    [InlineData(PowerAction.Shutdown, "Sigur doriți să opriți calculatorul? Mod de test: calculatorul nu se va opri.")]
    public void Test_mode_says_so_in_the_question_and_in_the_goodbye(PowerAction action, string question)
    {
        var h = Started(simulate: true);

        Assert.StartsWith(question, Ask(h, action), StringComparison.Ordinal);
        Assert.Equal(["Mod de test: HASI se închide, iar calculatorul rămâne pornit. La revedere."], h.Press(Key.Enter));
        Assert.Equal(action, h.System.PowerRequested);
        Assert.False(h.System.ForceRequested);
    }

    [Fact]
    public void Test_mode_is_off_by_default_since_the_installer()
    {
        var h = new ShellHarness().StartWithModules(new ExitModule());
        h.Press(Key.Enter, Key.Down);

        Assert.DoesNotContain("Mod de test", h.Press(Key.Enter)[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PowerAction.Restart, "Calculatorul se repornește. Așteptați, vă rog.")]
    [InlineData(PowerAction.Shutdown, "Calculatorul se oprește. Așteptați, vă rog.")]
    public void A_real_restart_or_shutdown_asks_the_host_without_forcing(PowerAction action, string goodbye)
    {
        var h = Started();
        Ask(h, action);

        Assert.Equal([goodbye], h.Press(Key.Enter));
        Assert.Equal(action, h.System.PowerRequested);
        Assert.False(h.System.ForceRequested);
        Assert.False(h.System.ExitRequested);
    }

    [Fact]
    public void When_windows_refuses_at_once_hasi_says_so()
    {
        var h = Started();
        Ask(h, PowerAction.Restart);
        h.Press(Key.Enter);

        h.Shell.PowerFailed(PowerAction.Restart);

        Assert.Equal("Calculatorul nu a putut fi repornit. HASI rămâne deschis.", h.LastSaid);
        Assert.False(h.Shell.PowerPending);
    }

    [Fact]
    public void Nothing_is_said_while_the_restart_proceeds_normally()
    {
        var h = Started();
        Confirmed(h, PowerAction.Restart);
        var said = h.Output.Announcements.Count;

        h.Shell.PowerSessionEnding();
        h.Shell.PowerTick(T0.AddSeconds(9));

        Assert.Equal(said, h.Output.Announcements.Count);
        Assert.True(h.Shell.PowerPending);
    }

    [Theory]
    [InlineData(PowerAction.Restart, "Calculatorul nu a început repornirea. HASI rămâne deschis. Puteți încerca din nou din meniul Ieșire.")]
    [InlineData(PowerAction.Shutdown, "Calculatorul nu a început oprirea. HASI rămâne deschis. Puteți încerca din nou din meniul Ieșire.")]
    public void A_restart_that_never_starts_is_reported_after_ten_seconds(PowerAction action, string message)
    {
        var h = Started();
        Confirmed(h, action);

        h.Shell.PowerTick(T0.AddSeconds(10));

        Assert.Equal(message, h.LastSaid);
        Assert.False(h.Shell.PowerPending);
    }

    [Fact]
    public void A_blocked_restart_is_explained_with_the_open_programs_and_repeated()
    {
        var h = Started(false, new OpenProgram("Notepad"));
        Confirmed(h, PowerAction.Restart);
        h.Shell.PowerSessionEnding();

        h.Shell.PowerTick(T0.AddSeconds(10));

        const string waiting = "Repornirea așteaptă. Probabil un program are modificări nesalvate și împiedică repornirea. "
            + "Pe ecran, Windows vă întreabă dacă reporniți oricum sau anulați. Este deschis un program: Notepad.";
        Assert.Equal(waiting, h.LastSaid);
        var said = h.Output.Announcements.Count;
        h.Shell.PowerTick(T0.AddSeconds(30));
        Assert.Equal(said, h.Output.Announcements.Count);
        h.Shell.PowerTick(T0.AddSeconds(40));
        Assert.Equal(waiting, h.LastSaid);
        Assert.Equal(said + 1, h.Output.Announcements.Count);
    }

    [Fact]
    public void A_cancelled_restart_is_announced_with_how_to_come_back()
    {
        var h = Started(false, new OpenProgram("Notepad"));
        Confirmed(h, PowerAction.Restart);
        h.Shell.PowerSessionEnding();
        var said = h.Output.Announcements.Count;

        // Windows' blocking screen takes the keyboard: no interruption while the restart may still go on.
        h.Shell.FocusLost();
        Assert.Equal(said, h.Output.Announcements.Count);

        h.Shell.PowerCancelled();

        Assert.Equal(
            "Repornirea a fost anulată. Rămâne deschis un program: Notepad. "
            + "HASI nu mai este în prim-plan, iar tastele merg către altă fereastră. Pentru a reveni, țineți apăsată tasta Alt și apăsați Tab.",
            h.LastSaid);
        Assert.False(h.Shell.PowerPending);
    }

    [Fact]
    public void A_cancellation_nobody_asked_hasi_for_is_ignored()
    {
        var h = Started();
        var said = h.Output.Announcements.Count;

        h.Shell.PowerSessionEnding();
        h.Shell.PowerCancelled();

        Assert.Equal(said, h.Output.Announcements.Count);
    }

    [Fact]
    public void Asking_again_while_waiting_does_not_start_a_second_restart()
    {
        var h = Started();
        Confirmed(h, PowerAction.Restart);
        h.Press(Key.Escape, Key.Escape, Key.End, Key.Enter, Key.End);

        Assert.Equal(["Calculatorul se repornește deja. Așteptați, vă rog."], h.Press(Key.Enter));
    }

    [Fact]
    public void After_a_blocked_attempt_forcing_needs_a_second_explicit_confirmation()
    {
        var h = Started(false, new OpenProgram("Notepad"));
        Confirmed(h, PowerAction.Restart);
        h.Shell.PowerSessionEnding();
        h.Shell.PowerCancelled();
        h.Press(Key.Escape, Key.Escape);

        Ask(h, PowerAction.Restart);
        Assert.Equal(
            ["Repornirea de mai devreme a fost împiedicată de un program. Doriți o repornire forțată? "
                + "Programele deschise se închid fără să salveze, iar modificările nesalvate se pierd. Apăsați Enter pentru da sau Escape pentru nu."],
            h.Press(Key.Enter));

        Assert.Equal(["Calculatorul se repornește. Așteptați, vă rog."], h.Press(Key.Enter));
        Assert.True(h.System.ForceRequested);
    }

    [Fact]
    public void Escape_at_the_force_question_cancels_everything()
    {
        var h = Started(false, new OpenProgram("Notepad"));
        Confirmed(h, PowerAction.Shutdown);
        h.Shell.PowerSessionEnding();
        h.Shell.PowerCancelled();
        h.Press(Key.Escape, Key.Escape);
        Ask(h, PowerAction.Shutdown);
        h.Press(Key.Enter);
        var before = h.System.PowerRequested;

        Assert.StartsWith("Anulat.", h.Press(Key.Escape)[0], StringComparison.Ordinal);
        Assert.Equal(before, h.System.PowerRequested);
        Assert.False(h.System.ForceRequested);
    }

    [Fact]
    public void Without_open_programs_forcing_is_never_offered()
    {
        var h = Started(false, new OpenProgram("Notepad"));
        Confirmed(h, PowerAction.Restart);
        h.Shell.PowerSessionEnding();
        h.Shell.PowerCancelled();
        h.System.Programs.Clear();
        h.Press(Key.Escape, Key.Escape);
        Ask(h, PowerAction.Restart);

        Assert.Equal(["Calculatorul se repornește. Așteptați, vă rog."], h.Press(Key.Enter));
        Assert.False(h.System.ForceRequested);
    }

    [Fact]
    public void Turning_test_mode_off_warns_once_at_the_next_start()
    {
        var settings = new InMemorySettingsService();
        settings.SetSimulatePower(true);
        var h = new ShellHarness(settings: settings).StartWithModules(new SettingsModule());
        // Setări → Diagnostic → Simulare oprire → Oprită
        h.Press(Key.Enter, Key.End, Key.Up, Key.Enter, Key.End, Key.Enter);
        Assert.Equal("Simulare oprire. 2 opțiuni. Selectat: Pornită: repornirea și oprirea sunt doar un test, actual, 1 din 2.", h.LastSaid);
        h.Press(Key.Down);

        Assert.Equal(["Simulare oprire: Oprită: repornirea și oprirea sunt reale. Diagnostic. Selectat: Simulare oprire, 5 din 5."], h.Press(Key.Enter));
        Assert.False(settings.Current.SimulatePower);
        Assert.True(settings.TakeRealPowerNotice());
        Assert.False(settings.TakeRealPowerNotice());
    }

    [Fact]
    public void Turning_test_mode_back_on_cancels_the_warning()
    {
        var settings = new InMemorySettingsService();
        settings.SetSimulatePower(false);
        settings.SetSimulatePower(true);

        Assert.False(settings.TakeRealPowerNotice());
        Assert.True(settings.Current.SimulatePower);
    }
}
