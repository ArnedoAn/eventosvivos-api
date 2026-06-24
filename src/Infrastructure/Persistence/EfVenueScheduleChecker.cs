using EventosVivos.Application.Abstractions;
using EventosVivos.Domain.Abstractions;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Infrastructure.Persistence;

public sealed class EfVenueScheduleChecker(IAppDbContext db) : IVenueScheduleChecker
{
    private readonly IAppDbContext _db = db;

    public Task<bool> HasOverlapAsync(
        int venueId,
        DateRange schedule,
        Guid? excludeEventId,
        CancellationToken ct)
    {
        return _db.Events
            .AsNoTracking()
            .Where(e => e.VenueId == venueId)
            .Where(e => e.Status == EventStatus.Activo)
            .Where(e => e.Schedule.StartUtc < schedule.EndUtc && schedule.StartUtc < e.Schedule.EndUtc)
            .Where(e => excludeEventId == null || e.Id != excludeEventId.Value)
            .AnyAsync(ct);
    }
}
