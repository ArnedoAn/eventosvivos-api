using EventosVivos.Application.Abstractions;
using EventosVivos.Domain.Abstractions;
using EventosVivos.Domain.Common;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Events;
using EventosVivos.Domain.Reservations;
using EventosVivos.Domain.Rules;
using EventosVivos.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Application.Reservations.CreateReservation;

public sealed class CreateReservationHandler(
    IAppDbContext db,
    IClock clock,
    IReservationOptions options,
    ReservationRuleSet rules,
    IConcurrencyRetryPolicy retryPolicy)
    : IRequestHandler<CreateReservationCommand, Result<ReservationResponse>>
{
    public async Task<Result<ReservationResponse>> Handle(CreateReservationCommand request, CancellationToken cancellationToken)
    {
        var evt = await db.Events
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);

        if (evt is null)
            return Result.Failure<ReservationResponse>(new Error("event.notFound", "Event not found."));

        var now = clock.UtcNow;
        evt.RefreshStatus(now);

        if (evt.Status != EventStatus.Activo)
            return Result.Failure<ReservationResponse>(new Error("event.notActive", "Event is not active."));

        var emailResult = Email.Create(request.BuyerEmail);
        if (emailResult.IsFailure)
            return Result.Failure<ReservationResponse>(emailResult.Error);

        var reservationRequest = new ReservationRequest(
            request.Quantity,
            evt.Schedule.StartUtc,
            evt.Price.Amount,
            evt.Remaining,
            now);

        var rulesResult = rules.Evaluate(reservationRequest);
        if (rulesResult.IsFailure)
            return Result.Failure<ReservationResponse>(rulesResult.Error);

        var holdResult = evt.HoldOnReserve(request.Quantity, options);
        if (holdResult.IsFailure)
            return Result.Failure<ReservationResponse>(holdResult.Error);

        var reservationResult = Reservation.Create(
            evt.Id,
            request.Quantity,
            request.BuyerName,
            emailResult.Value,
            now);

        if (reservationResult.IsFailure)
            return Result.Failure<ReservationResponse>(reservationResult.Error);

        return await retryPolicy.ExecuteAsync(async () =>
        {
            db.Reservations.Add(reservationResult.Value);
            await db.SaveChangesAsync(cancellationToken);

            var r = reservationResult.Value;
            return Result.Success(new ReservationResponse(
                r.Id,
                r.EventId,
                r.Quantity,
                r.BuyerName,
                r.Email.Value,
                r.Status,
                r.CreatedUtc));
        });
    }
}
