using EventosVivos.Domain.Abstractions;

namespace EventosVivos.Infrastructure.Options;

public sealed class ReservationOptions : IReservationOptions
{
    public bool PendingHoldsInventory { get; set; } = true;
    public int PendingExpirationMinutes { get; set; } = 0;
}
