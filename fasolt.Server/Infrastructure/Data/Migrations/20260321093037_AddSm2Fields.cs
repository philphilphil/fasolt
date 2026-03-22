using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fasolt.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSm2Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DueAt",
                table: "Cards",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EaseFactor",
                table: "Cards",
                type: "double precision",
                nullable: false,
                defaultValue: 2.5);

            migrationBuilder.AddColumn<int>(
                name: "Interval",
                table: "Cards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Repetitions",
                table: "Cards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Cards",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "new");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_UserId_DueAt",
                table: "Cards",
                columns: new[] { "UserId", "DueAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cards_UserId_DueAt",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "EaseFactor",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Interval",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Repetitions",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Cards");
        }
    }
}
