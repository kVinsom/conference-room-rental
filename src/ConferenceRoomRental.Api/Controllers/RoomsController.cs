using System.ComponentModel.DataAnnotations;
using ConferenceRoomRental.Api.Contracts;
using ConferenceRoomRental.Application.Rooms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ConferenceRoomRental.Api.Controllers;

/// <summary>Creates, maintains and searches the conference-room catalogue.</summary>
[ApiController]
[Route("api/v1/rooms")]
public sealed class RoomsController : ControllerBase
{
    /// <summary>Returns one active room.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<RoomDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoomDto>> Get(
        Guid id,
        [FromServices] GetRoomHandler handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(id, cancellationToken));

    /// <summary>Returns a stable, paginated list of active rooms.</summary>
    [HttpGet]
    [ProducesResponseType<PagedRoomsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedRoomsDto>> List(
        [FromServices] ListRoomsHandler handler,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await handler.HandleAsync(page, pageSize, cancellationToken));

    /// <summary>Creates a room and its available service catalogue.</summary>
    [HttpPost]
    [ProducesResponseType<RoomDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoomDto>> Create(
        CreateRoomRequest request,
        [FromServices] CreateRoomHandler handler,
        CancellationToken cancellationToken)
    {
        CreateRoomCommand command = new(
            request.Name,
            request.Capacity,
            request.BaseHourlyRate,
            request.Services.ToServiceInputs());

        RoomDto room = await handler.HandleAsync(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = room.Id }, room);
    }

    /// <summary>Replaces editable room details and synchronizes its service catalogue.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<RoomDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoomDto>> Update(
        Guid id,
        UpdateRoomRequest request,
        [FromServices] UpdateRoomHandler handler,
        CancellationToken cancellationToken)
    {
        UpdateRoomCommand command = new(
            id,
            request.Name,
            request.Capacity,
            request.BaseHourlyRate,
            request.Services.ToServiceInputs());

        return Ok(await handler.HandleAsync(command, cancellationToken));
    }

    /// <summary>Soft-deletes a room while preserving historical bookings and reports.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] DeleteRoomHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Finds rooms with enough capacity and no overlapping confirmed booking.</summary>
    [HttpGet("available")]
    [ProducesResponseType<IReadOnlyCollection<RoomDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<RoomDto>>> SearchAvailable(
        [FromQuery, BindRequired] DateOnly date,
        [FromQuery, BindRequired] TimeOnly start,
        [FromQuery, BindRequired] TimeOnly end,
        [FromQuery, Range(1, 10_000)] int minimumCapacity,
        [FromServices] SearchAvailableRoomsHandler handler,
        CancellationToken cancellationToken)
    {
        SearchAvailableRoomsQuery query = new(date, start, end, minimumCapacity);
        return Ok(await handler.HandleAsync(query, cancellationToken));
    }
}
