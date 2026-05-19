using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fasolt.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSourceHeading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceHeading",
                table: "Cards");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceHeading",
                table: "Cards",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }
    }
}
