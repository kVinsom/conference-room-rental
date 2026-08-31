using ConferenceRoomRental.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConferenceRoomRental.Infrastructure.Persistence.Configurations;

internal sealed class RoomServiceConfiguration : IEntityTypeConfiguration<RoomService>
{
    public void Configure(EntityTypeBuilder<RoomService> builder)
    {
        builder.ToTable("RoomServices", table =>
            table.HasCheckConstraint("CK_RoomServices_Price", "\"Price\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.RoomId, x.Name }).IsUnique();
    }
}
