using ConferenceRoomRental.Application.Abstractions;
using ConferenceRoomRental.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ConferenceRoomRental.Infrastructure.Persistence;

internal sealed partial class EfUnitOfWork(
    AppDbContext context,
    ILogger<EfUnitOfWork> logger) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            string entityTypes = string.Join(
                ", ",
                exception.Entries.Select(entry => entry.Metadata.ClrType.Name).Distinct(StringComparer.Ordinal));
            LogConcurrencyConflict(logger, exception, entityTypes);
            throw new ConflictException("The resource was modified by another request. Refresh it and retry.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            throw new ConflictException("The room was booked by another request for the same time.");
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("A resource with the same unique value already exists.");
        }
    }

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Warning,
        Message = "Optimistic concurrency conflict while saving entities: {EntityTypes}")]
    private static partial void LogConcurrencyConflict(
        ILogger logger,
        Exception exception,
        string entityTypes);
}
