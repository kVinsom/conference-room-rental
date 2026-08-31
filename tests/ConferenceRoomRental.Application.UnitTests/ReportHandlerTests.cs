using ConferenceRoomRental.Application.Abstractions;
using ConferenceRoomRental.Application.Reports;

namespace ConferenceRoomRental.Application.UnitTests;

public sealed class ReportHandlerTests
{
    private static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");
    private static readonly DateOnly ReportDate = new(2030, 1, 2);
    private static readonly DateTimeOffset Start = new(2030, 1, 2, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Revenue_WhenBookingsSpanRooms_AggregatesAndOrdersRooms()
    {
        Guid roomA = Guid.NewGuid();
        Guid roomB = Guid.NewGuid();
        RevenueReportHandler handler = new(
            new FakeReportRepository(
                Record(roomA, "Room A", Start, 60, 100m),
                Record(roomA, "Room A", Start.AddHours(2), 120, 150m),
                Record(roomB, "Room B", Start, 30, 50m)),
            TimeZone);

        RevenueReportDto result = await handler.HandleAsync(ReportDate, ReportDate, CancellationToken.None);

        Assert.Equal(3, result.BookingCount);
        Assert.Equal(300m, result.TotalRevenue);
        Assert.Equal(100m, result.AverageBookingValue);
        Assert.Collection(
            result.ByRoom,
            room =>
            {
                Assert.Equal(roomA, room.RoomId);
                Assert.Equal(2, room.BookingCount);
                Assert.Equal(250m, room.Revenue);
            },
            room => Assert.Equal(roomB, room.RoomId));
    }

    [Fact]
    public async Task Utilization_WhenBookingsHaveDifferentDurations_ComputesPerRoomPercentages()
    {
        Guid roomA = Guid.NewGuid();
        Guid roomB = Guid.NewGuid();
        UtilizationReportHandler handler = new(
            new FakeReportRepository(
                Record(roomA, "Room A", Start, 60, 100m),
                Record(roomA, "Room A", Start.AddHours(2), 120, 150m),
                Record(roomB, "Room B", Start, 30, 50m)),
            TimeZone);

        UtilizationReportDto result = await handler.HandleAsync(ReportDate, ReportDate, CancellationToken.None);

        Assert.Collection(
            result.Rooms,
            room =>
            {
                Assert.Equal(roomA, room.RoomId);
                Assert.Equal(3m, room.BookedHours);
                Assert.Equal(17m, room.AvailableHours);
                Assert.Equal(17.65m, room.UtilizationPercent);
            },
            room =>
            {
                Assert.Equal(roomB, room.RoomId);
                Assert.Equal(0.5m, room.BookedHours);
                Assert.Equal(2.94m, room.UtilizationPercent);
            });
    }

    [Fact]
    public async Task Revenue_WhenNoBookings_ReturnsZeroSummary()
    {
        RevenueReportHandler handler = new(new FakeReportRepository(), TimeZone);

        RevenueReportDto result = await handler.HandleAsync(ReportDate, ReportDate, CancellationToken.None);

        Assert.Equal(0, result.BookingCount);
        Assert.Equal(0m, result.TotalRevenue);
        Assert.Equal(0m, result.AverageBookingValue);
        Assert.Empty(result.ByRoom);
    }

    private static BookingReportRecord Record(
        Guid roomId,
        string roomName,
        DateTimeOffset start,
        int durationMinutes,
        decimal totalPrice) =>
        new(roomId, roomName, start, start.AddMinutes(durationMinutes), totalPrice);
}
