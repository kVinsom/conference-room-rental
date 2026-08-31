using System.ComponentModel.DataAnnotations;
using ConferenceRoomRental.Application.Rooms;

namespace ConferenceRoomRental.Api.Contracts;

public sealed record RoomServiceRequest(
    [param: Required, StringLength(100)] string Name,
    [param: Range(typeof(decimal), "0", "999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal Price);

public sealed record CreateRoomRequest(
    [param: Required, StringLength(150)] string Name,
    [param: Range(1, 10_000)] int Capacity,
    [param: Range(typeof(decimal), "0.01", "999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal BaseHourlyRate,
    [param: Required, MaxLength(50)] IReadOnlyCollection<RoomServiceRequest> Services);

public sealed record UpdateRoomRequest(
    [param: Required, StringLength(150)] string Name,
    [param: Range(1, 10_000)] int Capacity,
    [param: Range(typeof(decimal), "0.01", "999999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal BaseHourlyRate,
    [param: Required, MaxLength(50)] IReadOnlyCollection<RoomServiceRequest> Services);

internal static class RoomRequestMapping
{
    public static ServiceInput[] ToServiceInputs(this IReadOnlyCollection<RoomServiceRequest> services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.Select(service => new ServiceInput(service.Name, service.Price)).ToArray();
    }
}
