using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeoCop.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedAttribute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "Deliveries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "Deleted",
                table: "Assets");
        }
    }
}
