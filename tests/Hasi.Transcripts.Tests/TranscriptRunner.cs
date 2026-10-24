using Hasi.Core.Files;
using Hasi.Core.Input;
using Hasi.Core.Modules;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Testing;

namespace Hasi.Transcripts.Tests;

/// <summary>
/// Runs a transcript file against the real shell, modules and Romanian resources.
/// <code>
/// # comment
/// > start                  starts HASI on the main menu
/// > Down                   presses a key (Ctrl+H, Alt+F4, NumPad8 ...)
/// &lt; Învață tastele, 2 din 4.   what must be said after that key, in order
/// &lt; [sunet] Sfârșitul ...  the message comes with a short sound
/// > scrie: 12 lei          types text (the per-character echo is not checked)
/// > calendar: 2026-10-24   opens the generic date picker (test fixture only)
/// > time: 2026-10-24T12:00:03Z sets the fake clock
/// > tick                   checks hourly announcements
/// ! exit                   HASI must have been asked to close
/// </code>
/// A key followed by no "&lt;" line must produce silence.
/// </summary>
public static class TranscriptRunner
{
    public const string EarconMarker = "[sunet] ";

    /// <summary>"> scrie: text" types the text; the echo of each character is not checked.</summary>
    public const string TypePrefix = "scrie: ";

    /// <summary>The Documents folder of the fake disk every transcript runs with.</summary>
    public const string DocumentsFolder = @"C:\Users\Ana\Documents";

    public static IReadOnlyList<string> Run(string path, Func<IModule[]> modules)
    {
        var failures = new List<string>();
        var lines = File.ReadAllLines(path);
        ShellHarness? harness = null;
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 10, 24, 11, 32, 0, TimeSpan.Zero),
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest"));
        var lineNumber = 0;

        string? step = null;
        var stepLine = 0;
        var expected = new List<string>();
        List<string> actual = [];

        void CheckStep()
        {
            if (step is null) return;
            if (!expected.SequenceEqual(actual))
            {
                failures.Add(
                    $"{Path.GetFileName(path)}:{stepLine} after '{step}'\n" +
                    $"  expected:\n{Format(expected)}\n  actual:\n{Format(actual)}");
            }
            expected.Clear();
        }

        foreach (var raw in lines)
        {
            lineNumber++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var marker = line[0];
            var body = line[1..].Trim();
            switch (marker)
            {
                case '>':
                    CheckStep();
                    step = body;
                    stepLine = lineNumber;
                    var before = harness?.Output.Announcements.Count ?? 0;
                    if (body == "start")
                    {
                        harness = new ShellHarness(time: time, documents: new FakeDocumentFormats());
                        // The editor saves new documents in Documente; nothing else is on the disk.
                        harness.Files.AddPlace(PlaceKind.Documents, DocumentsFolder);
                        harness.StartWithModules(modules());
                        before = 0;
                    }
                    else if (body.StartsWith(TypePrefix, StringComparison.Ordinal))
                    {
                        if (harness is null) throw new FormatException($"{path}:{lineNumber}: '> start' must come first.");
                        harness.Type(body[TypePrefix.Length..]);
                        // The per-character echo is not checked here; TextInputTests cover it.
                        step = null;
                        continue;
                    }
                    else
                    {
                        if (harness is null) throw new FormatException($"{path}:{lineNumber}: '> start' must come first.");
                        if (body.StartsWith("calendar: ", StringComparison.Ordinal))
                        {
                            var date = DateOnly.ParseExact(body[10..], "yyyy-MM-dd");
                            harness.Shell.Show(ScreenResult.Push(new DatePickerScreen("Calendar", date,
                                new DateOnly(1900, 1, 1), new DateOnly(2099, 12, 31), _ => null,
                                DateOnly.FromDateTime(time.GetLocalNow().DateTime))));
                        }
                        else if (body.StartsWith("time: ", StringComparison.Ordinal))
                        {
                            time.SetUtcNow(DateTimeOffset.Parse(body[6..], System.Globalization.CultureInfo.InvariantCulture));
                        }
                        else if (body == "tick") harness.Shell.ClockTick();
                        else harness.Press(KeyChord.Parse(body));
                    }
                    actual = [.. harness.Output.Announcements.Skip(before).Select(Describe)];
                    break;
                case '<':
                    expected.Add(body);
                    break;
                case '!' when body == "exit":
                    CheckStep();
                    step = null;
                    if (harness?.System.ExitRequested != true)
                        failures.Add($"{Path.GetFileName(path)}:{lineNumber} HASI was not asked to close.");
                    break;
                default:
                    throw new FormatException($"{path}:{lineNumber}: unknown line '{line}'.");
            }
        }
        CheckStep();

        if (harness is not null)
        {
            failures.AddRange(harness.Localizer.MissingKeys.Select(k => $"{Path.GetFileName(path)}: missing resource '{k}'"));
        }
        return failures;
    }

    private static string Describe(Announcement announcement) =>
        (announcement.Utterance.Earcon == Earcon.None ? "" : EarconMarker) + announcement.Utterance.Text;

    private static string Format(IEnumerable<string> said)
    {
        var lines = said.Select(s => $"    < {s}").ToList();
        return lines.Count == 0 ? "    (nimic)" : string.Join('\n', lines);
    }
}
