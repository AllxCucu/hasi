namespace Hasi.Documents.Tests;

/// <summary>A fresh folder under the system temp folder, deleted after each test.</summary>
public abstract class TempFolder : IDisposable
{
    protected string Folder { get; } = Path.Combine(Path.GetTempPath(), "hasi-documents-" + Guid.NewGuid().ToString("N"));

    protected TempFolder() => Directory.CreateDirectory(Folder);

    protected string PathOf(string name) => Path.Combine(Folder, name);

    /// <summary>Files HASI left behind while saving (there should be none).</summary>
    protected string[] TemporaryFiles() => Directory.GetFiles(Folder, "~hasi-*.tmp");

    public void Dispose()
    {
        if (Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true);
        GC.SuppressFinalize(this);
    }
}
