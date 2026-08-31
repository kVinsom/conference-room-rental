using ConferenceRoomRental.Application.Common;

namespace ConferenceRoomRental.Application.UnitTests;

public sealed class LocalDateTimeRangeTests
{
    private static readonly TimeZoneInfo Kyiv = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kyiv");

    [Fact]
    public void FromDuration_WhenDurationExceedsBusinessWindow_ReturnsValidationError()
    {
        static void act() => LocalDateTimeRange.FromDuration(
            new DateOnly(2030, 1, 1),
            new TimeOnly(6, 0),
            1_021,
            Kyiv);

        Assert.Throws<ValidationException>(act);
    }

    [Fact]
    public void FromDuration_WhenEndWouldCrossMidnight_ReturnsValidationErrorWithoutOverflow()
    {
        static void act() => LocalDateTimeRange.FromDuration(
            DateOnly.MaxValue,
            new TimeOnly(23, 30),
            60,
            Kyiv);

        Assert.Throws<ValidationException>(act);
    }

    [Fact]
    public void ForDateRange_WhenInclusiveEndIsMaximumDate_ReturnsValidationError()
    {
        static void act() => LocalDateTimeRange.ForDateRange(DateOnly.MaxValue, DateOnly.MaxValue, Kyiv);

        Assert.Throws<ValidationException>(act);
    }

    [Fact]
    public void FromTimes_WhenUsingFullBusinessWindow_ConvertsToUtcRange()
    {
        LocalDateTimeRange range = LocalDateTimeRange.FromTimes(
            new DateOnly(2030, 1, 2),
            new TimeOnly(6, 0),
            new TimeOnly(23, 0),
            Kyiv);

        Assert.Equal(TimeSpan.FromHours(17), range.EndsAtUtc - range.StartsAtUtc);
        Assert.Equal(TimeSpan.Zero, range.StartsAtUtc.Offset);
        Assert.Equal(TimeSpan.Zero, range.EndsAtUtc.Offset);
    }

    [Fact]
    public void FromTimes_WhenLocalTimeIsAmbiguous_ReturnsValidationError()
    {
        TimeZoneInfo timeZone = CreateTimeZoneWithBusinessHoursTransition();

        void act() => LocalDateTimeRange.FromTimes(
            new DateOnly(2030, 10, 1),
            new TimeOnly(11, 30),
            new TimeOnly(12, 30),
            timeZone);

        ValidationException exception = Assert.Throws<ValidationException>(act);
        Assert.Contains("ambiguous", Assert.Single(exception.Errors["time"]), StringComparison.OrdinalIgnoreCase);
    }

    private static TimeZoneInfo CreateTimeZoneWithBusinessHoursTransition()
    {
        TimeZoneInfo.TransitionTime daylightStart = TimeZoneInfo.TransitionTime.CreateFixedDateRule(
            new DateTime(1, 1, 1, 10, 0, 0),
            6,
            1);
        TimeZoneInfo.TransitionTime daylightEnd = TimeZoneInfo.TransitionTime.CreateFixedDateRule(
            new DateTime(1, 1, 1, 12, 0, 0),
            10,
            1);
        TimeZoneInfo.AdjustmentRule rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2030, 1, 1),
            new DateTime(2030, 12, 31),
            TimeSpan.FromHours(1),
            daylightStart,
            daylightEnd);

        return TimeZoneInfo.CreateCustomTimeZone(
            "Test/BusinessHoursDst",
            TimeSpan.Zero,
            "Business-hours DST test zone",
            "Standard",
            "Daylight",
            [rule]);
    }
}
