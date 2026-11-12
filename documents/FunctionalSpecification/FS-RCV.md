# FS-RCV Functional Specification

| Field | Value |
|---|---|
| Document number | FS-RCV |
| Title | Sample receiving functional specification |
| Version | 0.1 |
| Status | Draft |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |

## Scope and reading notes

- This document covers `SampleController` (route `api/samples`) and the services behind it: receipt of product, raw material and packaging material samples, laboratory targeting, the reference number, periodic-retest test selection, adding a laboratory, correction, voiding and analyst assignment. Receipt of water, environmental monitoring and after-cleaning samples has its own controllers and is in FS-WAT, FS-ENV and FS-ACL. Review and approval are in FS-REV.
- Every statement was written from the code files named in each entry. Nothing was run. Pharmacopoeia chapters are pointers only (to be confirmed by QA).
- Which tests an item has, minimum sample quantities, grades and production stages are configured by the Section Head. None of them is in the code.
- `SampleCorrectionService.CorrectCoreAsync` calls `RecordVersion.EnsureCurrent` (the stale-form check). No other method in this document does. Where a method does not, the entry says "no stale-form version check". Overlapping saves of one row are still refused by the automatic `xmin` concurrency token on entities that carry a version column.
- `ProductWorkflowEngine.ReceiveAsync`, `ReceivingService.ReceiveSampleAsync`, `ReceiptLabGuard.ResolveTargetsAsync` and `SampleAssignmentService` write no explicit audit row for receipt. Whether the context's automatic audit capture covers the `Sample` and `TestOrder` rows was not checked in this task. `SampleAssignmentService` does write explicit `AuditLog` rows.

## Functions

### FS-RCV-001
**Satisfies:** URS-RCV-001, URS-RCV-004
**Function:** Receive an item-based sample (`POST api/samples`). Permission: `Samples.ReceiveOwnLab` (policy on the endpoint). Inputs: item ID, cause of testing ID, sample quantity, sampled by, batch number, control number, manufacturing date, expiry date, production stage, target laboratory (section) IDs, grade. Steps: (1) `ReceiptLabService.ResolveTargetsAsync` calls `ReceiptLabGuard.ResolveTargetsAsync` (FS-RCV-002); (2) `ReceivingService.ReceiveSampleAsync` runs `ReceiveSampleValidator`, which refuses when the item ID is not above 0, the cause of testing ID is not above 0, or batch number, control number or sampled by is blank; all messages are joined into one `BusinessRuleException`; (3) the sample is created by `ProductWorkflowEngine.ReceiveAsync` (FS-RCV-004). The received-by user is the caller, taken from the token. The validator does not check that the expiry date is not before the manufacturing date; that check exists only in the correction (FS-RCV-009). The engine does not read the cause of testing row except for its `RequiresTestSelection` flag, so an unknown cause ID is not refused by a message in the engine. The method uses no `UnitOfWork` transaction and does one `SaveChangesAsync`. The request carries no record version, so there is no stale-form version check.
**Code:** `backend/MicroLIMS.API/Controllers/SampleController.cs` — `Receive`
**Verified by:** reading (not run)

### FS-RCV-002
**Satisfies:** URS-RCV-001
**Function:** Laboratory targeting for a receipt and the lab picker. Inputs: the requested section IDs (and for the picker, an item ID). Rules in `ReceiptLabGuard.ResolveTargetsAsync`: at least one laboratory must be requested; duplicates are removed; every ID must be an active `DocumentSections` row; when the caller does not hold the permission `Samples.Receive`, every requested laboratory must be one the user is a member of (`IUserSectionScopeService.GetMemberSectionIdsAsync`), otherwise `UnauthorizedAccessException` (HTTP 403). A caller who holds `Samples.Receive` may target any active laboratory. `GET api/samples/receipt-labs?itemId=` (`ReceiptLabService.GetForItemAsync`) lists the laboratories that the item's assigned tests belong to with a test count per laboratory; for a caller without `Samples.Receive` only the laboratories of which the caller is a member are listed. The picker endpoint has no permission policy beyond being logged in, and does not check that the item exists or is active (an unknown item gives an empty list).
**Code:** `backend/MicroLIMS.Application/Services/ReceiptLabGuard.cs` — `ResolveTargetsAsync`; also `backend/MicroLIMS.Application/Services/ReceiptLabService.cs` — `GetForItemAsync`
**Verified by:** reading (not run)

