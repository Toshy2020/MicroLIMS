# MicroLIMS User Guide — Documentation Gaps (single gaps file)

| Item | Value |
|---|---|
| Created | Phase 2, repository commit `f773042c`, branch `docs/user-guide` |
| Rule | This is the only gaps file. Do not create a second one. Close a gap only with evidence; never by guessing. |
| Labels | **DISCREPANCY** = specification or documentation differs from the code. **NOT FOUND / NOT IMPLEMENTED** = searched, absent. **REQUIRES CONFIRMATION** = cannot be verified from the repository. **FACT NEEDING INTENT CONFIRMATION** = behaviour is verified, but whether it is the intended laboratory workflow is not; it is NOT a defect claim. |
| Evidence | `[src: ...]` tags are for the documentation team. The guide shows only callouts for confirmation items and discrepancies. |
| Count | 55 gaps; 9 of them are labelled DISCREPANCY (GAP-017 to GAP-024 and GAP-045). |

Entry format: **Question** / **Affected feature** / **Why it matters** / **Needs confirmation** / **Who or what could confirm**.

---

## A. Specifications that are missing

### GAP-001 No URS or FRS for the microbiology core (NOT FOUND)
- **Question:** What are the approved user and functional requirements for sample receiving, test order generation, the testing workspace, review and approval?
- **Affected:** MOD-SAMPLE-001, MOD-TESTING-001, MOD-COUNT-001, MOD-OBS-001, MOD-PATH-001, MOD-REVIEW-001, MOD-APPROVAL-001, MOD-TRACK-001, EM / Water / After Cleaning testing.
- **Why it matters:** The only specification sources for this area are `documents/DesignSpecification/BusinessRules.md` and `WorkflowDiagrams.md` plus design plans. The `documents/URS` and `documents/FunctionalSpecification` folders hold only a README each. Every INTENDED workflow for the core therefore rests on those two files, which are partly out of date (GAP-017 to GAP-021). Document Control has a URS and FRS; physicochemical modules have design and build specifications but no URS [src: knowledge/_raw/spec-sources.md §13].
- **Needs confirmation:** Which document is the approved requirement baseline for the core laboratory workflow, and whether the Section Head / QA accept BusinessRules.md as such.
- **Who or what:** Laboratory owner / QA; the original design document `MicroLIMS_v2.doc` named in `documents/DesignSpecification/README.md` (not in the repository).

---

## B. Mock, placeholder and hardcoded values found in the UI

### GAP-002 Trending tab uses hardcoded fallback options and default criteria
- **Question:** Are the built-in lists and defaults in the Reports "Trending & Analysis" tab intended for production?
- **Affected:** SCR-REPORTS-005 (MOD-REPORTS-001).
- **Facts:** When the server returns no options, the tab shows built-in product, test and category lists, including a hardcoded product name, and one test code is pre-selected on first open. The first-open criteria also pre-fill a hardcoded product name, category "FinishedProduct", a 12-month range and no comparison [src: frontend/src/modules/reports/components/TrendingTab.tsx:L44-L51, L218-L223, L250-L254, L277-L281].
- **Why it matters:** A user could read a trend for a product or test that was never selected on purpose, or see options that are not in the live master data. Figures from these defaults must not be presented as reliable in the guide.
- **Needs confirmation:** Whether the fallbacks are placeholders to be removed, and whether the default criteria are acceptable.
- **Who or what:** Product owner; developer.

### GAP-003 Reports Overview shows a static "Live Database Data" label
- **Question:** Is the label tied to any real data-freshness check?
- **Affected:** SCR-REPORTS-001.
- **Facts:** The chip text is fixed and is not driven by a data flag [src: frontend/src/modules/reports/components/OverviewTab.tsx:L120-L124].
- **Why it matters:** Users may read it as a guarantee about data currency.
- **Needs confirmation:** What the label means and whether it should be shown.
- **Who or what:** Product owner.

### GAP-004 Confirmatory plating end time falls back to 24 hours
- **Question:** Is a 24-hour fallback acceptable when the Test Master gives no maximum incubation time?
- **Affected:** SCR-PATH-001 (confirmatory plating panel).
- **Facts:** The panel sends an end time of 24 hours when the configured maximum is 0; the start time is the browser clock at click time [src: frontend/src/modules/testingWorkspace/pathogenSteps/ConfirmatoryPlatingPanel.tsx:L152-L160]. The same panel warns "Incubation duration is not configured in Test Master." when the value is 0 [src: same file:L287-L297].
- **Why it matters:** A recorded incubation window may not come from approved configuration.
- **Needs confirmation:** Intended behaviour when the window is not configured (block, as other panels do, or fallback).
- **Who or what:** Microbiology Section Head; developer.

### GAP-005 Batch confirmatory plating can submit media id 1
- **Question:** Is the fallback of the literal value 1 for a medium identifier intended?
- **Affected:** Pathogen session workspace, Confirmatory Plating step (SCR-PATH-002).
- **Facts:** The material id is the lot's material id, else the lot id when greater than 0, else the literal 1 [src: frontend/src/modules/testingWorkspace/pathogenSession/BatchConfirmatoryPlatingPanel.tsx:L386-L390].
- **Why it matters:** A wrong medium reference could be recorded without the user noticing.
- **Needs confirmation:** Whether this path can occur in practice and what the server does with it.
- **Who or what:** Developer; Microbiology Section Head.

