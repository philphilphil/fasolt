using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace spaced_md.Server.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class refactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UploadedAt",
                table: "MarkdownFiles",
                newName: "Md5");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "MarkdownFiles",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "HeadingLineNr",
                table: "Cards",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsageType",
                table: "Cards",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "MarkdownFiles");

            migrationBuilder.DropColumn(
                name: "HeadingLineNr",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "UsageType",
                table: "Cards");

            migrationBuilder.RenameColumn(
                name: "Md5",
                table: "MarkdownFiles",
                newName: "UploadedAt");
        }
    }
}
