namespace Hasi.Core.Files;

public enum PlaceKind
{
    Documents,
    Desktop,
    Downloads,
    UsbStick,
}

/// <summary>A starting point for browsing: a known folder of the user, or a USB stick.</summary>
/// <param name="Label">The stick's name ("Kingston"); null for known folders, which are named from the resources.</param>
/// <param name="FreeBytes">Free space, for sticks; null when unknown.</param>
public sealed record Place(PlaceKind Kind, string Path, string? Label = null, long? FreeBytes = null);

public sealed record FileEntry(string Path, string Name, bool IsFolder, long Size, DateTimeOffset Modified);

/// <summary>
/// The disk, as HASI shows it to a beginner: a few places, folders and files, without hidden or system files.
/// Implemented in the application (Windows known folders, removable drives); faked in tests.
/// </summary>
public interface IFileSystem
{
    /// <summary>Documents, Desktop, Downloads, then each USB stick plugged in.</summary>
    IReadOnlyList<Place> Places();

    /// <summary>Folders first, then files, each alphabetically (Romanian order); hidden and system entries left out. Empty when the folder cannot be read.</summary>
    IReadOnlyList<FileEntry> List(string folder);

    bool Exists(string path);
}
