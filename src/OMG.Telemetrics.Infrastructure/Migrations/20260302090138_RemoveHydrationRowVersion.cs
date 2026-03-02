using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMG.Telemetrics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHydrationRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                schema: "tele",
                table: "plant_hydration_state");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                schema: "tele",
                table: "plant_hydration_state",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);
        }
    }
}
