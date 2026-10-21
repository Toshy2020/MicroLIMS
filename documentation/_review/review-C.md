# Phase 11 review — part C (§30–§44, plus document-control header and contents list)

## Method and coverage

Scope: the document-control header and contents list (lines 1–63), and §30–§44 (lines 6855–10496 of `documentation/USER_GUIDE.md`). HTML comments were ignored.

Order of work: GMP-sensitive and permission content first (§30 equipment checks, §31 item, specification and preparation approval, §32.4 signed workbook load and verification, §35–§37 audit trail, signatures and segregation of duties, §38 Document Control approval, review and acknowledgement, §39 users and roles). Then every other sub-section was sampled.

Evidence used:
- Default grants: `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:521-575`, `backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt` and `PermissionConstants.Enforced`.
- Service and controller code for each rule.
- Front-end source for labels.
- Every quoted message or label was checked with `git grep -F`.

Coverage:
- Sub-sections reviewed: 30.1–30.4, 31.1–31.6, 32.1–32.4, 33.1–33.2, 34.1–34.8, 35.1–35.3, 36.1–36.3, 37.1–37.4, 38.1–38.15, 39.1–39.2, 40.1–40.2, 41.1, 42.1–42.9, 43.1–43.4, 44.1–44.6, plus the header and contents.
- Claims sampled: about 190. That is about 150 quoted strings or labels checked with `git grep -F`, plus about 40 behaviour, permission or calculation claims read in code. Examples of the behaviour claims: incubator eligibility, specification evaluator, Count-Tiered comparison, workbook verify and segregation of duties, the segregation-of-duties guard, the audit exclusion list, the signature lockout constants, KPI constants, Document Control approval, review and void rules, training-matrix scoping, report export limits and audit, and retest analyst carry-over.
- Contents list: every anchor was compared with its heading slug. All 44 resolve.
- The header's code commit `f773042c` is an ancestor of `HEAD`.

Not checked:
- Screenshots (placeholders only).
- Runtime behaviour. Nothing was run, per the brief.
- Visual layout claims, for example "forced light theme" and chip colours.
- Most KPI formula details beyond their constants.
- The fields of Change items and Impact assessment (already GAP-043).
- Whether database-level audit protection exists in the live database (the guide already qualifies this).
- §42 messages that belong to other parts' modules: sampled only, not traced to every caller.

## Findings

### F-C01 Incubator rejection message is not "always" the same
- Severity: MEDIUM
- Section: §30.3 (What happens next)
- Problem: The guide says that when an incubator is rejected "the message is always 'The selected incubator's set point is outside this medium's temperature range.'" That generic text is used only by the individual/grouped test-step path. Shared TSB starts and pathogen confirmatory setup (both listed in the same bullet above) return different messages that name the incubator and the medium.
- Evidence: `backend/MicroLIMS.Application/Workflows/TestWorkflow/TestWorkflowSupport.cs:807` (generic text); `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs:150` ("The shared TSB can't start: the selected incubator's set point is outside the {min}-{max} °C range of ..."); `backend/MicroLIMS.Application/Services/PathogenSessionService.cs:558` and `:975` ("Incubator {code}'s set point is outside the {range} range of ...").
- Recommended correction: Replace "always" with "in test steps"; list the shared-TSB and pathogen-confirmation messages too. Keep the point that none of them names an overdue calibration as the cause.

### F-C02 An incubator with no calibration due date passes the calibration check
- Severity: MEDIUM
- Section: §30.1 (System behavior, "Which record do test steps check?"), §30.3 (What happens next)
- Problem: The guide says the check is "that its calibration due date has not passed". The code also accepts an incubator whose configuration record has **no** calibration due date (`CalibrationDueDate == null`). A user would assume a missing date blocks use. This matters for GMP equipment control and is not stated anywhere in §30.
- Evidence: `backend/MicroLIMS.Application/Services/IncubatorEligibilityService.cs` (`GetEligibleIncubatorsAsync`: `(e.CalibrationDueDate == null || e.CalibrationDueDate >= now)`).
- Recommended correction: Add: "An incubator with no calibration due date in its configuration record is accepted." Add a reviewer note or GAP reference.

