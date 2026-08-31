using ConferenceRoomRental.Application.Abstractions;
using ConferenceRoomRental.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace ConferenceRoomRental.Infrastructure.Persistence.Repositories;

internal sealed class ReportRepository(AppDbContext context) : IReportRepository
{
    public IAsyncEnumerable<BookingReportRecord> StreamConfirmedBookings(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc) =>
        (
            from booking in context.Bookings.AsNoTracking()
            join room in context.Rooms.AsNoTracking() on booking.RoomId equals room.Id
            where booking.Status == BookingStatus.Confirmed &&
                booking.StartsAtUtc >= fromUtc &&
                booking.StartsAtUtc < toUtc
            select new BookingReportRecord(
                room.Id,
                room.Name,
                booking.StartsAtUtc,
                booking.EndsAtUtc,
                booking.RoomPrice + booking.ServicesPrice))
            .AsAsyncEnumerable();
}
