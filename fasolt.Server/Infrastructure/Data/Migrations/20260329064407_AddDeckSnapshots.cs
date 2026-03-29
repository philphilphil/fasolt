using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fasolt.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeckSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeckSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicId = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    DeckId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CardCount = table.Column<int>(type: "integer", nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeckSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeckSnapshots_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeckSnapshots_Decks_DeckId",
                        column: x => x.DeckId,
                        principalTable: "Decks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeckSnapshots_DeckId",
                table: "DeckSnapshots",
                column: "DeckId");

            migrationBuilder.CreateIndex(
                name: "IX_DeckSnapshots_PublicId",
                table: "DeckSnapshots",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeckSnapshots_UserId_DeckId_CreatedAt",
                table: "DeckSnapshots",
                columns: new[] { "UserId", "DeckId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeckSnapshots");
        }
    }
}
