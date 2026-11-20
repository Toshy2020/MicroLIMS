using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalystCompetency : Migration
    {
        private const string Process = "Migration:AddAnalystCompetency";
        private const string Reason = "Default analyst competency grants";
        private static readonly string[] Codes = ["Competency.View", "Competency.Manage", "Competency.Override"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RequalificationMonths",
                table: "TestDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AnalystQualifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    TestDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    Basis = table.Column<int>(type: "integer", nullable: false),
                    Evidence = table.Column<string>(type: "text", nullable: false),
                    EvidenceSampleId = table.Column<int>(type: "integer", nullable: true),
                    QualifiedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    SignedByUserId = table.Column<int>(type: "integer", nullable: false),
                    SignatureId = table.Column<int>(type: "integer", nullable: false),
                    SigningTransaction = table.Column<string>(type: "text", nullable: false, defaultValueSql: "pg_current_xact_id()::text"),
                    WithdrawnOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WithdrawnReason = table.Column<string>(type: "text", nullable: true),
                    WithdrawnByUserId = table.Column<int>(type: "integer", nullable: true),
                    WithdrawnSignatureId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalystQualifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalystQualifications_ElectronicSignatures_SignatureId",
                        column: x => x.SignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnalystQualifications_ElectronicSignatures_WithdrawnSignatu~",
                        column: x => x.WithdrawnSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnalystQualifications_Samples_EvidenceSampleId",
                        column: x => x.EvidenceSampleId,
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnalystQualifications_TestDefinitions_TestDefinitionId",
                        column: x => x.TestDefinitionId,
                        principalTable: "TestDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnalystQualifications_Users_SignedByUserId",
                        column: x => x.SignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnalystQualifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AnalystQualifications_Users_WithdrawnByUserId",
                        column: x => x.WithdrawnByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QualificationAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnalystQualificationId = table.Column<int>(type: "integer", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileExtension = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualificationAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QualificationAttachments_AnalystQualifications_AnalystQuali~",
                        column: x => x.AnalystQualificationId,
                        principalTable: "AnalystQualifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QualificationAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestDefinitions_RequalificationMonths",
                table: "TestDefinitions",
                sql: " \"RequalificationMonths\" BETWEEN 1 AND 120 ");

            migrationBuilder.CreateIndex(
                name: "IX_AnalystQualifications_EvidenceSampleId",
                table: "AnalystQualifications",
                column: "EvidenceSampleId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalystQualifications_SignatureId",
                table: "AnalystQualifications",
                column: "SignatureId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalystQualifications_SignedByUserId",
                table: "AnalystQualifications",
                column: "SignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalystQualifications_TestDefinitionId",
                table: "AnalystQualifications",
                column: "TestDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalystQualifications_UserId_TestDefinitionId_QualifiedOn",
                table: "AnalystQualifications",
                columns: new[] { "UserId", "TestDefinitionId", "QualifiedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_AnalystQualifications_WithdrawnByUserId",
                table: "AnalystQualifications",
                column: "WithdrawnByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalystQualifications_WithdrawnSignatureId",
                table: "AnalystQualifications",
                column: "WithdrawnSignatureId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QualificationAttachments_AnalystQualificationId",
                table: "QualificationAttachments",
                column: "AnalystQualificationId");

            migrationBuilder.CreateIndex(
                name: "IX_QualificationAttachments_UploadedByUserId",
                table: "QualificationAttachments",
                column: "UploadedByUserId");

            migrationBuilder.Sql("""
                CREATE FUNCTION protect_analyst_qualification() RETURNS trigger AS $$
                DECLARE
                    withdrawal_columns text[] := ARRAY['WithdrawnOn', 'WithdrawnReason', 'WithdrawnByUserId', 'WithdrawnSignatureId'];
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW."WithdrawnOn" IS NULL AND NEW."WithdrawnReason" IS NULL
                           AND NEW."WithdrawnByUserId" IS NULL AND NEW."WithdrawnSignatureId" IS NULL
                           AND EXISTS (SELECT 1 FROM "ElectronicSignatures" s
                               WHERE s."Id" = NEW."SignatureId" AND s."UserId" = NEW."SignedByUserId"
                                 AND s."EntityType" = 'AnalystQualification' AND s."EntityId" = NEW."Id"
                                 AND s."MeaningOfSignature" = 45)
                        THEN
                            -- The top-level transaction ID stays stable across EF savepoints.
                            NEW."SigningTransaction" := pg_current_xact_id()::text;
                            RETURN NEW;
                        END IF;
                    ELSIF TG_OP = 'UPDATE' THEN
                        IF OLD."WithdrawnOn" IS NULL AND OLD."WithdrawnReason" IS NULL
                           AND OLD."WithdrawnByUserId" IS NULL AND OLD."WithdrawnSignatureId" IS NULL
                           AND NEW."WithdrawnOn" IS NOT NULL AND NULLIF(btrim(NEW."WithdrawnReason"), '') IS NOT NULL
                           AND to_jsonb(NEW) - withdrawal_columns = to_jsonb(OLD) - withdrawal_columns
                           AND EXISTS (SELECT 1 FROM "ElectronicSignatures" s
                               WHERE s."Id" = NEW."WithdrawnSignatureId" AND s."UserId" = NEW."WithdrawnByUserId"
                                 AND s."EntityType" = 'AnalystQualification' AND s."EntityId" = NEW."Id"
                                 AND s."MeaningOfSignature" = 46 AND s."SignedAt" = NEW."WithdrawnOn"
                                 AND s."Comment" = NEW."WithdrawnReason")
                        THEN
                            RETURN NEW;
                        END IF;
                    END IF;

                    RAISE EXCEPTION 'append-only: % is prohibited for this analyst qualification', TG_OP;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_analystqualifications_immutable
                BEFORE INSERT OR UPDATE OR DELETE ON "AnalystQualifications"
                FOR EACH ROW EXECUTE FUNCTION protect_analyst_qualification();

                CREATE TRIGGER trg_analystqualifications_no_truncate
                BEFORE TRUNCATE ON "AnalystQualifications"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

                CREATE FUNCTION protect_qualification_attachment() RETURNS trigger AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        -- The original signing transaction cannot be changed at withdrawal.
                        IF EXISTS (SELECT 1 FROM "AnalystQualifications" q
                            WHERE q."Id" = NEW."AnalystQualificationId" AND q."SignedByUserId" = NEW."UploadedByUserId"
                              AND q."SigningTransaction" = pg_current_xact_id()::text)
                        THEN
                            RETURN NEW;
                        END IF;
                    END IF;
                    RAISE EXCEPTION 'append-only: % is prohibited for this qualification attachment', TG_OP;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_qualificationattachments_immutable
                BEFORE INSERT OR UPDATE OR DELETE ON "QualificationAttachments"
                FOR EACH ROW EXECUTE FUNCTION protect_qualification_attachment();

                CREATE TRIGGER trg_qualificationattachments_no_truncate
                BEFORE TRUNCATE ON "QualificationAttachments"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();
                """);

            // RoleType values are stable: SectionHead 1, Reviewer 2, Analyst 3, QcManager 4, Qa 5.
            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Code", "Description", "IsEnforced")
                SELECT p.code, p.description, p.enforced
                FROM (VALUES
                    ('Competency.View', 'View analyst qualifications.', FALSE),
                    ('Competency.Manage', 'Sign and withdraw analyst qualifications.', TRUE),
                    ('Competency.Override', 'Sign a competency override for a test order.', FALSE)
                ) AS p(code, description, enforced)
                WHERE NOT EXISTS (SELECT 1 FROM "Permissions" existing WHERE existing."Code" = p.code);
                """);
            migrationBuilder.Sql(RoleGrantSql.Grant("""ro."Type" IN (1, 2, 3, 4, 5) AND ro."IsSystemRole" IS TRUE""", ["Competency.View"], Process, Reason));
            migrationBuilder.Sql(RoleGrantSql.Grant("""ro."Type" IN (1, 4) AND ro."IsSystemRole" IS TRUE""", ["Competency.Manage"], Process, Reason));
            migrationBuilder.Sql(RoleGrantSql.Grant("""ro."Type" = 1 AND ro."IsSystemRole" IS TRUE""", ["Competency.Override"], Process, Reason));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_qualificationattachments_no_truncate ON "QualificationAttachments";
                DROP TRIGGER IF EXISTS trg_qualificationattachments_immutable ON "QualificationAttachments";
                DROP TRIGGER IF EXISTS trg_analystqualifications_no_truncate ON "AnalystQualifications";
                DROP TRIGGER IF EXISTS trg_analystqualifications_immutable ON "AnalystQualifications";
                DROP FUNCTION IF EXISTS protect_qualification_attachment();
                DROP FUNCTION IF EXISTS protect_analyst_qualification();
                """);

            migrationBuilder.Sql(RoleGrantSql.Revoke(Codes, Process, "Remove analyst competency grants"));
            migrationBuilder.Sql("""
                DELETE FROM "Permissions" WHERE "Code" IN ('Competency.View', 'Competency.Manage', 'Competency.Override');
                """);

            migrationBuilder.DropTable(
                name: "QualificationAttachments");

            migrationBuilder.DropTable(
                name: "AnalystQualifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TestDefinitions_RequalificationMonths",
                table: "TestDefinitions");

            migrationBuilder.DropColumn(
                name: "RequalificationMonths",
                table: "TestDefinitions");
        }
    }
}