### GAP-006 Medium count and quantitative-column rules come from name matching
- **Question:** Should "SALMONELLA means two confirmatory media" and "a column is quantitative when its code contains count, TAMC or TYMC" be driven by Test Master configuration?
- **Affected:** SCR-PATH-002 (Primary Matrix and Confirmatory Plating).
- **Facts:** The required confirmatory medium count defaults to 2 when the test code contains "SALMONELLA", else 1 (used when no configured count exists); a quantitative column is detected by matching text in the workflow type or test code [src: frontend/src/modules/testingWorkspace/pathogenSession/BatchConfirmatoryPlatingPanel.tsx:L161; PrimaryObservationMatrixPanel.tsx:L77-L79, L524-L527].
- **Why it matters:** A new or renamed test code could behave differently from its configuration.
- **Needs confirmation:** Whether configuration always supplies these values so the heuristics never apply.
- **Who or what:** Developer; Microbiology Section Head.

### GAP-007 Shared TSB defaults and unfiltered lists
- **Question:** Is it intended that the Shared TSB, Primary Matrix and batch confirmatory setups list incubators and media without set-point or material filters?
- **Affected:** SCR-PATH-002.
- **Facts:** The default TSB lot is chosen by matching "TSB", "Tryptic" or "Broth" in names, else the first lot; incubator lists come from a generic equipment lookup with no temperature filter, unlike other panels [src: frontend/src/modules/testingWorkspace/pathogenSession/SharedTsbEnrichmentPanel.tsx:L46-L66; raw: frontend-testing §6].
- **Why it matters:** Users could select an incubator outside the required temperature range; whether the server refuses it is not established here (the server has an INCUBATOR_TEMP_OUT_OF_RANGE error code [src: backend/MicroLIMS.Shared/Constants/WorkflowErrorCodes.cs]).
- **Needs confirmation:** Server-side enforcement on the session start and confirmatory setup endpoints.
- **Who or what:** Developer (read-only check); Section Head.

### GAP-008 Other display fallbacks and client-clock times
- **Question:** Are placeholder display values acceptable?
- **Affected:** Pathogen session panels, count dialog.
- **Facts:** Fallback texts such as "Standard Lot", "Standard Incubator", "Selective Agar", "GPT Conform", "Standard Culture Media" and "Analyst" appear when the server summary is missing; confirmatory, TSB and incubation start times default to the browser clock; the count dialog unit falls back to "CFU/mL" [src: raw: frontend-testing §10].
- **Why it matters:** A fallback label could be read as recorded data.
- **Needs confirmation:** Whether the server always supplies these values so fallbacks never show.
- **Who or what:** Developer.

---

## C. Signatures, permissions and security facts needing intent confirmation

### GAP-009 Return to Analyst is not signed (FACT NEEDING INTENT CONFIRMATION)
- **Question:** Is a Return to Analyst intended to be performed without an electronic signature?
- **Affected:** WF-REVIEW-002, SCR-REVIEW-002.
- **Facts:** The dialog has an optional reason and no password step; the reviewer's technical review itself is signed [src: frontend/src/components/ReturnToAnalystDialog.tsx:L62-L136; frontend/src/modules/testingWorkspace/SampleSummaryDialog.tsx:L232-L238; knowledge/_raw/verified-roles-permissions.md §6].
- **Why it matters:** The guide must describe the real behaviour without implying a signature.
- **Needs confirmation:** The intended signing rule for returns. This is not a defect claim.
- **Who or what:** QA; Section Head.

### GAP-010 Count result entry and pathogen steps are not signed (FACT NEEDING INTENT CONFIRMATION)
- **Question:** Is entering microbiology plate counts, pathogen observations, session completion and biochemical analyst decisions intended to need no password re-entry?
- **Affected:** WF-COUNT-001, WF-COUNT-002, WF-PATH-001, WF-PATH-002; physicochemical result entry, in contrast, is signed.
- **Facts:** No `SignAsync` caller exists for these actions; the count dialog imports no signature dialog [src: knowledge/_raw/verified-roles-permissions.md §6; raw: frontend-testing §2.1, §11.2].
- **Why it matters:** Users may assume every result entry is signed; it is not for microbiology.
- **Needs confirmation:** Intended signing policy for microbiology result entry.
- **Who or what:** QA; Section Head.

### GAP-011 Ten permission codes are NOT-ENFORCED
- **Question:** Should the guide and the Roles screen treat these codes as having no effect?
- **Affected:** MOD-ROLES-001, MOD-MSG-001, MOD-DISC-001, MOD-DC-001, MOD-DC-002, MOD-SECAUDIT-001.
- **Facts:** `Discussions.View`, `Discussions.Create`, `Messages.Use`, `System.ViewSecurityAudit`, `Documents.DraftEdit`, `Documents.RevisionCreate`, `Documents.Review`, `Documents.Approve`, `Documents.PeriodicReview`, `Documents.TrainingViewMatrix` are referenced nowhere outside the constants file and the seeder; the Roles screen labels them "Legacy-only" [src: knowledge/_raw/verified-roles-permissions.md §1; frontend/src/modules/roles/components/PermissionMatrix.tsx:L70-L83].
- **Why it matters:** An administrator could believe revoking `Messages.Use` or `Documents.Approve` restricts access; it does not.
- **Needs confirmation:** Whether these are to be enforced later (then the guide changes) or removed.
- **Who or what:** Product owner; developer.

