using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Application.Events.ListEvents;

public static class EventFilter
{
    public static IQueryable<Event> ApplyFilters(this IQueryable<Event> query, ListEventsQuery request)
    {
        if (request.Type.HasValue)
            query = query.Where(e => e.Type == request.Type.Value);

        if (request.VenueId.HasValue)
            query = query.Where(e => e.VenueId == request.VenueId.Value);

        if (request.FromUtc.HasValue)
            query = query.Where(e => e.Schedule.StartUtc >= request.FromUtc.Value);

        if (request.ToUtc.HasValue)
            query = query.Where(e => e.Schedule.StartUtc <= request.ToUtc.Value);

        if (!string.IsNullOrWhiteSpace(request.TitleContains))
        {
            var term = request.TitleContains.ToLower();
            query = query.Where(e => e.Title.ToLower().Contains(term));
        }

        return query;
    }
}
