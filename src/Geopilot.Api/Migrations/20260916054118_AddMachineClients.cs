using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineClients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deliveries_Users_DeclaringUserId",
                table: "Deliveries");

            migrationBuilder.AddColumn<int>(
                name: "MachineClientId",
                table: "PipelineRuns",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "DeclaringUserId",
                table: "Deliveries",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "DeclaringClientId",
                table: "Deliveries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MachineClients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuthIdentifier = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "varchar(24)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineClients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MachineClientOrganisation",
                columns: table => new
                {
                    MachineClientsId = table.Column<int>(type: "integer", nullable: false),
                    OrganisationsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineClientOrganisation", x => new { x.MachineClientsId, x.OrganisationsId });
                    table.ForeignKey(
                        name: "FK_MachineClientOrganisation_MachineClients_MachineClientsId",
                        column: x => x.MachineClientsId,
                        principalTable: "MachineClients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MachineClientOrganisation_Organisations_OrganisationsId",
                        column: x => x.OrganisationsId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_MachineClientId",
                table: "PipelineRuns",
                column: "MachineClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_DeclaringClientId",
                table: "Deliveries",
                column: "DeclaringClientId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Deliveries_Declarer",
                table: "Deliveries",
                sql: "(\"DeclaringUserId\" IS NULL) <> (\"DeclaringClientId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_MachineClientOrganisation_OrganisationsId",
                table: "MachineClientOrganisation",
                column: "OrganisationsId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineClients_AuthIdentifier",
                table: "MachineClients",
                column: "AuthIdentifier",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Deliveries_MachineClients_DeclaringClientId",
                table: "Deliveries",
                column: "DeclaringClientId",
                principalTable: "MachineClients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Deliveries_Users_DeclaringUserId",
                table: "Deliveries",
                column: "DeclaringUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PipelineRuns_MachineClients_MachineClientId",
                table: "PipelineRuns",
                column: "MachineClientId",
                principalTable: "MachineClients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deliveries_MachineClients_DeclaringClientId",
                table: "Deliveries");

            migrationBuilder.DropForeignKey(
                name: "FK_Deliveries_Users_DeclaringUserId",
                table: "Deliveries");

            migrationBuilder.DropForeignKey(
                name: "FK_PipelineRuns_MachineClients_MachineClientId",
                table: "PipelineRuns");

            migrationBuilder.DropTable(
                name: "MachineClientOrganisation");

            migrationBuilder.DropTable(
                name: "MachineClients");

            migrationBuilder.DropIndex(
                name: "IX_PipelineRuns_MachineClientId",
                table: "PipelineRuns");

            migrationBuilder.DropIndex(
                name: "IX_Deliveries_DeclaringClientId",
                table: "Deliveries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Deliveries_Declarer",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "MachineClientId",
                table: "PipelineRuns");

            migrationBuilder.DropColumn(
                name: "DeclaringClientId",
                table: "Deliveries");

            migrationBuilder.AlterColumn<int>(
                name: "DeclaringUserId",
                table: "Deliveries",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Deliveries_Users_DeclaringUserId",
                table: "Deliveries",
                column: "DeclaringUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
