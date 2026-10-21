using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddReactionSetToMandate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReactionSetId",
                table: "Mandates",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReactionSetValues",
                table: "Mandates",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "jsonb_build_object()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReactionSetId",
                table: "Mandates");

            migrationBuilder.DropColumn(
                name: "ReactionSetValues",
                table: "Mandates");
        }
    }
}
