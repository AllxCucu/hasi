using Hasi.Core.Files;
using Hasi.Core.Input;
using Hasi.Core.Localization;
using Hasi.Core.Output;

namespace Hasi.Core.Screens;

/// <summary>
/// Choosing a document, one level at a time: first the places (Documente, Desktop, Descărcări, USB sticks), then
/// folders and the files of the allowed types. Escape goes up one level. File names can be private, so every level
/// is sensitive.
/// </summary>
public sealed class FileBrowserScreen : IScreen, ISensitiveScreen
{
    /// <summary>The spoken type of each known extension; other allowed extensions are said with the full file name.</summary>
    private static readonly Dictionary<string, string> TypeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        [".docx"] = "files.type.docx",
        [".txt"] = "files.type.txt",
        [".pdf"] = "files.type.pdf",
        [".epub"] = "files.type.epub",
    };

    private readonly IFileSystem _files;
    private readonly HashSet<string> _extensions;
    private readonly Func<FileEntry, IScreenContext, ScreenResult> _onChoose;
    private readonly ListLevel _level;

    /// <param name="title">The first level's title, e.g. "Deschide un document".</param>
    /// <param name="extensions">The file types shown, e.g. [".txt", ".docx"].</param>
    /// <param name="onChoose">Runs on Enter on a file.</param>
    public FileBrowserScreen(string title, IFileSystem files, IEnumerable<string> extensions,
        Func<FileEntry, IScreenContext, ScreenResult> onChoose)
        : this(title, folder: null, files,
            new HashSet<string>(extensions.Select(e => e.StartsWith('.') ? e : "." + e), StringComparer.OrdinalIgnoreCase),
            onChoose)
    {
    }

    private FileBrowserScreen(string title, string? folder, IFileSystem files, HashSet<string> extensions,
        Func<FileEntry, IScreenContext, ScreenResult> onChoose)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(onChoose);
        Title = title;
        Folder = folder;
        _files = files;
        _extensions = extensions;
        _onChoose = onChoose;
        _level = folder is null
            ? new ListLevel(title, PlaceItems, c => c.Localizer.Get("files.help.places"), "files.noPlaces", "files.help.noPlaces")
            : new ListLevel(title, FolderItems, c => c.Localizer.Get("files.help.folder"), "files.empty", "files.help.empty");
    }

    public string Title { get; }

    /// <summary>The folder this level shows, or null for the first level (the places).</summary>
    public string? Folder { get; }

    public Utterance Enter(EnterReason reason, IScreenContext context) => _level.Enter(reason, context);

    public ScreenResult Handle(InputEvent input, IScreenContext context) => _level.Handle(input, context);

    public Utterance Help(IScreenContext context) => _level.Help(context);

    private IReadOnlyList<MenuItem> PlaceItems(IScreenContext context)
    {
        var l = context.Localizer;
        return [.. _files.Places().Select(place => new MenuItem(PlaceText.Describe(place, l),
            _ => ScreenResult.Push(Deeper(PlaceText.Name(place, l), place.Path))))];
    }

    /// <summary>Folders, then the files of the allowed types, in the order the file system gives them.</summary>
    private IReadOnlyList<MenuItem> FolderItems(IScreenContext context)
    {
        var l = context.Localizer;
        var entries = _files.List(Folder!);
        var folders = entries.Where(e => e.IsFolder).Select(entry => new MenuItem(
            l.Get("files.item.folder", ("name", entry.Name)),
            _ => ScreenResult.Push(Deeper(entry.Name, entry.Path))));
        var documents = entries.Where(e => !e.IsFolder && _extensions.Contains(Path.GetExtension(e.Name))).Select(entry =>
            new MenuItem(FileLabel(entry, l), c => _onChoose(entry, c)));
        return [.. folders, .. documents];
    }

    private FileBrowserScreen Deeper(string title, string folder) => new(title, folder, _files, _extensions, _onChoose);

    /// <summary>"Scrisoare, document Word": the name without its extension, then the type in words.</summary>
    private static string FileLabel(FileEntry entry, ILocalizer l)
    {
        var name = Path.GetFileNameWithoutExtension(entry.Name);
        return TypeKeys.TryGetValue(Path.GetExtension(entry.Name), out var typeKey) && name.Length > 0
            ? l.Get("files.item.file", ("name", name), ("type", l.Get(typeKey)))
            : entry.Name;
    }
}
