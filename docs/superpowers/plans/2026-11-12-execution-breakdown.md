# Improvement Programme — Execution Breakdown and Model Assignment

**Date:** 2026-11-12 · **Status:** proposal, awaiting approval. Nothing is built yet.
**Source:** PR #143 (`improvement-roadmap.md`, 31 package specs and plans, `2026-11-11-build-order.md`).
**Basis:** the build order read in full; every plan's goal and task headings; the roadmap's section list. Task bodies are read when each task's brief is written, and a model choice is rechecked then.

## 1. Size

31 packages, about 255 plan tasks, 7 releases (R0–R6) and three document streams. Each release ends in a usable system and its own pull request.

## 2. Models

| Model | Used for | Share |
|---|---|---|
| **Haiku** | Repeating a pattern already proven by an earlier task; moving files; appending enum values and permissions; document close-out | about 45 tasks |
| **Sonnet** | Normal building: services, endpoints, migrations, screens, tests, parsers, writing specifications from code | about 185 tasks |
| **Fable 5.2** | Only where a mistake spreads: the shared review / approval / closing methods, login and signing, separation of duties, locks and concurrency, the first use of a new shared mechanism | 26 tasks, plus orchestration and review |

Rules that apply to every task:

- **Escalation.** One failed attempt, or a review that rejects the approach (not a detail), moves the task up one model. Nothing starts above its listed model.
- **Pilot, then repeat.** Where a task repeats one change across many screens or modules, Sonnet does the first; Haiku does the rest from that example.
- **Review.** The orchestrator (Fable) reads the full diff of every task that changes a regulated record, a permission, a migration or a shared method, and reruns its tests. Screen-only and document tasks get a Sonnet review and an orchestrator spot check.
- **"Final verification and documents" tasks.** Haiku updates the documents. The orchestrator runs the verification itself; it is never delegated.

## 3. Acceptance for every task

A task is done only when all of these hold:

1. Every step and test named in the task's plan section is built, including the plan's "Cross-plan corrections" and "Review corrections" (the later section wins).
2. Backend: build clean; full suite passes against PostgreSQL with **0 skipped**; `has-pending-model-changes` clean when the model changed; trigger catalog test passes when a migration changed.
3. Frontend: lint, `debt:check` (never raised), `tsc -b`, tests and build pass.
4. Regulated changes carry the three checks of CLAUDE.md Rule 4.4 (saved state, audit entry, refusals), and the test fails if the save or the core condition is removed (Rule 4.7).
5. The package's "Review Focus" items each have a test.

**Expected outcome per package** is the plan's Goal line; it is repeated under each package below.

## 4. How the work runs

- **Branch.** All work lands on `ccr-2969e127-6eese4`, one pull request per release. After it is merged the branch restarts from `main`.
- **Lanes.** At most one task that changes the database model runs at a time (the model snapshot cannot be merged). Alongside it run one screen-only lane and one document lane, each worker in its own worktree. Tasks on the shared files listed in the build order section 1 never run in parallel.
- **Order inside a package** is the task order, unless a row says otherwise.
- **Gate.** After each release: full verification, a summary of what ran and what could not, then your approval before the next release.

## 5. Decisions needed before starting

| # | Decision | Recommendation |
|---|---|---|
| 1 | PR #143 is an unmerged draft, so the plans are not on `main`. | You merge #143; I then start from `main`. Otherwise I merge its branch into this one. |
| 2 | How far to go before stopping. | R0 only, then a gate after every release. |
| 3 | Tasks waiting on site inputs: B1 (printer, labels, scanner), B3 (identity provider), E1 Task 8 (ChemStation), E2 (SAP codes), E4 Task 5 (VITEK 2 files). | Build everything that does not need the input; hold the listed tasks and say so at the gate. |
| 4 | A1 writes URS and FS into `documents/`, which CLAUDE.md treats as the authority. | Mark each as Draft until you approve it. Where the code and an existing document disagree, list the conflict; never pick a side. |
| 5 | G1–G4 (file size, test debt, query findings, operations) have no plan in #143. | G1 and G2 are applied inside every package. G3 and G4 wait for their own plans. |

