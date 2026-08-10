using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNameLocalizationToMandate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert the plain text name to jsonb, keeping the existing value as the German entry.
            migrationBuilder.Sql(
                "ALTER TABLE \"Mandates\" " +
                "ALTER COLUMN \"Name\" TYPE jsonb USING jsonb_build_object('de', \"Name\"), " +
                "ALTER COLUMN \"Name\" SET DEFAULT jsonb_build_object();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to plain text, taking the German entry and falling back to an empty string.
            migrationBuilder.Sql(
                "ALTER TABLE \"Mandates\" " +
                "ALTER COLUMN \"Name\" DROP DEFAULT, " +
                "ALTER COLUMN \"Name\" TYPE text USING COALESCE(\"Name\" ->> 'de', '');");
        }
    }
}
