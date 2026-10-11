# KPI & Performance page remodel - design

Date: 2026-10-11. Status: draft for review (no code written).
Replaces: the Analyst KPI tab (`AnalystKpiTab.tsx`, `AnalystKpiService.ts`), `KpiService`/`KpiController`, and the per-test-code `WorkloadWeight` table.
Research basis: QC-lab KPI practice (turnaround with explicit clock events, percentiles over means, FDA quality-metrics indicators, data-integrity cautions on analyst metrics, EM trend metrics). Sources are listed in section 12.

## 0. Decisions

Decided by the user:

| # | Decision |
|---|---|
| D1 | Each **workflow type** gets a configured profile; any **test** may override it. |
| D2 | Analyst performance is measured against these configured weights. |

Proposed defaults (change in review if wrong):

| # | Proposal |
|---|---|
| D3 | A profile carries two numbers: **work units** and **target analyst-stage time**. 1 work unit = 1 standard analyst-hour of hands-on work, taken from SOP time studies. |
| D4 | Profiles are **append-only versions**, e-signed, with a reason. A completed test is scored with the version in force when it was approved. No retroactive changes (section 2.3). |
| D5 | A test with no resolvable profile is **unweighted**: excluded from weighted measures and shown as a gap. It is never defaulted to 1.0. |
| D6 | Analyst measures describe **load against capacity and flow**. There is no ranking, no leaderboard, and no default sort by a performance value. |
| D7 | A **KPI registry** on the backend defines every KPI (formula, clock events, population). One endpoint returns all of them under one filter scope. The frontend only renders. |
| D8 | An analyst sees only their own numbers. This is enforced on the server, not only in the UI. |
| D9 | Sections 4.2 and 4.3 define **fixed-formula estimates** (capacity, load and the on-time rule), not time-and-motion measurements. Their stated ceilings apply. |

## 1. Current state (why this exists)

Verified in the code on 2026-10-11:

- Weights are per **test code** (`WorkloadWeight`), seeded for 11 micro codes. Every other test (HPLC, dissolution, titration, ICP, ...) counts as 1.0 silently (`KpiService.cs:255`).
- KPI maths uses the **current** weight for work completed in the past, so editing a weight rewrites old figures. `WorkloadWeightHistory` is written but never read by the maths.
- One flat 7-day analyst SLA (`AnalystAssignmentSla`) applies to every workflow, so a 14-day test is overdue by construction.
- Date range and analyst filters do not reach `/kpi/analysts`, `/kpi/completion-stats`, `/kpi/delay-tracking`. The Tests Assigned/Completed/Pending cards are all-time.
- `UpdateWorkloadWeight` sits under the class-level `Kpi.View` policy, so anyone who can view KPIs can change weights.
- `/kpi/analysts` returns every analyst to every viewer; only the frontend hides other rows for the Analyst role.
- The page invented several figures (fake deltas, 100% defaults when empty, a 24 h default turnaround, review returns equal to doc corrections). The frontend part was fixed on 2026-10-11 and is superseded by this remodel.

## 2. Workflow Profile (slice S1)

### 2.1 Data

One append-only table:

```
WorkflowProfileVersion
  Id
  WorkflowType        enum, required
  TestCode            string, null = the type default; set = override for that test
  WorkUnits           decimal(8,2), null = inherit        (must be > 0 when set)
  TargetAnalystDays   decimal(6,2), null = inherit        (must be > 0 when set)
  EffectiveFrom       date
  Status              Draft | Effective
  ReasonForChange     string, required
  SignatureId         ElectronicSignature, required for Effective
  CreatedByUserId, CreatedAt
```

Retired `WorkflowType` values (`HplcAssay`, `ElementalAssay`, `HplcMultiAnalyte`, `StandardComparison`) are not offered.

### 2.2 Resolution

For a test with code C and workflow type W (read from `TestDefinition.WorkflowType` at calculation time), completed at time t, each field is resolved on its own:

1. the latest `Effective` version with `TestCode = C`, `EffectiveFrom <= t`, and that field non-null;
2. else the latest `Effective` version with `TestCode = null`, `WorkflowType = W`, `EffectiveFrom <= t`, field non-null;
3. else **unresolved**.

A test is *weighted* only if `WorkUnits` resolves; *target-aware* only if `TargetAnalystDays` resolves. Unresolved tests are counted and reported (section 3.3).

### 2.3 Versioning rules

- A new version must have `EffectiveFrom >= today`. Past dates are rejected, so history cannot be rewritten.
- **Baseline exception:** the first `Effective` version of a scope (a type default or an override) may set `EffectiveFrom` to any earlier date, once, with its own reason and signature. This lets the initial calibration cover existing history.
- Versions are never edited or deleted. Correcting a mistake means adding a new version.
- Editing requires the new permission `Kpi.ConfigureProfiles` (SectionHead and SystemAdministrator by default). Each change writes an audit entry and requires an e-signature.

