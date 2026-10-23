using System.Diagnostics;
using System.IO;
using Hasi.Core.Diagnostics;
using Hasi.Core.Localization;
using Hasi.Core.Output;
using Hasi.Core.Speech;
using Hasi.Speech;
using Hasi.Speech.Audio;
using Hasi.Speech.RhVoice;

namespace Hasi.App;

/// <summary>
/// Builds the voices: SAPI5 (default), OneCore, and RHVoice.dll directly when <c>--rhvoice-path</c> is given
/// (ADR 0004 experiment). Logs every engine's timings and the process load for the comparison.
/// </summary>
internal sealed class SpeechSetup
{
    /// <summary>RHVoice voice profile used by the experiment.</summary>
    private const string RhVoiceProfile = "Paul";

    private readonly StartupOptions _options;
    private readonly IDiagnosticLog _log;
    private readonly SpeechSettings _settings;
    private readonly PerformanceSampler _performance = new();

    public SpeechSetup(StartupOptions options, IDiagnosticLog log)
    {
        _options = options;
        _log = log;
        _settings = new SpeechSettings(options.Rate ?? 0, options.Pitch ?? 0, options.Volume ?? 100);
        // RHVoice.dll is the default engine (ADR 0004). --rhvoice-path overrides the default locations.
        string[] folders = options.RhVoicePath is { } path ? [path] : DefaultRhVoiceFolders;
        foreach (var folder in folders)
        {
            RhVoice = RhVoiceInstallation.Find(folder, out var problem);
            _log.Write("RHVOICE", RhVoice is null
                ? $"{folder}: not usable: {problem} (HASI is {RhVoiceInstallation.ProcessArchitecture})"
                : $"dll={RhVoice.DllPath} arch={RhVoice.Architecture} (HASI is {RhVoiceInstallation.ProcessArchitecture}) " +
                  $"data={string.Join("; ", RhVoice.ResourcePaths)}");
            if (RhVoice is not null) break;
        }
    }

    /// <summary>
    /// Where HASI looks for RHVoice.dll and the voice data when no --rhvoice-path is given: next to the
    /// executable (the future installer, phase 0.11), then the user's HASI folder (the tester's copy).
    /// </summary>
    public static string[] DefaultRhVoiceFolders =>
        [Path.Combine(AppContext.BaseDirectory, "rhvoice"), Path.Combine(AppPaths.LocalData, "rhvoice")];

    /// <summary>The engine tried first: --engine, else RHVoice.dll.</summary>
    private string PreferredEngine => _options.Engine ?? RhVoiceDllEngine.EngineId;

    public RhVoiceInstallation? RhVoice { get; private set; }

