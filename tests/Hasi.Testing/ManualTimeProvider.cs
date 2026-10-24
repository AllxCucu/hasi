namespace Hasi.Testing;

/// <summary>A clock tests can set and advance without waiting for real time.</summary>
public sealed class ManualTimeProvider(DateTimeOffset instant, TimeZoneInfo timeZone) : TimeProvider
{
    private DateTimeOffset _utcNow = instant.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone { get; } = timeZone;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void SetUtcNow(DateTimeOffset instant) => _utcNow = instant.ToUniversalTime();

    public void Advance(TimeSpan elapsed) => _utcNow += elapsed;
}
