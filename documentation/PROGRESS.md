# User Guide Generation — Progress

Prompt: `user_guide_prompt_v2.md` (Master User Guide Generation Agent v2).

| Item | Value |
|---|---|
| Branch | `docs/user-guide` (created from `refactor/consolidation` @ `f773042c`) |
| Base commit described | `f773042c` (refactor/consolidation, PR #86 not yet merged to main) |
| Git available | Yes |
| App run locally | Not yet — screen behaviour currently from code only (lower confidence) |

## Current phase
**Phase 4 complete — knowledge base and workflows written. STOPPED, awaiting user approval.** Do not start Phase 5 until approved.

Decisions in force: Phase 2 approved by the user 2026-10-15. The app was NOT run (user did not answer; default = code only). Memory notes of earlier user decisions are NOT used as INTENDED sources (prompt does not list them).

## Phase log
- 2026-10-15 — Phase 0: branch created, `documentation/` + `knowledge/` + `screenshots/` folders created.
- 2026-10-15 — Phase 1: raw inventories written to `knowledge/_raw/` (frontend core screens, testing workspace, configuration / physchem / Document Control screens, backend endpoints, backend enums, backend permissions (unverified), verified roles / permissions, specification sources).
- 2026-10-15 — Phase 2: application map, coverage matrix and gaps file written (below). Approved by the user.
- 2026-10-15/16 — Phases 3–4: 8 module clusters (`knowledge/_clusters/C1..C8`, see `CLUSTERS.md`, `BRIEF.md`). Each: extraction (Haiku; C7 and C8 by Gemini via agy at the user's request) → Sonnet re-verification of every role / permission / approval / status / calculation claim + Phase 4 workflows. Verifiers corrected ~10–45 claims per cluster; Gemini invented validation messages and audit codes in C7/C8, all removed. Merged by `E:/MicroLIMS/rls-tmp/merge_clusters.py` into the 11 thematic files; cluster files remain the source to edit, then re-run the script.

## Files created (Phase 2)
- `01_APPLICATION_MAP.md` — overview; 66 modules with status; navigation as built by `menuConfig.ts`; 104 screens; roles (4 default) and 50 permission codes (40 enforced, 10 NOT-ENFORCED); entities; 71 workflow names; integrations; reports; configuration areas; audit / traceability; legacy routes; Step B candidate checklist.
- `COVERAGE_MATRIX.md` — one row per module (66), plus the Step B candidates that were NOT FOUND.
- `DOCUMENTATION_GAPS.md` — 55 gaps (9 labelled DISCREPANCY).
- Phase 1 raw files under `knowledge/_raw/` (earlier step).

## Module status counts (from the application map)
COVERED 18, PARTIAL 38, REQUIRES CONFIRMATION 1, NOT-EXPOSED-IN-UI 8, LEGACY/RETIRED 1 (total 66).

## Files created (Phases 3–4)
- `knowledge/` — `modules.md`, `screens.md`, `roles.md`, `workflows.md` (81 workflow sections), `business-rules.md` (566 BR ids), `validations.md`, `statuses.md`, `terminology.md` (with standard glossary preamble), `reports.md`, `audit-trail.md`, `traceability.md`.
- `knowledge/_clusters/` — per-cluster source files (edit these, then re-merge), `_merged-gaps.md`.
- `DOCUMENTATION_GAPS.md` — 142 Phase 3–4 gaps appended (197 total).
- `COVERAGE_MATRIX.md` — Phase 4 workflow ids appended.

## Modules completed
Knowledge base + workflows: all 66 modules (clusters C1–C8). User Guide text: none yet (Phase 9).

## Modules remaining
Phases 5–15 for all 66 modules.

## Open questions
See `DOCUMENTATION_GAPS.md` (GAP-001..055, then cluster ranges GAP-100..821). Highest priority for the user's decision:
- GAP-001 no URS / FRS for the microbiology core (INTENDED workflow rests on BusinessRules.md and WorkflowDiagrams.md).
- GAP-017 to GAP-022 and GAP-045: discrepancies between specifications and code.
- GAP-014 Final Release is not found.
- GAP-015 / GAP-016: default role grants may differ in production (needs a read-only check by an authorised person).
- GAP-040: the application has not been run; permission is needed for a local test instance with fake data.

## Next step
1. User reviews `knowledge/workflows.md` and the new gaps, and approves Phase 4.
2. Then Phase 5 (domain review, per module) and Phase 6 (GMP / data-integrity review), then Phases 7–9 (roles, structure, writing the guide one module at a time).
