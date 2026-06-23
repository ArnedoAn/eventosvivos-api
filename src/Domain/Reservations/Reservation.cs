using EventosVivos.Domain.Common;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.ValueObjects;

namespace EventosVivos.Domain.Reservations;

public sealed class Reservation
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public int Quantity { get; private set; }
    public string BuyerName { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public ReservationStatus Status { get; private set; }
    public ReservationCode? Code { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime? CancelledUtc { get; private set; }
    public bool IsLost { get; private set; }

    private Reservation()
    {
    }

    public static Result<Reservation> Create(
        Guid eventId,
        int qty,
        string buyerName,
        Email email,
        DateTime nowUtc)
    {
        if (qty <= 0)
            return Result.Failure<Reservation>(new Error("reservation.quantity.invalid", "Quantity must be greater than zero."));

        if (string.IsNullOrWhiteSpace(buyerName))
            return Result.Failure<Reservation>(new Error("reservation.buyerName.required", "Buyer name is required."));

        if (email is null)
            return Result.Failure<Reservation>(new Error("reservation.email.required", "Email is required."));

        return new Reservation
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Quantity = qty,
            BuyerName = buyerName.Trim(),
            Email = email,
            Status = ReservationStatus.PendientePago,
            CreatedUtc = nowUtc
        };
    }

    public Result Confirm(ReservationCode code)
    {
        if (Status == ReservationStatus.Confirmada)
            return Result.Failure(new Error("reservation.alreadyConfirmed", "Reservation is already confirmed."));

        if (Status == ReservationStatus.Cancelada)
            return Result.Failure(new Error("reservation.cancelled", "Reservation is cancelled."));

        Code = code;
        Status = ReservationStatus.Confirmada;
        return Result.Success();
    }

    public Result<bool> Cancel(DateTime nowUtc, DateTime eventStartUtc)
    {
        if (Status == ReservationStatus.Cancelada)
            return Result.Failure<bool>(new Error("reservation.cancelled", "Reservation is cancelled."));

        if (Status != ReservationStatus.Confirmada)
            return Result.Failure<bool>(new Error("reservation.notConfirmed", "Only confirmed reservations can be cancelled."));

        var hoursBeforeEvent = (eventStartUtc - nowUtc).TotalHours;
        var penalty = hoursBeforeEvent < 48;

        Status = ReservationStatus.Cancelada;
        CancelledUtc = nowUtc;
        IsLost = penalty;

        return Result.Success(penalty);
    }
}
