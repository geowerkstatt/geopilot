using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigToRecordingReactionEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppVersion",
                table: "RecordingReactionEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Config",
                table: "RecordingReactionEvents",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppVersion",
                table: "RecordingReactionEvents");

            migrationBuilder.DropColumn(
                name: "Config",
                table: "RecordingReactionEvents");
        }
    }
}
