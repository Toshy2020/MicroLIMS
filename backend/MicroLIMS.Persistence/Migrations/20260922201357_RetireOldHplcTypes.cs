using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetireOldHplcTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HplcAssayResults");

            migrationBuilder.DropColumn(
                name: "HplcInjectionsPerPreparation",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "HplcPreparations",
                table: "TestDefinitions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HplcInjectionsPerPreparation",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HplcPreparations",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HplcAssayResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    SignatureId = table.Column<int>(type: "integer", nullable: false),
                    SystemSuitabilityRunId = table.Column<int>(type: "integer", nullable: false),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    ActionLimit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AlertLimit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ComparisonStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    MeanAssayPercent = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ReplicatesJson = table.Column<string>(type: "jsonb", nullable: false),
                    ReportedResult = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SampleDilution = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SampleWeightMg = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    SpecLimit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StandardDilution = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    StandardMeanArea = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    StandardPurityPercent = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    StandardWeightMg = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HplcAssayResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HplcAssayResults_ElectronicSignatures_SignatureId",
                        column: x => x.SignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HplcAssayResults_SystemSuitabilityRuns_SystemSuitabilityRun~",
                        column: x => x.SystemSuitabilityRunId,
                        principalTable: "SystemSuitabilityRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HplcAssayResults_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HplcAssayResults_Users_EnteredByUserId",
                        column: x => x.EnteredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HplcAssayResults_EnteredAt",
                table: "HplcAssayResults",
                column: "EnteredAt");

            migrationBuilder.CreateIndex(
                name: "IX_HplcAssayResults_EnteredByUserId",
                table: "HplcAssayResults",
                column: "EnteredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcAssayResults_SignatureId",
                table: "HplcAssayResults",
                column: "SignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcAssayResults_SystemSuitabilityRunId",
                table: "HplcAssayResults",
                column: "SystemSuitabilityRunId");

            migrationBuilder.CreateIndex(
                name: "IX_HplcAssayResults_TestOrderId_IsActive",
                table: "HplcAssayResults",
                columns: new[] { "TestOrderId", "IsActive" });
        }
    }
}
