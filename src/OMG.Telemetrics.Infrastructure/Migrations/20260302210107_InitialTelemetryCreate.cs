using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMG.Telemetrics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialTelemetryCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "telemetry");

            migrationBuilder.CreateTable(
                name: "plants",
                schema: "telemetry",
                columns: table => new
                {
                    PlantId = table.Column<Guid>(type: "uuid", nullable: false),
                    GardenId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeterId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IdealHumidityLevel = table.Column<int>(type: "integer", nullable: false),
                    CurrentHumidityLevel = table.Column<int>(type: "integer", nullable: false),
                    IsWatering = table.Column<bool>(type: "boolean", nullable: false),
                    HasIrrigationLine = table.Column<bool>(type: "boolean", nullable: false),
                    LastTelemetryAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plants", x => x.PlantId);
                });

            migrationBuilder.CreateTable(
                name: "watering_sessions",
                schema: "telemetry",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_watering_sessions", x => x.SessionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plants_GardenId",
                schema: "telemetry",
                table: "plants",
                column: "GardenId");

            migrationBuilder.CreateIndex(
                name: "IX_plants_MeterId",
                schema: "telemetry",
                table: "plants",
                column: "MeterId");

            migrationBuilder.CreateIndex(
                name: "IX_watering_sessions_PlantId",
                schema: "telemetry",
                table: "watering_sessions",
                column: "PlantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plants",
                schema: "telemetry");

            migrationBuilder.DropTable(
                name: "watering_sessions",
                schema: "telemetry");
        }
    }
}
