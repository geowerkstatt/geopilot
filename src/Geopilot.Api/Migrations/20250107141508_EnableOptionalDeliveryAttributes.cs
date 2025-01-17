using Geopilot.Api.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geopilot.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnableOptionalDeliveryAttributes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvaluateComment",
                table: "Mandates",
                type: "varchar(12)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EvaluatePartial",
                table: "Mandates",
                type: "varchar(12)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EvaluatePrecursorDelivery",
                table: "Mandates",
                type: "varchar(12)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<bool>(
                name: "Partial",
                table: "Deliveries",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.UpdateData(
                table: "Mandates",
                keyColumn: "EvaluateComment",
                keyValue: string.Empty,
                column: "EvaluateComment",
                value: FieldEvaluationType.Optional.ToString());

            migrationBuilder.UpdateData(
                table: "Mandates",
                keyColumn: "EvaluatePartial",
                keyValue: string.Empty,
                column: "EvaluatePartial",
                value: FieldEvaluationType.Required.ToString());

            migrationBuilder.UpdateData(
                table: "Mandates",
                keyColumn: "EvaluatePrecursorDelivery",
                keyValue: string.Empty,
                column: "EvaluatePrecursorDelivery",
                value: FieldEvaluationType.Optional.ToString());

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvaluateComment",
                table: "Mandates");

            migrationBuilder.DropColumn(
                name: "EvaluatePartial",
                table: "Mandates");

            migrationBuilder.DropColumn(
                name: "EvaluatePrecursorDelivery",
                table: "Mandates");

            migrationBuilder.AlterColumn<bool>(
                name: "Partial",
                table: "Deliveries",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);
        }
    }
}
