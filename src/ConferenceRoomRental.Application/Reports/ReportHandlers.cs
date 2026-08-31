using ConferenceRoomRental.Application.Abstractions;
using ConferenceRoomRental.Application.Common;

namespace ConferenceRoomRental.Application.Reports;

public sealed class RevenueReportHandler(IReportRepository reports, TimeZoneInfo businessTimeZone)
{
    public async Task<RevenueReportDto> HandleAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        LocalDateTimeRange range = LocalDateTimeRange.ForDateRange(from, to, businessTimeZone);
        ReportSummary summary = await ReportAggregation.LoadAsync(reports, range, cancellationToken);

        RevenueByRoomDto[] byRoom = summary.Rooms
            .Select(room => new RevenueByRoomDto(
                room.RoomId,
                room.RoomName,
                room.BookingCount,
                room.Revenue))
            .OrderByDescending(room => room.Revenue)
            .ThenBy(room => room.RoomName)
            .ToArray();

        return new RevenueReportDto(
            from,
            to,
            "UAH",
            summary.BookingCount,
            summary.TotalRevenue,
            summary.BookingCount == 0
                ? 0
                : Round(summary.TotalRevenue / summary.BookingCount),
            byRoom);
    }

    private static decimal Round(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}

public sealed class UtilizationReportHandler(IReportRepository reports, TimeZoneInfo businessTimeZone)
{
    private const decimal BusinessHoursPerDay = 17m;

    public async Task<UtilizationReportDto> HandleAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        LocalDateTimeRange range = LocalDateTimeRange.ForDateRange(from, to, businessTimeZone);
        ReportSummary summary = await ReportAggregation.LoadAsync(reports, range, cancellationToken);

        int dayCount = to.DayNumber - from.DayNumber + 1;
        decimal availableHours = dayCount * BusinessHoursPerDay;
        RoomUtilizationDto[] rooms = summary.Rooms
            .Select(room => new RoomUtilizationDto(
                room.RoomId,
                room.RoomName,
                Round(room.BookedHours),
                availableHours,
                Round(room.BookedHours / availableHours * 100m)))
            .OrderByDescending(room => room.UtilizationPercent)
            .ThenBy(room => room.RoomName)
            .ToArray();

        return new UtilizationReportDto(from, to, rooms);
    }

    private static decimal Round(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}

internal static class ReportAggregation
{
    public static async Task<ReportSummary> LoadAsync(
        IReportRepository reports,
        LocalDateTimeRange range,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, MutableRoomSummary> rooms = [];
        int bookingCount = 0;
        decimal totalRevenue = 0;

        // Stream rows so memory usage depends on the room count, not the booking count.
        await foreach (BookingReportRecord booking in reports
            .StreamConfirmedBookings(range.StartsAtUtc, range.EndsAtUtc)
            .WithCancellation(cancellationToken))
        {
            bookingCount++;
            totalRevenue += booking.TotalPrice;

            if (!rooms.TryGetValue(booking.RoomId, out MutableRoomSummary? room))
            {
                room = new MutableRoomSummary(booking.RoomId, booking.RoomName);
                rooms.Add(booking.RoomId, room);
            }

            room.Add(booking);
        }

        return new ReportSummary(
            bookingCount,
            totalRevenue,
            rooms.Values.Select(room => room.ToSummary()).ToArray());
    }

    private sealed class MutableRoomSummary(Guid roomId, string roomName)
    {
        private decimal _bookedHours;
        private int _bookingCount;
        private decimal _revenue;

        public void Add(BookingReportRecord booking)
        {
            _bookingCount++;
            _revenue += booking.TotalPrice;
            _bookedHours += (decimal)(booking.EndsAtUtc - booking.StartsAtUtc).Ticks / TimeSpan.TicksPerHour;
        }

        public RoomReportSummary ToSummary() =>
            new(roomId, roomName, _bookingCount, _revenue, _bookedHours);
    }
}

internal sealed record ReportSummary(
    int BookingCount,
    decimal TotalRevenue,
    IReadOnlyCollection<RoomReportSummary> Rooms);

internal sealed record RoomReportSummary(
    Guid RoomId,
    string RoomName,
    int BookingCount,
    decimal Revenue,
    decimal BookedHours);
