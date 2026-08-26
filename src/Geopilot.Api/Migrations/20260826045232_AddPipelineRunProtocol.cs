using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineRunProtocol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PipelineRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    PipelineId = table.Column<string>(type: "text", nullable: false),
                    Definition = table.Column<string>(type: "jsonb", nullable: false),
                    AppVersion = table.Column<string>(type: "text", nullable: false),
                    MandateId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    ClientKind = table.Column<string>(type: "text", nullable: false),
                    UploadId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadStorageLocation = table.Column<string>(type: "text", nullable: false),
                    UploadInitiatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScanState = table.Column<string>(type: "text", nullable: false),
                    ScanDetails = table.Column<string>(type: "text", nullable: true),
                    TerminalState = table.Column<string>(type: "text", nullable: true),
                    TerminalAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineRuns_Mandates_MandateId",
                        column: x => x.MandateId,
                        principalTable: "Mandates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PipelineRuns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PipelineRunFile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PipelineRunId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    DeclaredSize = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineRunFile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineRunFile_PipelineRuns_PipelineRunId",
                        column: x => x.PipelineRunId,
                        principalTable: "PipelineRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PipelineRunSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PipelineRunId = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    StepId = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "jsonb", nullable: false),
                    ProcessImplementation = table.Column<string>(type: "text", nullable: false),
                    ProcessAssemblyName = table.Column<string>(type: "text", nullable: true),
                    ProcessAssemblyVersion = table.Column<string>(type: "text", nullable: true),
                    State = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    StatusMessage = table.Column<string>(type: "jsonb", nullable: true),
                    ConditionMessage = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineRunSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineRunSteps_PipelineRuns_PipelineRunId",
                        column: x => x.PipelineRunId,
                        principalTable: "PipelineRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PipelineRunArtifact",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PipelineRunStepId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    PersistedFileName = table.Column<string>(type: "text", nullable: false),
                    FromUpload = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineRunArtifact", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineRunArtifact_PipelineRunSteps_PipelineRunStepId",
                        column: x => x.PipelineRunStepId,
                        principalTable: "PipelineRunSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PipelineRunCondition",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PipelineRunStepId = table.Column<int>(type: "integer", nullable: false),
                    ConditionId = table.Column<string>(type: "text", nullable: true),
                    Phase = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Expression = table.Column<string>(type: "text", nullable: false),
                    Matched = table.Column<bool>(type: "boolean", nullable: false),
                    EvaluatedValues = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineRunCondition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineRunCondition_PipelineRunSteps_PipelineRunStepId",
                        column: x => x.PipelineRunStepId,
                        principalTable: "PipelineRunSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRunArtifact_PipelineRunStepId",
                table: "PipelineRunArtifact",
                column: "PipelineRunStepId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRunCondition_PipelineRunStepId",
                table: "PipelineRunCondition",
                column: "PipelineRunStepId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRunFile_PipelineRunId",
                table: "PipelineRunFile",
                column: "PipelineRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_JobId",
                table: "PipelineRuns",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_MandateId",
                table: "PipelineRuns",
                column: "MandateId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_UserId",
                table: "PipelineRuns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRunSteps_PipelineRunId_StepId",
                table: "PipelineRunSteps",
                columns: new[] { "PipelineRunId", "StepId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PipelineRunArtifact");

            migrationBuilder.DropTable(
                name: "PipelineRunCondition");

            migrationBuilder.DropTable(
                name: "PipelineRunFile");

            migrationBuilder.DropTable(
                name: "PipelineRunSteps");

            migrationBuilder.DropTable(
                name: "PipelineRuns");
        }
    }
}
