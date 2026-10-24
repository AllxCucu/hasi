using Hasi.Core.Input;
using Hasi.Core.Modules;
using Hasi.Core.Screens;
using Hasi.Modules.Exit;
using Hasi.Testing;

namespace Hasi.Transcripts.Tests;

/// <summary>Module 2: closing HASI with a document that has unsaved changes, with the real texts.</summary>
public class UnsavedWorkExitTests
{
    private static readonly KeyChord AltF4 = new(Key.F4, KeyModifiers.Alt);

    private sealed class DocumentModule(FakeUnsavedWorkScreen document) : IModule
    {
        public string Id => "document";

        public string TitleKey => "about.title";

        public IScreen CreateRootScreen(IModuleContext context) => document;
    }

    private static (ShellHarness Harness, FakeUnsavedWorkScreen Document) Typing()
    {
        var document = new FakeUnsavedWorkScreen();
        var h = new ShellHarness().StartWithModules(new DocumentModule(document), new ExitModule());
        h.Press(Key.Enter);
        h.Type("a");
        return (h, document);
    }

    [Fact]
    public void Save_then_close()
    {
        var (h, document) = Typing();

        Assert.Equal(
            [
                "Documentul Scrisoare are modificări nesalvate. Ce doriți să faceți? 3 opțiuni. Selectat: Salvează și continuă, 1 din 3.",
                "Sigur doriți să închideți HASI? Apăsați Enter pentru da sau Escape pentru nu.",
                "La revedere.",
            ],
            h.Press(AltF4, Key.Enter, Key.Enter));
        Assert.Equal(1, document.Saves);
        Assert.True(h.System.ExitRequested);
    }

    [Fact]
    public void Close_without_saving()
    {
        var (h, document) = Typing();

        Assert.Equal(
            [
                "Documentul Scrisoare are modificări nesalvate. Ce doriți să faceți? 3 opțiuni. Selectat: Salvează și continuă, 1 din 3.",
                "Continuă fără să salvezi, 2 din 3.",
                "Modificările nesalvate se vor pierde. Apăsați Enter pentru da sau Escape pentru nu.",
                "Sigur doriți să închideți HASI? Apăsați Enter pentru da sau Escape pentru nu.",
                "La revedere.",
            ],
            h.Press(AltF4, Key.Down, Key.Enter, Key.Enter, Key.Enter));
        Assert.Equal((0, 1), (document.Saves, document.Discards));
        Assert.True(h.System.ExitRequested);
    }

    [Fact]
    public void Stay_in_the_document()
    {
        var (h, document) = Typing();

        Assert.Equal(
            [
                "Documentul Scrisoare are modificări nesalvate. Ce doriți să faceți? 3 opțiuni. Selectat: Salvează și continuă, 1 din 3.",
                "Renunță, 3 din 3.",
                "Anulat. Scrisoare. Document.",
            ],
            h.Press(AltF4, Key.End, Key.Enter));
        Assert.Same(document, h.Shell.Current);
        Assert.True(document.HasUnsavedChanges);
        Assert.False(h.System.ExitRequested);
    }

    [Fact]
    public void Help_and_where_am_i_on_the_question()
    {
        var (h, _) = Typing();
        h.Press(AltF4);

        Assert.Equal(
            [
                "Ajutor pentru Modificări nesalvate. Folosiți săgețile sus și jos pentru a alege o opțiune. Apăsați Enter pentru a o deschide și Escape pentru a vă întoarce. F2 sau Control R repetă ultimul mesaj. F3 sau Control U vă spune unde vă aflați. Tasta Control oprește vorbirea. Salvează și continuă păstrează modificările, apoi HASI face ce ați cerut. Continuă fără să salvezi pierde modificările, după o confirmare. Renunță, sau tasta Escape, vă întoarce unde erați, fără să se piardă nimic.",
                "Sunteți în: Meniu principal, Scrisoare, Modificări nesalvate.",
            ],
            h.Press(Key.F1, Key.F3));
    }

    [Fact]
    public void A_failed_save_keeps_hasi_open()
    {
        var (h, document) = Typing();
        document.SaveFails = true;

        Assert.Equal(["Documentul nu a putut fi salvat."], h.Press(AltF4, Key.Enter).Skip(1));
        Assert.False(h.System.ExitRequested);
        Assert.Equal(["Anulat. Scrisoare. Document."], h.Press(Key.Escape));
    }
}
