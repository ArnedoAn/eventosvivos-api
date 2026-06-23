using EventosVivos.Application.Abstractions;
using EventosVivos.Application.Events.CreateEvent;
using EventosVivos.Domain.Abstractions;
using EventosVivos.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Application.Events.ListEvents;

public sealed class ListEventsHandler(IAppDbContext db, IClock clock)
    : IRequestHandler<ListEventsQuery, Result<IReadOnlyList<EventResponse>>>
{
    public async Task<Result<IReadOnlyList<EventResponse>>> Handle(ListEventsQuery request, CancellationToken cancellationToken)
    {
        var events = await db.Events
            .AsNoTracking()
            .ApplyFilters(request)
            .ToListAsync(cancellationToken);

        var now = clock.UtcNow;
        foreach (var e in events)
            e.RefreshStatus(now);

        if (request.Status.HasValue)
            events = events.Where(e => e.Status == request.Status.Value).ToList();

        var responses = events.Select(e => new EventResponse(
            e.Id,
            e.Title,
            e.Description,
            e.VenueId,
            e.Capacity,
            e.Schedule.StartUtc,
            e.Schedule.EndUtc,
            e.Price.Amount,
            e.Type,
            e.Status)).ToList();

        return Result.Success<IReadOnlyList<EventResponse>>(responses);
    }
}
