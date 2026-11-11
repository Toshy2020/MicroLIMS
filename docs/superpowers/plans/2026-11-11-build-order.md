# MicroLIMS Improvement Programme — Build Order and Programme Conventions

**Date:** 2026-11-11
**Basis:** a whole-programme review of the 31 plans in this folder and of the code, done with eight read-only code and plan surveys, then an independent review of this document. Each affected plan ends with a "Cross-plan corrections" section and, where the independent review changed something, a "Review corrections" section; the later section wins.

## 1. The codebase in numbers (as surveyed, not run)

| Area | Size |
|---|---|
| Backend Domain | 199 entities and DbSets; 57 with a version token; 223 migrations; 13 append-only protected tables |
| Backend Application | 468 files, 84,500 lines; largest files 1,000–1,400 lines (`PathogenSessionService`, `TestDefinitionMasterDataService`, `ReportDocumentMapper`, `IncubationStepRecorder`) |
| Backend API | 106 controllers; JWT plus an agent-key scheme; permission policies from 70 codes; no SignalR; no global query filter anywhere |
| Backend tests | 455 files; about 3,400 facts, 180 theories, 352 PostgreSQL tests; 12 architecture tests (including a source-file size limit and an authorisation matrix every new endpoint must join) |
| Frontend | 926 files; 24 modules (laboratory configuration alone 44,000 lines); no query library; 178 test files; debt ratchet at 668 `fireEvent`, 129 project mocks, 340 `any` |
| CI | Backend run twice (without and with PostgreSQL) plus a result checker; migration-drift check; frontend lint, debt, types, tests, build; an `add-migration` workflow for agent branches |

**Shared code many packages change** (these decide the order more than anything else):

| File / method | Changed by |
|---|---|
| `SampleReviewService.CompleteReviewCoreAsync` | C5, C8, D1, D4, F3, F6 |
| `SampleApprovalService.DecideCoreAsync` (~380 lines), `FinalizeIfClosedAsync` and the retest path `PropagateOosOutcomeAsync` | C5, C8, D1, D4, E2, E3, F1 |
| Recorders (`IncubationStepRecorder`, `PathogenConfirmationRecorder`, `TestWorkflowSupport`, the per-type recorders) and `PathogenSessionService` | C4, D3, D4, D5, E1, E4, F2 |
| `ProductWorkflowEngine.ReceiveAsync` and the receiving forms | B1, C4, D3, E2, E3, F3 |
| `SpecificationLookup` and every specification reader | C7, D3 |
| `SampleSummaryService`, `CertificateOfAnalysisBuilder`, `ReportDocumentMapper` | D1, D5, E3, F2 |
| `TestWorkflowDialog.tsx`, `SampleSummaryDialog.tsx`, `TestResultCards.tsx` | C2, C4, C6, D5, E4, F2, F6 |
| `Header.tsx`, `MainLayout.tsx`, `Sidebar.tsx`, `menuConfig.ts` | C1, C2, C4, C5, C6, C8, F4 and every package adding a page |
| `PermissionConstants`, `DbSeeder`, `SegregationOfDutiesRules`, `SignatureMeaning` | almost every package |

## 2. Programme conventions (decided once, used by every package)

### 2.1 Enumerations stored as numbers
- `SignatureMeaning` today has 38 members (last `SampleAssignedToRun` = 37). **One R0 commit ("programme groundwork") adds a test that pins every member's name and number**; each later package appends its values at the end, in one commit, and extends the test. Planned additions per package: B4 `AuditTrailReviewed`; D1 six values; D2 fourteen; D3 nine; D4 four; E2 `ErpCorrectionApproved`; E4 `IdentificationConfirmed`; F3 two. Their numbers are whatever the end is when the package merges — never chosen in advance, never inserted in the middle.
- `RoleType`: `Qa = 5` (B4), `RdManager = 6` (D3). `EquipmentType`: `StabilityChamber` appended after `Caliper` (D3); E1 adds nothing (Balance, PhMeter, IcpOes, Gc exist).

