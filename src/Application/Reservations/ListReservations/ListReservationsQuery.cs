using EventosVivos.Domain.Common;
using EventosVivos.Domain.Enums;
using MediatR;

namespace EventosVivos.Application.Reservations.ListReservations;

public sealed record ReservationListItem(
    Guid Id,
    Guid EventId,
    string EventName,
    Guid UserId,
    int Quantity,
    string BuyerName,
    string BuyerEmail,
    ReservationStatus Status,
    DateTime CreatedUtc);

public sealed record ListReservationsQuery(
    Guid? EventId = null,
    ReservationStatus? Status = null,
    int Page = 1,
    int PageSize = 50)
    : IRequest<Result<IReadOnlyList<ReservationListItem>>>;
