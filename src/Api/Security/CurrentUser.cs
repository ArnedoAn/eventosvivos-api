using System.Security.Claims;
using EventosVivos.Application.Abstractions;

namespace EventosVivos.Api.Security;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? Id
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? accessor.HttpContext?.User.FindFirstValue("sub");

            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsInRole(string role) =>
        accessor.HttpContext?.User.IsInRole(role) ?? false;
}
