using EventosVivos.Domain.Common;

namespace EventosVivos.Domain.ValueObjects;

public sealed record Money(decimal Amount)
{
    public static Result<Money> Create(decimal amount)
    {
        if (amount <= 0)
            return Result.Failure<Money>(new Error("money.invalidAmount", "Amount must be greater than zero."));

        return new Money(amount);
    }
}
