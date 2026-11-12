using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameDeclareColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deliveries_MachineClients_DeclaringClientId",
                table: "Deliveries");

            migrationBuilder.DropForeignKey(
                name: "FK_Deliveries_Users_DeclaringUserId",
                table: "Deliveries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PipelineRuns_Declarer",
                table: "PipelineRuns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Deliveries_Declarer",
                table: "Deliveries");

            migrationBuilder.RenameColumn(
                name: "Declarability",
                table: "PipelineRuns",
                newName: "MayDeliver");

            migrationBuilder.Sql("ALTER TABLE \"PipelineRuns\" ALTER COLUMN \"MayDeliver\" TYPE boolean USING \"MayDeliver\" = 'Declarable';");

            migrationBuilder.RenameColumn(
                name: "DeclarationFailure",
                table: "PipelineRuns",
                newName: "DeliveryRejection");

            migrationBuilder.RenameColumn(
                name: "DeclaringUserId",
                table: "Deliveries",
                newName: "DeliveredByUserId");

            migrationBuilder.RenameColumn(
                name: "DeclaringClientId",
                table: "Deliveries",
                newName: "DeliveredByClientId");

            migrationBuilder.RenameIndex(
                name: "IX_Deliveries_DeclaringUserId",
                table: "Deliveries",
                newName: "IX_Deliveries_DeliveredByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Deliveries_DeclaringClientId",
                table: "Deliveries",
                newName: "IX_Deliveries_DeliveredByClientId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PipelineRuns_StartedBy",
                table: "PipelineRuns",
                sql: "NOT (\"UserId\" IS NOT NULL AND \"MachineClientId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Deliveries_DeliveredBy",
                table: "Deliveries",
                sql: "(\"DeliveredByUserId\" IS NULL) <> (\"DeliveredByClientId\" IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_Deliveries_MachineClients_DeliveredByClientId",
                table: "Deliveries",
                column: "DeliveredByClientId",
                principalTable: "MachineClients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Deliveries_Users_DeliveredByUserId",
                table: "Deliveries",
                column: "DeliveredByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deliveries_MachineClients_DeliveredByClientId",
                table: "Deliveries");

            migrationBuilder.DropForeignKey(
                name: "FK_Deliveries_Users_DeliveredByUserId",
                table: "Deliveries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PipelineRuns_StartedBy",
                table: "PipelineRuns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Deliveries_DeliveredBy",
                table: "Deliveries");

            migrationBuilder.Sql("ALTER TABLE \"PipelineRuns\" ALTER COLUMN \"MayDeliver\" TYPE text USING CASE \"MayDeliver\" WHEN true THEN 'Declarable' WHEN false THEN 'NotDeclarable' END;");

            migrationBuilder.RenameColumn(
                name: "MayDeliver",
                table: "PipelineRuns",
                newName: "Declarability");

            migrationBuilder.RenameColumn(
                name: "DeliveryRejection",
                table: "PipelineRuns",
                newName: "DeclarationFailure");

            migrationBuilder.RenameColumn(
                name: "DeliveredByUserId",
                table: "Deliveries",
                newName: "DeclaringUserId");

            migrationBuilder.RenameColumn(
                name: "DeliveredByClientId",
                table: "Deliveries",
                newName: "DeclaringClientId");

            migrationBuilder.RenameIndex(
                name: "IX_Deliveries_DeliveredByUserId",
                table: "Deliveries",
                newName: "IX_Deliveries_DeclaringUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Deliveries_DeliveredByClientId",
                table: "Deliveries",
                newName: "IX_Deliveries_DeclaringClientId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PipelineRuns_Declarer",
                table: "PipelineRuns",
                sql: "NOT (\"UserId\" IS NOT NULL AND \"MachineClientId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Deliveries_Declarer",
                table: "Deliveries",
                sql: "(\"DeclaringUserId\" IS NULL) <> (\"DeclaringClientId\" IS NULL)");

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
        }
    }
}
