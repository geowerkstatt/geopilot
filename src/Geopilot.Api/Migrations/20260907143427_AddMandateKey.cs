using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMandateKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Key",
                table: "Mandates",
                type: "varchar(128)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mandates_Key",
                table: "Mandates",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Mandates_Key",
                table: "Mandates");

            migrationBuilder.DropColumn(
                name: "Key",
                table: "Mandates");
        }
    }
}
