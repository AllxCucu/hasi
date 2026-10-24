using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Hasi.Core.Files;

namespace Hasi.App;

/// <summary>The real disk: the user's known folders, USB sticks, and folder listings without hidden or system entries.</summary>
public sealed class WindowsFileSystem : IFileSystem
{
    private static readonly Guid DownloadsFolderId = new("374DE290-123F-4565-9164-39C4925E467B");

    private static readonly StringComparer RomanianOrder =
        StringComparer.Create(CultureInfo.GetCultureInfo("ro-RO"), ignoreCase: true);

    public IReadOnlyList<Place> Places()
    {
        List<Place> places = [];
        AddIfExists(places, PlaceKind.Documents, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        AddIfExists(places, PlaceKind.Desktop, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        AddIfExists(places, PlaceKind.Downloads, DownloadsFolder());

        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (IOException)
        {
            return places;
        }
        catch (UnauthorizedAccessException)
        {
            return places;
        }

        foreach (var drive in drives)
        {
            try
            {
                if (drive.DriveType != DriveType.Removable || !drive.IsReady) continue;
                var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? null : drive.VolumeLabel;
                places.Add(new Place(PlaceKind.UsbStick, drive.RootDirectory.FullName, label, drive.AvailableFreeSpace));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The stick was pulled out while we were looking at it: leave it out.
            }
        }
        return places;
    }

    public IReadOnlyList<FileEntry> List(string folder)
    {
        try
        {
            var visible = new DirectoryInfo(folder).EnumerateFileSystemInfos()
                .Where(info => (info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                .ToList();
            var folders = visible.OfType<DirectoryInfo>()
                .OrderBy(d => d.Name, RomanianOrder)
                .Select(d => new FileEntry(d.FullName, d.Name, IsFolder: true, Size: 0, d.LastWriteTime));
            var files = visible.OfType<FileInfo>()
                .OrderBy(f => f.Name, RomanianOrder)
                .Select(f => new FileEntry(f.FullName, f.Name, IsFolder: false, f.Length, f.LastWriteTime));
            return [.. folders, .. files];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException
            or ArgumentException)
        {
            return [];
        }
    }

    public bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    private static void AddIfExists(List<Place> places, PlaceKind kind, string? path)
    {
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) places.Add(new Place(kind, path));
    }

    /// <summary>Downloads has no SpecialFolder value; ask Windows for the known folder (it may have been moved).</summary>
    private static string? DownloadsFolder()
    {
        var id = DownloadsFolderId;
        if (SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out var pointer) == 0)
        {
            try
            {
                var path = Marshal.PtrToStringUni(pointer);
                if (!string.IsNullOrEmpty(path)) return path;
            }
            finally
            {
                Marshal.FreeCoTaskMem(pointer);
            }
        }
        else if (pointer != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(pointer);
        }
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(profile) ? null : Path.Combine(profile, "Downloads");
    }

    [DllImport("shell32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int SHGetKnownFolderPath(ref Guid folderId, uint flags, IntPtr token, out IntPtr path);
}
