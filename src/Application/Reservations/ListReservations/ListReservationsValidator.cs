using FluentValidation;

namespace EventosVivos.Application.Reservations.ListReservations;

public sealed class ListReservationsValidator : AbstractValidator<ListReservationsQuery>
{
    public ListReservationsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
