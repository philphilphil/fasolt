using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fasolt.Server.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Moves the per-user SRS columns off <c>Cards</c> into a new <c>ReviewStates</c>
    /// table keyed by (UserId, CardId). One row is backfilled per card that has actually
    /// been reviewed or suspended; pristine-new cards get no row, since the absence of a
    /// row already means "new".
    ///
    /// WARNING: <c>Down</c> can only restore the card owner's own state. Any state another
    /// user accumulated on a card (possible once decks are shareable) is lost, and so is
    /// the (UserId, CardId) granularity in general. The production rollback story is a DB
    /// snapshot taken before deploying this migration; <c>Down</c> is a dev convenience.
    /// </summary>
    public partial class SplitReviewStateFromCard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewStates",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CardId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stability = table.Column<double>(type: "double precision", nullable: true),
                    Difficulty = table.Column<double>(type: "double precision", nullable: true),
                    Step = table.Column<int>(type: "integer", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "new"),
                    LastReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsSuspended = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewStates", x => new { x.UserId, x.CardId });
                    table.ForeignKey(
                        name: "FK_ReviewStates_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewStates_Cards_CardId",
                        column: x => x.CardId,
                        principalTable: "Cards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewStates_CardId",
                table: "ReviewStates",
                column: "CardId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewStates_UserId_DueAt",
                table: "ReviewStates",
                columns: new[] { "UserId", "DueAt" });

            // Backfill: one row per card that is not pristine-new. The extra
            // Difficulty/Step/DueAt checks are defensive — they can only preserve more
            // data, and any row they produce still differs from the implicit default.
            migrationBuilder.Sql("""
                INSERT INTO "ReviewStates"
                    ("UserId", "CardId", "Stability", "Difficulty", "Step", "DueAt", "State", "LastReviewedAt", "IsSuspended")
                SELECT "UserId", "Id", "Stability", "Difficulty", "Step", "DueAt", "State", "LastReviewedAt", "IsSuspended"
                FROM "Cards"
                WHERE "State" <> 'new'
                   OR "LastReviewedAt" IS NOT NULL
                   OR "IsSuspended"
                   OR "Stability" IS NOT NULL
                   OR "Difficulty" IS NOT NULL
                   OR "Step" IS NOT NULL
                   OR "DueAt" IS NOT NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Cards_UserId_DueAt",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "IsSuspended",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "LastReviewedAt",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Stability",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Step",
                table: "Cards");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Difficulty",
                table: "Cards",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DueAt",
                table: "Cards",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuspended",
                table: "Cards",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastReviewedAt",
                table: "Cards",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Stability",
                table: "Cards",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Cards",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "new");

            migrationBuilder.AddColumn<int>(
                name: "Step",
                table: "Cards",
                type: "integer",
                nullable: true);

            // Best-effort restore of the card owner's own state (see class remarks).
            migrationBuilder.Sql("""
                UPDATE "Cards" c
                SET "Stability" = rs."Stability",
                    "Difficulty" = rs."Difficulty",
                    "Step" = rs."Step",
                    "DueAt" = rs."DueAt",
                    "State" = rs."State",
                    "LastReviewedAt" = rs."LastReviewedAt",
                    "IsSuspended" = rs."IsSuspended"
                FROM "ReviewStates" rs
                WHERE rs."CardId" = c."Id" AND rs."UserId" = c."UserId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_UserId_DueAt",
                table: "Cards",
                columns: new[] { "UserId", "DueAt" });

            migrationBuilder.DropTable(
                name: "ReviewStates");
        }
    }
}
