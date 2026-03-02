using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMG.Management.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Telemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MeterId",
                schema: "gm",
                table: "plants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelemetryApiKey",
                schema: "gm",
                table: "gardens",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MeterId",
                schema: "gm",
                table: "plants");

            migrationBuilder.DropColumn(
                name: "TelemetryApiKey",
                schema: "gm",
                table: "gardens");
        }
    }
}
