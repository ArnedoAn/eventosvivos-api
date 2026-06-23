using EventosVivos.Domain.Common;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Reservations;
using EventosVivos.Domain.ValueObjects;
using FluentAssertions;

namespace EventosVivos.Domain.Tests.Reservations;

public class ReservationTests
{
    private static Email ValidEmail => Email.Create("buyer@example.com").Value;

    [Fact]
    public void Reservation_create_happy_path_returns_pendiente_pago()
    {
        var eventId = Guid.NewGuid();
        var now = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = Reservation.Create(eventId, 2, "John Doe", ValidEmail, now);

        result.IsSuccess.Should().BeTrue();
        result.Value.EventId.Should().Be(eventId);
        result.Value.Quantity.Should().Be(2);
        result.Value.BuyerName.Should().Be("John Doe");
        result.Value.Email.Should().Be(ValidEmail);
        result.Value.Status.Should().Be(ReservationStatus.PendientePago);
        result.Value.CreatedUtc.Should().Be(now);
        result.Value.CancelledUtc.Should().BeNull();
        result.Value.IsLost.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, "reservation.quantity.invalid")]
    [InlineData(-1, "reservation.quantity.invalid")]
    public void Reservation_create_invalid_quantity_fails(int quantity, string expectedCode)
    {
        var result = Reservation.Create(Guid.NewGuid(), quantity, "John Doe", ValidEmail, DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData(null, "reservation.buyerName.required")]
    [InlineData("", "reservation.buyerName.required")]
    [InlineData("   ", "reservation.buyerName.required")]
    public void Reservation_create_invalid_buyer_name_fails(string? buyerName, string expectedCode)
    {
        var result = Reservation.Create(Guid.NewGuid(), 2, buyerName!, ValidEmail, DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(expectedCode);
    }

    [Fact]
    public void Reservation_create_null_email_fails()
    {
        var result = Reservation.Create(Guid.NewGuid(), 2, "John Doe", null!, DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("reservation.email.required");
    }

    [Fact]
    public void Reservation_confirm_from_pendiente_pago_succeeds()
    {
        var reservation = Reservation.Create(Guid.NewGuid(), 2, "John Doe", ValidEmail, DateTime.UtcNow).Value;
        var code = ReservationCode.New(() => 123456);

        var result = reservation.Confirm(code);

        result.IsSuccess.Should().BeTrue();
        reservation.Status.Should().Be(ReservationStatus.Confirmada);
        reservation.Code.Should().Be(code);
    }

    [Fact]
    public void Reservation_confirm_twice_fails_with_already_confirmed()
    {
        var reservation = Reservation.Create(Guid.NewGuid(), 2, "John Doe", ValidEmail, DateTime.UtcNow).Value;
        var code = ReservationCode.New(() => 123456);
        reservation.Confirm(code);

        var result = reservation.Confirm(ReservationCode.New(() => 654321));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("reservation.alreadyConfirmed");
    }

    [Fact]
    public void Reservation_confirm_cancelled_fails_with_cancelled()
    {
        var reservation = Reservation.Create(Guid.NewGuid(), 2, "John Doe", ValidEmail, DateTime.UtcNow).Value;
        var code = ReservationCode.New(() => 123456);
        reservation.Confirm(code);
        reservation.Cancel(
            new DateTime(2030, 6, 13, 20, 0, 0, DateTimeKind.Utc),
            new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc));

        var result = reservation.Confirm(ReservationCode.New(() => 654321));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("reservation.cancelled");
    }

    [Fact]
    public void Reservation_cancel_from_pending_fails()
    {
        var reservation = Reservation.Create(Guid.NewGuid(), 2, "John Doe", ValidEmail, DateTime.UtcNow).Value;

        var result = reservation.Cancel(DateTime.UtcNow, DateTime.UtcNow.AddDays(2));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("reservation.notConfirmed");
    }

    [Fact]
    public void Reservation_cancel_within_48h_returns_penalty_true_and_marks_lost()
    {
        var eventStart = new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc);
        var now = eventStart.AddHours(-47);
        var reservation = Reservation.Create(Guid.NewGuid(), 2, "John Doe", ValidEmail, now.AddDays(-1)).Value;
        reservation.Confirm(ReservationCode.New(() => 123456));

        var result = reservation.Cancel(now, eventStart);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        reservation.Status.Should().Be(ReservationStatus.Cancelada);
        reservation.CancelledUtc.Should().Be(now);
        reservation.IsLost.Should().BeTrue();
    }

    [Fact]
    public void Reservation_cancel_at_exactly_48h_returns_penalty_false()
    {
        var eventStart = new DateTime(2030, 6, 15, 20, 0, 0, DateTimeKind.Utc);
        var now = eventStart.AddHours(-48);
        var reservation = Reservation.Create(Guid.NewGuid(), 2, "John Doe", ValidEmail, now.AddDays(-1)).Value;
        reservation.Confirm(ReservationCode.New(() => 123456));

        var result = reservation.Cancel(now, eventStart);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        reservation.IsLost.Should().BeFalse();
    }

    [Fact]
    public void ReservationCode_new_formats_value()
    {
        var code = ReservationCode.New(() => 42);

        code.Value.Should().Be("EV-000042");
    }
}
