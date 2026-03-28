using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fasolt.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCardIsSuspendedRenameDeckIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "Decks",
                newName: "IsSuspended");

            migrationBuilder.Sql("UPDATE \"Decks\" SET \"IsSuspended\" = NOT \"IsSuspended\"");

            migrationBuilder.AlterColumn<bool>(
                name: "IsSuspended",
                table: "Decks",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuspended",
                table: "Cards",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSuspended",
                table: "Cards");

            migrationBuilder.RenameColumn(
                name: "IsSuspended",
                table: "Decks",
                newName: "IsActive");

            migrationBuilder.Sql("UPDATE \"Decks\" SET \"IsActive\" = NOT \"IsActive\"");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Decks",
                nullable: false,
                defaultValue: true);
        }
    }
}
