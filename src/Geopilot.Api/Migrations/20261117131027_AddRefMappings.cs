using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRefMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bindings",
                table: "PipelineRuns",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RefMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Scope = table.Column<string>(type: "varchar(250)", nullable: false),
                    RefDataUris = table.Column<List<string>>(type: "text[]", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefMappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefMappings_Scope",
                table: "RefMappings",
                column: "Scope",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefMappings");

            migrationBuilder.DropColumn(
                name: "Bindings",
                table: "PipelineRuns");
        }
    }
}
