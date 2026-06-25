using EventosVivos.Application.Abstractions;
using EventosVivos.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Application.Reservations.ListReservations;

public sealed class ListReservationsHandler(IAppDbContext db)
    : IRequestHandler<ListReservationsQuery, Result<IReadOnlyList<ReservationListItem>>>
{
    public async Task<Result<IReadOnlyList<ReservationListItem>>> Handle(
        ListReservationsQuery request,
        CancellationToken cancellationToken)
    {
        var query =
            from r in db.Reservations
            join e in db.Events on r.EventId equals e.Id
            select new { r, EventTitle = e.Title };

        if (request.EventId.HasValue)
            query = query.Where(x => x.r.EventId == request.EventId.Value);

        if (request.Status.HasValue)
            query = query.Where(x => x.r.Status == request.Status.Value);

        var items = await query
            .OrderByDescending(x => x.r.CreatedUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new ReservationListItem(
                x.r.Id,
                x.r.EventId,
                x.EventTitle,
                x.r.UserId,
                x.r.Quantity,
                x.r.BuyerName,
                x.r.Email.Value,
                x.r.Status,
                x.r.CreatedUtc))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<ReservationListItem>>(items);
    }
}
