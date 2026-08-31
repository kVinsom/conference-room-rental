namespace ConferenceRoomRental.Domain.Common;

/// <summary>Represents a violated business invariant.</summary>
public sealed class DomainException(string message) : Exception(message);
