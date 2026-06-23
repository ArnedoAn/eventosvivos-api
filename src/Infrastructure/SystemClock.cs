using EventosVivos.Domain.Abstractions;

namespace EventosVivos.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
