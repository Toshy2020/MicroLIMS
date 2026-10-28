-- append-negative-entityid-corrections.sql   (WP2.9, decision D8)
--
-- WHY: before the two-phase capture in MicroLimsDbContext (CapturePendingAuditEntries /
-- AddAuditLogs), the automatic audit trail read the record id BEFORE the insert. On PostgreSQL
-- a new row only has a temporary EF key (a large negative number) at that point, so old "Create"
-- rows carry a negative AuditLogs.EntityId, and any foreign key to another new row in the same
-- save is a negative number inside the NewValue JSON. The bug is fixed going forward; the old
-- rows remain, and AuditLogs is append-only (trigger trg_auditlogs_immutable rejects UPDATE and
-- DELETE), so they are never edited.
--
-- WHAT: for every AuditLogs row whose EntityId is negative and that has no correction yet,
-- APPEND one correction row:
--     EntityName 'AuditLog', EntityId = the original row's Id,
--     Action/ActionCode 'AuditEntityIdCorrection', ActorType 2 (System),
--     SystemProcessName 'append-negative-entityid-corrections.sql',
--     ActionCategory 9 (AuditActionCategory.Other), UserId NULL,
--     NewValue = JSON { originalAuditLogId, originalEntityName, originalEntityId,
--                       resolvedEntityId (or null), resolution }.
-- Nothing is updated or deleted. The script is idempotent: a row that already has a correction
-- is skipped, so it can be re-run safely.
--
-- HOW THE REAL ID IS RESOLVED (conservative; null when not provable):
--   The first later "Update" of the same record carries the record's state in PreviousValue.
--   If that state equals the Create row's NewValue once "Id" and every negative-valued key
--   (temporary foreign keys) are removed from both, AND all such matches name exactly one id,
--   AND that id has no Create row of its own, the id is reported with resolution
--   'matched-later-update'. Otherwise resolvedEntityId is null and resolution is 'unresolved'
--   (records never updated cannot be matched; they are still flagged so reviewers know).
--
-- USAGE (psql; run as a role that may INSERT into AuditLogs):
--   Dry run (default, SELECT only, writes nothing):
--     psql -v ON_ERROR_STOP=1 -f scripts/audit/append-negative-entityid-corrections.sql "$CONN"
--   Apply (inserts the correction rows in one transaction):
--     psql -v ON_ERROR_STOP=1 -v apply=1 -f scripts/audit/append-negative-entityid-corrections.sql "$CONN"

\if :{?apply}
\else
\set apply 0
\endif

\echo ''
\if :apply
\echo '== APPLY MODE: correction rows will be appended =='
\else
\echo '== DRY RUN: nothing is written; showing what would be inserted =='
\endif

BEGIN;

CREATE TEMP TABLE _entityid_corrections ON COMMIT DROP AS
WITH neg AS (
    SELECT a."Id" AS audit_id, a."EntityName" AS entity_name, a."EntityId" AS entity_id, a."Action" AS action,
           CASE WHEN a."NewValue" IS JSON OBJECT THEN a."NewValue"::jsonb END AS nv
    FROM "AuditLogs" a
    WHERE a."EntityId" ~ '^-[0-9]+$'
      AND NOT EXISTS (SELECT 1 FROM "AuditLogs" c
                      WHERE c."ActionCode" = 'AuditEntityIdCorrection'
                        AND c."EntityName" = 'AuditLog'
                        AND c."EntityId" = a."Id"::text)
), keyed AS (
    SELECT n.*,
           ARRAY['Id']::text[] || COALESCE((SELECT array_agg(e.key)
                                            FROM jsonb_each(n.nv) e
                                            WHERE jsonb_typeof(e.value) = 'number' AND (e.value #>> '{}')::numeric < 0),
                                           '{}'::text[]) AS strip_keys
    FROM neg n
), matches AS (
    SELECT k.audit_id, array_agg(DISTINCT u."EntityId") AS ids
    FROM keyed k
    JOIN "AuditLogs" u
      ON k.action = 'Create' AND k.nv IS NOT NULL
     AND u."EntityName" = k.entity_name
     AND u."Action" = 'Update'
     AND u."EntityId" ~ '^[0-9]+$'
     AND u."Id" > k.audit_id
     AND CASE WHEN u."PreviousValue" IS JSON OBJECT
              THEN (u."PreviousValue"::jsonb - k.strip_keys) = (k.nv - k.strip_keys) END
    GROUP BY k.audit_id
)
SELECT k.audit_id, k.entity_name, k.entity_id, k.action,
       CASE WHEN cardinality(m.ids) = 1
             AND NOT EXISTS (SELECT 1 FROM "AuditLogs" p
                             WHERE p."EntityName" = k.entity_name AND p."EntityId" = m.ids[1] AND p."Action" = 'Create')
            THEN m.ids[1] END AS resolved_entity_id
FROM keyed k
LEFT JOIN matches m ON m.audit_id = k.audit_id;

\echo ''
\echo 'Rows that need (or would receive) a correction:'
SELECT audit_id AS original_audit_id, entity_name, entity_id AS negative_entity_id, action,
       resolved_entity_id,
       CASE WHEN resolved_entity_id IS NULL THEN 'unresolved' ELSE 'matched-later-update' END AS resolution
FROM _entityid_corrections
ORDER BY audit_id;

SELECT count(*) AS rows_to_correct,
       count(resolved_entity_id) AS resolved,
       count(*) - count(resolved_entity_id) AS unresolved
FROM _entityid_corrections;

\if :apply
INSERT INTO "AuditLogs" (
    "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
    "EventUid", "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason", "SourceContext")
SELECT 'AuditLog', c.audit_id::text, 'AuditEntityIdCorrection', NULL,
       jsonb_build_object(
           'originalAuditLogId', c.audit_id,
           'originalEntityName', c.entity_name,
           'originalEntityId', c.entity_id,
           'resolvedEntityId', c.resolved_entity_id,
           'resolution', CASE WHEN c.resolved_entity_id IS NULL THEN 'unresolved' ELSE 'matched-later-update' END)::text,
       NULL, now(),
       'EVT-' || lpad(nextval('audit_event_seq')::text, 7, '0'),
       2, 'append-negative-entityid-corrections.sql', 'AuditEntityIdCorrection', 9,
       'Data integrity: audit row ' || c.audit_id || ' recorded a temporary negative EntityId (' || c.entity_id ||
       ') captured before the insert; the original row is unchanged (append-only).',
       'AuditCorrectionScript'
FROM _entityid_corrections c
ORDER BY c.audit_id;
\echo 'Correction rows appended.'
\else
\echo 'Dry run complete: no rows written. Re-run with -v apply=1 to append them.'
\endif

COMMIT;
