using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CountTestAndPathogenIncubationSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Duration",
                table: "Incubations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedReadingAt",
                table: "Incubations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IncubatorEquipmentId",
                table: "Incubations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MediaId",
                table: "Incubations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Temperature",
                table: "Incubations",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CountTestReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    PlateReadings = table.Column<string>(type: "text", nullable: false),
                    DilutionFactor = table.Column<decimal>(type: "numeric", nullable: false),
                    Average = table.Column<decimal>(type: "numeric", nullable: false),
                    CalculatedResult = table.Column<decimal>(type: "numeric", nullable: false),
                    ReportedResult = table.Column<string>(type: "text", nullable: false),
                    AlertLimit = table.Column<string>(type: "text", nullable: true),
                    ActionLimit = table.Column<string>(type: "text", nullable: true),
                    SpecLimit = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    EnteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    EnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountTestReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CountTestReadings_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestDefinitionMedias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    MediaTypeId = table.Column<int>(type: "integer", nullable: false),
                    StepName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDefinitionMedias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestDefinitionMedias_MediaTypes_MediaTypeId",
                        column: x => x.MediaTypeId,
                        principalTable: "MediaTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TestDefinitionMedias_TestDefinitions_TestDefinitionId",
                        column: x => x.TestDefinitionId,
                        principalTable: "TestDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Incubations_IncubatorEquipmentId",
                table: "Incubations",
                column: "IncubatorEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Incubations_MediaId",
                table: "Incubations",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_CountTestReadings_TestOrderId",
                table: "CountTestReadings",
                column: "TestOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitionMedias_MediaTypeId",
                table: "TestDefinitionMedias",
                column: "MediaTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TestDefinitionMedias_TestDefinitionId_MediaTypeId_StepName",
                table: "TestDefinitionMedias",
                columns: new[] { "TestDefinitionId", "MediaTypeId", "StepName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Incubations_Equipment_IncubatorEquipmentId",
                table: "Incubations",
                column: "IncubatorEquipmentId",
                principalTable: "Equipment",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Incubations_Media_MediaId",
                table: "Incubations",
                column: "MediaId",
                principalTable: "Media",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incubations_Equipment_IncubatorEquipmentId",
                table: "Incubations");

            migrationBuilder.DropForeignKey(
                name: "FK_Incubations_Media_MediaId",
                table: "Incubations");

            migrationBuilder.DropTable(
                name: "CountTestReadings");

            migrationBuilder.DropTable(
                name: "TestDefinitionMedias");

            migrationBuilder.DropIndex(
                name: "IX_Incubations_IncubatorEquipmentId",
                table: "Incubations");

            migrationBuilder.DropIndex(
                name: "IX_Incubations_MediaId",
                table: "Incubations");

            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Incubations");

            migrationBuilder.DropColumn(
                name: "ExpectedReadingAt",
                table: "Incubations");

            migrationBuilder.DropColumn(
                name: "IncubatorEquipmentId",
                table: "Incubations");

            migrationBuilder.DropColumn(
                name: "MediaId",
                table: "Incubations");

            migrationBuilder.DropColumn(
                name: "Temperature",
                table: "Incubations");
        }
    }
}
