using ConferenceRoomRental.Domain.Bookings;
using ConferenceRoomRental.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace ConferenceRoomRental.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ConferenceRoom> Rooms => Set<ConferenceRoom>();

    public DbSet<RoomService> RoomServices => Set<RoomService>();

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<BookingService> BookingServices => Set<BookingService>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
