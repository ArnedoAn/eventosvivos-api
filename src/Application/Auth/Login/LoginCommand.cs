using EventosVivos.Domain.Common;
using MediatR;

namespace EventosVivos.Application.Auth.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<LoginResponse>>;