    /// <summary>
    /// Paul through RHVoice.dll (default, ADR 0004); then Paul through SAPI5; then a Romanian voice from
    /// SAPI5 or OneCore (Microsoft Andrei); then any voice. <c>--engine</c> moves that engine's voices to
    /// the front; the others stay in the fallback chain. A fallback at startup is said aloud.
    /// </summary>
    public (FallbackSpeechEngine Engine, string? Notice) CreateMainEngine(ILocalizer localizer)
    {
        var voices = ListVoices();
        var preferred = _options.Voice ?? VoiceChooser.DefaultPreferredVoice;
        var ranked = VoiceChooser.Rank(voices, preferred)
            .OrderBy(v => v.Engine.Equals(PreferredEngine, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .Select((voice, index) => new SpeechCandidate(voice.Name, () =>
            {
                var engine = StartVoice(voice);
                // --test-voice-failure: the best voice breaks after three messages, to hear the fallback.
                return engine is not null && index == 0 && _options.TestVoiceFailure ? new FailingAfterEngine(engine, 3) : engine;
            }));

        var chain = FallbackSpeechEngine.Start(ranked, (voice, ex) =>
        {
            ErrorLog.Write(ex);
            _log.Write("ERROR", $"voice \"{voice}\" did not start: {ex.Message}");
        });
        if (chain.CurrentVoiceName is not { } current)
        {
            _log.Write("ERROR", "no working voice; text is shown on screen only");
            return (chain, localizer.Get("speech.noVoice"));
        }
        string? notice = null;
        if (_options.Engine is { } wanted && !chain.Id.Equals(wanted, StringComparison.OrdinalIgnoreCase))
        {
            // The tester asked for an engine that did not start: say so, otherwise the comparison is wrong.
            notice = localizer.Get("speech.engineUnavailable", ("voice", current));
        }
        else if (_options.Voice is null && _options.Engine is null && !current.Contains(preferred, StringComparison.OrdinalIgnoreCase))
        {
            notice = localizer.Get("speech.voiceFallback", ("voice", current));
        }
        return (chain, notice);
    }

    /// <summary>Paul through SAPI and Paul through RHVoice.dll, each with its own engine; null if either is missing.</summary>
    public EngineVoiceComparison? CreateComparison(ILocalizer localizer, ITextNormalizer normalizer)
    {
        if (RhVoice is null) return null;
        var sapiPaul = ListSapiVoices().FirstOrDefault(v => v.Name.Contains(RhVoiceProfile, StringComparison.OrdinalIgnoreCase));
        if (sapiPaul is null)
        {
            _log.Write("COMPARE", "unavailable: Paul is not installed for SAPI5");
            return null;
        }
        var sapi = StartVoice(sapiPaul);
        var direct = StartVoice(RhVoiceCandidate());
        if (sapi is null || direct is null)
        {
            sapi?.Dispose();
            direct?.Dispose();
            return null;
        }
        return new EngineVoiceComparison(
            [(localizer.Get("compare.variant.sapi"), sapi), (localizer.Get("compare.variant.dll"), direct)],
            normalizer, localizer, _log);
    }

    private List<VoiceCandidate> ListVoices()
    {
        var voices = new List<VoiceCandidate>();
        voices.AddRange(ListSapiVoices());
        if (RhVoice is not null) voices.Add(RhVoiceCandidate());
        voices.AddRange(Attempt("OneCore voices", () => OneCoreEngine.Voices) ?? []);
        _log.Write("VOICES", string.Join(", ", voices.Select(v => $"{v.Engine}:\"{v.Name}\" {v.Culture}")));
        return voices;
    }

    private IReadOnlyList<VoiceCandidate> ListSapiVoices() => Attempt("SAPI5 voices", () =>
    {
        using var lister = new Sapi5Engine();
        return lister.Voices;
    }) ?? [];

    private static VoiceCandidate RhVoiceCandidate() => new($"{RhVoiceProfile} (RHVoice direct)", "ro-RO", RhVoiceDllEngine.EngineId);

    /// <summary>Each voice gets its own engine, so a failing one can be thrown away without affecting the next.</summary>
    private ISpeechEngine? StartVoice(VoiceCandidate voice)
    {
        ISpeechEngine? engine;
        switch (voice.Engine)
        {
            case Sapi5Engine.EngineId:
                var sapi = new Sapi5Engine();
                sapi.SelectVoice(voice.Name);
                sapi.Settings = _settings;
                engine = sapi;
                break;
            case "onecore":
                var oneCore = new OneCoreEngine();
                oneCore.SelectVoice(voice.Name);
                oneCore.Settings = _settings;
                engine = oneCore;
                break;
            case RhVoiceDllEngine.EngineId when RhVoice is not null:
                engine = StartRhVoice(RhVoice);
                break;
            default:
                return null;
        }
        Observe(engine);
        return engine;
    }

    private RhVoiceDllEngine StartRhVoice(RhVoiceInstallation installation)
    {
        var configFolder = Path.Combine(AppPaths.LocalData, "rhvoice-config");
        var library = new RhVoiceLibrary(installation.DllPath, installation.ResourcePaths, configFolder);
        if (!library.Voices.Contains(RhVoiceProfile, StringComparer.OrdinalIgnoreCase))
        {
            library.Dispose();
            throw new InvalidOperationException($"RHVoice {library.Version} has no voice {RhVoiceProfile}; voices: {string.Join(", ", library.Voices)}");
        }
        SpeechAudioOutput output;
        try
        {
            output = SpeechAudioOutput.Open(sampleRate: 24000);
        }
        catch
        {
            library.Dispose();
            throw;
        }
        _log.Write("RHVOICE", $"version {library.Version}, voices {string.Join(", ", library.Voices)}; audio: {output.DeviceDescription}");
        var engine = new RhVoiceDllEngine(library, output, RhVoiceProfile) { Settings = _settings };
        engine.WordStarted += (_, word) => _log.Write("WORD", $"engine={RhVoiceDllEngine.EngineId} {word.Position}+{word.Length} \"{word.Word}\"");
        return engine;
    }

    /// <summary>Logs timings (first sound, stop) and, after each message, the CPU and memory use.</summary>
    private void Observe(ISpeechEngine engine)
    {
        if (engine is not ISpeechMetrics metrics) return;
        metrics.MetricReported += (_, metric) =>
        {
            switch (metric.Kind)
            {
                case "word":
                    _log.Write("WORD", $"engine={metric.Engine} {metric.Detail}");
                    break;
                case "finished":
                    _log.Write("LATENCY", $"engine={metric.Engine} finished after {metric.Milliseconds:F0} ms");
                    _log.Write("PERF", $"engine={metric.Engine} {_performance.Sample()}");
                    break;
                default:
                    _log.Write("LATENCY", $"engine={metric.Engine} {metric.Kind}={metric.Milliseconds:F1} ms{(metric.Detail is null ? "" : $" ({metric.Detail})")}");
                    break;
            }
        };
    }

    private T? Attempt<T>(string what, Func<T?> action) where T : class
    {
        try
        {
            return action();
        }
#pragma warning disable CA1031 // Without speech HASI still shows every message on screen.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ErrorLog.Write(ex);
            _log.Write("ERROR", $"{what} unavailable: {ex.Message}");
            return null;
        }
    }
}

/// <summary>Process CPU (average since the previous sample) and memory, for the PERF lines of the diagnostic log.</summary>
internal sealed class PerformanceSampler
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly Stopwatch _wall = Stopwatch.StartNew();
    private TimeSpan _lastCpu;
    private TimeSpan _lastWall;

    public string Sample()
    {
        _process.Refresh();
        var cpu = _process.TotalProcessorTime;
        var wall = _wall.Elapsed;
        var load = (cpu - _lastCpu).TotalMilliseconds / Math.Max(1, (wall - _lastWall).TotalMilliseconds) / Environment.ProcessorCount * 100;
        _lastCpu = cpu;
        _lastWall = wall;
        return $"cpu={load:F1}% (of all {Environment.ProcessorCount} cores) working-set={_process.WorkingSet64 / 1048576.0:F0} MB " +
               $"private={_process.PrivateMemorySize64 / 1048576.0:F0} MB";
    }
}
