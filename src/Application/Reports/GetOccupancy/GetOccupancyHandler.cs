using EventosVivos.Application.Abstractions;
using EventosVivos.Domain.Abstractions;
using EventosVivos.Domain.Common;
using EventosVivos.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Application.Reports.GetOccupancy;

public sealed class GetOccupancyHandler(IAppDbContext db, IClock clock)
    : IRequestHandler<GetOccupancyQuery, Result<OccupancyResponse>>
{
    public async Task<Result<OccupancyResponse>> Handle(GetOccupancyQuery request, CancellationToken cancellationToken)
    {
        var evt = await db.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);

        if (evt is null)
            return Result.Failure<OccupancyResponse>(new Error("event.notFound", "Event not found."));

        evt.RefreshStatus(clock.UtcNow);

        var soldConfirmed = await db.Reservations
            .AsNoTracking()
            .Where(r => r.EventId == request.EventId && r.Status == ReservationStatus.Confirmada)
            .SumAsync(r => r.Quantity, cancellationToken);

        var retainedByPenalty = evt.SeatsLost;
        var availableRemaining = evt.Capacity - evt.SeatsTaken - evt.SeatsLost;
        var occupancyPercent = soldConfirmed / (double)evt.Capacity * 100;
        var totalRevenue = evt.Price.Amount * soldConfirmed;

        return Result.Success(new OccupancyResponse(
            evt.Id,
            evt.Title,
            evt.Capacity,
            soldConfirmed,
            availableRemaining,
            retainedByPenalty,
            occupancyPercent,
            totalRevenue,
            evt.Status));
    }
}
