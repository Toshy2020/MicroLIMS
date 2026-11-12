# FS-WST Functional Specification

| Field | Value |
|---|---|
| Document number | FS-WST |
| Title | Working standard qualification functional specification |
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

- This document covers `WorkingStandardsController`, `WorkingStandardService` (and its sign-off partial file), `WorkingStandardCalculator` and the qualification parts of `HplcRunService` (`HplcRunService.WorkingStandards.cs`). The HPLC run itself, system suitability and the sample result path are in FS-INS. The stock lot rules (`LotUsability`) are in FS-INS-003.
- Every statement was written from the code files named in each entry. Nothing was run. Pharmacopoeia chapters are pointers only (to be confirmed by QA).
- Four numbers are written in the code and are not configured: 6 replicates, a maximum RSD of 2.0 %, a validity of 12 months and a "due soon" warning of 30 days (`WorkingStandardRules`). See CONF-099.
- Section scope: a qualification is read with the user's readable sections and changed with the user's member sections (`EnsureSectionAsync`, `GetSectionIdsAsync`). A record outside the scope is reported as "not found".
- Qualifications, documents and lots are audited by the context's automatic capture; none is in `AuditCaptureExcludedTypes`. Every table also has an automatic `xmin` concurrency token. None of the methods in this document calls `RecordVersion.EnsureCurrent`; there is no stale-form version check, although the qualification detail returned to the screen carries a `Version`.
- Methods that sign validate the state first, then call `IElectronicSignatureService.SignAsync`, then change data, so a refused action leaves no signature (the signature service writes its own failed-attempt audit row; see FS-SIG).
- Rounding is `MidpointRounding.AwayFromZero`. Values are `decimal`.

## Functions

### FS-WST-001
**Satisfies:** URS-WST-001, URS-WST-007
**Function:** Permissions and lists. Reads (`GET lots`, `qualifications`, `qualifications/{id}`, `source-samples`, `documents/{id}/file`) need only a signed-in user; the service limits them to the user's readable sections. Creating and editing a draft and uploading documents need `WorkingStandards.Qualify`. Review, return and review-rejection need `Samples.Review`; approval and approval-rejection need `Samples.Approve`. `GetLotsAsync` lists lots of type WorkingStandard with a status worked out as follows: Depleted when the remaining quantity is zero or less; else Expired when the expiry date is before the laboratory-local date; else DueSoon when the expiry date is within 30 days of that date; else Valid. It adds the ID of the lowest-numbered open qualification (Draft, Assayed or Reviewed) of the lot. `GetEligibleSourceSamplesAsync` lists raw material samples (not voided, cancelled, rejected or retest-requested) that have a non-superseded test order at the step Approved whose test definition has the equation type HplcAssay, HplcMultiAnalyte, StandardComparison or HplcMethodAssay, one row per sample, with an optional search over sample number, item name and batch. The file download checks read access to the qualification's section.
**Code:** `backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs` — class `WorkingStandardsController`; `backend/MicroLIMS.Application/Services/WorkingStandardService.cs` — `GetLotsAsync`, `GetQualificationsAsync`, `GetAsync`, `GetEligibleSourceSamplesAsync`, `DownloadDocumentAsync`, `EnsureSectionAsync`, `LoadAsync`
**Verified by:** reading (not run)

### FS-WST-002
**Satisfies:** URS-WST-002
**Function:** Create and edit a draft qualification. `CreateAsync` takes a kind (Initial or Requalification). Requalification: a working standard lot must be named and found, must be in the user's member sections, must have no open qualification, must have a master entry, and quantity and location must be empty (they belong to the lot); the qualification copies the lot's section, master entry, name and batch. Initial: a reference standard master entry that is active; either a received raw material sample from the eligible list (FS-WST-001) or a material name and batch (both required), not both; a quantity in grams above zero and a storage location are required. The section is the master entry's section if the user is a member of it, otherwise the section chosen for the user by `ResolveSectionForCreateAsync`. Moisture, when given, must be from 0 to below 100 percent. The code ("WSQ-" prefix and the laboratory-local date) is generated, with one retry on a unique-index clash, and the status is Draft. `UpdateDraftAsync` allows a change only while the status is Draft; for an Initial kind quantity and location are required again, for a Requalification they are refused. Moisture is typed by the analyst; it is not read from any lot or result. No signature is asked for creation or editing.
**Code:** `backend/MicroLIMS.Application/Services/WorkingStandardService.cs` — `CreateAsync`, `UpdateDraftAsync`, `ValidateMoisture`
**Verified by:** reading (not run)

### FS-WST-003
**Satisfies:** URS-WST-002
**Function:** Qualification documents. Upload needs the status Draft, a non-empty file of at most 25 MB with a declared content type of application/pdf, image/png or image/jpeg (the check reads the declared type; the content is not inspected) and a file name. The file is stored under `working-standards/{qualification}/{document}{extension}`; if storing fails the row is removed. Older current documents of the same kind are marked superseded by the new one (kept, not deleted). `AssignProblem` is the rule for assignment to a run: the qualification must be Draft, the moisture must be entered, and an Initial qualification that has no source sample must have a current SourceReport document ("first test report").
**Code:** `backend/MicroLIMS.Application/Services/WorkingStandardService.cs` — `UploadDocumentAsync`, `ValidateFile`, `AssignProblem`
**Verified by:** reading (not run)

