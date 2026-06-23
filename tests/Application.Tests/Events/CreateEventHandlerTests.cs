using EventosVivos.Application.Abstractions;
using EventosVivos.Application.Events.CreateEvent;
using EventosVivos.Domain.Abstractions;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Events;
using EventosVivos.Domain.Reservations;
using EventosVivos.Domain.Users;
using EventosVivos.Domain.ValueObjects;
using EventosVivos.Domain.Venues;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Application.Tests.Events;

public class CreateEventHandlerTests
{
    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }

    private sealed class FakeVenueScheduleChecker(bool hasOverlap) : IVenueScheduleChecker
    {
        public Task<bool> HasOverlapAsync(int venueId, DateRange schedule, Guid? excludeEventId, CancellationToken ct)
            => Task.FromResult(hasOverlap);
    }

    private sealed class TestAppDbContext : DbContext, IAppDbContext
    {
        public TestAppDbContext(DbContextOptions<TestAppDbContext> options) : base(options) { }

        public DbSet<Event> Events => Set<Event>();
        public DbSet<Reservation> Reservations => Set<Reservation>();
        public DbSet<Venue> Venues => Set<Venue>();
        public DbSet<AppUser> Users => Set<AppUser>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Event>(e =>
            {
                e.HasKey(x => x.Id);
                e.OwnsOne(x => x.Schedule);
                e.OwnsOne(x => x.Price);
            });

            modelBuilder.Entity<Venue>(e => e.HasKey(x => x.Id));
            modelBuilder.Entity<AppUser>(e => e.HasKey(x => x.Id));
            modelBuilder.Entity<Reservation>(e =>
            {
                e.HasKey(x => x.Id);
                e.OwnsOne(x => x.Email);
                e.OwnsOne(x => x.Code);
            });
        }
    }

    private static CreateEventCommand ValidCommand() => new(
        Title: "Rock Concert",
        Description: "A great live rock show in the city.",
        VenueId: 1,
        Capacity: 100,
        StartUtc: new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc),
        EndUtc: new DateTime(2030, 6, 15, 22, 0, 0, DateTimeKind.Utc),
        Price: 50m,
        Type: EventType.Concierto);

    private static TestAppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new TestAppDbContext(options);
        context.Venues.Add(new Venue(1, "Auditorio Central", 200, "Bogotá"));
        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task Valid_command_creates_event()
    {
        var clock = new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDb();
        var checker = new FakeVenueScheduleChecker(false);
        var handler = new CreateEventHandler(db, checker, clock);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Rock Concert");
        db.Events.Should().ContainSingle();
    }

    [Fact]
    public async Task Overlapping_schedule_returns_venue_overlap_error()
    {
        var clock = new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDb();
        var checker = new FakeVenueScheduleChecker(true);
        var handler = new CreateEventHandler(db, checker, clock);

        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.venueOverlap");
    }

    [Fact]
    public async Task Capacity_exceeds_venue_capacity_returns_error()
    {
        var clock = new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDb();
        var checker = new FakeVenueScheduleChecker(false);
        var handler = new CreateEventHandler(db, checker, clock);
        var command = ValidCommand() with { Capacity = 201 };

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.capacity.exceedsVenue");
    }

    [Fact]
    public async Task Unknown_venue_returns_not_found_error()
    {
        var clock = new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDb();
        var checker = new FakeVenueScheduleChecker(false);
        var handler = new CreateEventHandler(db, checker, clock);
        var command = ValidCommand() with { VenueId = 99 };

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("venue.notFound");
    }
}
