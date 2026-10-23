namespace Hasi.Text.Ro.Tests;

/// <summary>
/// Runs every "*.cases" table: one case per line, <c>what is written =&gt; what must be spoken</c>.
/// Lines starting with # are comments. The tester adds a line for every mispronunciation found.
/// </summary>
public class CaseFileTests
{
    private const string Separator = " => ";
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Cases");
    private static readonly RomanianNormalizer Normalizer = new(AcronymLexicon.Load(AcronymLexicon.DefaultPath), NounGenderLexicon.LoadDefault());

    public static TheoryData<string> Files() =>
        new(Directory.GetFiles(Folder, "*.cases").Select(Path.GetFileName).Order()!);

    [Theory]
    [MemberData(nameof(Files))]
    public void Every_case_is_spoken_as_expected(string file)
    {
        var failures = new List<string>();
        var lines = File.ReadAllLines(Path.Combine(Folder, file));
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var at = line.IndexOf(Separator, StringComparison.Ordinal);
            if (at < 0)
            {
                failures.Add($"{file}:{i + 1}: missing '{Separator.Trim()}'");
                continue;
            }
            var written = line[..at];
            var expected = line[(at + Separator.Length)..];
            var actual = Normalizer.Normalize(written);
            if (actual != expected)
            {
                failures.Add($"{file}:{i + 1}\n  scris:    {written}\n  așteptat: {expected}\n  rostit:   {actual}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }

    [Fact]
    public void There_are_case_files()
    {
        Assert.NotEmpty(Files());
    }
}
