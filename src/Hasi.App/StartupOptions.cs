using System.Globalization;

namespace Hasi.App;

/// <summary>
/// Command-line switches for testing until the Settings screens exist (phase 0.6/0.8):
/// <c>--diagnostic</c>, <c>--voice "name"</c>, <c>--engine sapi5|onecore|rhvoice-dll</c>,
/// <c>--rhvoice-path folder</c>, <c>--rate -10..10</c>, <c>--pitch -10..10</c>, <c>--volume 0..100</c>,
/// <c>--audio-idle-seconds N</c> (0 = keep the audio stream open), <c>--test-voice-failure</c>.
/// </summary>
internal sealed record StartupOptions(
    bool Diagnostic,
    string? Voice,
    int? Rate,
    string? Engine,
    bool TestVoiceFailure,
    string? RhVoicePath = null,
    int? Pitch = null,
    int? Volume = null,
    int? AudioIdleSeconds = null)
{
    public static StartupOptions Parse(IReadOnlyList<string> args)
    {
        var options = new StartupOptions(false, null, null, null, false);
        for (var i = 0; i < args.Count; i++)
        {
            var next = i + 1 < args.Count ? args[i + 1] : null;
            switch (args[i].ToLowerInvariant())
            {
                case "--diagnostic":
                    options = options with { Diagnostic = true };
                    break;
                case "--test-voice-failure":
                    options = options with { TestVoiceFailure = true };
                    break;
                case "--voice" when next is not null:
                    options = options with { Voice = next };
                    i++;
                    break;
                case "--engine" when next is not null:
                    options = options with { Engine = next };
                    i++;
                    break;
                case "--rhvoice-path" when next is not null:
                    options = options with { RhVoicePath = next };
                    i++;
                    break;
                case "--rate" when Number(next) is { } rate:
                    options = options with { Rate = rate };
                    i++;
                    break;
                case "--pitch" when Number(next) is { } pitch:
                    options = options with { Pitch = pitch };
                    i++;
                    break;
                case "--audio-idle-seconds" when Number(next) is { } idle:
                    options = options with { AudioIdleSeconds = idle };
                    i++;
                    break;
                case "--volume" when Number(next) is { } volume:
                    options = options with { Volume = volume };
                    i++;
                    break;
            }
        }
        return options;
    }

    private static int? Number(string? text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
}
