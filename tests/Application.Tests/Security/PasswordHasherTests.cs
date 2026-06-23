using EventosVivos.Infrastructure.Security;
using FluentAssertions;

namespace EventosVivos.Application.Tests.Security;

public class PasswordHasherTests
{
    [Fact]
    public void Verify_matches_hashed_password()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("secret");

        hasher.Verify("secret", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_rejects_wrong_password()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("secret");

        hasher.Verify("wrong", hash).Should().BeFalse();
    }
}
