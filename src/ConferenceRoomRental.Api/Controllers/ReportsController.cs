using ConferenceRoomRental.Application.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ConferenceRoomRental.Api.Controllers;

/// <summary>Provides read-only revenue and room-utilization analytics.</summary>
[ApiController]
[Route("api/v1/reports")]
public sealed class ReportsController : ControllerBase
{
    /// <summary>Revenue totals and average booking value, grouped by room.</summary>
    [HttpGet("revenue")]
    [ProducesResponseType<RevenueReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RevenueReportDto>> Revenue(
        [FromQuery, BindRequired] DateOnly from,
        [FromQuery, BindRequired] DateOnly to,
        [FromServices] RevenueReportHandler handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(from, to, cancellationToken));

    /// <summary>Booked hours as a percentage of the 06:00-23:00 business window.</summary>
    [HttpGet("utilization")]
    [ProducesResponseType<UtilizationReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UtilizationReportDto>> Utilization(
        [FromQuery, BindRequired] DateOnly from,
        [FromQuery, BindRequired] DateOnly to,
        [FromServices] UtilizationReportHandler handler,
        CancellationToken cancellationToken) =>
        Ok(await handler.HandleAsync(from, to, cancellationToken));
}
