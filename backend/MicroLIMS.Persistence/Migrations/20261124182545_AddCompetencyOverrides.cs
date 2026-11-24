using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetencyOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompetencyOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TestOrderId = table.Column<int>(type: "integer", nullable: false),
                    SampleId = table.Column<int>(type: "integer", nullable: false),
                    AnalystUserId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    GrantedByUserId = table.Column<int>(type: "integer", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SignatureId = table.Column<int>(type: "integer", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndReason = table.Column<int>(type: "integer", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompetencyOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompetencyOverrides_ElectronicSignatures_SignatureId",
                        column: x => x.SignatureId,
                        principalTable: "ElectronicSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetencyOverrides_Samples_SampleId",
                        column: x => x.SampleId,
                        principalTable: "Samples",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetencyOverrides_TestOrders_TestOrderId",
                        column: x => x.TestOrderId,
                        principalTable: "TestOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetencyOverrides_Users_AnalystUserId",
                        column: x => x.AnalystUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompetencyOverrides_Users_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompetencyOverrides_ActiveOrder",
                table: "CompetencyOverrides",
                column: "TestOrderId",
                unique: true,
                filter: " \"EndedAtUtc\" IS NULL ");

            migrationBuilder.CreateIndex(
                name: "IX_CompetencyOverrides_AnalystUserId",
                table: "CompetencyOverrides",
                column: "AnalystUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetencyOverrides_GrantedByUserId",
                table: "CompetencyOverrides",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CompetencyOverrides_SampleId_GrantedByUserId",
                table: "CompetencyOverrides",
                columns: new[] { "SampleId", "GrantedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompetencyOverrides_SignatureId",
                table: "CompetencyOverrides",
                column: "SignatureId",
                unique: true);

            migrationBuilder.Sql("""
                UPDATE "Permissions" SET "IsEnforced" = TRUE WHERE "Code" = 'Competency.Override';

                CREATE FUNCTION protect_competency_override() RETURNS trigger AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW."EndedAtUtc" IS NULL AND NEW."EndReason" IS NULL
                            AND NEW."Reason" ~ '[^[:space:]]'
                            AND NEW."GrantedByUserId" <> NEW."AnalystUserId"
                            AND EXISTS (
                                SELECT 1 FROM "TestOrders" t
                                WHERE t."Id" = NEW."TestOrderId" AND t."SampleId" = NEW."SampleId"
                            )
                            AND EXISTS (
                                SELECT 1 FROM "ElectronicSignatures" s
                                WHERE s."Id" = NEW."SignatureId" AND s."UserId" = NEW."GrantedByUserId"
                                    AND s."EntityType" = 'CompetencyOverride' AND s."EntityId" = NEW."Id"
                                    AND s."MeaningOfSignature" = 47 AND s."Comment" = NEW."Reason"
                                    AND s."SignedAt" = NEW."GrantedAtUtc"
                            )
                        THEN RETURN NEW;
                        END IF;
                    ELSIF TG_OP = 'UPDATE' THEN
                        IF OLD."EndedAtUtc" IS NULL AND OLD."EndReason" IS NULL
                            AND NEW."EndedAtUtc" >= OLD."GrantedAtUtc" AND NEW."EndReason" BETWEEN 0 AND 3
                            AND (to_jsonb(NEW) - 'EndedAtUtc' - 'EndReason') = (to_jsonb(OLD) - 'EndedAtUtc' - 'EndReason')
                        THEN RETURN NEW;
                        END IF;
                    END IF;
                    RAISE EXCEPTION 'append-only: competency overrides require a matching signature and can only be ended once';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_competencyoverrides_immutable BEFORE INSERT OR UPDATE OR DELETE ON "CompetencyOverrides"
                FOR EACH ROW EXECUTE FUNCTION protect_competency_override();
                CREATE TRIGGER trg_competencyoverrides_no_truncate BEFORE TRUNCATE ON "CompetencyOverrides"
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_truncate();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompetencyOverrides");

            migrationBuilder.Sql("""
                DROP FUNCTION protect_competency_override();
                UPDATE "Permissions" SET "IsEnforced" = FALSE WHERE "Code" = 'Competency.Override';
                """);
        }
    }
}