### GAP-012 Failed-login counter is not reset when a lock expires
- **Question:** Is it intended that, after the 15-minute lock ends, one wrong password locks the account again?
- **Affected:** WF-AUTH-001, WF-USERS-001.
- **Facts:** The counter is cleared only by a successful login, an administrator unlock, or administrator recovery. After expiry the counter is still at least 5, so one further wrong password gives counter 6 and a new 15-minute lock [src: knowledge/_raw/verified-roles-permissions.md §9; backend/MicroLIMS.Application/Services/AuthenticationService.cs:L20, L22, L106-L137].
- **Why it matters:** Users locked once can be re-locked by a single typing error; the guide's troubleshooting must say so.
- **Needs confirmation:** Intended lockout behaviour.
- **Who or what:** Product owner; QA.

### GAP-013 Media product code change allows Section Head only
- **Question:** Is it intended that a System Administrator cannot change a media product code?
- **Affected:** WF-MEDIACFG-002, SCR-MEDIACFG-001.
- **Facts:** `PUT api/masterdata/media-products/{id}/code` is `[Authorize(Roles = SectionHead)]`, so a user of role type System Administrator is refused; it also needs password and reason. Permission edits do not change this check; custom roles follow their base type [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L52-L56; verified §3].
- **Why it matters:** The role/permission table in the guide must show an exception to "permissions decide everything".
- **Needs confirmation:** Whether the restriction is deliberate.
- **Who or what:** Product owner.

### GAP-014 Final Release as a separate step or global permission is not found (NOT FOUND)
- **Question:** Does the intended process include a "Final Release" step or permission distinct from the Section Head approval decision?
- **Affected:** MOD-APPROVAL-001, MOD-COA-001; guide section 19.
- **Facts:** None of the 50 permission codes is named Final Release; the menu and routes have no such item. The approval card is titled "Submit Release Decision"; the Certificate of Analysis becomes available once the sample is approved or rejected [src: raw: frontend-testing §7.2; frontend-core §5.2; backend/MicroLIMS.Shared/Constants/PermissionConstants.cs:L109-L128].
- **Why it matters:** No user procedure for "Final Release" will be written; the guide will document the approval decision and the certificate.
- **Needs confirmation:** Whether any additional release step is intended.
- **Who or what:** QA; laboratory owner.

### GAP-015 Production role grants may differ from the defaults
- **Question:** What do the Roles screen and the database hold in the target system?
- **Affected:** MOD-ROLES-001 and every "who can use it" statement.
- **Facts:** The seeder inserts role grants only when no grants exist; migrations only add grants; administrators can change any role at any time. Code analysis cannot see edits made on the Roles screen [src: verified §2.1, §3, §10].
- **Why it matters:** Role tables in the guide are defaults and are labelled as such.
- **Needs confirmation:** A read-only query of roles and role grants on the target system, run by an authorised person (not done in this project; no database was contacted).
- **Who or what:** System administrator.

### GAP-016 IsEnforced flags on production permission rows
- **Question:** Do all 40 enforced codes carry the enforced flag in the target database?
- **Affected:** Roles screen chips "Enforced" / "Legacy-only".
- **Facts:** The flag is written only when a permission row is first inserted or by a migration update; later changes to the constant set do not update existing rows [src: verified §1].
- **Why it matters:** The Roles screen could label an enforced code "Legacy-only" on an older database.
- **Needs confirmation:** Read-only check of the permission rows.
- **Who or what:** System administrator.

---

## D. Specification-versus-code discrepancies

### GAP-017 DISCREPANCY: approval decisions in BusinessRules.md versus the code
- **Question:** Are the decisions `Investigation` and `OOSInvestigation` still required?
- **Affected:** MOD-APPROVAL-001, MOD-OOS-001.
- **Facts:** BusinessRules.md section 9 lists six decisions, two of which require a non-blank comment [src: documents/DesignSpecification/BusinessRules.md:L171-L184]. The enum still has six values, but the sample approval service states that only four are reachable: "Investigation/OOSInvestigation are not sample-approval decisions" [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L17-L19; backend/MicroLIMS.Domain/Enums/ApprovalDecision.cs]. The UI offers four options: Approve, Not Conform (Final Conclusion), New Sample Required, Retest Retained Sample [src: frontend/src/modules/testingWorkspace/sampleSummary/summaryFormatting.ts:L73-L78]. `Investigation` is used only as an event decision value in the pathogen confirmation recorder [src: backend/MicroLIMS.Application/Workflows/TestWorkflow/PathogenConfirmationRecorder.cs:L834].
- **Why it matters:** The guide must not describe investigation approval decisions.
- **Needs confirmation:** Whether the specification is superseded by OOS Tracking.
- **Who or what:** QA; laboratory owner.

### GAP-018 DISCREPANCY: review and approval design in BusinessRules.md versus the code
- **Question:** Which description of review and approval is current?
- **Affected:** MOD-REVIEW-001, MOD-APPROVAL-001.
- **Facts:** The specification describes review per test order in `ReviewService.cs` (with a "quick table review" batch mode) and approval per test order in `ApprovalService.cs`, with test statuses ResultEntered, Reviewed [src: documents/DesignSpecification/BusinessRules.md:L157-L186]. The repository has `SampleReviewService.cs` and `SampleApprovalService.cs` working per laboratory section on the sample, with electronic signatures; no `ReviewService.cs`, `ApprovalService.cs` or quick-batch review method exists [src: git ls-files; git grep QuickReview returned no match]. The Return to Analyst function applies to count tests only.
- **Why it matters:** The INTENDED text for review and approval cannot be taken from BusinessRules.md without correction.
- **Needs confirmation:** That the section-level, signed design is the intended one; whether a quick batch review is wanted.
- **Who or what:** QA; Section Head.

