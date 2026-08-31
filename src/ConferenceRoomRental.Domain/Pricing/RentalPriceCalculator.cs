using ConferenceRoomRental.Domain.Common;

namespace ConferenceRoomRental.Domain.Pricing;

/// <summary>
/// Calculates the room charge in tariff segments. Segment boundaries make cross-period
/// bookings deterministic and avoid applying one multiplier to an entire booking.
/// </summary>
public static class RentalPriceCalculator
{
    public static readonly TimeOnly OpeningTime = new(6, 0);
    public static readonly TimeOnly MorningEnd = new(9, 0);
    public static readonly TimeOnly PeakStart = new(12, 0);
    public static readonly TimeOnly PeakEnd = new(14, 0);
    public static readonly TimeOnly EveningStart = new(18, 0);
    public static readonly TimeOnly ClosingTime = new(23, 0);

    public static RentalQuote Calculate(
        decimal baseHourlyRate,
        DateTime localStart,
        DateTime localEnd,
        IEnumerable<decimal> servicePrices)
    {
        ArgumentNullException.ThrowIfNull(servicePrices);
        Money.EnsurePositive(baseHourlyRate, "Base hourly rate");

        if (localStart.Kind != DateTimeKind.Unspecified || localEnd.Kind != DateTimeKind.Unspecified)
        {
            throw new DomainException("Local booking values must have an unspecified DateTime kind.");
        }

        if (localStart.Date != localEnd.Date || localEnd <= localStart)
        {
            throw new DomainException("A booking must start and end on the same date.");
        }

        TimeOnly start = TimeOnly.FromDateTime(localStart);
        TimeOnly end = TimeOnly.FromDateTime(localEnd);
        if (start < OpeningTime || end > ClosingTime)
        {
            throw new DomainException("Bookings are accepted only between 06:00 and 23:00.");
        }

        List<PriceSegment> segments = new(5);
        DateTime cursor = localStart;
        decimal roomPrice = 0;

        while (cursor < localEnd)
        {
            (PricingPeriod period, decimal multiplier, TimeOnly boundary) = GetTariff(TimeOnly.FromDateTime(cursor));
            DateTime nextBoundary = cursor.Date.Add(boundary.ToTimeSpan());
            DateTime segmentEnd = nextBoundary < localEnd ? nextBoundary : localEnd;
            decimal hours = (decimal)(segmentEnd - cursor).TotalMinutes / 60m;
            decimal amount = Money.Round(baseHourlyRate * hours * multiplier);

            segments.Add(new PriceSegment(
                period,
                TimeOnly.FromDateTime(cursor),
                TimeOnly.FromDateTime(segmentEnd),
                hours,
                multiplier,
                amount));

            roomPrice += amount;
            cursor = segmentEnd;
        }

        decimal servicesPrice = CalculateServicesPrice(servicePrices);
        return new RentalQuote(roomPrice, servicesPrice, roomPrice + servicesPrice, segments.ToArray());
    }

    private static (PricingPeriod Period, decimal Multiplier, TimeOnly Boundary) GetTariff(TimeOnly time) =>
        time switch
        {
            _ when time < MorningEnd => (PricingPeriod.Morning, 0.90m, MorningEnd),
            _ when time < PeakStart => (PricingPeriod.Standard, 1.00m, PeakStart),
            _ when time < PeakEnd => (PricingPeriod.Peak, 1.15m, PeakEnd),
            _ when time < EveningStart => (PricingPeriod.Standard, 1.00m, EveningStart),
            _ => (PricingPeriod.Evening, 0.80m, ClosingTime),
        };

    private static decimal CalculateServicesPrice(IEnumerable<decimal> servicePrices)
    {
        decimal total = 0;
        foreach (decimal servicePrice in servicePrices)
        {
            total += Money.EnsureNonNegative(servicePrice, "Service price");
        }

        return Money.Round(total);
    }
}
