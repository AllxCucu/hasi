using Hasi.Core.Input;
using Hasi.Core.Output;
using Hasi.Core.Screens;

namespace Hasi.Testing;

/// <summary>
/// A stand-in for a document being edited: typing makes it unsaved; saving can succeed, fail, or first ask for a
/// name on a screen of its own, as a new document does.
/// </summary>
public sealed class FakeUnsavedWorkScreen(string title = "Scrisoare") : IScreen, IUnsavedWork
{
    public string Title { get; } = title;

    public bool HasUnsavedChanges { get; set; }

    /// <summary>Saving fails: says why and does not continue.</summary>
    public bool SaveFails { get; set; }

    /// <summary>Saving first asks for a name (a new document), proposing the title.</summary>
    public bool AsksForName { get; set; }

    public int Saves { get; private set; }

    public int Discards { get; private set; }

    public int Drafts { get; private set; }

    /// <summary>Thrown by <see cref="SaveDraftNow"/>, to test that one failure stops nothing.</summary>
    public Exception? DraftFailure { get; set; }

    public string? SavedName { get; private set; }

    public Utterance Enter(EnterReason reason, IScreenContext context) => $"{Title}. Document.";

    public ScreenResult Handle(InputEvent input, IScreenContext context)
    {
        if (input.Character is not { } c) return ScreenResult.Unhandled;
        HasUnsavedChanges = true;
        return ScreenResult.Say(c.ToString());
    }

    public Utterance Help(IScreenContext context) => "Ajutor pentru document.";

    public string UnsavedDescription(IScreenContext context) => $"Documentul {Title} are modificări nesalvate.";

    public ScreenResult Save(IScreenContext context, Func<ScreenResult> then)
    {
        if (SaveFails) return ScreenResult.Say(new Utterance("Documentul nu a putut fi salvat.", Earcon: Earcon.Error));
        if (!AsksForName) return Saved(Title, then);
        return ScreenResult.Push(new TextInputScreen("Nume", "Scrieți numele documentului.",
            (name, _) => ScreenResult.Pop(announce: false, then: Saved(name, then)), defaultValue: Title));
    }

    public void Discard()
    {
        Discards++;
        HasUnsavedChanges = false;
    }

    public void SaveDraftNow()
    {
        if (DraftFailure is not null) throw DraftFailure;
        Drafts++;
    }

    private ScreenResult Saved(string name, Func<ScreenResult> then)
    {
        Saves++;
        SavedName = name;
        HasUnsavedChanges = false;
        return then();
    }
}
