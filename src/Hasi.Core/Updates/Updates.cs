using Hasi.Core.Diagnostics;
using Hasi.Core.Localization;
using Hasi.Core.Modules;
using Hasi.Core.Output;
using Hasi.Core.Screens;
using Hasi.Core.Settings;

namespace Hasi.Core.Updates;

public enum UpdateCheckStatus
{
    UpToDate,
    Available,

    /// <summary>No internet connection (or the source cannot be reached).</summary>
    Offline,

    /// <summary>HASI was not installed with the installer (for example it runs from a build folder).</summary>
    NotInstalled,

    Failed,
}

/// <param name="Version">The newest version, when <see cref="UpdateCheckStatus.Available"/>.</param>
public sealed record UpdateCheck(UpdateCheckStatus Status, string? Version = null);

/// <summary>
/// Checks, downloads and applies updates. Implemented by the application over Velopack; every callback is
/// delivered on the shell's thread.
/// </summary>
public interface IUpdateService
{
    string CurrentVersion { get; }

    void Check(Action<UpdateCheck> done);

    /// <param name="progress">Percent, 0..100.</param>
    /// <param name="done">True when the update is ready to apply.</param>
    void Download(Action<int> progress, Action<bool> done);

    /// <summary>Once the current speech has finished: closes HASI, applies the update and starts HASI again.</summary>
    void ApplyAndRestart();
}

public enum UpdateSourceKind
{
    GitHub,
    Folder,
    Web,
}

/// <summary>
/// Where updates come from, as written in <c>settings.json</c> (<c>UpdateSource</c>) or given with
/// <c>--update-source</c>: nothing or <c>github</c> = HASI's GitHub Releases; <c>github:&lt;repository URL&gt;</c>;
/// <c>folder:&lt;path&gt;</c> (tests, computers without internet); <c>http:&lt;URL&gt;</c> or a plain https URL.
/// </summary>
public sealed record UpdateSource(UpdateSourceKind Kind, string Location)
{
    public const string HasiRepository = "https://github.com/AllxCucu/hasi";

    public static UpdateSource Default { get; } = new(UpdateSourceKind.GitHub, HasiRepository);

    public static UpdateSource Parse(string? text)
    {
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value) || value.Equals("github", StringComparison.OrdinalIgnoreCase)) return Default;
        if (TryPrefix(value, "github:", out var repository)) return new(UpdateSourceKind.GitHub, repository);
        if (TryPrefix(value, "folder:", out var folder)) return new(UpdateSourceKind.Folder, folder);
        if (TryPrefix(value, "http:", out var url) && !url.StartsWith("//", StringComparison.Ordinal)) return new(UpdateSourceKind.Web, url);
        if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return new(UpdateSourceKind.Web, value);
        }
        throw new FormatException($"Unknown update source '{value}'. Use github, github:<url>, folder:<path> or http:<url>.");
    }

    private static bool TryPrefix(string value, string prefix, out string rest)
    {
        rest = value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? value[prefix.Length..].Trim() : "";
        return rest.Length > 0;
    }
}

/// <summary>
/// What the user hears about updates, at startup and from Settings → Actualizări. At startup a new version is
/// offered **once**: after Escape HASI does not ask again for it, only for a newer one (the owner's decision,
/// 2026-10-23); it stays available in Settings.
/// </summary>
public sealed class UpdateFlow(IUpdateService updates, ISettingsService settings, IShellServices shell, ILocalizer localizer, IDiagnosticLog log)
{
    private int _lastProgressStep;

    public string CurrentVersion => updates.CurrentVersion;

    /// <summary>Called after the welcome. Says nothing unless a version not offered before is available.</summary>
    public void CheckAtStartup()
    {
        if (!settings.Current.AutoCheckUpdates) return;
        updates.Check(check =>
        {
            log.Write("UPDATE", $"startup check: {check.Status} {check.Version}");
            if (check is not { Status: UpdateCheckStatus.Available, Version: { } version }) return;
            if (!IsNewer(version, settings.Current.UpdateOfferedVersion)) return;
            settings.Update(s => s with { UpdateOfferedVersion = version });
            shell.Show(Offer(version), UtterancePriority.Queue);
        });
    }

    /// <summary>"Caută actualizări acum": always answers, and always offers an available version.</summary>
    public ScreenResult CheckNow()
    {
        updates.Check(check =>
        {
            log.Write("UPDATE", $"manual check: {check.Status} {check.Version}");
            shell.Show(check switch
            {
                { Status: UpdateCheckStatus.Available, Version: { } version } => Offer(version),
                { Status: UpdateCheckStatus.UpToDate } => Say("update.upToDate", ("version", updates.CurrentVersion)),
                { Status: UpdateCheckStatus.Offline } => SayError("update.offline"),
                { Status: UpdateCheckStatus.NotInstalled } => SayError("update.notInstalled"),
                _ => SayError("update.checkFailed"),
            });
        });
        return Say("update.checking");
    }

    private ScreenResult Offer(string version) => ScreenResult.Confirm(
        localizer.Get("update.offer", ("version", version)),
        // Installing closes HASI: unsaved work is asked about first.
        () => shell.GuardUnsaved(() => StartDownload(version)),
        localizer.Get("update.later"));

    private ScreenResult StartDownload(string version)
    {
        _lastProgressStep = 0;
        log.Write("UPDATE", $"download {version}");
        updates.Download(
            percent =>
            {
                // Every quarter, never twice the same: enough to know it is moving, not a stream of numbers.
                var step = Math.Clamp(percent, 0, 100) / 25;
                if (step <= _lastProgressStep || step >= 4) return;
                _lastProgressStep = step;
                shell.Show(Say("update.progress", ("percent", step * 25)), UtterancePriority.Queue);
            },
            ready =>
            {
                log.Write("UPDATE", ready ? $"{version} ready" : $"{version} download failed");
                if (!ready)
                {
                    shell.Show(SayError("update.downloadFailed", ("version", updates.CurrentVersion)));
                    return;
                }
                // Something may have been typed during the download: asked about again before HASI closes.
                shell.Show(shell.GuardUnsaved(() =>
                {
                    // Said first: applying waits only for the speech already started.
                    shell.Show(Say("update.ready"));
                    updates.ApplyAndRestart();
                    return ScreenResult.Handled;
                }));
            });
        return Say("update.downloading");
    }

    /// <summary>True when <paramref name="version"/> is newer than <paramref name="offered"/> (or nothing was offered yet).</summary>
    public static bool IsNewer(string version, string? offered)
    {
        if (offered is null) return true;
        return System.Version.TryParse(Core(version), out var a) && System.Version.TryParse(Core(offered), out var b)
            ? a > b
            : !string.Equals(version, offered, StringComparison.OrdinalIgnoreCase);

        // "0.2.0-beta.1+abc" → "0.2.0"
        static string Core(string v) => v.Split('-', '+')[0];
    }

    private ScreenResult Say(string key, params (string Name, object Value)[] args) =>
        ScreenResult.Say(localizer.Get(key, args));

    private ScreenResult SayError(string key, params (string Name, object Value)[] args) =>
        ScreenResult.Say(new Utterance(localizer.Get(key, args), Earcon: Earcon.Error));
}
