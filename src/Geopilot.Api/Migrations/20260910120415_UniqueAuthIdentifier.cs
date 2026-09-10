using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class UniqueAuthIdentifier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_AuthIdentifier",
                table: "Users",
                column: "AuthIdentifier",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_AuthIdentifier",
                table: "Users");
        }
    }
}
