using Hasi.Core.Storage;
using Hasi.Testing;

namespace Hasi.Core.Tests;

public sealed class JsonUserDataStoreTests : IDisposable
{
    private sealed record Draft(string Name, string Text, int Cursor);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "hasi-userdata-" + Guid.NewGuid().ToString("N"));

    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero),
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private JsonUserDataStore Store() => new(_root, _time);

    private static Draft Sample(string text = "Dragă Ana, ți-am scris.") => new("Scrisoare", text, 7);

    [Fact]
    public void A_value_written_is_read_back_and_the_folder_is_created()
    {
        var store = Store();

        Assert.True(store.Write("ciorna", Sample()));

        Assert.Equal(Sample(), store.Read<Draft>("ciorna"));
        Assert.True(File.Exists(Path.Combine(_root, "ciorna.json")));
    }

    [Fact]
    public void The_file_is_indented_utf8_json_with_the_diacritics_as_written()
    {
        Store().Write("ciorna", Sample());

        var bytes = File.ReadAllBytes(Path.Combine(_root, "ciorna.json"));
        var text = File.ReadAllText(Path.Combine(_root, "ciorna.json"));
        Assert.NotEqual(0xEF, bytes[0]); // No byte order mark.
        Assert.Contains(Environment.NewLine + "  \"Name\": \"Scrisoare\"", text, StringComparison.Ordinal);
        Assert.Equal(Sample(), System.Text.Json.JsonSerializer.Deserialize<Draft>(text));
    }

    [Fact]
    public void Nothing_stored_reads_as_default()
    {
        Assert.Null(Store().Read<Draft>("ciorna"));
        Assert.Equal(0, Store().Read<int>("numar"));
    }

    [Fact]
    public void Replacing_leaves_no_temporary_file()
    {
        var store = Store();
        store.Write("ciorna", Sample("unu"));

        store.Write("ciorna", Sample("doi"));

        Assert.Equal("doi", store.Read<Draft>("ciorna")!.Text);
        Assert.Equal(["ciorna.json"], Directory.GetFiles(_root).Select(Path.GetFileName));
    }

    [Fact]
    public void The_first_write_of_a_day_keeps_the_previous_file_once()
    {
        var store = Store();
        store.Write("ciorna", Sample("ieri"));
        Assert.Empty(store.Backups("ciorna")); // Nothing to keep before the very first write.

        store.Write("ciorna", Sample("azi 1"));
        store.Write("ciorna", Sample("azi 2"));

        var backup = Assert.Single(store.Backups("ciorna"));
        Assert.Equal("ciorna-2026-10-24.json", Path.GetFileName(backup));
        Assert.Contains("ieri", File.ReadAllText(backup), StringComparison.Ordinal);
    }

    [Fact]
    public void The_day_is_the_local_day()
    {
        var store = Store();
        store.Write("ciorna", Sample());
        _time.SetUtcNow(new DateTimeOffset(2026, 10, 24, 22, 30, 0, TimeSpan.Zero)); // 01:30 on the 25th in Bucharest.

        store.Write("ciorna", Sample("după miezul nopții"));

        Assert.Equal("ciorna-2026-10-25.json", Path.GetFileName(Assert.Single(store.Backups("ciorna"))));
    }

    [Fact]
    public void Only_the_seven_newest_backups_are_kept_per_name()
    {
        var store = Store();
        store.Write("ciorna", Sample("0"));
        store.Write("recente", Sample("alt nume"));
        for (var day = 1; day <= 9; day++)
        {
            _time.Advance(TimeSpan.FromDays(1));
            store.Write("ciorna", Sample(day.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        _time.Advance(TimeSpan.FromDays(1));
        store.Write("recente", Sample("alt nume, a doua zi"));

        var kept = store.Backups("ciorna").Select(Path.GetFileName).ToList();
        Assert.Equal(7, kept.Count);
        Assert.Equal("ciorna-2026-11-02.json", kept[0]);
        Assert.Equal("ciorna-2026-10-27.json", kept[^1]);
        Assert.Single(store.Backups("recente"));
    }

    [Fact]
    public void A_corrupt_file_falls_back_to_the_newest_readable_backup()
    {
        var store = Store();
        store.Write("ciorna", Sample("vechi"));
        _time.Advance(TimeSpan.FromDays(1));
        store.Write("ciorna", Sample("ieri"));
        _time.Advance(TimeSpan.FromDays(1));
        store.Write("ciorna", Sample("azi"));
        // The newest backup is damaged too; the one before it is read.
        File.WriteAllText(store.Backups("ciorna")[0], "{ nu e JSON");
        File.WriteAllText(store.PathOf("ciorna"), "{\"Name\": \"Scris");

        Assert.Equal("vechi", store.Read<Draft>("ciorna")!.Text);
    }

    [Fact]
    public void A_corrupt_file_without_backups_reads_as_default()
    {
        var store = Store();
        Directory.CreateDirectory(_root);
        File.WriteAllText(store.PathOf("ciorna"), "nu e JSON");

        Assert.Null(store.Read<Draft>("ciorna"));
    }

    [Fact]
    public void A_write_that_cannot_reach_the_disk_returns_false()
    {
        Directory.CreateDirectory(_root);
        var blocked = Path.Combine(_root, "fisier");
        File.WriteAllText(blocked, "");
        // The root is a file, so no folder can be created there.
        var store = new JsonUserDataStore(blocked, _time);

        Assert.False(store.Write("ciorna", Sample()));
        Assert.Null(store.Read<Draft>("ciorna"));
    }

    [Fact]
    public void Delete_removes_the_value_but_keeps_the_backups()
    {
        var store = Store();
        store.Write("ciorna", Sample("unu"));
        store.Write("ciorna", Sample("doi"));

        store.Delete("ciorna");
        store.Delete("nu-exista");

        Assert.Null(store.Read<Draft>("ciorna"));
        Assert.Single(store.Backups("ciorna"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ciorna")]
    [InlineData("ciornă")]
    [InlineData("ciorna.json")]
    [InlineData("../settings")]
    [InlineData("a b")]
    [InlineData("ciorna\n")]
    public void Names_are_lowercase_letters_digits_and_dashes(string name)
    {
        var store = Store();

        Assert.Throws<ArgumentException>(() => store.Write(name, 1));
        Assert.Throws<ArgumentException>(() => store.Read<int>(name));
        Assert.Throws<ArgumentException>(() => store.Delete(name));
    }

    [Fact]
    public void The_harness_store_round_trips_copies_and_can_fail()
    {
        var store = new InMemoryUserDataStore();
        var draft = Sample();

        Assert.True(store.Write("ciorna", draft));
        Assert.Equal(draft, store.Read<Draft>("ciorna"));
        store.FailWrites = true;
        Assert.False(store.Write("ciorna", Sample("alt text")));
        Assert.Equal(draft, store.Read<Draft>("ciorna"));
        store.Delete("ciorna");
        Assert.Null(store.Read<Draft>("ciorna"));
    }
}
