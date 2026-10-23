using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstrumentIngest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstrumentIngests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstrumentAgentId = table.Column<int>(type: "integer", nullable: false),
                    EquipmentId = table.Column<int>(type: "integer", nullable: false),
                    SequenceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InjectionNumber = table.Column<int>(type: "integer", nullable: false),
                    AcquiredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentIngests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstrumentIngests_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InstrumentIngests_InstrumentAgents_InstrumentAgentId",
                        column: x => x.InstrumentAgentId,
                        principalTable: "InstrumentAgents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstrumentIngestVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstrumentIngestId = table.Column<int>(type: "integer", nullable: false),
                    VersionNo = table.Column<int>(type: "integer", nullable: false),
                    ReportSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReportStorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReportFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StatusReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcquiredAtLocalText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TimeZoneIdUsed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClockOffsetSeconds = table.Column<int>(type: "integer", nullable: false),
                    OperatorAsReported = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResolvedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentIngestVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstrumentIngestVersions_InstrumentIngests_InstrumentIngest~",
                        column: x => x.InstrumentIngestId,
                        principalTable: "InstrumentIngests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstrumentIngestPayloads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstrumentIngestVersionId = table.Column<int>(type: "integer", nullable: false),
                    ParserVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ManifestJson = table.Column<string>(type: "jsonb", nullable: false),
                    ParsedJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentIngestPayloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstrumentIngestPayloads_InstrumentIngestVersions_Instrumen~",
                        column: x => x.InstrumentIngestVersionId,
                        principalTable: "InstrumentIngestVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentIngestPayloads_InstrumentIngestVersionId",
                table: "InstrumentIngestPayloads",
                column: "InstrumentIngestVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentIngests_EquipmentId_SequenceName_InjectionNumber_~",
                table: "InstrumentIngests",
                columns: new[] { "EquipmentId", "SequenceName", "InjectionNumber", "AcquiredAtUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentIngests_InstrumentAgentId",
                table: "InstrumentIngests",
                column: "InstrumentAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentIngestVersions_InstrumentIngestId_ReportSha256",
                table: "InstrumentIngestVersions",
                columns: new[] { "InstrumentIngestId", "ReportSha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentIngestVersions_InstrumentIngestId_VersionNo",
                table: "InstrumentIngestVersions",
                columns: new[] { "InstrumentIngestId", "VersionNo" },
                unique: true);

            // Append-only protection (21 CFR Part 11 / Annex 11). Distinct
            // function names: prevent_audit_modification belongs to the audit
            // tables and must stay untouched.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION prevent_ingest_payload_modification()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'Table % is append-only: UPDATE and DELETE operations are prohibited (GMP data integrity).', TG_TABLE_NAME;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_instrumentingestpayloads_immutable
BEFORE UPDATE OR DELETE ON ""InstrumentIngestPayloads""
FOR EACH ROW EXECUTE FUNCTION prevent_ingest_payload_modification();

CREATE OR REPLACE FUNCTION prevent_ingest_version_modification()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'Table % does not allow DELETE: received instrument reports are never removed (GMP data integrity).', TG_TABLE_NAME;
    END IF;
    IF NEW.""InstrumentIngestId"" IS DISTINCT FROM OLD.""InstrumentIngestId""
       OR NEW.""VersionNo"" IS DISTINCT FROM OLD.""VersionNo""
       OR NEW.""ReportSha256"" IS DISTINCT FROM OLD.""ReportSha256""
       OR NEW.""ReportStorageKey"" IS DISTINCT FROM OLD.""ReportStorageKey""
       OR NEW.""ReportFileName"" IS DISTINCT FROM OLD.""ReportFileName""
       OR NEW.""OperatorAsReported"" IS DISTINCT FROM OLD.""OperatorAsReported""
       OR NEW.""AcquiredAtLocalText"" IS DISTINCT FROM OLD.""AcquiredAtLocalText""
       OR NEW.""ReceivedAt"" IS DISTINCT FROM OLD.""ReceivedAt""
       OR NEW.""TimeZoneIdUsed"" IS DISTINCT FROM OLD.""TimeZoneIdUsed""
       OR NEW.""ClockOffsetSeconds"" IS DISTINCT FROM OLD.""ClockOffsetSeconds"" THEN
        RAISE EXCEPTION 'Table % evidence columns are immutable: only the review outcome may change (GMP data integrity).', TG_TABLE_NAME;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_instrumentingestversions_immutable
BEFORE UPDATE OR DELETE ON ""InstrumentIngestVersions""
FOR EACH ROW EXECUTE FUNCTION prevent_ingest_version_modification();

CREATE OR REPLACE FUNCTION prevent_ingest_modification()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'Table % does not allow DELETE: ingested injections are never removed (GMP data integrity).', TG_TABLE_NAME;
    END IF;
    IF NEW.""InstrumentAgentId"" IS DISTINCT FROM OLD.""InstrumentAgentId""
       OR NEW.""EquipmentId"" IS DISTINCT FROM OLD.""EquipmentId""
       OR NEW.""SequenceName"" IS DISTINCT FROM OLD.""SequenceName""
       OR NEW.""InjectionNumber"" IS DISTINCT FROM OLD.""InjectionNumber""
       OR NEW.""AcquiredAtUtc"" IS DISTINCT FROM OLD.""AcquiredAtUtc"" THEN
        RAISE EXCEPTION 'Table % identity columns are immutable (GMP data integrity).', TG_TABLE_NAME;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_instrumentingests_immutable
BEFORE UPDATE OR DELETE ON ""InstrumentIngests""
FOR EACH ROW EXECUTE FUNCTION prevent_ingest_modification();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_instrumentingests_immutable ON ""InstrumentIngests"";
DROP TRIGGER IF EXISTS trg_instrumentingestversions_immutable ON ""InstrumentIngestVersions"";
DROP TRIGGER IF EXISTS trg_instrumentingestpayloads_immutable ON ""InstrumentIngestPayloads"";
DROP FUNCTION IF EXISTS prevent_ingest_modification();
DROP FUNCTION IF EXISTS prevent_ingest_version_modification();
DROP FUNCTION IF EXISTS prevent_ingest_payload_modification();
");

            migrationBuilder.DropTable(
                name: "InstrumentIngestPayloads");

            migrationBuilder.DropTable(
                name: "InstrumentIngestVersions");

            migrationBuilder.DropTable(
                name: "InstrumentIngests");
        }
    }
}
