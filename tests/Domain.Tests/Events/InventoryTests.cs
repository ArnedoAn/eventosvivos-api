using EventosVivos.Domain.Abstractions;
using EventosVivos.Domain.Common;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Events;
using EventosVivos.Domain.ValueObjects;
using FluentAssertions;

namespace EventosVivos.Domain.Tests.Events;

public class InventoryTests
{
    private sealed class TestClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }

    private sealed class TestOptions : IReservationOptions
    {
        public bool PendingHoldsInventory { get; init; } = true;
        public int PendingExpirationMinutes { get; init; } = 0;
    }

    private static Event CreateEvent(int capacity = 100)
    {
        var clock = new TestClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var schedule = DateRange.Create(
            new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 15, 22, 0, 0, DateTimeKind.Utc)).Value;

        return Event.Create(
            "Rock Concert",
            "A great live rock show in the city.",
            venueId: 1,
            venueCapacity: capacity,
            capacity: capacity,
            schedule,
            Money.Create(50m).Value,
            EventType.Concierto,
            clock).Value;
    }

    [Fact]
    public void HoldOnReserve_with_pending_holds_inventory_increments_seats_taken()
    {
        var ev = CreateEvent();
        var options = new TestOptions { PendingHoldsInventory = true };

        var result = ev.HoldOnReserve(3, options);

        result.IsSuccess.Should().BeTrue();
        ev.SeatsTaken.Should().Be(3);
        ev.SeatsLost.Should().Be(0);
        ev.Remaining.Should().Be(97);
    }

    [Fact]
    public void HoldOnReserve_without_pending_holds_inventory_is_no_op()
    {
        var ev = CreateEvent();
        var options = new TestOptions { PendingHoldsInventory = false };

        var result = ev.HoldOnReserve(3, options);

        result.IsSuccess.Should().BeTrue();
        ev.SeatsTaken.Should().Be(0);
        ev.Remaining.Should().Be(100);
    }

    [Fact]
    public void ConsumeOnConfirm_with_pending_holds_inventory_is_no_op()
    {
        var ev = CreateEvent();
        var options = new TestOptions { PendingHoldsInventory = true };
        ev.HoldOnReserve(3, options);

        var result = ev.ConsumeOnConfirm(3, options);

        result.IsSuccess.Should().BeTrue();
        ev.SeatsTaken.Should().Be(3);
        ev.Remaining.Should().Be(97);
    }

    [Fact]
    public void ConsumeOnConfirm_without_pending_holds_inventory_increments_seats_taken()
    {
        var ev = CreateEvent();
        var options = new TestOptions { PendingHoldsInventory = false };
        ev.HoldOnReserve(3, options);

        var result = ev.ConsumeOnConfirm(3, options);

        result.IsSuccess.Should().BeTrue();
        ev.SeatsTaken.Should().Be(3);
        ev.Remaining.Should().Be(97);
    }

    [Fact]
    public void ReleaseOnCancel_without_penalty_restores_remaining()
    {
        var ev = CreateEvent();
        var options = new TestOptions { PendingHoldsInventory = true };
        ev.HoldOnReserve(3, options);

        var result = ev.ReleaseOnCancel(3, penalty: false);

        result.IsSuccess.Should().BeTrue();
        ev.SeatsTaken.Should().Be(0);
        ev.SeatsLost.Should().Be(0);
        ev.Remaining.Should().Be(100);
    }

    [Fact]
    public void ReleaseOnCancel_with_penalty_moves_quantity_to_seats_lost()
    {
        var ev = CreateEvent();
        var options = new TestOptions { PendingHoldsInventory = true };
        ev.HoldOnReserve(3, options);

        var result = ev.ReleaseOnCancel(3, penalty: true);

        result.IsSuccess.Should().BeTrue();
        ev.SeatsTaken.Should().Be(0);
        ev.SeatsLost.Should().Be(3);
        ev.Remaining.Should().Be(97);
    }

    [Fact]
    public void HoldOnReserve_exceeding_capacity_fails_with_event_capacity_exceeded()
    {
        var ev = CreateEvent(10);
        var options = new TestOptions { PendingHoldsInventory = true };

        var result = ev.HoldOnReserve(11, options);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.capacity.exceeded");
        ev.SeatsTaken.Should().Be(0);
    }

    [Fact]
    public void ConsumeOnConfirm_exceeding_capacity_fails_with_event_capacity_exceeded()
    {
        var ev = CreateEvent(10);
        var options = new TestOptions { PendingHoldsInventory = false };

        var result = ev.ConsumeOnConfirm(11, options);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.capacity.exceeded");
        ev.SeatsTaken.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReleaseOnCancel_non_positive_quantity_fails(int qty)
    {
        var ev = CreateEvent();
        var options = new TestOptions { PendingHoldsInventory = true };
        ev.HoldOnReserve(3, options);

        var result = ev.ReleaseOnCancel(qty, penalty: false);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.quantity.invalid");
    }

    [Fact]
    public void ReleaseOnCancel_exceeding_held_seats_fails()
    {
        var ev = CreateEvent();
        var options = new TestOptions { PendingHoldsInventory = true };
        ev.HoldOnReserve(3, options);

        var result = ev.ReleaseOnCancel(5, penalty: false);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.capacity.overRelease");
        ev.SeatsTaken.Should().Be(3);
    }

    [Fact]
    public void ReleasePendingHold_decrements_seats_taken()
    {
        var ev = CreateEvent();
        var options = new TestOptions { PendingHoldsInventory = true };
        ev.HoldOnReserve(5, options);

        var result = ev.ReleasePendingHold(3);

        result.IsSuccess.Should().BeTrue();
        ev.SeatsTaken.Should().Be(2);
        ev.SeatsLost.Should().Be(0);
        ev.Remaining.Should().Be(98);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReleasePendingHold_non_positive_quantity_fails(int qty)
    {
        var ev = CreateEvent();
        var options = new TestOptions { PendingHoldsInventory = true };
        ev.HoldOnReserve(5, options);

        var result = ev.ReleasePendingHold(qty);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.quantity.invalid");
    }

    [Fact]
    public void ReleasePendingHold_exceeding_held_seats_fails()
    {
        var ev = CreateEvent();
        var options = new TestOptions { PendingHoldsInventory = true };
        ev.HoldOnReserve(3, options);

        var result = ev.ReleasePendingHold(5);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.capacity.overRelease");
        ev.SeatsTaken.Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void HoldOnReserve_non_positive_quantity_fails(int qty)
    {
        var ev = CreateEvent();
        var options = new TestOptions { PendingHoldsInventory = true };

        var result = ev.HoldOnReserve(qty, options);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.quantity.invalid");
        ev.SeatsTaken.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConsumeOnConfirm_non_positive_quantity_fails(int qty)
    {
        var ev = CreateEvent();
        var options = new TestOptions { PendingHoldsInventory = false };

        var result = ev.ConsumeOnConfirm(qty, options);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.quantity.invalid");
        ev.SeatsTaken.Should().Be(0);
    }
}
