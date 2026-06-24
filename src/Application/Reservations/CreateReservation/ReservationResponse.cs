using EventosVivos.Domain.Enums;

namespace EventosVivos.Application.Reservations.CreateReservation;

public sealed record ReservationResponse(
    Guid Id,
    Guid EventId,
    Guid UserId,
    int Quantity,
    string BuyerName,
    string BuyerEmail,
    ReservationStatus Status,
    DateTime CreatedUtc);
