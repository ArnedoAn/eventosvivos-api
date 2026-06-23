using EventosVivos.Domain.ValueObjects;
using FluentAssertions;

namespace EventosVivos.Domain.Tests.ValueObjects;

public class DateRangeTests
{
    [Fact]
    public void DateRange_rejects_end_before_start() =>
        DateRange.Create(new DateTime(2030, 1, 2), new DateTime(2030, 1, 1)).IsFailure.Should().BeTrue();

    [Fact]
    public void DateRange_rejects_equal_start_and_end() =>
        DateRange.Create(new DateTime(2030, 1, 1, 10, 0, 0), new DateTime(2030, 1, 1, 10, 0, 0)).IsFailure.Should().BeTrue();

    [Fact]
    public void DateRange_overlap_is_detected()
    {
        var a = DateRange.Create(new(2030, 1, 1, 10, 0, 0), new(2030, 1, 1, 12, 0, 0)).Value;
        var b = DateRange.Create(new(2030, 1, 1, 11, 0, 0), new(2030, 1, 1, 13, 0, 0)).Value;
        a.Overlaps(b).Should().BeTrue();
    }

    [Fact]
    public void DateRange_non_overlapping_ranges_do_not_overlap()
    {
        var a = DateRange.Create(new(2030, 1, 1, 10, 0, 0), new(2030, 1, 1, 12, 0, 0)).Value;
        var b = DateRange.Create(new(2030, 1, 1, 12, 0, 0), new(2030, 1, 1, 14, 0, 0)).Value;
        a.Overlaps(b).Should().BeFalse();
    }
}