## 6. Task breakdown

Columns: task · model · reason. "Deps" lists what must be merged first.

### R0 — Safety and groundwork

**G5 Laboratory scope on test-configuration writes.** Outcome: seven writes enforce laboratory scope, five check versions. Deps: none.

| Task | Model | Reason |
|---|---|---|
| 1 Workflow step and type writes | Sonnet | Applies the existing scope and version pattern |
| 2 Freeze and unfreeze | Sonnet | Same pattern, second service |
| 3 Frontend sends versions | Haiku | Mechanical: add one field to seven calls |
| 4 Verification and record | Haiku + orchestrator | Document update; orchestrator verifies |

**P0 Programme conventions.** Outcome: `SignatureMeaning` pin test; `AdvisoryLockKeys` with a distinct-values test. Deps: none.

| Task | Model | Reason |
|---|---|---|
| Pin test and lock-key class | Haiku | Fully specified, no judgement |

**A1 Specification set** (starts here, runs alongside). Outcome: numbered URS and FS for every existing area, calculation register, conflicts list, CI check.

| Task | Model | Reason |
|---|---|---|
| 1 Document checker and CI step | Sonnet | Small tool with tests |
| 2 Skeleton, templates, Document Control move | Haiku | File moves and templates |
| 3–8 Area groups (six, in parallel) | Sonnet | Reading code and writing requirements; separate files |
| 9 Calculation register | Sonnet | Every formula cited to file and line; orchestrator checks a sample against the code |
| 10 Close-out | Haiku | Index and cross-links |

### R1 — Identity and access

**B4 QA role.** Outcome: independent read-everything QA role; audit trail review and access review signed. Deps: R0.

| Task | Model | Reason |
|---|---|---|
| 1 Role type, permissions, migration, seeder | Sonnet | Standard model change |
| 2 Independence rule | Fable | Separation of duties, used by five later packages |
| 3 Cross-section reading, write refusal | Fable | Changes scope checks across every service |
| 4 Audit trail review — domain, schema, service | Sonnet | New protected table following the existing pattern |
| 5 Audit trail review — API and PDF | Sonnet | Standard |
| 6 Access review signature | Sonnet | Reuses the signature service |
| 7 QA as document approver | Sonnet | Standard |
| 8 Frontend — role type, labels, routing | Haiku | Labels and route entries |
| 9 Frontend — review screens | Sonnet | Two new screens |
| 10 Documents and verification | Haiku + orchestrator | |

**B2 Multi-factor authentication.** Outcome: TOTP second factor, trusted computers, recovery codes, forced enrolment, administrator reset. Deps: B4.

| Task | Model | Reason |
|---|---|---|
| 1 TOTP algorithm | Sonnet | Published algorithm with test vectors |
| 2 Secret protector | Sonnet | Specified in the plan; orchestrator reviews key handling |
| 3 Data model, settings, event codes, migration | Sonnet | Standard |
| 4 Policy service | Sonnet | Standard |
| 5 Login challenge, trusted computers, recovery codes | Fable | The login path; a flaw locks users out or lets them in |
| 6 Self-service enrolment | Sonnet | Standard |
| 7 Administrator actions and policy guard | Sonnet | Standard |
| 8 Revocation hooks and startup check | Sonnet | Standard |
| 9–11 Frontend — login step, enrolment, administration | Sonnet | Three screens, one after another |
| 12 Final verification and documents | Haiku + orchestrator | |

**B3 Single sign-on.** Outcome: company sign-in by OIDC or Active Directory, separate signing password, emergency local accounts. Deps: B2. Site input needed for a live test.

