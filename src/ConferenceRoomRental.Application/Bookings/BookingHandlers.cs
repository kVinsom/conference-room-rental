using ConferenceRoomRental.Application.Abstractions;
using ConferenceRoomRental.Application.Common;
using ConferenceRoomRental.Domain.Bookings;
using ConferenceRoomRental.Domain.Pricing;
using ConferenceRoomRental.Domain.Rooms;

namespace ConferenceRoomRental.Application.Bookings;

public sealed class CreateBookingHandler(
    IRoomRepository rooms,
    IBookingRepository bookings,
    IUnitOfWork unitOfWork,
    TimeZoneInfo businessTimeZone,
    TimeProvider timeProvider)
{
    public async Task<BookingDto> HandleAsync(CreateBookingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.RoomId == Guid.Empty)
        {
            throw new ValidationException("roomId", "Room identifier is required.");
        }

        ArgumentNullException.ThrowIfNull(command.SelectedServiceIds);

        ConferenceRoom room = await rooms.GetAsync(command.RoomId, asTracking: false, cancellationToken)
            ?? throw new NotFoundException($"Room '{command.RoomId}' was not found.");

        LocalDateTimeRange range = LocalDateTimeRange.FromDuration(
            command.Date,
            command.Start,
            command.DurationMinutes,
            businessTimeZone);

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (range.StartsAtUtc <= now)
        {
            throw new ValidationException("start", "A booking must start in the future.");
        }

        if (command.AttendeeCount > room.Capacity || command.AttendeeCount < 1)
        {
            throw new ValidationException("attendeeCount", $"Attendee count must be between 1 and {room.Capacity}.");
        }

        HashSet<Guid> serviceIds = new(command.SelectedServiceIds);
        if (serviceIds.Count != command.SelectedServiceIds.Count || serviceIds.Contains(Guid.Empty))
        {
            throw new ValidationException(
                "selectedServiceIds",
                "Service identifiers must be non-empty and unique.");
        }

        RoomService[] selectedServices = room.Services.Where(x => serviceIds.Contains(x.Id)).ToArray();
        if (selectedServices.Length != serviceIds.Count)
        {
            throw new ValidationException("selectedServiceIds", "Every selected service must belong to the room.");
        }

        if (await bookings.HasOverlapAsync(room.Id, range.StartsAtUtc, range.EndsAtUtc, cancellationToken))
        {
            throw new ConflictException("The room is no longer available for the requested time.");
        }

        RentalQuote quote = RentalPriceCalculator.Calculate(
            room.BaseHourlyRate,
            range.LocalStart,
            range.LocalEnd,
            selectedServices.Select(x => x.Price));

        Booking booking = Booking.Create(
            room,
            range.StartsAtUtc,
            range.EndsAtUtc,
            command.AttendeeCount,
            quote.RoomPrice,
            selectedServices,
            now);

        bookings.Add(booking);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return BookingMapping.ToDto(booking, quote, businessTimeZone.Id);
    }
}

public sealed class GetBookingHandler(IBookingRepository bookings, TimeZoneInfo businessTimeZone)
{
    public async Task<BookingDto> HandleAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        if (bookingId == Guid.Empty)
        {
            throw new ValidationException("bookingId", "Booking identifier is required.");
        }

        Booking booking = await bookings.GetAsync(bookingId, cancellationToken)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        DateTime localStart = TimeZoneInfo.ConvertTime(booking.StartsAtUtc, businessTimeZone).DateTime;
        DateTime localEnd = TimeZoneInfo.ConvertTime(booking.EndsAtUtc, businessTimeZone).DateTime;
        RentalQuote quote = RentalPriceCalculator.Calculate(
            booking.BaseHourlyRateSnapshot,
            localStart,
            localEnd,
            booking.Services.Select(x => x.Price));

        return BookingMapping.ToDto(booking, quote, businessTimeZone.Id);
    }
}

internal static class BookingMapping
{
    public static BookingDto ToDto(Booking booking, RentalQuote quote, string timeZone) =>
        new(
            booking.Id,
            booking.RoomId,
            booking.StartsAtUtc,
            booking.EndsAtUtc,
            timeZone,
            booking.AttendeeCount,
            booking.BaseHourlyRateSnapshot,
            booking.RoomPrice,
            booking.ServicesPrice,
            booking.TotalPrice,
            "UAH",
            booking.Services.Select(x => new SelectedServiceDto(x.RoomServiceId, x.Name, x.Price)).ToArray(),
            quote.Segments.Select(x => new PriceSegmentDto(
                x.Period.ToString(), x.From, x.To, x.Hours, x.Multiplier, x.Amount)).ToArray());
}
