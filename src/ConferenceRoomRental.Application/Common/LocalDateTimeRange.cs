using ConferenceRoomRental.Domain.Pricing;

namespace ConferenceRoomRental.Application.Common;

public sealed record LocalDateTimeRange(
    DateTime LocalStart,
    DateTime LocalEnd,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc)
{
    private const int MaximumBookingDurationMinutes = 17 * 60;

    public static LocalDateTimeRange FromTimes(
        DateOnly date,
        TimeOnly start,
        TimeOnly end,
        TimeZoneInfo businessTimeZone)
    {
        ArgumentNullException.ThrowIfNull(businessTimeZone);

        if (end <= start)
        {
            throw new ValidationException("end", "End time must be later than start time on the same date.");
        }

        return Create(date.ToDateTime(start), date.ToDateTime(end), businessTimeZone);
    }

    public static LocalDateTimeRange FromDuration(
        DateOnly date,
        TimeOnly start,
        int durationMinutes,
        TimeZoneInfo businessTimeZone)
    {
        ArgumentNullException.ThrowIfNull(businessTimeZone);

        if (durationMinutes is <= 0 or > MaximumBookingDurationMinutes)
        {
            throw new ValidationException(
                "durationMinutes",
                $"Duration must be between 1 and {MaximumBookingDurationMinutes} minutes.");
        }

        int endMinuteOfDay = (start.Hour * 60) + start.Minute + durationMinutes;
        if (endMinuteOfDay >= 24 * 60)
        {
            throw new ValidationException("durationMinutes", "The booking must end on the same calendar date.");
        }

        DateTime localStart = date.ToDateTime(start);
        DateTime localEnd = localStart.AddMinutes(durationMinutes);
        return Create(localStart, localEnd, businessTimeZone);
    }

    public static LocalDateTimeRange ForDateRange(
        DateOnly from,
        DateOnly toInclusive,
        TimeZoneInfo businessTimeZone)
    {
        ArgumentNullException.ThrowIfNull(businessTimeZone);

        if (toInclusive < from)
        {
            throw new ValidationException("to", "The report end date cannot be earlier than its start date.");
        }

        if (toInclusive.DayNumber - from.DayNumber > 366)
        {
            throw new ValidationException("to", "A report period cannot exceed 367 days.");
        }

        if (toInclusive == DateOnly.MaxValue)
        {
            throw new ValidationException("to", "The maximum date cannot be used as an inclusive report boundary.");
        }

        DateTime localStart = from.ToDateTime(TimeOnly.MinValue);
        DateTime localEnd = toInclusive.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return Create(localStart, localEnd, businessTimeZone, validateBusinessHours: false);
    }

    private static LocalDateTimeRange Create(
        DateTime localStart,
        DateTime localEnd,
        TimeZoneInfo businessTimeZone,
        bool validateBusinessHours = true)
    {
        if (validateBusinessHours)
        {
            TimeOnly start = TimeOnly.FromDateTime(localStart);
            TimeOnly end = TimeOnly.FromDateTime(localEnd);
            if (start < RentalPriceCalculator.OpeningTime || end > RentalPriceCalculator.ClosingTime)
            {
                throw new ValidationException("time", "Bookings are accepted only between 06:00 and 23:00.");
            }
        }

        if (businessTimeZone.IsInvalidTime(localStart) || businessTimeZone.IsInvalidTime(localEnd))
        {
            throw new ValidationException("time", "The selected local time does not exist because of a daylight-saving transition.");
        }

        if (businessTimeZone.IsAmbiguousTime(localStart) || businessTimeZone.IsAmbiguousTime(localEnd))
        {
            throw new ValidationException("time", "The selected local time is ambiguous because of a daylight-saving transition.");
        }

        DateTimeOffset startUtc = new(TimeZoneInfo.ConvertTimeToUtc(localStart, businessTimeZone));
        DateTimeOffset endUtc = new(TimeZoneInfo.ConvertTimeToUtc(localEnd, businessTimeZone));
        return new LocalDateTimeRange(localStart, localEnd, startUtc, endUtc);
    }
}
