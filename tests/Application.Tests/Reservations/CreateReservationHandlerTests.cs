using EventosVivos.Application.Abstractions;
using EventosVivos.Application.Reservations.CreateReservation;
using EventosVivos.Domain.Abstractions;
using EventosVivos.Domain.Common;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Events;
using EventosVivos.Domain.Reservations;
using EventosVivos.Domain.Rules;
using EventosVivos.Domain.Users;
using EventosVivos.Domain.ValueObjects;
using EventosVivos.Domain.Venues;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Application.Tests.Reservations;

public class CreateReservationHandlerTests
{
    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }

    private sealed class FakeReservationOptions : IReservationOptions
    {
        public bool PendingHoldsInventory { get; init; } = true;
        public int PendingExpirationMinutes { get; init; } = 0;
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

    private sealed class EagerReservationExpirer : IReservationExpirer
    {
        private readonly IAppDbContext _db;
        private readonly IReservationOptions _options;

        public EagerReservationExpirer(IAppDbContext db, IReservationOptions options)
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

            var evt = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
            if (evt is null)
                return;

            foreach (var reservation in pending)
            {
                if (reservation.Expire(nowUtc).IsSuccess)
                    evt.ReleasePendingHold(reservation.Quantity);
            }

            await _db.SaveChangesAsync(ct);
        }
    }

    private sealed class CountingClock(DateTime start) : IClock
    {
        private int _calls;
        public DateTime UtcNow => start.AddHours(_calls++);
    }

    private sealed class RetryOnConcurrencyPolicy : IConcurrencyRetryPolicy
    {
        public async Task<Result<T>> ExecuteAsync<T>(Func<Task<Result<T>>> action, int maxAttempts = 3)
        {
            DbUpdateConcurrencyException? last = null;
            for (var i = 0; i < maxAttempts; i++)
            {
                try
                {
                    return await action();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    last = ex;
                }
            }
            throw last!;
        }
    }

    private sealed class ThrowOnceSaveContext(TestAppDbContext inner) : IAppDbContext
    {
        private bool _throw = true;

        public DbSet<Event> Events => inner.Events;
        public DbSet<Reservation> Reservations => inner.Reservations;
        public DbSet<Venue> Venues => inner.Venues;
        public DbSet<AppUser> Users => inner.Users;

        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            if (_throw)
            {
                _throw = false;
                inner.ChangeTracker.Clear();
                throw new DbUpdateConcurrencyException("Simulated concurrency conflict.");
            }

            return inner.SaveChangesAsync(ct);
        }

        public void ResetChangeTracker() => inner.ResetChangeTracker();
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

        var context = new TestAppDbContext(options);
        context.Venues.Add(new Venue(1, "Auditorio Central", 200, "Bogotá"));
        context.SaveChanges();
        return context;
    }

    private static Event CreateTestEvent(
        DateTime startUtc,
        DateTime endUtc,
        int capacity = 100,
        decimal price = 50m,
        IClock? clock = null)
    {
        var creationClock = clock ?? new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
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
            creationClock).Value;
    }

    private static ReservationRuleSet RuleSet() =>
        new(new IReservationRule[]
        {
            new LateReservationRule(),
            new Near24hRule(),
            new HighPriceRule(),
            new AvailabilityRule()
        });

    private static CreateReservationHandler CreateHandler(
        IAppDbContext db,
        IClock clock,
        IConcurrencyRetryPolicy? retryPolicy = null,
        IReservationOptions? options = null,
        IReservationExpirer? expirer = null)
    {
        options ??= new FakeReservationOptions();
        expirer ??= new NoOpReservationExpirer();

        return new CreateReservationHandler(
            db,
            clock,
            options,
            RuleSet(),
            expirer,
            retryPolicy ?? new NoOpConcurrencyRetryPolicy());
    }

    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static CreateReservationCommand ValidCommand(Guid eventId) => new(
        EventId: eventId,
        UserId: TestUserId,
        Quantity: 2,
        BuyerName: "John Doe",
        BuyerEmail: "john@example.com");

    [Fact]
    public async Task Valid_command_creates_pending_payment_reservation_and_holds_inventory()
    {
        var clock = new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDb();
        var evt = CreateTestEvent(
            new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 15, 22, 0, 0, DateTimeKind.Utc));
        db.Events.Add(evt);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db, clock);

        var result = await handler.Handle(ValidCommand(evt.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(ReservationStatus.PendientePago);
        result.Value.UserId.Should().Be(TestUserId);
        result.Value.Quantity.Should().Be(2);
        result.Value.BuyerEmail.Should().Be("john@example.com");
        db.Reservations.Should().ContainSingle();
        evt.Remaining.Should().Be(98);
    }

    [Fact]
    public async Task Completed_event_returns_not_active_error()
    {
        var creationClock = new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var queryClock = new FixedClock(new DateTime(2030, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDb();
        var evt = CreateTestEvent(
            new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 15, 22, 0, 0, DateTimeKind.Utc),
            clock: creationClock);
        db.Events.Add(evt);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db, queryClock);

        var result = await handler.Handle(ValidCommand(evt.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.notActive");
    }

    [Fact]
    public async Task Exceeding_remaining_seats_returns_sold_out_error()
    {
        var clock = new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDb();
        var evt = CreateTestEvent(
            new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 15, 22, 0, 0, DateTimeKind.Utc),
            capacity: 10);
        db.Events.Add(evt);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db, clock);
        var command = ValidCommand(evt.Id) with { Quantity = 11 };

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("reserve.soldOut");
    }

    [Fact]
    public async Task Unknown_event_returns_not_found_error()
    {
        var clock = new FixedClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await using var db = CreateDb();

        var handler = CreateHandler(db, clock);

        var result = await handler.Handle(ValidCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.notFound");
    }

    [Fact]
    public async Task Expired_pending_reservation_releases_inventory_for_new_reservation()
    {
        var now = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var clock = new FixedClock(now);
        await using var db = CreateDb();
        var evt = CreateTestEvent(
            new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 15, 22, 0, 0, DateTimeKind.Utc),
            capacity: 10);
        db.Events.Add(evt);

        var email = Email.Create("existing@example.com").Value;
        var oldReservation = Reservation.Create(evt.Id, Guid.NewGuid(), 10, "Jane Doe", email, now.AddMinutes(-30)).Value;
        db.Reservations.Add(oldReservation);
        evt.HoldOnReserve(10, new FakeReservationOptions());
        await db.SaveChangesAsync();

        var options = new FakeReservationOptions { PendingExpirationMinutes = 15 };
        var expirer = new EagerReservationExpirer(db, options);
        var handler = CreateHandler(db, clock, options: options, expirer: expirer);

        var result = await handler.Handle(
            ValidCommand(evt.Id) with { BuyerEmail = "new@example.com" },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Quantity.Should().Be(2);
        evt.Remaining.Should().Be(8);
        db.Reservations.Count(r => r.EventId == evt.Id).Should().Be(2);
        db.Reservations.Count(r => r.EventId == evt.Id && r.Status == ReservationStatus.Cancelada).Should().Be(1);
    }

    [Fact]
    public async Task Retry_attempt_uses_fresh_clock_time()
    {
        var t1 = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var clock = new CountingClock(t1);
        await using var db = CreateDb();
        var context = new ThrowOnceSaveContext(db);
        var evt = CreateTestEvent(
            new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 15, 22, 0, 0, DateTimeKind.Utc));
        db.Events.Add(evt);
        await db.SaveChangesAsync();

        var handler = CreateHandler(context, clock, new RetryOnConcurrencyPolicy());

        var result = await handler.Handle(ValidCommand(evt.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedUtc.Should().Be(t1.AddHours(1));
        db.Reservations.Should().ContainSingle();
    }

    [Fact]
    public void Invalid_email_fails_validation()
    {
        var validator = new CreateReservationValidator();
        var command = new CreateReservationCommand(
            EventId: Guid.NewGuid(),
            UserId: TestUserId,
            Quantity: 1,
            BuyerName: "John Doe",
            BuyerEmail: "not-an-email");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.BuyerEmail);
    }

    [Fact]
    public void Zero_quantity_fails_validation()
    {
        var validator = new CreateReservationValidator();
        var command = new CreateReservationCommand(
            EventId: Guid.NewGuid(),
            UserId: TestUserId,
            Quantity: 0,
            BuyerName: "John Doe",
            BuyerEmail: "john@example.com");

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }
}
