namespace EventosVivos.Application.Abstractions;

public interface IRandomProvider
{
    int Next(int minValue, int maxValue);
}
