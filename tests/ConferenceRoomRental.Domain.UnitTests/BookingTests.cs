using ConferenceRoomRental.Domain.Bookings;
using ConferenceRoomRental.Domain.Common;
using ConferenceRoomRental.Domain.Rooms;

namespace ConferenceRoomRental.Domain.UnitTests;

public sealed class BookingTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WhenSelectedServiceIsRepeated_RejectsDuplicateCharge()
    {
        ConferenceRoom room = CreateRoom("Room A");
        RoomService service = Assert.Single(room.Services);

        void act() => Booking.Create(
            room,
            Now.AddDays(1),
            Now.AddDays(1).AddHours(1),
            10,
            100m,
            [service, service],
            Now);

        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Create_WhenServiceBelongsToDifferentRoom_RejectsService()
    {
        ConferenceRoom room = CreateRoom("Room A");
        ConferenceRoom otherRoom = CreateRoom("Room B");

        void act() => Booking.Create(
            room,
            Now.AddDays(1),
            Now.AddDays(1).AddHours(1),
            10,
            100m,
            [Assert.Single(otherRoom.Services)],
            Now);

        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Create_WhenRoomPriceIsNegative_RejectsInvalidSnapshot()
    {
        ConferenceRoom room = CreateRoom("Room A");

        void act() => Booking.Create(
            room,
            Now.AddDays(1),
            Now.AddDays(1).AddHours(1),
            10,
            -1m,
            [],
            Now);

        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Create_WithMultipleServices_SumsEverySnapshotExactlyOnce()
    {
        ConferenceRoom room = ConferenceRoom.Create(
            "Room A",
            50,
            2_000m,
            [new ServiceDefinition("Projector", 500m), new ServiceDefinition("Wi-Fi", 300m)],
            Now);

        Booking booking = Booking.Create(
            room,
            Now.AddDays(1),
            Now.AddDays(1).AddHours(1),
            10,
            2_000m,
            room.Services,
            Now);

        Assert.Equal(800m, booking.ServicesPrice);
        Assert.Equal(2_800m, booking.TotalPrice);
    }

    [Fact]
    public void Create_WhenTimestampHasNonUtcOffset_RejectsPersistenceUnsafeValue()
    {
        ConferenceRoom room = CreateRoom("Room A");
        DateTimeOffset localStart = new(2030, 1, 2, 10, 0, 0, TimeSpan.FromHours(2));

        void act() => Booking.Create(
            room,
            localStart,
            localStart.AddHours(1),
            10,
            100m,
            [],
            Now);

        Assert.Throws<DomainException>(act);
    }

    private static ConferenceRoom CreateRoom(string name) =>
        ConferenceRoom.Create(
            name,
            50,
            2_000m,
            [new ServiceDefinition("Projector", 500m)],
            Now);
}
