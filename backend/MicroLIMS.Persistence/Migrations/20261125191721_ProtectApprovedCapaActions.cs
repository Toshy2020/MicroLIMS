using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProtectApprovedCapaActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION prevent_approved_capa_action_modification()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "QualityRecords" r
                        JOIN "CapaDetails" c ON c."RecordId" = r."Id"
                        WHERE r."Id" = OLD."RecordId"
                          AND r."Type" = 1
                          AND (r."IsCancelled" OR c."Status" IN (1, 2, 3, 4))
                    ) THEN
                        IF NEW."RecordId" IS DISTINCT FROM OLD."RecordId"
                           OR NEW."Kind" IS DISTINCT FROM OLD."Kind"
                           OR NEW."Description" IS DISTINCT FROM OLD."Description"
                           OR NEW."ResponsibleUserId" IS DISTINCT FROM OLD."ResponsibleUserId"
                           OR NEW."DueDate" IS DISTINCT FROM OLD."DueDate" THEN
                            RAISE EXCEPTION 'CAPA action % planned fields cannot be modified after plan approval or cancellation.', OLD."Id";
                        END IF;

                        IF (OLD."DoneAtUtc" IS NOT NULL AND NEW."DoneAtUtc" IS DISTINCT FROM OLD."DoneAtUtc")
                           OR (OLD."DoneByUserId" IS NOT NULL AND NEW."DoneByUserId" IS DISTINCT FROM OLD."DoneByUserId")
                           OR (OLD."Evidence" IS NOT NULL AND NEW."Evidence" IS DISTINCT FROM OLD."Evidence")
                           OR (OLD."EvidenceLinkId" IS NOT NULL AND NEW."EvidenceLinkId" IS DISTINCT FROM OLD."EvidenceLinkId") THEN
                            RAISE EXCEPTION 'CAPA action % completion fields cannot be modified or cleared once recorded.', OLD."Id";
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER trg_qualityactions_capa_immutable
                BEFORE UPDATE ON "QualityActions"
                FOR EACH ROW EXECUTE FUNCTION prevent_approved_capa_action_modification();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_qualityactions_capa_immutable ON "QualityActions";
                DROP FUNCTION IF EXISTS prevent_approved_capa_action_modification();
                """);
        }
    }
}