| Task | Model | Reason |
|---|---|---|
| 1 Data model, settings, event codes, migration | Sonnet | Standard |
| 2 Mode service, local-login refusal, emergency accounts | Fable | Lock-out risk for the whole site |
| 3 Signing password | Fable | The two-part electronic signature depends on it |
| 4 OpenID Connect sign-in | Sonnet | Standard library flow |
| 5 Active Directory sign-in | Sonnet | Standard |
| 6 Leavers — session cap and worker | Sonnet | Standard |
| 7 Administration API | Sonnet | Standard |
| 8–9 Frontend — sign-in, administration | Sonnet | Standard |
| 10 Final verification and documents | Haiku + orchestrator | |

**B1 Barcode labels and scanning** (parallel to B4/B2/B3; only Task 2 uses the model lane). Outcome: labels for six record types, scan to open, scan to pick lots and instruments. Site input needed for label formats.

| Task | Model | Reason |
|---|---|---|
| 1 Payload registry and normaliser | Sonnet | Pure logic with tests |
| 2 Data model and migration | Sonnet | Standard |
| 3 Equipment code freeze and asset link | Sonnet | Standard |
| 4 Barcode encoder | Sonnet | Library use with tests |
| 5 Label PDF renderer | Sonnet | Layout work |
| 6 Label data source, usability rules, formats | Sonnet | Standard |
| 7 Print service and endpoints | Sonnet | Standard |
| 8 Creation endpoints return what they created | Haiku | Same small change on each endpoint |
| 9 Scan resolve service and endpoint | Sonnet | Scope checks on lookup |
| 10 Frontend service and print UI | Sonnet | Standard |
| 11 Offer printing where records are created | Haiku | Repeats Task 10's button |
| 12 Scan to open in Ctrl+K | Sonnet | Standard |
| 13 ScanField and five pickers | Sonnet first, Haiku for pickers 2–5 | Pilot, then repeat |
| 14 Camera scanning, label formats page | Sonnet | Standard |
| 15 Final verification and documents | Haiku + orchestrator | |

### R2 — Daily experience

**C2 Bench and tablet mode.** Outcome: bench mode, tablet layouts, local drafts, installable shell. Deps: R1.

| Task | Model | Reason |
|---|---|---|
| 1 Preference and theme variant | Sonnet | Standard |
| 2 Idle activity, Switch user, wake lock | Sonnet | Session handling; orchestrator reviews |
| 3 Number field, keypad, action bar, bench dialog | Sonnet | Shared components |
| 4 Draft store, connection status, draft hook | Sonnet | Shared block used by C3, C4, C7, F2 |
| 5 Count, incubation, qualitative screens | Sonnet | Standard |
| 6 Backend — draft start time in audit trail | Sonnet | Small regulated change |
| 7 EM and water grids | Sonnet | Standard |
| 8 Pathogen observation screens | Sonnet | Standard |
| 9 Media and solution preparation | Sonnet | Standard |
| 10 Installable shell | Sonnet | Caching rules must not keep records |
| 11 Final verification and documents | Haiku + orchestrator | |

**C4 Faster data entry.** Deps: C2. Outcome: batch receiving, field errors, keyboard model, paste with preview.

| Task | Model | Reason |
|---|---|---|
| 1 Field-error contract | Sonnet | Shared block, well specified |
| 2 Lock-safe sample numbers | Fable | Lock must cover number and insert in one transaction |
| 3 Batch receive — backend | Sonnet | Standard, on top of Task 2 |
| 4 Grid navigation, shortcuts, focus | Sonnet | Standard |
| 5 Receiving grid | Sonnet | Standard |
| 6 Last-choice defaults | Sonnet | Standard |
| 7 Paste with preview — receiving, EM, water | Sonnet | Standard |
| 8 Server count preview | Sonnet | Reuses backend calculation |
| 9 Configuration paste (five screens) | Sonnet first, Haiku for 2–5 | Pilot, then repeat |
| 10 Remaining grids, verification, documents | Haiku + orchestrator | Repeats Task 4 |

**C7 Simpler configuration.** Deps: C4. Outcome: four wizards, where-used panel, server-enforced reasons, specification retirement.

