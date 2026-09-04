using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentControlEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "AuditLogs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "EntityName",
                table: "AuditLogs",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "EntityId",
                table: "AuditLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "ActionCategory",
                table: "AuditLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActionCode",
                table: "AuditLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActorType",
                table: "AuditLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentMasterId",
                table: "AuditLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DocumentRevisionId",
                table: "AuditLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventUid",
                table: "AuditLogs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "AuditLogs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceContext",
                table: "AuditLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemProcessName",
                table: "AuditLogs",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditEventChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuditLogId = table.Column<int>(type: "integer", nullable: false),
                    FieldName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PreviousValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEventChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEventChanges_AuditLogs_AuditLogId",
                        column: x => x.AuditLogId,
                        principalTable: "AuditLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SettingKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DataType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SettingGroup = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfigurationSettings_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentDepartments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentDepartments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentNumberingConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Prefix = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NumberFormat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentNumberingConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentNumberingConfigurations_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DefaultReviewCycleMonths = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentSections_DocumentDepartments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "DocumentDepartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentKeywords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentMasterId = table.Column<int>(type: "integer", nullable: false),
                    Keyword = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentKeywords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentMasterAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentMasterId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AssignmentRole = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AssignedByUserId = table.Column<int>(type: "integer", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentMasterAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentMasterAssignments_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentMasterAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentMasters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MicroLimsDocumentId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CompanyDocumentCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DocumentTypeId = table.Column<int>(type: "integer", nullable: false),
                    DepartmentId = table.Column<int>(type: "integer", nullable: false),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    DocumentOwnerUserId = table.Column<int>(type: "integer", nullable: false),
                    Confidentiality = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RecordOrigin = table.Column<int>(type: "integer", nullable: false),
                    RecordStatus = table.Column<int>(type: "integer", nullable: false),
                    CurrentEffectiveRevisionId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentMasters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentMasters_DocumentDepartments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "DocumentDepartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentMasters_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentMasters_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentMasters_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentMasters_Users_DocumentOwnerUserId",
                        column: x => x.DocumentOwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentMasters_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentMasters_Users_VoidedByUserId",
                        column: x => x.VoidedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentMasterId = table.Column<int>(type: "integer", nullable: false),
                    RevisionNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RevisionSequence = table.Column<int>(type: "integer", nullable: false),
                    RevisionStatus = table.Column<int>(type: "integer", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextReviewDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewCycleMonths = table.Column<int>(type: "integer", nullable: true),
                    RevisionType = table.Column<int>(type: "integer", nullable: true),
                    ReasonForRevision = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ChangeReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RecordOrigin = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledByUserId = table.Column<int>(type: "integer", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRevisions_DocumentMasters_DocumentMasterId",
                        column: x => x.DocumentMasterId,
                        principalTable: "DocumentMasters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentRevisions_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentRevisions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RevisionFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentRevisionId = table.Column<int>(type: "integer", nullable: false),
                    FileRole = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SupersededByFileId = table.Column<int>(type: "integer", nullable: true),
                    UploadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RevisionFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RevisionFiles_DocumentRevisions_DocumentRevisionId",
                        column: x => x.DocumentRevisionId,
                        principalTable: "DocumentRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RevisionFiles_RevisionFiles_SupersededByFileId",
                        column: x => x.SupersededByFileId,
                        principalTable: "RevisionFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RevisionFiles_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CorrelationId",
                table: "AuditLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_DocumentMasterId",
                table: "AuditLogs",
                column: "DocumentMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_DocumentRevisionId",
                table: "AuditLogs",
                column: "DocumentRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EventUid",
                table: "AuditLogs",
                column: "EventUid");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEventChanges_AuditLogId",
                table: "AuditEventChanges",
                column: "AuditLogId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationSettings_ModifiedByUserId",
                table: "ConfigurationSettings",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationSettings_SettingKey",
                table: "ConfigurationSettings",
                column: "SettingKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentDepartments_Code",
                table: "DocumentDepartments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentKeywords_DocumentMasterId_Keyword",
                table: "DocumentKeywords",
                columns: new[] { "DocumentMasterId", "Keyword" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMasterAssignments_AssignedByUserId",
                table: "DocumentMasterAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMasterAssignments_DocumentMasterId_UserId_Assignmen~",
                table: "DocumentMasterAssignments",
                columns: new[] { "DocumentMasterId", "UserId", "AssignmentRole" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMasterAssignments_UserId",
                table: "DocumentMasterAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMasters_CompanyDocumentCode",
                table: "DocumentMasters",
                column: "CompanyDocumentCode",
                unique: true,
                filter: "\"RecordStatus\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMasters_CreatedByUserId",
                table: "DocumentMasters",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMasters_CurrentEffectiveRevisionId",
                table: "DocumentMasters",
                column: "CurrentEffectiveRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMasters_DepartmentId",
                table: "DocumentMasters",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMasters_DocumentOwnerUserId",
                table: "DocumentMasters",
                column: "DocumentOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMasters_DocumentTypeId",
                table: "DocumentMasters",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMasters_MicroLimsDocumentId",
                table: "DocumentMasters",
                column: "MicroLimsDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMasters_ModifiedByUserId",
                table: "DocumentMasters",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMasters_SectionId",
                table: "DocumentMasters",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentMasters_VoidedByUserId",
                table: "DocumentMasters",
                column: "VoidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentNumberingConfigurations_ModifiedByUserId",
                table: "DocumentNumberingConfigurations",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRevisions_CancelledByUserId",
                table: "DocumentRevisions",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRevisions_CreatedByUserId",
                table: "DocumentRevisions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRevisions_DocumentMasterId_RevisionNumber",
                table: "DocumentRevisions",
                columns: new[] { "DocumentMasterId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRevisions_RevisionSequence",
                table: "DocumentRevisions",
                column: "RevisionSequence");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSections_DepartmentId_Name",
                table: "DocumentSections",
                columns: new[] { "DepartmentId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_Code",
                table: "DocumentTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RevisionFiles_DocumentRevisionId_FileRole",
                table: "RevisionFiles",
                columns: new[] { "DocumentRevisionId", "FileRole" },
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionFiles_SupersededByFileId",
                table: "RevisionFiles",
                column: "SupersededByFileId");

            migrationBuilder.CreateIndex(
                name: "IX_RevisionFiles_UploadedByUserId",
                table: "RevisionFiles",
                column: "UploadedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentKeywords_DocumentMasters_DocumentMasterId",
                table: "DocumentKeywords",
                column: "DocumentMasterId",
                principalTable: "DocumentMasters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentMasterAssignments_DocumentMasters_DocumentMasterId",
                table: "DocumentMasterAssignments",
                column: "DocumentMasterId",
                principalTable: "DocumentMasters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentMasters_DocumentRevisions_CurrentEffectiveRevisionId",
                table: "DocumentMasters",
                column: "CurrentEffectiveRevisionId",
                principalTable: "DocumentRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentRevisions_DocumentMasters_DocumentMasterId",
                table: "DocumentRevisions");

            migrationBuilder.DropTable(
                name: "AuditEventChanges");

            migrationBuilder.DropTable(
                name: "ConfigurationSettings");

            migrationBuilder.DropTable(
                name: "DocumentKeywords");

            migrationBuilder.DropTable(
                name: "DocumentMasterAssignments");

            migrationBuilder.DropTable(
                name: "DocumentNumberingConfigurations");

            migrationBuilder.DropTable(
                name: "RevisionFiles");

            migrationBuilder.DropTable(
                name: "DocumentMasters");

            migrationBuilder.DropTable(
                name: "DocumentRevisions");

            migrationBuilder.DropTable(
                name: "DocumentSections");

            migrationBuilder.DropTable(
                name: "DocumentTypes");

            migrationBuilder.DropTable(
                name: "DocumentDepartments");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_CorrelationId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_DocumentMasterId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_DocumentRevisionId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EventUid",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ActionCategory",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ActionCode",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ActorType",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "DocumentMasterId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "DocumentRevisionId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "EventUid",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "SourceContext",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "SystemProcessName",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "AuditLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EntityName",
                table: "AuditLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "EntityId",
                table: "AuditLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "AuditLogs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }
    }
}
