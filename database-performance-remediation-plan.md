# Database Performance Remediation Plan

Status: **plan only — nothing in this document is implemented yet.**
Date: 2026-11-15. Follows the read-only repository audit of the same date and
extends `slow-query-audit-findings.md`.

## 1. Why this plan exists

The schema is sound for a GMP system: foreign keys restrict deletes on
regulated history, concurrency uses PostgreSQL `xmin`, audit rows commit in
the same transaction as the change they describe, and multi-save commands are
wrapped by `UnitOfWork.RunAsync` (160 call sites). What is missing is indexing
on the busiest tables and set-based queries on dashboards and search.

Production is small today (about 8,700 audit rows in September 2026), so this
is a growth plan, not an incident response.

### How the audit measured

All 445 migrations were applied to a scratch PostgreSQL 16 database, which was
then loaded with synthetic volume: 150,000 samples, 600,000 test orders,
3,000,000 audit rows, 300,000 notifications. Queries were hand-written in the
shape EF Core emits (confirmed for the sample search with `ToQueryString`).

Limits of that evidence: synthetic data, PostgreSQL 16 rather than the
deployed major version, warm cache, localhost. Treat the figures as
indicative. Phase 0 replaces them with a maintained, repeatable measurement.

### Measured effect of candidate indexes

| Query | Before | After |
|---|---:|---:|
| E-signature failed-attempt check (`ElectronicSignatureService.cs:46`, runs on every signature) | 379 ms | 0.09 ms |
| Recent activity feed (`RecentActivityService`) | 622 ms | 0.04 ms |
| Audit trail for one sample | 318 ms | 0.11 ms |
| Audit trail for one record (`EntityName`, `EntityId`) | 286 ms | 0.03 ms |
| Unread notifications for one user | 21 ms | 0.03 ms |
| Open tests in a section / for an analyst | 32–33 ms | 0.2–1 ms |
| Samples received today | 22 ms | 0.03 ms |
| Global search on samples (trigram) | 91 ms | 0.4 ms |
| Global search on test orders (OR across a join) | 147 ms | 171 ms — not fixed by an index |

Other measurements:

- 3,000,000 synthetic audit rows occupied 2.7 GB (about 900 bytes per row).
- The four audit indexes cost about 345 MB at that volume.
- One `SaveChanges` is four round trips (begin, data, audit rows, commit):
  7–9 ms per save on localhost. Fifty inserts in one save are still two
  commands.
- Audit payloads as `jsonb` instead of `text`: 261 MB against 227 MB for the
  same 300,000 rows (15% larger). Faster only for queries that look inside the
  payload, which the application does not do.

### Catalogue findings

- 201 tables, 433 foreign keys, 770 indexes, 26 append-only triggers.
- Five foreign keys with no usable index: `SampleLocations.TestOrderId`,
  `DocumentTrainingConfigurations.DocumentMasterId`,
  `DocumentTrainingConfigurations.DocumentTypeId`,
  `DocumentEscalationRecords.ResolvedByUserId`, `NotApplicableTests.ItemId`.
- Fifteen indexes are a strict prefix of another index on the same table.
- `NotificationLogs` and `LoginHistories` have a primary key and nothing else.
- `AuditLogs` has no index on `Timestamp`, `UserId`, `SampleId`,
  `TestOrderId` or `(EntityName, EntityId)`.
- `TestOrders` has no index on `Status` or `AssignedAnalystId`; `Samples` has
  none on `ReceivedAt` or `Status`.
- No trigram or full-text index exists; the only extension is `plpgsql`.

## 2. Decisions taken (2026-11-15)

| Topic | Decision |
|---|---|
| Hosting | Production is on the free Render and Neon tiers. Upgrade to paid, always-on tiers in the same region **before** the dashboard work (Phase 3). |
| Audit retention | Audit records stay online in the live database for at least 5 years, then may be archived. The exact period and archive procedure go through QA change control. |
| Redundant indexes | The index migration adds the missing indexes and drops the 15 redundant ones together. Each dropped index is listed in the pull request. |
| Recording | This plan is committed now. Each agent rule in section 5 is added to `CLAUDE.md` in the same pull request that creates the test enforcing it. |
| Scope | Plan document only for now. No phase starts without a further instruction. |
| Result recording | `IncubationStepRecorder.RecordResultCoreAsync` (six saves in one transaction) is **deferred** until measurements on paid hosting show it is slow. Only the file-upload consistency fixes in Phase 5 proceed. |
| Caching | No caching for now. Reconsider only if a measured need remains after Phases 1–3. |
| Report range | Trend and comparison reports default to the last 12 months when no dates are given and accept at most 5 years. Aggregation moves into SQL. |
| JSON | No conversion of relational data or audit payloads to `jsonb`. |

