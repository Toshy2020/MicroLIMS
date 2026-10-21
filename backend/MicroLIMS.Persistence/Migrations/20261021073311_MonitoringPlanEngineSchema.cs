using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MonitoringPlanEngineSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MonitoringPlanRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GeneratedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ApprovalSignatureId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringPlanRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitoringPlanRevisions_ElectronicSignatures_ApprovalSignat~",
                        column: x => x.ApprovalSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MonitoringOccurrences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedByRevisionId = table.Column<int>(type: "integer", nullable: false),
                    RetiredByRevisionId = table.Column<int>(type: "integer", nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    WaterSamplingPointId = table.Column<int>(type: "integer", nullable: true),
                    RoomId = table.Column<int>(type: "integer", nullable: true),
                    MachineId = table.Column<int>(type: "integer", nullable: true),
                    PeriodKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    FrequencySnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    WindowStart = table.Column<DateOnly>(type: "date", nullable: false),
                    WindowEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    OriginalTargetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OriginalWindowEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    ExceptionState = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringOccurrences", x => x.Id);
                    table.CheckConstraint("CK_MonitoringOccurrences_OneTarget", "(CASE WHEN \"WaterSamplingPointId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"RoomId\" IS NULL THEN 0 ELSE 1 END + CASE WHEN \"MachineId\" IS NULL THEN 0 ELSE 1 END) = 1");
                    table.ForeignKey(
                        name: "FK_MonitoringOccurrences_MonitoringPlanRevisions_CreatedByRevi~",
                        column: x => x.CreatedByRevisionId,
                        principalTable: "MonitoringPlanRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonitoringOccurrences_MonitoringPlanRevisions_RetiredByRevi~",
                        column: x => x.RetiredByRevisionId,
                        principalTable: "MonitoringPlanRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MonitoringOccurrenceEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MonitoringOccurrenceId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    PerformedByUserId = table.Column<int>(type: "integer", nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ElectronicSignatureId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringOccurrenceEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitoringOccurrenceEvents_ElectronicSignatures_ElectronicS~",
                        column: x => x.ElectronicSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MonitoringOccurrenceEvents_MonitoringOccurrences_Monitoring~",
                        column: x => x.MonitoringOccurrenceId,
                        principalTable: "MonitoringOccurrences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MonitoringOccurrenceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MonitoringOccurrenceId = table.Column<int>(type: "integer", nullable: false),
                    RoomTestConfigurationId = table.Column<int>(type: "integer", nullable: true),
                    MachinePartConfigurationId = table.Column<int>(type: "integer", nullable: true),
                    TestCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoringOccurrenceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MonitoringOccurrenceItems_MonitoringOccurrences_MonitoringO~",
                        column: x => x.MonitoringOccurrenceId,
                        principalTable: "MonitoringOccurrences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringOccurrenceEvents_ElectronicSignatureId",
                table: "MonitoringOccurrenceEvents",
                column: "ElectronicSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringOccurrenceEvents_MonitoringOccurrenceId",
                table: "MonitoringOccurrenceEvents",
                column: "MonitoringOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringOccurrenceItems_MonitoringOccurrenceId",
                table: "MonitoringOccurrenceItems",
                column: "MonitoringOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringOccurrences_CreatedByRevisionId",
                table: "MonitoringOccurrences",
                column: "CreatedByRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringOccurrences_MachineId",
                table: "MonitoringOccurrences",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringOccurrences_RetiredByRevisionId",
                table: "MonitoringOccurrences",
                column: "RetiredByRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringOccurrences_RoomId",
                table: "MonitoringOccurrences",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringOccurrences_Target_Period",
                table: "MonitoringOccurrences",
                columns: new[] { "TargetType", "WaterSamplingPointId", "RoomId", "MachineId", "PeriodKey", "Sequence" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringOccurrences_WaterSamplingPointId",
                table: "MonitoringOccurrences",
                column: "WaterSamplingPointId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringOccurrences_WindowEnd",
                table: "MonitoringOccurrences",
                column: "WindowEnd");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringOccurrences_WindowStart",
                table: "MonitoringOccurrences",
                column: "WindowStart");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringPlanRevisions_ApprovalSignatureId",
                table: "MonitoringPlanRevisions",
                column: "ApprovalSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringPlanRevisions_Year_Approved",
                table: "MonitoringPlanRevisions",
                column: "Year",
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MonitoringPlanRevisions_Year_RevisionNumber",
                table: "MonitoringPlanRevisions",
                columns: new[] { "Year", "RevisionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MonitoringOccurrenceEvents");

            migrationBuilder.DropTable(
                name: "MonitoringOccurrenceItems");

            migrationBuilder.DropTable(
                name: "MonitoringOccurrences");

            migrationBuilder.DropTable(
                name: "MonitoringPlanRevisions");
        }
    }
}
