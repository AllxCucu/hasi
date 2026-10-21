using System.Globalization;

namespace Hasi.App;

/// <summary>
/// Command-line switches for testing until the Settings screens exist (phase 0.6/0.8):
/// <c>--diagnostic</c>, <c>--voice "name"</c>, <c>--rate -10..10</c>.
/// </summary>
internal sealed record StartupOptions(bool Diagnostic, string? Voice, int? Rate)
{
    public static StartupOptions Parse(IReadOnlyList<string> args)
    {
        var diagnostic = false;
        string? voice = null;
        int? rate = null;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--diagnostic":
                    diagnostic = true;
                    break;
                case "--voice" when i + 1 < args.Count:
                    voice = args[++i];
                    break;
                case "--rate" when i + 1 < args.Count
                    && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value):
                    rate = value;
                    i++;
                    break;
            }
        }
        return new StartupOptions(diagnostic, voice, rate);
    }
}
