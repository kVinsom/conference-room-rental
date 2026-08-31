using ConferenceRoomRental.Domain.Bookings;
using ConferenceRoomRental.Domain.Rooms;

namespace ConferenceRoomRental.Application.Abstractions;

public interface IRoomRepository
{
    void Add(ConferenceRoom room);

    Task<ConferenceRoom?> GetAsync(Guid id, bool asTracking, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ConferenceRoom>> ListAsync(int skip, int take, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);

    Task<bool> ActiveNameExistsAsync(string name, Guid? excludingId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ConferenceRoom>> FindAvailableAsync(
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        int minimumCapacity,
        CancellationToken cancellationToken);
}

public interface IBookingRepository
{
    void Add(Booking booking);

    Task<Booking?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> HasOverlapAsync(
        Guid roomId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        CancellationToken cancellationToken);

    Task<bool> HasFutureBookingAsync(
        Guid roomId,
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken);
}

public sealed record BookingReportRecord(
    Guid RoomId,
    string RoomName,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    decimal TotalPrice);

public interface IReportRepository
{
    IAsyncEnumerable<BookingReportRecord> StreamConfirmedBookings(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
