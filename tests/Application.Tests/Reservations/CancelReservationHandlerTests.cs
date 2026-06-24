using EventosVivos.Application.Abstractions;
using EventosVivos.Application.Reservations.CancelReservation;
using EventosVivos.Application.Reservations.CreateReservation;
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

namespace EventosVivos.Application.Tests.Reservations;

public class CancelReservationHandlerTests
{
    private sealed class FakeReservationOptions : IReservationOptions
    {
        public bool PendingHoldsInventory => true;
        public int PendingExpirationMinutes => 0;
    }

    private sealed class NoOpConcurrencyRetryPolicy : IConcurrencyRetryPolicy
    {
        public Task<Result<T>> ExecuteAsync<T>(Func<Task<Result<T>>> action, int maxAttempts = 3)
            => action();
    }

    private sealed class NoOpReservationExpirer : IReservationExpirer
    {
        public Task ExpireOverduePendingReservationsAsync(Guid eventId, DateTime nowUtc, CancellationToken ct = default)
            => Task.CompletedTask;
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

    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }

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

    private static Event CreateTestEvent(
        DateTime startUtc,
        DateTime endUtc,
        int capacity = 100,
        decimal price = 50m)
    {
        var clock = new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var schedule = DateRange.Create(startUtc, endUtc).Value;
        var money = Money.Create(price).Value;
        return Event.Create(
            "Rock Concert",
            "A great live rock show in the city.",
            1,
            200,
            capacity,
            schedule,
            money,
            EventType.Concierto,
            clock).Value;
    }

    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static Reservation CreateConfirmedReservation(
        Guid eventId,
        int quantity = 2,
        DateTime? createdUtc = null)
    {
        var clock = new FixedClock(createdUtc ?? new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var email = Email.Create("buyer@example.com").Value;
        var reservation = Reservation.Create(eventId, TestUserId, quantity, "John Doe", email, clock.UtcNow).Value;
        reservation.Confirm(ReservationCode.New(() => 123456));
        return reservation;
    }

    private sealed class FakeCurrentUser(Guid? id, bool isAdmin = false) : ICurrentUser
    {
        public Guid? Id { get; } = id;
        public bool IsInRole(string role) => isAdmin && role == "Admin";
    }

    private static CancelReservationHandler CreateHandler(
        IAppDbContext db,
        IClock clock,
        Guid? currentUserId = null,
        bool isAdmin = false)
    {
        return new CancelReservationHandler(
            db,
            clock,
            new NoOpReservationExpirer(),
            new NoOpConcurrencyRetryPolicy(),
            new FakeCurrentUser(currentUserId ?? TestUserId, isAdmin));
    }

    [Fact]
    public async Task Cancel_confirmed_reservation_more_than_48h_before_event_restores_remaining()
    {
        var now = new DateTime(2030, 6, 13, 19, 59, 59, DateTimeKind.Utc);
        var clock = new FixedClock(now);
        await using var db = CreateDb();
        var evt = CreateTestEvent(
            new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 15, 22, 0, 0, DateTimeKind.Utc));
        db.Events.Add(evt);
        var reservation = CreateConfirmedReservation(evt.Id);
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        evt.HoldOnReserve(reservation.Quantity, new FakeReservationOptions());
        await db.SaveChangesAsync();

        var handler = CreateHandler(db, clock);

        var result = await handler.Handle(new CancelReservationCommand(reservation.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ReservationStatus.Cancelada);

        var refreshedReservation = await db.Reservations
            .AsNoTracking()
            .FirstAsync(r => r.Id == reservation.Id);
        refreshedReservation.Status.Should().Be(ReservationStatus.Cancelada);
        refreshedReservation.CancelledUtc.Should().Be(now);
        refreshedReservation.IsLost.Should().BeFalse();

        evt.SeatsTaken.Should().Be(0);
        evt.SeatsLost.Should().Be(0);
        evt.Remaining.Should().Be(100);
    }

    [Fact]
    public async Task Cancel_confirmed_reservation_less_than_48h_before_event_makes_seats_lost()
    {
        var now = new DateTime(2030, 6, 13, 20, 0, 1, DateTimeKind.Utc);
        var clock = new FixedClock(now);
        await using var db = CreateDb();
        var evt = CreateTestEvent(
            new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 15, 22, 0, 0, DateTimeKind.Utc));
        db.Events.Add(evt);
        var reservation = CreateConfirmedReservation(evt.Id);
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        evt.HoldOnReserve(reservation.Quantity, new FakeReservationOptions());
        await db.SaveChangesAsync();

        var handler = CreateHandler(db, clock);

        var result = await handler.Handle(new CancelReservationCommand(reservation.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ReservationStatus.Cancelada);

        var refreshedReservation = await db.Reservations
            .AsNoTracking()
            .FirstAsync(r => r.Id == reservation.Id);
        refreshedReservation.Status.Should().Be(ReservationStatus.Cancelada);
        refreshedReservation.CancelledUtc.Should().Be(now);
        refreshedReservation.IsLost.Should().BeTrue();

        evt.SeatsTaken.Should().Be(0);
        evt.SeatsLost.Should().Be(2);
        evt.Remaining.Should().Be(98);
    }

    [Fact]
    public async Task Cancel_pending_reservation_returns_notConfirmed_error()
    {
        var clock = new FixedClock(new DateTime(2030, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDb();
        var evt = CreateTestEvent(
            new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 15, 22, 0, 0, DateTimeKind.Utc));
        db.Events.Add(evt);
        var email = Email.Create("buyer@example.com").Value;
        var reservation = Reservation.Create(evt.Id, TestUserId, 2, "John Doe", email, clock.UtcNow).Value;
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db, clock);

        var result = await handler.Handle(new CancelReservationCommand(reservation.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("reservation.notConfirmed");
    }

    [Fact]
    public async Task Cancel_already_cancelled_reservation_returns_cancelled_error()
    {
        var clock = new FixedClock(new DateTime(2030, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDb();
        var evt = CreateTestEvent(
            new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 15, 22, 0, 0, DateTimeKind.Utc));
        db.Events.Add(evt);
        var reservation = CreateConfirmedReservation(evt.Id);
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        reservation.Cancel(clock.UtcNow, evt.Schedule.StartUtc);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db, clock);

        var result = await handler.Handle(new CancelReservationCommand(reservation.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("reservation.cancelled");
    }

    [Fact]
    public async Task Cancel_unknown_reservation_returns_notFound_error()
    {
        var clock = new FixedClock(new DateTime(2030, 6, 10, 0, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDb();
        var handler = CreateHandler(db, clock);

        var result = await handler.Handle(new CancelReservationCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("reservation.notFound");
    }

    [Fact]
    public async Task Cancel_reservation_by_non_owner_returns_forbidden_error()
    {
        var now = new DateTime(2030, 6, 13, 19, 0, 0, DateTimeKind.Utc);
        var clock = new FixedClock(now);
        await using var db = CreateDb();
        var evt = CreateTestEvent(
            new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 15, 22, 0, 0, DateTimeKind.Utc));
        db.Events.Add(evt);
        var reservation = CreateConfirmedReservation(evt.Id);
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        evt.HoldOnReserve(reservation.Quantity, new FakeReservationOptions());
        await db.SaveChangesAsync();

        var handler = CreateHandler(db, clock, currentUserId: OtherUserId);

        var result = await handler.Handle(new CancelReservationCommand(reservation.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("reservation.forbidden");
    }
}
