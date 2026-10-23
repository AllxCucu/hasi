using Hasi.Core.Localization;
using Hasi.Core.Output;

namespace Hasi.Core;

/// <summary>What HASI found at startup that the user has to hear about.</summary>
/// <param name="AfterError">HASI restarted itself after a serious error.</param>
/// <param name="NarratorRunning">Windows Narrator is on and will speak over HASI.</param>
/// <param name="RealPowerNotice">"Simulare oprire" was turned off since the last start.</param>
/// <param name="ReturnHotkeyUnavailable">Another program holds Ctrl+Alt+H.</param>
/// <param name="UpdatedTo">HASI was just updated to this version.</param>
/// <param name="AutostartTurnedOn">First start after installing: autostart was turned on.</param>
public sealed record StartupFacts(
    bool AfterError = false,
    bool NarratorRunning = false,
    bool RealPowerNotice = false,
    bool ReturnHotkeyUnavailable = false,
    string? UpdatedTo = null,
    bool AutostartTurnedOn = false);

/// <summary>The notices said after the welcome, queued, most important first.</summary>
public static class StartupNotices
{
    public static IReadOnlyList<Utterance> For(StartupFacts facts, ILocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(localizer);
        List<Utterance> notices = [];
        if (facts.UpdatedTo is { } version) notices.Add(new Utterance(localizer.Get("startup.updated", ("version", version)), UtterancePriority.Queue));
        if (facts.AfterError) notices.Add(new Utterance(localizer.Get("error.restarted"), UtterancePriority.Queue, Earcon.Error));
        if (facts.NarratorRunning) notices.Add(new Utterance(localizer.Get("startup.narrator"), UtterancePriority.Queue));
        if (facts.RealPowerNotice) notices.Add(new Utterance(localizer.Get("power.realNotice"), UtterancePriority.Queue));
        if (facts.ReturnHotkeyUnavailable) notices.Add(new Utterance(localizer.Get("startup.hotkeyUnavailable"), UtterancePriority.Queue));
        if (facts.AutostartTurnedOn) notices.Add(new Utterance(localizer.Get("startup.autostartOn"), UtterancePriority.Queue));
        return notices;
    }
}
