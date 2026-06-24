using EventosVivos.Application.Abstractions;
using EventosVivos.Domain.Abstractions;
using EventosVivos.Domain.Events;
using EventosVivos.Domain.Reservations;
using EventosVivos.Domain.Users;
using EventosVivos.Domain.Venues;
using EventosVivos.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext, IAppDbContext
{
    private readonly IClock? _clock;

    public AppDbContext(DbContextOptions<AppDbContext> options, IClock? clock = null)
        : base(options)
    {
        _clock = clock;
    }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<AppUser> Users => Set<AppUser>();

    Task<int> IAppDbContext.SaveChangesAsync(CancellationToken ct) => base.SaveChangesAsync(ct);

    public void ResetChangeTracker() => ChangeTracker.Clear();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        SeedData.Seed(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }
}