### F-C03 Production-stage delete dialog quoted as fact, but samples lose their stage
- Severity: HIGH
- Section: §32.2 (What you see, Production Stages delete confirmation; How to use it step 5)
- Problem: The guide quotes the confirmation "Samples already recorded with this stage keep it; only the dropdown changes." without comment, and the procedure tells the user to "click the bin icon and confirm". In fact deleting a stage sets `ProductionStageId` to null on every sample that used it. That changes which stage-specific specifications and replicate counts apply (§31.3, §31.5). The reviewer note in §32.2 (GAP-709) says this, so the section contradicts itself, and a user following the procedure would trust the on-screen text.
- Evidence: `frontend/src/modules/laboratoryConfiguration/masterDataSimple/ProductionStagesCard.tsx:390` (dialog text); `backend/MicroLIMS.Application/Services/MasterData/ReferenceListMasterDataService.cs:143-151` (hard delete, no reference check); `backend/MicroLIMS.Persistence/Configurations/SampleConfiguration.cs:32` (`OnDelete(DeleteBehavior.SetNull)`).
- Recommended correction: In "What you see", mark the dialog text as inaccurate. In step 5, add a warning: "Deleting a stage removes it from every sample already recorded with it. Do not delete a stage that is in use." Keep GAP-709.

### F-C04 "No exemption, two people for every decision" is overstated and contradicts other sections
- Severity: HIGH
- Section: §37.4 ("Absolute Rule: No Administrative Exemption"); also §42.4 intro and §43.3 FAQ 15
- Problem: The guide says "Every analytical review and release decision requires two distinct authorized individuals". It also says that a System Administrator who "standardizes a titrant ... is strictly blocked from reviewing or approving that record". §42.4 says "MicroLIMS does not let the same person perform and then review or approve."
  - Titrant standardization has no review or approval step, and it is not counted as "performing a test" by the segregation-of-duties guard. The titrant example is unsupported.
  - The guide's own §31.2 says the item preparation configuration can be approved by the person who saved it.
  - The guide's own §34.7 says workflow profile versions are signed by their creator with no second person.
  - Solution preparation completion, HPLC system suitability confirmation and ICP calibration confirmation are single-signer actions.
  - Segregation of duties is enforced only for the actions in the §37.4 table. A reader relying on the "absolute" wording would assume controls that do not exist.
- Evidence:
  - `backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs:25-54`: "performed" covers the assigned analyst, preparation, results, count readings, pathogen observations, step results, test analyses and confirmatory observations. Titrant standardization is not included.
  - `backend/MicroLIMS.Application/Services/TitrantStandardizationService.cs:244`: single signature, no review step.
  - `backend/MicroLIMS.Application/Services/ItemPreparationConfigurationService.cs:84-97` (`ApproveAsync`): no check against the creator.
  - `backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:81,117` (GAP-820).
- Recommended correction:
  - Replace the "Absolute Rule" paragraph with: "The rules in the table compare user accounts; System Administrators are not exempt from them. Other signed or approved actions (for example item preparation configuration approval, workflow profile versions, titrant standardization, solution completion) have no second-person check."
  - Remove "standardizes a titrant".
  - In §42.4 and FAQ 15, limit the wording to "for the actions listed in §37.4".

### F-C05 "Select All Matching" exports more than the approved records, and the PDF is silently cut at 200
- Severity: HIGH
- Section: §34.2 (How to use it, step 7; Selection action bar)
- Problem: The guide says "Only records with Approved status can be selected" and tells the user to "Select All {totalCount} Matching Records" before **Export Selected CSV / PDF**. After **Select All**:
  - **Export Selected CSV** runs the normal server export for the current filters. It includes records of every approval status (Pending, Rejected, Voided, Cancelled), not only the approved ones.
  - **Export Selected PDF** fetches only the first 200 matching rows, again of every status, and gives no warning. Its title is "Laboratory Results Export (All Matching)".

  A user who thinks they are exporting approved results only could pass on unapproved or rejected results, or an incomplete list. This matters for GMP reporting.
- Evidence:
  - `frontend/src/modules/reports/components/ReportResultsTable.tsx:141-144` (`selectAllMatchingRecords` only sets a flag).
  - `:173-177` (`handleExportSelectedCsv` calls `runExport()` with `appliedParams`; no approval filter is added).
  - `:213-228` (`handleExportSelectedPdf` uses `searchResults({ ...appliedParams, page: 1, pageSize: 200 })`).
  - The tooltip at `:348` claims "Selects every Approved result matching your current filters".
- Recommended correction: State that after **Select All Matching Records** the export contains every record that matches the filters, whatever its approval status. Tell users to set **Approval Status = Approved** first. Add that the selected-PDF export holds at most 200 records. Raise this as a GAP; the code does not match its own tooltip.

