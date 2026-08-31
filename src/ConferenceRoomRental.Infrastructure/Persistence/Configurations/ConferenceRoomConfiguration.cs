using ConferenceRoomRental.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConferenceRoomRental.Infrastructure.Persistence.Configurations;

internal sealed class ConferenceRoomConfiguration : IEntityTypeConfiguration<ConferenceRoom>
{
    public void Configure(EntityTypeBuilder<ConferenceRoom> builder)
    {
        builder.ToTable("Rooms", table =>
        {
            table.HasCheckConstraint("CK_Rooms_Capacity", "\"Capacity\" BETWEEN 1 AND 10000");
            table.HasCheckConstraint("CK_Rooms_BaseHourlyRate", "\"BaseHourlyRate\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.BaseHourlyRate).HasPrecision(18, 2);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");

        // PostgreSQL's transaction id provides optimistic concurrency without leaking a version field into the domain.
        builder.Property<uint>("xmin").IsRowVersion();

        builder.HasIndex(x => x.NormalizedName)
            .IsUnique()
            .HasFilter("\"IsActive\" = TRUE");

        builder.HasMany(x => x.Services)
            .WithOne()
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Services).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
