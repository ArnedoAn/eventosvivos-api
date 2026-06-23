using EventosVivos.Domain.Abstractions;
using FluentValidation;

namespace EventosVivos.Application.Reservations.CreateReservation;

public sealed class CreateReservationValidator : AbstractValidator<CreateReservationCommand>
{
    public CreateReservationValidator(IClock clock)
    {
        RuleFor(x => x.EventId)
            .NotEmpty();

        RuleFor(x => x.Quantity)
            .GreaterThan(0);

        RuleFor(x => x.BuyerName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.BuyerEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);
    }
}