### F-C06 Document compliance drill-down shows every person's status to any user
- Severity: MEDIUM
- Section: §38.10 (Who can use it; How to use it step 4)
- Problem: The guide says users without the *Document Training Assignment* permission "see only their own row". That is true for the grid and the user drill-down. The **Document Compliance Profile** drawer (column header click) is not restricted, though. Any signed-in user can see the qualification status of every assigned person on a document. The step 4 text lists this drawer without any limit.
- Evidence: `backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingMatrixController.cs`. `GetTrainingMatrixGrid` and `GetUserCompliance` check `IsAdminOrSectionHead`. `GetDocumentCompliance(int documentMasterId)` has no check.
- Recommended correction: Add: "The Document Compliance Profile drawer shows every assigned person's status to any user who can open the matrix." Raise a GAP.

### F-C07 "Previous Period" comparison option does nothing
- Severity: MEDIUM
- Section: §34.5 (What you see, **Compare With**)
- Problem: **Previous Period** is listed as a comparison choice with no caveat. Selecting it has no effect. Only Product Comparison and Location Comparison open the comparison dialog, and the chart has no previous-period overlay.
- Evidence: `frontend/src/modules/reports/components/TrendingTab.tsx:305-321`. A code comment says "Previous Period" is out of scope, and only the two comparison values call `setCompareDialogOpen(true)`.
- Recommended correction: Add: "**Previous Period** can be selected but does not change the chart." Raise a GAP.

### F-C08 Trending Category list is described as fixed
- Severity: LOW
- Section: §34.5 (What you see, **Category**)
- Problem: The four categories listed (FinishedProduct, RawMaterial, Water, EnvironmentalMonitoring) are only the built-in fallback, shown when the filter options fail to load. Normally the list comes from the server and can include After Cleaning and other categories. The same page mentions an After Cleaning location cascade.
- Evidence: `frontend/src/modules/reports/components/TrendingTab.tsx:269-281`.
- Recommended correction: "Category: the categories that have results; if the lists cannot be loaded, four default categories are shown." Link this to GAP-800.

### F-C09 Two Quick Reports tiles are not filters
- Severity: LOW
- Section: §34.2 (What you see, Quick Reports)
- Problem: The guide calls all eight tiles "one-click filter tiles". **Media/GPT** and **Reference Strains** switch to their own tabs, and **Custom Report** clears the presets. Only the five category tiles filter the Record Search.
- Evidence: `frontend/src/modules/reports/components/QuickReportsTiles.tsx:12-48`; `frontend/src/modules/reports/RecordSearchTab.tsx:97-102` (`onNavigateTab?.(2)` / `(3)`).
- Recommended correction: Describe the five filter tiles. Say that **Media/GPT** and **Reference Strains** open the matching tabs.

### F-C10 Raw signature meaning code shown to users
- Severity: LOW
- Section: §34.7 (How to use it, step 6)
- Problem: "The system attaches the electronic signature meaning **MasterDataChanged**" is an internal enum name. §32.4 writes "master data changed" for the same meaning, and the §32 review log says meaning codes were removed.
- Evidence: `backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:81`.
- Recommended correction: Write "meaning: master data changed", consistent with §32.4.

### F-C11 "Nine kinds" of unaudited records, eight listed
- Severity: LOW
- Section: §35.1 (Records that are not audited)
- Problem: The text says nine kinds but names eight. "The audit records themselves" covers two record types: audit log entries and audit field-change rows.
- Evidence: `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:310-318`.
- Recommended correction: Write "the audit records and their field-change details", or drop the number.

### F-C12 Glossary "Active Equipment" contradicts §30.1
- Severity: LOW
- Section: §44.5
- Problem: The glossary says the tab lists "in-service assets". §30.1 says it "lists every asset in scope". The code returns all inventory assets in scope, whatever their status.
- Evidence: `backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs:242-246` (no status filter).
- Recommended correction: "A tab of the Equipment Inventory listing every asset in your laboratories with its activities."

### F-C13 Glossary "Source File" access rule is wrong
- Severity: LOW
- Section: §44.6
- Problem: The glossary says the Word source is "accessible only during draft and technical review stages". Access depends on who you are, not on the revision stage:
  - System Administrators, Section Heads and the document owner or assigned author can always open it.
  - A technical reviewer can open it only while their review task is open.
