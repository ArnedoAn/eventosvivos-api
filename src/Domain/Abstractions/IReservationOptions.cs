namespace EventosVivos.Domain.Abstractions;

public interface IReservationOptions
{
    bool PendingHoldsInventory { get; }
    int PendingExpirationMinutes { get; }
}
