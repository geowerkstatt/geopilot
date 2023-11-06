using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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
                name: "DeliveryMandates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    FileTypes = table.Column<string[]>(type: "text[]", nullable: false),
                    SpatialExtent = table.Column<Geometry>(type: "geometry", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryMandates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Organisations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organisations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuthIdentifier = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryMandateOrganisation",
                columns: table => new
                {
                    MandatesId = table.Column<int>(type: "integer", nullable: false),
                    OrganisationsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryMandateOrganisation", x => new { x.MandatesId, x.OrganisationsId });
                    table.ForeignKey(
                        name: "FK_DeliveryMandateOrganisation_DeliveryMandates_MandatesId",
                        column: x => x.MandatesId,
                        principalTable: "DeliveryMandates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeliveryMandateOrganisation_Organisations_OrganisationsId",
                        column: x => x.OrganisationsId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Deliveries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeclaringUserId = table.Column<int>(type: "integer", nullable: false),
                    DeliveryMandateId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deliveries_DeliveryMandates_DeliveryMandateId",
                        column: x => x.DeliveryMandateId,
                        principalTable: "DeliveryMandates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Deliveries_Users_DeclaringUserId",
                        column: x => x.DeclaringUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganisationUser",
                columns: table => new
                {
                    OrganisationsId = table.Column<int>(type: "integer", nullable: false),
                    UsersId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationUser", x => new { x.OrganisationsId, x.UsersId });
                    table.ForeignKey(
                        name: "FK_OrganisationUser_Organisations_OrganisationsId",
                        column: x => x.OrganisationsId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganisationUser_Users_UsersId",
                        column: x => x.UsersId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    OriginalFilename = table.Column<string>(type: "text", nullable: false),
                    SanitizedFilename = table.Column<string>(type: "text", nullable: false),
                    AssetType = table.Column<string>(type: "varchar(24)", nullable: false),
                    DeliveryId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assets_Deliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "Deliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Assets_DeliveryId",
                table: "Assets",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_DeclaringUserId",
                table: "Deliveries",
                column: "DeclaringUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_DeliveryMandateId",
                table: "Deliveries",
                column: "DeliveryMandateId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryMandateOrganisation_OrganisationsId",
                table: "DeliveryMandateOrganisation",
                column: "OrganisationsId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationUser_UsersId",
                table: "OrganisationUser",
                column: "UsersId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropTable(
                name: "DeliveryMandateOrganisation");

            migrationBuilder.DropTable(
                name: "OrganisationUser");

            migrationBuilder.DropTable(
                name: "Deliveries");

            migrationBuilder.DropTable(
                name: "Organisations");

            migrationBuilder.DropTable(
                name: "DeliveryMandates");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
