namespace EventosVivos.Domain.ValueObjects;

public sealed record ReservationCode(string Value)
{
    public static ReservationCode New(Func<int> sixDigits)
    {
        var number = sixDigits();
        if (number < 0 || number > 999_999)
            throw new ArgumentOutOfRangeException(nameof(sixDigits), "Generated reservation code number must be between 0 and 999999.");

        return new ReservationCode($"EV-{number:D6}");
    }
}
