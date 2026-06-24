using EventosVivos.Application.Abstractions;

namespace EventosVivos.Infrastructure.Security;

public sealed class RandomProvider : IRandomProvider
{
    public int Next(int minValue, int maxValue) => Random.Shared.Next(minValue, maxValue);
}
