namespace ConferenceRoomRental.Domain.Pricing;

public sealed record PriceSegment(
    PricingPeriod Period,
    TimeOnly From,
    TimeOnly To,
    decimal Hours,
    decimal Multiplier,
    decimal Amount);

public sealed record RentalQuote(
    decimal RoomPrice,
    decimal ServicesPrice,
    decimal TotalPrice,
    IReadOnlyCollection<PriceSegment> Segments);
