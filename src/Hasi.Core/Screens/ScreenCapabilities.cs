using Hasi.Core.Input;

namespace Hasi.Core.Screens;

/// <summary>A screen whose keys mean something else than in menus (a text editor): see <see cref="KeyProfile"/>.</summary>
public interface IHasKeyProfile
{
    KeyProfile KeyProfile { get; }
}

/// <summary>
/// A screen that shows private content (a document, a contact). While one is anywhere in the stack, the
/// diagnostic log masks typed characters, spoken text and screen titles.
/// </summary>
public interface ISensitiveScreen;

/// <summary>A screen that wants the shell's periodic tick (about every 5 seconds), for example to save a draft.</summary>
public interface ITickingScreen
{
    void Tick(DateTimeOffset now);
}

/// <summary>
/// A screen holding work that is not saved. Before HASI closes, restarts or shuts down the computer, or installs an
/// update, the shell asks the user what to do with it (<see cref="Shell.GuardUnsaved"/>).
/// </summary>
public interface IUnsavedWork
{
    bool HasUnsavedChanges { get; }

    /// <summary>What is said first, e.g. "Documentul Scrisoare are modificări nesalvate."</summary>
    string UnsavedDescription(IScreenContext context);

    /// <summary>
    /// Saves, asking whatever it needs (a name for a new document), then returns <paramref name="then"/>; on failure
    /// it says why and does not continue.
    /// </summary>
    ScreenResult Save(IScreenContext context, Func<ScreenResult> then);

    /// <summary>The user chose to lose the changes.</summary>
    void Discard();

    /// <summary>Last chance, without asking anything (Windows is ending the session): keep a draft if possible.</summary>
    void SaveDraftNow();
}
