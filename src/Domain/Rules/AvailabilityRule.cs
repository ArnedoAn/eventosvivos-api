using EventosVivos.Domain.Common;

namespace EventosVivos.Domain.Rules;

public sealed class AvailabilityRule : IReservationRule
{
    public int Order => 40;

    public Result Evaluate(ReservationRequest request)
    {
        if (request.Quantity < 1)
        {
            return Result.Failure(new Error("reserve.minQuantity", "At least one seat must be reserved."));
        }

        if (request.Quantity > request.RemainingSeats)
        {
            return Result.Failure(new Error("reserve.soldOut", "Not enough seats available."));
        }

        return Result.Success();
    }
}
