using Hasi.Core.Input;
using Hasi.Core.Speech;
using Hasi.Modules.About;
using Hasi.Modules.Exit;
using Hasi.Modules.KeyTutor;
using Hasi.Modules.Settings;
using Hasi.Testing;

namespace Hasi.Transcripts.Tests;

public class VoiceComparisonScreenTests
{
    private sealed class FakeComparison : IVoiceComparison
    {
        public List<(string Text, int? Variant)> Played { get; } = [];

        public int Stops { get; private set; }

        public IReadOnlyList<string> Variants => ["Paul prin SAPI", "Paul prin RHVoice direct"];

        public void Play(string text, int? variant = null) => Played.Add((text, variant));

        public void Stop() => Stops++;
    }

    private static (ShellHarness Harness, FakeComparison Comparison) Open()
    {
        var comparison = new FakeComparison();
        var h = new ShellHarness(comparison: comparison)
            .StartWithModules(new SettingsModule(), new KeyTutorModule(), new AboutModule(), new ExitModule());
        h.Press(Key.Enter, Key.End, Key.Up, Key.Enter, Key.Down, Key.Enter);
        return (h, comparison);
    }

    [Fact]
    public void Lists_the_fixed_phrases()
    {
        var (h, _) = Open();

        Assert.Equal("Comparare voci. 5 opțiuni. Selectat: Numere și sume, 1 din 5.", h.LastSaid);
    }

    [Fact]
    public void Enter_plays_the_phrase_through_every_variant_and_says_nothing_itself()
    {
        var (h, comparison) = Open();

        Assert.Empty(h.Press(Key.Enter));
        var played = Assert.Single(comparison.Played);
        Assert.StartsWith("Am plătit 1.250,99 lei", played.Text);
        Assert.Null(played.Variant);
    }

    [Fact]
    public void Left_and_right_play_only_one_variant()
    {
        var (h, comparison) = Open();
        h.Press(Key.Down);

        h.Press(Key.Left, Key.Right);

        Assert.Equal([0, 1], comparison.Played.Select(p => p.Variant));
        Assert.All(comparison.Played, p => Assert.StartsWith("Șase școlari", p.Text));
    }

    [Fact]
    public void Help_names_the_variants()
    {
        var (h, _) = Open();

        Assert.Contains("Paul prin SAPI, Paul prin RHVoice direct", h.Press(Key.F1)[0]);
    }
}
