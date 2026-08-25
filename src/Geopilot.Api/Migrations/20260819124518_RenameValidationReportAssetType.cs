using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations;

/// <inheritdoc />
public partial class RenameValidationReportAssetType : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "Assets"
            SET "AssetType" = 'ProcessedData'
            WHERE "AssetType" = 'ValidationReport';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "Assets"
            SET "AssetType" = 'ValidationReport'
            WHERE "AssetType" = 'ProcessedData';
            """);
    }
}
