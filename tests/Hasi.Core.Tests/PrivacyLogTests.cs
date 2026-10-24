using Hasi.Core.Diagnostics;
using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Testing;

namespace Hasi.Core.Tests;

/// <summary>While a private screen (<see cref="ISensitiveScreen"/>) is open, the diagnostic log shows none of its content.</summary>
public class PrivacyLogTests
{
    private const string Typed = "secret";

    /// <summary>Echoes what is typed; Enter closes it, saying the text.</summary>
    private class NoteScreen : IScreen
    {
        private string _text = "";

        public string Title => "Scrisoare către Ana";

        public Utterance Enter(EnterReason reason, IScreenContext context) => $"{Title}. Text: {_text}";

        public ScreenResult Handle(InputEvent input, IScreenContext context)
        {
            if (input.Character is { } c)
            {
                _text += c;
                return ScreenResult.Say($"Litera {c}");
            }
            return input.Command == Command.Activate
                ? ScreenResult.Pop(prefix: $"Am păstrat: {_text}.")
                : ScreenResult.Unhandled;
        }

        public Utterance Help(IScreenContext context) => $"Ajutor pentru {Title}: {_text}";
    }

    private sealed class PrivateNoteScreen : NoteScreen, ISensitiveScreen;

    /// <summary>Opens the note from a menu, types into it, asks for help, where-am-I and repeat, then closes it.</summary>
    private static (ShellHarness Harness, StringWriter Log) Run(NoteScreen note)
    {
        var lines = new StringWriter();
        var log = new TextDiagnosticLog(lines, TimeProvider.System);
        var h = new ShellHarness(diagnosticLog: log)
            .Start(new MenuScreen("Meniu", [new MenuItem("Notă", _ => ScreenResult.Push(note)), new MenuItem("Altceva")]));
        h.Press(Key.Enter);
        h.Shell.Interrupt(new KeyChord(Key.S)); // The key behind the first character, as the window reports it.
        h.Type(Typed);
        h.Press(Key.F1, Key.F3, Key.F2, Key.Enter);
        return (h, lines);
    }

    [Fact]
    public void Nothing_typed_or_said_on_a_private_screen_reaches_the_log()
    {
        var (h, lines) = Run(new PrivateNoteScreen());
        var log = lines.ToString();

        Assert.DoesNotContain(Typed, log, StringComparison.Ordinal);
        Assert.DoesNotContain("'s'", log, StringComparison.Ordinal);
        Assert.DoesNotContain("KEY    S", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Ana", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Litera", log, StringComparison.Ordinal);
        Assert.Contains("CHAR   '*'", log, StringComparison.Ordinal);
        Assert.Contains("SCREEN Meniu > ***", log, StringComparison.Ordinal);
        Assert.Contains($"text=\"{TextDiagnosticLog.Mask}\"", log, StringComparison.Ordinal);
        // Still said and shown: only the log is masked.
        Assert.Contains($"Am păstrat: {Typed}. Meniu. Selectat: Notă, 1 din 2.", h.LastSaid, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_utterance_from_opening_to_closing_a_private_screen_is_marked_sensitive()
    {
        var (h, _) = Run(new PrivateNoteScreen());

        var welcome = h.Output.Announcements[0];
        Assert.False(welcome.Utterance.Sensitive);
        Assert.All(h.Output.Announcements.Skip(1), a => Assert.True(a.Utterance.Sensitive, a.Utterance.Text));
    }

    [Fact]
    public void After_the_private_screen_closes_the_log_is_clear_again()
    {
        var (h, lines) = Run(new PrivateNoteScreen());

        h.Press(Key.Down);
        h.Type("x");

        Assert.False(h.LastAnnouncement!.Utterance.Sensitive);
        var log = lines.ToString();
        Assert.Contains("Altceva, 2 din 2.", log, StringComparison.Ordinal);
        Assert.Contains("CHAR   'x'", log, StringComparison.Ordinal);
        Assert.EndsWith("SCREEN Meniu", log.Split(Environment.NewLine).Last(l => l.Contains("SCREEN", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_flow_on_a_normal_screen_is_logged_in_full()
    {
        var (h, lines) = Run(new NoteScreen());
        var log = lines.ToString();

        Assert.Contains("CHAR   's'", log, StringComparison.Ordinal);
        Assert.Contains("KEY    S", log, StringComparison.Ordinal);
        Assert.Contains("SCREEN Meniu > Scrisoare către Ana", log, StringComparison.Ordinal);
        Assert.Contains("Litera t", log, StringComparison.Ordinal);
        Assert.Contains($"Am păstrat: {Typed}.", log, StringComparison.Ordinal);
        Assert.All(h.Output.Announcements, a => Assert.False(a.Utterance.Sensitive));
    }

    [Fact]
    public void Repeat_on_a_private_screen_masks_a_message_that_was_not_masked_the_first_time()
    {
        var lines = new StringWriter();
        var h = new ShellHarness(diagnosticLog: new TextDiagnosticLog(lines, TimeProvider.System))
            .Start(new MenuScreen("Meniu", [new MenuItem("Notă")]));
        h.Shell.Show(ScreenResult.Push(new PrivateNoteScreen()));
        h.Announcer.Say("Mesaj public"); // Not through the shell, so not masked.

        h.Press(Key.F2);

        Assert.Equal("Mesaj public", h.LastSaid);
        Assert.True(h.LastAnnouncement!.Utterance.Sensitive);
        // Only the first time: its text and its normalized form.
        Assert.Equal(2, lines.ToString().Split("Mesaj public").Length - 1);
    }
}
