using EventosVivos.Domain.Common;

namespace EventosVivos.Domain.Rules;

public sealed class ReservationRuleSet
{
    private readonly IReservationRule[] _rules;

    public ReservationRuleSet(IEnumerable<IReservationRule> rules)
    {
        _rules = rules?.OrderBy(r => r.Order).ToArray() ?? throw new ArgumentNullException(nameof(rules));
    }

    public IReadOnlyList<IReservationRule> Rules => _rules;

    public Result Evaluate(ReservationRequest request)
    {
        foreach (var rule in _rules)
        {
            var result = rule.Evaluate(request);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }
}