### GAP-019 DISCREPANCY: pathogen engine description versus the code
- **Question:** Which pathogen workflow is intended?
- **Affected:** MOD-PATH-001; guide section 15.
- **Facts:** The specification and diagrams describe a universal chain (TSB, observation, detection medium) and a Salmonella chain TSB, RVS, XLD+TSI recorded in strict order, closing early on a negative at TSB or RVS, interpreted by a `PathogenWorkflowEngine` [src: documents/DesignSpecification/BusinessRules.md:L52-L79; WorkflowDiagrams.md:L20-L45]. The code has a configurable five-stage step chain (BrothEnrichment, SelectiveBroth, SelectivePlating, ConfirmatoryPlating, BiochemicalTest) driven by Test Master, a shared-TSB session workspace, and agreement logic across confirmatory media; no `PathogenWorkflowEngine.cs` exists, and RVS / XLD occur only as example medium names in comments [src: backend/MicroLIMS.Domain/Enums/StepType.cs; raw: frontend-testing §4; git grep RVS].
- **Why it matters:** The guide will describe the implemented chain; the specification cannot be cited for it.
- **Needs confirmation:** That the step chain from Test Master is the approved process, including early closure rules.
- **Who or what:** Microbiology Section Head; QA.

### GAP-020 DISCREPANCY: EM two-step incubation and out-of-trend rule versus the code
- **Question:** Is the automatic out-of-trend (OOT) calculation still required?
- **Affected:** MOD-EM-001; guide section 21.
- **Facts:** The specification states OOT is computed when step 2 completes as (Step 1 count + Step 2 count) greater than the action limit and persisted [src: documents/DesignSpecification/BusinessRules.md:L102-L116]. The code keeps an `IsOutOfTrend` field on the room monitoring entity and its DTO, but no code that computes it was found outside those two declarations; EM tests now run through the shared test workflow ("Advance to Next Incubation Window") [src: backend/MicroLIMS.Domain/Entities/RoomMonitoring.cs:L15; backend/MicroLIMS.Application/DTOs/EMResultDto.cs:L10; raw: frontend-testing §2.1].
- **Why it matters:** The guide must not promise automatic out-of-trend detection.
- **Needs confirmation:** Whether OOT detection exists elsewhere or was dropped.
- **Who or what:** Developer; Microbiology Section Head.

### GAP-021 DISCREPANCY: GPT engine and stages versus the code
- **Question:** Is the Preparation, Sterility, Recovery, Release flow still the intended GPT process?
- **Affected:** MOD-MEDIA-001; guide section 20.
- **Facts:** The specification describes a `GptWorkflowEngine` advancing `GptStage` strictly forward and releasing only when all challenge organisms pass [src: documents/DesignSpecification/BusinessRules.md:L136-L153]. The code has a `MediaEvaluationEngine` "carried over unchanged from the old GptWorkflowEngine"; the `GptStage` enum is declared and used nowhere else; evaluations are typed GrowthPromotion, IndicationInhibition, EnrichmentCharacteristics, with release as a separate signed decision [src: backend/MicroLIMS.Application/Workflows/MediaEvaluationEngine.cs:L23; backend/MicroLIMS.Domain/Enums/GptStage.cs; EvaluationType.cs].
- **Why it matters:** Media status and release rules in the guide must come from the implemented flow.
- **Needs confirmation:** The approved GPT acceptance and release rules.
- **Who or what:** Microbiology Section Head; QA.

### GAP-022 DISCREPANCY: sterility mentioned in the UI but no sterility test found
- **Question:** Is a sterility check intended as part of media evaluation?
- **Affected:** MOD-MEDIA-001.
- **Facts:** The page subtitle and menu description mention "GPT, Sterility, Indication/Inhibition, Enrichment"; the evaluation type enum has no sterility value and no sterility result field or entity was found; `GptStage.Sterility` is unused [src: frontend/src/modules/laboratoryConfiguration/media/MediaPage.tsx:L217; frontend/src/routes/menuConfig.ts:L136; backend/MicroLIMS.Domain/Enums/EvaluationType.cs; git grep sterility].
- **Why it matters:** Users reading the screen text may expect a sterility record that the system does not create.
- **Needs confirmation:** Whether sterility is meant to be recorded in MicroLIMS and where.
- **Who or what:** Microbiology Section Head; product owner.

### GAP-023 DISCREPANCY: sign-in page compliance claim has no validation evidence in the repository
- **Question:** Is the compliance statement on the sign-in page supported by approved validation documentation?
- **Affected:** SCR-AUTH-001, SCR-DC-006 (Document approval signature dialog); all guide sections.
- **Facts:** The sign-in footer reads "MicroLIMS v0.1 · FDA 21 CFR Part 11 & EU Annex 11 Compliant"; the document-approval signature dialog cites 21 CFR 11.200 [src: frontend/src/pages/Login.tsx:L204; frontend/src/modules/documentControl/components/ElectronicSignatureDialog.tsx:L202]. The repository's validation documents are drafts (`docs/validation` Version 0.1 Draft) and Release 1a to 1d records for Document Control only [src: knowledge/_raw/spec-sources.md §6].
- **Why it matters:** The guide must not repeat these claims; the project rule is to describe functions objectively.
- **Needs confirmation:** Whether the claim stays in the product.
- **Who or what:** QA; regulatory owner.

