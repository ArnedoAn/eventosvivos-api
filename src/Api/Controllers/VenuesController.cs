using EventosVivos.Api.Common;
using EventosVivos.Application.Venues.ListVenues;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EventosVivos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class VenuesController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<VenueResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListVenuesQuery(), cancellationToken);
        return result.ToActionResult(Ok);
    }
}
