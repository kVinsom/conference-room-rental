using ConferenceRoomRental.Domain.Common;
using ConferenceRoomRental.Domain.Rooms;

namespace ConferenceRoomRental.Domain.Bookings;

/// <summary>Immutable confirmed reservation with monetary snapshots for auditability.</summary>
public sealed class Booking
{
    private readonly List<BookingService> _services = [];

    private Booking()
    {
    }

    private Booking(
        Guid roomId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        int attendeeCount,
        decimal baseHourlyRate,
        decimal roomPrice,
        DateTimeOffset now)
    {
        if (roomId == Guid.Empty)
        {
            throw new DomainException("Room identifier is required.");
        }

        if (endsAtUtc <= startsAtUtc)
        {
            throw new DomainException("Booking end must be later than its start.");
        }

        if (startsAtUtc.Offset != TimeSpan.Zero || endsAtUtc.Offset != TimeSpan.Zero || now.Offset != TimeSpan.Zero)
        {
            throw new DomainException("Booking timestamps must be expressed in UTC.");
        }

        if (attendeeCount < 1)
        {
            throw new DomainException("Attendee count must be positive.");
        }

        Id = Guid.NewGuid();
        RoomId = roomId;
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        AttendeeCount = attendeeCount;
        BaseHourlyRateSnapshot = Money.EnsurePositive(baseHourlyRate, "Base hourly rate");
        RoomPrice = Money.EnsureNonNegative(roomPrice, "Room price");
        Status = BookingStatus.Confirmed;
        CreatedAtUtc = now;
    }

    public Guid Id { get; private set; }

    public Guid RoomId { get; private set; }

    public DateTimeOffset StartsAtUtc { get; private set; }

    public DateTimeOffset EndsAtUtc { get; private set; }

    public int AttendeeCount { get; private set; }

    public decimal BaseHourlyRateSnapshot { get; private set; }

    public decimal RoomPrice { get; private set; }

    public decimal ServicesPrice { get; private set; }

    public decimal TotalPrice => RoomPrice + ServicesPrice;

    public BookingStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<BookingService> Services => _services;

    public static Booking Create(
        ConferenceRoom room,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        int attendeeCount,
        decimal roomPrice,
        IEnumerable<RoomService> selectedServices,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(selectedServices);

        if (!room.IsActive)
        {
            throw new DomainException("The selected room is archived.");
        }

        if (attendeeCount > room.Capacity)
        {
            throw new DomainException("Attendee count exceeds room capacity.");
        }

        Booking booking = new(
            room.Id,
            startsAtUtc,
            endsAtUtc,
            attendeeCount,
            room.BaseHourlyRate,
            roomPrice,
            now);

        HashSet<Guid> selectedServiceIds = [];
        decimal servicesPrice = 0;
        foreach (RoomService? service in selectedServices)
        {
            if (service is null || service.RoomId != room.Id)
            {
                throw new DomainException("Every selected service must belong to the booked room.");
            }

            if (!selectedServiceIds.Add(service.Id))
            {
                throw new DomainException("Selected services must be unique.");
            }

            booking._services.Add(new BookingService(booking.Id, service.Id, service.Name, service.Price));
            servicesPrice += service.Price;
        }

        booking.ServicesPrice = Money.Round(servicesPrice);
        return booking;
    }
}