## 3. Phases

Every phase is its own pull request. Phases 1–5 touch persistence or regulated
code, so each must pass the PostgreSQL integration run with zero skipped
`[PostgresFact]` tests (CLAUDE.md Rule 4.2).

### Phase 0 — Measurement harness (no production code)

- **Volume seed:** a maintained script under `scripts/` that loads realistic
  volume into a scratch database only. It must refuse to run against a
  database that is not a `microlims_test_*` or explicitly named scratch
  database.
- **Query-count helper:** a test fixture that counts SQL commands per service
  call so a test can assert an upper bound.
- **Baseline:** recorded query counts and timings for the Section Head,
  Reviewer and Analyst dashboards, global search, audit search and
  e-signature signing.

Exit: the baseline is committed and reproducible with one command.

### Phase 1 — Indexes (one migration)

Add:

| Table | Index | Serves |
|---|---|---|
| `AuditLogs` | `(UserId, Timestamp)` | E-signature failed-attempt check, user filter |
| `AuditLogs` | `(Timestamp)` | Recent activity, date-range search |
| `AuditLogs` | `(SampleId, Id)` where `SampleId` is not null | Audit trail per sample, KPI reassignment lookup |
| `AuditLogs` | `(TestOrderId, Id)` where `TestOrderId` is not null | Audit trail per test |
| `AuditLogs` | `(EntityName, EntityId, Id)` | Audit trail per record |
| `NotificationLogs` | `(UserId, IsRead, CreatedAt)` | Notification list and unread count |
| `LoginHistories` | user and time (exact columns confirmed against the entity when implementing) | Login history |
| `TestOrders` | `(SectionId, Status)` | Dashboard counts and queues |
| `TestOrders` | `(AssignedAnalystId, Status)` | Analyst workload |
| `Samples` | `(ReceivedAt)`, `(Status)` | Dashboard counts and trends |
| `Results` | `(EnteredByUserId, EnteredAt)` | Analyst metrics |
| Five tables | the five unindexed foreign keys listed in section 1 | Joins and restricted deletes |

Drop: the 15 prefix-redundant indexes, each named in the pull request with the
index that covers it. `(SectionId, Status)` makes the existing
`IX_TestOrders_SectionId` one of them.

Notes:

- Adding an index to `AuditLogs` does not rebuild the table, so its
  append-only triggers are untouched. `AppendOnlyTriggerCatalogPostgresTests`
  must still pass.
- Tables are small today, so plain index creation inside the migration is
  acceptable. Once `AuditLogs` is large, index changes on it must be built
  concurrently, outside the migration transaction.
- The free-text audit lookup columns (`BatchNumber`, `ControlNumber`,
  `SampleReferenceNumber`, `MediaLotNumber`, `ReferenceStrainCode`,
  `CryovialCode`) are **not** indexed in this phase. Decide after Phase 0
  shows how often the audit search filters on them.

New test: `IndexCatalogPostgresTests` — applies all migrations to an empty
database, then fails if any foreign key has no index whose leading columns
match it, or if an index on its required list is missing.

Exit: the Phase 0 baseline queries re-measured; before and after figures in
the pull request. Rule 10.1, 10.2 and 10.13 added to `CLAUDE.md`.

### Phase 2 — Global search

- **Ordering defect:** `GlobalSearchService` takes `limit * 2` rows with no
  `ORDER BY` and ranks them in memory (`SearchSamplesAsync`,
  `SearchTestOrdersAsync`, `SearchEquipmentAsync` and the remaining
  categories). When more rows match than the limit, an exact
  reference-number match can be left out. Move the ranking (exact match, then
  prefix, then newest) into SQL before `Take`. `SearchMaterialsAsync` already
  orders first and is the pattern to follow.
- **Regression test first:** seed more matches than the limit and assert the
  exact match is returned.
- **Trigram indexes:** enable `pg_trgm` and create expression indexes on
  `lower(column)` for the searched columns, with raw SQL in the migration.
  The query code keeps emitting `lower(col) LIKE @p`, so tests on the
  in-memory provider are unaffected.
- **Test-order search:** replace the OR across the `Samples` join with two
  indexed lookups combined in SQL.
- **Projection:** select only the fields the result needs instead of whole
  entities with four includes.

