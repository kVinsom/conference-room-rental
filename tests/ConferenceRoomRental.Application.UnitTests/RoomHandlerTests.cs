using ConferenceRoomRental.Application.Common;
using ConferenceRoomRental.Application.Rooms;
using ConferenceRoomRental.Domain.Common;
using ConferenceRoomRental.Domain.Rooms;

namespace ConferenceRoomRental.Application.UnitTests;

public sealed class RoomHandlerTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_WhenDomainInputIsInvalid_DoesNotQueryOrWriteRepository()
    {
        FakeRoomRepository repository = new();
        FakeUnitOfWork unitOfWork = new();
        CreateRoomHandler handler = new(repository, unitOfWork, new FixedTimeProvider(Now));
        CreateRoomCommand command = new(
            "Room A",
            50,
            2_000m,
            [new ServiceInput(" ", 100m)]);

        Task Act() => handler.HandleAsync(command, CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(Act);
        Assert.Equal(0, repository.ActiveNameExistsCallCount);
        Assert.Null(repository.AddedRoom);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task Create_WhenActiveNameExists_ReturnsConflictWithoutWriting()
    {
        FakeRoomRepository repository = new() { ActiveNameExists = true };
        FakeUnitOfWork unitOfWork = new();
        CreateRoomHandler handler = new(repository, unitOfWork, new FixedTimeProvider(Now));
        CreateRoomCommand command = new(" Room A ", 50, 2_000m, []);

        Task Act() => handler.HandleAsync(command, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(Act);
        Assert.Equal(1, repository.ActiveNameExistsCallCount);
        Assert.Null(repository.AddedRoom);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task List_WhenPaginationIsInvalid_ReturnsValidationError(int page, int pageSize)
    {
        ListRoomsHandler handler = new(new FakeRoomRepository());

        Task Act() => handler.HandleAsync(page, pageSize, CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(Act);
    }

    [Fact]
    public async Task Delete_WhenRoomHasFutureBooking_ReturnsConflictWithoutArchiving()
    {
        ConferenceRoom room = CreateRoom();
        FakeBookingRepository bookings = new() { HasFutureBooking = true };
        FakeUnitOfWork unitOfWork = new();
        DeleteRoomHandler handler = new(
            new FakeRoomRepository(room),
            bookings,
            unitOfWork,
            new FixedTimeProvider(Now));

        Task Act() => handler.HandleAsync(room.Id, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(Act);
        Assert.True(room.IsActive);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    private static ConferenceRoom CreateRoom() =>
        ConferenceRoom.Create("Room A", 50, 2_000m, [], Now);
}
