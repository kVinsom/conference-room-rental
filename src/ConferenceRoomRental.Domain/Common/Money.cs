namespace ConferenceRoomRental.Domain.Common;

internal static class Money
{
    public static decimal EnsurePositive(decimal amount, string fieldName)
    {
        if (amount <= 0 || HasMoreThanTwoDecimalPlaces(amount))
        {
            throw new DomainException($"{fieldName} must be positive and have at most two decimal places.");
        }

        return amount;
    }

    public static decimal EnsureNonNegative(decimal amount, string fieldName)
    {
        if (amount < 0 || HasMoreThanTwoDecimalPlaces(amount))
        {
            throw new DomainException($"{fieldName} must be non-negative and have at most two decimal places.");
        }

        return amount;
    }

    public static decimal Round(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

    private static bool HasMoreThanTwoDecimalPlaces(decimal amount) =>
        decimal.Round(amount, 2) != amount;
}
