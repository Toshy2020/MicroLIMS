using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EMAfterCleaningBatchModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SampleLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SampleId = table.Column<int>(type: "integer", nullable: false),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    LocationType = table.Column<int>(type: "integer", nullable: false),
                    RoomTestConfigurationId = table.Column<int>(type: "integer", nullable: true),
                    MachinePartConfigurationId = table.Column<int>(type: "integer", nullable: true),
                    DilutionFactor = table.Column<decimal>(type: "numeric", nullable: false),
                    CFUResult = table.Column<decimal>(type: "numeric", nullable: true),
                    CalculatedResult = table.Column<decimal>(type: "numeric", nullable: true),
                    ReportedResult = table.Column<string>(type: "text", nullable: true),
                    AlertLimit = table.Column<string>(type: "text", nullable: true),
                    ActionLimit = table.Column<string>(type: "text", nullable: true),
                    SpecLimit = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: true),
                    EnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EnteredByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SampleLocations_MachinePartConfigurations_MachinePartConfig~",
                        column: x => x.MachinePartConfigurationId,
                        principalTable: "MachinePartConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SampleLocations_RoomTestConfigurations_RoomTestConfiguratio~",
                        column: x => x.RoomTestConfigurationId,
                        principalTable: "RoomTestConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SampleLocations_Samples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SampleLocations_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SampleLocations_MachinePartConfigurationId",
                table: "SampleLocations",
                column: "MachinePartConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleLocations_RoomTestConfigurationId",
                table: "SampleLocations",
                column: "RoomTestConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_SampleLocations_SampleId_TestOrderId",
                table: "SampleLocations",
                columns: new[] { "SampleId", "TestOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_SampleLocations_TestOrderId_MachinePartConfigurationId",
                table: "SampleLocations",
                columns: new[] { "TestOrderId", "MachinePartConfigurationId" },
                unique: true,
                filter: "\"MachinePartConfigurationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SampleLocations_TestOrderId_RoomTestConfigurationId",
                table: "SampleLocations",
                columns: new[] { "TestOrderId", "RoomTestConfigurationId" },
                unique: true,
                filter: "\"RoomTestConfigurationId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SampleLocations");
        }
    }
}
