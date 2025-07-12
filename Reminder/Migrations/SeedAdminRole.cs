using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reminder.Migrations
{
    /// <inheritdoc />
    public partial class SeedAdminRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Insert Admin role
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "Name", "NormalizedName", "ConcurrencyStamp" },
                values: new object[] { "admin-role-id", "Admin", "ADMIN", "admin-concurrency-stamp" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove Admin role
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Name",
                keyValue: "Admin"
            );
        }
    }
} 