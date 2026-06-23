using EventosVivos.Domain.Common;

namespace EventosVivos.Domain.Rules;

public sealed class LateReservationRule : IReservationRule
{
    public int Order => 10;

    public Result Evaluate(ReservationRequest request)
    {
        if (request.EventStartUtc - request.NowUtc < TimeSpan.FromHours(1))
        {
            return Result.Failure(new Error("reserve.tooLate", "Reservations are closed within 1 hour of the event start."));
        }

        return Result.Success();
    }
}
