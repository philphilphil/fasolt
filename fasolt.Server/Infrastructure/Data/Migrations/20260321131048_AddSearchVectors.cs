using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Fasolt.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchVectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "MarkdownFiles",
                type: "tsvector",
                nullable: false,
                computedColumnSql: "to_tsvector('simple', coalesce(\"FileName\",''))",
                stored: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Decks",
                type: "tsvector",
                nullable: false,
                computedColumnSql: "to_tsvector('english', coalesce(\"Name\",'') || ' ' || coalesce(\"Description\",''))",
                stored: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Cards",
                type: "tsvector",
                nullable: false,
                computedColumnSql: "to_tsvector('english', coalesce(\"Front\",'') || ' ' || coalesce(\"Back\",''))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarkdownFiles_SearchVector",
                table: "MarkdownFiles",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_Decks_SearchVector",
                table: "Decks",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_SearchVector",
                table: "Cards",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MarkdownFiles_SearchVector",
                table: "MarkdownFiles");

            migrationBuilder.DropIndex(
                name: "IX_Decks_SearchVector",
                table: "Decks");

            migrationBuilder.DropIndex(
                name: "IX_Cards_SearchVector",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "MarkdownFiles");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Decks");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Cards");
        }
    }
}