### FS-RCV-003
**Satisfies:** URS-RCV-002
**Function:** Reference number. Format as coded: category code, two-digit month, two-digit year, three-digit sequence. The category codes in the code are FP, RM, PM, WT, EM and AC; any other category gets "SM". The month and year are taken from the laboratory clock (`ILabClock`, default zone "Africa/Cairo" when none is injected), so the month changes at laboratory midnight. The sequence is the count of samples of the same category whose `ReceivedAt` lies inside the laboratory month, plus 1, padded to three digits. Voided and rejected samples are counted. The count and the later save are not in one transaction and no lock is taken. The database has a unique index on the reference number (`SampleConfiguration`), so two receipts that read the same count cannot both be saved; the second save fails with a database error. Nothing in `ProductWorkflowEngine.ReceiveAsync` retries. The column is limited to 20 characters. A second method, `GenerateOosCodeAsync`, builds "OOS", month, year (UTC month, not laboratory month) and a three-digit sequence from the count of distinct OOS group codes with that prefix; it is described here only because it shares the class. A month with more than 999 samples of one category would produce a four-digit sequence; the code has no limit check.
**Code:** `backend/MicroLIMS.Application/Services/ReferenceNumberGenerator.cs` — `GenerateAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-060

### FS-RCV-004
**Satisfies:** URS-RCV-003, URS-RCV-004
**Function:** Automatic test-order generation for item-based samples, as coded in `ProductWorkflowEngine.ReceiveAsync`. Preconditions, in order: the item exists; the item is active (a frozen item is refused); the item has at least one assigned test (otherwise the message says the Section Head must complete the configuration); for a Finished Product the production stage must be present and must match a known production stage by name, ignoring letter case; the grade is resolved (FS-RCV-005). The sample is created with status Received and preparation status NeedsPreparation, a reference number (FS-RCV-003), the category of the item, and the received-by user. Assigned tests are filtered: a test is dropped when an approved not-applicable declaration covers it for the sample's grade, or when its specification rows are all of other grades. If no test remains the receipt is refused. When target laboratories were passed (the HTTP endpoint always passes them), each target must have at least one remaining test or the receipt is refused with the laboratory's name, and only the tests of the target laboratories are kept. Unless the cause of testing is flagged `RequiresTestSelection` (FS-RCV-007), one `TestOrder` is created for each remaining test with section from `TestSectionLookup`, approval status Pending and workflow step Waiting; the preparation status is then set by `PreparationRules.InitialStatusAsync` from the sections of the new orders. This is the only place at receipt where test orders are created for item-based samples; no endpoint in `SampleController` creates a test order directly, but three other methods add test orders to an existing sample (FS-RCV-007, FS-RCV-008, FS-RCV-009). Which tests an item has is configured by the Section Head. The method does not check the analyst or assign one; analysts are put on tests later (FS-RCV-011).
**Code:** `backend/MicroLIMS.Application/Workflows/ProductWorkflowEngine.cs` — `ReceiveAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-061

