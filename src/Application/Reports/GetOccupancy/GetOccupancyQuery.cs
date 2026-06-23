using EventosVivos.Domain.Common;
using MediatR;

namespace EventosVivos.Application.Reports.GetOccupancy;

public sealed record GetOccupancyQuery(Guid EventId)
    : IRequest<Result<OccupancyResponse>>;
