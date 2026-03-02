using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMG.Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIsEmailVerifiedFromUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsEmailVerified",
                schema: "auth",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEmailVerified",
                schema: "auth",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
