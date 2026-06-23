using EventosVivos.Domain.Abstractions;
using EventosVivos.Domain.Common;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Events;
using EventosVivos.Domain.ValueObjects;
using FluentAssertions;

namespace EventosVivos.Domain.Tests.Events;

public class EventCreationTests
{
    private sealed class TestClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }

    private static EventCreationParameters ValidParameters(DateTime start, DateTime? end = null) => new(
        "Rock Concert",
        "A great live rock show in the city.",
        VenueId: 1,
        VenueCapacity: 1000,
        Capacity: 500,
        DateRange.Create(start, end ?? start.AddHours(2)).Value,
        Money.Create(50m).Value,
        EventType.Concierto);

    private readonly record struct EventCreationParameters(
        string Title,
        string Description,
        int VenueId,
        int VenueCapacity,
        int Capacity,
        DateRange Schedule,
        Money Price,
        EventType Type);

    private static Result<Event> CreateEvent(EventCreationParameters p, IClock clock) =>
        Event.Create(p.Title, p.Description, p.VenueId, p.VenueCapacity, p.Capacity, p.Schedule, p.Price, p.Type, clock);

    [Fact]
    public void Event_create_happy_path_returns_success()
    {
        var clock = new TestClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var parameters = ValidParameters(new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc));

        var result = CreateEvent(parameters, clock);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be(parameters.Title);
        result.Value.Description.Should().Be(parameters.Description);
        result.Value.VenueId.Should().Be(parameters.VenueId);
        result.Value.Capacity.Should().Be(parameters.Capacity);
        result.Value.Schedule.Should().Be(parameters.Schedule);
        result.Value.Price.Should().Be(parameters.Price);
        result.Value.Type.Should().Be(parameters.Type);
        result.Value.Status.Should().Be(EventStatus.Activo);
        result.Value.SeatsTaken.Should().Be(0);
        result.Value.SeatsLost.Should().Be(0);
        result.Value.Version.Should().Be(0u);
    }

    [Fact]
    public void Event_create_capacity_exceeds_venue_fails_with_RN_01()
    {
        var clock = new TestClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var parameters = ValidParameters(new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc)) with { Capacity = 1001 };

        var result = CreateEvent(parameters, clock);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.capacity.exceedsVenue");
    }

    [Fact]
    public void Event_create_start_in_past_fails()
    {
        var clock = new TestClock(new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc));
        var parameters = ValidParameters(new DateTime(2030, 6, 15, 19, 0, 0, DateTimeKind.Utc));

        var result = CreateEvent(parameters, clock);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.start.past");
    }

    [Fact]
    public void Event_create_saturday_after_22_00_fails_with_RN_03()
    {
        // 2030-06-15 is a Saturday
        var clock = new TestClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var parameters = ValidParameters(new DateTime(2030, 6, 15, 22, 30, 0, DateTimeKind.Utc));

        var result = CreateEvent(parameters, clock);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("event.start.weekendNight");
    }

    [Fact]
    public void Event_create_saturday_at_21_00_passes()
    {
        // 2030-06-15 is a Saturday
        var clock = new TestClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var parameters = ValidParameters(new DateTime(2030, 6, 15, 21, 0, 0, DateTimeKind.Utc));

        var result = CreateEvent(parameters, clock);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Event_create_title_length_boundaries(int length, bool shouldSucceed)
    {
        var clock = new TestClock(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var title = new string('x', length);
        var parameters = ValidParameters(new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc)) with { Title = title };

        var result = CreateEvent(parameters, clock);

        if (shouldSucceed)
        {
            result.IsSuccess.Should().BeTrue();
        }
        else
        {
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("event.title.length");
        }
    }
}
