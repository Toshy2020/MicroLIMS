using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenAppendOnlyAuditTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SecurityAuditEvents_Users_ActorUserId",
                table: "SecurityAuditEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_SecurityAuditEvents_Users_TargetUserId",
                table: "SecurityAuditEvents");

            migrationBuilder.AddForeignKey(
                name: "FK_SecurityAuditEvents_Users_ActorUserId",
                table: "SecurityAuditEvents",
                column: "ActorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SecurityAuditEvents_Users_TargetUserId",
                table: "SecurityAuditEvents",
                column: "TargetUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Append-only (21 CFR Part 11 / Annex 11). Reuses prevent_audit_modification()
            // for the two newly protected tables, and adds a statement-level TRUNCATE
            // guard to every append-only table (row-level triggers do not fire on TRUNCATE).
            migrationBuilder.Sql(@"
CREATE TRIGGER trg_loginhistories_immutable
BEFORE UPDATE OR DELETE ON ""LoginHistories""
FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification();

CREATE TRIGGER trg_securityauditevents_immutable
BEFORE UPDATE OR DELETE ON ""SecurityAuditEvents""
FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification();

CREATE OR REPLACE FUNCTION prevent_audit_truncate()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'Table % is append-only: TRUNCATE is prohibited by GMP regulations (21 CFR Part 11, EU Annex 11).', TG_TABLE_NAME;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_auditlogs_no_truncate
BEFORE TRUNCATE ON ""AuditLogs""
FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

CREATE TRIGGER trg_auditeventchanges_no_truncate
BEFORE TRUNCATE ON ""AuditEventChanges""
FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

CREATE TRIGGER trg_electronicsignatures_no_truncate
BEFORE TRUNCATE ON ""ElectronicSignatures""
FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

CREATE TRIGGER trg_documentacknowledgementrecords_no_truncate
BEFORE TRUNCATE ON ""DocumentAcknowledgementRecords""
FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

CREATE TRIGGER trg_workflowprofileversions_no_truncate
BEFORE TRUNCATE ON ""WorkflowProfileVersions""
FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

CREATE TRIGGER trg_instrumentingestpayloads_no_truncate
BEFORE TRUNCATE ON ""InstrumentIngestPayloads""
FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

CREATE TRIGGER trg_instrumentingestversions_no_truncate
BEFORE TRUNCATE ON ""InstrumentIngestVersions""
FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

CREATE TRIGGER trg_instrumentingests_no_truncate
BEFORE TRUNCATE ON ""InstrumentIngests""
FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

CREATE TRIGGER trg_loginhistories_no_truncate
BEFORE TRUNCATE ON ""LoginHistories""
FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

CREATE TRIGGER trg_securityauditevents_no_truncate
BEFORE TRUNCATE ON ""SecurityAuditEvents""
FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_securityauditevents_no_truncate ON ""SecurityAuditEvents"";
DROP TRIGGER IF EXISTS trg_loginhistories_no_truncate ON ""LoginHistories"";
DROP TRIGGER IF EXISTS trg_instrumentingests_no_truncate ON ""InstrumentIngests"";
DROP TRIGGER IF EXISTS trg_instrumentingestversions_no_truncate ON ""InstrumentIngestVersions"";
DROP TRIGGER IF EXISTS trg_instrumentingestpayloads_no_truncate ON ""InstrumentIngestPayloads"";
DROP TRIGGER IF EXISTS trg_workflowprofileversions_no_truncate ON ""WorkflowProfileVersions"";
DROP TRIGGER IF EXISTS trg_documentacknowledgementrecords_no_truncate ON ""DocumentAcknowledgementRecords"";
DROP TRIGGER IF EXISTS trg_electronicsignatures_no_truncate ON ""ElectronicSignatures"";
DROP TRIGGER IF EXISTS trg_auditeventchanges_no_truncate ON ""AuditEventChanges"";
DROP TRIGGER IF EXISTS trg_auditlogs_no_truncate ON ""AuditLogs"";
DROP FUNCTION IF EXISTS prevent_audit_truncate();
DROP TRIGGER IF EXISTS trg_securityauditevents_immutable ON ""SecurityAuditEvents"";
DROP TRIGGER IF EXISTS trg_loginhistories_immutable ON ""LoginHistories"";
");

            migrationBuilder.DropForeignKey(
                name: "FK_SecurityAuditEvents_Users_ActorUserId",
                table: "SecurityAuditEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_SecurityAuditEvents_Users_TargetUserId",
                table: "SecurityAuditEvents");

            migrationBuilder.AddForeignKey(
                name: "FK_SecurityAuditEvents_Users_ActorUserId",
                table: "SecurityAuditEvents",
                column: "ActorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SecurityAuditEvents_Users_TargetUserId",
                table: "SecurityAuditEvents",
                column: "TargetUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
