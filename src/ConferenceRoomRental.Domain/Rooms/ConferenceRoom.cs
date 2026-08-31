using ConferenceRoomRental.Domain.Common;

namespace ConferenceRoomRental.Domain.Rooms;

/// <summary>Aggregate root that owns the room data and its service catalogue.</summary>
public sealed class ConferenceRoom
{
    private readonly List<RoomService> _services = [];

    private ConferenceRoom()
    {
    }

    private ConferenceRoom(string name, int capacity, decimal baseHourlyRate, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        IsActive = true;
        CreatedAtUtc = now;
        UpdateDetails(name, capacity, baseHourlyRate, now);
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public int Capacity { get; private set; }

    public decimal BaseHourlyRate { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public IReadOnlyCollection<RoomService> Services => _services;

    public static ConferenceRoom Create(
        string name,
        int capacity,
        decimal baseHourlyRate,
        IEnumerable<ServiceDefinition> services,
        DateTimeOffset now)
    {
        ConferenceRoom room = new(name, capacity, baseHourlyRate, now);
        room.ReplaceServices(services, now);
        return room;
    }

    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Room name is required.");
        }

        string normalizedName = name.Trim();
        if (normalizedName.Length > 150)
        {
            throw new DomainException("Room name cannot exceed 150 characters.");
        }

        return normalizedName;
    }

    public void Update(
        string name,
        int capacity,
        decimal baseHourlyRate,
        IEnumerable<ServiceDefinition> services,
        DateTimeOffset now)
    {
        EnsureActive();
        UpdateDetails(name, capacity, baseHourlyRate, now);
        ReplaceServices(services, now);
    }

    public void Archive(DateTimeOffset now)
    {
        EnsureActive();
        IsActive = false;
        UpdatedAtUtc = now;
    }

    private void UpdateDetails(string name, int capacity, decimal baseHourlyRate, DateTimeOffset now)
    {
        if (capacity is < 1 or > 10_000)
        {
            throw new DomainException("Room capacity must be between 1 and 10,000.");
        }

        string normalizedName = NormalizeName(name);
        Name = normalizedName;
        NormalizedName = normalizedName.ToUpperInvariant();
        Capacity = capacity;
        BaseHourlyRate = Money.EnsurePositive(baseHourlyRate, "Base hourly rate");
        UpdatedAtUtc = now;
    }

    private void ReplaceServices(IEnumerable<ServiceDefinition> services, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.TryGetNonEnumeratedCount(out int serviceCount) && serviceCount > 50)
        {
            throw new DomainException("A room cannot expose more than 50 optional services.");
        }

        List<ServiceDefinition> normalizedDefinitions = new(serviceCount);
        HashSet<string> serviceNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (ServiceDefinition? definition in services)
        {
            // Enforce the bound while enumerating so an untrusted iterator is never materialized without a limit.
            if (normalizedDefinitions.Count == 50)
            {
                throw new DomainException("A room cannot expose more than 50 optional services.");
            }

            if (definition is null)
            {
                throw new DomainException("Service definitions cannot contain null values.");
            }

            string normalizedName = RoomService.NormalizeName(definition.Name);
            decimal price = Money.EnsureNonNegative(definition.Price, "Service price");
            if (!serviceNames.Add(normalizedName))
            {
                throw new DomainException("Service names must be unique within a room.");
            }

            normalizedDefinitions.Add(new ServiceDefinition(normalizedName, price));
        }

        Dictionary<string, RoomService> currentByName = _services.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        List<RoomService> replacement = new(normalizedDefinitions.Count);

        foreach (ServiceDefinition definition in normalizedDefinitions)
        {
            if (currentByName.Remove(definition.Name, out RoomService? existing))
            {
                existing.Update(definition.Name, definition.Price);
                replacement.Add(existing);
            }
            else
            {
                replacement.Add(new RoomService(Id, definition.Name, definition.Price));
            }
        }

        _services.Clear();
        _services.AddRange(replacement);
        UpdatedAtUtc = now;
    }

    private void EnsureActive()
    {
        if (!IsActive)
        {
            throw new DomainException("An archived room cannot be modified.");
        }
    }
}
