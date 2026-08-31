namespace ConferenceRoomRental.Application.Bookings;

public sealed record CreateBookingCommand(
    Guid RoomId,
    DateOnly Date,
    TimeOnly Start,
    int DurationMinutes,
    int AttendeeCount,
    IReadOnlyCollection<Guid> SelectedServiceIds);

public sealed record SelectedServiceDto(Guid ServiceId, string Name, decimal Price);

public sealed record PriceSegmentDto(
    string Period,
    TimeOnly From,
    TimeOnly To,
    decimal Hours,
    decimal Multiplier,
    decimal Amount);

public sealed record BookingDto(
    Guid Id,
    Guid RoomId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string TimeZone,
    int AttendeeCount,
    decimal BaseHourlyRate,
    decimal RoomPrice,
    decimal ServicesPrice,
    decimal TotalPrice,
    string Currency,
    IReadOnlyCollection<SelectedServiceDto> Services,
    IReadOnlyCollection<PriceSegmentDto> PriceBreakdown);
