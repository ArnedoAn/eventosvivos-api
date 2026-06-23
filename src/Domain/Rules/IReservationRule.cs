using EventosVivos.Domain.Common;

namespace EventosVivos.Domain.Rules;

public readonly record struct ReservationRequest(
    int Quantity,
    DateTime EventStartUtc,
    decimal EventPrice,
    int RemainingSeats,
    DateTime NowUtc);

public interface IReservationRule
{
    int Order { get; }
    Result Evaluate(ReservationRequest request);
}
