using EventosVivos.Application.Reservations.CreateReservation;
using EventosVivos.Domain.Common;
using MediatR;

namespace EventosVivos.Application.Reservations.CancelReservation;

public sealed record CancelReservationCommand(
    Guid ReservationId,
    Guid UserId,
    bool IsAdmin = false)
    : IRequest<Result<ReservationResponse>>;
