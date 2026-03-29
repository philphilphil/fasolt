using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fasolt.Server.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalProviderUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ExternalProvider_ExternalProviderId",
                table: "AspNetUsers",
                columns: new[] { "ExternalProvider", "ExternalProviderId" },
                unique: true,
                filter: "\"ExternalProvider\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ExternalProvider_ExternalProviderId",
                table: "AspNetUsers");
        }
    }
}
