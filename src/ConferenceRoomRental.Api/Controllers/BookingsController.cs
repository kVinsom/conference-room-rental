using ConferenceRoomRental.Api.Contracts;
using ConferenceRoomRental.Application.Bookings;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceRoomRental.Api.Controllers;

/// <summary>Confirms bookings and returns auditable price breakdowns.</summary>
[ApiController]
[Route("api/v1/bookings")]
public sealed class BookingsController : ControllerBase
{
    /// <summary>Returns a confirmed booking with the price recalculated from its stored snapshots.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<BookingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingDto>> Get(
        Guid id,
        [FromServices] GetBookingHandler handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(id, cancellationToken));

    /// <summary>Books a room. Service fees are charged once; room rates are prorated per tariff segment.</summary>
    [HttpPost]
    [ProducesResponseType<BookingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingDto>> Create(
        CreateBookingRequest request,
        [FromServices] CreateBookingHandler handler,
        CancellationToken cancellationToken)
    {
        CreateBookingCommand command = new(
            request.RoomId,
            request.Date,
            request.Start,
            request.DurationMinutes,
            request.AttendeeCount,
            request.SelectedServiceIds);

        BookingDto booking = await handler.HandleAsync(command, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = booking.Id }, booking);
    }
}
