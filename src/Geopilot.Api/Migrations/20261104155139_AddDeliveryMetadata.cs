using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MetadataFields",
                table: "Mandates",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "jsonb_build_array()");

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "Deliveries",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "jsonb_build_array()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetadataFields",
                table: "Mandates");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "Deliveries");
        }
    }
}
