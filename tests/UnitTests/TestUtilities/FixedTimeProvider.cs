namespace Kart.Search.UnitTests.TestUtilities;

/// <summary>A deterministic <see cref="TimeProvider"/> test double - avoids a flaky assertion
/// against the real system clock.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
