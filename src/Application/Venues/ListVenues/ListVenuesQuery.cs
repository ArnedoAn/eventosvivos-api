using EventosVivos.Domain.Common;
using MediatR;

namespace EventosVivos.Application.Venues.ListVenues;

public sealed record VenueResponse(int Id, string Name, int Capacity, string City);

public sealed record ListVenuesQuery : IRequest<Result<IReadOnlyList<VenueResponse>>>;