### FS-RCV-005
**Satisfies:** URS-RCV-005
**Function:** Grade rule. `ProductWorkflowEngine.ResolveGrade` receives the item's specification rows and the requested grade (trimmed by `SpecificationService.NormalizeGrade`). If the item has no graded specification rows, a requested grade is refused and the stored grade is empty. If the item has graded rows, a grade is required and must equal one of the item's grades ignoring letter case; the item's own spelling is stored. `TestAppliesToGrade` says a test applies when it has no specification rows, or an ungraded row, or a row whose grade equals the sample's grade ignoring letter case. `AppliesToGrade` also drops a test covered by an approved not-applicable record (`IsApprovedNotApplicableFor`: the record names no grade or the sample's grade, compared ignoring letter case). The grade names are configured by the Section Head; no grade name is in this code. Grade matching here is case-insensitive; other services match differently (CONF-005).
**Code:** `backend/MicroLIMS.Application/Workflows/ProductWorkflowEngine.cs` — `ResolveGrade`, `TestAppliesToGrade`, `AppliesToGrade`
**Verified by:** reading (not run)
**Conflicts:** CONF-005

### FS-RCV-006
**Satisfies:** URS-RCV-006
**Function:** Minimum quantity warning. After the orders are built, if the item has a minimum quantity (for a periodic retest the separate retest minimum), the leading number of the entered quantity text is read (digits with an optional decimal part; a comma is read as a point). If no number can be read, no warning is made. The unit is the text after the number. When the item has no unit, a warning is made only when the entered text is a bare number below the minimum. When the item has a unit: a bare number is read in the item unit; the same unit (ignoring letter case) is compared directly; mass units and volume units that the code recognises are converted to the item's unit; any other unit pair gives no warning. The warning text is added to `Sample.Warnings` and shown to the user; the receipt is not refused. The unit names and conversion factors recognised are in the code (`TryNormalizeMass`, `TryNormalizeVolume`); the minimum and its unit are configured by the Section Head.
**Code:** `backend/MicroLIMS.Application/Workflows/ProductWorkflowEngine.cs` — `ReceiveAsync`, `LeadingQuantity`, `TryConvertToExpectedUnit`
**Verified by:** reading (not run)

### FS-RCV-007
**Satisfies:** URS-RCV-007
**Function:** Periodic-retest test selection. At receipt, when the cause of testing is flagged `RequiresTestSelection`, no test orders are made; the sample is marked `AwaitingTestSelection`, the target laboratories are stored in `PendingTargetSectionIds`, and the preparation status is Ready. `GET api/samples/{id}/test-selection` (scope check `EnsureSampleAccessAsync`, no permission policy) lists the tests of the laboratories still pending that apply to the sample's grade, and marks `CanSelect` for a caller whose role is SectionHead or Reviewer and who is a member of the laboratory. `POST api/samples/{id}/test-selection` needs `Samples.Correct`. `SampleTestSelectionService.SelectAsync` runs in one transaction. Rules: the caller's role must be SectionHead or Reviewer (else `UnauthorizedAccessException`); the sample must still be awaiting selection; the sample must not be Voided, Cancelled, Rejected or Approved; the caller must be a member of at least one pending laboratory (else `UnauthorizedAccessException`, or the message "already selected" if the caller's laboratory already has tests); at least one test code must be sent, and each code must be on the eligible list of a laboratory covered by this call. The caller's password is verified through `IElectronicSignatureService.SignAsync` with meaning `TestsSelected` before anything is created. Orders are then created (Pending, Waiting); only the laboratories covered by the call leave the pending list; the sample stays awaiting until every laboratory has its tests. Preparation status is recomputed as in FS-RCV-008. An explicit audit event `SAMPLE_TESTS_SELECTED` records laboratories and test codes, with the comment as the reason. The controller does not call `EnsureSampleAccessAsync` for the POST; the scope check is inside the service. No stale-form version check. The code does not stop the person who received the sample from choosing its tests.
**Code:** `backend/MicroLIMS.Application/Services/SampleTestSelectionService.cs` — `GetEligibleAsync`, `SelectAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-061

### FS-RCV-008
**Satisfies:** URS-RCV-008
**Function:** Add a laboratory (`POST api/samples/{id}/laboratories`). Permission: `Samples.Receive` (the any-laboratory receiving permission). The controller makes no `EnsureSampleAccessAsync` call and the service makes no laboratory-membership check, so a holder of `Samples.Receive` can add any laboratory to any sample. `AddLaboratoryService.AddAsync` runs in one transaction. Inputs: sample ID, section ID, password, reason. Rules: a reason is required; the sample must exist; the overall status must not be Rejected, Voided or Cancelled; the sample must not be awaiting test selection; the section must not already have test orders on the sample; the sample must be item-based; at least one of the item's assigned tests must belong to that section and apply to the sample's grade (not-applicable records are honoured). Then open sections are frozen (`SampleSectionRollup.FreezeOpenSections`), the signature is made by `ReviewGateService.SignAndLogAsync` with meaning `LaboratoryAdded` and the password and reason, new orders (Pending, Waiting) are added, the section's sign-off status is set to InTesting, and the roll-up is recomputed. If the sample was in testing with an Approve decision, the approval decision, time and approver are cleared. If the new laboratory's tests need preparation and the sample has no preparation record, the preparation status goes back to NeedsPreparation. No stale-form version check. No explicit audit event is written in this method beyond what `ReviewGateService.SignAndLogAsync` writes (not read in this task).
**Code:** `backend/MicroLIMS.Application/Services/AddLaboratoryService.cs` — `AddAsync`, `AddCoreAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-061, CONF-062

### FS-RCV-009
**Satisfies:** URS-RCV-009
**Function:** Correct a sample (`PUT api/samples/{id}/correct`). Permission: `Samples.Correct`; `EnsureSampleAccessAsync` is called before the service (write scope = laboratory membership). Inputs: the full corrected set of fields, a reason, the password. `SampleCorrectionService.CorrectAsync` runs in one transaction. Rules in order: a reason is required; `RecordVersion.EnsureCurrent` compares the sample's version with the If-Match value (the stale-form check); the status must be Received or InTesting, otherwise the record is locked. Control number and sampled by are required. A changed cause of testing must exist, and neither the old nor the new cause may be the "Retest" cause. For item-based samples: batch number required, expiry not before manufacturing date, grade re-resolved when sent or when the item changes, production stage required and known for a Finished Product. For water samples: storage condition (only when already set) must be Refrigerator or RoomTemperature, and a refrigerated sample needs a non-negative storage time. For after-cleaning samples: previous product and its batch number are required, and the sample batch number follows the previous batch number. A change of item or location is allowed only if the new item has the same category, is active and has tests, the sample is not a retest sample (one with an original sample), is not awaiting test selection, and no test work exists (`EnsureNoTestWorkAsync` checks status, workflow step, incubations, results, count readings, pathogen observations, workflow step results, media usage, room monitorings, return events and entered locations); the orders and preparation are then rebuilt. A grade change alone is allowed only while every test is Pending. If nothing differs the method refuses with "No changes were made." Only after these checks is the password verified (`IElectronicSignatureService.SignAsync`, meaning `SampleCorrected`, with the reason); then the changes are applied, result projection rows copy the new batch and control numbers, a review-workflow event is logged, and an explicit audit event `SampleCorrected` records every field with old and new value and the reason. Correction is not blocked for the user who received the sample, and no second person is required.
**Code:** `backend/MicroLIMS.Application/Services/SampleCorrectionService.cs` — `CorrectAsync`, `CorrectCoreAsync`, `EnsureNoTestWorkAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-061

### FS-RCV-010
**Satisfies:** URS-RCV-010
**Function:** Void a sample (`POST api/samples/{id}/void`). Permission: `Samples.Correct`; `EnsureSampleAccessAsync` is called first. Inputs: reason, password. `SampleCorrectionService.VoidAsync` runs in one transaction. Rules: a reason is required; the sample must exist and must not already be Voided. No other status is refused: this method makes no check on the current status, so a sample that is InTesting, in review or Approved can be voided by a holder of `Samples.Correct`. There is no stale-form version check. The password is verified through `IElectronicSignatureService.SignAsync` (meaning `SampleVoided`) before any change. The sample status becomes Voided, `CertificateRemarks` is set to "Voided: " and the reason, every order that is not superseded becomes Voided, every laboratory sign-off becomes Voided, and the result projection rows take the Voided sample status. No approval decision, approver or decision time is written, so a void is kept separate from a rejection. A review-workflow event and an explicit audit event `SampleVoided` (status old and new, voided tests, reason) are written. No second person is required to approve a void.
**Code:** `backend/MicroLIMS.Application/Services/SampleCorrectionService.cs` — `VoidAsync`, `VoidCoreAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-063

### FS-RCV-011
**Satisfies:** URS-RCV-011, URS-RCV-012
**Function:** Assign analysts. `PUT api/samples/{id}/assign-analyst` (whole sample) and `PUT api/samples/{id}/test-assignments` (one analyst per test) need `Samples.AssignAnalyst` and call `EnsureSampleAccessAsync` first. In `SampleAssignmentService`: the acting user changes only open tests (not superseded, not Approved, Rejected, Voided or Cancelled) in laboratories of which the acting user is a member (write scope). Whole-sample assignment gives the analyst the acting user's open tests that are in the assignee's laboratories; a null analyst clears all of the acting user's open tests; it is refused when there are no open tests in scope. Per-test assignment refuses an empty list, a repeated test, a test not on the sample, a closed test, and a test in a laboratory outside the acting user's membership. `AnalystAssignmentRules.EnsureAssignableAsync` applies to every named assignee: the user must exist, be active and not be locked; the role must be Analyst, Reviewer or SectionHead; a SectionHead can be assigned only by themselves; the assignee must be a member of the test's laboratory. Each changed test gets one `AuditLog` row (`AssignedAnalyst`, `ReassignedAnalyst` or `UnassignedAnalyst`) with the previous and new analyst names, the reason, the acting user and the time from the injected clock. Tests whose analyst does not change get no row. No stale-form version check. No check is made here that the assignee is not the person who received the sample.
**Code:** `backend/MicroLIMS.Application/Services/SampleAssignmentService.cs` — `AssignAnalystAsync`, `AssignTestsAsync`, `ApplyAsync`
**Verified by:** reading (not run)

### FS-RCV-012
**Satisfies:** URS-RCV-011
**Function:** Assignment options (`GET api/samples/{id}/assignment-options`). Permission `Samples.AssignAnalyst`; scope check first. Returns each non-superseded test with its current analyst, whether the acting user can assign it (open and in the acting user's laboratories), and the IDs of eligible users: active, unlocked users of role Analyst or Reviewer, plus the acting user when the acting user is a SectionHead, who are members of the test's laboratory (`AnalystAssignmentRules.Candidates`). The result is read-only and is not audited.
**Code:** `backend/MicroLIMS.Application/Services/SampleAssignmentService.cs` — `GetAssignmentOptionsAsync`
**Verified by:** reading (not run)
