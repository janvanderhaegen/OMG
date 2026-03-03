using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMG.Management.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlantCreatedAndDeletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                schema: "gm",
                table: "plants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "gm",
                table: "plants",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "gm",
                table: "plants");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "gm",
                table: "plants");
        }
    }
}
