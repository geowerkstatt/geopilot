using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeclarabilityToPipelineRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Declarability",
                table: "PipelineRuns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeclarationFailure",
                table: "PipelineRuns",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Declarability",
                table: "PipelineRuns");

            migrationBuilder.DropColumn(
                name: "DeclarationFailure",
                table: "PipelineRuns");
        }
    }
}
