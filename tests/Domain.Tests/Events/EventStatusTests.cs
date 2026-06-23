using EventosVivos.Domain.Abstractions;
using EventosVivos.Domain.Common;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Events;
using EventosVivos.Domain.ValueObjects;
using FluentAssertions;

namespace EventosVivos.Domain.Tests.Events;

public class EventStatusTests
{
    private sealed class TestClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }

    private static Event CreateActiveEvent(DateTime startUtc, DateTime endUtc)
    {
        var clock = new TestClock(startUtc.AddDays(-1));
        var schedule = DateRange.Create(startUtc, endUtc).Value;
        var result = Event.Create(
            "Rock Concert",
            "A great live rock show in the city.",
            venueId: 1,
            venueCapacity: 1000,
            capacity: 500,
            schedule,
            Money.Create(50m).Value,
            EventType.Concierto,
            clock);

        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public void RefreshStatus_active_event_past_end_becomes_completed()
    {
        var start = new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);
        var ev = CreateActiveEvent(start, end);

        ev.RefreshStatus(end.AddMinutes(1));

        ev.Status.Should().Be(EventStatus.Completado);
    }

    [Fact]
    public void RefreshStatus_cancelled_event_past_end_stays_cancelled()
    {
        var start = new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);
        var ev = CreateActiveEvent(start, end);
        ev.Cancel().IsSuccess.Should().BeTrue();

        ev.RefreshStatus(end.AddMinutes(1));

        ev.Status.Should().Be(EventStatus.Cancelado);
    }

    [Fact]
    public void RefreshStatus_active_event_before_end_stays_active()
    {
        var start = new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);
        var ev = CreateActiveEvent(start, end);

        ev.RefreshStatus(start.AddMinutes(30));

        ev.Status.Should().Be(EventStatus.Activo);
    }

    [Fact]
    public void Cancel_active_event_succeeds_and_sets_status_to_cancelled()
    {
        var start = new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);
        var ev = CreateActiveEvent(start, end);

        var result = ev.Cancel();

        result.IsSuccess.Should().BeTrue();
        ev.Status.Should().Be(EventStatus.Cancelado);
    }

    [Fact]
    public void Cancel_completed_event_fails_with_not_active_error()
    {
        var start = new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);
        var ev = CreateActiveEvent(start, end);
        ev.RefreshStatus(end.AddMinutes(1));
        ev.Status.Should().Be(EventStatus.Completado);

        var result = ev.Cancel();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.notActive");
    }

    [Fact]
    public void Cancel_cancelled_event_fails_with_not_active_error()
    {
        var start = new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(2);
        var ev = CreateActiveEvent(start, end);
        ev.Cancel().IsSuccess.Should().BeTrue();

        var result = ev.Cancel();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.notActive");
    }
}
