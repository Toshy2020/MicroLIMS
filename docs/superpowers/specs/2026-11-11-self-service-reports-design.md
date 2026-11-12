# F5 — Self-Service Reports: Design Specification

**Roadmap package:** F5 (`improvement-roadmap.md`)
**Status:** Draft for user review
**Date:** 2026-11-11
**Related:** C3 (saved views and sharing), C5 (notifications and email), D1/D2 (quality records), B4 (QA role).

## 1. Purpose

Let users build their own reports over a fixed set of safe datasets — pick columns, filters, grouping and a chart — save and share them, schedule them, and export them, with every export recorded.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Datasets | Results; samples and turnaround; media, equipment and stock; quality records |
| 2 | Builders | Everyone who can read the data builds personal reports; sharing with a laboratory needs a Section Head, QC Manager or QA |
| 3 | Scheduled email | A link only; the data is seen after signing in, with the reader's own access |
| 4 | Exports | CSV, Excel and PDF |

## 3. Out of scope

- Free SQL or arbitrary joins. Only the datasets defined here, with their fields.
- Changing any calculation: turnaround times and KPI values use the existing KPI definitions; results use the stored reported values and limits.
- Dashboards with several reports on one page (a later step).
- Reports on audit trail entries (the audit search screen remains the tool).

## 4. Current state (from code, not run)

- Reports have fixed tabs: overview, a result search with fixed filters, media growth promotion, reference strains, trends and control charts, KPIs. The result search reads a flat result table kept up to date when results are recorded and approved.
- Server CSV exports (results, media, strains) are capped at 10,000 rows and recorded in the export log. **Some exports are produced in the browser and are not recorded:** the result table's own CSV, PDF prints of reports, and the KPI PDF.
- No saved, shared or scheduled reports. Excel files can be produced on the server (a library is already used for the master-data workbook); server PDFs are produced for summaries and archives.
- Eleven KPIs with definitions exist.

## 5. Design

### 5.1 Datasets

Each dataset is a defined list of fields with a name, type and meaning, read only from data the user may already see (laboratory scope, and the rules of each module).

| Dataset | Rows | Example fields |
|---|---|---|
| Results | One per reported result | Sample, category, item, batch, test, parameter, value as reported, unit, limits, level, analyst, entered, reviewed, approved, decision, laboratory |
| Samples and turnaround | One per sample and laboratory section | Sample, category, item, batch, cause, received, testing started, reviewed, approved, decision, turnaround per stage (KPI definitions), on time |
| Media | One per prepared lot | Medium, lot, prepared, expiry, GPT outcome, sterility outcome, released |
| Equipment | One per instrument | Code, type, laboratory, status, last and next calibration, overdue |
| Stock | One per lot | Material, lot, received, expiry, quantity remaining, status |
| Quality records | One per record | Type, number, status, classification, laboratory, raised, due, closed, cause category, source (from D1 and D2; available once those packages exist) |

- Invalidated, superseded and voided values are excluded unless the user ticks "include history", and are then labelled.
- A dataset can be added later by defining its fields; nothing else changes.

### 5.2 Builder

- Choose a dataset, then columns (order and width), filters (any field; operators fit the type; relative dates like "last 30 days"), grouping (up to two levels) with counts, sums, averages, minimum and maximum where meaningful, sorting, and an optional chart (bar, line or pie) from a grouped column.
- Averages and sums are offered only for numeric fields marked summable; a result value is never averaged across different tests or units (the builder refuses grouping that would mix units).
- The preview shows the first 100 rows; the report runs fully on demand.
- Every date shows its time zone, as elsewhere.

### 5.3 Saving and sharing

- Reports are personal by default. A Section Head, QC Manager or QA can share a report with one or more laboratories or with everyone; shared reports are read-only for others, who can copy them.
- A shared report always runs with the **reader's** access, so a reader never sees rows they could not see otherwise.
- Changes to a shared report are recorded (who, when, what changed).

### 5.4 Scheduling

- The owner can schedule a report (daily, weekly on a weekday, monthly on a day, at a time in laboratory time) for themselves and, if they may share it, for chosen users or laboratories.
- When it runs, the result is stored as a **snapshot** (rows and the filters used, time of run) for 90 days, and each recipient gets a notification and an email **with a link only**. Opening the link shows the snapshot filtered to the recipient's own access.
- A failing schedule notifies the owner; three failures in a row pause it.

### 5.5 Exports

- CSV, Excel and PDF are produced on the **server**. Each export file starts with a header stating the report name, dataset, filters, user, time and time zone; the Excel file has typed columns and a second sheet with the same header; the PDF has the table and the chart.
- Every export is recorded in the export log (user, time, report, filters, row count, format). The browser-produced exports that are not recorded today (result table CSV, report prints, KPI PDF) move to the server path so they are recorded too.
- Limit: 50,000 rows per export (setting); above it the user is asked to narrow the filters.

### 5.6 Permissions

| Permission | Holders | Allows |
|---|---|---|
| `Reports.Build` | All laboratory roles, QA | Build, save, schedule for self, export |
| `Reports.Share` | Section Head, QC Manager, QA | Share and schedule for others |

Export permission follows the existing data-export permission where one applies.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Grouping would mix units or tests | Refused with the reason |
| Too many rows | "Narrow the filters to 50,000 rows or fewer." |
| Field no longer exists in a dataset | The saved report shows it as removed and runs without it |
| Recipient lost access | Snapshot shows only rows they may see (possibly none) |
| Schedule fails | Owner notified; paused after three failures |

## 7. Testing

- **Backend `[PostgresFact]`:** each dataset's fields and scope (another laboratory's rows never returned); history exclusion; filters per type and relative dates; grouping and aggregates; unit-mixing refusal; share rules; shared report runs with reader access; schedules create snapshots and link-only notifications; snapshot filtered per recipient; pause after failures; exports in three formats with header and correct row count; every export logged; row limit.
- **Frontend (`userEvent`):** builder steps; preview; chart data; save, copy, share; schedule editor; export buttons; former browser exports now call the server.
- **Manual:** one report per dataset built by an analyst and shared by a Section Head.

## 8. Delivery stages

1. Dataset framework and Results dataset; builder and preview.
2. Server exports (CSV, Excel, PDF) with logging; move browser exports to the server.
3. Saving and sharing.
4. Samples and turnaround; media, equipment and stock datasets.
5. Scheduling, snapshots and link-only notifications.
6. Quality records dataset (after D1 and D2).
7. Documents.
