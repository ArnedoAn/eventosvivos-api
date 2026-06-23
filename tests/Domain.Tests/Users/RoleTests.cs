using EventosVivos.Domain.Users;
using FluentAssertions;

namespace EventosVivos.Domain.Tests.Users;

public class RoleTests
{
    [Fact]
    public void Role_has_exact_members()
    {
        Enum.GetNames<Role>()
            .Should()
            .BeEquivalentTo(new[] { "Admin", "User" });
    }
}
