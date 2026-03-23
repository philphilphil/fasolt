using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fasolt.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "Decks",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true,
                defaultValue: null);

            migrationBuilder.AddColumn<string>(
                name: "PublicId",
                table: "Cards",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true,
                defaultValue: null);

            // Backfill existing Decks with unique 12-char IDs derived from their primary key
            migrationBuilder.Sql("""
                UPDATE "Decks"
                SET "PublicId" = substring(md5("Id"::text || random()::text), 1, 12)
                WHERE "PublicId" IS NULL OR "PublicId" = '';
                """);

            // Backfill existing Cards with unique 12-char IDs derived from their primary key
            migrationBuilder.Sql("""
                UPDATE "Cards"
                SET "PublicId" = substring(md5("Id"::text || random()::text), 1, 12)
                WHERE "PublicId" IS NULL OR "PublicId" = '';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "PublicId",
                table: "Decks",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PublicId",
                table: "Cards",
                type: "character varying(12)",
                maxLength: 12,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(12)",
                oldMaxLength: 12,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Decks_PublicId",
                table: "Decks",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_PublicId",
                table: "Cards",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Decks_PublicId",
                table: "Decks");

            migrationBuilder.DropIndex(
                name: "IX_Cards_PublicId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Cards");
        }
    }
}
