# User Guide Generation — Progress

Prompt: `user_guide_prompt_v2.md` (Master User Guide Generation Agent v2).

| Item | Value |
|---|---|
| Branch | `docs/user-guide` (created from `refactor/consolidation` @ `f773042c`) |
| Base commit described | `f773042c` (refactor/consolidation, PR #86 not yet merged to main) |
| Git available | Yes |
| App run locally | Not yet — screen behaviour currently from code only (lower confidence) |

## Current phase
**All phases (0–15) complete.** USER_GUIDE.md DRAFT 0.1 is corrected and ready for human review and approval. Nothing pushed; branch `docs/user-guide` is local.

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

- 2026-10-20 — Phase 13: 44 reconciliation corrections applied to `guide/` in three agy batches, each reviewed by Sonnet (nothing invented; minor tidy-ups); knowledge-base fixes in C2, C3, C5, C8, R3; GAP-901..910 added and GAP-214/221/227/300 updated; guide header gains review/approval rows and revision history.
- 2026-10-20 — Phase 14: final checks: 44/44 sections reviewed, every cited GAP id exists, no compliance or code-name wording, 123/123 screenshot placeholders planned, every module mapped to a guide section except the 7 backend-only modules (stated in §2).
- 2026-10-20 — Phase 15: `DOCUMENTATION_INDEX.md` + `DOCUMENTATION_INDEX.csv` (66 modules, 106 module × workflow rows) via `E:/MicroLIMS/rls-tmp/build_index.py`.

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

## Final report

| # | Item | Result |
|---|---|---|
| 1 | Modules | 66. Coverage matrix: every row complete; 7 backend-only modules (no screens) have no guide section by design and are listed in §2. |
| 2 | Screens | 107 screen ids |
| 3 | Workflows | 80 (81 sections; Test Master split micro / physicochemical) |
| 4 | Roles / permission codes | 4 default roles (admin-editable); 50 codes (40 enforced, 10 not enforced) |
| 5 | Business rules | 566 |
| 6 | Screenshots required | 123 (102 priority P1) |
| 7 | Documentation gaps | 226 |
| 8 | Discrepancies (spec vs code, or spec vs spec) | 17 gap entries labelled DISCREPANCY |
| 9 | Critical findings | 0 after reconciliation |
| 10 | High findings | 10 confirmed in Phase 12, all corrected in Phase 13 |
| 11 | Files created | see "Files created" sections above; all under `documentation/` |
| 12 | Human confirmation needed | GAP list (top: no URS/FRS for microbiology core GAP-001; Final Release absent GAP-014; production role grants GAP-015/016; EM/Water/AC receive and prepare need only sign-in GAP-227; unsigned return-to-analyst and count entry GAP-009/010; pathogen reset reach GAP-221; Select All export GAP-906) |
| 13 | git diff outside documentation/ | none (`git diff --name-only f773042c HEAD` lists only `documentation/` paths) |

The documentation makes no claim that the application is validated or compliant. The application was never run; all screen behaviour comes from the code.

## Next step
1. Human review of `USER_GUIDE.md` (reviewer-note boxes first) and the GAP list.
2. Optional: run a local test instance with fake data to take the 123 screenshots and confirm screen behaviour.
3. Push / PR the `docs/user-guide` branch when the user decides.
