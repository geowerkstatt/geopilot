using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIsPublicToMandate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "Mandates",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "Mandates");
        }
    }
}
