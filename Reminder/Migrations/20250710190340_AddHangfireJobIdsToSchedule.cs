using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reminder.Migrations
{
    /// <inheritdoc />
    public partial class AddHangfireJobIdsToSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CallJobId",
                table: "Schedule",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailJobId",
                table: "Schedule",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmsJobId",
                table: "Schedule",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CallJobId",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "EmailJobId",
                table: "Schedule");

            migrationBuilder.DropColumn(
                name: "SmsJobId",
                table: "Schedule");
        }
    }
}
