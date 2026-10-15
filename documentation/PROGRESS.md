# User Guide Generation — Progress

Prompt: `user_guide_prompt_v2.md` (Master User Guide Generation Agent v2).

| Item | Value |
|---|---|
| Branch | `docs/user-guide` (created from `refactor/consolidation` @ `f773042c`) |
| Base commit described | `f773042c` (refactor/consolidation, PR #86 not yet merged to main) |
| Git available | Yes |
| App run locally | Not yet — screen behaviour currently from code only (lower confidence) |

## Current phase
**Phase 2 complete — application map written. STOPPED, awaiting user approval** (module list, screen inventory, coverage matrix, gaps). Do not start Phase 3 until approved.

## Phase log
- 2026-10-15 — Phase 0: branch created, `documentation/` + `knowledge/` + `screenshots/` folders created.
- 2026-10-15 — Phase 1: raw inventories written to `knowledge/_raw/` (frontend core screens, testing workspace, configuration / physchem / Document Control screens, backend endpoints, backend enums, backend permissions (unverified), verified roles / permissions, specification sources).
- 2026-10-15 — Phase 2: application map, coverage matrix and gaps file written (below).

## Files created (Phase 2)
- `01_APPLICATION_MAP.md` — overview; 66 modules with status; navigation as built by `menuConfig.ts`; 104 screens; roles (4 default) and 50 permission codes (40 enforced, 10 NOT-ENFORCED); entities; 71 workflow names; integrations; reports; configuration areas; audit / traceability; legacy routes; Step B candidate checklist.
- `COVERAGE_MATRIX.md` — one row per module (66), plus the Step B candidates that were NOT FOUND.
- `DOCUMENTATION_GAPS.md` — 55 gaps (9 labelled DISCREPANCY).
- Phase 1 raw files under `knowledge/_raw/` (earlier step).

## Module status counts (from the application map)
COVERED 18, PARTIAL 38, REQUIRES CONFIRMATION 1, NOT-EXPOSED-IN-UI 8, LEGACY/RETIRED 1 (total 66).

## Modules completed
None written for the guide yet (the guide itself starts in Phase 9).

## Modules remaining
All 66 (see `COVERAGE_MATRIX.md`).

## Open questions
See `DOCUMENTATION_GAPS.md` (GAP-001 to GAP-055). Highest priority for the user's decision:
- GAP-001 no URS / FRS for the microbiology core (INTENDED workflow rests on BusinessRules.md and WorkflowDiagrams.md).
- GAP-017 to GAP-022 and GAP-045: discrepancies between specifications and code.
- GAP-014 Final Release is not found.
- GAP-015 / GAP-016: default role grants may differ in production (needs a read-only check by an authorised person).
- GAP-040: the application has not been run; permission is needed for a local test instance with fake data.

## Next step
1. User reviews and approves (or amends) the module list, the PARTIAL / REQUIRES CONFIRMATION modules and the gaps.
2. After approval, start **Phase 3 (knowledge base)** in `knowledge/`: `modules.md`, `screens.md`, `roles.md`, `workflows.md`, `business-rules.md`, `validations.md`, `statuses.md`, `terminology.md`, `reports.md`, `audit-trail.md`, `traceability.md`, one module at a time. First complete the work lists in GAP-043 and GAP-044 (labels and rules not yet read).
3. Then Phase 4 (workflow reconstruction) and STOP again for approval.
