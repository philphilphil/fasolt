using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Fasolt.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixFileSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "MarkdownFiles",
                type: "tsvector",
                nullable: false,
                computedColumnSql: "to_tsvector('simple', regexp_replace(coalesce(\"FileName\",''), '[.\\-_]', ' ', 'g'))",
                stored: true,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector",
                oldComputedColumnSql: "to_tsvector('simple', coalesce(\"FileName\",''))",
                oldStored: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "MarkdownFiles",
                type: "tsvector",
                nullable: false,
                computedColumnSql: "to_tsvector('simple', coalesce(\"FileName\",''))",
                stored: true,
                oldClrType: typeof(NpgsqlTsVector),
                oldType: "tsvector",
                oldComputedColumnSql: "to_tsvector('simple', regexp_replace(coalesce(\"FileName\",''), '[.\\-_]', ' ', 'g'))",
                oldStored: true);
        }
    }
}