### 2.4 Migration from `WorkloadWeight`

- Create `WorkflowProfileVersion`. For each existing `WorkloadWeight` row, insert an **override** version with `WorkUnits = Weight`, `TargetAnalystDays = null`, `Status = Draft`, reason "Imported from legacy workload weight".
- Old weights were relative (1.0 = a count test), not hours, so imports are **Draft** and have no effect until a Section Head reviews and signs each one.
- Seed one **Draft type default** per live workflow type (no numbers filled in). The configuration page lists every active test definition and every workflow type that has no `Effective` profile, so gaps are visible.
- Keep `WorkloadWeight` and its history table for one release as read-only, then drop in a follow-up migration.
- Down migration restores the legacy tables untouched.

### 2.5 Configuration UI

Replaces `WorkloadWeightsDialog`. A "Workflow Profiles" panel on the KPI page, permission-gated:

- Table by workflow type: effective units, effective target, last change, status. Expand to see per-test overrides.
- "Add version" dialog: units, target, effective date, reason, e-signature.
- A "Needs configuration" filter shows tests and types with no effective profile.
- Each row has a history drawer (all versions, who, when, why).

## 3. KPI registry and dashboard contract (slice S2)

### 3.1 Backend shape (Application layer)

- `IKpi`: `Key`, `Name`, `Unit`, `Direction` (higher/lower is better, or neutral), `Definition` (plain text), `ClockEvents`, `Population`, `ComputeAsync(KpiQuery)`. One small class per KPI, registered in DI. Many implementations, so the interface earns its place.
- `KpiQuery`: `From`, `To`, `SectionIds` (from `IUserSectionScopeService`, as today), optional `Category`, `Location`, `TestCode`, `AnalystId`.
- `KpiResult`: `Value` (nullable), `Numerator`, `Denominator`, `N`, `Prior` (same measure over the equal-length period immediately before, with its own `N`), `Status` = `Ok | NoData | Partial`, `Gaps[]` (for example "12 tests unweighted"), `Series[]` (optional breakdown or trend points).
- `NoData` means the denominator is zero. It is returned as such. It is never a 0% or a 100%.
- Time basis is explicit and documented per KPI (the event date that places a record in the period).

### 3.2 API

- `GET /api/kpi/dashboard?from&to&category&location&testCode` returns all registry results in one response under one filter scope. Optional `analystId` applies only where meaningful.
- `GET /api/kpi/{key}/records?from&to&...&page` returns the underlying samples or tests so any figure can be traced to records.
- `GET/POST /api/kpi/workflow-profiles` (S1) for the configuration panel.
- The 14 existing `/api/kpi/*` endpoints are removed after the frontend moves (S4). `Kpi.View` stays the read policy.

### 3.3 Error and empty states

