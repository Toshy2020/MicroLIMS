using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroLIMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowOosInvalidationReversalOnReopen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_oos_investigation_result_modification()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        IF (to_jsonb(NEW) - 'IsInvalidated' - 'InvalidatedAtUtc')
                               = (to_jsonb(OLD) - 'IsInvalidated' - 'InvalidatedAtUtc') THEN
                            IF NOT OLD."IsInvalidated" AND NEW."IsInvalidated" THEN
                                RETURN NEW;
                            END IF;
                            IF OLD."IsInvalidated" AND NOT NEW."IsInvalidated" AND NEW."InvalidatedAtUtc" IS NULL
                               AND EXISTS (
                                   SELECT 1
                                   FROM "OosInvestigationReopenings" reopening
                                   JOIN "OosInvestigations" investigation ON investigation."Id" = reopening."InvestigationId"
                                   JOIN "TestOrders" test_order ON test_order."Id" = OLD."TestOrderId"
                                   JOIN "ElectronicSignatures" signature ON signature."Id" = reopening."SignatureId"
                                   WHERE investigation."Id" = OLD."InvestigationId"
                                     AND test_order."SampleId" = investigation."SampleId"
                                     AND reopening.xmin = pg_current_xact_id()::xid
                                     AND signature.xmin = reopening.xmin
                                     AND reopening."PreviousConclusion" = 1 -- Invalidated
                                     AND signature."EntityType" = 'OosInvestigation'
                                     AND signature."EntityId" = investigation."Id"
                                     AND signature."UserId" = reopening."ReopenedByUserId"
                                     AND signature."MeaningOfSignature" = 42 -- OosInvestigationReopened
                               ) THEN
                                RETURN NEW;
                            END IF;
                        END IF;
                    END IF;
                    RAISE EXCEPTION 'OOS investigation result % is immutable except for invalidation and signed origin reopening.', OLD."Id";
                END;
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_oos_investigation_result_modification()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        IF NOT OLD."IsInvalidated" AND NEW."IsInvalidated"
                           AND (to_jsonb(NEW) - 'IsInvalidated' - 'InvalidatedAtUtc')
                               = (to_jsonb(OLD) - 'IsInvalidated' - 'InvalidatedAtUtc') THEN
                            RETURN NEW;
                        END IF;
                    END IF;
                    RAISE EXCEPTION 'OOS investigation result % is immutable except for one-way invalidation.', OLD."Id";
                END;
                $$;
                """);
        }
    }
}
