using EventosVivos.Domain.Common;
using EventosVivos.Domain.Rules;
using FluentAssertions;

namespace EventosVivos.Domain.Tests.Rules;

public class ReservationRuleTests
{
    private static ReservationRuleSet CreateRuleSet() =>
        new(new IReservationRule[]
        {
            new LateReservationRule(),
            new Near24hRule(),
            new HighPriceRule(),
            new AvailabilityRule()
        });

    [Theory]
    [InlineData(59, false)]   // 59 min before start -> rejected
    [InlineData(61, true)]    // 61 min -> allowed (subject to later rules)
    public void Late_rule_blocks_under_1h(int minutesBefore, bool ok)
    {
        var rule = new LateReservationRule();
        var eventStart = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var now = eventStart.AddMinutes(-minutesBefore);
        var req = new ReservationRequest(1, eventStart, 50m, 100, now);

        var result = rule.Evaluate(req);

        if (ok)
        {
            result.IsSuccess.Should().BeTrue();
        }
        else
        {
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("reserve.tooLate");
        }
    }

    [Fact]
    public void Near24h_takes_priority_over_high_price()
    {
        var set = CreateRuleSet();
        var req = new ReservationRequest(
            Quantity: 6,
            EventStartUtc: new DateTime(2030, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            EventPrice: 150m,
            RemainingSeats: 100,
            NowUtc: new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)); // 10h before -> <24h window

        var result = set.Evaluate(req);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("reserve.max5Near24h");
    }

    [Fact]
    public void High_price_limits_to_10_when_far_out()
    {
        var set = CreateRuleSet();
        var req = new ReservationRequest(
            Quantity: 11,
            EventStartUtc: new DateTime(2030, 1, 11, 0, 0, 0, DateTimeKind.Utc),
            EventPrice: 150m,
            RemainingSeats: 100,
            NowUtc: new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = set.Evaluate(req);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("reserve.max10HighPrice");
    }

    [Fact]
    public void Availability_blocks_when_quantity_exceeds_remaining()
    {
        var set = CreateRuleSet();
        var req = new ReservationRequest(
            Quantity: 5,
            EventStartUtc: new DateTime(2030, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            EventPrice: 50m,
            RemainingSeats: 3,
            NowUtc: new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = set.Evaluate(req);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("reserve.soldOut");
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public void Availability_enforces_minimum_quantity(int quantity, bool ok)
    {
        var rule = new AvailabilityRule();
        var req = new ReservationRequest(
            quantity,
            new DateTime(2030, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            50m,
            100,
            new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = rule.Evaluate(req);

        if (ok)
        {
            result.IsSuccess.Should().BeTrue();
        }
        else
        {
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("reserve.minQuantity");
        }
    }

    [Theory]
    [InlineData(23, 6, false)] // <24h and qty>5 -> fail
    [InlineData(23, 5, true)]  // <24h and qty=5 -> ok
    [InlineData(25, 6, true)]  // >24h and qty>5 -> ok
    public void Near24h_rule_boundaries(int hoursBefore, int quantity, bool ok)
    {
        var rule = new Near24hRule();
        var eventStart = new DateTime(2030, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var now = eventStart.AddHours(-hoursBefore);
        var req = new ReservationRequest(quantity, eventStart, 50m, 100, now);

        var result = rule.Evaluate(req);

        if (ok)
        {
            result.IsSuccess.Should().BeTrue();
        }
        else
        {
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("reserve.max5Near24h");
        }
    }

    [Theory]
    [InlineData(100, 11, true)]  // price<=100 -> ok regardless of qty
    [InlineData(150, 10, true)]  // qty<=10 -> ok regardless of price
    [InlineData(150, 11, false)] // price>100 and qty>10 -> fail
    public void High_price_rule_boundaries(decimal price, int quantity, bool ok)
    {
        var rule = new HighPriceRule();
        var req = new ReservationRequest(
            quantity,
            new DateTime(2030, 1, 11, 0, 0, 0, DateTimeKind.Utc),
            price,
            100,
            new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = rule.Evaluate(req);

        if (ok)
        {
            result.IsSuccess.Should().BeTrue();
        }
        else
        {
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("reserve.max10HighPrice");
        }
    }

    [Fact]
    public void RuleSet_returns_success_when_all_rules_pass()
    {
        var set = CreateRuleSet();
        var req = new ReservationRequest(
            Quantity: 5,
            EventStartUtc: new DateTime(2030, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            EventPrice: 50m,
            RemainingSeats: 10,
            NowUtc: new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = set.Evaluate(req);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RuleSet_sorts_rules_by_order()
    {
        var set = new ReservationRuleSet(new IReservationRule[]
        {
            new AvailabilityRule(),
            new LateReservationRule(),
            new HighPriceRule(),
            new Near24hRule()
        });

        var req = new ReservationRequest(
            Quantity: 1,
            EventStartUtc: new DateTime(2030, 1, 1, 0, 30, 0, DateTimeKind.Utc),
            EventPrice: 50m,
            RemainingSeats: 100,
            NowUtc: new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = set.Evaluate(req);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("reserve.tooLate");
    }
}
