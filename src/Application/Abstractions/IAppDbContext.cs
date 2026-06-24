using EventosVivos.Domain.Events;
using EventosVivos.Domain.Reservations;
using EventosVivos.Domain.Users;
using EventosVivos.Domain.Venues;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Event> Events { get; }
    DbSet<Reservation> Reservations { get; }
    DbSet<Venue> Venues { get; }
    DbSet<AppUser> Users { get; }
    Task<int> SaveChangesAsync(CancellationToken ct);
    void ResetChangeTracker();
}
