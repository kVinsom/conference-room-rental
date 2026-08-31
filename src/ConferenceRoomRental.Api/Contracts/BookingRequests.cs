using System.ComponentModel.DataAnnotations;

namespace ConferenceRoomRental.Api.Contracts;

public sealed record CreateBookingRequest(
    [param: Required] Guid RoomId,
    [param: Required] DateOnly Date,
    [param: Required] TimeOnly Start,
    [param: Range(1, 1_020)] int DurationMinutes,
    [param: Range(1, 10_000)] int AttendeeCount,
    [param: Required, MaxLength(50)] IReadOnlyCollection<Guid> SelectedServiceIds);
