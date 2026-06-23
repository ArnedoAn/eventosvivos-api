namespace EventosVivos.Domain.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
