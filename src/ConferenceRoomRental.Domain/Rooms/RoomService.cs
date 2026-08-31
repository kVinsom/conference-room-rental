using ConferenceRoomRental.Domain.Common;

namespace ConferenceRoomRental.Domain.Rooms;

/// <summary>A paid optional service available for a particular room.</summary>
public sealed class RoomService
{
    private RoomService()
    {
    }

    internal RoomService(Guid roomId, string name, decimal price)
    {
        Id = Guid.NewGuid();
        RoomId = roomId;
        Name = NormalizeName(name);
        Price = Money.EnsureNonNegative(price, "Service price");
    }

    public Guid Id { get; private set; }

    public Guid RoomId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    internal void Update(string name, decimal price)
    {
        Name = NormalizeName(name);
        Price = Money.EnsureNonNegative(price, "Service price");
    }

    internal static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Service name is required.");
        }

        string normalized = name.Trim();
        if (normalized.Length > 100)
        {
            throw new DomainException("Service name cannot exceed 100 characters.");
        }

        return normalized;
    }
}
