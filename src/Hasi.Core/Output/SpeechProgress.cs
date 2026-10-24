namespace Hasi.Core.Output;

/// <summary>How a spoken message ended.</summary>
public enum SpeechOutcome
{
    /// <summary>All of its sound has been heard (not only synthesized).</summary>
    Completed,

    /// <summary>Stopped before the end: a key, Ctrl, or a message that interrupted it.</summary>
    Cancelled,

    /// <summary>The engine could not speak it.</summary>
    Failed,
}

/// <param name="Id">The id the announcer gave the announcement (<see cref="Announcement.Id"/>).</param>
public sealed record SpeechFinished(long Id, SpeechOutcome Outcome);
