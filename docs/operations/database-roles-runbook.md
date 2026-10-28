# Database roles runbook (production)

Decision D6 of `admin-panel-remediation-plan.md` (WP2.5): in production the
application must not connect as the role that owns the schema. Triggers
(see below) stop an application bug or a compromised application from
changing audit evidence, but a table **owner** can still `ALTER TABLE ...
DISABLE TRIGGER`, `DROP TRIGGER` or `TRUNCATE`. Only a separate, non-owner
runtime role closes that gap. This is configuration, not code: nothing in the
repository enforces it, so it must be applied and verified per environment.

## 1. Layers of protection

| Layer | What it blocks | Where it is defined |
|---|---|---|
| Row trigger `prevent_audit_modification()` | UPDATE / DELETE | `20260902181230_AddAuditImmutabilityTriggers` (AuditLogs, AuditEventChanges, ElectronicSignatures); `20260904160000_AddDocumentAcknowledgementRecords` (DocumentAcknowledgementRecords); `20261027050929_HardenAppendOnlyAuditTables` (LoginHistories, SecurityAuditEvents) |
| Row triggers with their own functions | DELETE always; UPDATE conditionally | `prevent_workflowprofileversion_modification()` on WorkflowProfileVersions (UPDATE only while Draft); `prevent_ingest_payload_modification()`, `prevent_ingest_version_modification()`, `prevent_ingest_modification()` on InstrumentIngestPayloads / InstrumentIngestVersions / InstrumentIngests (`20261023182835_AddInstrumentIngest`) |
| Statement trigger `prevent_audit_truncate()` (`trg_<table>_no_truncate`) | TRUNCATE on all ten tables listed in section 3 | `20261027050929_HardenAppendOnlyAuditTables` |
| FK `ON DELETE RESTRICT` on `SecurityAuditEvents.ActorUserId` / `TargetUserId` | Deleting a user the events reference (previously SET NULL, which would have been an UPDATE of the evidence) | same migration |
| **Non-owner runtime role (this runbook)** | `DROP/DISABLE TRIGGER`, `TRUNCATE`, any DDL, and UPDATE/DELETE on audit tables even if a trigger were removed | database grants |

Triggers do not protect against the table owner or a superuser. Section 5
verifies the role setup.

## 2. Roles

| Role | Purpose | Privileges |
|---|---|---|
| `microlims_owner` | Owns the database objects. Used **only** to apply EF Core migrations (and the first-run seed). Never used by the running API. | Owner of all schema objects; `LOGIN` only from the migration job / DBA host. |
| `microlims_app` | Connection used by the running API. | `LOGIN`, `NOSUPERUSER NOCREATEDB NOCREATEROLE`, owns no object. Table grants as in section 3. |

Names are examples; substitute your own. Database name below: `microlims`,
schema `public`.

## 3. Grants

Tables are grouped by what the application legitimately does to them.

**Strictly append-only: `SELECT, INSERT` only** (no UPDATE, DELETE, TRUNCATE):

- `AuditLogs`
- `AuditEventChanges`
- `ElectronicSignatures`
- `DocumentAcknowledgementRecords`
- `LoginHistories`
- `SecurityAuditEvents`
- `InstrumentIngestPayloads`

**Append-only with a permitted in-place transition: `SELECT, INSERT, UPDATE`** (no DELETE, TRUNCATE).
The row triggers restrict which updates succeed, but the role must be allowed to attempt them:

- `WorkflowProfileVersions` (a Draft is updated when signing makes it Effective)
- `InstrumentIngestVersions` (only the review outcome may change)
- `InstrumentIngests` (identity columns immutable)

**Everything else:** normal DML (`SELECT, INSERT, UPDATE, DELETE`). Never `TRUNCATE`, `REFERENCES`, `TRIGGER`, or any DDL.

**`"__EFMigrationsHistory"`:** `SELECT` only (the readiness health check reads it; only the owner applies migrations).

**Sequences:** `USAGE, SELECT` on all sequences (`DatabaseSequenceHelper` calls `nextval`, and identity columns need it on INSERT).

```sql
-- Run as a superuser / the database admin, once per environment.
CREATE ROLE microlims_owner LOGIN PASSWORD '<secret>';
CREATE ROLE microlims_app   LOGIN PASSWORD '<secret>'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT;

-- The owner role owns the database and schema (fresh install) ...
ALTER DATABASE microlims OWNER TO microlims_owner;
\c microlims
ALTER SCHEMA public OWNER TO microlims_owner;
-- ... or, on an existing database, reassign what the old owner holds:
--   REASSIGN OWNED BY <old_app_role> TO microlims_owner;

REVOKE ALL ON DATABASE microlims FROM PUBLIC;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT CONNECT ON DATABASE microlims TO microlims_app;
GRANT USAGE   ON SCHEMA public TO microlims_app;   -- note: no CREATE
```

The grant script below is **idempotent and must be re-run after every
migration** (new tables otherwise get no privileges, or inherit
default privileges that are too wide). Run it as `microlims_owner`.

