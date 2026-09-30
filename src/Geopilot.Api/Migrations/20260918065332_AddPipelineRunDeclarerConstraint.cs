using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineRunDeclarerConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_PipelineRuns_Declarer",
                table: "PipelineRuns",
                sql: "NOT (\"UserId\" IS NOT NULL AND \"MachineClientId\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PipelineRuns_Declarer",
                table: "PipelineRuns");
        }
    }
}
