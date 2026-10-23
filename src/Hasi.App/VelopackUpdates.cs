using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Threading;
using Hasi.Core.Diagnostics;
using Hasi.Core.Updates;
using Velopack;
using Velopack.Sources;

namespace Hasi.App;

/// <summary>
/// Updates through Velopack (ADR 0007). Work runs in the background; every result goes back to the window's
/// thread, where the shell lives. Only one check or download at a time.
/// </summary>
internal sealed class VelopackUpdates(
    Hasi.Core.Updates.UpdateSource source,
    string fallbackVersion,
    Dispatcher dispatcher,
    IDiagnosticLog log,
    Func<Task> waitForSpeech,
    Action beforeExit) : IUpdateService
{
    private readonly UpdateManager _manager = new(Create(source));
    private UpdateInfo? _available;

    public string CurrentVersion => _manager.CurrentVersion?.ToString() ?? fallbackVersion;

    public void Check(Action<UpdateCheck> done) => Run(async () =>
    {
        if (!_manager.IsInstalled) return new UpdateCheck(UpdateCheckStatus.NotInstalled);
        // A local folder needs no network; GitHub and web sources do.
        if (source.Kind != UpdateSourceKind.Folder && !NetworkInterface.GetIsNetworkAvailable()) return new UpdateCheck(UpdateCheckStatus.Offline);
        _available = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
        return _available is null
            ? new UpdateCheck(UpdateCheckStatus.UpToDate)
            : new UpdateCheck(UpdateCheckStatus.Available, _available.TargetFullRelease.Version.ToString());
    }, done, error => new UpdateCheck(error));

    public void Download(Action<int> progress, Action<bool> done) => Run(async () =>
    {
        if (_available is not { } update) return false;
        await _manager.DownloadUpdatesAsync(update, percent => dispatcher.InvokeAsync(() => progress(percent))).ConfigureAwait(false);
        return true;
    }, done, _ => false);

    public void ApplyAndRestart() => _ = ApplyAfterSpeechAsync();

    private async Task ApplyAfterSpeechAsync()
    {
        if (_available is not { } update) return;
        await waitForSpeech().ConfigureAwait(true);
        log.Write("UPDATE", $"applying {update.TargetFullRelease.Version} and restarting");
        beforeExit();
        // Exits HASI at once; Velopack applies the update and starts the new version, which then says so.
        _manager.ApplyUpdatesAndRestart(update.TargetFullRelease);
    }

    /// <summary>Runs <paramref name="work"/> off the window's thread and hands the result back to it.</summary>
    private void Run<T>(Func<Task<T>> work, Action<T> done, Func<UpdateCheckStatus, T> failed) => Task.Run(async () =>
    {
        T result;
        try
        {
            result = await work().ConfigureAwait(false);
        }
        catch (Velopack.Exceptions.NotInstalledException)
        {
            result = failed(UpdateCheckStatus.NotInstalled);
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or TaskCanceledException or IOException)
        {
            log.Write("UPDATE", $"offline: {ex.GetType().Name}: {ex.Message}");
            result = failed(UpdateCheckStatus.Offline);
        }
#pragma warning disable CA1031 // Any other failure is reported to the user in plain words, never as a crash.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ErrorLog.Write(ex, "update");
            log.Write("UPDATE", $"failed: {ex.GetType().Name}: {ex.Message}");
            result = failed(UpdateCheckStatus.Failed);
        }
        await dispatcher.InvokeAsync(() => done(result));
    });

    private static IUpdateSource Create(Hasi.Core.Updates.UpdateSource source) => source.Kind switch
    {
        UpdateSourceKind.Folder => new SimpleFileSource(new DirectoryInfo(source.Location)),
        UpdateSourceKind.Web => new SimpleWebSource(source.Location),
        _ => new GithubSource(source.Location, null, false),
    };
}