```sql
BEGIN;

-- 1. Baseline: normal DML on every table, then narrow the audit tables.
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM microlims_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO microlims_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO microlims_app;

-- 2. Strictly append-only: SELECT, INSERT only.
REVOKE ALL ON "AuditLogs", "AuditEventChanges", "ElectronicSignatures",
              "DocumentAcknowledgementRecords", "LoginHistories",
              "SecurityAuditEvents", "InstrumentIngestPayloads"
    FROM microlims_app;
GRANT SELECT, INSERT ON "AuditLogs", "AuditEventChanges", "ElectronicSignatures",
              "DocumentAcknowledgementRecords", "LoginHistories",
              "SecurityAuditEvents", "InstrumentIngestPayloads"
    TO microlims_app;

-- 3. Append-only with a guarded UPDATE: SELECT, INSERT, UPDATE (no DELETE).
REVOKE ALL ON "WorkflowProfileVersions", "InstrumentIngestVersions", "InstrumentIngests"
    FROM microlims_app;
GRANT SELECT, INSERT, UPDATE ON "WorkflowProfileVersions", "InstrumentIngestVersions",
              "InstrumentIngests"
    TO microlims_app;

-- 4. Migration history is read-only for the runtime role.
REVOKE ALL ON "__EFMigrationsHistory" FROM microlims_app;
GRANT SELECT ON "__EFMigrationsHistory" TO microlims_app;

-- 5. Future tables: do NOT grant automatically (they would bypass step 2/3).
ALTER DEFAULT PRIVILEGES FOR ROLE microlims_owner IN SCHEMA public
    REVOKE ALL ON TABLES FROM microlims_app;
ALTER DEFAULT PRIVILEGES FOR ROLE microlims_owner IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO microlims_app;

COMMIT;
```

Whenever a new append-only table is added (a new `prevent_*` trigger in a
migration), add it to step 2 or 3 and to the verification queries in section 5.

## 4. Connection strings and the migration step

The application reads `ConnectionStrings__Default`
(`backend/MicroLIMS.API/Program.cs`). It runs `db.Database.Migrate()` plus the
seeder on startup **only** when `ASPNETCORE_ENVIRONMENT=Development` or
`APPLY_MIGRATIONS=true`. In production:

1. **Migration step** (deployment job, before the new version receives traffic):
   run the API once with the **owner** credentials and migrations enabled, then stop it:

   ```bash
   APPLY_MIGRATIONS=true \
   Seed__InitialAdminPassword=<only on first install> \
   ConnectionStrings__Default="Host=<db>;Database=microlims;Username=microlims_owner;Password=<owner secret>" \
   dotnet MicroLIMS.API.dll   # exits when stopped after "Database migrations applied successfully."
   ```

   Alternatively generate an idempotent script
   (`dotnet ef migrations script --idempotent ...`) and run it with `psql` as
   `microlims_owner`.
2. Re-run the grant script from section 3 as `microlims_owner`.
3. **Runtime**: start the API with `APPLY_MIGRATIONS` unset/false and
   `ConnectionStrings__Default="Host=<db>;Database=microlims;Username=microlims_app;Password=<app secret>"`.
   Keep the two secrets in separate secret-store entries; the owner secret is
   available only to the deployment job.

If the runtime role cannot see a table (step 2 forgotten after a migration),
the API reports permission errors at first use. Fix by re-running the grants,
never by granting the app role ownership.

## 5. Verification queries

Run as a superuser or `microlims_owner`.

```sql
-- (a) The runtime role owns nothing and is not privileged.
SELECT rolname, rolsuper, rolcreatedb, rolcreaterole FROM pg_roles WHERE rolname = 'microlims_app';
SELECT count(*) AS owned_tables FROM pg_tables WHERE schemaname='public' AND tableowner='microlims_app';  -- expect 0

-- (b) Privileges on the append-only tables (expect INSERT/SELECT; UPDATE only on the three guarded tables).
SELECT table_name, string_agg(privilege_type, ', ' ORDER BY privilege_type) AS privileges
FROM information_schema.role_table_grants
WHERE grantee = 'microlims_app'
  AND table_name IN ('AuditLogs','AuditEventChanges','ElectronicSignatures','DocumentAcknowledgementRecords',
                     'LoginHistories','SecurityAuditEvents','InstrumentIngestPayloads',
                     'WorkflowProfileVersions','InstrumentIngestVersions','InstrumentIngests')
GROUP BY table_name ORDER BY table_name;

-- (c) The runtime role cannot do the dangerous things.
SELECT has_table_privilege('microlims_app', '"AuditLogs"', 'UPDATE')   AS audit_update,    -- expect f
       has_table_privilege('microlims_app', '"AuditLogs"', 'DELETE')   AS audit_delete,    -- expect f
       has_table_privilege('microlims_app', '"AuditLogs"', 'TRUNCATE') AS audit_truncate,  -- expect f
       has_table_privilege('microlims_app', '"AuditLogs"', 'TRIGGER')  AS audit_trigger,   -- expect f
       has_schema_privilege('microlims_app', 'public', 'CREATE')       AS schema_create;   -- expect f

-- (d) Every append-only table has both its modification trigger and its TRUNCATE trigger enabled
--     (tgenabled = 'O' means enabled; 'D' means someone disabled it).
SELECT c.relname, t.tgname, t.tgenabled
FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid
WHERE NOT t.tgisinternal
  AND (t.tgname LIKE 'trg\_%\_immutable' OR t.tgname LIKE 'trg\_%\_no\_truncate')
ORDER BY c.relname, t.tgname;   -- expect 10 *_no_truncate rows and 10 *_immutable rows, all 'O'

-- (e) Behavioural check as the runtime role (each statement must fail; run inside a transaction and ROLLBACK).
--   psql "host=<db> dbname=microlims user=microlims_app"
--   UPDATE "AuditLogs" SET "Action" = "Action" WHERE false;   -- ERROR: permission denied
--   TRUNCATE "SecurityAuditEvents";                           -- ERROR: permission denied
--   ALTER TABLE "AuditLogs" DISABLE TRIGGER ALL;              -- ERROR: must be owner
```

Repeat (a)-(d) after every deployment and whenever privileges are changed.
Any change that makes a check differ from the expected value must be raised
as a deviation: audit evidence integrity can no longer be assumed.
