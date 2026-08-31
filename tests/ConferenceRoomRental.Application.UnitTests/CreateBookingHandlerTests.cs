using ConferenceRoomRental.Application.Bookings;
using ConferenceRoomRental.Application.Common;
using ConferenceRoomRental.Domain.Rooms;

namespace ConferenceRoomRental.Application.UnitTests;

public sealed class CreateBookingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");

    [Fact]
    public async Task HandleAsync_WhenRoomIsAvailable_CreatesBookingWithPriceBreakdown()
    {
        ConferenceRoom room = CreateRoom();
        FakeBookingRepository bookingRepository = new();
        FakeUnitOfWork unitOfWork = new();
        CreateBookingHandler handler = CreateHandler(room, bookingRepository, unitOfWork);

        BookingDto result = await handler.HandleAsync(
            CreateCommand(room, selectedServiceIds: [Assert.Single(room.Services).Id]),
            CancellationToken.None);

        Assert.Equal(4_600m, result.RoomPrice);
        Assert.Equal(500m, result.ServicesPrice);
        Assert.Equal(5_100m, result.TotalPrice);
        Assert.Equal("Peak", Assert.Single(result.PriceBreakdown).Period);
        Assert.NotNull(bookingRepository.AddedBooking);
        Assert.Equal(1, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenRoomHasOverlap_ReturnsConflictBeforeWriting()
    {
        ConferenceRoom room = CreateRoom();
        FakeBookingRepository bookingRepository = new() { HasOverlap = true };
        FakeUnitOfWork unitOfWork = new();
        CreateBookingHandler handler = CreateHandler(room, bookingRepository, unitOfWork);

        Task Act() => handler.HandleAsync(CreateCommand(room), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(Act);
        Assert.Null(bookingRepository.AddedBooking);
        Assert.Equal(0, unitOfWork.SaveChangesCount);
    }

    [Fact]
    public async Task HandleAsync_WhenServiceIdentifiersRepeat_ReturnsValidationError()
    {
        ConferenceRoom room = CreateRoom();
        Guid serviceId = Assert.Single(room.Services).Id;
        CreateBookingHandler handler = CreateHandler(room);

        Task Act() => handler.HandleAsync(
            CreateCommand(room, selectedServiceIds: [serviceId, serviceId]),
            CancellationToken.None);

        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(Act);
        Assert.Contains("selectedServiceIds", exception.Errors.Keys);
    }

    [Fact]
    public async Task HandleAsync_WhenServiceBelongsToAnotherRoom_ReturnsValidationError()
    {
        ConferenceRoom room = CreateRoom();
        ConferenceRoom otherRoom = CreateRoom("Room B");
        Guid otherServiceId = Assert.Single(otherRoom.Services).Id;
        CreateBookingHandler handler = CreateHandler(room);

        Task Act() => handler.HandleAsync(
            CreateCommand(room, selectedServiceIds: [otherServiceId]),
            CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(Act);
    }

    [Fact]
    public async Task HandleAsync_WhenStartIsNotInFuture_ReturnsValidationError()
    {
        ConferenceRoom room = CreateRoom();
        CreateBookingHandler handler = CreateHandler(room);
        CreateBookingCommand command = CreateCommand(room) with
        {
            Date = new DateOnly(2029, 12, 31),
            Start = new TimeOnly(12, 0),
        };

        Task Act() => handler.HandleAsync(command, CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(Act);
    }

    [Fact]
    public async Task HandleAsync_WhenRoomIdentifierIsEmpty_DoesNotQueryRepository()
    {
        CreateBookingHandler handler = new(
            new FakeRoomRepository(),
            new FakeBookingRepository(),
            new FakeUnitOfWork(),
            TimeZone,
            new FixedTimeProvider(Now));

        Task Act() => handler.HandleAsync(
            new CreateBookingCommand(
                Guid.Empty,
                new DateOnly(2030, 1, 2),
                new TimeOnly(12, 0),
                120,
                25,
                []),
            CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(Act);
    }

    private static CreateBookingHandler CreateHandler(
        ConferenceRoom room,
        FakeBookingRepository? bookingRepository = null,
        FakeUnitOfWork? unitOfWork = null) =>
        new(
            new FakeRoomRepository(room),
            bookingRepository ?? new FakeBookingRepository(),
            unitOfWork ?? new FakeUnitOfWork(),
            TimeZone,
            new FixedTimeProvider(Now));

    private static CreateBookingCommand CreateCommand(
        ConferenceRoom room,
        IReadOnlyCollection<Guid>? selectedServiceIds = null) =>
        new(
            room.Id,
            new DateOnly(2030, 1, 2),
            new TimeOnly(12, 0),
            120,
            25,
            selectedServiceIds ?? []);

    private static ConferenceRoom CreateRoom(string name = "Room A") =>
        ConferenceRoom.Create(
            name,
            50,
            2_000m,
            [new ServiceDefinition("Projector", 500m)],
            Now);
}
