using EventosVivos.Domain.Common;
using EventosVivos.Domain.ValueObjects;
using FluentAssertions;

namespace EventosVivos.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Money_rejects_non_positive(decimal v) =>
        Money.Create(v).IsFailure.Should().BeTrue();

    [Fact]
    public void Money_accepts_positive_amount()
    {
        var result = Money.Create(10.5m);
        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(10.5m);
    }
}