### GAP-024 DISCREPANCY: older Document Control release records say features are not implemented that the code contains
- **Question:** Which release records describe the current state of Document Control?
- **Affected:** MOD-DC-001, MOD-DC-002.
- **Facts:** Records state e-signatures, automatic effective-date activation, the periodic review scheduler, and training / reading lists as NOT IMPLEMENTED (release 1b) and Release 1c (Training Matrix and Reading Lists) as NOT IMPLEMENTED [src: docs/Release_1b_WP3_Completion_Report.md:L168-L171; docs/Release_1b_Production_Closure_Record.md:L121]. The current code contains the Training Matrix, My Reading List, acknowledgement, periodic review, an effective-date worker and an approval signature [src: frontend/src/routes/AppRoutes.tsx:L246-L252; backend/MicroLIMS.API/BackgroundServices/DocumentEffectiveDateWorker.cs]. Release 1d records mark parts as PLANNED [src: docs/Release_1d_RTM_Delta.md:L23]. `DocumentControl_Integration_Report.md:251` also states a per-record policy is NOT IMPLEMENTED.
- **Why it matters:** The documents are point-in-time records; the INTENDED scope for each Document Control feature must come from the latest approved FRS and RTM.
- **Needs confirmation:** The current approved release baseline and which items remain out of scope (for example KAF / quizzes: status `KafPending` exists in the training status enum but no UI for it was found).
- **Who or what:** Document Control owner; QA.

### GAP-045 DISCREPANCY: KPI remodel specification says "draft, no code written" but the KPI page exists
- **Question:** Is the specification status stale?
- **Affected:** MOD-KPI-001.
- **Facts:** The design file states "Status: draft for review (no code written)" [src: docs/superpowers/specs/2026-10-11-kpi-performance-remodel-design.md:L3]; the code has a KPI tab, workload table, workflow profiles, KPI permission codes, and recent commits refactoring the KPI endpoints [src: raw: frontend-core §6; git log].
- **Why it matters:** The guide cannot treat the draft design as approved intent for KPI definitions.
- **Needs confirmation:** The approved KPI definitions (on-time, TAT, workload weights) and their owner.
- **Who or what:** Laboratory owner; developer.

---

## E. Other gaps in document control, workflows and screens

### GAP-025 Document Control: server-side gating of some pages and the configuration check
- **Question:** Who may open the Training Matrix and Training Compliance pages, and is the configuration check consistent?
- **Affected:** MOD-DC-002, MOD-DC-004, MOD-DC-003.
- **Facts:** The pages have no route guard and no menu permission; `Documents.TrainingViewMatrix` is NOT-ENFORCED. The Document Control configuration route needs `Documents.ConfigManage`, but the service-level check requires role type System Administrator, so a custom role holding the permission could pass the route but fail the service [src: frontend/src/routes/AppRoutes.tsx:L248-L249, L268-L269; backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L229-L233]. Per-service role lists in the review, approval, file, revision and periodic review services were not individually re-read in verification [src: verified §10].
- **Why it matters:** "Who can use it" for these screens is not fully established.
- **Needs confirmation:** Service-level rules per Document Control action.
- **Who or what:** Developer; Document Control owner.

### GAP-026 Segregation of duties for HPLC / ICP run review not found
- **Question:** Is there a rule that the person who enters HPLC or ICP results differs from the reviewer?
- **Affected:** MOD-HPLC-001, MOD-ICP-001, MOD-GC-001.
- **Facts:** The segregation guard is used by sample review, sample approval, pathogen biochemical decision, media release, cryovial approval and working standards; the sample-level review rule is therefore the only separation found for run results, once they reach sample review (REQUIRES CONFIRMATION for physicochemical samples) [src: verified §5].
- **Why it matters:** The guide must state only rules that exist at the sample review step.
- **Needs confirmation:** Whether additional run-level separation is intended.
- **Who or what:** QA; physicochemical Section Head.

### GAP-027 Per-action signature comment and reason requirements are not uniform
- **Question:** Which signed actions require a non-empty reason or comment?
- **Affected:** MOD-SIG-001 and all signed workflows.
- **Facts:** The comment is a free-text field per endpoint; many require a reason (corrections, voids, code change, add laboratory), but not all [src: verified §6].
- **Why it matters:** Procedures must state which fields are mandatory.
- **Needs confirmation:** Per action, in Phase 4.
- **Who or what:** Documentation team (read each endpoint).

### GAP-028 Microbiology count and pathogen workflows never prompt for a password (verification note)
- **Question:** Confirm no hidden signature step exists.
- **Facts:** A code search for signature callers shows none for these workflows; `PersistTestAnalysisAndFinalize` is used by physicochemical recorders only [src: verified §6].
- **Why it matters:** The guide's statement "microbiology result entry is not signed" depends on this negative search.
- **Needs confirmation:** A runtime check in a test environment.
- **Who or what:** QA tester using the running application.

### GAP-029 Biochemical reviewer decision has no UI caller
- **Question:** Is the reviewer decision on a biochemical confirmation meant to be reachable?
- **Affected:** MOD-PATH-001, MOD-REVIEW-001.
- **Facts:** The endpoint `POST /test-workflow/results/{id}/biochemical-decision` exists and is guarded by `TestWorkflow.BiochemicalDecision` (default Section Head, Reviewer, System Administrator) with a segregation-of-duties rule; the frontend service states "Reviewer-only. No frontend UI calls this yet." [src: frontend/src/modules/testingWorkspace/services/TestWorkflowService.ts:L268-L270; backend/MicroLIMS.Application/Workflows/TestWorkflow/PathogenConfirmationRecorder.cs:L780-L782]. The analyst decision step ("Conclude as Detected" / "Proceed to Biochemical Confirmation") is in the UI.
- **Why it matters:** The permission appears in the Roles screen but no screen uses it.
- **Needs confirmation:** Whether a reviewer decision screen is planned.
- **Who or what:** Product owner.