- A failed KPI returns an error entry for that key only; the page shows an error tile. Nothing is swallowed into zeros (today's `.catch(() => null)`).
- Every tile shows `N`, the prior-period comparison (only when prior `N` is above zero), and any gap chip.
- Rates with fewer than `MinSampleForRate = 5` records show the value muted with "too few records to judge".

## 4. KPI catalog

Eight headline tiles, then four sections. All figures use the section scope of the viewer.

### 4.1 Headline tiles

| # | KPI | Formula and clock | Source |
|---|---|---|---|
| 1 | **Sample TAT** (median and P90, days) | Sample received -> sample approval decision (`Approve`), for samples approved in period | `Sample.ReceivedAt`, `ReviewWorkflowEvent` `ApprovalDecisionMade` with `Decision = Approve` |
| 2 | **On time, all stages** (%) | Samples approved in period that met each stage's target: analyst stage by the sample's longest test target, then Review <= 24 h, Approval <= 24 h | existing stage-window logic, with per-workflow target (4.3) |
| 3 | **Right first time** (% by work units) | Units of tests approved in period with no `TestReturnEvent` / units of tests approved in period | `TestReturnEvent`, approved `ResultRecord` |
| 4 | **Retest rate** (%) | Samples with decision `RetestRetainedSample` in period / samples with any approval decision in period | `ReviewWorkflowEvent.Decision` |
| 5 | **OOS rate** (%) | Samples with decision `OOSInvestigation` in period / samples with any approval decision in period | `ReviewWorkflowEvent.Decision` |
| 6 | **WIP backlog** (tests) | Open tests (`Pending` or `InProgress`, not superseded) now, with aging in section 4.4 | `TestOrder` |
| 7 | **Incubation reads on time** (%) | Incubation windows completed in period within `ExpectedReadingAt` + 4 h grace / windows completed in period | `Incubation` (existing step-violation rule) |
| 8 | **EM excursion rate** (%) | EM results at `AlertLevel` or `ActionLevel` / all EM results with a level, in period; with alert and action rates shown separately | `ResultRecord` level |

Notes:

- Tiles 1-2 replace the current TAT cards and unify them with the stage windows.
- EM tile carries a trend flag using the published adverse-trend thresholds (alert-level rate above 2.5%, action-level rate above 0.15%) as **configurable defaults**, and a small trend series so a slow rise under a fixed limit is visible.
- **Invalidated OOS rate** (FDA indicator of lab performance) is **not computable today**: no invalidation outcome is captured. It is listed as deferred (section 11), not approximated.
- The OOS tile is a lab-quality signal, labelled "not an analyst penalty" as the current page does.

### 4.2 Analyst measures against work units (slice S3)

Per analyst, always measured on **work assigned to them** (analysts do not choose their workload):

| Measure | Definition |
|---|---|
| **Delivered units** | Sum of resolved `WorkUnits` of tests assigned to the analyst and approved in period |
| **Utilisation** | Delivered units / capacity units for the period |
| **Forward load** | Units of the analyst's open tests / capacity units for the next `LoadHorizonDays` (default 5) working days |
| **Weighted on time** | Units of delivered tests whose sample met the analyst-stage target / delivered units |
| **Weighted right first time** | Units of delivered tests with no return / delivered units |

Rules:

- Retests and investigations earn units like any test (the `INVESTIGATION` profile exists). They are never penalties.
- Utilisation above 100% is shown as "check profile" (the effort is probably underestimated), not as praise.
- Unweighted tests are excluded from numerator and denominator and shown as a gap chip ("n tests unweighted").
- Ceiling: a test's effort is its full `WorkUnits` regardless of partial progress, and an on-time outcome is inherited from the sample (there is no per-test "submitted" timestamp). Upgrade path: per-test submission events.

### 4.3 Per-workflow analyst target

The analyst-stage deadline for a sample is `AssignedAt + max(TargetAnalystDays of its assigned tests)`. A test with no resolvable target falls back to the current 7-day constant, and the tile shows a gap chip ("n tests on the default 7-day target"). `AnalystAssignmentSla` stays only as that fallback.

### 4.4 Backlog aging

Open tests are bucketed relative to their own target: within 50% of target, 50-100%, over 100%. Targets differ by workflow, so age in days alone would mislead. Stage shown: Testing, Incubation, In Review, In Approval (sample-level gates from `SectionReviewQueues`).

### 4.5 Capacity (supports 4.2)

```
KpiSettings (per section)
  HoursPerAnalystDay   decimal(4,2)   default 6.0, provisional until confirmed by the Section Head
  WorkingWeekdays      bitmask         default Mon-Fri, edit to match the site week
  LoadHorizonDays      int             default 5

AnalystAvailability
  Id, UserId, FromDate, ToDate, FractionAvailable (0..1), Reason, EnteredByUserId, EnteredAt
```

Capacity units for a period = sum over working days of `HoursPerAnalystDay x availability`. Because 1 unit = 1 hour, capacity and units compare directly. Availability rows (leave, training, secondment) are entered by the Section Head and audit-logged. If a section has no settings row, the page shows a banner "capacity assumes full availability and the default hours" so the assumption is visible.

## 5. Page design (slice S4)

Layout (desktop; tiles wrap on narrow widths):

```
Filters: period | category | location | test | [Definitions] [Workflow Profiles] [Export]

HEADLINE  8 tiles: value, N, vs prior period, gap chip, definition on hover

1 FLOW        stage bars (Testing / Incubation / Review / Approval): median and P90 days;
              backlog aging buckets per stage
2 QUALITY     right-first-time, retest, OOS: trend lines, returns by cause (not by person)
3 MICRO       EM excursion trend with alert/action rates, recovery rate where configured,
              incubation read timeliness
4 WORKLOAD    team table (section head): capacity, delivered units, utilisation, forward load,
              weighted on-time and RFT, sorted alphabetically by default
              analyst view: own numbers only, plus own tests behind them
```

Rules:

- No rank, no "top performer", no colour that treats low output as failure. Forward load above 100% is amber (overload). Everything else is neutral.
- Column sorting on performance values is allowed only on explicit click and never saved as the default.
- Every number opens its record list (`/api/kpi/{key}/records`).
- The Definitions drawer is generated from the registry: definition, clock events, population, source, and the profile versions in force for the chosen period.
- The existing PDF export (`exportKpi.ts`) is rebuilt from the same response so printed figures equal screen figures.

Frontend structure: `KpiTile`, `KpiSection`, `WorkloadTable`, `DefinitionsDrawer`, `WorkflowProfilesPanel`, one `useKpiDashboard` hook. The ~1,000-line `AnalystKpiTab` and the hand-built `AnalystKpiService` go away. The Reports page mounts the new component in the same tab.

## 6. Permissions and audit

- `Kpi.View` (existing): read dashboard and records.
- `Kpi.ViewTeam` (new): see other analysts' measures. Without it the server returns only the caller's own row (D8).
- `Kpi.ConfigureProfiles` (new): create profile versions, edit `KpiSettings` and `AnalystAvailability`.
- New permissions require changes to `PermissionConstants`, the permission seed, `authorization-matrix.txt` and `RolePermissionSeedDataTests`, in the same slice.
- Profile versions, settings and availability changes write audit entries with e-signature where weights are involved.

## 7. Migration and rollout

| Slice | Content | Depends on |
|---|---|---|
| S1 | `WorkflowProfileVersion`, resolution service, configuration API and panel, permission, legacy import (Draft) | none |
| S2 | KPI registry, `/dashboard` and `/records`, headline tiles 1-8, per-workflow target in stage windows, server-side scope | S1 |
| S3 | Work-unit analyst measures, `KpiSettings`, `AnalystAvailability`, `Kpi.ViewTeam` | S1, S2 |
| S4 | New page, Definitions drawer, PDF export, removal of the 14 old endpoints and the old frontend service | S2, S3 |

Each slice builds and passes tests on its own. Until S1 profiles are signed, weighted measures show "unweighted" gaps by design.

## 8. Testing

- Unit: profile resolution (version choice by completion time, override fallback per field, baseline exception once only, past-dated version rejected, retired types not offered).
- Unit: each KPI against fixed fixtures with a fixed `TimeProvider`, including empty, single-record and boundary periods, and the prior-period calculation.
- Unit: capacity maths (working days, partial availability, no settings row).
- Integration (Postgres, as the existing KPI/performance tests): dashboard returns identical figures to a hand-calculated golden dataset; migration Up/Down; unweighted tests excluded and reported.
- Authorization matrix test updated; a test that an Analyst cannot read another analyst's row through any endpoint.
- Frontend (Vitest/RTL): tile states (Ok, NoData, Partial, error), "too few records", no default performance sort, Definitions drawer content.
- After any delegated work: `dotnet build`, `dotnet test`, `npm test`, per project rules.

## 9. Risks

- **Weights need calibration.** Section Heads must supply standard hours; until then weighted measures are blank. Mitigation: gap chips and the "Needs configuration" list.
- **Metric gaming.** Any measure can be gamed. Mitigations: no ranking, minimum record counts, retests earn units, review-return rate shown alongside speed.
- **Effort is an estimate.** Units are a planning figure, not a time-and-motion record (D9).

## 10. Non-goals

- Ranking, scoring or bonus-linked analyst metrics.
- Replacing review/approval workflow logic or changing how results are stored.
- External BI export.

## 11. Deferred

- Invalidated OOS rate: needs an invalidation outcome and reason captured on OOS investigation.
- Per-test submission timestamps (would remove the sample-level on-time inheritance).
- Recovery-rate metric for aseptic areas, if EM data carries what it needs.
- Per-step weights inside a workflow.

## 12. Sources

- FDA quality metrics (lot acceptance, product quality complaint, invalidated OOS): https://gmp-compliance.org/gmp-news/fda-publishes-quality-metrics-guidance
- Turnaround clock definitions and Lean lab metrics: https://bsmlean.com/sites/default/files/docs/2020-01/Efeso-Metrics%20Methodology.pdf, https://www.manufacturingchemist.com/leaner-labs-for-thicker-pipelines-33455
- Percentile and within-cut-off TAT reporting: https://doaj.org/article/833e3f51b2f7448baddfc8a71bf85411
- KPIs first, then layout; limited headline set: https://purl.utwente.nl/essays/96580
- Metrics and data-integrity risk: https://books.rsc.org/books/monograph/780/chapter/426987/Quality-Metrics-for-Data-Integrity, https://www.lachmanconsultants.com/2022/09/rewarding-quality-metrics-is-it-a-good-practice/
- EM excursion rates and trending: https://www.americanpharmaceuticalreview.com/Featured-Articles/347267-Using-Contamination-Rates-For-Environmental-Monitoring-Trending-It-s-Not-Just-For-Clean-Rooms/, https://investigationsquality.com/?p=15541