### FS-WST-004
**Satisfies:** URS-WST-003
**Function:** Assignment to an HPLC run and entry (in `HplcRunService`). `AssignQualificationsAsync` needs `Hplc.Operate` and `WorkingStandards.Qualify`, write scope on the run, the run open, the SST Passed, and refuses when the SST used a lot that is not a ReferenceStandard (a working standard cannot be qualified against a working standard). Only assay-mode methods qualify. Eligible qualifications are Draft, in the run's section, whose master entry is the standard entry of one of the method's analytes, not on another run that is not abandoned, and that pass `AssignProblem`. The qualification is tied to that method analyte and added to the run as a sample. `SaveQualificationReplicatesAsync` needs the sample still assigned, the qualification Draft, and the run Open (or Completed with a return reason); every replicate needs a weight above zero, one response for the qualified analyte only (above zero), no peaks, and an internal standard response above zero if the method has one. `SubmitQualificationAsync` refuses unless the SST has passed, exactly 6 replicates were entered, the SST standard values are complete, the moisture is entered and a current sample report evidence file is uploaded for the run sample. It calculates (FS-WST-005), then signs (meaning ResultRecorded, bound to the qualification), and sets the mean, RSD, potency, pass flag, reasons, the replicate assay percents (as JSON), the status Assayed, the submitting user, time and signature. No stale-form version check is made.
**Code:** `backend/MicroLIMS.Application/Services/HplcRunService.WorkingStandards.cs` — `AssignQualificationsAsync`, `EligibleQualificationsAsync`, `SaveQualificationReplicatesAsync`, `QualificationEntryProblem`, `QualificationSubmitProblem`, `SubmitQualificationAsync`; `backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs` — `AssignQualifications`, `SaveQualificationReplicates`, `SubmitQualification`
**Verified by:** reading (not run)

### FS-WST-005
**Satisfies:** URS-WST-003
**Function:** Qualification calculation, as coded. Each replicate's assay percent is the HPLC assay formula of FS-INS-007 (`HplcAssayCalculator.Calculate`) for the single qualified analyte, using the SST mean response, the actual standard weight, purity and moisture from the SST, the method's theoretical weights, and the effective response (divided by the internal standard response if the method has one). The mean is the average of the replicate assay percents. The RSD is the sample standard deviation (n minus 1; square root by Newton iteration rounded to 10 decimals) divided by the mean times 100; it is empty with fewer than 2 replicates and 0 when the mean is 0. The potency is the mean times 100 divided by (100 minus the qualification's moisture percent), rounded to 3 decimals (moisture from 0 to below 100). The qualification passes when the replicate count equals 6 and, if an RSD exists, the RSD rounded to 2 decimals is not above 2.0; otherwise the reasons are stored. No limit is applied to the potency or to the mean. The numbers 6 and 2.0 are written in the code (CONF-099).
**Code:** `backend/MicroLIMS.Application/Helpers/WorkingStandardCalculator.cs` — `Evaluate`, `DriedBasisPotency`, `PassesRsd`, `WorkingStandardRules`
**Calculations:** CALC-057, CALC-058, CALC-059
**Verified by:** reading (not run)
**Conflicts:** CONF-099

### FS-WST-006
**Satisfies:** URS-WST-004, URS-WST-005, URS-WST-008
**Function:** Sign-off. `ReviewAsync` (`Samples.Review`) needs the status Assayed, a qualification that passed, a user other than the submitting analyst, and a run that was not abandoned; it signs (meaning Reviewed) and sets Reviewed with user, time and signature. `ReturnAsync` (`Samples.Review`) needs Assayed and a reason (trimmed, at most 500 characters); it needs no signature and no password; it sets the status back to Draft, stores the return reason and clears the mean, RSD, potency, replicate values, reasons, pass flag and the submitting user, time and signature of the assay. The cleared values remain only in the audit trail. No check is made that the returning user differs from the analyst who submitted. `RejectAtReviewAsync` needs Assayed and a user other than the submitter. `RejectAtApprovalAsync` needs Reviewed and a user who is neither the submitter nor the reviewer. Both rejections need a reason and sign (meaning Rejected) and set Rejected with user, time, signature and reason; a failed qualification can only be rejected. `ApproveAsync` (`Samples.Approve`) needs Reviewed, a passed qualification, a user who is neither the submitter nor the reviewer, and a run not abandoned; it signs (meaning Approved). All five also need write scope on the qualification's section.
**Code:** `backend/MicroLIMS.Application/Services/WorkingStandardService.SignOff.cs` — `ReviewAsync`, `ReturnAsync`, `RejectAtReviewAsync`, `RejectAtApprovalAsync`, `ApproveAsync`, `RejectAsync`
**Verified by:** reading (not run)

### FS-WST-007
**Satisfies:** URS-WST-006
**Function:** Effect of approval on the lot (in `ApproveAsync`, same save as the status change). The expiry is the laboratory-local date plus 12 months (`WorkingStandardRules.ValidityMonths`, CONF-099). Initial: a new `Material` of type WorkingStandard is created with a code ("WS-" prefix and date), a stock ID, the master entry, the source batch number, manufacturer name "In-house", unit Gram, quantity received and remaining equal to the entered grams, purity = the measured potency, moisture = the qualification moisture, the location, the expiry and the approving user. Requalification: the existing lot's purity, moisture and expiry are replaced in place (the earlier values remain only in the audit trail), CONF-100. A clash of the generated lot code is reported ("sign again"). The lot becomes usable in other methods once it passes `LotUsability.Check`. `MaterialService.UpdateAsync` refuses a direct edit of a working standard lot ("change only through their qualifications").
**Code:** `backend/MicroLIMS.Application/Services/WorkingStandardService.SignOff.cs` — `ApproveAsync`; `backend/MicroLIMS.Application/Services/MaterialService.cs` — `UpdateAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-099, CONF-100
