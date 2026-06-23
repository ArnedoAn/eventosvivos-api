using EventosVivos.Domain.Common;

namespace EventosVivos.Domain.Rules;

public sealed class Near24hRule : IReservationRule
{
    public int Order => 20;

    public Result Evaluate(ReservationRequest request)
    {
        if (request.EventStartUtc - request.NowUtc < TimeSpan.FromHours(24) && request.Quantity > 5)
        {
            return Result.Failure(new Error("reserve.max5Near24h", "Maximum 5 seats within 24 hours of the event."));
        }

        return Result.Success();
    }
}
