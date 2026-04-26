using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fasolt.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDayStartHourAndTimeZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DayStartHour",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "AspNetUsers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Retroactively bucket existing review-state cards to the default 04:00 UTC rollover.
            // Users who later set a custom timezone or day-start hour will see future reviews respect it.
            migrationBuilder.Sql(@"
                UPDATE ""Cards""
                SET ""DueAt"" = date_trunc('day', ""DueAt"" - interval '4 hour', 'UTC') + interval '4 hour'
                WHERE ""DueAt"" IS NOT NULL
                  AND ""State"" = 'review';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DayStartHour",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "AspNetUsers");
        }
    }
}
