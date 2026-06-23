using EventosVivos.Domain.Common;

namespace EventosVivos.Domain.Rules;

public sealed class HighPriceRule : IReservationRule
{
    public int Order => 30;

    public Result Evaluate(ReservationRequest request)
    {
        if (request.EventPrice > 100m && request.Quantity > 10)
        {
            return Result.Failure(new Error("reserve.max10HighPrice", "Maximum 10 seats for high-price events."));
        }

        return Result.Success();
    }
}
