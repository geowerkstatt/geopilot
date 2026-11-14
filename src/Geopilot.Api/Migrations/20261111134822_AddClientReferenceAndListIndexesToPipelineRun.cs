using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddClientReferenceAndListIndexesToPipelineRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientReference",
                table: "PipelineRuns",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_ClientReference",
                table: "PipelineRuns",
                column: "ClientReference",
                filter: "\"ClientReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_StartedAt_Id",
                table: "PipelineRuns",
                columns: new[] { "StartedAt", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PipelineRuns_ClientReference",
                table: "PipelineRuns");

            migrationBuilder.DropIndex(
                name: "IX_PipelineRuns_StartedAt_Id",
                table: "PipelineRuns");

            migrationBuilder.DropColumn(
                name: "ClientReference",
                table: "PipelineRuns");
        }
    }
}
