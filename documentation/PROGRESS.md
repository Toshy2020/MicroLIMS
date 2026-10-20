# User Guide Generation — Progress

Prompt: `user_guide_prompt_v2.md` (Master User Guide Generation Agent v2).

| Item | Value |
|---|---|
| Branch | `docs/user-guide` (created from `refactor/consolidation` @ `f773042c`) |
| Base commit described | `f773042c` (refactor/consolidation, PR #86 not yet merged to main) |
| Git available | Yes |
| App run locally | Not yet — screen behaviour currently from code only (lower confidence) |

## Current phase
**Phase 12 complete — reconciliation written. STOPPED, awaiting user approval** before Phase 13 (apply corrections).

Decisions in force: Phases 2, 4 and 5–9 approved by the user. App NOT run (code only). Memory notes are not INTENDED sources. Model routing: agy (Gemini) writes, Sonnet reviews; Phases 11–12 Opus as the prompt requires.

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

- 2026-10-16/20 — Phases 5–9: Sonnet domain + GMP reviews per cluster (`knowledge/_clusters/R1..R8-review.md`, ~180 guide callouts); Phase 7 `knowledge/role-guide.md` (agy draft, Sonnet-reviewed, 11 fixes); Phase 8 `USER_GUIDE_STRUCTURE.md` (44 sections, 3 new); Phase 9: 44 section files in `guide/` drafted by agy per `guide/WRITING_BRIEF.md`, every one reviewed by Sonnet per `guide/REVIEW_GUIDE_BRIEF.md` (review log at the end of each file; hundreds of corrections, many invented labels/messages removed); assembled by `E:/MicroLIMS/rls-tmp/assemble_guide.py` into `USER_GUIDE.md`. C6 gap headings normalised to `### GAP-6NN`.

- 2026-10-20 — Phase 10: `SCREENSHOT_PLAN.md` (123 screenshots, 102 P1) from `guide/_ss/*.md`.
- 2026-10-20 — Phase 11: three fresh-context Opus reviewers (§1–16, §17–29, §30–44) → `_review/review-A/B/C.md`, merged into `USER_GUIDE_REVIEW.md` (43 findings: 0 critical, 8 high, 16 medium, 19 low).
- 2026-10-20 — Phase 12: Opus reconciliation → `RECONCILIATION.md` (38 confirmed, 4 partly, 1 rejected, 2 new; final 10 high / 13 medium / 21 low; no CRITICAL). Proposes GAP-901..910 and updates to GAP-214/221/227/300 and clusters C2, C3, C5, C8, R3.

## Files created (Phases 5–9)
- `USER_GUIDE.md` — DRAFT 0.1, 44 sections, ~10,500 lines, 275 reviewer notes, 123 screenshot placeholders; every cited GAP id exists in `DOCUMENTATION_GAPS.md` (216 gaps).
- `USER_GUIDE_STRUCTURE.md`, `guide/NN-*.md` (edit these, then re-run the assemble script), `guide/_ss/*.md` + `_ss/_all.md` (screenshot rows for Phase 10), `knowledge/role-guide.md`, `knowledge/_clusters/R*-review.md`.

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
1. User approves `RECONCILIATION.md` (or amends decisions).
2. Phase 13: apply the confirmed corrections to `guide/NN-*.md` and the named cluster files (agy writes, Sonnet reviews), add GAP-901..910 and the gap updates to `DOCUMENTATION_GAPS.md`, re-run `E:/MicroLIMS/rls-tmp/merge_clusters.py` and `assemble_guide.py`.
3. Phase 14: final guide check against `COVERAGE_MATRIX.md`. Phase 15: `DOCUMENTATION_INDEX.md` + `.csv`. Final report.
