using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordingReadModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HeartbeatAt",
                table: "Recordings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PipelineDisplayName",
                table: "Recordings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QueuedAt",
                table: "Recordings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionMandateKey",
                table: "Recordings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recordings_UploadId_Open",
                table: "Recordings",
                column: "UploadId",
                filter: "\"TerminalState\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Recordings_UploadId_Open",
                table: "Recordings");

            migrationBuilder.DropColumn(
                name: "HeartbeatAt",
                table: "Recordings");

            migrationBuilder.DropColumn(
                name: "PipelineDisplayName",
                table: "Recordings");

            migrationBuilder.DropColumn(
                name: "QueuedAt",
                table: "Recordings");

            migrationBuilder.DropColumn(
                name: "SubmissionMandateKey",
                table: "Recordings");
        }
    }
}
