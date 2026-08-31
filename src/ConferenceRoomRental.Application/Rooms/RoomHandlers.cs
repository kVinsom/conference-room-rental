using ConferenceRoomRental.Application.Abstractions;
using ConferenceRoomRental.Application.Common;
using ConferenceRoomRental.Domain.Rooms;

namespace ConferenceRoomRental.Application.Rooms;

public sealed class CreateRoomHandler(
    IRoomRepository rooms,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<RoomDto> HandleAsync(CreateRoomCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        string normalizedCandidate = ConferenceRoom.NormalizeName(command.Name);
        ConferenceRoom room = ConferenceRoom.Create(
            normalizedCandidate,
            command.Capacity,
            command.BaseHourlyRate,
            command.Services.ToServiceDefinitions(),
            timeProvider.GetUtcNow());

        if (await rooms.ActiveNameExistsAsync(normalizedCandidate, null, cancellationToken))
        {
            throw new ConflictException($"An active room named '{normalizedCandidate}' already exists.");
        }

        rooms.Add(room);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return room.ToDto();
    }
}

public sealed class UpdateRoomHandler(
    IRoomRepository rooms,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<RoomDto> HandleAsync(UpdateRoomCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        string normalizedCandidate = ConferenceRoom.NormalizeName(command.Name);

        ConferenceRoom room = await rooms.GetAsync(command.Id, asTracking: true, cancellationToken)
            ?? throw new NotFoundException($"Room '{command.Id}' was not found.");

        if (await rooms.ActiveNameExistsAsync(normalizedCandidate, command.Id, cancellationToken))
        {
            throw new ConflictException($"An active room named '{normalizedCandidate}' already exists.");
        }

        room.Update(
            normalizedCandidate,
            command.Capacity,
            command.BaseHourlyRate,
            command.Services.ToServiceDefinitions(),
            timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return room.ToDto();
    }
}

public sealed class DeleteRoomHandler(
    IRoomRepository rooms,
    IBookingRepository bookings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(Guid roomId, CancellationToken cancellationToken)
    {
        RoomValidation.EnsureValidId(roomId);

        ConferenceRoom room = await rooms.GetAsync(roomId, asTracking: true, cancellationToken)
            ?? throw new NotFoundException($"Room '{roomId}' was not found.");

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (await bookings.HasFutureBookingAsync(roomId, now, cancellationToken))
        {
            throw new ConflictException("A room with future bookings cannot be deleted.");
        }

        room.Archive(now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class GetRoomHandler(IRoomRepository rooms)
{
    public async Task<RoomDto> HandleAsync(Guid roomId, CancellationToken cancellationToken)
    {
        RoomValidation.EnsureValidId(roomId);

        ConferenceRoom room = await rooms.GetAsync(roomId, asTracking: false, cancellationToken)
            ?? throw new NotFoundException($"Room '{roomId}' was not found.");
        return room.ToDto();
    }
}

public sealed class ListRoomsHandler(IRoomRepository rooms)
{
    public async Task<PagedRoomsDto> HandleAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1)
        {
            throw new ValidationException("page", "Page must be at least 1.");
        }

        if (pageSize is < 1 or > 100)
        {
            throw new ValidationException("pageSize", "Page size must be between 1 and 100.");
        }

        IReadOnlyCollection<ConferenceRoom> items = await rooms.ListAsync((page - 1) * pageSize, pageSize, cancellationToken);
        int totalCount = await rooms.CountAsync(cancellationToken);
        return new PagedRoomsDto(items.Select(x => x.ToDto()).ToArray(), page, pageSize, totalCount);
    }
}

public sealed class SearchAvailableRoomsHandler(IRoomRepository rooms, TimeZoneInfo businessTimeZone)
{
    public async Task<IReadOnlyCollection<RoomDto>> HandleAsync(
        SearchAvailableRoomsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.MinimumCapacity < 1)
        {
            throw new ValidationException("minimumCapacity", "Minimum capacity must be positive.");
        }

        LocalDateTimeRange range = LocalDateTimeRange.FromTimes(
            query.Date,
            query.Start,
            query.End,
            businessTimeZone);

        IReadOnlyCollection<ConferenceRoom> available = await rooms.FindAvailableAsync(
            range.StartsAtUtc,
            range.EndsAtUtc,
            query.MinimumCapacity,
            cancellationToken);

        return available.Select(x => x.ToDto()).ToArray();
    }
}

internal static class RoomValidation
{
    public static void EnsureValidId(Guid roomId)
    {
        if (roomId == Guid.Empty)
        {
            throw new ValidationException("roomId", "Room identifier is required.");
        }
    }
}

internal static class RoomMapping
{
    public static ServiceDefinition[] ToServiceDefinitions(this IReadOnlyCollection<ServiceInput> services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.Select(service => new ServiceDefinition(service.Name, service.Price)).ToArray();
    }

    public static RoomDto ToDto(this ConferenceRoom room) =>
        new(
            room.Id,
            room.Name,
            room.Capacity,
            room.BaseHourlyRate,
            room.Services.OrderBy(x => x.Name).Select(x => new ServiceDto(x.Id, x.Name, x.Price)).ToArray());
}
