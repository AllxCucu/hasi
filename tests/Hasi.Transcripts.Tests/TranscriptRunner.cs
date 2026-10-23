using Hasi.Core.Input;
using Hasi.Core.Modules;
using Hasi.Core.Output;
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
/// ! exit                   HASI must have been asked to close
/// </code>
/// A key followed by no "&lt;" line must produce silence.
/// </summary>
public static class TranscriptRunner
{
    public const string EarconMarker = "[sunet] ";

    /// <summary>"> scrie: text" types the text; the echo of each character is not checked.</summary>
    public const string TypePrefix = "scrie: ";

    public static IReadOnlyList<string> Run(string path, Func<IModule[]> modules)
    {
        var failures = new List<string>();
        var lines = File.ReadAllLines(path);
        ShellHarness? harness = null;
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
                        harness = new ShellHarness().StartWithModules(modules());
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
                        harness.Press(KeyChord.Parse(body));
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
