using System.Globalization;
using Hasi.Core.Files;

namespace Hasi.Testing;

/// <summary>
/// A disk in memory: places, folders and files that a test sets up. Like the real one, <see cref="List"/> gives folders
/// first, then files, each in Romanian order. Paths use backslashes, as on Windows.
/// </summary>
public sealed class FakeFileSystem : IFileSystem
{
    private static readonly StringComparer RomanianOrder =
        StringComparer.Create(CultureInfo.GetCultureInfo("ro-RO"), ignoreCase: true);

    private readonly List<Place> _places = [];
    private readonly Dictionary<string, List<FileEntry>> _folders = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Folders that exist but cannot be read (no permission): <see cref="List"/> returns nothing for them.</summary>
    public HashSet<string> Unreadable { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Adds a place and creates its folder.</summary>
    public FakeFileSystem AddPlace(PlaceKind kind, string path, string? label = null, long? freeBytes = null)
    {
        _places.Add(new Place(kind, path, label, freeBytes));
        _folders.TryAdd(path, []);
        return this;
    }

    /// <summary>Takes a place away, as when a stick is pulled out; its files stay in memory but are no longer listed as a place.</summary>
    public FakeFileSystem RemovePlace(string path)
    {
        _places.RemoveAll(p => string.Equals(p.Path, path, StringComparison.OrdinalIgnoreCase));
        return this;
    }

    /// <summary>Creates a folder (and the folders above it).</summary>
    public FakeFileSystem AddFolder(string path, DateTimeOffset? modified = null)
    {
        Ensure(path, modified);
        return this;
    }

    /// <summary>Creates a file (and the folders above it).</summary>
    public FakeFileSystem AddFile(string path, long size = 0, DateTimeOffset? modified = null)
    {
        var parent = Parent(path) ?? throw new ArgumentException("A file needs a folder.", nameof(path));
        Ensure(parent, modified);
        var entries = _folders[parent];
        entries.RemoveAll(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
        entries.Add(new FileEntry(path, Name(path), IsFolder: false, size, modified ?? DateTimeOffset.UnixEpoch));
        return this;
    }

    public IReadOnlyList<Place> Places() => [.. _places];

    public IReadOnlyList<FileEntry> List(string folder)
    {
        if (Unreadable.Contains(folder) || !_folders.TryGetValue(folder, out var entries)) return [];
        return
        [
            .. entries.Where(e => e.IsFolder).OrderBy(e => e.Name, RomanianOrder),
            .. entries.Where(e => !e.IsFolder).OrderBy(e => e.Name, RomanianOrder),
        ];
    }

    public bool Exists(string path) =>
        _folders.ContainsKey(path)
        || (Parent(path) is { } parent && _folders.TryGetValue(parent, out var entries)
            && entries.Any(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase)));

    private void Ensure(string folder, DateTimeOffset? modified)
    {
        if (_folders.ContainsKey(folder)) return;
        _folders[folder] = [];
        if (Parent(folder) is not { } parent) return;
        Ensure(parent, modified);
        _folders[parent].Add(new FileEntry(folder, Name(folder), IsFolder: true, 0, modified ?? DateTimeOffset.UnixEpoch));
    }

    private static string? Parent(string path)
    {
        var trimmed = path.TrimEnd('\\');
        var cut = trimmed.LastIndexOf('\\');
        if (cut < 0) return null;
        // "E:\file.txt" lives in the drive root "E:\".
        return cut > 0 && trimmed[cut - 1] == ':' ? trimmed[..(cut + 1)] : trimmed[..cut];
    }

    private static string Name(string path)
    {
        var trimmed = path.TrimEnd('\\');
        return trimmed[(trimmed.LastIndexOf('\\') + 1)..];
    }
}
