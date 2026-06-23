using EventosVivos.Application.Abstractions;
using EventosVivos.Application.Reservations.CreateReservation;
using EventosVivos.Domain.Abstractions;
using EventosVivos.Domain.Common;
using EventosVivos.Domain.Events;
using EventosVivos.Domain.Reservations;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Application.Reservations.CancelReservation;

public sealed class CancelReservationHandler(
    IAppDbContext db,
    IClock clock,
    IConcurrencyRetryPolicy retryPolicy)
    : IRequestHandler<CancelReservationCommand, Result<ReservationResponse>>
{
    public async Task<Result<ReservationResponse>> Handle(CancelReservationCommand request, CancellationToken cancellationToken)
    {
        return await retryPolicy.ExecuteAsync(async () =>
        {
            var reservation = await db.Reservations
                .FirstOrDefaultAsync(r => r.Id == request.ReservationId, cancellationToken);

            if (reservation is null)
                return Result.Failure<ReservationResponse>(new Error("reservation.notFound", "Reservation not found."));

            var evt = await db.Events
                .FirstOrDefaultAsync(e => e.Id == reservation.EventId, cancellationToken);

            if (evt is null)
                return Result.Failure<ReservationResponse>(new Error("event.notFound", "Event not found."));

            var cancelResult = reservation.Cancel(clock.UtcNow, evt.Schedule.StartUtc);
            if (cancelResult.IsFailure)
                return Result.Failure<ReservationResponse>(cancelResult.Error);

            evt.ReleaseOnCancel(reservation.Quantity, cancelResult.Value);

            await db.SaveChangesAsync(cancellationToken);

            return Result.Success(new ReservationResponse(
                reservation.Id,
                reservation.EventId,
                reservation.Quantity,
                reservation.BuyerName,
                reservation.Email.Value,
                reservation.Status,
                reservation.CreatedUtc));
        });
    }
}
