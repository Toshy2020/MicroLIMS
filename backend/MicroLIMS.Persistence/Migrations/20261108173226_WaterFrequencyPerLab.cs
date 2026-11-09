using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WaterFrequencyPerLab : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MonitoringOccurrences_Target_Period",
                table: "MonitoringOccurrences");

            migrationBuilder.AddColumn<int>(
                name: "LabSectionId",
                table: "MonitoringOccurrences",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(@"UPDATE ""MonitoringOccurrences"" SET ""LabSectionId"" = (SELECT ""Id"" FROM ""DocumentSections"" WHERE ""Code"" = 'MICRO') WHERE ""TargetType"" = 0;");

            migrationBuilder.CreateTable(
                name: "WaterPointLabFrequencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    WaterSamplingPointId = table.Column<int>(type: "integer", nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    FrequencyType = table.Column<int>(type: "integer", nullable: true),
                    FrequencyInterval = table.Column<int>(type: "integer", nullable: true),
                    SamplingWeekday = table.Column<int>(type: "integer", nullable: true),
                    FrequencyAnchorMonth = table.Column<int>(type: "integer", nullable: true),
                    FrequencyWeekOfMonth = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterPointLabFrequencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaterPointLabFrequencies_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WaterPointLabFrequencies_WaterSamplingPoints_WaterSamplingP~",
                        column: x => x.WaterSamplingPointId,
                        principalTable: "WaterSamplingPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringOccurrences_LabSectionId",
                table: "MonitoringOccurrences",
                column: "LabSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringOccurrences_Target_Period",
                table: "MonitoringOccurrences",
                columns: new[] { "TargetType", "WaterSamplingPointId", "RoomId", "MachineId", "LabSectionId", "PeriodKey", "Sequence" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_WaterPointLabFrequencies_SectionId",
                table: "WaterPointLabFrequencies",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_WaterPointLabFrequencies_WaterSamplingPointId_SectionId",
                table: "WaterPointLabFrequencies",
                columns: new[] { "WaterSamplingPointId", "SectionId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MonitoringOccurrences_DocumentSections_LabSectionId",
                table: "MonitoringOccurrences",
                column: "LabSectionId",
                principalTable: "DocumentSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MonitoringOccurrences_DocumentSections_LabSectionId",
                table: "MonitoringOccurrences");

            migrationBuilder.DropTable(
                name: "WaterPointLabFrequencies");

            migrationBuilder.DropIndex(
                name: "IX_MonitoringOccurrences_LabSectionId",
                table: "MonitoringOccurrences");

            migrationBuilder.DropIndex(
                name: "IX_MonitoringOccurrences_Target_Period",
                table: "MonitoringOccurrences");

            migrationBuilder.DropColumn(
                name: "LabSectionId",
                table: "MonitoringOccurrences");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringOccurrences_Target_Period",
                table: "MonitoringOccurrences",
                columns: new[] { "TargetType", "WaterSamplingPointId", "RoomId", "MachineId", "PeriodKey", "Sequence" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}