| Task | Model | Reason |
|---|---|---|
| 1 Where-used service and panel | Sonnet | Read queries |
| 2 Wizard frame | Sonnet | Shared component |
| 3 Reason rule, specification retirement, validate-only | Fable | Historical results must stay bound to their specification (Rule 3.1) |
| 4 Specification wizard | Sonnet | Standard |
| 5 Copy specifications; item copy | Sonnet | Standard |
| 6 Test Master wizard | Sonnet | Largest screen; standard |
| 7 Equipment wizard and copy | Sonnet | Repeats the frame |
| 8 Add Material wizard | Sonnet | Repeats the frame |
| 9 Final verification and documents | Haiku + orchestrator | |

**C3 Smarter tables.** Deps: C2. Outcome: saved views, URL state, bulk actions, audited export, virtual scrolling.

| Task | Model | Reason |
|---|---|---|
| 1 Characterisation tests and table registry | Sonnet | Must pin current behaviour correctly |
| 2 Table state, URL sync, columns, density | Sonnet | Standard |
| 3–4 Saved views — backend, frontend | Sonnet | Standard |
| 5 Selection, action bar, audited CSV export | Sonnet | Standard |
| 6 Bulk assign | Sonnet | Regulated write; orchestrator reviews |
| 7 Move three Reports tables; remove `DataTable` | Sonnet | Standard |
| 8 Virtual scrolling, expandable rows | Sonnet | Standard |
| 9 Sample register and workspaces | Sonnet | Sets the pattern |
| 10 Remaining registers | Haiku | Repeats Task 9 |
| 11 Final verification and documents | Haiku + orchestrator | |

**C6 In-app help.** Task 1 first (before C1); the rest after C1.

| Task | Model | Reason |
|---|---|---|
| 1 Interface-state store, version, Help menu | Sonnet | Shared block for C8 and F4 |
| 2 Guide content, route map, safe rendering | Sonnet | Rendering must be safe |
| 3 Help panel and guide page | Sonnet | Standard |
| 4 Field hints | Haiku | Repeated text entries |
| 5 Items using a test, Open SOP | Sonnet | Standard |
| 6 First-login tour | Sonnet | Standard |
| 7 What's new | Haiku | Small, specified |
| 8 Final verification and documents | Haiku + orchestrator | |

**C1 My Work.** Deps: C6 Task 1. Outcome: one landing page of everything waiting for the user; self-assignment.

| Task | Model | Reason |
|---|---|---|
| 1 Contract, urgency rules, aggregator | Sonnet | Specified in the plan |
| 2 Analyst sources and the single ready rule | Sonnet | Reuses backend eligibility |
| 3 Review and approval sources with locking | Fable | Concurrency and self-review rules together |
| 4 Take and release | Sonnet | Standard regulated write |
| 5 Documents and equipment sources | Sonnet | Standard |
| 6–7 Frontend — page, landing, badge | Sonnet | Standard |
| 8 Final verification and documents | Haiku + orchestrator | |

**C5 Notifications and live updates.** Deps: C1.

| Task | Model | Reason |
|---|---|---|
| 1 Data model, catalogue, migration | Sonnet | Standard |
| 2 Publisher, recipients, event wiring | Fable | Edits the shared review and approval methods |
| 3 Scheduler, worker, outbox sender | Sonnet | Standard worker |
| 4 Notification centre and preferences | Sonnet | Standard |
| 5 Working hours | Sonnet | Pure logic with tests |
| 6 Escalation rules | Sonnet | Standard |
| 7 Live signals — backend | Fable | First SignalR use; hub access and section scope |
| 8 Live signals — frontend | Sonnet | Standard |
| 9 "Changed by someone else" banner | Sonnet | Standard |
| 10 Final verification and documents | Haiku + orchestrator | |

**C8 Navigation.** Deps: C5.