### GAP-030 Backend-only capabilities with no screen
- **Question:** Are these capabilities intended to stay hidden?
- **Affected:** MOD-AUDIT-002, MOD-SECAUDIT-001, MOD-SIG-002, MOD-REPORTS-002, MOD-KPI-002, MOD-DC-005, MOD-RESULT-001.
- **Facts:** No frontend caller was found for: login history (`GET /api/admin/audit/login-history`), the signature trail (`GET /api/signatures`, `Signatures.Manage`), the reporting backfill (`POST /api/admin/reporting/backfill`, `Reporting.Admin`), analyst availability and KPI section settings (service functions exist, no screen), document escalations (`/api/document-control/escalations/*`), and the results list (`GET /api/results`) [src: knowledge/_raw/backend-endpoints.md; git grep over frontend/src]. Security audit events are recorded but have no viewer.
- **Why it matters:** The guide will not describe screens that do not exist; auditors may expect these records to be viewable.
- **Needs confirmation:** Whether any is intended for a future screen, or accessed by other tools.
- **Who or what:** Product owner; system administrator.

### GAP-031 Instrument report reading code exists without a caller
- **Question:** Is automatic reading of instrument reports (OCR and report parsers) intended to be offered?
- **Affected:** MOD-INSTIMPORT-001, MOD-HPLC-001, MOD-ICP-001.
- **Facts:** Parsers for Empower-style and ICP reports and a Tesseract OCR reader exist; only the text reader is registered; no controller or screen calls them. HPLC / ICP workspaces accept uploaded report files as evidence, and results are entered by hand. No live feed from instruments or CDS was found [src: backend/MicroLIMS.Application/Services/InstrumentImport/; backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs:L299].
- **Why it matters:** The guide's Integrations section must state that instrument import is not available.
- **Needs confirmation:** Status of this feature (planned or abandoned).
- **Who or what:** Product owner.

### GAP-032 SAP / ERP integration not found (NOT FOUND)
- **Question:** Is any ERP integration planned or required?
- **Facts:** Searched backend and frontend source for SAP and ERP as whole words: no match.
- **Why it matters:** No integration section will be written.
- **Needs confirmation:** Absence is expected.
- **Who or what:** Product owner.

### GAP-033 Local AI assistant not found (NOT FOUND)
- **Question:** Is an assistant feature planned?
- **Facts:** Searched backend and frontend source for common AI/LLM terms: no match.
- **Why it matters:** No section will be written.
- **Needs confirmation:** Absence is expected.
- **Who or what:** Product owner.

### GAP-034 Routed pages with no link: Specifications and Media Evaluation
- **Question:** Are the stand-alone Specifications page and Media Evaluation page still in use?
- **Affected:** MOD-SPEC-001, MOD-MEDIA-001.
- **Facts:** `/laboratory-configuration/specifications` and `/laboratory-configuration/media-evaluation` are routed with guards (`MasterData.Manage`, `TestWorkflow.Execute`) but no menu item or in-app link was found; item specifications are managed in Items, and media evaluations are shown in the media lot workspace [src: frontend/src/routes/AppRoutes.tsx:L160-L161, L229; git grep for the paths].
- **Why it matters:** A user could reach them only by typing the address; the content of the Specifications page was not read (title, tabs and buttons UNCLEAR).
- **Needs confirmation:** Whether to document them, treat them as legacy, or remove them.
- **Who or what:** Product owner; developer.

### GAP-035 Frontend routes with no permission guard
- **Question:** Does the server enforce access on each unguarded page's data calls?
- **Affected:** HPLC, GC and ICP workspaces, Working Standards, Document Control pages, Reports, Messages, Discussions, sample report, Certificate of Analysis, media and cryovial reports.
- **Facts:** These routes have no `PermissionRoute`. Write actions in HPLC / ICP / Working Standards use `Hplc.Operate` / `WorkingStandards.Qualify` (enforced on the server). Read access, report pages and Document Control are governed by authentication and, where present, laboratory scope [src: frontend/src/routes/AppRoutes.tsx; raw: frontend-config "Routes in scope that have NO PermissionRoute"].
- **Why it matters:** "Who can use it" for these screens is partly unknown.
- **Needs confirmation:** Server-side read rules per page.
- **Who or what:** Developer (read-only review).

### GAP-036 EM annual plan, EM schedules, water frequencies and monitoring plans not found (NOT FOUND)
- **Question:** Are monitoring plans, schedules and sampling frequencies intended features?
- **Affected:** MOD-EM-001, MOD-WATER-001, MOD-AC-001.
- **Facts:** Entities exist for rooms, departments, sampling points and per-location test limits; no entity or screen for annual plans, schedules or frequencies was found (searched the entity list and menu) [src: backend/MicroLIMS.Domain/Entities; frontend/src/routes/menuConfig.ts].
- **Why it matters:** The guide will describe sampling as on-demand receiving and preparation only.
- **Needs confirmation:** Whether scheduling is done outside MicroLIMS.
- **Who or what:** Microbiology Section Head.

### GAP-037 Reference strain passage, cryopreservation and expiry handling not fully established
- **Question:** Is passage tracking required, and how is cryovial expiry derived?
- **Affected:** MOD-CRYO-001.
- **Facts:** Cryovials have storage location moves, thaw counts, destroy, approval and an expiry date; no passage-number field was found (the word occurs in one comment). How the expiry date is set (manual or calculated) was not read [src: backend/MicroLIMS.Domain/Entities/Cryovial.cs:L44; backend/MicroLIMS.Domain/Entities/MediaEvaluation.cs:L44].
- **Why it matters:** The reference strain section may omit a traceability element users expect.
- **Needs confirmation:** Passage requirements and expiry rule.
- **Who or what:** Microbiology Section Head.

