using ConferenceRoomRental.Application.Common;
using ConferenceRoomRental.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceRoomRental.Api.Infrastructure;

internal sealed partial class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int status, string title) = exception switch
        {
            ValidationException or DomainException => (StatusCodes.Status400BadRequest, "Validation failed"),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ConflictException => (StatusCodes.Status409Conflict, "Request conflict"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred"),
        };

        if (status >= 500)
        {
            LogUnhandledException(exception, httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            LogExpectedException(status, exception.Message);
        }

        ProblemDetails details = exception is ValidationException validation
            ? new ValidationProblemDetails(validation.Errors.ToDictionary(x => x.Key, x => x.Value))
            : new ProblemDetails();

        details.Status = status;
        details.Title = title;
        details.Detail = status >= 500 ? "Contact support with the trace identifier." : exception.Message;
        details.Instance = httpContext.Request.Path;

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = details,
            Exception = exception,
        });
    }

    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "Unhandled exception while processing {Method} {Path}")]
    private partial void LogUnhandledException(Exception exception, string method, PathString path);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Request failed with status {StatusCode}: {Message}")]
    private partial void LogExpectedException(int statusCode, string message);
}
