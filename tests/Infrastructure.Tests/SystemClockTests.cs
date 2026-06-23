using EventosVivos.Domain.Abstractions;
using EventosVivos.Infrastructure;
using FluentAssertions;

namespace EventosVivos.Infrastructure.Tests;

public class SystemClockTests
{
    [Fact]
    public void UtcNow_returns_current_utc_time()
    {
        IClock clock = new SystemClock();

        var before = DateTime.UtcNow;
        var now = clock.UtcNow;
        var after = DateTime.UtcNow;

        now.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }
}
