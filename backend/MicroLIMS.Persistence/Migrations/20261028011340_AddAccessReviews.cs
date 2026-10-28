using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessReviews : Migration
    {
        // Access.Review: generate, record findings on and sign the periodic access review. Granted only to the
        // built-in role (Type 0 = SystemAdministrator AND IsSystemRole); custom roles are left alone (a QA reviewer
        // is granted it through Roles). Raw SQL bypasses RoleService, so PermissionVersion is bumped here to
        // invalidate issued tokens, exactly as in 20261027033248_GrantSystemAdministratorConfigureAllSections.
        private const string Reason = "Administrators run the periodic access review (Annex 11 s.12)";

        private const string Declarations = """
            DECLARE
                r RECORD;
                prev_codes text[];
                new_codes text[];
                granted text[];
                revoked text[];
            """;

        private const string SnapshotPrevious = """
                    SELECT COALESCE(array_agg(p."Code" ORDER BY p."Code"), '{}') INTO prev_codes
                    FROM "RolePermissions" rp JOIN "Permissions" p ON p."Id" = rp."PermissionId"
                    WHERE rp."RoleId" = r."Id";
            """;

        private const string AuditAndBump = """
                    SELECT COALESCE(array_agg(p."Code" ORDER BY p."Code"), '{}') INTO new_codes
                    FROM "RolePermissions" rp JOIN "Permissions" p ON p."Id" = rp."PermissionId"
                    WHERE rp."RoleId" = r."Id";
                    granted := ARRAY(SELECT unnest(new_codes) EXCEPT SELECT unnest(prev_codes));
                    revoked := ARRAY(SELECT unnest(prev_codes) EXCEPT SELECT unnest(new_codes));
                    IF cardinality(granted) > 0 OR cardinality(revoked) > 0 THEN
                        UPDATE "Roles" SET "PermissionVersion" = "PermissionVersion" + 1 WHERE "Id" = r."Id";
                        INSERT INTO "AuditLogs" (
                            "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                            "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                        VALUES (
                            'Role', r."Id"::text, 'ROLE_PERMISSIONS_UPDATED',
                            jsonb_build_object('PermissionCodes', to_jsonb(prev_codes))::text,
                            jsonb_build_object('PermissionCodes', to_jsonb(new_codes), 'Granted', to_jsonb(granted), 'Revoked', to_jsonb(revoked))::text,
                            0, now(), 2, '{PROCESS}', 'RolePermissionsUpdated', 7, '{REASON}');
                    END IF;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GeneratedByUserId = table.Column<int>(type: "integer", nullable: false),
                    PeriodLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    SnapshotSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewerUserId = table.Column<int>(type: "integer", nullable: true),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ElectronicSignatureId = table.Column<int>(type: "integer", nullable: true),
                    Conclusion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessReviews_ElectronicSignatures_ElectronicSignatureId",
                        column: x => x.ElectronicSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessReviews_Users_GeneratedByUserId",
                        column: x => x.GeneratedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessReviews_Users_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccessReviewFindings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReviewId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessReviewFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessReviewFindings_AccessReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "AccessReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessReviewFindings_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccessReviewFindings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessReviewFindings_RecordedByUserId",
                table: "AccessReviewFindings",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessReviewFindings_ReviewId",
                table: "AccessReviewFindings",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessReviewFindings_UserId",
                table: "AccessReviewFindings",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessReviews_ElectronicSignatureId",
                table: "AccessReviews",
                column: "ElectronicSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessReviews_GeneratedAt",
                table: "AccessReviews",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AccessReviews_GeneratedByUserId",
                table: "AccessReviews",
                column: "GeneratedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessReviews_ReviewerUserId",
                table: "AccessReviews",
                column: "ReviewerUserId");

            // Immutability (21 CFR Part 11 / Annex 11). AccessReviews: DELETE never; UPDATE only while Draft, and only
            // the Draft -> Signed transition (reviewer, time, signature set) with every snapshot column unchanged.
            // AccessReviewFindings: INSERT and UPDATE only while the parent review is Draft; DELETE never. Both
            // tables also get the shared TRUNCATE guard (see HardenAppendOnlyAuditTables).
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_access_review_modification()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Table % is append-only: DELETE is prohibited by GMP regulations (21 CFR Part 11, EU Annex 11).', TG_TABLE_NAME;
                    END IF;
                    IF OLD."Status" <> 0 THEN
                        RAISE EXCEPTION 'Access review % is signed and immutable (21 CFR Part 11, EU Annex 11).', OLD."Id";
                    END IF;
                    IF NEW."Status" <> 1 OR NEW."ReviewerUserId" IS NULL OR NEW."SignedAt" IS NULL OR NEW."ElectronicSignatureId" IS NULL THEN
                        RAISE EXCEPTION 'Access review % can only change by being signed (Draft to Signed).', OLD."Id";
                    END IF;
                    IF NEW."Id" IS DISTINCT FROM OLD."Id"
                       OR NEW."GeneratedAt" IS DISTINCT FROM OLD."GeneratedAt"
                       OR NEW."GeneratedByUserId" IS DISTINCT FROM OLD."GeneratedByUserId"
                       OR NEW."PeriodLabel" IS DISTINCT FROM OLD."PeriodLabel"
                       OR NEW."SnapshotJson" IS DISTINCT FROM OLD."SnapshotJson"
                       OR NEW."SnapshotSha256" IS DISTINCT FROM OLD."SnapshotSha256" THEN
                        RAISE EXCEPTION 'The snapshot of access review % is immutable.', OLD."Id";
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE OR REPLACE FUNCTION prevent_access_review_finding_modification()
                RETURNS TRIGGER AS $$
                DECLARE
                    review_status integer;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Table % is append-only: DELETE is prohibited by GMP regulations (21 CFR Part 11, EU Annex 11).', TG_TABLE_NAME;
                    END IF;
                    IF TG_OP = 'UPDATE' AND (NEW."ReviewId" IS DISTINCT FROM OLD."ReviewId" OR NEW."UserId" IS DISTINCT FROM OLD."UserId") THEN
                        RAISE EXCEPTION 'A finding cannot be moved to another review or account.';
                    END IF;
                    -- FOR SHARE serialises with a concurrent signing of the same review.
                    SELECT "Status" INTO review_status FROM "AccessReviews" WHERE "Id" = NEW."ReviewId" FOR SHARE;
                    IF review_status IS NOT NULL AND review_status <> 0 THEN
                        RAISE EXCEPTION 'Access review % is signed and immutable: its findings can no longer change (21 CFR Part 11, EU Annex 11).', NEW."ReviewId";
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_accessreviews_immutable
                BEFORE UPDATE OR DELETE ON "AccessReviews"
                FOR EACH ROW EXECUTE FUNCTION prevent_access_review_modification();

                CREATE TRIGGER trg_accessreviews_no_truncate
                BEFORE TRUNCATE ON "AccessReviews"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

                CREATE TRIGGER trg_accessreviewfindings_immutable
                BEFORE INSERT OR UPDATE OR DELETE ON "AccessReviewFindings"
                FOR EACH ROW EXECUTE FUNCTION prevent_access_review_finding_modification();

                CREATE TRIGGER trg_accessreviewfindings_no_truncate
                BEFORE TRUNCATE ON "AccessReviewFindings"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();
                """);

            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Code", "Description", "IsEnforced")
                SELECT 'Access.Review', 'Generate, record findings on and sign the periodic access review (who holds which access).', TRUE
                WHERE NOT EXISTS (SELECT 1 FROM "Permissions" WHERE "Code" = 'Access.Review');
                """);
            migrationBuilder.Sql("""UPDATE "Permissions" SET "IsEnforced" = TRUE WHERE "Code" = 'Access.Review';""");

            migrationBuilder.Sql(
                "DO $$\n" + Declarations + "\nBEGIN\n" +
                """
                FOR r IN SELECT "Id" FROM "Roles" WHERE "Type" = 0 AND "IsSystemRole" LOOP
                """ + "\n" + SnapshotPrevious + "\n" +
                """
                    INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
                    SELECT r."Id", p."Id" FROM "Permissions" p
                    WHERE p."Code" = 'Access.Review'
                      AND NOT EXISTS (SELECT 1 FROM "RolePermissions" rp WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id");

                """ + "\n" + AuditAndBump.Replace("{PROCESS}", "Migration 20261028011340_AddAccessReviews").Replace("{REASON}", Reason) +
                "\nEND LOOP;\nEND $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DO $$\n" + Declarations + "\nBEGIN\n" +
                """
                FOR r IN SELECT "Id" FROM "Roles" WHERE "Type" = 0 AND "IsSystemRole" LOOP
                """ + "\n" + SnapshotPrevious + "\n" +
                """
                    DELETE FROM "RolePermissions"
                    WHERE "RoleId" = r."Id"
                      AND "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" = 'Access.Review');

                """ + "\n" + AuditAndBump.Replace("{PROCESS}", "Migration 20261028011340_AddAccessReviews (Down)").Replace("{REASON}", "Access review grant rolled back (migration Down)") +
                "\nEND LOOP;\nEND $$;");

            // Any other role that was granted the code is cleared too, so the permission row can go.
            migrationBuilder.Sql("""
                DELETE FROM "RolePermissions"
                WHERE "PermissionId" IN (SELECT "Id" FROM "Permissions" WHERE "Code" = 'Access.Review');
                """);
            migrationBuilder.Sql("""DELETE FROM "Permissions" WHERE "Code" = 'Access.Review';""");

            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_accessreviewfindings_no_truncate ON "AccessReviewFindings";
                DROP TRIGGER IF EXISTS trg_accessreviewfindings_immutable ON "AccessReviewFindings";
                DROP TRIGGER IF EXISTS trg_accessreviews_no_truncate ON "AccessReviews";
                DROP TRIGGER IF EXISTS trg_accessreviews_immutable ON "AccessReviews";
                DROP FUNCTION IF EXISTS prevent_access_review_finding_modification();
                DROP FUNCTION IF EXISTS prevent_access_review_modification();
                """);

            migrationBuilder.DropTable(
                name: "AccessReviewFindings");

            migrationBuilder.DropTable(
                name: "AccessReviews");
        }
    }
}
