using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations;

/// <inheritdoc />
public partial class RenameDeliveryMandate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Deliveries_DeliveryMandates_DeliveryMandateId",
            table: "Deliveries");

        migrationBuilder.RenameColumn(
            name: "DeliveryMandateId",
            table: "Deliveries",
            newName: "MandateId");

        migrationBuilder.DropIndex(
            name: "IX_Deliveries_DeliveryMandateId",
            table: "Deliveries");

        migrationBuilder.DropIndex(
            name: "IX_DeliveryMandateOrganisation_OrganisationsId",
            table: "DeliveryMandateOrganisation");

        migrationBuilder.DropForeignKey(
            name: "FK_DeliveryMandateOrganisation_DeliveryMandates_MandatesId",
            table: "DeliveryMandateOrganisation");

        migrationBuilder.DropForeignKey(
            name: "FK_DeliveryMandateOrganisation_Organisations_OrganisationsId",
            table: "DeliveryMandateOrganisation");

        migrationBuilder.DropPrimaryKey(
            name: "PK_DeliveryMandateOrganisation",
            table: "DeliveryMandateOrganisation");

        migrationBuilder.DropPrimaryKey(
            name: "PK_DeliveryMandates",
            table: "DeliveryMandates");

        migrationBuilder.RenameTable(
            name: "DeliveryMandates",
            newName: "Mandates");

        migrationBuilder.RenameTable(
            name: "DeliveryMandateOrganisation",
            newName: "MandateOrganisation");

        migrationBuilder.AddPrimaryKey(
            name: "PK_Mandates",
            table: "Mandates",
            column: "Id");

        migrationBuilder.AddPrimaryKey(
            name: "PK_MandateOrganisation",
            table: "MandateOrganisation",
            columns: new[] { "MandatesId", "OrganisationsId" });

        migrationBuilder.AddForeignKey(
            name: "FK_MandateOrganisation_Mandates_MandateId",
            table: "MandateOrganisation",
            column: "MandatesId",
            principalTable: "Mandates",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_MandateOrganisation_Organisations_OrganisationId",
            table: "MandateOrganisation",
            column: "OrganisationsId",
            principalTable: "Organisations",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.CreateIndex(
            name: "IX_MandateOrganisation_OrganisationId",
            table: "MandateOrganisation",
            column: "OrganisationsId");

        migrationBuilder.AddForeignKey(
            name: "FK_Deliveries_Mandates_MandateId",
            table: "Deliveries",
            column: "MandateId",
            principalTable: "Mandates",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.CreateIndex(
            name: "IX_Deliveries_MandateId",
            table: "Deliveries",
            column: "MandateId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
