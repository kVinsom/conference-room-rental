using ConferenceRoomRental.Application.Abstractions;
using ConferenceRoomRental.Domain.Bookings;
using ConferenceRoomRental.Domain.Rooms;

namespace ConferenceRoomRental.Application.UnitTests;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeRoomRepository(params ConferenceRoom[] rooms) : IRoomRepository
{
    private readonly List<ConferenceRoom> _rooms = new(rooms);

    public bool ActiveNameExists { get; set; }

    public int ActiveNameExistsCallCount { get; private set; }

    public ConferenceRoom? AddedRoom { get; private set; }

    public void Add(ConferenceRoom room)
    {
        AddedRoom = room;
        _rooms.Add(room);
    }

    public Task<ConferenceRoom?> GetAsync(Guid id, bool asTracking, CancellationToken cancellationToken) =>
        Task.FromResult(_rooms.SingleOrDefault(room => room.Id == id));

    public Task<IReadOnlyCollection<ConferenceRoom>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<ConferenceRoom>>(_rooms.Skip(skip).Take(take).ToArray());

    public Task<int> CountAsync(CancellationToken cancellationToken) => Task.FromResult(_rooms.Count);

    public Task<bool> ActiveNameExistsAsync(
        string name,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        ActiveNameExistsCallCount++;
        return Task.FromResult(ActiveNameExists);
    }

    public Task<IReadOnlyCollection<ConferenceRoom>> FindAvailableAsync(
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        int minimumCapacity,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<ConferenceRoom>>(
            _rooms.Where(room => room.Capacity >= minimumCapacity).ToArray());
}

internal sealed class FakeBookingRepository : IBookingRepository
{
    public bool HasOverlap { get; set; }

    public bool HasFutureBooking { get; set; }

    public Booking? AddedBooking { get; private set; }

    public void Add(Booking booking) => AddedBooking = booking;

    public Task<Booking?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(AddedBooking?.Id == id ? AddedBooking : null);

    public Task<bool> HasOverlapAsync(
        Guid roomId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        CancellationToken cancellationToken) => Task.FromResult(HasOverlap);

    public Task<bool> HasFutureBookingAsync(
        Guid roomId,
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken) => Task.FromResult(HasFutureBooking);
}

internal sealed class FakeReportRepository(params BookingReportRecord[] records) : IReportRepository
{
    public async IAsyncEnumerable<BookingReportRecord> StreamConfirmedBookings(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        await Task.CompletedTask;

        foreach (BookingReportRecord record in records)
        {
            yield return record;
        }
    }
}
