using System.Text;

using Hasi.Core.Files;
using Hasi.Core.Input;
using Hasi.Documents;
using Hasi.Modules.Editor;
using Hasi.Testing;

namespace Hasi.Transcripts.Tests;

/// <summary>The editor end to end with the real .docx and .txt files, in a temporary Documents folder.</summary>
public sealed class EditorRealFilesTests : IDisposable
{
    private static readonly KeyChord CtrlS = new(Key.S, KeyModifiers.Ctrl);
    private static readonly KeyChord CtrlEnd = new(Key.End, KeyModifiers.Ctrl);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "hasi-editor-" + Guid.NewGuid().ToString("N"));

    public EditorRealFilesTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void A_new_document_is_saved_as_docx_reopened_from_recents_saved_over_itself_and_copied_as_text()
    {
        var h = new ShellHarness(documents: new DocumentFormats());
        h.Files.AddPlace(PlaceKind.Documents, _folder);
        h.StartWithModules(new EditorModule());
        h.Press(Key.Enter, Key.Enter);
        h.Type("Dragă Ana,");
        h.Press(Key.Enter);
        h.Type("Ce mai faci? Știu că țin minte.");

        Assert.Equal(["Documentul a fost salvat: Dragă Ana."], h.Press(CtrlS, Key.Enter).Skip(1));
        var path = Path.Combine(_folder, "Dragă Ana.docx");
        Assert.True(File.Exists(path));

        h.Press(Key.Escape);
        Assert.Equal(["Dragă Ana. Dragă Ana,"], h.Press(Key.End, Key.Enter, Key.Enter).Skip(2));
        var editor = Assert.IsType<EditorScreen>(h.Shell.Current);
        Assert.True(editor.MadeByHasi);
        Assert.Equal("Dragă Ana,\nCe mai faci? Știu că țin minte.", editor.Buffer.Text);

        h.Press(CtrlEnd);
        h.Type(" Pa!");
        Assert.Equal(["Documentul a fost salvat."], h.Press(CtrlS));
        var saved = new DocumentFormats().Load(path).Document!;
        Assert.Equal(["Dragă Ana,", "Ce mai faci? Știu că țin minte. Pa!"], saved.Paragraphs.Select(p => p.Text));
        Assert.True(saved.MadeByHasi);

        // A copy as plain text, next to it.
        h.Press(Key.F10, Key.Down, Key.Enter, Key.Down, Key.Enter, Key.Enter);
        Assert.Equal(["Copia a fost salvată: Dragă Ana."], h.Press(Key.Enter));
        Assert.Equal("Dragă Ana,\r\nCe mai faci? Știu că țin minte. Pa!",
            File.ReadAllText(Path.Combine(_folder, "Dragă Ana.txt"), Encoding.UTF8));
        Assert.Equal(path, editor.FilePath);
        Assert.Same(editor, h.Shell.Current);
    }
}
