using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fasolt.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeckPublishingAndHandles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CopiedFromDeckPublicId",
                table: "Decks",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CopiedFromHandle",
                table: "Decks",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CopyCount",
                table: "Decks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "Decks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "Decks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Private");

            migrationBuilder.AddColumn<bool>(
                name: "CanPublish",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Handle",
                table: "AspNetUsers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Decks_Visibility_CopyCount",
                table: "Decks",
                columns: new[] { "Visibility", "CopyCount" });

            migrationBuilder.CreateIndex(
                name: "IX_Decks_Visibility_PublishedAt",
                table: "Decks",
                columns: new[] { "Visibility", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Handle",
                table: "AspNetUsers",
                column: "Handle",
                unique: true,
                filter: "\"Handle\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Decks_Visibility_CopyCount",
                table: "Decks");

            migrationBuilder.DropIndex(
                name: "IX_Decks_Visibility_PublishedAt",
                table: "Decks");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_Handle",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CopiedFromDeckPublicId",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "CopiedFromHandle",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "CopyCount",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "CanPublish",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Handle",
                table: "AspNetUsers");
        }
    }
}
