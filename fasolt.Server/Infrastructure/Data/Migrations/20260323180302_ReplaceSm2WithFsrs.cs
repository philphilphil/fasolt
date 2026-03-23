using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fasolt.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceSm2WithFsrs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EaseFactor",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Interval",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Repetitions",
                table: "Cards");

            migrationBuilder.AddColumn<double>(
                name: "Difficulty",
                table: "Cards",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Stability",
                table: "Cards",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Step",
                table: "Cards",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("UPDATE \"Cards\" SET \"State\" = 'new' WHERE \"State\" NOT IN ('new', 'learning', 'review', 'relearning')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Stability",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Step",
                table: "Cards");

            migrationBuilder.AddColumn<double>(
                name: "EaseFactor",
                table: "Cards",
                type: "double precision",
                nullable: false,
                defaultValue: 2.5);

            migrationBuilder.AddColumn<int>(
                name: "Interval",
                table: "Cards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Repetitions",
                table: "Cards",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
