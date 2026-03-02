using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMG.Telemetrics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialTelemetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tele");

            migrationBuilder.CreateTable(
                name: "plant_hydration_state",
                schema: "tele",
                columns: table => new
                {
                    PlantId = table.Column<Guid>(type: "uuid", nullable: false),
                    GardenId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlantType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IdealHumidityLevel = table.Column<int>(type: "integer", nullable: false),
                    CurrentHumidity = table.Column<int>(type: "integer", nullable: false),
                    LastIrrigationStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastIrrigationEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsWatering = table.Column<bool>(type: "boolean", nullable: false),
                    HasIrrigationLine = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plant_hydration_state", x => x.PlantId);
                });

            migrationBuilder.CreateTable(
                name: "watering_sessions",
                schema: "tele",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlantId = table.Column<Guid>(type: "uuid", nullable: false),
                    GardenId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_watering_sessions", x => x.SessionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plant_hydration_state_GardenId",
                schema: "tele",
                table: "plant_hydration_state",
                column: "GardenId");

            migrationBuilder.CreateIndex(
                name: "IX_watering_sessions_EndsAt",
                schema: "tele",
                table: "watering_sessions",
                column: "EndsAt");

            migrationBuilder.CreateIndex(
                name: "IX_watering_sessions_PlantId_Status",
                schema: "tele",
                table: "watering_sessions",
                columns: new[] { "PlantId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plant_hydration_state",
                schema: "tele");

            migrationBuilder.DropTable(
                name: "watering_sessions",
                schema: "tele");
        }
    }
}
