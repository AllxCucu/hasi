namespace Hasi.Core.Speech;

/// <summary>
/// HASI's own speech settings, the same for every engine: rate and pitch from -10 to 10 (0 = the voice's
/// default), volume from 0 to 100 (100 = full). Each engine maps them to its own ranges.
/// </summary>
public sealed record SpeechSettings(int Rate = 0, int Pitch = 0, int Volume = 100)
{
    public int Rate { get; init; } = Math.Clamp(Rate, -10, 10);

    public int Pitch { get; init; } = Math.Clamp(Pitch, -10, 10);

    public int Volume { get; init; } = Math.Clamp(Volume, 0, 100);

    /// <summary>RHVoice absolute rate/pitch: -1 to 1, linear.</summary>
    public double RhVoiceRate => Rate / 10.0;

    public double RhVoicePitch => Pitch / 10.0;

    /// <summary>Linear gain 0 to 1 for engines whose audio HASI plays itself.</summary>
    public float Gain => Volume / 100f;

    /// <summary>OneCore speaking rate: 0.5 to 6, 1 = normal; each step multiplies by 3^(1/10), as SAPI does.</summary>
    public double OneCoreRate => Math.Clamp(Math.Pow(3, Rate / 10.0), 0.5, 6.0);

    /// <summary>OneCore pitch: 0 to 2, 1 = normal.</summary>
    public double OneCorePitch => 1 + Pitch / 10.0;
}

/// <summary>A timing measurement from an engine, for the diagnostic log and ADR 0004.</summary>
/// <param name="Kind">"first-sound", "stop", "word" or "finished".</param>
public sealed record SpeechMetric(string Engine, string Kind, double Milliseconds, string? Detail = null);

/// <summary>Engines that can report timings implement this in addition to <see cref="ISpeechEngine"/>.</summary>
public interface ISpeechMetrics
{
    event EventHandler<SpeechMetric>? MetricReported;
}

/// <summary>Engines whose rate, pitch and volume can be changed while HASI runs.</summary>
public interface ISpeechSettingsTarget
{
    void Apply(SpeechSettings settings);
}
