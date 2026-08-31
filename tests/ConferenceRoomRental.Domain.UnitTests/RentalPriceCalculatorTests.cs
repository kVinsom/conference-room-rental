using ConferenceRoomRental.Domain.Common;
using ConferenceRoomRental.Domain.Pricing;

namespace ConferenceRoomRental.Domain.UnitTests;

public sealed class RentalPriceCalculatorTests
{
    private static readonly DateTime BusinessDate = new(2030, 9, 1);

    [Theory]
    [InlineData(6, 0, 9, 0, 5_400)]
    [InlineData(9, 0, 11, 0, 4_000)]
    [InlineData(12, 0, 14, 0, 4_600)]
    [InlineData(18, 0, 20, 0, 3_200)]
    public void Calculate_WhenBookingFitsOneTariff_AppliesExpectedMultiplier(
        int startHour,
        int startMinute,
        int endHour,
        int endMinute,
        decimal expectedRoomPrice)
    {
        RentalQuote quote = RentalPriceCalculator.Calculate(
            2_000m,
            BusinessDate.AddHours(startHour).AddMinutes(startMinute),
            BusinessDate.AddHours(endHour).AddMinutes(endMinute),
            []);

        Assert.Equal(expectedRoomPrice, quote.RoomPrice);
        Assert.Single(quote.Segments);
    }

    [Fact]
    public void Calculate_WhenBookingCrossesPeakPeriod_PricesEverySegmentSeparately()
    {
        RentalQuote quote = RentalPriceCalculator.Calculate(
            2_000m,
            BusinessDate.AddHours(11),
            BusinessDate.AddHours(15),
            [500m, 300m]);

        Assert.Equal(8_600m, quote.RoomPrice);
        Assert.Equal(800m, quote.ServicesPrice);
        Assert.Equal(9_400m, quote.TotalPrice);
        Assert.Collection(
            quote.Segments,
            segment => Assert.Equal(PricingPeriod.Standard, segment.Period),
            segment => Assert.Equal(PricingPeriod.Peak, segment.Period),
            segment => Assert.Equal(PricingPeriod.Standard, segment.Period));
    }

    [Fact]
    public void Calculate_WhenOutsideBusinessHours_ThrowsDomainException()
    {
        DomainException exception = Assert.Throws<DomainException>(() =>
            RentalPriceCalculator.Calculate(
                2_000m,
                BusinessDate.AddHours(5),
                BusinessDate.AddHours(7),
                []));

        Assert.Contains("06:00", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Calculate_WhenOneServicePriceIsNegative_RejectsItEvenWhenTotalIsPositive()
    {
        static void act() => RentalPriceCalculator.Calculate(
            2_000m,
            BusinessDate.AddHours(9),
            BusinessDate.AddHours(10),
            [-100m, 200m]);

        Assert.Throws<DomainException>(act);
    }

    [Fact]
    public void Calculate_WhenServicePricesAreNull_ThrowsArgumentNullException()
    {
        static void act() => RentalPriceCalculator.Calculate(
            2_000m,
            BusinessDate.AddHours(9),
            BusinessDate.AddHours(10),
            null!);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void Calculate_WhenBookingLastsOneMinute_RoundsSegmentToCurrencyPrecision()
    {
        RentalQuote quote = RentalPriceCalculator.Calculate(
            100m,
            BusinessDate.AddHours(6),
            BusinessDate.AddHours(6).AddMinutes(1),
            []);

        Assert.Equal(1.50m, quote.RoomPrice);
        Assert.Equal(1.50m, Assert.Single(quote.Segments).Amount);
    }

    [Fact]
    public void Calculate_WhenBookingSpansBusinessDay_ProducesFiveBoundedSegments()
    {
        RentalQuote quote = RentalPriceCalculator.Calculate(
            2_000m,
            BusinessDate.AddHours(6),
            BusinessDate.AddHours(23),
            []);

        Assert.Equal(32_000m, quote.RoomPrice);
        Assert.Equal(5, quote.Segments.Count);
    }
}
