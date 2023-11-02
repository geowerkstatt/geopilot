using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace GeoCop.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "Operate",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    FileTypes = table.Column<string[]>(type: "text[]", nullable: false),
                    SpatialExtent = table.Column<Geometry>(type: "geometry", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operate", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Identifier = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Identifier);
                });

            migrationBuilder.CreateTable(
                name: "Organisations",
                columns: table => new
                {
                    Name = table.Column<string>(type: "text", nullable: false),
                    OperatId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organisations", x => x.Name);
                    table.ForeignKey(
                        name: "FK_Organisations_Operate_OperatId",
                        column: x => x.OperatId,
                        principalTable: "Operate",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Deliveries",
                columns: table => new
                {
                    FileHash = table.Column<string>(type: "text", nullable: false),
                    Filename = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeclaringUserIdentifier = table.Column<string>(type: "text", nullable: false),
                    OperatId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliveries", x => x.FileHash);
                    table.ForeignKey(
                        name: "FK_Deliveries_Operate_OperatId",
                        column: x => x.OperatId,
                        principalTable: "Operate",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Deliveries_Users_DeclaringUserIdentifier",
                        column: x => x.DeclaringUserIdentifier,
                        principalTable: "Users",
                        principalColumn: "Identifier",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganisationUser",
                columns: table => new
                {
                    OrganisationsName = table.Column<string>(type: "text", nullable: false),
                    UsersIdentifier = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationUser", x => new { x.OrganisationsName, x.UsersIdentifier });
                    table.ForeignKey(
                        name: "FK_OrganisationUser_Organisations_OrganisationsName",
                        column: x => x.OrganisationsName,
                        principalTable: "Organisations",
                        principalColumn: "Name",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganisationUser_Users_UsersIdentifier",
                        column: x => x.UsersIdentifier,
                        principalTable: "Users",
                        principalColumn: "Identifier",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_DeclaringUserIdentifier",
                table: "Deliveries",
                column: "DeclaringUserIdentifier");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_OperatId",
                table: "Deliveries",
                column: "OperatId");

            migrationBuilder.CreateIndex(
                name: "IX_Organisations_OperatId",
                table: "Organisations",
                column: "OperatId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationUser_UsersIdentifier",
                table: "OrganisationUser",
                column: "UsersIdentifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Deliveries");

            migrationBuilder.DropTable(
                name: "OrganisationUser");

            migrationBuilder.DropTable(
                name: "Organisations");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Operate");
        }
    }
}
