using EventosVivos.Application.Behaviors;
using EventosVivos.Application.Reservations.CreateReservation;
using EventosVivos.Domain.Common;
using FluentAssertions;
using FluentValidation;
using MediatR;

namespace EventosVivos.Application.Tests.Behaviors;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Invalid_command_short_circuits_with_validation_failure()
    {
        var validator = new CreateReservationValidator();
        var behavior = new ValidationBehavior<CreateReservationCommand, Result<ReservationResponse>>(
            new[] { validator });

        var request = new CreateReservationCommand(
            EventId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Quantity: 1,
            BuyerName: new string('A', 201),
            BuyerEmail: "not-an-email");

        var result = await behavior.Handle(request, _ => Task.FromResult(Result.Success(new ReservationResponse(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "John", "john@example.com", Domain.Enums.ReservationStatus.PendientePago, DateTime.UtcNow))), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation.failed");
    }

    [Fact]
    public async Task Valid_command_proceeds_to_next_delegate()
    {
        var validator = new CreateReservationValidator();
        var behavior = new ValidationBehavior<CreateReservationCommand, Result<ReservationResponse>>(
            new[] { validator });

        var request = new CreateReservationCommand(
            EventId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Quantity: 1,
            BuyerName: "John Doe",
            BuyerEmail: "john@example.com");

        var response = Result.Success(new ReservationResponse(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "John", "john@example.com", Domain.Enums.ReservationStatus.PendientePago, DateTime.UtcNow));

        var result = await behavior.Handle(request, _ => Task.FromResult(response), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
