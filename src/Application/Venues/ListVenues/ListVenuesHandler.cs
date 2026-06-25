using EventosVivos.Application.Abstractions;
using EventosVivos.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Application.Venues.ListVenues;

public sealed class ListVenuesHandler(IAppDbContext db)
    : IRequestHandler<ListVenuesQuery, Result<IReadOnlyList<VenueResponse>>>
{
    public async Task<Result<IReadOnlyList<VenueResponse>>> Handle(
        ListVenuesQuery request,
        CancellationToken cancellationToken)
    {
        var venues = await db.Venues
            .AsNoTracking()
            .OrderBy(v => v.Id)
            .Select(v => new VenueResponse(v.Id, v.Name, v.Capacity, v.City))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<VenueResponse>>(venues);
    }
}
