using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class LocalizeReactionMessages : Migration
    {
        private static readonly string[] Tables = ["DeliveryReactions", "PipelineRunReactionEvents"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The messages so far were English only, so the existing text becomes the English entry.
            foreach (var table in Tables)
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE \"{table}\" " +
                    "ALTER COLUMN \"Message\" TYPE jsonb " +
                    "USING CASE WHEN \"Message\" IS NULL THEN NULL ELSE jsonb_build_object('en', \"Message\") END;");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to plain text, keeping the English entry.
            foreach (var table in Tables)
            {
                migrationBuilder.Sql(
                    $"ALTER TABLE \"{table}\" " +
                    "ALTER COLUMN \"Message\" TYPE text USING \"Message\" ->> 'en';");
            }
        }
    }
}