| Task | Model | Reason |
|---|---|---|
| 1 Favourites | Sonnet | Standard |
| 2 Recently opened | Sonnet | Standard |
| 3 Last laboratory and tab | Haiku | Small, uses `UserUiState` |
| 4 Crumbs on deep pages | Haiku | Repeated per page |
| 5 Action availability — backend (`ActionCheck`) | Fable | One check feeds action and screen; five later packages build on it |
| 6 Gated-action component and adoption | Sonnet | Standard |
| 7 Final verification and documents | Haiku + orchestrator | |

**F4 Arabic and RTL — foundation.** Deps: C6 Task 1, C1, C3. Arabic wording needs a native reader at the site.

| Task | Model | Reason |
|---|---|---|
| 1 Foundations | Sonnet | Standard |
| 2 Shared components and layout | Sonnet | Standard |
| 4 Server message codes | Sonnet first, Haiku for the rest | Pilot, then repeat |
| 3 Modules (one commit each, runs through later releases) | Sonnet for the first module, Haiku after | Repetitive conversion |
| 5 Screenshots and documents | Haiku | |

### R3 — Quality core

**D1 OOS investigation.** Deps: B4, C5, C8. Outcome: signed investigation opened at review; approval and certificates blocked until concluded; signed retest plan.

| Task | Model | Reason |
|---|---|---|
| 1 Shared outcome reading | Sonnet | Refactor with tests |
| 2 Entities, schema, numbering, protection | Sonnet | Existing protected-table pattern |
| 3 Opening at review, and the gate | Fable | Inside `CompleteReviewCoreAsync` and the approval gate |
| 4 Permissions | Haiku | Constants and seeder rows |
| 5 Templates, categories, Phase I | Sonnet | Standard |
| 6 Retest plan and the single closing method | Fable | Extracts `FinalizeClosedSampleAsync` used by both closing paths |
| 7 Phase II, conclusion, reopen | Sonnet | Signed transitions, specified |
| 8 QA request and notifications | Sonnet | Standard |
| 9 Excursion review | Sonnet | Standard |
| 10 Read API, report, trending | Sonnet | Standard |
| 11–12 Frontend | Sonnet | Standard |
| 13 Documents and verification | Haiku + orchestrator | |

**D2 Quality events.** Deps: D1.

| Task | Model | Reason |
|---|---|---|
| 1 Schema, numbering, protection, permissions | Sonnet | Standard |
| 2 Core service | Sonnet | Standard |
| 3 Deviation | Sonnet | Sets the pattern |
| 4 Checklists, classifications, due rules | Sonnet | Standard |
| 5–7 CAPA, change control, complaint | Sonnet | Follow Task 3's pattern |
| 8 Section and version checks on master-data writes | Sonnet | Repeats G5's pattern |
| 9 Master-data change-control gate | Fable | Cuts across every master-data write |
| 10 Queries, report, trending, overdue job | Sonnet | Standard |
| 11–13 Frontend | Sonnet | Standard |
| 14 Documents and verification | Haiku + orchestrator | |

**D4 Competency gate.** Parallel to D1 for its own files; merged after D1.

| Task | Model | Reason |
|---|---|---|
| 1 Records, intervals, permissions | Sonnet | Standard |
| 2 The guard | Fable | Shared block for D5, E4, F2, E1 |
| 3 Recording points | Fable | Every recorder and the pathogen session service; coverage test |
| 4 Assignment points | Sonnet | Standard |
| 5 Override and separation | Fable | Signed override inside the review path |
| 6 Switch, impact preview, reminders, matrix | Sonnet | Standard |
| 7 Frontend | Sonnet | Standard |
| 8 Documents and verification | Haiku + orchestrator | |

**D5 SOP revision on test.** Deps: D4.

| Task | Model | Reason |
|---|---|---|
| 1 Schema and capture service | Sonnet | Standard |
| 2 Call at every first-step point | Sonnet | Uses D4's recording points and coverage test |
| 3 Display | Sonnet | Report and certificate header |
| 4 Verification | Orchestrator | |

### R4 — Integration

**E1 Instrument connectivity.** Task 8 waits for ChemStation answers.

