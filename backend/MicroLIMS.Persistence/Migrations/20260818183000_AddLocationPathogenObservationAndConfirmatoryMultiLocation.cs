using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Persistence.DbContext;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MicroLimsDbContext))]
    [Migration("20260818183000_AddLocationPathogenObservationAndConfirmatoryMultiLocation")]
    public partial class AddLocationPathogenObservationAndConfirmatoryMultiLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocationPathogenObservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SampleLocationId = table.Column<int>(type: "integer", nullable: false),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    GrowthObservation = table.Column<int>(type: "integer", nullable: false),
                    SelectiveMediaSnapshot = table.Column<string>(type: "text", nullable: true),
                    ObservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ObservedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationPathogenObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationPathogenObservations_SampleLocations_SampleLocationId",
                        column: x => x.SampleLocationId,
                        principalTable: "SampleLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LocationPathogenObservations_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LocationPathogenObservations_Users_ObservedByUserId",
                        column: x => x.ObservedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocationPathogenObservations_ObservedByUserId",
                table: "LocationPathogenObservations",
                column: "ObservedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationPathogenObservations_SampleLocationId",
                table: "LocationPathogenObservations",
                column: "SampleLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationPathogenObservations_TestOrderId",
                table: "LocationPathogenObservations",
                column: "TestOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationPathogenObservations_SampleLocationId_TestOrderId",
                table: "LocationPathogenObservations",
                columns: new[] { "SampleLocationId", "TestOrderId" });

            // Alter ConfirmatoryPlateObservations
            migrationBuilder.AddColumn<int>(
                name: "SampleLocationId",
                table: "ConfirmatoryPlateObservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LocationPathogenObservationId",
                table: "ConfirmatoryPlateObservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediumIndex",
                table: "ConfirmatoryPlateObservations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmatoryPlateObservations_SampleLocationId",
                table: "ConfirmatoryPlateObservations",
                column: "SampleLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmatoryPlateObservations_LocationPathogenObservationId",
                table: "ConfirmatoryPlateObservations",
                column: "LocationPathogenObservationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmatoryPlateObservations_LocationPathogenObservationI~",
                table: "ConfirmatoryPlateObservations",
                columns: new[] { "LocationPathogenObservationId", "MaterialId" },
                unique: true,
                filter: "\"LocationPathogenObservationId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ConfirmatoryPlateObservations_LocationPathogenObservations_~",
                table: "ConfirmatoryPlateObservations",
                column: "LocationPathogenObservationId",
                principalTable: "LocationPathogenObservations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConfirmatoryPlateObservations_SampleLocations_SampleLocatio~",
                table: "ConfirmatoryPlateObservations",
                column: "SampleLocationId",
                principalTable: "SampleLocations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Add ConfirmatoryMediaCount to TestWorkflowSteps
            migrationBuilder.AddColumn<int>(
                name: "ConfirmatoryMediaCount",
                table: "TestWorkflowSteps",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Seed Salmonella test workflows with ConfirmatoryMediaCount = 2
            migrationBuilder.Sql(
                @"UPDATE ""TestWorkflowSteps""
                  SET ""ConfirmatoryMediaCount"" = 2
                  WHERE ""StepType"" = 4
                  AND ""TestDefinitionId"" IN (
                    SELECT ""Id"" FROM ""TestDefinitions""
                    WHERE ""Code"" ILIKE '%SALMONELLA%'
                  );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConfirmatoryPlateObservations_LocationPathogenObservations_~",
                table: "ConfirmatoryPlateObservations");

            migrationBuilder.DropForeignKey(
                name: "FK_ConfirmatoryPlateObservations_SampleLocations_SampleLocatio~",
                table: "ConfirmatoryPlateObservations");

            migrationBuilder.DropIndex(
                name: "IX_ConfirmatoryPlateObservations_SampleLocationId",
                table: "ConfirmatoryPlateObservations");

            migrationBuilder.DropIndex(
                name: "IX_ConfirmatoryPlateObservations_LocationPathogenObservationId",
                table: "ConfirmatoryPlateObservations");

            migrationBuilder.DropIndex(
                name: "IX_ConfirmatoryPlateObservations_LocationPathogenObservationI~",
                table: "ConfirmatoryPlateObservations");

            migrationBuilder.DropColumn(
                name: "SampleLocationId",
                table: "ConfirmatoryPlateObservations");

            migrationBuilder.DropColumn(
                name: "LocationPathogenObservationId",
                table: "ConfirmatoryPlateObservations");

            migrationBuilder.DropColumn(
                name: "MediumIndex",
                table: "ConfirmatoryPlateObservations");

            migrationBuilder.DropColumn(
                name: "ConfirmatoryMediaCount",
                table: "TestWorkflowSteps");

            migrationBuilder.DropTable(
                name: "LocationPathogenObservations");
        }
    }
}