- Evidence: `backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:156-190` (`CanAccessSourceFileAsync`).
- Recommended correction: "The editable Word file. It can be opened by System Administrators, Section Heads, the document owner or assigned author, and by a technical reviewer while their review is open."

### F-C14 Glossary terms that do not match the screens
- Severity: LOW
- Section: §44.2, §44.5
- Problem:
  - "Set Point ... for an incubator, refrigerator, or oven": set points can be edited only for incubators (§30.3), and the equipment types have no refrigerator or oven.
  - "On-Time Reading Rate" is not a screen label. The KPI tile is **Incubation reads on time** (§34.6).
- Evidence: `EquipmentConfigurationService` ("Equipment {code} is not an incubator."); §30.2 type list; `backend/MicroLIMS.Application/Services/Kpi/IncubationReadsOnTimeKpi.cs:18`.
- Recommended correction: Limit Set Point to incubators. Rename the term to **Incubation reads on time**.

### F-C15 Guide asserts "legal" effect of reading acknowledgement in its own voice
- Severity: MEDIUM
- Section: §38.9 (Purpose; System behavior, "Conscious Legal Acknowledgement")
- Problem: Screen text that is quoted is fine; GAP-023 already covers the on-screen regulatory wording. The guide's own prose, however, states "record conscious legal read-and-understand acknowledgements" and "Legal acknowledgement strictly requires ...". That claims a legal or regulatory status the brief does not allow the guide to assert. The acknowledgement is unsigned (no password), as the same section says.
- Evidence: `backend/MicroLIMS.Application/Services/DocumentControl/DocumentAcknowledgementService.cs:127-131` (checkbox check only; no signature service call).
- Recommended correction: Use neutral wording: "record a read-and-understood acknowledgement (checkbox confirmation, no password)". Keep the legal wording only inside quoted screen text.

### F-C16 Document-control header lacks approval and history fields; reviewer notes are in the user text
- Severity: LOW
- Section: Document-control header (lines 3–13)
- Problem: The header has Title, Version, Date, branch, code commit, Author and Status. It has no rows for reviewer, approver, approval date or signature, and no revision history table. The guide also contains many "Note for reviewers" boxes in the body. These suit a draft but must be removed or turned into user-facing warnings before controlled use. The header names DOCUMENTATION_GAPS.md without a path.
- Evidence: `documentation/USER_GUIDE.md:3-13`.
- Recommended correction: Add Reviewed by, Approved by, Effective date and a revision history table. Plan how reviewer notes are resolved before release. Give the path `documentation/DOCUMENTATION_GAPS.md`.

### F-C17 Incubation-reads KPI window described ambiguously
- Severity: LOW
- Section: §34.6 tile 7; §44.2 "On-Time Reading Rate"
- Problem: "Finished within 4 hours of the expected read time" suggests a window of plus or minus 4 hours. In the code, a read is on time when it happens no later than the expected time plus 4 hours, so early reads also count as on time.
- Evidence: `backend/MicroLIMS.Application/Services/Kpi/IncubationReadsOnTimeKpi.cs:18,59`; `KpiTypes.cs:46`.
- Recommended correction: "read no later than 4 hours after the expected read time".

## Summary

Counts by severity: CRITICAL 0, HIGH 3, MEDIUM 5, LOW 9 (17 findings).

- HIGH: F-C03 (production-stage delete), F-C04 (segregation of duties overstated), F-C05 (Select-All export).
- MEDIUM: F-C01, F-C02, F-C06, F-C07, F-C15.
- LOW: F-C08 to F-C14, F-C16, F-C17.

Overall assessment:
- Part C is in good shape on the points that matter most for GMP:
  - Default role grants and the permissions named in §30–§39 match `DbSeeder` and the authorization matrix in every case sampled.
  - Almost every quoted message exists word for word.
  - The audit-trail scope, the signature lockout (5 attempts in 15 minutes), the second-person workbook verification, the Document Control segregation-of-duties and void rules, and the "not implemented" lists (instrument import, ERP, obsolescence, IQ/OQ/PQ) are accurate and properly marked with GAP references.
  - No CRITICAL compliance claims were found. Regulatory wording appears only as quoted screen text with GAP-023 notes.
- The main weaknesses:
  1. Over-general statements about segregation of duties that the guide itself contradicts elsewhere (F-C04).
  2. On-screen text repeated as fact where the code behaves differently (F-C03, F-C05).
  3. A few report features described as working when they do nothing or behave differently (F-C07, F-C09).

  These should be corrected before the guide is used under document control.

