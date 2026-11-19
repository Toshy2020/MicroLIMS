using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditTrailReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditTrailReviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PeriodFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScopeKind = table.Column<int>(type: "integer", nullable: false),
                    ScopeEntityName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ScopeRecordId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CutoffAuditLogId = table.Column<int>(type: "integer", nullable: false),
                    EntryCount = table.Column<int>(type: "integer", nullable: false),
                    StartedByUserId = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: true),
                    Conclusion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SignedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ElectronicSignatureId = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditTrailReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditTrailReviews_ElectronicSignatures_ElectronicSignatureId",
                        column: x => x.ElectronicSignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditTrailReviews_Users_StartedByUserId",
                        column: x => x.StartedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditTrailReviewFindings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReviewId = table.Column<int>(type: "integer", nullable: false),
                    AuditLogId = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClosedComment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ClosedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecordedByUserId = table.Column<int>(type: "integer", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditTrailReviewFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditTrailReviewFindings_AuditLogs_AuditLogId",
                        column: x => x.AuditLogId,
                        principalTable: "AuditLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditTrailReviewFindings_AuditTrailReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "AuditTrailReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditTrailReviewFindings_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditTrailReviewFindings_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrailReviewFindings_AuditLogId",
                table: "AuditTrailReviewFindings",
                column: "AuditLogId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrailReviewFindings_ClosedByUserId",
                table: "AuditTrailReviewFindings",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrailReviewFindings_RecordedByUserId",
                table: "AuditTrailReviewFindings",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrailReviewFindings_ReviewId_AuditLogId",
                table: "AuditTrailReviewFindings",
                columns: new[] { "ReviewId", "AuditLogId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrailReviewFindings_Status",
                table: "AuditTrailReviewFindings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrailReviews_ElectronicSignatureId",
                table: "AuditTrailReviews",
                column: "ElectronicSignatureId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrailReviews_StartedAtUtc",
                table: "AuditTrailReviews",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrailReviews_StartedByUserId",
                table: "AuditTrailReviews",
                column: "StartedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrailReviews_Status",
                table: "AuditTrailReviews",
                column: "Status");

            // Immutability (21 CFR Part 11 / EU Annex 11).
            // AuditTrailReviews: DELETE never; UPDATE rejected once Signed (Status = 1); TRUNCATE never.
            // AuditTrailReviewFindings: DELETE and TRUNCATE never; UPDATE allowed only once (Open -> Closed)
            // on the close fields (Status, ClosedComment, ClosedByUserId, ClosedAtUtc) with every other column unchanged.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_audit_trail_review_modification()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Table % is append-only: DELETE is prohibited by GMP regulations (21 CFR Part 11, EU Annex 11).', TG_TABLE_NAME;
                    END IF;
                    IF OLD."Status" = 1 THEN
                        RAISE EXCEPTION 'Audit trail review % is signed and immutable (21 CFR Part 11, EU Annex 11).', OLD."Id";
                    END IF;
                    -- The scope is fixed when the review starts: it never changes, signed or not.
                    IF NEW."Id" IS DISTINCT FROM OLD."Id"
                       OR NEW."PeriodFromUtc" IS DISTINCT FROM OLD."PeriodFromUtc"
                       OR NEW."PeriodToUtc" IS DISTINCT FROM OLD."PeriodToUtc"
                       OR NEW."ScopeKind" IS DISTINCT FROM OLD."ScopeKind"
                       OR NEW."ScopeEntityName" IS DISTINCT FROM OLD."ScopeEntityName"
                       OR NEW."ScopeRecordId" IS DISTINCT FROM OLD."ScopeRecordId"
                       OR NEW."CutoffAuditLogId" IS DISTINCT FROM OLD."CutoffAuditLogId"
                       OR NEW."EntryCount" IS DISTINCT FROM OLD."EntryCount"
                       OR NEW."StartedByUserId" IS DISTINCT FROM OLD."StartedByUserId"
                       OR NEW."StartedAtUtc" IS DISTINCT FROM OLD."StartedAtUtc" THEN
                        RAISE EXCEPTION 'Audit trail review % scope is fixed at start and cannot be modified.', OLD."Id";
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE OR REPLACE FUNCTION prevent_audit_trail_review_finding_modification()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'Table % is append-only: DELETE is prohibited by GMP regulations (21 CFR Part 11, EU Annex 11).', TG_TABLE_NAME;
                    END IF;
                    IF OLD."Status" <> 0 THEN
                        RAISE EXCEPTION 'Audit trail review finding % is closed and immutable (21 CFR Part 11, EU Annex 11).', OLD."Id";
                    END IF;
                    IF NEW."Status" <> 1
                       OR NEW."ClosedComment" IS NULL
                       OR NEW."ClosedByUserId" IS NULL
                       OR NEW."ClosedAtUtc" IS NULL THEN
                        RAISE EXCEPTION 'Audit trail review finding % can only change by being closed (Open to Closed).', OLD."Id";
                    END IF;
                    IF NEW."Id" IS DISTINCT FROM OLD."Id"
                       OR NEW."ReviewId" IS DISTINCT FROM OLD."ReviewId"
                       OR NEW."AuditLogId" IS DISTINCT FROM OLD."AuditLogId"
                       OR NEW."Note" IS DISTINCT FROM OLD."Note"
                       OR NEW."RecordedByUserId" IS DISTINCT FROM OLD."RecordedByUserId"
                       OR NEW."RecordedAtUtc" IS DISTINCT FROM OLD."RecordedAtUtc" THEN
                        RAISE EXCEPTION 'Audit trail review finding % immutable columns cannot be modified.', OLD."Id";
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_audittrailreviews_immutable
                BEFORE UPDATE OR DELETE ON "AuditTrailReviews"
                FOR EACH ROW EXECUTE FUNCTION prevent_audit_trail_review_modification();

                CREATE TRIGGER trg_audittrailreviews_no_truncate
                BEFORE TRUNCATE ON "AuditTrailReviews"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();

                CREATE TRIGGER trg_audittrailreviewfindings_immutable
                BEFORE UPDATE OR DELETE ON "AuditTrailReviewFindings"
                FOR EACH ROW EXECUTE FUNCTION prevent_audit_trail_review_finding_modification();

                CREATE TRIGGER trg_audittrailreviewfindings_no_truncate
                BEFORE TRUNCATE ON "AuditTrailReviewFindings"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_audittrailreviewfindings_no_truncate ON "AuditTrailReviewFindings";
                DROP TRIGGER IF EXISTS trg_audittrailreviewfindings_immutable ON "AuditTrailReviewFindings";
                DROP TRIGGER IF EXISTS trg_audittrailreviews_no_truncate ON "AuditTrailReviews";
                DROP TRIGGER IF EXISTS trg_audittrailreviews_immutable ON "AuditTrailReviews";
                DROP FUNCTION IF EXISTS prevent_audit_trail_review_finding_modification();
                DROP FUNCTION IF EXISTS prevent_audit_trail_review_modification();
                """);

            migrationBuilder.DropTable(
                name: "AuditTrailReviewFindings");

            migrationBuilder.DropTable(
                name: "AuditTrailReviews");
        }
    }
}
