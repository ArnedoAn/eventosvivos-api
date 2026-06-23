using EventosVivos.Domain.ValueObjects;
using FluentAssertions;

namespace EventosVivos.Domain.Tests.ValueObjects;

public class EmailTests
{
    [Theory]
    [InlineData("a@b.com", true)]
    [InlineData("nope", false)]
    [InlineData("", false)]
    public void Email_validates_format(string raw, bool ok) =>
        Email.Create(raw).IsSuccess.Should().Be(ok);

    [Fact]
    public void Email_trims_and_lowercases()
    {
        var result = Email.Create("  Test.User@Example.COM  ");
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("test.user@example.com");
    }
}
