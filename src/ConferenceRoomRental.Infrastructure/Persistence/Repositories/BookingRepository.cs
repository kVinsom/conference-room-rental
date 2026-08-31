using ConferenceRoomRental.Application.Abstractions;
using ConferenceRoomRental.Domain.Bookings;
using Microsoft.EntityFrameworkCore;

namespace ConferenceRoomRental.Infrastructure.Persistence.Repositories;

internal sealed class BookingRepository(AppDbContext context) : IBookingRepository
{
    public void Add(Booking booking) => context.Bookings.Add(booking);

    public Task<Booking?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        context.Bookings
            .AsNoTracking()
            .Include(x => x.Services)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> HasOverlapAsync(
        Guid roomId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        CancellationToken cancellationToken) =>
        context.Bookings.AnyAsync(
            booking => booking.RoomId == roomId &&
                booking.Status == BookingStatus.Confirmed &&
                booking.StartsAtUtc < endsAtUtc &&
                startsAtUtc < booking.EndsAtUtc,
            cancellationToken);

    public Task<bool> HasFutureBookingAsync(
        Guid roomId,
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken) =>
        context.Bookings.AnyAsync(
            booking => booking.RoomId == roomId &&
                booking.Status == BookingStatus.Confirmed &&
                booking.EndsAtUtc > fromUtc,
            cancellationToken);
}
