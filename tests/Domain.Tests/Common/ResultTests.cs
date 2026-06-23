using EventosVivos.Domain.Common;
using FluentAssertions;

namespace EventosVivos.Domain.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Failure_carries_error_and_is_not_success()
    {
        var err = new Error("rule.violated", "nope");
        Result r = Result.Failure(err);
        r.IsSuccess.Should().BeFalse();
        r.Error.Should().Be(err);
    }

    [Fact]
    public void SuccessT_exposes_value()
    {
        Result<int> r = 42;
        r.IsSuccess.Should().BeTrue();
        r.Value.Should().Be(42);
    }
}
