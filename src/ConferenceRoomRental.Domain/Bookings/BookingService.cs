using ConferenceRoomRental.Domain.Common;
using ConferenceRoomRental.Domain.Rooms;

namespace ConferenceRoomRental.Domain.Bookings;

/// <summary>Immutable price snapshot of a service selected for a booking.</summary>
public sealed class BookingService
{
    private BookingService()
    {
    }

    internal BookingService(Guid bookingId, Guid roomServiceId, string name, decimal price)
    {
        if (bookingId == Guid.Empty || roomServiceId == Guid.Empty)
        {
            throw new DomainException("Booking and room service identifiers are required.");
        }

        Id = Guid.NewGuid();
        BookingId = bookingId;
        RoomServiceId = roomServiceId;
        Name = RoomService.NormalizeName(name);
        Price = Money.EnsureNonNegative(price, "Service price");
    }

    public Guid Id { get; private set; }

    public Guid BookingId { get; private set; }

    public Guid RoomServiceId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public decimal Price { get; private set; }
}
