using Hasi.Core.Diagnostics;

namespace Hasi.Core.Output;

/// <summary>
/// The single entry point for everything HASI says: normalizes text, handles interruption,
/// remembers the last message for "repeat", and logs what was said.
/// </summary>
public sealed class Announcer(OutputHub hub, ITextNormalizer normalizer, IDiagnosticLog log)
{
    private long _lastId;

    public Utterance? Last { get; private set; }

    /// <summary>Says <paramref name="utterance"/>; returns the id of its announcement, to match the speech channel's report.</summary>
    public long Say(Utterance utterance)
    {
        var id = Present(utterance);
        // A silent update (only the screen changes, e.g. typing with key echo off) is not a message to repeat.
        if (!string.IsNullOrWhiteSpace(utterance.Text)) Last = utterance;
        return id;
    }

    /// <summary>Says the last message again, interrupting whatever is being said.</summary>
    /// <param name="sensitive">Mask it in the log even if it was not masked the first time (a private screen is open).</param>
    public bool Repeat(bool sensitive = false)
    {
        if (Last is null) return false;
        Present(Last with { Priority = UtterancePriority.Interrupt, Sensitive = Last.Sensitive || sensitive });
        return true;
    }

    public void Silence() => hub.Cancel();

    private long Present(Utterance utterance)
    {
        // Ids increase, starting at 1 (0 means "no id"). Announcements are made on the shell's thread only.
        var id = ++_lastId;
        if (utterance.Priority == UtterancePriority.Interrupt) hub.Cancel();
        var spoken = normalizer.Normalize(utterance.Text);
        if (log.IsEnabled)
        {
            var text = utterance.Sensitive ? TextDiagnosticLog.Mask : utterance.Text;
            var normalized = utterance.Sensitive ? TextDiagnosticLog.Mask : spoken;
            log.Write("SAY", $"[{utterance.Priority}] text=\"{text}\"");
            log.Write("", $"normalized=\"{normalized}\"");
        }
        hub.Present(new Announcement(utterance, spoken, id));
        return id;
    }
}