### GAP-038 Equipment qualification records not found (NOT FOUND)
- **Question:** Is equipment qualification (IQ / OQ / PQ) intended in MicroLIMS?
- **Affected:** MOD-EQUIP-001, MOD-EQUIP-002.
- **Facts:** Equipment inventory shows "Calibration Due" and calibration certificate documents with supersede / void; operational status history exists; no qualification record type was found [src: raw: frontend-config §2.2; backend/MicroLIMS.Domain/Enums/EquipmentDocumentType.cs].
- **Why it matters:** The Equipment section will not describe qualification.
- **Needs confirmation:** Where qualification evidence is kept.
- **Who or what:** QA; Section Head.

### GAP-039 Minor inconsistencies between code comments and behaviour (not counted as discrepancies)
- **Question:** Which is correct: the code comment or the behaviour?
- **Facts:** (a) The kanban view comment says a card click opens the Sample Summary dialog, but the page only selects the sample [src: frontend/src/modules/testingWorkspace/SampleKanbanView.tsx:L18-L19; ReceivingTestingWorkspacePage.tsx:L1013-L1019]. (b) The idle-timeout warning comment says only the button cancels it, but mouse, key, click and scroll activity also cancel it [src: verified §8]. (c) The Primary Matrix auto-advance compares counts captured at render [src: raw: frontend-testing §4.10].
- **Why it matters:** Guide steps must follow the behaviour, not the comments.
- **Needs confirmation:** Check in a running application.
- **Who or what:** QA tester.

### GAP-040 The application was not run
- **Question:** Do the screens behave as the code suggests?
- **Facts:** Phase 1 and Phase 2 used code only. Screen behaviour statements are lower confidence.
- **Why it matters:** Phase 9 screenshots and procedures need a running test instance with fake data.
- **Needs confirmation:** Permission and a test environment (local, test database, no production credentials).
- **Who or what:** User.

### GAP-041 Existing draft Master Data Workbook user guide
- **Question:** Should the guide reuse or link to the existing draft?
- **Affected:** MOD-WORKBOOK-001.
- **Facts:** `docs/validation/ML-SOP-MDW-001_Master_Data_Workbook_User_Guide.md` (Draft v0.1, 308 lines) is the only end-user guide in the repository [src: knowledge/_raw/spec-sources.md §11]. It was not compared with the code in Phase 2.
- **Why it matters:** Two documents could contradict each other.
- **Needs confirmation:** Whether the draft stays authoritative for the workbook.
- **Who or what:** Document owner.

### GAP-042 Equation Types page: editable or read-only?
- **Question:** Can users create or change equation types?
- **Facts:** Only a list call was found for the page; no create / update / delete calls were located. The equation types are a fixed enumeration in the backend [src: raw: frontend-config §1.10; backend-enums.md EquationType].
- **Why it matters:** The guide must not describe edit steps that do not exist.
- **Needs confirmation:** Read the page source in Phase 3.
- **Who or what:** Documentation team.

### GAP-043 Areas read only partially in Phase 1 (to be completed in Phase 3)
- **Question:** n/a (work list).
- **Facts:** Not enumerated or read by pattern search only: Analyst dashboard tile labels; register table, KPI cards and selection bar in receiving; Specifications page; Media Lot, Cryovial, Material and Equipment filter option values and field lists of most dialogs; Document Control dashboard and Compliance dashboard content; audit diff and traceability chain labels; Media / GPT and Reference Strains report labels; CoA body; Laboratory Sections dialog; OOS document "void" control (service method exists, UI not located); physicochemical result panel details [src: raw: frontend-core §18; frontend-config "Could not read / not enumerated"; frontend-testing §12].
- **Why it matters:** Guide procedures for these screens cannot be written until read.
- **Needs confirmation:** Complete in Phase 3 before writing.
- **Who or what:** Documentation team.

### GAP-044 Physicochemical result panels read by pattern search only
- **Question:** n/a (work list).
- **Facts:** Measurement, Gravimetric, Qualitative, Titration, Dissolution, Disintegration and Weight Variation panels were read by label search, not fully [src: raw: frontend-testing §12].
- **Why it matters:** Calculation and validation rules for these panels must be read from source before documentation.
- **Needs confirmation:** Full read in Phase 3/4.
- **Who or what:** Documentation team (Sonnet / Opus pass).

### GAP-046 Laboratory incident, deviation or CAPA module not found (NOT FOUND)
- **Question:** Is a quality incident / deviation workflow expected?
- **Affected:** MOD-ERR-001.
- **Facts:** "Error Monitoring" shows technical incidents and states "this is not part of the GxP record". No deviation, incident-report or CAPA module was found [src: frontend/src/modules/errorMonitoring/ErrorMonitoringPage.tsx:L60-L61].
- **Why it matters:** Users must not look in Error Monitoring for quality incidents. The guide will explain this.
- **Needs confirmation:** Where deviations are recorded.
- **Who or what:** QA.

