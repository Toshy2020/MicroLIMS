namespace MicroLIMS.Tests.Fixtures;

// A clock tests control. KPI tests must never read the wall clock.
public sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now) => _now = now;

    public void SetUtcNow(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}
