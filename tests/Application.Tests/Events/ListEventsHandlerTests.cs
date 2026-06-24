using EventosVivos.Application.Abstractions;
using EventosVivos.Application.Events.ListEvents;
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

public class ListEventsHandlerTests
{
    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }

    private sealed class TestAppDbContext : DbContext, IAppDbContext
    {
        public TestAppDbContext(DbContextOptions<TestAppDbContext> options) : base(options) { }

        public DbSet<Event> Events => Set<Event>();
        public DbSet<Reservation> Reservations => Set<Reservation>();
        public DbSet<Venue> Venues => Set<Venue>();
        public DbSet<AppUser> Users => Set<AppUser>();
        public void ResetChangeTracker() => ChangeTracker.Clear();

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

    private static TestAppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }

    private static Event CreateTestEvent(
        string title,
        EventType type,
        DateTime startUtc,
        DateTime endUtc,
        int venueId = 1,
        int capacity = 100,
        decimal price = 10m,
        string description = "Description long enough.",
        IClock? clock = null)
    {
        var creationClock = clock ?? new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var schedule = DateRange.Create(startUtc, endUtc).Value;
        var money = Money.Create(price).Value;
        return Event.Create(title, description, venueId, capacity, capacity, schedule, money, type, creationClock).Value;
    }

    [Fact]
    public async Task Filter_by_type_returns_only_matching_events()
    {
        await using var db = CreateDb();
        db.Events.Add(CreateTestEvent("Rock Night", EventType.Concierto,
            new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 15, 22, 0, 0, DateTimeKind.Utc)));
        db.Events.Add(CreateTestEvent("Conferencia X", EventType.Conferencia,
            new DateTime(2030, 6, 16, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 16, 12, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var clock = new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var handler = new ListEventsHandler(db, clock);
        var query = new ListEventsQuery(Type: EventType.Conferencia);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.Single().Type.Should().Be(EventType.Conferencia);
        result.Value.Single().Title.Should().Be("Conferencia X");
    }

    [Fact]
    public async Task Title_filter_is_case_insensitive_partial_match()
    {
        await using var db = CreateDb();
        db.Events.Add(CreateTestEvent("Conferencia X", EventType.Conferencia,
            new DateTime(2030, 6, 16, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 16, 12, 0, 0, DateTimeKind.Utc)));
        db.Events.Add(CreateTestEvent("Taller de Cocina", EventType.Taller,
            new DateTime(2030, 6, 17, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 17, 12, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var clock = new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var handler = new ListEventsHandler(db, clock);
        var query = new ListEventsQuery(TitleContains: "conf");

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.Single().Title.Should().Be("Conferencia X");
    }

    [Fact]
    public async Task Date_range_filter_is_inclusive_on_start()
    {
        await using var db = CreateDb();
        db.Events.Add(CreateTestEvent("Day One", EventType.Conferencia,
            new DateTime(2030, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 15, 12, 0, 0, DateTimeKind.Utc)));
        db.Events.Add(CreateTestEvent("Day Two", EventType.Conferencia,
            new DateTime(2030, 6, 16, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 16, 12, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var clock = new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var handler = new ListEventsHandler(db, clock);
        var query = new ListEventsQuery(
            FromUtc: new DateTime(2030, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2030, 6, 15, 23, 59, 59, DateTimeKind.Utc));

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.Single().Title.Should().Be("Day One");
    }

    [Fact]
    public async Task Venue_filter_returns_only_events_at_that_venue()
    {
        await using var db = CreateDb();
        db.Events.Add(CreateTestEvent("Venue One Event", EventType.Conferencia,
            new DateTime(2030, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 15, 12, 0, 0, DateTimeKind.Utc), venueId: 1));
        db.Events.Add(CreateTestEvent("Venue Two Event", EventType.Conferencia,
            new DateTime(2030, 6, 16, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 16, 12, 0, 0, DateTimeKind.Utc), venueId: 2));
        await db.SaveChangesAsync();

        var clock = new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var handler = new ListEventsHandler(db, clock);
        var query = new ListEventsQuery(VenueId: 2);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.Single().Title.Should().Be("Venue Two Event");
    }

    [Fact]
    public async Task Status_filter_reflects_auto_completed_events()
    {
        await using var db = CreateDb();
        var creationClock = new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        db.Events.Add(CreateTestEvent("Past Event", EventType.Concierto,
            new DateTime(2030, 2, 1, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 2, 1, 12, 0, 0, DateTimeKind.Utc),
            clock: creationClock));
        db.Events.Add(CreateTestEvent("Future Event", EventType.Taller,
            new DateTime(2030, 5, 1, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            clock: creationClock));
        await db.SaveChangesAsync();

        var queryClock = new FixedClock(new DateTime(2030, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        var handler = new ListEventsHandler(db, queryClock);
        var query = new ListEventsQuery(Status: EventStatus.Completado);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.Single().Title.Should().Be("Past Event");
        result.Value.Single().Status.Should().Be(EventStatus.Completado);
    }

    [Fact]
    public async Task No_filters_returns_all_events_with_refreshed_status()
    {
        await using var db = CreateDb();
        var creationClock = new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        db.Events.Add(CreateTestEvent("Past Event", EventType.Concierto,
            new DateTime(2030, 2, 1, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 2, 1, 12, 0, 0, DateTimeKind.Utc),
            clock: creationClock));
        db.Events.Add(CreateTestEvent("Future Event", EventType.Taller,
            new DateTime(2030, 5, 1, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            clock: creationClock));
        await db.SaveChangesAsync();

        var queryClock = new FixedClock(new DateTime(2030, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        var handler = new ListEventsHandler(db, queryClock);
        var query = new ListEventsQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(e => e.Status == EventStatus.Completado && e.Title == "Past Event");
        result.Value.Should().Contain(e => e.Status == EventStatus.Activo && e.Title == "Future Event");
    }
}
