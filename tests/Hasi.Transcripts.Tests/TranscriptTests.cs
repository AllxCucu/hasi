using Hasi.Core.Modules;
using Hasi.Modules.About;
using Hasi.Modules.Exit;
using Hasi.Modules.KeyTutor;
using Hasi.Modules.Settings;

namespace Hasi.Transcripts.Tests;

public class TranscriptTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Transcripts");

    /// <summary>Main menu order. Keep in sync with src/Hasi.App/App.xaml.cs.</summary>
    private static IModule[] Modules() =>
        [new SettingsModule(), new KeyTutorModule(), new AboutModule(), new ExitModule()];

    public static TheoryData<string> Files() =>
        new(Directory.GetFiles(Folder, "*.transcript").Select(Path.GetFileName).Order()!);

    [Theory]
    [MemberData(nameof(Files))]
    public void Transcript_matches(string file)
    {
        var failures = TranscriptRunner.Run(Path.Combine(Folder, file), Modules);

        Assert.True(failures.Count == 0, string.Join("\n\n", failures));
    }

    [Fact]
    public void There_are_transcripts()
    {
        Assert.NotEmpty(Files());
    }
}
