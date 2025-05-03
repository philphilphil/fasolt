using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace spaced_md.Server.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class addheadingandheadinglinenr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Heading",
                table: "Cards",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Heading",
                table: "Cards");
        }
    }
}
