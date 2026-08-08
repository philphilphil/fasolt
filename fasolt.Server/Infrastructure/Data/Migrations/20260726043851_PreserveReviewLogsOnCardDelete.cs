using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fasolt.Server.Infrastructure.Data.Migrations
{
    /// <summary>
    /// ReviewLogs.CardId becomes nullable with ON DELETE SET NULL. Under the cascade
    /// it replaced, an author deleting a card took every subscriber's log for it with
    /// it — retroactively shrinking streaks and totals for reviews those users really
    /// did. The row is the reviewer's, so it outlives the card it pointed at.
    /// Down() re-imposes the cascade and cannot recover orphaned CardIds; those rows
    /// are given the zero GUID and violate the restored foreign key, so it is only
    /// safe on a database where nothing has been orphaned yet.
    /// </summary>
    public partial class PreserveReviewLogsOnCardDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReviewLogs_Cards_CardId",
                table: "ReviewLogs");

            migrationBuilder.AlterColumn<Guid>(
                name: "CardId",
                table: "ReviewLogs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewLogs_Cards_CardId",
                table: "ReviewLogs",
                column: "CardId",
                principalTable: "Cards",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReviewLogs_Cards_CardId",
                table: "ReviewLogs");

            migrationBuilder.AlterColumn<Guid>(
                name: "CardId",
                table: "ReviewLogs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewLogs_Cards_CardId",
                table: "ReviewLogs",
                column: "CardId",
                principalTable: "Cards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
