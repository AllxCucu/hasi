using Hasi.Core.Diagnostics;

namespace Hasi.Core.Output;

/// <summary>
/// The single entry point for everything HASI says: normalizes text, handles interruption,
/// remembers the last message for "repeat", and logs what was said.
/// </summary>
public sealed class Announcer(OutputHub hub, ITextNormalizer normalizer, IDiagnosticLog log)
{
    public Utterance? Last { get; private set; }

    public void Say(Utterance utterance)
    {
        Present(utterance);
        // A silent update (only the screen changes, e.g. typing with key echo off) is not a message to repeat.
        if (!string.IsNullOrWhiteSpace(utterance.Text)) Last = utterance;
    }

    /// <summary>Says the last message again, interrupting whatever is being said.</summary>
    public bool Repeat()
    {
        if (Last is null) return false;
        Present(Last with { Priority = UtterancePriority.Interrupt });
        return true;
    }

    public void Silence() => hub.Cancel();

    private void Present(Utterance utterance)
    {
        if (utterance.Priority == UtterancePriority.Interrupt) hub.Cancel();
        var spoken = normalizer.Normalize(utterance.Text);
        if (log.IsEnabled)
        {
            var text = utterance.Sensitive ? TextDiagnosticLog.Mask : utterance.Text;
            var normalized = utterance.Sensitive ? TextDiagnosticLog.Mask : spoken;
            log.Write("SAY", $"[{utterance.Priority}] text=\"{text}\"");
            log.Write("", $"normalized=\"{normalized}\"");
        }
        hub.Present(new Announcement(utterance, spoken));
    }
}
