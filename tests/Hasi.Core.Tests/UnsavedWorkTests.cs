using Hasi.Core.Diagnostics;
using Hasi.Core.Input;
using Hasi.Core.Power;
using Hasi.Core.Screens;
using Hasi.Core.Updates;
using Hasi.Testing;

namespace Hasi.Core.Tests;

/// <summary>Shell.GuardUnsaved: before closing, restarting, shutting down or updating, unsaved work is asked about.</summary>
public class UnsavedWorkTests
{
    private static readonly KeyChord AltF4 = new(Key.F4, KeyModifiers.Alt);

    private const string Question =
        "Documentul Scrisoare are modificări nesalvate. Ce doriți să faceți? 3 opțiuni. Selectat: Salvează și continuă, 1 din 3.";

    private const string ExitQuestion = "Sigur doriți să închideți HASI? Apăsați Enter pentru da sau Escape pentru nu.";

    /// <summary>The main menu, with the document open on top of it.</summary>
    private static (ShellHarness Harness, FakeUnsavedWorkScreen Document) Editing(bool unsaved = true)
    {
        var document = new FakeUnsavedWorkScreen { HasUnsavedChanges = unsaved };
        var h = new ShellHarness().Start(new MenuScreen("Meniu", [new MenuItem("Document", _ => ScreenResult.Push(document))]));
        h.Press(Key.Enter);
        return (h, document);
    }

    [Fact]
    public void Without_unsaved_work_alt_f4_asks_only_the_exit_question()
    {
        var (h, document) = Editing(unsaved: false);

        Assert.Equal([ExitQuestion], h.Press(AltF4));
        Assert.IsType<ConfirmScreen>(h.Shell.Current);
        Assert.Equal(0, document.Saves);
    }

    [Fact]
    public void Unsaved_work_is_asked_about_first_with_the_menu_entry_after_the_description()
    {
        var (h, _) = Editing();

        Assert.Equal([Question], h.Press(AltF4));
        Assert.Equal("Modificări nesalvate", h.Shell.Current.Title);
        Assert.Equal(["Continuă fără să salvezi, 2 din 3.", "Renunță, 3 din 3."], h.Press(Key.Down, Key.Down));
    }

    [Fact]
    public void Save_and_continue_saves_then_asks_the_exit_question()
    {
        var (h, document) = Editing();
        h.Press(AltF4);

        Assert.Equal([ExitQuestion], h.Press(Key.Enter));
        Assert.Equal(1, document.Saves);
        Assert.Equal(3, h.Shell.Stack.Count); // Menu, document, confirmation: the question is gone.
        Assert.Equal(["La revedere."], h.Press(Key.Enter));
        Assert.True(h.System.ExitRequested);
    }

    [Fact]
    public void Escape_on_the_exit_question_after_saving_goes_back_to_the_saved_document()
    {
        var (h, _) = Editing();
        h.Press(AltF4, Key.Enter);

        Assert.Equal(["Anulat. Scrisoare. Document."], h.Press(Key.Escape));
        Assert.False(h.System.ExitRequested);
    }

    [Fact]
    public void A_save_that_asks_for_a_name_closes_the_name_screen_and_the_question()
    {
        var (h, document) = Editing();
        document.AsksForName = true;
        h.Press(AltF4);

        Assert.Equal(["Nume. Scrieți numele documentului."], h.Press(Key.Enter));
        Assert.Equal([ExitQuestion], h.Press(Key.Enter)); // Empty field: the proposed name.
        Assert.Equal("Scrisoare", document.SavedName);
        Assert.Equal(3, h.Shell.Stack.Count);
    }

    [Fact]
    public void A_failed_save_says_why_and_keeps_hasi_open_on_the_question()
    {
        var (h, document) = Editing();
        document.SaveFails = true;
        h.Press(AltF4);

        Assert.Equal(["Documentul nu a putut fi salvat."], h.Press(Key.Enter));
        Assert.IsType<UnsavedWorkScreen>(h.Shell.Current);
        Assert.False(h.System.ExitRequested);
        Assert.True(document.HasUnsavedChanges);
    }

