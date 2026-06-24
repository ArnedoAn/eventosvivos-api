using EventosVivos.Application.Abstractions;
using EventosVivos.Application.Reports.GetOccupancy;
using EventosVivos.Domain.Abstractions;
using EventosVivos.Domain.Common;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Events;
using EventosVivos.Domain.Reservations;
using EventosVivos.Domain.Users;
using EventosVivos.Domain.ValueObjects;
using EventosVivos.Domain.Venues;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Application.Tests.Reports;

public class GetOccupancyHandlerTests
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

    private sealed class FakeReservationOptions : IReservationOptions
    {
        public bool PendingHoldsInventory => true;
        public int PendingExpirationMinutes => 0;
    }

    private static TestAppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }

    private static Event CreateTestEvent(
        int capacity = 100,
        decimal price = 50m,
        DateTime? startUtc = null,
        DateTime? endUtc = null,
        IClock? clock = null)
    {
        var creationClock = clock ?? new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var scheduleStart = startUtc ?? new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc);
        var scheduleEnd = endUtc ?? new DateTime(2030, 6, 15, 22, 0, 0, DateTimeKind.Utc);
        var schedule = DateRange.Create(scheduleStart, scheduleEnd).Value;
        var money = Money.Create(price).Value;
        return Event.Create(
            "Rock Concert",
            "A great live rock show in the city.",
            1,
            capacity,
            capacity,
            schedule,
            money,
            EventType.Concierto,
            creationClock).Value;
    }

    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static Reservation CreateConfirmedReservation(Guid eventId, int quantity, DateTime nowUtc, string code)
    {
        var reservation = Reservation.Create(
            eventId,
            TestUserId,
            quantity,
            "Buyer",
            Email.Create("buyer@example.com").Value,
            nowUtc).Value;

        reservation.Confirm(ReservationCode.New(() => int.Parse(code)));
        return reservation;
    }

    [Fact]
    public async Task Existing_event_returns_occupancy_with_confirmed_lost_and_available_seats()
    {
        var nowUtc = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var queryClock = new FixedClock(nowUtc);
        await using var db = CreateDb();
        var evt = CreateTestEvent(capacity: 100, price: 50m);
        db.Events.Add(evt);
        await db.SaveChangesAsync();

        // 30 confirmed seats
        evt.HoldOnReserve(30, new FakeReservationOptions());
        evt.ConsumeOnConfirm(30, new FakeReservationOptions());
        db.Reservations.Add(CreateConfirmedReservation(evt.Id, 30, nowUtc, "000001"));

        // 10 confirmed then cancelled with penalty (retained)
        evt.HoldOnReserve(10, new FakeReservationOptions());
        evt.ConsumeOnConfirm(10, new FakeReservationOptions());
        var lostReservation = CreateConfirmedReservation(evt.Id, 10, nowUtc, "000002");
        lostReservation.Cancel(nowUtc, evt.Schedule.StartUtc);
        evt.ReleaseOnCancel(10, true).IsSuccess.Should().BeTrue();
        db.Reservations.Add(lostReservation);

        await db.SaveChangesAsync();

        var handler = new GetOccupancyHandler(db, queryClock);
        var query = new GetOccupancyQuery(evt.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.EventId.Should().Be(evt.Id);
        result.Value.Title.Should().Be("Rock Concert");
        result.Value.Capacity.Should().Be(100);
        result.Value.SoldConfirmed.Should().Be(30);
        result.Value.RetainedByPenalty.Should().Be(10);
        result.Value.AvailableRemaining.Should().Be(60);
        result.Value.OccupancyPercent.Should().Be(30.0);
        result.Value.TotalRevenue.Should().Be(1500m);
        result.Value.Status.Should().Be(EventStatus.Activo);
    }

    [Fact]
    public async Task Unknown_event_returns_not_found_error()
    {
        var clock = new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDb();

        var handler = new GetOccupancyHandler(db, clock);
        var query = new GetOccupancyQuery(Guid.NewGuid());

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.notFound");
    }

    [Fact]
    public async Task Completed_event_reflects_auto_completed_status()
    {
        var creationClock = new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var queryClock = new FixedClock(new DateTime(2030, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDb();
        var evt = CreateTestEvent(
            startUtc: new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc),
            endUtc: new DateTime(2030, 6, 15, 22, 0, 0, DateTimeKind.Utc),
            clock: creationClock);
        db.Events.Add(evt);
        await db.SaveChangesAsync();

        var handler = new GetOccupancyHandler(db, queryClock);
        var query = new GetOccupancyQuery(evt.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(EventStatus.Completado);
        result.Value.SoldConfirmed.Should().Be(0);
        result.Value.AvailableRemaining.Should().Be(100);
    }
}
