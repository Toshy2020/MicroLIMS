# HPLC Chain S7 — HPLC Workspace Frontend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The analyst's HPLC Workspace screens on top of the S6 API: instrument overview → instrument workspace → start run (method, column, mobile phases) → SST and standard (with report upload) → assign samples → replicate entry and results → report upload → Send for Review; run history; the reviewer sees reports beside the entered values.

**Architecture:** One feature module `frontend/src/modules/hplcWorkspace/` built by agy (ui-ux-pro-max skill). Container pages load data through one typed service and render presentational components. Every gate, status, calculation and reason comes from the API (`CanConfirmSst`, `CanAssignSamples`, `CanSubmit` + reasons, preview results); the UI only renders them. Two agy jobs run in parallel on disjoint folders: S7a (`overview/`, `run/`, `sst/`) and S7b (`samples/`, `entry/`, `evidence/`, `history/`, `review/`); the shared `services/`, `types.ts` and routes are written first by S7a and only read by S7b.

**Tech Stack:** React + TS + MUI (existing design system).

**Spec:** `docs/superpowers/specs/2026-09-29-hplc-chain-design.md` §5; source FS "HPLC Workspace Frontend v1.1" (routes §3, components §4, gating §8 of the URS). API: `backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs` (S6 Task B4) and the DTOs in `backend/MicroLIMS.Application` it returns.

## Global Constraints
- Files ≤ ~350 lines; split pages, sections, dialogs, hooks.
- UI never decides scientific or workflow acceptance: disabled actions show the backend's reason; preview results are labelled "Preview — not the official result" until submitted.
- Status always text + icon (`HplcStatusBadge`); no `alert/confirm`; theme tokens only; shared dialogs (`ReasonDialog`, the existing e-signature dialog).
- Evidence: upload shows parent context; never replaces — "Replace" opens supersede with a reason; old versions stay listed.
- Preserve entered values on validation errors; disable submit buttons while a request is in flight (duplicate-submit guard).
- Routes (FS §3): `/hplc-workspace`, `/hplc-workspace/:instrumentId`, `/hplc-workspace/:instrumentId/new-run`, `/hplc-workspace/:instrumentId/run/:runId`, `/hplc-workspace/:instrumentId/run/:runId/sst`, `/hplc-workspace/:instrumentId/run/:runId/samples`, `/hplc-workspace/:instrumentId/run/:runId/sample/:runSampleId`, `/hplc-workspace/:instrumentId/history`. Menu "HPLC Workspace" in the Physicochemical lab area, permission `Hplc.Operate` for actions (the overview is visible to authenticated users of the lab).

## Review Focus
- Direct navigation to the samples route while SST is Pending → page shows the lock reason; assign button disabled; no request sent.
- A mobile-phase preparation that expired since run start → SST confirm shows the backend's refusal text, keeps entered values.
- Replicate table: removing a row then saving must not leave stale responses for other analytes.
- Upload failure → evidence not shown as attached; retry keeps context.
- Returned-to-analyst sample → entry editable again, return reason visible.

---

### Task S7a (agy job 1): shell, overview, run start, SST
Files: `hplcWorkspace/types.ts`, `services/HplcWorkspaceService.ts` (all endpoints, incl. multipart evidence), `components/HplcStatusBadge.tsx`, `components/WorkflowStepRail.tsx` (completed/current/locked steps with lock reasons), `overview/HplcWorkspacePage.tsx` + `overview/HplcInstrumentCard.tsx` (name, code, Available/Running/Unavailable with reason, active run summary), `run/HplcInstrumentWorkspace.tsx` (header: instrument, method, run code, analyst, start; tabs Overview · SST · Samples · Evidence · History), `run/StartHplcRunWizard.tsx` (steps: Method — active methods with eligible-test counts; Column — compatible active columns; Mobile phases — per method channel, available preparations (`GET /api/solution-preparations/available?solutionMasterId&hplcMethodId`) with code, prepared at/by, expiry, and "Prepare new" linking to `/preparation/new?type=MobilePhase&solutionId=&methodId=` then refreshing on return), `run/MethodReadOnlyPanel.tsx` (snapshot: column, elution/gradient, detection, injection, solutions, analytes with Th.Wt read-only), `sst/SystemSuitabilityPanel.tsx` + `sst/StandardEntryForm.tsx` (per analyte: standard lot picker showing purity + MC from the lot, Th.Wt.std read-only, actual weight, injection responses × n, reported values for the criteria the method sets) + `sst/SstValuesTable.tsx` (criteria vs entered) + `sst/SstStatusCard.tsx` (Pending/Passed/Failed with reasons, "Sample assignment is locked until system suitability passes.") + standard-report upload via S7b's `evidence/ReportUploadPanel.tsx` (S7a imports it — agree the props: `{ runId, runSampleId?: number, context: "Sst"|"Sample"|"Run", kind, onChanged }`; if S7b has not committed it yet, S7a creates it and S7b extends it). Abandon run with reason. Routes + menu.
Verify build + eslint. **Commit** `feat(ui): hplc workspace overview, run start and system suitability`

### Task S7b (agy job 2, after S7a's service/types commit exists)
Files: `samples/SampleAssignmentPanel.tsx` + `samples/EligibleSampleTable.tsx` (search, multi-select, assign; remove with reason), `entry/HplcSampleEntryPage.tsx` (identity, linked SST code/status, basis badge), `entry/ReplicateEntryTable.tsx` (rows: replicate no., actual weight mg, response per analyte; add/remove rows until submitted; required count shown), `entry/CalculationSummaryCard.tsx` (per-replicate assay %, reported result with `ResultBasisBadge` Mean/Individual, amount per unit + label claim + unit when present, separate status per quantity + overall), `entry/SendForReviewDialog.tsx` (completeness list from the API, e-signature), `evidence/ReportUploadPanel.tsx` + `evidence/ChromatogramEvidencePanel.tsx` (list: name, kind, context, uploaded by/at, view in a new tab via the file endpoint, supersede with reason), `history/HplcRunHistoryTable.tsx` (completed/abandoned runs: code, method, dates, analyst, status), `review/HplcReviewPanel.tsx` — mounted in the existing FP result card/review dialog for `HplcMethodAssay` test orders (find where `StandardComparison` results are rendered for reviewers): reports list beside entered weights/responses and the method snapshot parameters. Run "Complete" action on the workspace header.
Verify build + eslint. **Commit** `feat(ui): hplc workspace samples, entry, evidence, history and review view`

### Task S7c: Verify
Opus runs build + eslint, reviews both diffs (file sizes, no business rules in components), then S8 browser end-to-end.
