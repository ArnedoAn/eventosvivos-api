namespace EventosVivos.Application.Abstractions;

/// <summary>
/// Identity of the caller for the current request, resolved at the transport edge
/// (e.g. from the JWT principal) so Application handlers stay free of ASP.NET dependencies.
/// </summary>
public interface ICurrentUser
{
    Guid? Id { get; }
    bool IsInRole(string role);
}
