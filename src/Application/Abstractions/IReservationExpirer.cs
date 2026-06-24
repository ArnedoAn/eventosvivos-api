namespace EventosVivos.Application.Abstractions;

public interface IReservationExpirer
{
    Task ExpireOverduePendingReservationsAsync(Guid eventId, DateTime nowUtc, CancellationToken ct = default);
}