| Task | Model | Reason |
|---|---|---|
| 1 Instruments, formats, permissions | Sonnet | Standard |
| 2 Readings and the browser route | Sonnet | Standard |
| 3 Frame parsers | Sonnet | Pure logic with tests |
| 4 Captured values, balance checks, minimum weight | Sonnet | Rules come from configuration |
| 5 Connect the fields | Sonnet first, Haiku after | Pilot, then repeat |
| 6 Browser route and capture UI | Sonnet | Standard |
| 7 The agent | Sonnet | Standard |
| 8 Export files, pending data, adoption | Sonnet | Held for site input |
| 9 Documents and verification | Haiku + orchestrator | |

**E2 ERP integration.** Deps: E1, B4, D1. Mapping waits for SAP codes.

| Task | Model | Reason |
|---|---|---|
| 1 Clients, inbox, outbox schema, permissions | Fable | Shared outbox and machine-client model that E3 and E4 reuse |
| 2 Inbound — mapping, inspection lots, expected samples | Sonnet | Standard |
| 3 Outbox worker and transports | Sonnet | Standard worker |
| 4 Receiving from expected samples, outbound decision | Fable | Adds the final-decision hook to the approval path |
| 5 Corrections after sending | Fable | Post-decision gate on approved records |
| 6 Monitoring and configuration UI | Sonnet | Standard |
| 7 Documents and verification | Haiku + orchestrator | |

**E3 Public API and webhooks.** Deps: E2.

| Task | Model | Reason |
|---|---|---|
| 1 Accounts, keys, scopes, rate limit, access log | Fable | New outside-facing access path |
| 2 Samples, results, certificates, items | Sonnet | Read endpoints; sets the pattern |
| 3 Customers | Sonnet | Customer scoping |
| 4 Quality records, documents, KPIs | Sonnet | Follows Task 2 |
| 5 Webhooks | Sonnet | Uses E2's outbox |
| 6 Screens and documents | Sonnet, Haiku for documents | |

### R5 — Science modules

**D3 Stability studies.** Deps: B4, D2.

| Task | Model | Reason |
|---|---|---|
| 1 Role, permissions, conditions, chamber type | Haiku | Enum and seeder rows |
| 2 Protocol schema and limit sets | Sonnet | Standard |
| 3 Limit-set aware specification resolution | Fable | Changes `SpecificationLookup` and every reader |
| 4 Authoring, approval, amendments | Sonnet | Signed transitions, specified |
| 5 Chambers, placement, schedule, inventory | Sonnet | Standard |
| 6 Pull to sample, time zero | Fable | Receiving path and the `Routine()` rule across all queries |
| 7 Missed pulls, excursions, reminders | Sonnet | Standard |
| 8 Results, report, completion | Sonnet | Standard |
| 9 Frontend | Sonnet | Standard |
| 10 Documents and verification | Haiku + orchestrator | |

**E4 Microbial identification.** Deps: D4, E1. Task 5 waits for VITEK 2 files.

| Task | Model | Reason |
|---|---|---|
| 1 Organism list control | Sonnet | Standard |
| 2 Isolates and manual identifications | Sonnet | Standard |
| 3 Mapping, thresholds, acceptance, final identity | Sonnet | Configured rules; database-enforced status |
| 4 Requirements, open list, pathogen consistency | Sonnet | Standard |
| 5 VITEK 2 import | Sonnet | Held for site input |
| 6 Screens, reports, documents | Sonnet, Haiku for documents | |

**F1 EM analytics.** Deps: D1, E4.

| Task | Model | Reason |
|---|---|---|
| 1 Grades, areas, drawings, markers | Sonnet | Standard |
| 2 Room states | Sonnet | Standard |
| 3 Trends, seasons, flora | Sonnet | Read queries |
| 4 Trend rules and evaluation | Sonnet | Versioned, configured rules |
| 5 Firing effects | Fable | Hook on closing; must never roll back an approval |
| 6 Screens | Sonnet | Standard |
| 7 Documents and verification | Haiku + orchestrator | |

