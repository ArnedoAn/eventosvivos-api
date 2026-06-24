using EventosVivos.Application.Abstractions;
using EventosVivos.Domain.Abstractions;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Reservations;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Infrastructure.Persistence;

public sealed class ReservationExpirer : IReservationExpirer
{
    private readonly IAppDbContext _db;
    private readonly IReservationOptions _options;

    public ReservationExpirer(IAppDbContext db, IReservationOptions options)
    {
        _db = db;
        _options = options;
    }

    public async Task ExpireOverduePendingReservationsAsync(Guid eventId, DateTime nowUtc, CancellationToken ct = default)
    {
        if (!_options.PendingHoldsInventory || _options.PendingExpirationMinutes <= 0)
            return;

        var cutoff = nowUtc.AddMinutes(-_options.PendingExpirationMinutes);
        var pending = await _db.Reservations
            .Where(r => r.EventId == eventId && r.Status == ReservationStatus.PendientePago && r.CreatedUtc <= cutoff)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return;

        var evt = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (evt is null)
            return;

        foreach (var reservation in pending)
        {
            var expireResult = reservation.Expire(nowUtc);
            if (expireResult.IsFailure)
                continue;

            var releaseResult = evt.ReleasePendingHold(reservation.Quantity);
            if (releaseResult.IsFailure)
                continue;
        }

        await _db.SaveChangesAsync(ct);
    }
}
