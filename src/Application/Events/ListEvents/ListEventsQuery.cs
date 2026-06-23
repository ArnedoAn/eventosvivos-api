using EventosVivos.Application.Events.CreateEvent;
using EventosVivos.Domain.Common;
using EventosVivos.Domain.Enums;
using MediatR;

namespace EventosVivos.Application.Events.ListEvents;

public sealed record ListEventsQuery(
    EventType? Type = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int? VenueId = null,
    EventStatus? Status = null,
    string? TitleContains = null)
    : IRequest<Result<IReadOnlyList<EventResponse>>>;
