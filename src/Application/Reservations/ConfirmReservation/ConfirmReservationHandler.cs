using EventosVivos.Application.Abstractions;
using EventosVivos.Application.Reservations.CreateReservation;
using EventosVivos.Domain.Abstractions;
using EventosVivos.Domain.Common;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Events;
using EventosVivos.Domain.Reservations;
using EventosVivos.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Application.Reservations.ConfirmReservation;

public sealed class ConfirmReservationHandler
    : IRequestHandler<ConfirmReservationCommand, Result<ReservationResponse>>
{
    private readonly IAppDbContext _db;
    private readonly IReservationOptions _options;
    private readonly IConcurrencyRetryPolicy _retryPolicy;
    private readonly Func<int> _randomSixDigits;

    public ConfirmReservationHandler(
        IAppDbContext db,
        IReservationOptions options,
        IConcurrencyRetryPolicy retryPolicy,
        Func<int>? randomSixDigits = null)
    {
        _db = db;
        _options = options;
        _retryPolicy = retryPolicy;
        _randomSixDigits = randomSixDigits ?? (() => Random.Shared.Next(0, 1_000_000));
    }

    public async Task<Result<ReservationResponse>> Handle(ConfirmReservationCommand request, CancellationToken cancellationToken)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var reservation = await _db.Reservations
                .FirstOrDefaultAsync(r => r.Id == request.ReservationId, cancellationToken);

            if (reservation is null)
                return Result.Failure<ReservationResponse>(new Error("reservation.notFound", "Reservation not found."));

            var evt = await _db.Events
                .FirstOrDefaultAsync(e => e.Id == reservation.EventId, cancellationToken);

            if (evt is null)
                return Result.Failure<ReservationResponse>(new Error("event.notFound", "Event not found."));

            var code = await GenerateUniqueCodeAsync(reservation.Id, cancellationToken);
            if (code is null)
                return Result.Failure<ReservationResponse>(new Error("reservation.codeCollision", "Could not generate a unique reservation code."));

            var confirmResult = reservation.Confirm(code);
            if (confirmResult.IsFailure)
                return Result.Failure<ReservationResponse>(confirmResult.Error);

            var consumeResult = evt.ConsumeOnConfirm(reservation.Quantity, _options);
            if (consumeResult.IsFailure)
                return Result.Failure<ReservationResponse>(consumeResult.Error);

            await _db.SaveChangesAsync(cancellationToken);

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

    private async Task<ReservationCode?> GenerateUniqueCodeAsync(Guid currentReservationId, CancellationToken cancellationToken)
    {
        const int maxAttempts = 10;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var code = ReservationCode.New(_randomSixDigits);
            var exists = await _db.Reservations
                .AnyAsync(r =>
                    r.Id != currentReservationId &&
                    r.Status == ReservationStatus.Confirmada &&
                    r.Code.Value == code.Value, cancellationToken);

            if (!exists)
                return code;
        }

        return null;
    }
}
