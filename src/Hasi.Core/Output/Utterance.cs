namespace Hasi.Core.Output;

public enum UtterancePriority
{
    /// <summary>Stops whatever is being said and speaks now (navigation, answers to keys).</summary>
    Interrupt,

    /// <summary>Waits for the current speech to finish (continuous reading, follow-up notices).</summary>
    Queue,
}

/// <summary>Short non-speech sounds that accompany an utterance.</summary>
public enum Earcon
{
    None,

    /// <summary>The selection is already at the start or end of a list.</summary>
    EdgeOfList,

    /// <summary>The key has no function on this screen.</summary>
    Unavailable,

    Error,
}

/// <summary>Something HASI wants to tell the user.</summary>
/// <param name="Text">The text as written in the UI resources.</param>
/// <param name="DisplayText">What the screen shows, when it differs from <paramref name="Text"/>.</param>
/// <param name="Sensitive">Masked in diagnostic logs (passwords, private content).</param>
public sealed record Utterance(
    string Text,
    UtterancePriority Priority = UtterancePriority.Interrupt,
    Earcon Earcon = Earcon.None,
    string? DisplayText = null,
    bool Sensitive = false)
{
    public static implicit operator Utterance(string text) => new(text);

    /// <summary>Joins two utterances; the result keeps this one's priority and earcon.</summary>
    public Utterance Then(Utterance next) => this with
    {
        Text = $"{Text} {next.Text}",
        Earcon = Earcon == Earcon.None ? next.Earcon : Earcon,
        DisplayText = DisplayText is null && next.DisplayText is null
            ? null
            : $"{DisplayText ?? Text} {next.DisplayText ?? next.Text}",
        Sensitive = Sensitive || next.Sensitive,
    };
}

/// <summary>What output channels receive: the utterance plus the exact text sent to speech.</summary>
public sealed record Announcement(Utterance Utterance, string SpokenText);
