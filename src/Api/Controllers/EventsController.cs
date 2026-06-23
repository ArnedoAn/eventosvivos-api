using EventosVivos.Api.Common;
using EventosVivos.Application.Events.CreateEvent;
using EventosVivos.Application.Events.ListEvents;
using EventosVivos.Application.Reports.GetOccupancy;
using EventosVivos.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventosVivos.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class EventsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateEventCommand command, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return result.ToCreatedResult($"/api/events/{result.Value.Id}");
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<EventResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] EventType? type,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? venueId,
        [FromQuery] EventStatus? status,
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var query = new ListEventsQuery(type, from, to, venueId, status, q);
        var result = await sender.Send(query, cancellationToken);
        return result.ToActionResult(Ok);
    }

    [HttpGet("{id:guid}/occupancy")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(OccupancyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Occupancy(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetOccupancyQuery(id), cancellationToken);
        return result.ToActionResult(Ok);
    }
}