    [Fact]
    public void Continue_without_saving_is_confirmed_then_discards_and_asks_the_exit_question()
    {
        var (h, document) = Editing();
        h.Press(AltF4, Key.Down);

        Assert.Equal(["Modificările nesalvate se vor pierde. Apăsați Enter pentru da sau Escape pentru nu."], h.Press(Key.Enter));
        Assert.Equal(0, document.Discards);
        Assert.Equal([ExitQuestion], h.Press(Key.Enter));
        Assert.Equal(1, document.Discards);
        Assert.Equal(0, document.Saves);
    }

    [Fact]
    public void Escape_on_the_discard_confirmation_returns_to_the_question()
    {
        var (h, document) = Editing();
        h.Press(AltF4, Key.Down, Key.Enter);

        Assert.Equal(["Anulat. Modificări nesalvate. Selectat: Continuă fără să salvezi, 2 din 3."], h.Press(Key.Escape));
        Assert.Equal(0, document.Discards);
    }

    [Theory]
    [InlineData(Key.Escape, 0)]
    [InlineData(Key.Enter, 2)]
    public void Cancel_or_escape_stays_in_the_document(Key key, int downs)
    {
        var (h, document) = Editing();
        h.Press(AltF4);
        for (var i = 0; i < downs; i++) h.Press(Key.Down);

        Assert.Equal(["Anulat. Scrisoare. Document."], h.Press(key));
        Assert.Same(document, h.Shell.Current);
        Assert.False(h.System.ExitRequested);
        Assert.True(document.HasUnsavedChanges);
    }

    [Fact]
    public void Closing_the_window_while_asked_says_the_question_again()
    {
        var (h, _) = Editing();
        h.Press(AltF4, Key.Down);
        var question = "Documentul Scrisoare are modificări nesalvate. Ce doriți să faceți? 3 opțiuni. Selectat: Continuă fără să salvezi, 2 din 3.";

        Assert.Equal([question], h.Press(AltF4));
        h.Shell.RequestExitConfirmation();
        Assert.Equal(question, h.LastSaid);
        Assert.Equal(3, h.Shell.Stack.Count);
    }

    [Fact]
    public void Closing_the_window_from_outside_asks_about_unsaved_work_too()
    {
        var (h, _) = Editing();

        h.Shell.RequestExitConfirmation();

        Assert.Equal(Question, h.LastSaid);
    }

    [Fact]
    public void Every_open_unsaved_work_is_asked_about_top_first_then_the_action()
    {
        var (h, below) = Editing();
        var above = new FakeUnsavedWorkScreen("Rețetă") { HasUnsavedChanges = true };
        h.Shell.Show(ScreenResult.Push(above));
        var clean = new FakeUnsavedWorkScreen("Listă");
        h.Shell.Show(ScreenResult.Push(clean));

        Assert.Equal(["Documentul Rețetă are modificări nesalvate. Ce doriți să faceți? 3 opțiuni. Selectat: Salvează și continuă, 1 din 3."], h.Press(AltF4));
        Assert.Equal([Question], h.Press(Key.Enter));
        Assert.Equal([ExitQuestion], h.Press(Key.Enter));
        Assert.Equal((1, 1, 0), (above.Saves, below.Saves, clean.Saves));
    }

    [Fact]
    public void Work_settled_once_is_not_asked_about_again_even_if_it_stays_unsaved()
    {
        var (h, document) = Editing();
        var stubborn = new StubbornWork();
        h.Shell.Show(ScreenResult.Push(stubborn));
        h.Press(AltF4, Key.Down, Key.Enter);

        Assert.Equal([Question], h.Press(Key.Enter));
        Assert.Equal(1, stubborn.Discards);
        Assert.Equal([ExitQuestion], h.Press(Key.Enter));
        Assert.Equal(1, document.Saves);
    }

