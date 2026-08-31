using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConferenceRoomRental.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeBookingReportIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_CreatedAtUtc",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_Status_StartsAtUtc",
                table: "Bookings",
                columns: new[] { "Status", "StartsAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_Status_StartsAtUtc",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CreatedAtUtc",
                table: "Bookings",
                column: "CreatedAtUtc");
        }
    }
}
