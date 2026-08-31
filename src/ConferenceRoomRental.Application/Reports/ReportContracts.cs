namespace ConferenceRoomRental.Application.Reports;

public sealed record RevenueByRoomDto(
    Guid RoomId,
    string RoomName,
    int BookingCount,
    decimal Revenue);

public sealed record RevenueReportDto(
    DateOnly From,
    DateOnly To,
    string Currency,
    int BookingCount,
    decimal TotalRevenue,
    decimal AverageBookingValue,
    IReadOnlyCollection<RevenueByRoomDto> ByRoom);

public sealed record RoomUtilizationDto(
    Guid RoomId,
    string RoomName,
    decimal BookedHours,
    decimal AvailableHours,
    decimal UtilizationPercent);

public sealed record UtilizationReportDto(
    DateOnly From,
    DateOnly To,
    IReadOnlyCollection<RoomUtilizationDto> Rooms);
