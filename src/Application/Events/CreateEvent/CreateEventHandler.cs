using EventosVivos.Application.Abstractions;
using EventosVivos.Domain.Abstractions;
using EventosVivos.Domain.Common;
using EventosVivos.Domain.Events;
using EventosVivos.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Application.Events.CreateEvent;

public sealed class CreateEventHandler(
    IAppDbContext db,
    IVenueScheduleChecker scheduleChecker,
    IClock clock)
    : IRequestHandler<CreateEventCommand, Result<EventResponse>>
{
    public async Task<Result<EventResponse>> Handle(CreateEventCommand request, CancellationToken cancellationToken)
    {
        var venue = await db.Venues.FirstOrDefaultAsync(v => v.Id == request.VenueId, cancellationToken);
        if (venue is null)
            return Result.Failure<EventResponse>(new Error("venue.notFound", "Venue not found."));

        var scheduleResult = DateRange.Create(request.StartUtc, request.EndUtc);
        if (scheduleResult.IsFailure)
            return Result.Failure<EventResponse>(scheduleResult.Error);

        var priceResult = Money.Create(request.Price);
        if (priceResult.IsFailure)
            return Result.Failure<EventResponse>(priceResult.Error);

        var hasOverlap = await scheduleChecker.HasOverlapAsync(
            request.VenueId,
            scheduleResult.Value,
            excludeEventId: null,
            cancellationToken);

        if (hasOverlap)
            return Result.Failure<EventResponse>(new Error("event.venueOverlap", "Venue schedule overlaps with another event."));

        var eventResult = Event.Create(
            request.Title,
            request.Description,
            request.VenueId,
            venue.Capacity,
            request.Capacity,
            scheduleResult.Value,
            priceResult.Value,
            request.Type,
            clock);

        if (eventResult.IsFailure)
            return Result.Failure<EventResponse>(eventResult.Error);

        db.Events.Add(eventResult.Value);
        await db.SaveChangesAsync(cancellationToken);

        var e = eventResult.Value;
        return Result.Success(new EventResponse(
            e.Id,
            e.Title,
            e.Description,
            e.VenueId,
            e.Capacity,
            e.Schedule.StartUtc,
            e.Schedule.EndUtc,
            e.Price.Amount,
            e.Type,
            e.Status));
    }
}
