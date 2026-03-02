using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMG.Management.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plants",
                schema: "gm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GardenId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Species = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlantationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SurfaceAreaRequired = table.Column<decimal>(type: "numeric", nullable: false),
                    IdealHumidityLevel = table.Column<int>(type: "integer", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plants_gardens_GardenId",
                        column: x => x.GardenId,
                        principalSchema: "gm",
                        principalTable: "gardens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plants_GardenId",
                schema: "gm",
                table: "plants",
                column: "GardenId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plants",
                schema: "gm");
        }
    }
}