Open check before starting: confirm `pg_trgm` can be created by the
`microlims_ci` role in CI and on the production database.

Exit: query-count and timing figures in the pull request. Rules 10.5 and 10.6
added to `CLAUDE.md`.

### Hosting upgrade (before Phase 3)

Move the API and database to paid, always-on tiers in the same region. Set an
explicit pool size, command timeout and connection lifetime in the connection
string and document them in `DEPLOYMENT.md`. Re-run the Phase 0 baseline on
the new hosting so later phases are compared against it.

`EnableRetryOnFailure` stays off (CLAUDE.md Rule 5.4) until Phase 5 is done.

### Phase 3 — Dashboards and reports

| Location | Problem | Fix |
|---|---|---|
| `DashboardService.cs:635-639` | Two count queries per analyst | Two grouped queries for all analysts |
| `DashboardService.cs:209-210` | Loads sample graphs with test orders and sign-offs to take `.Count` | Count in SQL |
| `GetStatusDistributionAsync`, `GetKpiDeltasAsync` | Three to six counts over the same set | One grouped query each |
| `GetMonthlyTrendAsync` | Pulls every timestamp into memory | Group by month in SQL |
| `QueueEntriesAsync` and callers | Two collections included in one query, no split | Project to DTOs; `AsSplitQuery` only where both collections are needed |
| `DashboardService.cs:587` | Loads every user with role | Load only the users referenced |
| `ReportingQueryService` trend and compare | Unbounded history aggregated in memory | Default 12 months, maximum 5 years, aggregate in SQL |

These are read-only changes that feed regulated screens. Each must be proven
to return identical results on the seeded database, and each gets a
query-count assertion.

Exit: Rules 10.3, 10.4 and 10.7 added to `CLAUDE.md`.

### Phase 4 — Endpoint hygiene

- Remove `SampleRepository.GetAllAsync` after confirming it has no callers.
- Cap page sizes on the unbounded list endpoints recorded in
  `slow-query-audit-findings.md`, one controller at a time.
- Add an architecture-test ratchet that counts unbounded list endpoints and
  can only go down.

Exit: Rule 10.10 added to `CLAUDE.md`.

### Phase 5 — File-upload consistency

- **Evidence uploads outside a transaction** (`HplcRunService.UploadEvidenceAsync`
  and `SupersedeEvidenceAsync`, the ICP equivalents,
  `WorkingStandardService.UploadDocumentAsync`): the row is saved, the file
  written, then the row saved again. A storage failure leaves a row with no
  file. Make the two consistent.
- **File writes inside a transaction** (`OosInvestigationDocumentService`,
  `EquipmentDocumentService`, `CalendarEventService`,
  `MasterDataWorkbookService`): move the storage call outside the database
  transaction with cleanup on rollback.

Each change needs the GMP triad tests (persisted state, audit entry, negative
case for storage failure).

Deferred: reducing the six saves in
`IncubationStepRecorder.RecordResultCoreAsync`.

Exit: Rule 10.9 added to `CLAUDE.md`.

### Phase 6 — Audit trail growth (design only)

An architecture decision record covering:

- partitioning `AuditLogs` by time, with the append-only triggers re-created
  on every partition (CLAUDE.md Rule 7.4);
- an online window of at least 5 years, exact period set by QA;
- the archive procedure, how archived records remain readable for inspection,
  and how integrity is verified after the move.

No implementation until the record is approved through change control.

Exit: Rules 10.8, 10.11 and 10.12 added to `CLAUDE.md` (they need no new
test and may be added earlier if wanted).

## 4. Not in this plan

- Converting columns to `jsonb`.
- Server-side caching.
- Enabling `EnableRetryOnFailure`.
- Refactoring result recording.
- Mass-rewriting legacy queries that no phase names.

## 5. Proposed agent rules (CLAUDE.md Section 10)

These are **not yet in force**. Each is added to `CLAUDE.md` by the phase
named beside it, together with the test that enforces it. They are written to
sit beside the existing rules (Rule 5.4 already covers `AsNoTracking`,
pagination clamps and split queries).

> **Scope for existing code:** these rules bind every new or modified query,
> entity, index and migration. Do not mass-rewrite legacy queries as a side
> effect of another task; bring a method into compliance when you change it.

**Rule 10.1 — A query ships with its index (Phase 1).**
Before adding or changing a query that filters, joins or sorts on a column,
check the table's existing indexes in the model snapshot. If none serves the
predicate, add the index in the same pull request. Every foreign key column
must have an index whose leading columns match it.
`IndexCatalogPostgresTests` fails the build if a foreign key is unindexed or
a required index is missing; add new required indexes to its list.

