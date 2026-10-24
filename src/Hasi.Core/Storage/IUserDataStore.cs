namespace Hasi.Core.Storage;

/// <summary>
/// The user's personal data (drafts, recent documents, later the agenda), as small JSON files in one local folder
/// (<c>%LOCALAPPDATA%\HASI\userdata</c>, deleted at uninstall: ADR 0007 decision 9). Writes are atomic (a temporary
/// file, then a replace), so a power cut never leaves half a file; the first write of each day keeps a backup
/// copy, the last 7 days.
/// </summary>
public interface IUserDataStore
{
    /// <summary>The value stored under <paramref name="name"/> (a file name without extension), or default when there is none or it cannot be read.</summary>
    T? Read<T>(string name);

    /// <summary>False when the value could not be written (disk full, no access); the caller decides what to say.</summary>
    bool Write<T>(string name, T value);

    void Delete(string name);
}
