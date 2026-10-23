using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Speech.Synthesis;
using Hasi.Core.Diagnostics;

namespace Hasi.App;

/// <summary>
/// After a serious error (a crash, a burst of errors, a frozen window) HASI starts itself again with
/// <c>--after-error</c>, so the user is not left in silence; the new instance says what happened.
/// If that keeps happening (<see cref="CrashPolicy.MayRestart"/>), HASI says it is stopping and does not restart.
/// </summary>
internal sealed class CrashRecovery(IReadOnlyList<string> args, IDiagnosticLog log)
{
    public const string AfterErrorFlag = "--after-error";

    private static readonly string HistoryFile = Path.Combine(AppPaths.LocalData, "logs", "restarts.txt");

    private int _recovering;

    /// <summary>Whether diagnostic mode is on, so the new instance keeps it.</summary>
    public Func<bool> DiagnosticIsOn { get; set; } = () => false;

    /// <summary>Restarts HASI (or gives up) and ends this process. Only the first call does anything.</summary>
    /// <param name="restarting">Said before restarting, if HASI's voice still works.</param>
    /// <param name="givingUp">Said when HASI stops for good; with Windows' own voice if HASI's cannot be used.</param>
    /// <param name="sayWithHasi">Says a message with HASI's voice and waits for it; false if it could not.</param>
    public void RestartOrGiveUp(string reason, string restarting, string givingUp, Func<string, bool>? sayWithHasi)
    {
        if (Interlocked.Exchange(ref _recovering, 1) == 1) return;
        var now = DateTimeOffset.Now;
        var history = ReadHistory();
        if (!CrashPolicy.MayRestart(history, now))
        {
            log.Write("ERROR", $"{reason}: too many restarts, giving up");
            ErrorLog.Write(null, $"{reason}: too many automatic restarts, HASI stops");
            if (sayWithHasi?.Invoke(givingUp) != true) SayWithWindowsVoice(givingUp);
            Kill();
            return;
        }

        log.Write("ERROR", $"{reason}: restarting");
        sayWithHasi?.Invoke(restarting);
        WriteHistory([.. history.Where(t => now - t < CrashPolicy.RestartWindow), now]);
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { Arguments = RestartArguments(), UseShellExecute = false });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            ErrorLog.Write(ex, "automatic restart failed");
            SayWithWindowsVoice(givingUp);
        }
        Kill();
    }

    /// <summary>The same options, minus test triggers, plus <c>--after-error</c> (and <c>--diagnostic</c> if it was on).</summary>
    public string RestartArguments()
    {
        List<string> kept = [];
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i].Equals("--test-error", StringComparison.OrdinalIgnoreCase)) { i++; continue; }
            if (args[i].Equals(AfterErrorFlag, StringComparison.OrdinalIgnoreCase)) continue;
            kept.Add(args[i]);
        }
        if (DiagnosticIsOn() && !kept.Contains("--diagnostic", StringComparer.OrdinalIgnoreCase)) kept.Add("--diagnostic");
        kept.Add(AfterErrorFlag);
        return string.Join(" ", kept.Select(Quote));
    }

    private static string Quote(string arg) =>
        arg.Length > 0 && !arg.Any(c => char.IsWhiteSpace(c) || c == '"') ? arg : $"\"{arg.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    /// <summary>Ends at once: no WER dialog (an unannounced pop-up), no waiting on a frozen window.</summary>
    private static void Kill()
    {
        using var self = Process.GetCurrentProcess();
        self.Kill();
    }

    /// <summary>Last resort, when HASI's own speech is broken or frozen: a Romanian SAPI voice if there is one.</summary>
    private static void SayWithWindowsVoice(string text)
    {
        try
        {
            using var synthesizer = new SpeechSynthesizer();
            var romanian = synthesizer.GetInstalledVoices()
                .FirstOrDefault(v => v.Enabled && v.VoiceInfo.Culture.Name.StartsWith("ro", StringComparison.OrdinalIgnoreCase));
            if (romanian is not null) synthesizer.SelectVoice(romanian.VoiceInfo.Name);
            synthesizer.SetOutputToDefaultAudioDevice();
            synthesizer.Speak(text);
        }
#pragma warning disable CA1031 // Nothing else can be done at this point.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ErrorLog.Write(ex, "last-resort speech failed");
        }
    }

    private static List<DateTimeOffset> ReadHistory()
    {
        try
        {
            if (!File.Exists(HistoryFile)) return [];
            return [.. File.ReadAllLines(HistoryFile)
                .Select(line => DateTimeOffset.TryParse(line, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : (DateTimeOffset?)null)
                .OfType<DateTimeOffset>()];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static void WriteHistory(IEnumerable<DateTimeOffset> times)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HistoryFile)!);
            File.WriteAllLines(HistoryFile, times.Select(t => t.ToString("O", CultureInfo.InvariantCulture)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write(ex, "restart history");
        }
    }
}
