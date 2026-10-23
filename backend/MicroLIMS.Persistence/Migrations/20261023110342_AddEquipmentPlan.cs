using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "AutoOutOfServiceSince",
                table: "EquipmentInventories",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CalibrationIntervalUnit",
                table: "EquipmentInventories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CalibrationIntervalValue",
                table: "EquipmentInventories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastCalibratedOn",
                table: "EquipmentInventories",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastPmOn",
                table: "EquipmentInventories",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PmIntervalUnit",
                table: "EquipmentInventories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PmIntervalValue",
                table: "EquipmentInventories",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CalendarEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    Month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EquipmentInventoryId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvents", x => x.Id);
                    table.CheckConstraint("CK_CalendarEvents_DateOrMonth", "(\"Date\" IS NULL) <> (\"Month\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_CalendarEvents_EquipmentInventories_EquipmentInventoryId",
                        column: x => x.EquipmentInventoryId,
                        principalTable: "EquipmentInventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentPlanRevisions",
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
                    table.PrimaryKey("PK_EquipmentPlanRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentPlanRevisions_ElectronicSignatures_ApprovalSignatu~",
                        column: x => x.ApprovalSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalendarEventDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CalendarEventId = table.Column<int>(type: "integer", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileExtension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEventDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEventDocuments_CalendarEvents_CalendarEventId",
                        column: x => x.CalendarEventId,
                        principalTable: "CalendarEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalendarEventDocuments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceLogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipmentInventoryId = table.Column<int>(type: "integer", nullable: false),
                    EntryType = table.Column<int>(type: "integer", nullable: false),
                    PerformedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PerformedByUserId = table.Column<int>(type: "integer", nullable: false),
                    WorkDone = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PartsReplaced = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Findings = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ElectronicSignatureId = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CalendarEventId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceLogEntries_CalendarEvents_CalendarEventId",
                        column: x => x.CalendarEventId,
                        principalTable: "CalendarEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceLogEntries_ElectronicSignatures_ElectronicSignat~",
                        column: x => x.ElectronicSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceLogEntries_EquipmentInventories_EquipmentInvento~",
                        column: x => x.EquipmentInventoryId,
                        principalTable: "EquipmentInventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    EquipmentInventoryId = table.Column<int>(type: "integer", nullable: false),
                    TaskType = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    IsUnplanned = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByRevisionId = table.Column<int>(type: "integer", nullable: true),
                    RetiredByRevisionId = table.Column<int>(type: "integer", nullable: true),
                    OriginalDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CompletedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CalibrationDocumentId = table.Column<int>(type: "integer", nullable: true),
                    MaintenanceLogEntryId = table.Column<int>(type: "integer", nullable: true),
                    IntervalSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentTasks_EquipmentDocuments_CalibrationDocumentId",
                        column: x => x.CalibrationDocumentId,
                        principalTable: "EquipmentDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentTasks_EquipmentInventories_EquipmentInventoryId",
                        column: x => x.EquipmentInventoryId,
                        principalTable: "EquipmentInventories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentTasks_EquipmentPlanRevisions_CreatedByRevisionId",
                        column: x => x.CreatedByRevisionId,
                        principalTable: "EquipmentPlanRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentTasks_EquipmentPlanRevisions_RetiredByRevisionId",
                        column: x => x.RetiredByRevisionId,
                        principalTable: "EquipmentPlanRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentTasks_MaintenanceLogEntries_MaintenanceLogEntryId",
                        column: x => x.MaintenanceLogEntryId,
                        principalTable: "MaintenanceLogEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentTaskEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EquipmentTaskId = table.Column<int>(type: "integer", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PerformedByUserId = table.Column<int>(type: "integer", nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ElectronicSignatureId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentTaskEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentTaskEvents_ElectronicSignatures_ElectronicSignatur~",
                        column: x => x.ElectronicSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentTaskEvents_EquipmentTasks_EquipmentTaskId",
                        column: x => x.EquipmentTaskId,
                        principalTable: "EquipmentTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventDocuments_CalendarEventId",
                table: "CalendarEventDocuments",
                column: "CalendarEventId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventDocuments_CalendarEventId_Status",
                table: "CalendarEventDocuments",
                columns: new[] { "CalendarEventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventDocuments_UploadedByUserId",
                table: "CalendarEventDocuments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_Date",
                table: "CalendarEvents",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_EquipmentInventoryId",
                table: "CalendarEvents",
                column: "EquipmentInventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_Month",
                table: "CalendarEvents",
                column: "Month");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_Status",
                table: "CalendarEvents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentPlanRevisions_ApprovalSignatureId",
                table: "EquipmentPlanRevisions",
                column: "ApprovalSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentPlanRevisions_Year_Approved",
                table: "EquipmentPlanRevisions",
                column: "Year",
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentPlanRevisions_Year_RevisionNumber",
                table: "EquipmentPlanRevisions",
                columns: new[] { "Year", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTaskEvents_ElectronicSignatureId",
                table: "EquipmentTaskEvents",
                column: "ElectronicSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTaskEvents_EquipmentTaskId",
                table: "EquipmentTaskEvents",
                column: "EquipmentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTasks_CalibrationDocumentId",
                table: "EquipmentTasks",
                column: "CalibrationDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTasks_CreatedByRevisionId",
                table: "EquipmentTasks",
                column: "CreatedByRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTasks_DueDate",
                table: "EquipmentTasks",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTasks_EquipmentInventoryId",
                table: "EquipmentTasks",
                column: "EquipmentInventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTasks_EquipmentInventoryId_TaskType_Year_Sequence",
                table: "EquipmentTasks",
                columns: new[] { "EquipmentInventoryId", "TaskType", "Year", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTasks_MaintenanceLogEntryId",
                table: "EquipmentTasks",
                column: "MaintenanceLogEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentTasks_RetiredByRevisionId",
                table: "EquipmentTasks",
                column: "RetiredByRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceLogEntries_CalendarEventId",
                table: "MaintenanceLogEntries",
                column: "CalendarEventId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceLogEntries_ElectronicSignatureId",
                table: "MaintenanceLogEntries",
                column: "ElectronicSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceLogEntries_EquipmentInventoryId",
                table: "MaintenanceLogEntries",
                column: "EquipmentInventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceLogEntries_PerformedOn",
                table: "MaintenanceLogEntries",
                column: "PerformedOn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarEventDocuments");

            migrationBuilder.DropTable(
                name: "EquipmentTaskEvents");

            migrationBuilder.DropTable(
                name: "EquipmentTasks");

            migrationBuilder.DropTable(
                name: "EquipmentPlanRevisions");

            migrationBuilder.DropTable(
                name: "MaintenanceLogEntries");

            migrationBuilder.DropTable(
                name: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "AutoOutOfServiceSince",
                table: "EquipmentInventories");

            migrationBuilder.DropColumn(
                name: "CalibrationIntervalUnit",
                table: "EquipmentInventories");

            migrationBuilder.DropColumn(
                name: "CalibrationIntervalValue",
                table: "EquipmentInventories");

            migrationBuilder.DropColumn(
                name: "LastCalibratedOn",
                table: "EquipmentInventories");

            migrationBuilder.DropColumn(
                name: "LastPmOn",
                table: "EquipmentInventories");

            migrationBuilder.DropColumn(
                name: "PmIntervalUnit",
                table: "EquipmentInventories");

            migrationBuilder.DropColumn(
                name: "PmIntervalValue",
                table: "EquipmentInventories");
        }
    }
}
