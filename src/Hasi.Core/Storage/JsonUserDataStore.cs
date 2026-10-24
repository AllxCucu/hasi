using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Text.Unicode;

namespace Hasi.Core.Storage;

/// <summary>
/// <see cref="IUserDataStore"/> over one folder: <c>&lt;root&gt;/&lt;name&gt;.json</c>, written through a temporary
/// file and a replace. Before the first write of each local day the previous file is copied to
/// <c>&lt;root&gt;/backups/&lt;name&gt;-yyyy-MM-dd.json</c>; the 7 newest copies are kept. Never throws on disk problems:
/// a read falls back to the newest readable copy, a write returns false.
/// </summary>
public sealed partial class JsonUserDataStore : IUserDataStore
{
    public const int BackupsKept = 7;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        // Romanian letters stay readable in the file instead of \u escapes.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    private readonly string _root;
    private readonly TimeProvider _time;

    public JsonUserDataStore(string root, TimeProvider time)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentNullException.ThrowIfNull(time);
        _root = root;
        _time = time;
    }

    public string BackupFolder => Path.Combine(_root, "backups");

    public string PathOf(string name) => Path.Combine(_root, Checked(name) + ".json");

    public T? Read<T>(string name)
    {
        var path = PathOf(name);
        if (!File.Exists(path)) return default;
        if (TryRead<T>(path, out var value)) return value;
        foreach (var backup in Backups(name))
        {
            if (TryRead(backup, out value)) return value;
        }
        return default;
    }

    public bool Write<T>(string name, T value)
    {
        var path = PathOf(name);
        var temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(_root);
            BackUpOncePerDay(name, path);
            var bytes = Utf8.GetBytes(JsonSerializer.Serialize(value, Json));
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temporary);
            return false;
        }
    }

    public void Delete(string name) => TryDelete(PathOf(name));

    /// <summary>The backup copies of <paramref name="name"/>, newest first.</summary>
    public IReadOnlyList<string> Backups(string name)
    {
        Checked(name);
        if (!Directory.Exists(BackupFolder)) return [];
        try
        {
            return [.. Directory.EnumerateFiles(BackupFolder, name + "-*.json")
                .Where(file => BackupName().Match(Path.GetFileName(file)) is { Success: true } match && match.Groups["name"].Value == name)
                .OrderByDescending(file => Path.GetFileName(file), StringComparer.Ordinal)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void BackUpOncePerDay(string name, string path)
    {
        if (!File.Exists(path)) return;
        var day = _time.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var backup = Path.Combine(BackupFolder, $"{name}-{day}.json");
        if (File.Exists(backup)) return;
        Directory.CreateDirectory(BackupFolder);
        File.Copy(path, backup);
        foreach (var old in Backups(name).Skip(BackupsKept)) TryDelete(old);
    }

    private static bool TryRead<T>(string path, out T? value)
    {
        try
        {
            value = JsonSerializer.Deserialize<T>(File.ReadAllText(path, Utf8), Json);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            value = default;
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left behind; the next write replaces it.
        }
    }

    private static string Checked(string name)
    {
        if (string.IsNullOrEmpty(name) || !ValidName().IsMatch(name))
        {
            throw new ArgumentException($"'{name}' is not a valid user data name: use lowercase letters, digits and '-'.", nameof(name));
        }
        return name;
    }

    [GeneratedRegex(@"^[a-z0-9-]+\z")]
    private static partial Regex ValidName();

    [GeneratedRegex(@"^(?<name>[a-z0-9-]+)-\d{4}-\d{2}-\d{2}\.json\z")]
    private static partial Regex BackupName();
}
