using EventosVivos.Domain.Common;
using EventosVivos.Domain.Enums;
using MediatR;

namespace EventosVivos.Application.Events.CreateEvent;

public sealed record CreateEventCommand(
    string Title,
    string Description,
    int VenueId,
    int Capacity,
    DateTime StartUtc,
    DateTime EndUtc,
    decimal Price,
    EventType Type) : IRequest<Result<EventResponse>>;
