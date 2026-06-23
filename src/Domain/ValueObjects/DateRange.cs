using EventosVivos.Domain.Common;

namespace EventosVivos.Domain.ValueObjects;

public sealed record DateRange(DateTime StartUtc, DateTime EndUtc)
{
    public static Result<DateRange> Create(DateTime startUtc, DateTime endUtc)
    {
        if (endUtc <= startUtc)
            return Result.Failure<DateRange>(new Error("DateRange.InvalidRange", "End must be after start."));

        return new DateRange(startUtc, endUtc);
    }

    public bool Overlaps(DateRange other) =>
        StartUtc < other.EndUtc && other.StartUtc < EndUtc;
}
