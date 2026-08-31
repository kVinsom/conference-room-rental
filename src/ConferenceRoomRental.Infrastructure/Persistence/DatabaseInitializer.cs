using ConferenceRoomRental.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace ConferenceRoomRental.Infrastructure.Persistence;

public sealed class DatabaseInitializer(AppDbContext context, TimeProvider timeProvider)
{
    public async Task InitializeAsync(bool applyMigrations, bool seedData, CancellationToken cancellationToken)
    {
        if (applyMigrations)
        {
            await context.Database.MigrateAsync(cancellationToken);
        }

        if (!seedData || await context.Rooms.AnyAsync(cancellationToken))
        {
            return;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ServiceDefinition[] standardServices =
        [
            new("Projector", 500m),
            new("Wi-Fi", 300m),
            new("Sound equipment", 700m),
        ];

        ConferenceRoom[] rooms =
        [
            ConferenceRoom.Create("Room A", 50, 2_000m, standardServices, now),
            ConferenceRoom.Create("Room B", 100, 3_500m, standardServices, now),
            ConferenceRoom.Create("Room C", 30, 1_500m, standardServices, now),
        ];

        context.Rooms.AddRange(rooms);
        await context.SaveChangesAsync(cancellationToken);
    }
}
