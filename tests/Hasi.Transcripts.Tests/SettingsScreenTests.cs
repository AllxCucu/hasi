using Hasi.Core.Input;
using Hasi.Core.Settings;
using Hasi.Modules.About;
using Hasi.Modules.Exit;
using Hasi.Modules.KeyTutor;
using Hasi.Modules.Settings;
using Hasi.Testing;

namespace Hasi.Transcripts.Tests;

/// <summary>Phase 0.6: what each setting says, and that every change is saved at once.</summary>
public class SettingsScreenTests
{
    private static (ShellHarness Harness, InMemorySettingsService Settings) InSettings()
    {
        var settings = new InMemorySettingsService();
        var h = new ShellHarness(settings: settings)
            .StartWithModules(new SettingsModule(), new KeyTutorModule(), new AboutModule(), new ExitModule());
        h.Press(Key.Enter);
        return (h, settings);
    }

    private static void Open(ShellHarness h, int index)
    {
        h.Press(Key.Home);
        for (var i = 0; i < index; i++) h.Press(Key.Down);
        h.Press(Key.Enter);
    }

    [Fact]
    public void Rate_is_said_as_0_to_100_with_50_normal_and_saved_at_each_step()
    {
        var (h, settings) = InSettings();
        Open(h, 1);

        Assert.Equal("Viteză. 50, viteza normală. Săgețile stânga și dreapta schimbă valoarea. Enter sau Escape vă întoarce.", h.LastSaid);
        Assert.Equal(["55", "60"], h.Press(Key.Right, Key.Right));
        Assert.Equal(2, settings.Current.Rate);
        Assert.Equal(2, settings.Saved.Count);
        Assert.Equal(["Setări. Selectat: Viteză, 2 din 12."], h.Press(Key.Escape));
    }

    [Fact]
    public void Volume_goes_in_steps_of_ten_and_full_volume_is_named()
    {
        var (h, settings) = InSettings();
        Open(h, 2);

        Assert.StartsWith("Volum. 100, volumul maxim.", h.LastSaid);
        Assert.Equal(["90"], h.Press(Key.Left));
        Assert.Equal(90, settings.Current.Volume);
    }

    [Fact]
    public void Pitch_is_said_like_rate()
    {
        var (h, settings) = InSettings();
        Open(h, 3);

        Assert.StartsWith("Ton. 50, tonul normal.", h.LastSaid);
        Assert.Equal(["45"], h.Press(Key.Left));
        Assert.Equal(-1, settings.Current.Pitch);
    }

    [Fact]
    public void Voice_choice_lists_the_voices_and_confirms_the_new_one()
    {
        var (h, settings) = InSettings();
        Open(h, 0);

        Assert.Equal("Voce. 4 opțiuni. Selectat: Paul, RHVoice direct, actual, 1 din 4.", h.LastSaid);
        h.Press(Key.Down);
        Assert.Equal(["Vocea a fost schimbată: Paul, prin SAPI. Setări. Selectat: Voce, 1 din 12."], h.Press(Key.Enter));
        Assert.Equal("sapi5:Paul", settings.Current.Voice);
    }

    [Fact]
    public void A_voice_that_does_not_start_is_said_and_the_old_one_stays()
    {
        var (h, settings) = InSettings();
        settings.BrokenVoices.Add("sapi5:Microsoft Andrei");
        Open(h, 0);
        h.Press(Key.Down, Key.Down);

        Assert.Equal(["Vocea Microsoft Andrei, prin SAPI nu a pornit. Rămâne vocea Paul, RHVoice direct."], h.Press(Key.Enter));
        Assert.Null(settings.Current.Voice);
    }

    [Fact]
    public void Voices_that_are_not_romanian_appear_only_after_show_all()
    {
        var (h, settings) = InSettings();
        Open(h, 0);

        Assert.Equal(["Arată toate vocile, 4 din 4."], h.Press(Key.End));
        Assert.Equal(["Voce. 4 opțiuni. Selectat: Paul, RHVoice direct, actual, 1 din 4."], h.Press(Key.Enter));
        Assert.Equal(["Microsoft David, prin SAPI, 4 din 4."], h.Press(Key.End));
        h.Press(Key.Enter);
        Assert.Equal("sapi5:Microsoft David", settings.Current.Voice);

        // The chosen voice stays visible in the short list even though it is not Romanian;
        // nothing else is hidden here, so "Arată toate vocile" is not offered.
        Open(h, 0);
        Assert.Equal("Voce. 4 opțiuni. Selectat: Microsoft David, prin SAPI, actual, 4 din 4.", h.LastSaid);
    }

    [Fact]
    public void Key_echo_choice_changes_how_typing_is_spoken()
    {
        var (h, settings) = InSettings();
        Open(h, 4);

        Assert.Equal("Ecoul tastelor. 3 opțiuni. Selectat: Fiecare caracter, actual, 1 din 3.", h.LastSaid);
        h.Press(Key.Down);
        Assert.Equal(["Ecoul tastelor: Fiecare cuvânt. Setări. Selectat: Ecoul tastelor, 5 din 12."], h.Press(Key.Enter));
        Assert.Equal(KeyEcho.Word, settings.Current.KeyEcho);
        Assert.Equal(KeyEcho.Word, h.Shell.Options.KeyEcho);
    }

    [Fact]
    public void Colors_and_text_size_are_saved()
    {
        var (h, settings) = InSettings();
        Open(h, 5);
        h.Press(Key.End, Key.Enter);
        Open(h, 6);

        Assert.Equal("Mărimea textului. 5 opțiuni. Selectat: Mare, actual, 3 din 5.", h.LastSaid);
        h.Press(Key.End, Key.Enter);

        Assert.Equal((ColorScheme.BlackOnYellow, TextSize.Huge), (settings.Current.Colors, settings.Current.TextSize));
    }

    [Fact]
    public void List_end_can_be_switched_to_wrapping()
    {
        var (h, settings) = InSettings();
        Open(h, 7);
        h.Press(Key.Down, Key.Enter);

        Assert.True(settings.Current.WrapLists);
        Assert.True(h.Shell.Options.WrapLists);
        // The settings list itself now wraps.
        h.Press(Key.Home);
        Assert.Equal(["Actualizări, 12 din 12."], h.Press(Key.Up));
    }
}
