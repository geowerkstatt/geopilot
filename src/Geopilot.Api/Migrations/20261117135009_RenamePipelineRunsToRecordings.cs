﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <summary>
    /// Renames the tables of the execution protocol from <c>PipelineRun…</c> to <c>Recording…</c>, with their key
    /// columns, keys, indexes and check constraint. Renames only, so the recorded protocols stay and no key is rebuilt:
    /// all pending migrations run as one script within the default command timeout at startup, and a rename changes
    /// the catalog alone. The identity sequences keep their names, nothing addresses them.
    /// </summary>
    public partial class RenamePipelineRunsToRecordings : Migration
    {
        // "{0}" stands for the root of each name: "PipelineRun" before the migration, "Recording" after it.
        private static readonly string[] Tables =
            ["{0}s", "{0}Files", "{0}Steps", "{0}Inputs", "{0}Conditions", "{0}Artifacts", "{0}ReactionEvents"];

        private static readonly (string Table, string Column)[] Columns =
        [
            ("{0}Files", "{0}Id"),
            ("{0}Steps", "{0}Id"),
            ("{0}ReactionEvents", "{0}Id"),
            ("{0}Inputs", "{0}StepId"),
            ("{0}Conditions", "{0}StepId"),
            ("{0}Artifacts", "{0}StepId"),
        ];

        // Renaming a primary key renames its index as well.
        private static readonly (string Table, string Constraint)[] Constraints =
        [
            ("{0}s", "PK_{0}s"),
            ("{0}s", "CK_{0}s_Declarer"),
            ("{0}s", "FK_{0}s_MachineClients_MachineClientId"),
            ("{0}s", "FK_{0}s_Mandates_MandateId"),
            ("{0}s", "FK_{0}s_Users_UserId"),
            ("{0}Files", "PK_{0}Files"),
            ("{0}Files", "FK_{0}Files_{0}s_{0}Id"),
            ("{0}Steps", "PK_{0}Steps"),
            ("{0}Steps", "FK_{0}Steps_{0}s_{0}Id"),
            ("{0}Inputs", "PK_{0}Inputs"),
            ("{0}Inputs", "FK_{0}Inputs_{0}Steps_{0}StepId"),
            ("{0}Conditions", "PK_{0}Conditions"),
            ("{0}Conditions", "FK_{0}Conditions_{0}Steps_{0}StepId"),
            ("{0}Artifacts", "PK_{0}Artifacts"),
            ("{0}Artifacts", "FK_{0}Artifacts_{0}Steps_{0}StepId"),
            ("{0}ReactionEvents", "PK_{0}ReactionEvents"),
            ("{0}ReactionEvents", "FK_{0}ReactionEvents_{0}s_{0}Id"),
            ("{0}ReactionEvents", "FK_{0}ReactionEvents_Users_UserId"),
        ];

        private static readonly (string Table, string Index)[] Indexes =
        [
            ("{0}s", "IX_{0}s_ClientReference"),
            ("{0}s", "IX_{0}s_JobId"),
            ("{0}s", "IX_{0}s_MachineClientId"),
            ("{0}s", "IX_{0}s_MandateId"),
            ("{0}s", "IX_{0}s_StartedAt_Id"),
            ("{0}s", "IX_{0}s_UserId"),
            ("{0}Files", "IX_{0}Files_{0}Id"),
            ("{0}Steps", "IX_{0}Steps_{0}Id_StepId"),
            ("{0}Inputs", "IX_{0}Inputs_{0}StepId"),
            ("{0}Conditions", "IX_{0}Conditions_{0}StepId"),
            ("{0}Artifacts", "IX_{0}Artifacts_{0}StepId"),
            ("{0}ReactionEvents", "IX_{0}ReactionEvents_{0}Id"),
            ("{0}ReactionEvents", "IX_{0}ReactionEvents_UserId"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => Rename(migrationBuilder, "PipelineRun", "Recording");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => Rename(migrationBuilder, "Recording", "PipelineRun");

        private static void Rename(MigrationBuilder migrationBuilder, string from, string to)
        {
            static string Name(string template, string root) => template.Replace("{0}", root, StringComparison.Ordinal);

            foreach (var table in Tables)
            {
                migrationBuilder.RenameTable(name: Name(table, from), newName: Name(table, to));
            }

            foreach (var (table, column) in Columns)
            {
                migrationBuilder.RenameColumn(name: Name(column, from), table: Name(table, to), newName: Name(column, to));
            }

            foreach (var (table, constraint) in Constraints)
            {
                migrationBuilder.Sql($"ALTER TABLE \"{Name(table, to)}\" RENAME CONSTRAINT \"{Name(constraint, from)}\" TO \"{Name(constraint, to)}\";");
            }

            foreach (var (table, index) in Indexes)
            {
                migrationBuilder.RenameIndex(name: Name(index, from), table: Name(table, to), newName: Name(index, to));
            }
        }
    }
}
