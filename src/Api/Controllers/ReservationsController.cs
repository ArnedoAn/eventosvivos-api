using System.Security.Claims;
using EventosVivos.Api.Common;
using EventosVivos.Application.Reservations.CancelReservation;
using EventosVivos.Application.Reservations.ConfirmReservation;
using EventosVivos.Application.Reservations.CreateReservation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventosVivos.Api.Controllers;

public sealed record CreateReservationRequest(
    Guid EventId,
    int Quantity,
    string BuyerName,
    string BuyerEmail);

[ApiController]
[Route("api/[controller]")]
public sealed class ReservationsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "User")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(CreateReservationRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var command = new CreateReservationCommand(
            request.EventId,
            userId.Value,
            request.Quantity,
            request.BuyerName,
            request.BuyerEmail);

        var result = await sender.Send(command, cancellationToken);
        return result.ToCreatedResult(value => $"/api/reservations/{value.Id}");
    }

    private Guid? GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(value, out var id) ? id : null;
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ConfirmReservationCommand(id), cancellationToken);
        return result.ToActionResult(Ok);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "User,Admin")]
    [ProducesResponseType(typeof(ReservationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var command = new CancelReservationCommand(id, userId.Value, User.IsInRole("Admin"));
        var result = await sender.Send(command, cancellationToken);
        return result.ToActionResult(Ok);
    }
}
