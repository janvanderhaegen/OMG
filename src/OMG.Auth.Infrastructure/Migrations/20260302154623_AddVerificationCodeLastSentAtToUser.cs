using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMG.Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVerificationCodeLastSentAtToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerificationCodeLastSentAt",
                schema: "auth",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerificationCodeLastSentAt",
                schema: "auth",
                table: "users");
        }
    }
}
