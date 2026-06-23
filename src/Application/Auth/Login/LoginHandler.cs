using EventosVivos.Application.Abstractions;
using EventosVivos.Domain.Common;
using EventosVivos.Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Application.Auth.Login;

public sealed class LoginHandler(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService)
    : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user is null)
            return Result.Failure<LoginResponse>(new Error("auth.invalidCredentials", "Invalid credentials."));

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
            return Result.Failure<LoginResponse>(new Error("auth.invalidCredentials", "Invalid credentials."));

        var token = jwtTokenService.Generate(user);
        return Result.Success(new LoginResponse(token, user.Role.ToString()));
    }
}
