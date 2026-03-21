using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpacedMd.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMarkdownFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarkdownFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
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
                    Text = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
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
                name: "IX_FileHeadings_FileId",
                table: "FileHeadings",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_MarkdownFiles_UserId",
                table: "MarkdownFiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MarkdownFiles_UserId_FileName",
                table: "MarkdownFiles",
                columns: new[] { "UserId", "FileName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileHeadings");

            migrationBuilder.DropTable(
                name: "MarkdownFiles");
        }
    }
}
