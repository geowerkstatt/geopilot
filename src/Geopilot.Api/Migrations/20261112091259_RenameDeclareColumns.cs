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
            migrationBuilder.RenameColumn(
                name: "Declarability",
                table: "PipelineRuns",
                newName: "PermissionToDeliver");

            migrationBuilder.Sql("UPDATE \"PipelineRuns\" SET \"PermissionToDeliver\" = CASE \"PermissionToDeliver\" WHEN 'Declarable' THEN 'Permitted' WHEN 'NotDeclarable' THEN 'NotPermitted' END WHERE \"PermissionToDeliver\" IS NOT NULL;");

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

            RenameConstraint(migrationBuilder, "Deliveries", "FK_Deliveries_Users_DeclaringUserId", "FK_Deliveries_Users_DeliveredByUserId");
            RenameConstraint(migrationBuilder, "Deliveries", "FK_Deliveries_MachineClients_DeclaringClientId", "FK_Deliveries_MachineClients_DeliveredByClientId");
            RenameConstraint(migrationBuilder, "Deliveries", "CK_Deliveries_Declarer", "CK_Deliveries_DeliveredBy");
            RenameConstraint(migrationBuilder, "PipelineRuns", "CK_PipelineRuns_Declarer", "CK_PipelineRuns_StartedBy");

            RenameMandateValue(migrationBuilder, "includeDeclarer", "includeSupplier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RenameMandateValue(migrationBuilder, "includeSupplier", "includeDeclarer");

            RenameConstraint(migrationBuilder, "Deliveries", "FK_Deliveries_Users_DeliveredByUserId", "FK_Deliveries_Users_DeclaringUserId");
            RenameConstraint(migrationBuilder, "Deliveries", "FK_Deliveries_MachineClients_DeliveredByClientId", "FK_Deliveries_MachineClients_DeclaringClientId");
            RenameConstraint(migrationBuilder, "Deliveries", "CK_Deliveries_DeliveredBy", "CK_Deliveries_Declarer");
            RenameConstraint(migrationBuilder, "PipelineRuns", "CK_PipelineRuns_StartedBy", "CK_PipelineRuns_Declarer");

            migrationBuilder.Sql("UPDATE \"PipelineRuns\" SET \"PermissionToDeliver\" = CASE \"PermissionToDeliver\" WHEN 'Permitted' THEN 'Declarable' WHEN 'NotPermitted' THEN 'NotDeclarable' END WHERE \"PermissionToDeliver\" IS NOT NULL;");

            migrationBuilder.RenameColumn(
                name: "PermissionToDeliver",
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
        }

        // EF Core has no operation to rename a constraint, and dropping and adding it again checks every row.
        private static void RenameConstraint(MigrationBuilder migrationBuilder, string table, string name, string newName)
            => migrationBuilder.Sql($"ALTER TABLE \"{table}\" RENAME CONSTRAINT \"{name}\" TO \"{newName}\";");

        // A channel finds mandate values by key, both in the mandate and in the snapshot of every reaction already created.
        private static void RenameMandateValue(MigrationBuilder migrationBuilder, string key, string newKey)
        {
            migrationBuilder.Sql($$"""
                UPDATE "Mandates" m
                SET "ReactionSetValues" = (
                    SELECT jsonb_object_agg(r.key,
                        CASE WHEN r.value -> '{{key}}' IS NOT NULL
                            THEN (r.value - '{{key}}') || jsonb_build_object('{{newKey}}', r.value -> '{{key}}')
                            ELSE r.value END)
                    FROM jsonb_each(m."ReactionSetValues") r)
                WHERE jsonb_path_exists(m."ReactionSetValues", '$.*.{{key}}');
                """);

            foreach (var part in new[] { "config", "mandate" })
            {
                migrationBuilder.Sql($$"""
                    UPDATE "DeliveryReactions"
                    SET "Config" = "Config" || jsonb_build_object('{{part}}',
                        (("Config" -> '{{part}}') - '{{key}}') || jsonb_build_object('{{newKey}}', "Config" -> '{{part}}' -> '{{key}}'))
                    WHERE "Config" -> '{{part}}' -> '{{key}}' IS NOT NULL;
                    """);
            }
        }
    }
}