**Rule 10.2 — Append-only and history tables are never scanned (Phase 1).**
`AuditLogs`, `SecurityAuditEvents`, `LoginHistories`, `NotificationLogs`,
`WorkflowHistories` and `ElectronicSignatures` grow without limit. Any query
against them must use an indexed predicate and a bounded result. A query on
one of these tables inside a write transaction (a throttle or duplicate
check, for example) must be shown to be indexed with a query plan in the pull
request.

**Rule 10.3 — No database call inside a loop (Phase 3).**
Never `await` an EF Core query inside `foreach`, `for`, or a LINQ `Select`
over in-memory items. Fetch the set once with a grouped or `Contains` query
and look up in memory. The only exception is a per-item regulated command
that must carry its own signature and audit record; say so in a comment.

**Rule 10.4 — Count, sum and group in SQL (Phase 3).**
Never materialize rows to count, sum, average or group them
(`(await query.ToListAsync()).Count`, in-memory `GroupBy` after a fetch).
Several counts over the same filtered set are one grouped query, not several
round trips.

**Rule 10.5 — Order before you limit (Phase 2).**
Every `Take` or `Skip` must follow an explicit `OrderBy` ending in a unique
key. Ranking that decides which rows are returned happens in SQL before the
limit, never in memory after it.

**Rule 10.6 — Text search uses a search index (Phase 2).**
A `Contains` or `LIKE '%term%'` filter on a table expected to exceed a few
thousand rows requires a trigram index on the same expression the query
emits. Do not add a new searched column without its index. Never fetch a
table to filter it in memory.

**Rule 10.7 — Measure, do not assert (Phase 3; helper from Phase 0).**
A pull request that claims a performance change must include before and after
`EXPLAIN (ANALYZE, BUFFERS)` output or query-count figures, taken on the
volume seed database, not an empty one. Dashboards, workspace loads and
search endpoints must have a query-count test; raising its limit requires a
stated reason. A "should be faster" claim with no measurement is not
accepted.

**Rule 10.8 — JSON is for snapshots, not for queryable data (Phase 6 or earlier).**
`jsonb` is permitted only for immutable snapshots and opaque payloads that
are stored and displayed whole (method snapshots, recipe snapshots,
instrument payloads). Anything that is filtered, joined, sorted, constrained
or evaluated against a specification is a typed relational column. Never
move a regulated field into JSON to avoid a migration.

**Rule 10.9 — Keep transactions short and database-only (Phase 5).**
A command saves as few times as correctness allows. No file storage, email,
PDF generation or HTTP call may run between `BeginTransaction` and commit,
with or without a retrying execution strategy. A file and its database row
must not be left inconsistent on failure: write the file first, or clean up
on rollback.

**Rule 10.10 — Every list is bounded (Phase 4).**
No endpoint or repository method returns an unbounded table. Master-data
lists that are genuinely small must state their expected maximum in a
comment; everything else is paged under Rule 5.4. Generic `GetAllAsync`
methods on regulated tables are forbidden. The unbounded-endpoint ratchet in
the architecture tests may only go down.

**Rule 10.11 — Cache only what cannot affect a GMP decision (Phase 6 or earlier).**
Caching is allowed for slow-changing reference data used for display, with
invalidation on write. Never serve from a cache any value used to evaluate a
result, check eligibility, enforce Segregation of Duties, or verify
traceability at the moment of the decision. No cache exists today; adding the
first one requires explicit user approval.

**Rule 10.12 — Plan for growth when adding a table (Phase 6 or earlier).**
A new entity that gains rows per sample, per test, or per user action must
state in its configuration comment its expected yearly row count and its
retention approach. A new append-only table must be added to Rule 10.2.

**Rule 10.13 — Protected-table index and schema changes (Phase 1).**
Index changes on a large protected table are built concurrently, outside the
migration transaction. Any change that rewrites a protected table follows
Rule 7.4 in full. Never change a column type on `AuditLogs` or a signed
record table for a performance reason without explicit user approval.

## 6. Open items

1. Exact audit retention period and archive procedure — QA, through change
   control (Phase 6).
2. Whether `pg_trgm` can be created by the CI role and on the production
   database (before Phase 2).
3. Whether the free-text audit lookup columns need indexes (after Phase 0).
4. Which paid tiers and region to use (before Phase 3).
