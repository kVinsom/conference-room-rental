namespace ConferenceRoomRental.Application.Rooms;

public sealed record ServiceInput(string Name, decimal Price);

public sealed record ServiceDto(Guid Id, string Name, decimal Price);

public sealed record RoomDto(
    Guid Id,
    string Name,
    int Capacity,
    decimal BaseHourlyRate,
    IReadOnlyCollection<ServiceDto> Services);

public sealed record PagedRoomsDto(
    IReadOnlyCollection<RoomDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record CreateRoomCommand(
    string Name,
    int Capacity,
    decimal BaseHourlyRate,
    IReadOnlyCollection<ServiceInput> Services);

public sealed record UpdateRoomCommand(
    Guid Id,
    string Name,
    int Capacity,
    decimal BaseHourlyRate,
    IReadOnlyCollection<ServiceInput> Services);

public sealed record SearchAvailableRoomsQuery(
    DateOnly Date,
    TimeOnly Start,
    TimeOnly End,
    int MinimumCapacity);
