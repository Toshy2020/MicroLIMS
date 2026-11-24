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
                            IF OLD."IsInvalidated" AND OLD."InvalidatedAtUtc" IS NOT NULL
                               AND NOT NEW."IsInvalidated" AND NEW."InvalidatedAtUtc" IS NULL
                               AND EXISTS (
                                   SELECT 1
                                   FROM "OosInvestigations" investigation
                                   JOIN "TestOrders" test_order ON test_order."Id" = OLD."TestOrderId"
                                   JOIN "ElectronicSignatures" conclusion ON conclusion."Id" = investigation."ConclusionSignatureId"
                                   WHERE investigation."Id" = OLD."InvestigationId"
                                     AND test_order."SampleId" = investigation."SampleId"
                                     AND investigation."Status" = 3 -- Concluded
                                     AND conclusion."EntityType" = 'OosInvestigation'
                                     AND conclusion."EntityId" = investigation."Id"
                                     AND conclusion."UserId" = investigation."ConcludedByUserId"
                                     AND conclusion."MeaningOfSignature" = 41 -- OosInvestigationConcluded
                                     AND (
                                         (investigation."Conclusion" = 0 -- Confirmed
                                          AND investigation.xmin = pg_current_xact_id()::xid
                                          AND conclusion.xmin = investigation.xmin)
                                         OR
                                         (investigation."Conclusion" = 1 -- Invalidated
                                          -- PostgreSQL xid age orders the prior signature before this top-level
                                          -- transaction across xid wraparound, including when savepoints are used.
                                          AND age(conclusion.xmin) > age(pg_current_xact_id()::xid)
                                          AND EXISTS (
                                              SELECT 1
                                              FROM "OosInvestigationReopenings" reopening
                                              JOIN "ElectronicSignatures" signature ON signature."Id" = reopening."SignatureId"
                                              WHERE reopening."InvestigationId" = investigation."Id"
                                                AND reopening."PreviousConclusion" = investigation."Conclusion"
                                                AND reopening."PreviousConclusionSignatureId" = conclusion."Id"
                                                AND reopening.xmin = pg_current_xact_id()::xid
                                                AND signature.xmin = reopening.xmin
                                                AND signature."EntityType" = 'OosInvestigation'
                                                AND signature."EntityId" = investigation."Id"
                                                AND signature."UserId" = reopening."ReopenedByUserId"
                                                AND signature."MeaningOfSignature" = 42 -- OosInvestigationReopened
                                          ))
                                     )
                               ) THEN
                                RETURN NEW;
                            END IF;
                        END IF;
                    END IF;
                    RAISE EXCEPTION 'OOS investigation result % is immutable except for invalidation and signed origin reversal.', OLD."Id";
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