    [Theory]
    [InlineData(PowerAction.Restart, "Sigur doriți să reporniți calculatorul? Apăsați Enter pentru da sau Escape pentru nu.")]
    [InlineData(PowerAction.Shutdown, "Sigur doriți să opriți calculatorul? Apăsați Enter pentru da sau Escape pentru nu.")]
    public void Restart_and_shutdown_are_guarded(PowerAction action, string confirmation)
    {
        var (h, document) = Editing();

        h.Shell.Show(action == PowerAction.Restart ? h.Shell.ConfirmRestart() : h.Shell.ConfirmShutdown());
        Assert.Equal(Question, h.LastSaid);
        Assert.Equal([confirmation], h.Press(Key.Enter));
        Assert.Equal(1, document.Saves);
        h.Press(Key.Enter);

        Assert.Equal(action, h.System.PowerRequested);
    }

    [Fact]
    public void An_update_is_guarded_when_accepted_and_again_when_ready()
    {
        var (h, document) = Editing();
        var updates = new FakeUpdateService();
        var flow = new UpdateFlow(updates, h.Settings, h.Shell, h.Localizer, NullDiagnosticLog.Instance);
        flow.CheckNow();
        updates.FinishCheck(UpdateCheckStatus.Available, "0.2.0");

        Assert.Equal([Question], h.Press(Key.Enter));
        Assert.Equal(0, updates.Downloads);
        Assert.Equal(["Se descarcă actualizarea."], h.Press(Key.Enter));
        Assert.Equal(1, updates.Downloads);
        Assert.Same(document, h.Shell.Current);

        // Typed during the download: asked again before HASI closes.
        h.Type("x");
        updates.FinishDownload(ready: true);
        Assert.Equal(Question, h.LastSaid);
        Assert.False(updates.Applied);
        Assert.Equal(["Actualizarea este gata. HASI pornește din nou."], h.Press(Key.Enter));
        Assert.True(updates.Applied);
        Assert.Equal(2, document.Saves);
    }

    [Fact]
    public void A_ready_update_without_unsaved_work_is_applied_at_once()
    {
        var (h, _) = Editing(unsaved: false);
        var updates = new FakeUpdateService();
        var flow = new UpdateFlow(updates, h.Settings, h.Shell, h.Localizer, NullDiagnosticLog.Instance);
        flow.CheckNow();
        updates.FinishCheck(UpdateCheckStatus.Available, "0.2.0");
        h.Press(Key.Enter);

        updates.FinishDownload(ready: true);

        Assert.Equal("Actualizarea este gata. HASI pornește din nou.", h.LastSaid);
        Assert.True(updates.Applied);
    }

    [Fact]
    public void When_the_session_ends_every_unsaved_work_keeps_a_draft_without_asking()
    {
        var (h, document) = Editing();
        var failing = new FakeUnsavedWorkScreen("Rețetă") { HasUnsavedChanges = true, DraftFailure = new IOException("disc plin") };
        h.Shell.Show(ScreenResult.Push(failing));
        var clean = new FakeUnsavedWorkScreen("Listă");
        h.Shell.Show(ScreenResult.Push(clean));
        var said = h.Output.Announcements.Count;

        h.Shell.PowerSessionEnding();

        Assert.Equal(1, document.Drafts);
        Assert.Equal(0, clean.Drafts);
        Assert.Equal(said, h.Output.Announcements.Count);
        Assert.Same(clean, h.Shell.Current);
    }

    /// <summary>A module bug: discarding does not clear the changes. HASI must not ask forever.</summary>
    private sealed class StubbornWork : IScreen, IUnsavedWork
    {
        public int Discards { get; private set; }

        public string Title => "Încăpățânat";

        public bool HasUnsavedChanges => true;

        public Output.Utterance Enter(EnterReason reason, IScreenContext context) => Title;

        public ScreenResult Handle(InputEvent input, IScreenContext context) => ScreenResult.Unhandled;

        public Output.Utterance Help(IScreenContext context) => Title;

        public string UnsavedDescription(IScreenContext context) => "Încăpățânat are modificări nesalvate.";

        public ScreenResult Save(IScreenContext context, Func<ScreenResult> then) => ScreenResult.Say("Nu.");

        public void Discard() => Discards++;

        public void SaveDraftNow() { }
    }
}