### 2.2 Numbering and advisory locks
One class `backend/MicroLIMS.Application/Helpers/AdvisoryLockKeys.cs`, created in the same R0 groundwork commit, with a unit test that all values are distinct. Constants: `SampleNumber`, `OosNumber`, `QualityDeviation`, `QualityCapa`, `QualityChangeControl`, `QualityComplaint`, `StabilityProtocol`, `Isolate`, `RetainedSample`, `DisposalRequest`. Values are distinct 64-bit numbers chosen once and never reused. All numbers use the laboratory-clock year or month (today `GenerateOosCodeAsync` uses the UTC month; D1 Task 2 fixes it). A lock must cover generating the number **and** inserting the row in one transaction (see C4's review corrections).

### 2.3 Shared building blocks and who builds them
| Block | Built by | Used by |
|---|---|---|
| `UserUiState` | C6 Task 1 | C8, F4 |
| `api/client-settings`, `useBenchDraft` (explicit field allow-list) | C2 | C3, C4, C7, F2 |
| `FieldError`, `FieldValidationException` | C4 Task 3 | C7, later forms |
| My Work endpoint and page | C1 | C5 Task 8, C6 Task 6 (tours), F4 (`PartialOutageBanner`) |
| Notification publisher, live signals | C5 (after C1) | D1, D2, D4, F5 and later |
| Excursion review API (`OpenManualExcursionAsync`, `OpenExcursionForReadingsAsync`) | D1 Task 9 | F1 |
| `OosCauseCategory` (shared cause list) | D1 | D2 |
| `ICompetencyGuard`, `ItemSopResolver`, `RecordingPointCoverageTests` | D4 | D5, E4, F2, E1 |
| `EquipmentCalibration.IsCurrent(Equipment, DateTime nowUtc)` | D3 Task 5 or E1, whichever first | both |
| Shared outbox (`Channel`, `OrderingKey`), `IntegrationClient` (`Kind`) with `IntegrationClientKey` rows, key scheme `ClientKey` / header `X-Api-Key` with a `client_kind` claim, `PostDecisionCorrectionGate` (SAP-linked samples only) | E2 | E3, E4 |
| `FinalizeClosedSampleAsync` (one closing method for approval and retest paths) | D1 Task 6 | E2, E3, F1 hooks |
| `ActionCheck` (one check feeds both the action and its availability) | C8 Task 5 | D1, D4, E2, F3, F6 pre-signing checks |
| Post-decision hook list (see 2.4) | first of E2 / F1 / E3 to touch `FinalizeIfClosedAsync` | the others |

### 2.4 Hooks in the two busiest methods
- **Closing a sample:** D1 Task 6 extracts `FinalizeClosedSampleAsync`, used both by approval and by the retest path that today closes the original sample by itself. The first package that adds work there introduces `IFinalDecisionHook` (`Task OnFinalDecisionAsync(Sample sample, ArchivedRecord? certificate, CancellationToken ct)` — the certificate can be missing because archiving can fail), called in registration order inside the same transaction. E2 (ERP outbox), E3 (webhook events) and F1 register hooks instead of editing the method. No hook calls an external service. **F1's hook only queues a trend-evaluation request; the evaluation runs in a worker with retries, so it can never roll back an approval** (user decision 2026-11-11). E2 and E3 write their messages in the same save as the decision.
- **`CompleteReviewCoreAsync`:** order of checks before the review signature — existing checks → D4 separation (override) → F3 retained quantity → F6 exceptions ticked; after the signature, in the same save — D1 open investigation → F3 register retained sample → F6 record exceptions → C5 notifications queued for after commit. Each pre-signing check is also an `ActionCheck` (C8), so the screen and the server agree. C5 Task 2 is built before C8 Task 5; both change `CompleteReviewAsync`.

### 2.5 Migrations
One migration per package task that changes the model, named as each plan's corrections section says. Never two packages' changes in one migration. When two branches add migrations in parallel, the later one is regenerated after rebasing (the model snapshot must match), using the repository's `add-migration` workflow where needed. **Regeneration drops hand-written SQL**: any trigger or data SQL in the old migration must be re-applied to the new one and the trigger catalog test run (CLAUDE.md Rule 7.4).

### 2.6 Audit capture
Approved exclusions: `MfaChallenge` (2026-11-10), and (2026-11-11) `SsoPendingRequest`, `SsoExchangeCode`, `EmailOutbox`, `EscalationEvent`, `UserUiState`, `RecentRecord`, `IntegrationInboxMessage`, `IntegrationOutboxMessage`, `ApiAccessLogEntry`. Each is added with a justification comment. **`ReviewExceptionTick` stays audited** (CLAUDE.md Rule 3.3: review records may not be excluded; decision revised 2026-11-11). Nothing else is excluded without asking.

### 2.7 Translation
Once F4 Tasks 1, 2 and 4 are merged (language foundation, shared components, server message codes), every new screen uses translation keys and every new server refusal carries a message code; the ratchets enforce it. Every later plan repeats this in its corrections. Building the foundation early (see 3) is cheaper than converting all new screens later.

## 3. Build order

The order follows the hard dependencies, puts the shared building blocks before their users, and spreads changes to the busy files over time. Each release is usable on its own.

```
R0  G5 ─ conventions (SignatureMeaning pin test, AdvisoryLockKeys)      A1 starts (writing stream)
R1  B4 QA role ── B2 MFA ── B3 SSO          B1 labels and scanning
R2  C2 bench ─┬─ C4 data entry ── C7 configuration
              └─ C3 tables ─────────────┐
    C6 Task 1 (UserUiState) ── C1 My Work ─┼─ F4 Tasks 1, 2, 4 (language foundation)
                                          ├─ C5 notifications ── C8 navigation
                                          └─ C6 rest (help, tours)
R3  D1 OOS ── D2 quality records          D4 competency ── D5 SOP revision
R4  E1 instruments ── E2 ERP ── E3 public API
R5  D3 stability     E4 identification ── F1 EM analytics     F2 photos     F3 retained samples
R6  F5 reports       F6 review by exception
Throughout: A1 → A2 (grows with each release) ; A3 after R2 (screenshots) and refreshed each release ; F4 Task 3–5 module conversion
```

| Release | Packages (in order) | Why here |
|---|---|---|
| **R0 — Safety and groundwork** | G5; programme conventions (2.1, 2.2); A1 begins | G5 is a security fix independent of everything. The conventions must exist before parallel work starts. A1 is writing and runs alongside all releases. |
| **R1 — Identity and access** | B4, B2, B3; B1 in parallel | B4 is needed by D1, D2, D3, E2, F3 (QA role and independence rule). B3 needs B2. B1 is independent and gives scanning to C3, C4, F3. |
| **R2 — Daily experience** | C2 → C4 → C7; C2 → C3; C6 Task 1 → C1 → C5 → C8; C6 rest after C1; F4 Tasks 1, 2, 4 after C6 Task 1, C1 and C3 | C2 provides drafts and client settings to C3, C4, C7; C4 provides field errors to C7; C6 Task 1 provides interface state to C8 and F4. C1 comes before C5 (which edits My Work) and C6's tours. C5's review changes come before C8's. F4 needs C1's banner and C3's table changes; building its foundation here makes every later screen bilingual from the start. |
| **R3 — Quality core** | D1 → D2; D4 → D5 | D2 needs D1 (cause list, deviation requests, numbering). D1 Task 6 builds the single closing method used by later hooks. D5 reuses D4's SOP resolver and recording-point rule. D4 also covers the pathogen session screen, which writes results through its own service. D1 and D4 can run in parallel; both touch review and approval, so merge D1 first. |
| **R4 — Integration** | E1 → E2 → E3 | E2 builds the shared outbox, machine-client model and post-decision gate that E3 uses; E2's file transport uses E1's agent. |
| **R5 — Science modules** | D3; E4 → F1; F2; F3 | D3 needs B4 and D2 (system-raised deviations for missed pulls); it builds the calibration helper itself if E1 has not. E4 needs D4 (and E1 for VITEK 2). F1 needs D1's excursion API and E4's identifications. F3 needs B1, B4 and the D1/D2 links. F2 uses D4's recording points. |
| **R6 — Insight** | F5; F6 | F5's quality dataset needs D1 and D2. F6's exception sources are richest after E1, D4 and F2. |

**If several people work in parallel:** stream 1 documents (A1 → A2 → A3); stream 2 security (G5, B4, B2, B3, then E2/E3); stream 3 user experience (B1, C2–C8, F4); stream 4 quality and science (D1–D5, then D3, E4, F1–F3). The critical path is **B4 → D1 → D2 → D3** and **D4 → E4 → F1**.

## 4. Inputs needed before a package can start

| Package | Needed from the site |
|---|---|
| B1 | Printer model, label sizes, scanner model, label SOP |
| B3 | Identity provider details (OIDC) or directory details (LDAP) |
| D1, D2 | Checklists can ship empty; nothing blocking |
| D3 (chambers), E1 (balances and pH meters) | **Decided 2026-11-11:** a missing calibration due date counts as in calibration (CONF-003) |
| C4 Task 4 | Whether a supplier certificate may be attached before the sample is registered (C4 spec §9) |
| C7 Task 3 | **Decided 2026-11-11:** a result with no specification is accepted as a pass, and is flagged to the reviewer (F6 exception) and on the approval screen |
| E1 step 6 | ChemStation open questions and sample exports |
| E2 | SAP codes: usage decision codes, inspection characteristics, plant codes; middleware choice |
| E4 Task 5 | VITEK 2 sample export files |
| F4 | None (translation done in the package) |

## 5. Risks the review found and how the plans now handle them

| Risk | Handling |
|---|---|
| Incubation and pathogen recording check laboratory access in the API, not in the recorders, and the pathogen session screen writes results through its own service; a check placed "next to the access check" would miss them | D4/D5/F2 corrections: the guard, SOP capture and photo rule are called in every recording method including `PathogenSessionService`; `RecordingPointCoverageTests` fails on any method that writes a result entity without the guard |
| The retest path closes the original sample without the normal closing method, so hooks would not run | One closing method (D1 Task 6) used by both paths |
| Reviewer ticks excluded from audit would break CLAUDE.md Rule 3.3 | Ticks stay audited; unticking keeps the row |
| D3 would have added the first global query filter, which also hides rows behind required navigations | Replaced by an explicit `Routine()` predicate enforced by an architecture test |
| The sample-number lock would have been released before the sample row was saved | Lock covers generate-plus-insert in one transaction; generator refuses to run outside one |
| Webhooks and ERP messages would collide in one outbox | E2 outbox has channels and ordering keys; E3 uses the webhook channel |
| Two different machine-key schemes (E2, E3) | One `IntegrationClient` with kinds, several keys per client, and a `client_kind` claim so ERP and public-API keys cannot cross |
| Correction gate named differently in E2, E3, E4 | One `PostDecisionCorrectionGate` built by E2, applying only to SAP-linked samples (user decision); other post-approval changes follow normal rules (Section Head with reason) |
| D3 needed system-raised deviations that D2 did not allow | D2 creates the raiser as optional from the start |
| Identification attempts described as both immutable and changing (E4) | Facts immutable; status changes forward only, enforced in the database |
| Photo files keyed by a row id that does not exist yet (F2) | GUID key; pending photos bound once by a controlled update |
| Enum numbers and lock keys clashing between parallel branches | Pin test and `AdvisoryLockKeys` from R0 |
| Draft rules differing between C2, C4, C7 | Explicit allow-list everywhere |

## 6. What this order does not change

No package's scope or user decisions change. The corrections only fix contradictions, missing names and dependencies, and make shared pieces explicit.
