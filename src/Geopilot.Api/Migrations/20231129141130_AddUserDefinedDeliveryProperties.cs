using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDefinedDeliveryProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "Deliveries",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Partial",
                table: "Deliveries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PrecursorDeliveryId",
                table: "Deliveries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_PrecursorDeliveryId",
                table: "Deliveries",
                column: "PrecursorDeliveryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Deliveries_Deliveries_PrecursorDeliveryId",
                table: "Deliveries",
                column: "PrecursorDeliveryId",
                principalTable: "Deliveries",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deliveries_Deliveries_PrecursorDeliveryId",
                table: "Deliveries");

            migrationBuilder.DropIndex(
                name: "IX_Deliveries_PrecursorDeliveryId",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "Partial",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "PrecursorDeliveryId",
                table: "Deliveries");
        }
    }
}
