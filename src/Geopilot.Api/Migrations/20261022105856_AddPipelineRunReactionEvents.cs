using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineRunReactionEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PipelineRunReactionEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PipelineRunId = table.Column<int>(type: "integer", nullable: false),
                    DeliveryId = table.Column<int>(type: "integer", nullable: false),
                    ReactionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReactionKind = table.Column<string>(type: "text", nullable: false),
                    Implementation = table.Column<string>(type: "text", nullable: false),
                    Event = table.Column<string>(type: "text", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    State = table.Column<string>(type: "text", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineRunReactionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineRunReactionEvents_PipelineRuns_PipelineRunId",
                        column: x => x.PipelineRunId,
                        principalTable: "PipelineRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PipelineRunReactionEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRunReactionEvents_PipelineRunId",
                table: "PipelineRunReactionEvents",
                column: "PipelineRunId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRunReactionEvents_UserId",
                table: "PipelineRunReactionEvents",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PipelineRunReactionEvents");
        }
    }
}
