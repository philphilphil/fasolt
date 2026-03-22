using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Fasolt.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class McpFirstPivot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceFile",
                table: "Cards",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Cards" c
                SET "SourceFile" = m."FileName"
                FROM "MarkdownFiles" m
                WHERE c."FileId" = m."Id"
            """);

            migrationBuilder.DropForeignKey(
                name: "FK_Cards_MarkdownFiles_FileId",
                table: "Cards");

            migrationBuilder.DropTable(
                name: "FileHeadings");

            migrationBuilder.DropTable(
                name: "MarkdownFiles");

            migrationBuilder.DropIndex(
                name: "IX_Cards_FileId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "CardType",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "FileId",
                table: "Cards");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_UserId_SourceFile",
                table: "Cards",
                columns: new[] { "UserId", "SourceFile" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cards_UserId_SourceFile",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "SourceFile",
                table: "Cards");

            migrationBuilder.AddColumn<string>(
                name: "CardType",
                table: "Cards",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "FileId",
                table: "Cards",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MarkdownFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false, computedColumnSql: "to_tsvector('simple', regexp_replace(coalesce(\"FileName\",''), '[.\\-_]', ' ', 'g'))", stored: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarkdownFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarkdownFiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileHeadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileHeadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileHeadings_MarkdownFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "MarkdownFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_FileId",
                table: "Cards",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_FileHeadings_FileId",
                table: "FileHeadings",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_MarkdownFiles_SearchVector",
                table: "MarkdownFiles",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_MarkdownFiles_UserId",
                table: "MarkdownFiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MarkdownFiles_UserId_FileName",
                table: "MarkdownFiles",
                columns: new[] { "UserId", "FileName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cards_MarkdownFiles_FileId",
                table: "Cards",
                column: "FileId",
                principalTable: "MarkdownFiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
