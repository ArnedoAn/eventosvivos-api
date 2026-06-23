using EventosVivos.Application.Reservations.CreateReservation;
using EventosVivos.Domain.Common;
using MediatR;

namespace EventosVivos.Application.Reservations.ConfirmReservation;

public sealed record ConfirmReservationCommand(Guid ReservationId)
    : IRequest<Result<ReservationResponse>>;