### GAP-047 E-mail and notification delivery depends on configuration
- **Question:** Is SMTP configured in the target system, and are critical alerts enabled?
- **Affected:** MOD-NOTIF-001, MOD-AUTH-001 (password reset by e-mail), MOD-ERR-001.
- **Facts:** An empty SMTP host disables e-mail silently; critical incident alerts are off unless enabled in configuration [src: backend/MicroLIMS.Infrastructure/Email/SmtpOptions.cs:L9-L20; backend/MicroLIMS.API/BackgroundServices/CriticalAlertWorker.cs:L29-L33]. User reset e-mails require an address on file.
- **Why it matters:** Password reset by e-mail and alert e-mails may not work in a given deployment; the in-app bell and administrator-assisted recovery still work.
- **Needs confirmation:** Deployment configuration (no secrets to be copied into the documentation).
- **Who or what:** System administrator.

### GAP-048 Document Control file integrity features not examined
- **Question:** Does the system verify the integrity of stored document files (for example by checksum)?
- **Affected:** MOD-DC-001.
- **Facts:** Not searched in Phase 1. The prompt lists file integrity as a Document Control topic.
- **Why it matters:** The guide must not claim integrity checks that do not exist.
- **Needs confirmation:** Code search in Phase 3.
- **Who or what:** Documentation team.

### GAP-049 Printed record theme and print behaviour
- **Question:** Do printed records force a light theme?
- **Facts:** Print styles (`@media print`) exist for the sample report, Certificate of Analysis and report exports; whether the light theme is forced regardless of dark mode was not verified. The printed sample report footer says "Any printed copy is uncontrolled." [src: frontend/src/modules/testingWorkspace/reportStyles.ts:L354, L511; SampleReportPage.tsx:L307].
- **Why it matters:** Users printing in dark mode may get different output.
- **Needs confirmation:** Test print in dark mode.
- **Who or what:** QA tester.

### GAP-050 Reception and QA roles; custom roles in use
- **Question:** Do the laboratory's Reception and QA staff have custom roles, and which?
- **Affected:** MOD-ROLES-001; guide section 4.
- **Facts:** Only four roles are seeded (System Administrator, Section Head, Reviewer, Analyst). No Reception or QA role exists in code. Document Control comments call the Section Head the "Document Controller" [src: backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:L23-L32].
- **Why it matters:** The guide's role chapter cannot invent roles.
- **Needs confirmation:** Roles actually used in the target system (see GAP-015).
- **Who or what:** System administrator.

### GAP-051 Titration terminology and a separate Titration Methods master
- **Question:** Is there meant to be a Titration Methods master separate from Test Master?
- **Affected:** MOD-TITR-001.
- **Facts:** No such menu item exists; titration settings are part of Test Master and the Titration panel; titrant standardization sits in Solution Preparation [src: frontend/src/routes/menuConfig.ts:L189-L207; raw: frontend-config §1.26].
- **Why it matters:** The prompt's checklist mentions Titration Methods; the guide will describe the implemented layout.
- **Needs confirmation:** None needed unless a separate master is intended.
- **Who or what:** Physicochemical Section Head.

### GAP-052 Export formats and SLA / overdue definitions
- **Question:** What are the exact definitions of overdue, on-time and turnaround used in dashboards and KPIs?
- **Affected:** MOD-DASH-001, MOD-KPI-001, MOD-REPORTS-001.
- **Facts:** Dashboards use ">24h" wording; KPI definitions come from the backend `/kpi/definitions` and a workflow-profile table with "Target (days)"; the record CSV export carries a 10,000-row cap [src: raw: frontend-core §2, §6].
- **Why it matters:** The guide may only quote definitions found in approved sources.
- **Needs confirmation:** Approved KPI definitions (see GAP-045).
- **Who or what:** Laboratory owner.

### GAP-053 Session handling details
- **Question:** Is a client-side idle timeout without a server idle timeout acceptable?
- **Facts:** The browser signs the user out after 20 minutes idle plus a 60-second warning; the server has no idle timeout, only token lifetimes (access 15 minutes, refresh 7 days with rotation) [src: knowledge/_raw/verified-roles-permissions.md §8; frontend/src/hooks/useIdleTimeout.ts:L6-L8].
- **Why it matters:** The guide will state the browser timeout and not promise server-side session termination.
- **Needs confirmation:** Intended session policy.
- **Who or what:** QA; IT security.

### GAP-054 Retired enumeration values and legacy items still present in code
- **Question:** Which retired values must be hidden from the guide?
- **Facts:** The workflow type and equation type enumerations still contain values for retired paths (for example `HplcAssay`, `ElementalAssay`, `HplcMultiAnalyte`, `StandardComparison`, `CalibrationCurve`); specs say several are retired. Signature meanings for retired calibration runs remain [src: backend/MicroLIMS.Domain/Enums/WorkflowType.cs; EquationType.cs; docs/FP_Standard_Comparison_Assay_Build_Spec.md:L11, L19].
- **Why it matters:** These values may appear in old records or reports.
- **Needs confirmation:** Which are retired versus still in use.
- **Who or what:** Developer; physicochemical Section Head.

### GAP-055 Dashboard variant follows role type while data access follows permissions
- **Question:** What does a user see if a role's dashboard permission is removed?
- **Affected:** MOD-DASH-001.
- **Facts:** The dashboard page is chosen by role type (Analyst, Reviewer, System Administrator, otherwise Section Head), while the Section Head and Reviewer dashboard data calls require `Dashboards.LabOverview` / `Dashboards.Review` [src: frontend/src/modules/dashboard/DashboardPage.tsx:L40-L44; knowledge/_raw/backend-permissions-roles-auth.md §1]. Which endpoint needs which code was not individually verified.
- **Why it matters:** A custom role without the code may see a dashboard that fails to load ("Dashboard could not be loaded").
- **Needs confirmation:** Check endpoint policies in Phase 3.
- **Who or what:** Documentation team (code read).
