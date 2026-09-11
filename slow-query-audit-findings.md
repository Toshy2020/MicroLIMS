# Slow Query Audit — Findings

## Summary

- **46 static backend findings**: the original 34 API findings plus 12 Application-layer report, aggregation, review, and projection candidates captured in the Phase 0 scope expansion below.
- **7 live database observations** and **0 correlated pairs**: Neon production (`production` / `neondb`) was queried read-only on 2026-09-09. `pg_stat_statements` contains platform/metadata and audit-tool statements, but no identifiable MicroLIMS business query to cost or plan.
- The `pg_stat_statements` snapshot is sparse for application traffic. It may have been reset by a Neon compute recycle/scale event, so the absence of a slow MicroLIMS statement is explicitly **not** evidence of healthy application-query performance.
- EF command logging and the four requested runtime flow measurements were not performed. Enabling `LogTo` and sensitive data logging requires a non-production runtime/configuration change, which remains outside this read-only recon.
- Findings #5 and #7–#11 were removed by the approved Phase 1 projection cleanup. The `AsNoTracking` aspect of #13 and #15–#28 was also addressed; their unbounded-collection aspect remains part of the historical baseline. The table is retained as an audit trail rather than represented as a current defect list.

## Backend findings

| # | File:Line | Pattern | Risk | Notes |
|---:|---|---|---|---|
| 1 | `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:436` | N+1 | One appearance lookup and one media-lot query run for every configured step medium. | `GetPermittedConfirmatoryMedia` performs 2 × medium-count round trips; the list has no aggregate fetch. |
| 2 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:973` | N+1 | One `Organisms.AnyAsync` call is made per submitted media-configuration challenge. | Request size controls the round-trip count. |
| 3 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:1083` | N+1 | One `Organisms.AnyAsync` call is made per updated media-configuration challenge. | Request size controls the round-trip count. |
| 4 | `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:145` | Multiple independent reads / tracking | The testing-workspace current-step action makes a workflow-engine read followed by a tracked, five-navigation test-order graph read. | Additional conditional specification/configuration queries follow at lines 156–162. Query count is data-dependent. |
| 5 | `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:254` | Redundant `Include` before projection | `Media` and `IncubatorEquipment` are included and then projected to scalar fields. | EF Core projections normally generate required joins without materializing the included entity graph; this is a query-shape candidate, not proof of excess rows. |
| 6 | `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:293` | Over-fetch / tracked graph | A shared workflow-step result loads incubation, media, and equipment entities where only a few scalar fields are consumed. | This is on the sample-workspace critical path. |
| 7 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:236` | Redundant `Include` before projection | Department/room endpoint includes `Rooms` then projects scalars. | Projection makes the include unnecessary for relationship fix-up; no page cap. |
| 8 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:290` | Redundant `Include` before projection | Room/department endpoint includes `Department` then projects scalars. | No page cap. |
| 9 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:351` | Redundant `Include` before projection | Machine/part endpoint includes `Parts` then projects scalars. | No page cap. |
| 10 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:881` | Redundant `Include` before nested projection | Media configurations include challenge/organism graphs before a nested DTO projection. | No page cap; nested child collections can grow response size with configuration volume. |
| 11 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:1336` | Redundant `Include` before nested projection | Workflow-step endpoint includes five navigation paths and then projects fields. | No page cap; projection should be inspected against generated SQL. |
| 12 | `backend/MicroLIMS.API/Controllers/AuditController.cs:63` | Tracked read / caller-controlled limit | Login history begins as a tracking query. | `Take(take)` exists, but `take` has no server-side maximum. |
| 13 | `backend/MicroLIMS.API/Controllers/RoleController.cs:34` | Tracked unbounded entity read | Returns all `Role` entities with `ToListAsync`. | No `AsNoTracking`, projection, or page cap. |
| 14 | `backend/MicroLIMS.API/Controllers/SignaturesController.cs:29` | Unbounded collection | Returns every signature for an entity. | Scalar projection avoids entity tracking, but no page cap exists for a long-lived audit trail. |
| 15 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:78` | Tracked unbounded entity read | Returns all water-sampling points. | No `AsNoTracking`, projection, or page cap. |
| 16 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:133` | Unbounded collection | Returns all water departments and nested sampling points. | Projection avoids tracking, but response cardinality is unbounded. |
| 17 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:186` | Tracked unbounded entity read | Returns all sampling configurations for a point. | No `AsNoTracking`, projection, or page cap. |
| 18 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:445` | Tracked unbounded entity read | Returns all specifications for an item. | No `AsNoTracking`, projection, or page cap. |
| 19 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:494` | Tracked unbounded entity read | Returns all active causes of testing. | No `AsNoTracking`, projection, or page cap. |
| 20 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:547` | Tracked unbounded entity read | Returns all active samplers. | No `AsNoTracking`, projection, or page cap. |
| 21 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:593` | Tracked unbounded entity read | Returns all active production stages. | No `AsNoTracking`, projection, or page cap. |
| 22 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:637` | Tracked unbounded entity read | Returns all diluent types. | No `AsNoTracking`, projection, or page cap. |
| 23 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:651` | Tracked unbounded entity read | Returns all active neutralizers. | No `AsNoTracking`, projection, or page cap. |
| 24 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:667` | Tracked unbounded entity read | Returns all equipment, optionally filtered only by type. | No `AsNoTracking`, projection, or page cap. |
| 25 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:779` | Tracked unbounded entity read | Returns all room-test configurations for a room. | No `AsNoTracking`, projection, or page cap. |
| 26 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:828` | Tracked unbounded entity read | Returns all machine-part configurations for a machine part. | No `AsNoTracking`, projection, or page cap. |
| 27 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:1198` | Tracked unbounded entity read | Returns all organisms. | No `AsNoTracking`, projection, or page cap. |
| 28 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:1266` | Tracked unbounded entity read | Returns all test definitions. | No `AsNoTracking`, projection, or page cap. |
| 29 | `backend/MicroLIMS.API/Controllers/MasterDataController.cs:1371` | Unbounded nested collection | Returns all workflow steps and nested step media, stages, and phenotypic tests for a definition. | Projection avoids tracking but collection size is uncapped. |
| 30 | `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:272` | Unbounded collection | Returns every incubation/previous step for a test order. | Projection avoids entity tracking; retention/history determines response size. |
| 31 | `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:429` | Tracked graph / over-fetch candidate | Permitted-media action loads step media/material, target organism, and test definition as tracked entities. | The subsequent loop adds the N+1 finding in #1. |
| 32 | `backend/MicroLIMS.API/BackgroundServices/DatabaseHealthMonitorWorker.cs:160` | Raw SQL | Fixed SQL reads `pg_stat_statements`. | The SQL is constant rather than concatenated and has no caller input; it is a privileged monitoring dependency, not an injection finding. |
| 33 | `backend/MicroLIMS.API/BackgroundServices/DatabaseHealthMonitorWorker.cs:325` | Raw SQL | Fixed SQL reads `pg_stat_activity` and invokes `pg_blocking_pids`. | The SQL is constant rather than concatenated and has no caller input; it depends on PostgreSQL statistics visibility. |
| 34 | `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:145-388` | Repeated related reads per action | Current-step loading independently reads order/sample, optional spec/config, workflow-step result map, incubations, shared step, optional user, template step, and return info. | These are not duplicate query shapes by static inspection, but form a high query-count candidate requiring runtime measurement. |

### Static scan negatives

- No `FromSqlRaw`, `ExecuteSqlRaw`, or string-concatenated EF SQL was found under `backend/MicroLIMS.API`.
- No `AsEnumerable`/explicit client-side EF evaluation was found under `backend/MicroLIMS.API`.
- No clear instance of the same `IQueryable<T>` being materialized twice was found in `backend/MicroLIMS.API`.
- Absence of `AsNoTracking` is reported above only where the query materializes entities directly. Scalar/anonymous projections are normally non-tracking in EF Core and are not automatically a missing-tracking defect.

### Phase 0 scope expansion — `MicroLIMS.Application`

| # | File:Line | Pattern | Risk | Notes |
|---:|---|---|---|---|
| 35 | `backend/MicroLIMS.Application/Services/ResultProjectionService.cs:374` | N+1 / per-row save | Backfill probes, upserts, and saves once per count-test reading. | Round trips grow linearly with source rows. |
| 36 | `backend/MicroLIMS.Application/Services/ResultProjectionService.cs:399` | N+1 / per-row save | Backfill probes, upserts, and saves once per pathogen test order. | Round trips grow linearly with source rows. |
| 37 | `backend/MicroLIMS.Application/Services/ResultProjectionService.cs:417` | N+1 / per-row save | Backfill probes, upserts, and saves once per sample location. | Round trips grow linearly with source rows. |
| 38 | `backend/MicroLIMS.Application/Services/ReviewService.cs:71` | N+1 / per-item workflow | Quick review invokes the review workflow once per selected order. | May be intentional for individual electronic-signature/audit semantics; unmeasured. |
| 39 | `backend/MicroLIMS.Application/Services/ReportingQueryService.cs:118` | Multiple enumeration | A paginated result search performs `CountAsync` then a paged `ToListAsync` on the filtered query. | Expected pagination pattern; filter/index selectivity is unmeasured. |
| 40 | `backend/MicroLIMS.Application/Services/ReportingQueryService.cs:129` | Unbounded distinct collections | Filter options materialize distinct categories, test codes, subject names, and units. | Subject-name cardinality can grow with operational data. |
| 41 | `backend/MicroLIMS.Application/Services/ReportingQueryService.cs:155` | Multiple enumeration | CSV export counts, then materializes the full filtered set. | A caller-provided export cap is enforced before materialization. |
| 42 | `backend/MicroLIMS.Application/Services/ReportingQueryService.cs:239` | Unbounded client-side aggregation | Trend analysis materializes matching results before calculating statistics in process memory. | Date filters are optional. |
| 43 | `backend/MicroLIMS.Application/Services/ReportingQueryService.cs:280` | Repeated aggregate queries | Overview executes six counts plus grouped/recent-record queries over the same filtered base. | Fixed high query count per dashboard request; cost unmeasured. |
| 44 | `backend/MicroLIMS.Application/Services/ReportingQueryService.cs:361` | Unbounded collection | Qualitative-event reporting materializes every matching detected record. | No-filter use spans qualifying history. |
| 45 | `backend/MicroLIMS.Application/Services/ReportingQueryService.cs:435` | Unbounded client-side aggregation | Compare-by-subject materializes all matching records, then groups/calculates in memory. | Date filters are optional. |
| 46 | `backend/MicroLIMS.Application/Services/MediaGptReportService.cs:20`; `ReferenceStrainReportService.cs:20` | Read-only tracked graphs | Both paged searches count, load tracked entity graphs, then perform batched dependent/user lookups. | Not N+1; plan and payload cost unmeasured. |

## Runtime flow measurements

| Flow | Query count | Total DB time | Evidence |
|---|---:|---:|---|
| Pathogen confirmation workflow (Panel A → B → C) | Not observed | Not observed | No configured non-production database/runtime was available. |
| Count-test multi-plate result entry | Not observed | Not observed | No configured non-production database/runtime was available. |
| Reports module (KPI/Performance, Trend Analysis) | Not observed | Not observed | API delegates these reads to `MicroLIMS.Application` services, outside the API-only static scope. |
| Sample Testing Workspace load | Not observed | Not observed | Static candidate #34; no runtime instrumentation was enabled. |

## Database findings

| # | Query (truncated) | Table(s) | Mean time | Calls | Seq scans? |
|---:|---|---|---:|---:|---|
| 1 | `SELECT state, to_char(state_change, ...) FROM pg_stat_activity ...` | `pg_stat_activity` | 0.137 ms | 961 | No user-table scan indicated |
| 2 | `SELECT count(*) FROM pg_stat_replication ...` | `pg_stat_replication` | 0.117 ms | 961 | No |
| 3 | `SELECT count(*) FROM pg_stat_activity ...` | `pg_stat_activity` | 0.094 ms | 961 | No user-table scan indicated |
| 4 | PostgreSQL catalog attribute introspection | `pg_catalog` | 49.728 ms | 3 | Catalog access only |
| 5 | `pg_stat_user_tables` sequential-scan counters | `NotificationLogs`, `TestOrders`, `SampleLocations`, `Incubations`, `AuditLogs` | — | — | Yes; see observations below |
| 6 | `pg_stat_user_indexes WHERE idx_scan = 0` | 250 indexes, including primary keys | — | 0 scans in snapshot | Not applicable |
| 7 | `pg_stat_user_tables` dead-tuples/autovacuum/analyze data | `TestOrders`, `Incubations`, `WorkflowHistories`, `WorkflowStepResults`, `ResultRecords` | — | — | Not applicable |

`pg_stat_statements` top 25 was captured. Entries #1–#4 are platform/administrative metadata statements, not identifiable MicroLIMS request SQL. The monitor-related statements run frequently but are sub-millisecond on average; the catalog lookup is infrequent. No application-workload query reached the top 25.

The largest observed sequential-scan counters were `NotificationLogs` (14,761 scans / 9,641,821 tuples read / 653 average rows per scan), `TestOrders` (50,241 / 5,724,776 / 113), `SampleLocations` (17,205 / 3,308,605 / 192), `Incubations` (15,764 / 2,727,885 / 173), and `AuditLogs` (329 / 1,231,760 / 3,743). Current estimated row counts remain modest except `AuditLogs` (8,678) and `NotificationLogs` (1,772), so these counters alone do not establish an index deficiency.

All 250 zero-scan indexes are snapshot candidates only: the query includes primary/unique indexes and the observation window is unknown. They are not evidence that an index is removable. The largest dead-tuple estimates were `TestOrders` 104 (325 live), `Incubations` 85 (547 live), `WorkflowHistories` 72 (526 live), `WorkflowStepResults` 57 (260 live), and `ResultRecords` 49 (158 live); many tables have no recorded `last_analyze` value.

`EXPLAIN (ANALYZE, BUFFERS)` was not run. The top entries are platform/metadata statements or normalized statements whose bound values are unavailable, and no expensive MicroLIMS business statement was identified to execute safely. This is an evidence limitation, not a clean plan result.

## Correlated findings

No correlated pairs. The live top statements do not match an executed MicroLIMS request query. A targeted `pg_stat_statements` search also did not find an executed instance of the fixed monitor SQL in findings #32–#33; it found only Neon inspection/platform statements.

## Uncorrelated DB findings

1. The three high-call sub-millisecond metadata queries (#1–#3) have no obvious MicroLIMS source and appear to be Neon/platform monitoring.
2. The catalog introspection query (#4) has no identified MicroLIMS source; it is consistent with schema/browser tooling.
3. The sequential-scan, zero-scan-index, and dead-tuple observations (#5–#7) identify tables/indexes rather than one generated query shape, so no backend source can be assigned from the current statistics snapshot.
