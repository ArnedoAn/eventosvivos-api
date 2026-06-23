using EventosVivos.Domain.Enums;

namespace EventosVivos.Application.Events.CreateEvent;

public sealed record EventResponse(
    Guid Id,
    string Title,
    string Description,
    int VenueId,
    int Capacity,
    DateTime StartUtc,
    DateTime EndUtc,
    decimal Price,
    EventType Type,
    EventStatus Status);
