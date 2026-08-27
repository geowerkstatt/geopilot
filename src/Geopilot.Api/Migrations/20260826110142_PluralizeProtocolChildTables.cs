using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class PluralizeProtocolChildTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PipelineRunArtifact_PipelineRunSteps_PipelineRunStepId",
                table: "PipelineRunArtifact");

            migrationBuilder.DropForeignKey(
                name: "FK_PipelineRunCondition_PipelineRunSteps_PipelineRunStepId",
                table: "PipelineRunCondition");

            migrationBuilder.DropForeignKey(
                name: "FK_PipelineRunFile_PipelineRuns_PipelineRunId",
                table: "PipelineRunFile");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PipelineRunFile",
                table: "PipelineRunFile");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PipelineRunCondition",
                table: "PipelineRunCondition");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PipelineRunArtifact",
                table: "PipelineRunArtifact");

            migrationBuilder.RenameTable(
                name: "PipelineRunFile",
                newName: "PipelineRunFiles");

            migrationBuilder.RenameTable(
                name: "PipelineRunCondition",
                newName: "PipelineRunConditions");

            migrationBuilder.RenameTable(
                name: "PipelineRunArtifact",
                newName: "PipelineRunArtifacts");

            migrationBuilder.RenameIndex(
                name: "IX_PipelineRunFile_PipelineRunId",
                table: "PipelineRunFiles",
                newName: "IX_PipelineRunFiles_PipelineRunId");

            migrationBuilder.RenameIndex(
                name: "IX_PipelineRunCondition_PipelineRunStepId",
                table: "PipelineRunConditions",
                newName: "IX_PipelineRunConditions_PipelineRunStepId");

            migrationBuilder.RenameIndex(
                name: "IX_PipelineRunArtifact_PipelineRunStepId",
                table: "PipelineRunArtifacts",
                newName: "IX_PipelineRunArtifacts_PipelineRunStepId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PipelineRunFiles",
                table: "PipelineRunFiles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PipelineRunConditions",
                table: "PipelineRunConditions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PipelineRunArtifacts",
                table: "PipelineRunArtifacts",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PipelineRunArtifacts_PipelineRunSteps_PipelineRunStepId",
                table: "PipelineRunArtifacts",
                column: "PipelineRunStepId",
                principalTable: "PipelineRunSteps",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PipelineRunConditions_PipelineRunSteps_PipelineRunStepId",
                table: "PipelineRunConditions",
                column: "PipelineRunStepId",
                principalTable: "PipelineRunSteps",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PipelineRunFiles_PipelineRuns_PipelineRunId",
                table: "PipelineRunFiles",
                column: "PipelineRunId",
                principalTable: "PipelineRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PipelineRunArtifacts_PipelineRunSteps_PipelineRunStepId",
                table: "PipelineRunArtifacts");

            migrationBuilder.DropForeignKey(
                name: "FK_PipelineRunConditions_PipelineRunSteps_PipelineRunStepId",
                table: "PipelineRunConditions");

            migrationBuilder.DropForeignKey(
                name: "FK_PipelineRunFiles_PipelineRuns_PipelineRunId",
                table: "PipelineRunFiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PipelineRunFiles",
                table: "PipelineRunFiles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PipelineRunConditions",
                table: "PipelineRunConditions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PipelineRunArtifacts",
                table: "PipelineRunArtifacts");

            migrationBuilder.RenameTable(
                name: "PipelineRunFiles",
                newName: "PipelineRunFile");

            migrationBuilder.RenameTable(
                name: "PipelineRunConditions",
                newName: "PipelineRunCondition");

            migrationBuilder.RenameTable(
                name: "PipelineRunArtifacts",
                newName: "PipelineRunArtifact");

            migrationBuilder.RenameIndex(
                name: "IX_PipelineRunFiles_PipelineRunId",
                table: "PipelineRunFile",
                newName: "IX_PipelineRunFile_PipelineRunId");

            migrationBuilder.RenameIndex(
                name: "IX_PipelineRunConditions_PipelineRunStepId",
                table: "PipelineRunCondition",
                newName: "IX_PipelineRunCondition_PipelineRunStepId");

            migrationBuilder.RenameIndex(
                name: "IX_PipelineRunArtifacts_PipelineRunStepId",
                table: "PipelineRunArtifact",
                newName: "IX_PipelineRunArtifact_PipelineRunStepId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PipelineRunFile",
                table: "PipelineRunFile",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PipelineRunCondition",
                table: "PipelineRunCondition",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PipelineRunArtifact",
                table: "PipelineRunArtifact",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PipelineRunArtifact_PipelineRunSteps_PipelineRunStepId",
                table: "PipelineRunArtifact",
                column: "PipelineRunStepId",
                principalTable: "PipelineRunSteps",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PipelineRunCondition_PipelineRunSteps_PipelineRunStepId",
                table: "PipelineRunCondition",
                column: "PipelineRunStepId",
                principalTable: "PipelineRunSteps",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PipelineRunFile_PipelineRuns_PipelineRunId",
                table: "PipelineRunFile",
                column: "PipelineRunId",
                principalTable: "PipelineRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
