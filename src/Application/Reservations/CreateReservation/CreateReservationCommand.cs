using EventosVivos.Domain.Common;
using MediatR;

namespace EventosVivos.Application.Reservations.CreateReservation;

public sealed record CreateReservationCommand(
    Guid EventId,
    Guid UserId,
    int Quantity,
    string BuyerName,
    string BuyerEmail) : IRequest<Result<ReservationResponse>>;
