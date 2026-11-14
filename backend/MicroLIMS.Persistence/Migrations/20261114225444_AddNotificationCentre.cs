using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationCentre : Migration
    {
        private const string Process = "Migration 20261114225444_AddNotificationCentre";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DedupeKey",
                table: "NotificationLogs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "NotificationLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "NotificationLogs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "NotificationLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetId",
                table: "NotificationLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetType",
                table: "NotificationLogs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "NotificationLogs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmailOutbox",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NotificationLogId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    ToAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOutbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailOutbox_NotificationLogs_NotificationLogId",
                        column: x => x.NotificationLogId,
                        principalTable: "NotificationLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EmailOutbox_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EscalationRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    Trigger = table.Column<int>(type: "integer", nullable: false),
                    ThresholdWorkingHours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    NotifyRoleType = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetiredByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReplacesRuleId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscalationRules_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EscalationRules_EscalationRules_ReplacesRuleId",
                        column: x => x.ReplacesRuleId,
                        principalTable: "EscalationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EscalationRules_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EscalationRules_Users_RetiredByUserId",
                        column: x => x.RetiredByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabWorkingHours",
                columns: table => new
                {
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    IsWorkingDay = table.Column<bool>(type: "boolean", nullable: false),
                    StartMinute = table.Column<int>(type: "integer", nullable: false),
                    EndMinute = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabWorkingHours", x => x.DayOfWeek);
                    table.CheckConstraint("CK_LabWorkingHours_StartBeforeEndOnWorkingDay", "NOT \"IsWorkingDay\" OR (\"StartMinute\" >= 0 AND \"EndMinute\" <= 1440 AND \"StartMinute\" < \"EndMinute\")");
                });

            migrationBuilder.CreateTable(
                name: "MandatoryNotificationTypes",
                columns: table => new
                {
                    SectionId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SetByUserId = table.Column<int>(type: "integer", nullable: false),
                    SetAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MandatoryNotificationTypes", x => new { x.SectionId, x.Type });
                    table.ForeignKey(
                        name: "FK_MandatoryNotificationTypes_DocumentSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "DocumentSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MandatoryNotificationTypes_Users_SetByUserId",
                        column: x => x.SetByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserNotificationPreferences",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EmailMode = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotificationPreferences", x => new { x.UserId, x.Type });
                    table.ForeignKey(
                        name: "FK_UserNotificationPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EscalationEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EscalationRuleId = table.Column<int>(type: "integer", nullable: false),
                    RecordType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecordId = table.Column<int>(type: "integer", nullable: false),
                    FiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecipientUserIds = table.Column<string>(type: "jsonb", nullable: false),
                    NoRecipient = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscalationEvents_EscalationRules_EscalationRuleId",
                        column: x => x.EscalationRuleId,
                        principalTable: "EscalationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_UserId_DedupeKey_Open",
                table: "NotificationLogs",
                columns: new[] { "UserId", "DedupeKey" },
                unique: true,
                filter: "\"DedupeKey\" IS NOT NULL AND \"ResolvedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_UserId_IsRead_CreatedAt",
                table: "NotificationLogs",
                columns: new[] { "UserId", "IsRead", "CreatedAt" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_NextAttemptAt_Pending",
                table: "EmailOutbox",
                column: "NextAttemptAt",
                filter: "\"SentAt\" IS NULL AND \"FailedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_NotificationLogId",
                table: "EmailOutbox",
                column: "NotificationLogId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutbox_UserId",
                table: "EmailOutbox",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationEvents_Rule_Record",
                table: "EscalationEvents",
                columns: new[] { "EscalationRuleId", "RecordType", "RecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_CreatedByUserId",
                table: "EscalationRules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_ReplacesRuleId",
                table: "EscalationRules",
                column: "ReplacesRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_RetiredByUserId",
                table: "EscalationRules",
                column: "RetiredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_SectionId_Trigger_Active",
                table: "EscalationRules",
                columns: new[] { "SectionId", "Trigger" },
                filter: "\"RetiredAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MandatoryNotificationTypes_SetByUserId",
                table: "MandatoryNotificationTypes",
                column: "SetByUserId");

            // Enforced by the notification preference, working-hours and escalation controllers added in later tasks;
            // stays IsEnforced = FALSE here so PermissionAuthorizationTests stays accurate until those endpoints exist.
            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Code", "Description", "IsEnforced")
                SELECT 'Notifications.Configure', 'Mark notification types as mandatory and configure escalation rules and laboratory working hours for your section.', FALSE
                WHERE NOT EXISTS (SELECT 1 FROM "Permissions" WHERE "Code" = 'Notifications.Configure');
                """);

            // RoleType: 1 = SectionHead, 4 = QcManager. RoleGrantSql never grants to roles holding Users.Manage or Roles.Manage.
            migrationBuilder.Sql(RoleGrantSql.Grant(
                @"ro.""Type"" IN (1, 4) AND ro.""IsSystemRole""",
                new[] { "Notifications.Configure" },
                Process,
                "Section Heads and QC Managers configure section notifications, working hours and escalation rules"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RoleGrantSql.Revoke(
                new[] { "Notifications.Configure" },
                Process + " (Down)",
                "Notifications.Configure rolled back (migration Down)"));

            migrationBuilder.Sql("""
                DELETE FROM "Permissions" WHERE "Code" = 'Notifications.Configure';
                """);
            migrationBuilder.DropTable(
                name: "EmailOutbox");

            migrationBuilder.DropTable(
                name: "EscalationEvents");

            migrationBuilder.DropTable(
                name: "LabWorkingHours");

            migrationBuilder.DropTable(
                name: "MandatoryNotificationTypes");

            migrationBuilder.DropTable(
                name: "UserNotificationPreferences");

            migrationBuilder.DropTable(
                name: "EscalationRules");

            migrationBuilder.DropIndex(
                name: "IX_NotificationLogs_UserId_DedupeKey_Open",
                table: "NotificationLogs");

            migrationBuilder.DropIndex(
                name: "IX_NotificationLogs_UserId_IsRead_CreatedAt",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "DedupeKey",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "TargetId",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "TargetType",
                table: "NotificationLogs");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "NotificationLogs");
        }
    }
}