**F2 Evidence photos.** Deps: D4, C2.

| Task | Model | Reason |
|---|---|---|
| 1 Photo record, storage, derivatives | Sonnet | Standard |
| 2 Targets and scope | Sonnet | Standard |
| 3 Required photos | Sonnet | Uses D4's recording points |
| 4 Frontend capture, gallery, review | Sonnet | Standard |
| 5 Report appendix and verification | Sonnet + orchestrator | |

**F3 Retained sample storage.** Deps: B1, B4, D1, D2.

| Task | Model | Reason |
|---|---|---|
| 1 Locations | Sonnet | Standard |
| 2 Register | Sonnet | Registers at review, after the signature |
| 3 Custody events | Sonnet | Append-only pattern |
| 4 Retention rules | Sonnet | Standard |
| 5 Disposal | Sonnet | QA-signed, specified |
| 6 Screens and verification | Sonnet + orchestrator | |

### R6 — Insight

**F5 Self-service reports.** Deps: D1, D2.

| Task | Model | Reason |
|---|---|---|
| 1 Datasets, spec, compiler, Results dataset | Fable | A query builder that must hold section scope for every user |
| 2 Server exports and logging | Sonnet | Standard |
| 3 Saving and sharing | Sonnet | Standard |
| 4 Other datasets | Sonnet | Follows Task 1 |
| 5 Schedules and snapshots | Sonnet | Standard |
| 6 Builder UI and documents | Sonnet, Haiku for documents | |

**F6 Review by exception.** Deps: E1, D4, F2.

| Task | Model | Reason |
|---|---|---|
| 1 Exception service | Sonnet | Read logic |
| 2 Materials and equipment at use | Sonnet | Standard |
| 3 Ticks and completion | Fable | Bound to the review signature inside the review path |
| 4 Screen and queue | Sonnet | Standard |
| 5 Later-package sources and verification | Sonnet + orchestrator | |

### Throughout

**A2 Traceability and validation pack** (after A1; grows with each release).

| Task | Model | Reason |
|---|---|---|
| 1 Risk assessment format and check | Sonnet | Small tool |
| 2 Backend requirement tags | Sonnet | Attribute and discovery |
| 3 Matrix builder | Sonnet | Small tool |
| 4 CI job and evidence artifact | Sonnet | Standard |
| 5–10 Risk rows, tags and OQ scripts per area group | Sonnet | Drafts; risk ratings need your approval |
| 11 IQ, PQ, templates, plan update | Haiku | Templates |
| 12 Close-out | Haiku | |

**A3 User manual and SOP templates** (after R2; refreshed each release).

| Task | Model | Reason |
|---|---|---|
| 1 Move the guide and pictures | Haiku | File moves |
| 2 Role guides | Sonnet | Writing from the screens |
| 3 Capture tool skeleton and safety | Sonnet | Must refuse to run against production |
| 4 Demo data through the API | Sonnet | Standard |
| 5 Shot list and capture | Haiku | Repetitive list |
| 6 Manual CI job | Haiku | Copies an existing job |
| 7 SOP templates | Sonnet | Writing |
| 8 Template checks | Haiku | Small check |
| 9 Close-out | Haiku | |

## 7. What can run in parallel

| Release | Model lane (one at a time) | Alongside |
|---|---|---|
| R0 | G5 → P0 | A1 Tasks 1–2, then 3–8 six at once |
| R1 | B4 → B2 → B3, B1 Task 2 slotted between | B1 other tasks; frontend tasks of the package before |
| R2 | C2 → C4 → C7; C6 Task 1 → C1 → C5 → C8; C3 Task 3 slotted | C3 screens; C6 rest; F4 foundation; A3 |
| R3 | D1 → D4 → D2 → D5 | D4's own-file tasks during D1; frontends |
| R4 | E1 → E2 → E3 | frontends; F4 module conversion |
| R5 | D3 → E4 → F1 → F2 → F3 | frontends |
| R6 | F5 → F6 | frontends |
