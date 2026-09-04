using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroLIMS.Persistence.DbContext;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    public partial class AddAuditImmutabilityTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION prevent_audit_modification()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'Table % is append-only: UPDATE and DELETE operations are prohibited by GMP regulations (21 CFR Part 11, EU Annex 11).', TG_TABLE_NAME;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_auditlogs_immutable
BEFORE UPDATE OR DELETE ON ""AuditLogs""
FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification();

CREATE TRIGGER trg_auditeventchanges_immutable
BEFORE UPDATE OR DELETE ON ""AuditEventChanges""
FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification();

CREATE TRIGGER trg_electronicsignatures_immutable
BEFORE UPDATE OR DELETE ON ""ElectronicSignatures""
FOR EACH ROW EXECUTE FUNCTION prevent_audit_modification();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_electronicsignatures_immutable ON ""ElectronicSignatures"";
DROP TRIGGER IF EXISTS trg_auditeventchanges_immutable ON ""AuditEventChanges"";
DROP TRIGGER IF EXISTS trg_auditlogs_immutable ON ""AuditLogs"";
DROP FUNCTION IF EXISTS prevent_audit_modification();
");
        }
    }
}
