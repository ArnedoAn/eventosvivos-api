using EventosVivos.Domain.ValueObjects;

namespace EventosVivos.Domain.Abstractions;

/// <summary>
/// Contract for checking whether a venue already has an overlapping active event
/// during the requested schedule.
/// </summary>
public interface IVenueScheduleChecker
{
    /// <summary>
    /// Returns <c>true</c> when another active event shares the venue with a
    /// <see cref="DateRange"/> that overlaps the supplied schedule.
    /// </summary>
    /// <param name="venueId">Venue identifier.</param>
    /// <param name="schedule">Requested date/time range.</param>
    /// <param name="excludeEventId">Optional event identifier to exclude from the check.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> HasOverlapAsync(
        int venueId,
        DateRange schedule,
        Guid? excludeEventId,
        CancellationToken ct);
}
