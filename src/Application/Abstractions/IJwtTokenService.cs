using EventosVivos.Domain.Users;

namespace EventosVivos.Application.Abstractions;

public interface IJwtTokenService
{
    string Generate(AppUser user);
}
