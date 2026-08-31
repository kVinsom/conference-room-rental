using ConferenceRoomRental.Domain.Bookings;
using ConferenceRoomRental.Domain.Common;
using ConferenceRoomRental.Domain.Rooms;

namespace ConferenceRoomRental.Domain.UnitTests;

public sealed class ConferenceRoomTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WhenServiceNamesDifferOnlyByCase_RejectsDuplicates()
    {
        DomainException exception = Assert.Throws<DomainException>(() => ConferenceRoom.Create(
            "Room A",
            50,
            2_000m,
            [new ServiceDefinition("Wi-Fi", 300m), new ServiceDefinition("wi-fi", 400m)],
            Now));

        Assert.Contains("unique", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Update_WhenServiceRemains_PreservesItsIdentifier()
    {
        ConferenceRoom room = CreateRoom();
        Guid originalId = Assert.Single(room.Services).Id;

        room.Update(
            "Room A+",
            60,
            2_500m,
            [new ServiceDefinition("Projector", 650m)],
            Now.AddMinutes(1));

        RoomService service = Assert.Single(room.Services);
        Assert.Equal(originalId, service.Id);
        Assert.Equal(650m, service.Price);
    }

    [Fact]
    public void Create_WhenNamesContainOuterWhitespace_NormalizesAggregateValues()
    {
        ConferenceRoom room = ConferenceRoom.Create(
            "  Room A  ",
            50,
            2_000m,
            [new ServiceDefinition("  Projector  ", 500m)],
            Now);

        Assert.Equal("Room A", room.Name);
        Assert.Equal("ROOM A", room.NormalizedName);
        Assert.Equal("Projector", Assert.Single(room.Services).Name);
    }

    [Fact]
    public void Create_WhenServiceNameIsWhitespace_ThrowsDomainExceptionInsteadOfNullReference()
    {
        static void act() => ConferenceRoom.Create(
            "Room A",
            50,
            2_000m,
            [new ServiceDefinition(" ", 500m)],
            Now);

        Assert.Throws<DomainException>(act);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public void Create_WhenCapacityIsOutsideSupportedRange_ThrowsDomainException(int capacity)
    {
        void act() => ConferenceRoom.Create(
            "Room A",
            capacity,
            2_000m,
            [],
            Now);

        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Create_WhenRateHasSubCentPrecision_ThrowsDomainException()
    {
        static void act() => ConferenceRoom.Create("Room A", 50, 1.001m, [], Now);

        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Create_WhenLazyServiceSequenceExceedsLimit_StopsAtFirstExcessItem()
    {
        int enumeratedCount = 0;

        IEnumerable<ServiceDefinition> services = GetServices();
        void act() => ConferenceRoom.Create("Room A", 50, 2_000m, services, Now);

        Assert.Throws<DomainException>(act);
        Assert.Equal(51, enumeratedCount);

        IEnumerable<ServiceDefinition> GetServices()
        {
            for (int index = 1; index <= 1_000; index++)
            {
                enumeratedCount++;
                yield return new ServiceDefinition($"Service {index}", 0m);
            }
        }
    }

    [Fact]
    public void Update_WhenRoomIsArchived_ThrowsDomainException()
    {
        ConferenceRoom room = CreateRoom();
        room.Archive(Now.AddMinutes(1));

        void act() => room.Update("Changed", 10, 100m, [], Now.AddMinutes(2));

        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Booking_CapturesServicePriceSnapshot()
    {
        ConferenceRoom room = CreateRoom();
        RoomService selected = Assert.Single(room.Services);
        Booking booking = Booking.Create(
            room,
            Now.AddDays(1),
            Now.AddDays(1).AddHours(1),
            20,
            2_000m,
            [selected],
            Now);

        room.Update(
            room.Name,
            room.Capacity,
            room.BaseHourlyRate,
            [new ServiceDefinition("Projector", 900m)],
            Now.AddMinutes(1));

        Assert.Equal(500m, Assert.Single(booking.Services).Price);
        Assert.Equal(2_500m, booking.TotalPrice);
    }

    private static ConferenceRoom CreateRoom() => ConferenceRoom.Create(
        "Room A",
        50,
        2_000m,
        [new ServiceDefinition("Projector", 500m)],
        Now);
}
