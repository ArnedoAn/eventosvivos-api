using EventosVivos.Domain.Abstractions;
using EventosVivos.Domain.Common;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.ValueObjects;

namespace EventosVivos.Domain.Events;

public sealed class Event
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int VenueId { get; private set; }
    public int Capacity { get; private set; }
    public DateRange Schedule { get; private set; } = null!;
    public Money Price { get; private set; } = null!;
    public EventType Type { get; private set; }
    public EventStatus Status { get; private set; }
    public int SeatsTaken { get; private set; }
    public int SeatsLost { get; private set; }
    public uint Version { get; private set; }

    private Event()
    {
    }

    public static Result<Event> Create(
        string title,
        string description,
        int venueId,
        int venueCapacity,
        int capacity,
        DateRange schedule,
        Money price,
        EventType type,
        IClock clock)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length < 5 || title.Length > 100)
            return Result.Failure<Event>(new Error("event.title.length", "Title must be between 5 and 100 characters."));

        if (string.IsNullOrWhiteSpace(description) || description.Length < 10 || description.Length > 500)
            return Result.Failure<Event>(new Error("event.description.length", "Description must be between 10 and 500 characters."));

        if (capacity <= 0)
            return Result.Failure<Event>(new Error("event.capacity.invalid", "Capacity must be greater than zero."));

        if (capacity > venueCapacity)
            return Result.Failure<Event>(new Error("event.capacity.exceedsVenue", "Capacity cannot exceed venue capacity."));

        if (schedule.StartUtc <= clock.UtcNow)
            return Result.Failure<Event>(new Error("event.start.past", "Start must be in the future."));

        if (IsWeekendNight(schedule.StartUtc))
            return Result.Failure<Event>(new Error("event.start.weekendNight", "Weekend events cannot start after 22:00."));

        return new Event
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = description,
            VenueId = venueId,
            Capacity = capacity,
            Schedule = schedule,
            Price = price,
            Type = type,
            Status = EventStatus.Activo,
            SeatsTaken = 0,
            SeatsLost = 0,
            Version = 0
        };
    }

    /// <summary>
    /// Updates the event status based on the current time.
    /// On-read auto-completion: an active event whose schedule has ended becomes completed.
    /// Cancelled events are never changed by this method.
    /// </summary>
    public void RefreshStatus(DateTime nowUtc)
    {
        if (Status == EventStatus.Activo && nowUtc > Schedule.EndUtc)
            Status = EventStatus.Completado;
    }

    /// <summary>
    /// Cancels the event. Only active events can be cancelled.
    /// </summary>
    public Result Cancel()
    {
        if (Status != EventStatus.Activo)
            return Result.Failure(new Error("event.notActive", "Only active events can be cancelled."));

        Status = EventStatus.Cancelado;
        return Result.Success();
    }

    private static bool IsWeekendNight(DateTime startUtc)
    {
        var day = startUtc.DayOfWeek;
        if (day != DayOfWeek.Saturday && day != DayOfWeek.Sunday)
            return false;

        return startUtc.TimeOfDay > TimeSpan.FromHours(22);
    }
}
