namespace ConferenceRoomRental.Domain.Rooms;

/// <summary>Input value used when a room's service catalogue is replaced.</summary>
public sealed record ServiceDefinition(string Name, decimal Price);
