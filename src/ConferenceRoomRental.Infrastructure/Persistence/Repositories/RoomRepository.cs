using ConferenceRoomRental.Application.Abstractions;
using ConferenceRoomRental.Domain.Bookings;
using ConferenceRoomRental.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace ConferenceRoomRental.Infrastructure.Persistence.Repositories;

internal sealed class RoomRepository(AppDbContext context) : IRoomRepository
{
    public void Add(ConferenceRoom room) => context.Rooms.Add(room);

    public async Task<ConferenceRoom?> GetAsync(Guid id, bool asTracking, CancellationToken cancellationToken)
    {
        IQueryable<ConferenceRoom> query = context.Rooms
            .Include(x => x.Services)
            .Where(x => x.IsActive);

        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ConferenceRoom>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken) =>
        await context.Rooms
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Services)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Skip(skip)
            .Take(take)
            .ToArrayAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken) =>
        context.Rooms.CountAsync(x => x.IsActive, cancellationToken);

    public Task<bool> ActiveNameExistsAsync(
        string name,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        string normalizedName = ConferenceRoom.NormalizeName(name).ToUpperInvariant();
        return context.Rooms.AnyAsync(
            x => x.IsActive && x.NormalizedName == normalizedName && (!excludingId.HasValue || x.Id != excludingId.Value),
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<ConferenceRoom>> FindAvailableAsync(
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        int minimumCapacity,
        CancellationToken cancellationToken) =>
        await context.Rooms
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Services)
            .Where(room => room.IsActive &&
                room.Capacity >= minimumCapacity &&
                !context.Bookings.Any(booking =>
                    booking.RoomId == room.Id &&
                    booking.Status == BookingStatus.Confirmed &&
                    booking.StartsAtUtc < endsAtUtc &&
                    startsAtUtc < booking.EndsAtUtc))
            .OrderBy(x => x.Capacity)
            .ThenBy(x => x.BaseHourlyRate)
            .ThenBy(x => x.Id)
            .ToArrayAsync(cancellationToken);
}
