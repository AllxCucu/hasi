using System.Text.Json;
using Hasi.Core.Storage;

namespace Hasi.Testing;

/// <summary>The user's data in memory, kept as JSON so a read returns a copy, as from the disk.</summary>
public sealed class InMemoryUserDataStore : IUserDataStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <summary>Simulates a disk that cannot be written (full, removed, no access).</summary>
    public bool FailWrites { get; set; }

    public IReadOnlyCollection<string> Names => _values.Keys;

    public int Writes { get; private set; }

    public T? Read<T>(string name) => _values.TryGetValue(name, out var json) ? JsonSerializer.Deserialize<T>(json) : default;

    public bool Write<T>(string name, T value)
    {
        if (FailWrites) return false;
        Writes++;
        _values[name] = JsonSerializer.Serialize(value);
        return true;
    }

    public void Delete(string name) => _values.Remove(name);
}
