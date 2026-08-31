using ConferenceRoomRental.Domain.Bookings;
using ConferenceRoomRental.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConferenceRoomRental.Infrastructure.Persistence.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings", table =>
        {
            table.HasCheckConstraint("CK_Bookings_TimeRange", "\"EndsAtUtc\" > \"StartsAtUtc\"");
            table.HasCheckConstraint("CK_Bookings_AttendeeCount", "\"AttendeeCount\" > 0");
            table.HasCheckConstraint("CK_Bookings_Prices", "\"RoomPrice\" >= 0 AND \"ServicesPrice\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.StartsAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.EndsAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.BaseHourlyRateSnapshot).HasPrecision(18, 2);
        builder.Property(x => x.RoomPrice).HasPrecision(18, 2);
        builder.Property(x => x.ServicesPrice).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(x => x.TotalPrice);

        builder.HasOne<ConferenceRoom>()
            .WithMany()
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Services)
            .WithOne()
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Services).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(x => new { x.RoomId, x.StartsAtUtc, x.EndsAtUtc });
        builder.HasIndex(x => new { x.Status, x.StartsAtUtc });
    }
}
