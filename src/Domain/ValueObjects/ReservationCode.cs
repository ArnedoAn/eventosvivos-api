namespace EventosVivos.Domain.ValueObjects;

public sealed record ReservationCode(string Value)
{
    public static ReservationCode New(Func<int> sixDigits)
    {
        var number = sixDigits() % 1_000_000;
        return new ReservationCode($"EV-{number:D6}");
    }
}
