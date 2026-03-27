using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fasolt.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCardFieldLengthLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SourceHeading",
                table: "Cards",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            // Drop the generated SearchVector column and its GIN index before altering Front/Back types
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Cards_SearchVector";""");
            migrationBuilder.Sql("""ALTER TABLE "Cards" DROP COLUMN IF EXISTS "SearchVector";""");

            migrationBuilder.AlterColumn<string>(
                name: "Front",
                table: "Cards",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Back",
                table: "Cards",
                type: "character varying(50000)",
                maxLength: 50000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            // Recreate the generated SearchVector column and GIN index
            migrationBuilder.Sql("""ALTER TABLE "Cards" ADD COLUMN "SearchVector" tsvector GENERATED ALWAYS AS (to_tsvector('english', coalesce("Front",'') || ' ' || coalesce("Back",''))) STORED;""");
            migrationBuilder.Sql("""CREATE INDEX "IX_Cards_SearchVector" ON "Cards" USING gin ("SearchVector");""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SourceHeading",
                table: "Cards",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            // Drop the generated SearchVector column and its GIN index before reverting Front/Back types
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Cards_SearchVector";""");
            migrationBuilder.Sql("""ALTER TABLE "Cards" DROP COLUMN IF EXISTS "SearchVector";""");

            migrationBuilder.AlterColumn<string>(
                name: "Front",
                table: "Cards",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10000)",
                oldMaxLength: 10000);

            migrationBuilder.AlterColumn<string>(
                name: "Back",
                table: "Cards",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50000)",
                oldMaxLength: 50000);

            // Recreate the generated SearchVector column and GIN index with original text types
            migrationBuilder.Sql("""ALTER TABLE "Cards" ADD COLUMN "SearchVector" tsvector GENERATED ALWAYS AS (to_tsvector('english', coalesce("Front",'') || ' ' || coalesce("Back",''))) STORED;""");
            migrationBuilder.Sql("""CREATE INDEX "IX_Cards_SearchVector" ON "Cards" USING gin ("SearchVector");""");
        }
    }
}
