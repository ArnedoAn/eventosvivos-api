using System.IdentityModel.Tokens.Jwt;
using EventosVivos.Domain.Users;
using EventosVivos.Infrastructure.Options;
using EventosVivos.Infrastructure.Security;
using FluentAssertions;

namespace EventosVivos.Infrastructure.Tests.Security;

public class JwtTokenServiceTests
{
    [Fact]
    public void Generate_returns_valid_token_with_role_claim()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            Key = "super-secret-key-at-least-32-bytes!",
            ExpiryMinutes = 60
        });
        var service = new JwtTokenService(options);
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "admin@example.com",
            Role = Role.Admin
        };

        var token = service.Generate(user);

        token.Should().NotBeNullOrWhiteSpace();
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();
        var jwt = handler.ReadJwtToken(token);
        jwt.Issuer.Should().Be("test-issuer");
        jwt.Audiences.Should().Contain("test-audience");
        jwt.Claims.Should().Contain(c => c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Value == "Admin");
    }
}
