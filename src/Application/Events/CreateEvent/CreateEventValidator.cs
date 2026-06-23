using EventosVivos.Domain.Abstractions;
using FluentValidation;

namespace EventosVivos.Application.Events.CreateEvent;

public sealed class CreateEventValidator : AbstractValidator<CreateEventCommand>
{
    public CreateEventValidator(IClock clock)
    {
        RuleFor(x => x.Title)
            .Length(5, 100);

        RuleFor(x => x.Description)
            .Length(10, 500);

        RuleFor(x => x.Capacity)
            .GreaterThan(0);

        RuleFor(x => x.Price)
            .GreaterThan(0);

        RuleFor(x => x.StartUtc)
            .GreaterThan(x => clock.UtcNow);

        RuleFor(x => x.EndUtc)
            .GreaterThan(x => x.StartUtc);

        RuleFor(x => x.Type)
            .IsInEnum();
    }
}
