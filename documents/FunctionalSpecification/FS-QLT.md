# FS-QLT Functional Specification

| Field | Value |
|---|---|
| Document number | FS-QLT |
| Title | Quality records (deviation, CAPA, change control, complaint) functional specification |
| Version | 0.2 |
| Status | Draft — pending approval |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-25 | AI coding agent, draft from code | First draft for D2 Tasks 1 to 8 as merged on r3/integration (FS-QLT-001..018; Draft — pending approval). |
| 0.2 | 2026-11-26 | AI coding agent, draft from code | Added D2 Task 9, the master-data change-control gate (FS-QLT-022..024, Draft — pending approval); corrected FS-QLT-001, FS-QLT-016 and FS-QLT-018 against the code; scope notes updated to D2 Tasks 1 to 10. |

## Scope and reading notes

- This document describes the quality record code merged for D2 Tasks 1 to 10 and Task 9 (FS-QLT-022 to FS-QLT-024). Every statement was written from the code files named in each entry.
- Not described here: the screens (D2 Tasks 11 to 13).
- Where the approved specification and the code disagree, the entry says what the code does and names the `CONF` entry in `documents/Conflicts.md`.
- In every entry, "refused" means the service throws a coded error (message codes in `backend/MicroLIMS.Shared/Messages/MessageCodes.Quality.cs` and the English texts in `frontend/src/locales/en/serverMessages.json`).

## Functions

### FS-QLT-001
*(Draft — pending approval)*
**Satisfies:** URS-QLT-001
**Function:** Creation and numbering. `QualityRecordCore.CreateAsync` is the one place that writes a new `QualityRecord`. Create access by type (`EnsureCreateAccessAsync`): Deviation needs `Quality.Raise`; ChangeControl needs `Quality.Raise` and an active user whose role is SectionHead, QcManager or Qa; Capa needs `Quality.Investigate` or `Quality.Approve`; Complaint needs `Quality.Approve`. A section, when given, must be one the user is a member of and must exist (`DocumentSections`). Title is 1 to 200 characters and description 1 to 4000 (`RequiredText`). Links given at creation must be readable by the user. The number comes from `ReferenceNumberGenerator.GenerateQualityNumberAsync` under a per-type transaction advisory lock (`AdvisoryLockKeys.QualityDeviation`, `QualityCapa`, `QualityChangeControl`, `QualityComplaint`): the prefix is `DEV-`, `CAPA-`, `CC-` or `CMP-` followed by the laboratory year and `-`, and the sequence is the highest existing suffix plus one, written with four digits. There is no generic create route: `POST api/quality/{type}` was removed (CONF-230), so every record is created through its typed service.
**Code:** `backend/MicroLIMS.Application/Services/Quality/QualityRecordCore.cs` — `CreateAsync`, `EnsureCreateAccessAsync`; `backend/MicroLIMS.Application/Services/ReferenceNumberGenerator.cs` — `GenerateQualityNumberAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-230

### FS-QLT-002
*(Draft — pending approval)*
**Satisfies:** URS-QLT-008, URS-QLT-013
**Function:** Read scope, version check and locking. `GetReadAccessAsync` loads the caller's active role, permission codes and, only when the caller holds `Quality.View`, the readable section IDs. `ReadableRecords` returns nothing without `Quality.View`; otherwise it keeps site-wide records and records of readable sections. A user whose role is Analyst and who holds none of `Quality.Investigate`, `Quality.LabConclude` or `Quality.Approve` sees only records they raised, own, or hold an action in. `LoadForReadAsync` and `ReadDtoAsync` refuse a record outside that set with `Quality.NotFound`. `LoadForWriteAsync` compares the `If-Match` version with the stored version and refuses a difference with `Quality.Stale`. `LockForWriteAsync` takes a row lock (`SELECT … FOR UPDATE`) on the record before loading it. `MutateAsync` (used by links, attachments and comments) requires `Quality.Raise`, or, for removal and voiding, the record owner or `Quality.Investigate`. Adding links is refused on a closed, rejected or cancelled record (`Quality.RecordTerminal`); comments and attachments remain allowed as a dated addendum (CONF-238).
**Code:** `backend/MicroLIMS.Application/Services/Quality/QualityRecordCore.cs` — `GetReadAccessAsync`, `ReadableRecords`, `LoadForReadAsync`, `LoadForWriteAsync`, `LockForWriteAsync`, `MutateAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-238

### FS-QLT-003
*(Draft — pending approval)*
**Satisfies:** URS-QLT-007
**Function:** Signed step. `QualityRecordCore.SignStepAsync` refuses to run outside a database transaction (`Quality.TransactionRequired`). It locks the record row, refuses a stale version, and locks the record's link rows (`FOR UPDATE`) so that a link cannot change while the step is signed. It serialises a snapshot `{ Version, Data }` of the caller's content, calls `IElectronicSignatureService.SignAsync(userId, password, meaning, "QualityRecord", recordId, comment, ip)` (user identity and password checked by that service), sets the signature time from the injected clock, advances the record version, writes one `QualityRecordStep` (`Kind`, `SignatureId`, `SignedByUserId`, `SignedAtUtc`, `Comment`, `SnapshotJson`) and saves. A comment is limited to 4000 characters. `CancelAsync` uses the same path with `QualityCancelled` and sets `IsCancelled` and the cancel reason. The steps and their meanings are listed in FS-QLT-007 to FS-QLT-010 and FS-QLT-015.
**Code:** `backend/MicroLIMS.Application/Services/Quality/QualityRecordCore.cs` — `SignStepAsync`, `CancelAsync`; `backend/MicroLIMS.Domain/Entities/Quality/QualityChildren.cs` — `QualityRecordStep`
**Verified by:** reading (not run)

### FS-QLT-004
*(Draft — pending approval)*
**Satisfies:** URS-QLT-007
**Function:** Database protection of signed content, from the migrations. `20261124201920_AddQualityRecords` adds triggers that reject UPDATE, DELETE and TRUNCATE on `QualityRecordSteps` and `QualityChecklistAnswers`; reject DELETE and TRUNCATE on `QualityRecords`, `QualityActions` and `QualityAttachments`; reject DELETE once a step is signed for the record on `QualityRecordLinks` (function `prevent_quality_record_link_delete`), and reject TRUNCATE on it. `20261125043853_ProtectQualityExtensionRequests` rejects DELETE on `QualityExtensionRequests`, rejects any UPDATE once `Decision` is set, rejects TRUNCATE, and rejects a change of the link target columns (`RecordId`, `TargetType`, `TargetId`, `NewRecordCode`) after a signed step. `20261125191721_ProtectApprovedCapaActions` rejects a change to the planned fields of a CAPA action (`RecordId`, `Kind`, `Description`, `ResponsibleUserId`, `DueDate`) when the CAPA status is PlanApproved, CompletionVerified, Closed or Cancelled, or the record is cancelled; under the same condition it rejects clearing or changing the completion fields (`DoneAtUtc`, `DoneByUserId`, `Evidence`, `EvidenceLinkId`) once they are set. No trigger blocks UPDATE on `QualityRecords` or on the type detail tables (`DeviationDetails`, `CapaDetails`, `ChangeControlDetails`, `ComplaintDetails`); those are protected by the services only.
**Code:** `backend/MicroLIMS.Persistence/Migrations/20261124201920_AddQualityRecords.cs` — `Up`; `backend/MicroLIMS.Persistence/Migrations/20261125043853_ProtectQualityExtensionRequests.cs` — `Up`; `backend/MicroLIMS.Persistence/Migrations/20261125191721_ProtectApprovedCapaActions.cs` — `Up`
**Verified by:** reading (not run). The trigger catalogue test (`AppendOnlyTriggerCatalogPostgresTests`) was not run for this document.

### FS-QLT-005
*(Draft — pending approval)*
**Satisfies:** URS-QLT-006
**Function:** Separation rules, as coded. `QualityRecordCore.EnsureNotRaiserOrOwner` refuses with `Quality.RaiserOrOwner` when the caller raised or owns the record. A null raiser never matches, so a record without a person is excluded from the rule ("System"). The rule is called in: `DeviationService.ApproveAsync`, `ReturnAsync`, `CloseAsync`; `CapaService.ApprovePlanAsync`, `ReturnPlanAsync`, `VerifyCompletionAsync`, `SignEffectivenessAsync`; `ChangeControlService.ApproveAsync`, `RejectAsync`, `CloseAsync`; `ComplaintService.ConcludeAsync`, `CloseAsync`; `QualityRecordCore.CancelAsync` (all four types); and `QualityConfigService.DecideAsync` (extension decision). It is not called in `DeviationService.AssessAsync` or `RejectAsync`, or in complaint receipt and investigation request (CONF-228, CONF-229). The owner cannot sign the laboratory part of the same record: `DeviationService.SignLabConclusionAsync` (`Deviation.OwnerLabConclusion`) and `ComplaintService.SignLabReportAsync` (`Complaint.OwnerLabReport`). The analyst who produced a test result cannot own a deviation linked to it: `DeviationService.EnsureOwnerAsync` (`Deviation.AnalystOwner`), using `SegregationOfDutiesGuard.GetTestOrderIdsPerformedByAsync` over linked test orders and OOS investigation orders. A QA user cannot be named owner of a deviation, complaint or CAPA (`EnsureOwnerAsync`, `ComplaintService.EnsureOwnerAsync`, `CapaService.EnsureLaboratoryMemberAsync`). A change control owner must be an active SectionHead or QcManager (`ChangeControlService.EnsureChangeControlOwnerAsync`). An extension requester cannot decide their own request (`Quality.ExtensionSelfDecision`).
**Code:** `backend/MicroLIMS.Application/Services/Quality/QualityRecordCore.cs` — `EnsureNotRaiserOrOwner`; `backend/MicroLIMS.Application/Services/Quality/DeviationService.cs` — `EnsureOwnerAsync`, `SignLabConclusionAsync`; `backend/MicroLIMS.Application/Services/Quality/ComplaintService.cs` — `SignLabReportAsync`; `backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs`
**Verified by:** reading (not run)
**Conflicts:** CONF-228, CONF-229

### FS-QLT-006
*(Draft — pending approval)*
**Satisfies:** URS-QLT-002
**Function:** Deviation raise and source raises. `DeviationService.RaiseAsync` (`Quality.Raise`) refuses an occurrence time that is not UTC (`Deviation.OccurrenceUtc`) or is in the future (`Deviation.FutureOccurrence`); location is optional and up to 200 characters; immediate action is 1 to 4000 characters; the record is created with status Raised. Source raises (`DeviationService.Sources.cs`) take the source row lock and call `EnsureNoSourceDeviationAsync`, which refuses when a non-cancelled deviation already links the source (`Deviation.DuplicateSource`, naming that deviation): `RaiseFromOosRequestAsync` (`Quality.Approve`) requires a readable OOS investigation, and, when the request is already addressed, refuses if a non-cancelled deviation already links the investigation; it creates the deviation with a link to the investigation, and marks the request addressed (`OosInvestigationService.MarkDeviationRequestAddressedAsync`) in the same transaction; `EscalateMonitoringDeviationAsync` (`Quality.Approve` or `Quality.Investigate`) copies the monitoring deviation's description, keeps the source unchanged and refuses a stale source version; `RaiseFromAuditFindingAsync` (`Quality.Approve`) compares the expected version with the finding's status value and writes a description that names the finding.
**Code:** `backend/MicroLIMS.Application/Services/Quality/DeviationService.cs` — `RaiseAsync`, `CreateAsync`; `backend/MicroLIMS.Application/Services/Quality/DeviationService.Sources.cs` — `RaiseFromOosRequestAsync`, `EscalateMonitoringDeviationAsync`, `RaiseFromAuditFindingAsync`, `EnsureNoSourceDeviationAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-224

### FS-QLT-007
*(Draft — pending approval)*
**Satisfies:** URS-QLT-002
**Function:** Deviation path. Status values: Raised, Investigation, LabConcluded, Approved, Closed, Rejected, Cancelled. Each step first takes the record lock and refuses a stale version (`LockForWriteAsync`), then refuses if the record is cancelled (`Deviation.WrongState`), then checks the status the step requires.
- Assess (`Quality.Approve`; Raised): `Accept` must be true (otherwise `Deviation.UseReject`); the classification must be active (`Deviation.Classification`); the owner is checked (`EnsureOwnerAsync`); the due date may be given, not before the laboratory date (`Deviation.PastDueDate`), or is computed from the due rule (empty when none). Signs `DeviationAssessed`; status Investigation.
- Reject (`Quality.Approve`; Raised): reason 1 to 4000 characters; signs `DeviationRejected`; status Rejected (final).
- Save investigation (owner, or `Quality.Investigate` and not `Quality.Approve`; Investigation): impact assessment, root cause description and proposed actions are each 1 to 4000 characters; the root cause category must be an active `OosCauseCategory` (`Deviation.Cause`). Not signed.
- Lab conclusion (`Quality.LabConclude`; Investigation): the caller must not be the owner (`Deviation.OwnerLabConclusion`); the investigation must be complete (`Deviation.Incomplete`). Signs `QualityLabConclusion`; status LabConcluded.
- Approve (`Quality.Approve`; LabConcluded; not raiser or owner): records whether a CAPA is required. Signs `QualityApproved`; status Approved.
- Return (`Quality.Approve`; LabConcluded; not raiser or owner): comment 1 to 2000 characters is stored as a `QualityComment`; status returns to Investigation. Nothing is signed, and the earlier laboratory conclusion remains in the timeline.
- Close (`Quality.Approve`; Approved; not raiser or owner): when a CAPA is required, a non-cancelled linked CAPA in PlanApproved, CompletionVerified or Closed must exist (`FindApprovedCapaAsync`; `Deviation.CapaRequired`); no action of the record may be unfinished (`Deviation.UnfinishedActions`, naming the first). Signs `QualityClosed`; status Closed.
- Cancel (`Quality.Approve`; Raised, Investigation, LabConcluded or Approved): signs `QualityCancelled`; status Cancelled (`Deviation.FinalCancellation` otherwise).
**Code:** `backend/MicroLIMS.Application/Services/Quality/DeviationService.cs` — `AssessAsync`, `RejectAsync`, `SaveInvestigationAsync`, `SignLabConclusionAsync`, `ApproveAsync`, `ReturnAsync`, `CloseAsync`, `CancelAsync`, `FindApprovedCapaAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-223, CONF-224, CONF-228

### FS-QLT-008
*(Draft — pending approval)*
**Satisfies:** URS-QLT-003
**Function:** CAPA path. Status values: Plan, PlanApproved, CompletionVerified, Closed, Cancelled, with effectiveness result Effective or NotEffective.
- Create (`Quality.Investigate` or `Quality.Approve`, by the route policy and by `EnsureCreateAccessAsync`): the owner must be an active non-QA user with `Quality.View` who is a member of the section (`Capa.InvalidOwner`); a source record, when given, must be readable; the due date comes from the due rule with no classification.
- Save plan (owner only, and the owner must not hold `Quality.Approve`; status Plan): at least one action (`Capa.PlanEmpty`); each action kind is Corrective or Preventive; each responsible person is an active laboratory user in the section; no action due date before the laboratory date; the effectiveness criteria and method are 1 to 4000 characters; the planned effectiveness date must be after the latest action due date (`Capa.EffectivenessDateOrder`). Existing actions are updated in place by position and cannot be removed.
- Approve plan (`Quality.Approve`; Plan; not raiser or owner): needs at least one action and the effectiveness fields (`Capa.PlanIncomplete`). Signs `CapaPlanApproved`; status PlanApproved.
- Return plan (`Quality.Approve`; Plan; not raiser or owner): comment stored as a `QualityComment`; status stays Plan.
- Complete action (responsible person only; PlanApproved): not already done (`Capa.ActionAlreadyDone`); evidence 1 to 4000 characters; an evidence link must belong to this record. Not signed.
- Verify completion (`Quality.Approve`; PlanApproved; not raiser or owner): no unfinished action (`Capa.UnfinishedActions`). Signs `CapaCompletionVerified`; status CompletionVerified.
- Effectiveness (`Quality.Approve`; CompletionVerified; not raiser or owner): the laboratory date must be on or after the planned date (`Capa.EffectivenessEarly`); the result must be a defined value. For NotEffective, `NewCapaId` must name a different, non-cancelled CAPA whose `SourceRecordId` is this CAPA (`Capa.NewCapaRequired`). Signs `CapaEffectivenessChecked`; status Closed for either result.
- Cancel (`Quality.Approve`; any status except Closed and Cancelled): signs `QualityCancelled`.
**Code:** `backend/MicroLIMS.Application/Services/Quality/CapaService.cs` — `CreateAsync`, `SavePlanAsync`, `ApprovePlanAsync`, `ReturnPlanAsync`, `CompleteActionAsync`, `VerifyCompletionAsync`, `SignEffectivenessAsync`, `CancelAsync`
**Verified by:** reading (not run)

### FS-QLT-009
*(Draft — pending approval)*
**Satisfies:** URS-QLT-004
**Function:** Change control path. Status values: Proposed, Assessed, ApprovedForImplementation, Closed, Rejected, Cancelled.
- Propose (`Quality.Raise` and an active SectionHead, QcManager or Qa role): the owner defaults to the proposer, so a QA proposer must name a SectionHead or QcManager as owner; the owner must be an active SectionHead or QcManager who is a member of the section (`ChangeControl.InvalidOwner`). Affected records must be of type Item, Specification, TestDefinition, Equipment or DocumentMaster; an existing record must be readable, and a new one needs a `NewRecordCode` of up to 100 characters; a duplicate is refused (`ChangeControl.DuplicateAffected`). Reason 1 to 4000 characters; status Proposed.
- Add affected (`Quality.Raise`; raiser, owner or a SectionHead, QcManager or Qa role; Proposed only): `ChangeControl.AffectedFrozen` after the impact assessment is signed (CONF-226, CONF-231).
- Save assessment (the named owner — SectionHead or QcManager — or a non-QA user with `Quality.Investigate`; Proposed): impact summary 1 to 4000 characters; tasks become `ChangeTask` actions with a laboratory responsible person and a due date not before the laboratory date; tasks cannot be removed; checklist answers are recorded (FS-QLT-013). Not signed.
- Sign assessment (the named owner — SectionHead or QcManager — or a non-QA user with `Quality.Investigate`; Proposed): the summary must be saved (`ChangeControl.AssessmentIncomplete`); every active checklist item must be answered (`ChangeControl.ChecklistIncomplete`). Signs `ChangeImpactAssessed`, with a snapshot of the affected links, tasks and answers; status Assessed (CONF-236).
- Approve (`Quality.Approve`; Assessed; not raiser or owner): signs `ChangeApproved`; status ApprovedForImplementation.
- Reject (`Quality.Approve`; Proposed or Assessed; not raiser or owner): reason stored on the detail and the step; signs `ChangeRejected`; status Rejected.
- Complete task (responsible person only; ApprovedForImplementation): evidence 1 to 4000 characters; evidence link must belong to this change control. Not signed.
- Close (`Quality.Approve`; ApprovedForImplementation; not raiser or owner): no unfinished task (`ChangeControl.UnfinishedTasks`); when a Specification or TestDefinition link has no `ChangedAtUtc`, an unchanged justification is required (`ChangeControl.UnchangedJustificationRequired`) (CONF-232); the post-change review is required (1 to 4000 characters). Signs `ChangeClosed` with meaning `QualityClosed`; status Closed.
- Cancel (`Quality.Approve`; not Closed, Rejected or Cancelled): signs `QualityCancelled`.
**Code:** `backend/MicroLIMS.Application/Services/Quality/ChangeControlService.cs` — `ProposeAsync`, `AddAffectedAsync`, `SaveAssessmentAsync`, `SignAssessmentAsync`, `ApproveAsync`, `RejectAsync`, `CompleteTaskAsync`, `CloseAsync`, `CancelAsync`, `IsAllowedAffectedType`
**Verified by:** reading (not run)
**Conflicts:** CONF-226, CONF-231, CONF-232, CONF-235, CONF-236

### FS-QLT-010
*(Draft — pending approval)*
**Satisfies:** URS-QLT-005
**Function:** Complaint path. Status values: Received, Investigation, LabReported, Concluded, Closed, Cancelled.
- Receive (`Quality.Approve`): source 1 to 200 characters; received date not default and not after the laboratory date (`Complaint.FutureReceivedOn`); external reference and lot text optional, each up to 200 characters; complaint text 1 to 4000 characters; an item, when given, must be active. Status Received.
- Request investigation (`Quality.Approve`; Received): classification must be active (`Complaint.Classification`); owner rules as for a deviation (`Complaint.InvalidOwner`); due date given or computed (`Complaint.PastDueDate`); what to test is 1 to 4000 characters. Status Investigation.
- Save investigation (owner, or `Quality.Investigate` and not `Quality.Approve`; Investigation): findings 1 to 4000 characters; each sample ID is checked for read access and added as a `Sample` link when not already linked. Not signed.
- Sign laboratory report (`Quality.LabConclude`; Investigation): the caller must not be the owner (`Complaint.OwnerLabReport`); findings must be present (`Complaint.Incomplete`). Signs `ComplaintLabReport` with meaning `QualityLabConclusion`; status LabReported.
- Conclude (`Quality.Approve`; LabReported; not raiser or owner): outcome must be Justified, NotJustified or Inconclusive (`Complaint.OutcomeInvalid`); summary 1 to 4000 characters. When outcome is `Justified`, a linked non-cancelled Deviation or CAPA is required (existing `QualityRecordLink` or `FollowUpRecordId` in `ConcludeComplaintRequest`, refused with `Complaint.FollowUpRequired`); optional otherwise (CONF-234). Signs `ComplaintConcluded`; status Concluded.
- Close (`Quality.Approve`; Concluded; not raiser or owner): no unfinished action (`Complaint.UnfinishedActions`). Signs `ComplaintClosed` with meaning `QualityClosed`; status Closed.
- Cancel (`Quality.Approve`; Received, Investigation, LabReported or Concluded): signs `QualityCancelled`; status Cancelled.
**Code:** `backend/MicroLIMS.Application/Services/Quality/ComplaintService.cs` — `ReceiveAsync`, `RequestInvestigationAsync`, `SaveInvestigationAsync`, `SignLabReportAsync`, `ConcludeAsync`, `CloseAsync`, `CancelAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-233, CONF-234

### FS-QLT-011
*(Draft — pending approval)*
**Satisfies:** URS-QLT-009
**Function:** Links. `QualityLinkService.AddAsync` (`Quality.Raise`, through `MutateAsync`) refuses a self-link (`Quality.SelfLink`), an affected-record link on a non-change-control record or outside the affected types, an unreadable or missing target (`Quality.NotFound`, from `QualityLinkTargets.EnsureReadableAsync`), and a duplicate target (`Quality.DuplicateLink`). `RemoveAsync` (owner or `Quality.Investigate`) refuses once the record has any signed step (`Quality.SignedLink`). `ResolveAsync` returns a paged list (page size normalised by `Paging.Normalize`) in which each target that the caller can read carries its number, label and route; a target the caller cannot read carries its number only and `IsRestricted = true`. `ForTargetAsync` lists the quality records, readable by the caller, that link a given target. `QualityLinkTargets.EnsureAccessAsync` applies the existing scope rule for each target type, for example `scope.EnsureSampleAccessAsync` for a sample, `OosView` with section scope and analyst ownership for an OOS investigation, `AuditView` for an audit finding, and `DocumentAuthorizationService` for a controlled document.
**Code:** `backend/MicroLIMS.Application/Services/Quality/QualityLinkService.cs` — `AddAsync`, `RemoveAsync`, `ResolveAsync`, `ForTargetAsync`; `backend/MicroLIMS.Application/Services/Quality/QualityLinkTargets.cs` — `EnsureReadableAsync`, `EnsureAccessAsync`, `ResolveBatchAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-221, CONF-238

### FS-QLT-012
*(Draft — pending approval)*
**Satisfies:** URS-QLT-010
**Function:** Attachments and comments. `QualityAttachmentService.UploadAsync` (`Quality.Raise`) takes the file name without its path, refuses a name over 255 characters or with invalid characters, and runs `DiscussionAttachmentPolicy.Validate` (allowed extension, not empty, at most 10 MB, and a magic-byte signature for types that have one) (`Quality.InvalidAttachment`). It stores the SHA-256 hash, and stores the content through `IFileStorageService` under the key `quality/{recordId}/{guid}_{name}`. The route limits the request to 10 MB plus 1 MB. `GetContentAsync` (`Quality.View` and readable record) reads the stored bytes, recomputes the SHA-256, and on a mismatch writes a `FileIntegrityVerificationFailed` audit entry and refuses delivery. On success it writes a `QualityAttachmentDownloaded` audit entry. A voided attachment is still served, and the controller sets the header `X-Quality-Attachment-Voided`. `VoidAsync` (owner or `Quality.Investigate`) requires a reason of 1 to 4000 characters, sets `VoidedAtUtc`, `VoidedByUserId` and `VoidReason`, and keeps the row. `QualityCommentService.AddAsync` (`Quality.Raise`) stores text of 1 to 2000 characters; there is no edit or delete operation. `ListAsync` pages comments for readable records.
**Code:** `backend/MicroLIMS.Application/Services/Quality/QualityAttachmentService.cs` — `UploadAsync`, `GetContentAsync`, `VoidAsync`; `backend/MicroLIMS.Application/Services/Quality/QualityCommentService.cs` — `AddAsync`, `ListAsync`; `backend/MicroLIMS.API/Controllers/QualityRecordsController.cs` — `UploadAttachment`, `DownloadAttachment`, `VoidAttachment`, `AddComment`
**Verified by:** reading (not run)
**Conflicts:** CONF-221, CONF-238

### FS-QLT-013
*(Draft — pending approval)*
**Satisfies:** URS-QLT-011, URS-QLT-012
**Function:** Checklist templates and answers. Templates are keyed by type and section; a section template (when it exists) is used in place of the site template (`QualityConfigService.EffectiveTemplates`). `GetTemplateAsync` requires `Quality.View` and read access to the section. `SaveTemplateAsync` requires `Quality.Investigate` and configure scope for the section (or `Quality.Approve` for the site template); keys are unique and 1 to 36 characters, texts are 1 to 4000 characters, and the table is locked (`LOCK TABLE`). The version is the PostgreSQL row version of the template row (`xmin`), and a difference is refused with `Quality.Stale`. Removed items are deleted from the template, and answers keep their text. `AnswerAsync` (owner or `Quality.Investigate`, section member, record not terminal: `Quality.RecordTerminal`) refuses a value other than Yes, No or NotApplicable (`Quality.InvalidAnswer`), refuses an item that is not active in the effective template, stores the item text as it is at that time, and limits the comment to 2000 characters. For a change control, answers are refused once the impact assessment is signed (`ChangeControl.AssessmentAlreadySigned`). `ListAnswersAsync` returns the latest answer for each key. `AreAllActiveItemsAnsweredAsync` returns true when there are no active items.
**Code:** `backend/MicroLIMS.Application/Services/Quality/QualityConfigService.cs` — `GetTemplateAsync`, `SaveTemplateAsync`, `AnswerAsync`, `RecordAnswerInternalAsync`, `AreAllActiveItemsAnsweredAsync`, `ListAnswersAsync`, `EffectiveTemplates`; `backend/MicroLIMS.API/Controllers/Quality/QualityConfigController.cs` — `GetTemplate`, `SaveTemplate`, `Answer`
**Verified by:** reading (not run)

### FS-QLT-014
*(Draft — pending approval)*
**Satisfies:** URS-QLT-011, URS-QLT-012
**Function:** Classifications and due rules. `SaveClassificationAsync` requires `Quality.Approve`, a name of 1 to 100 characters that is unique (`Quality.DuplicateClassification`), a locked table and a matching version. `SaveDueRuleAsync` requires `Quality.Approve`, a valid type, working days of zero or more or empty (`Quality.InvalidWorkingDays`), and an existing classification when one is given; there is one rule per type and classification. The list calls require `Quality.View` and are paged, ordered by `Order` then `Id`. `QualityDueDateService.ComputeDueAsync` selects the rule for the classification if it has a non-empty `WorkingDays`, otherwise falls back to the site rule with a non-empty `WorkingDays`, and returns `PlanWindowCalculator.AddWorkingDays` from the start date over the working weekdays of `MonitoringSettings` (default when absent) and the laboratory calendar dates from the start date. When neither rule has `WorkingDays`, it returns null (CONF-239).
**Code:** `backend/MicroLIMS.Application/Services/Quality/QualityConfigService.cs` — `ListClassificationsAsync`, `SaveClassificationAsync`, `ListDueRulesAsync`, `SaveDueRuleAsync`; `backend/MicroLIMS.Application/Services/Quality/QualityDueDateService.cs` — `ComputeDueAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-225, CONF-239

### FS-QLT-015
*(Draft — pending approval)*
**Satisfies:** URS-QLT-011
**Function:** Extensions and overdue listing. `RequestAsync` (`Quality.View`, section member, record not terminal) locks the record and, for a record-level request, requires the caller to be the record owner; for an action-level request, the action's responsible user or the record owner (CONF-237). The new date must be later than the current due date (`Quality.ExtensionDate`); when the current due date is empty the comparison refuses nothing. The reason is 1 to 4000 characters. Nothing is signed. `DecideAsync` (`Quality.Approve`) refuses an already-decided request (`Quality.ExtensionAlreadyDecided`), a request by the same user (`Quality.ExtensionSelfDecision`), and a decision by the record's raiser or owner (`Quality.RaiserOrOwner`, CONF-228), refuses a stale request version, and, for approval, requires a later date. It signs `QualityExtensionDecided` with a snapshot of the request, the previous and new dates and the decision (`QualityConfigService.Extensions.cs`). Inside that transaction, after the signature is verified and before the save, an approved date replaces the record's due date or the action's due date. The request then stores the decision, the decider, the time, the comment and the signature ID. A failed signature check is reported as `Quality.ExtensionSignatureFailed`. `ListExtensionsAsync` returns a readable record's requests, newest first. `ListOverdueAsync` (`Quality.View`) returns open records whose due date is before the laboratory date and actions not done whose due date is before that date, each page at most the requested size.
**Code:** `backend/MicroLIMS.Application/Services/Quality/QualityConfigService.Extensions.cs` — `RequestAsync`, `DecideAsync`, `ListExtensionsAsync`, `ListOverdueAsync`; `backend/MicroLIMS.Application/Services/Quality/QualityConfigService.cs` — `EnsureOpenAsync`, `IsOpenExpression`
**Verified by:** reading (not run)
**Conflicts:** CONF-228, CONF-237

### FS-QLT-016
*(Draft — pending approval)*
**Satisfies:** URS-QLT-008, URS-QLT-013
**Function:** Endpoints and authorisation. Routes: `api/quality/deviations`, `api/quality/capas`, `api/quality/change-controls`, `api/quality/complaints`, `api/quality` (configuration, extensions and overdue) and `api/quality` (records, links, attachments, comments) in `QualityRecordsController`. Each endpoint has a permission policy (`PermissionConstants.Quality*`, or `PermissionPolicyProvider.QualityApproveOrInvestigate`). The policy is necessary but not sufficient: where a service makes the decision, for example the CAPA plan save, the complaint investigation save and the CAPA action completion, the route policy is `Quality.View` and the service refuses the caller. Every change to an existing record requires an `If-Match` version (`RecordVersionMiddleware.ParseExpectedVersion`); a missing version is refused with `Quality.VersionRequired`. Creation routes do not take a version. `backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt` lists the role types permitted by each of the 66 quality controller endpoints.
**Code:** `backend/MicroLIMS.API/Controllers/Quality/DeviationsController.cs`; `backend/MicroLIMS.API/Controllers/Quality/CapasController.cs`; `backend/MicroLIMS.API/Controllers/Quality/ChangeControlsController.cs`; `backend/MicroLIMS.API/Controllers/Quality/ComplaintsController.cs`; `backend/MicroLIMS.API/Controllers/Quality/QualityConfigController.cs`; `backend/MicroLIMS.API/Controllers/QualityRecordsController.cs`; `backend/MicroLIMS.API/Authorization/PermissionPolicyProvider.cs`
**Verified by:** reading (not run)
**Conflicts:** CONF-230

### FS-QLT-017
*(Draft — pending approval)*
**Satisfies:** URS-QLT-006, URS-QLT-012
**Function:** Permissions and default grants. The five codes are `Quality.View`, `Quality.Raise`, `Quality.Investigate`, `Quality.LabConclude` and `Quality.Approve` (`PermissionConstants`). `Quality.Approve` is in `SegregationOfDutiesRules.QaOnlyCodes`; `Quality.Investigate` and `Quality.LabConclude` are in `GxpExecutionAndApprovalCodes`, so QA cannot hold them. Default grants seeded by `DbSeeder`: SectionHead holds View, Raise, Investigate and LabConclude; Reviewer and Analyst hold View and Raise; QcManager holds View (through the reviewer read set), Raise and LabConclude; QA holds View, Raise and Approve (`PermissionConstants.QaGrants`). `Quality.Approve` is therefore held by QA by default, and the Section Head holds the investigation rights. The seeded QcManager does not hold `Quality.Investigate` (CONF-236).
**Code:** `backend/MicroLIMS.Shared/Constants/PermissionConstants.cs` — `QualityView` to `QualityApprove`, `QaGrants`; `backend/MicroLIMS.Shared/Constants/SegregationOfDutiesRules.cs` — `QaOnlyCodes`, `GxpExecutionAndApprovalCodes`; `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs` — default role grants and permission descriptions
**Verified by:** reading (not run)
**Conflicts:** CONF-236

### FS-QLT-018
*(Draft — pending approval)*
**Satisfies:** URS-QLT-008
**Function:** Master-data writes that quality records depend on (the Task 8 fix, merged as commit 9ed6ced). `TestWorkflowStepMasterDataService` takes the caller's user ID on the workflow-type update and on the create, update, move and delete of a step, and checks `TestConfigurationScope.EnsureCanConfigureAsync` for the test's section. The update, move and delete of a step, and the workflow-type update, call `RecordVersion.EnsureCurrent` on the stored row. `TestDefinitionMasterDataService.FreezeTestDefinitionAsync` and `UnfreezeTestDefinitionAsync` take the user ID, check the same scope and version, and then run the change-reason guard (`_reasonGuard.EnsureAsync`) before saving.
**Code:** `backend/MicroLIMS.Application/Services/MasterData/TestWorkflowStepMasterDataService.cs` — `UpdateWorkflowTypeAsync`, `UpdateTestWorkflowStepAsync`, `MoveTestWorkflowStepAsync`, `DeleteTestWorkflowStepAsync`; `backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs` — `FreezeTestDefinitionAsync`, `UnfreezeTestDefinitionAsync`
**Verified by:** reading (not run). The Task 8 tests (`TestConfigurationWriteScopePostgresTests`) were not run for this document.

### FS-QLT-019
*(Draft — pending approval)*
**Satisfies:** URS-QLT-008, URS-QLT-013, URS-QLT-014, URS-QLT-015
**Function:** Queries, full detail, PDF report and dashboard (D2 Task 10). `QualityQueryService.ListAsync` (`GET api/quality/records`) filters readable records by type, status (including `Cancelled`), classification, section, owner, overdue flag (reusing `QualityConfigService.IsOpenExpression` / `IsNotOpenExpression`) and raised date range (a date-only `to` includes the entire day), using `AsNoTracking`, SQL projections and `Paging.Normalize` (page size capped at 100). `GetDetailAsync` (`GET api/quality/records/{id}`) projects the header, type detail, path steps, signed timeline (signer full name, UTC time with `UTC` zone, `ReportVocabulary` meaning text), actions, attachments and links count, refusing out-of-scope records with `Quality.NotFound`. `QualityReportBuilder.GeneratePdfAsync` (`GET api/quality/records/{id}/pdf`) renders the PDF via `IPdfGenerator.GenerateReportAsync` with restricted links shown as number/code + `"Restricted"` only, appends `"N more linked records not shown"` when links exceed the 100-link cap, and logs the export via `DataExportAuditService` (`QualityRecordPdf`). `GetDashboardAsync` (`GET api/quality/dashboard`) returns role-scoped lists each capped at 50 items plus `TotalCount` (O5), enforces SoD and completeness rules on `WaitingForMyDecision` (complete non-returned CAPA plans; deviations ready to close only when `CapaRequired == false` or an approved CAPA exists via `DeviationService.IsApprovedCapaExpression`), applies O2 for overdue actions, and provides `LaboratoryOpenRecords` and `LaboratoryOpenActions` for Section Head users (O5).
**Code:** `backend/MicroLIMS.Application/Services/Quality/QualityQueryService.cs`; `backend/MicroLIMS.Application/Services/Quality/QualityReportBuilder.cs`; `backend/MicroLIMS.API/Controllers/QualityRecordsController.cs`
**Verified by:** `QualityQueriesPostgresTests`, `QualityTrendsAndDashboardPostgresTests`
**Conflicts:** CONF-241

### FS-QLT-020
*(Draft — pending approval)*
**Satisfies:** URS-QLT-013, URS-QLT-015
**Function:** Quality trending (D2 Task 10). `QualityQueryService.GetTrendsAsync` (`GET api/quality/trends`) defaults `from`/`to` to the last 365 laboratory days, caps the window at 5 years (`Quality.TrendWindowTooLarge` beyond), and treats a date-only `to` as inclusive of the whole day (O4). It groups readable records by `type`, `classification`, `cause`, `section`, `item` (O1: direct `Item` link, else `ComplaintDetail.ItemId`, else `Sample.ItemId` via linked `Sample`, `TestOrder`, or `OosInvestigation`) or `source` (refusing unknown values with `Quality.TrendGroupByInvalid`), grouping in SQL for `type`, `classification`, `section` and `cause`. Cancelled and rejected records are excluded from time-to-close, on-time rate, CAPA effectiveness rate and repeat-cause detection, and are reported as `CancelledCount` and `RejectedCount` per group while `TotalCount` counts all (O3). Repeat-cause detection reports only the contiguous cluster inside `repeatWindowDays` and masks out-of-scope `Equipment` names as `"{Code} (Restricted)"`.
**Code:** `backend/MicroLIMS.Application/Services/Quality/QualityQueryService.cs` — `GetTrendsAsync`
**Verified by:** `QualityTrendsAndDashboardPostgresTests`
**Conflicts:** CONF-241

### FS-QLT-021
*(Draft — pending approval)*
**Satisfies:** URS-QLT-011, URS-QLT-016
**Function:** Quality notifications and overdue background job (D2 Task 10). `OosRecipientResolver` implements `IPermissionRecipientResolver` (`ResolveAsync(int? sectionId, string permissionCode, CancellationToken)`), preserving existing OOS behaviour while supporting site-wide quality records. `QualityNotifier` sends post-commit notifications (`QualityRecordRaised`, `QualityRecordAssigned` when owner != creator, `QualityRecordReturned`, `QualityAwaitingQa` including deduped CAPA plan save and last CAPA action / CC task completion, `QualityActionAssigned`, `QualityRecordClosed`, `QualityRecordRejected`, `QualityRecordCancelled`) using `CancellationToken.None`; all exceptions including cancellation are logged and never fail or roll back a committed step. `QualityOverdueWorker` (`BackgroundJobClassification.Gxp`, `QualityOverdue:Enabled`, `QualityOverdue:IntervalMinutes` default 60) acquires a PostgreSQL transaction advisory lock (`AdvisoryLockKeys.QualityOverdueWorker`) and runs `QualityNotifier.NotifyOverdueAsync` using `ILabClock.LabToday`, `QualityConfigService.IsActionExecutionActiveExpression` (O2), and batch dedupe via `NotificationPublisher.PublishBatchAsync` to emit at most one notification per recipient per item per laboratory day.
**Code:** `backend/MicroLIMS.Application/Services/Notifications/IPermissionRecipientResolver.cs`; `backend/MicroLIMS.Application/Services/Oos/OosRecipientResolver.cs`; `backend/MicroLIMS.Application/Services/Quality/QualityNotifier.cs`; `backend/MicroLIMS.API/BackgroundServices/QualityOverdueWorker.cs`; `backend/MicroLIMS.Application/Services/BackgroundJobs/BackgroundJobCatalog.cs`
**Verified by:** `QualityNotificationsAndWorkerPostgresTests`
**Conflicts:** CONF-241

### FS-QLT-022
*(Draft — pending approval)*
**Satisfies:** URS-QLT-017
**Function:** Gated master-data writes (D2 Task 9). `ChangeControlGate` runs inside the caller's transaction and refuses with `ChangeControl.GateTransactionRequired` when there is none. For each laboratory section that the write touches it takes a shared row lock on the section's `QualitySectionSettings` row. When `ChangeControlRequired` is false for every such section, the write proceeds unchanged and the `X-Change-Control` value is ignored. When it is true, the write needs the change-control number from the header (set by the middleware, FS-QLT-023): a missing number is refused with `ChangeControl.GateRequired`; a number with no change control record of type ChangeControl is refused with `ChangeControl.GateNotFound`; a cancelled change control, or one not in `ApprovedForImplementation`, with `ChangeControl.GateNotApproved`; a change control whose laboratory differs from the write's section with `ChangeControl.GateWrongLaboratory`; and a change control with no matching affected link with `ChangeControl.GateRecordNotListed`. Only affected links (`IsAffectedByChange`) are considered. A link matches the record by `TargetId`. A link with no `TargetId` (a created test definition) matches by `NewRecordCode`, trimmed and case-insensitive. A specification create matches an Item link by item ID or item code, or a TestDefinition link by test ID or test code (`EnsureSpecificationCreateAllowedAsync`). Specification update, delete and reinstate match only the specification's own link by its ID, so a listed item or test does not cover edits to existing specifications. The 16 gated writes are: specification create, update, delete and reinstate; test definition create, update, freeze and unfreeze; test workflow type update, step create, update, move and delete; and test stage replicate create, update and delete. Other writes are not gated (CONF-227). After a gated write, `RecordAppliedAsync` looks for the audit row that this write created for the entity. When it exists, the matched link gets `ChangedAtUtc` from the injected clock and `ChangeAuditLogId`, and a repeated change overwrites both. When no audit row is created (a write that changes nothing), the link is not changed (CONF-227 D1). The audit reason of the write is set to `Change control {number}` when the gate applied.
**Code:** `backend/MicroLIMS.Application/Services/Quality/ChangeControlGate.cs` — `EnsureAllowedAsync`, `EnsureAllowedForSectionsAsync`, `EnsureSpecificationCreateAllowedAsync`, `RecordAppliedAsync`; `backend/MicroLIMS.Application/Services/MasterData/SpecificationMasterDataService.cs` — `CreateSpecificationAsync`, `UpdateSpecificationAsync`, `DeleteSpecificationAsync`, `ReinstateSpecificationAsync`; `backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs` — `CreateTestDefinitionAsync`, `UpdateTestDefinitionAsync`, `FreezeTestDefinitionAsync`, `UnfreezeTestDefinitionAsync`; `backend/MicroLIMS.Application/Services/MasterData/TestWorkflowStepMasterDataService.cs` — `UpdateWorkflowTypeAsync`, `CreateTestWorkflowStepAsync`, `UpdateTestWorkflowStepAsync`, `MoveTestWorkflowStepAsync`, `DeleteTestWorkflowStepAsync`; `backend/MicroLIMS.Application/Services/MasterData/TestStageReplicateMasterDataService.cs` — `CreateTestDefinitionStageReplicateAsync`, `UpdateTestDefinitionStageReplicateAsync`, `DeleteTestDefinitionStageReplicateAsync`
**Verified by:** reading (not run). `ChangeControlGatePostgresTests` and `ChangeControlGateFixesPostgresTests` were not run for this document.
**Conflicts:** CONF-227, CONF-232

### FS-QLT-023
*(Draft — pending approval)*
**Satisfies:** URS-QLT-018
**Function:** Gate switch and the change-control header. `QualityConfigService.SetSectionSettingAsync` (`PUT api/quality/section-settings/{sectionId}`) switches `ChangeControlRequired` for one laboratory. It requires `Quality.Approve`, and refuses a blank reason (`ChangeControl.GateReasonRequired`); the reason is stored as entered, up to 4000 characters. An unknown section is refused. The transaction takes a table lock on `QualitySectionSettings`. When a settings row exists, the expected version (`If-Match`) is required (`Quality.VersionRequired` when missing) and a different version is refused with `ChangeControl.GateVersionStale`; a first save with a non-zero expected version is refused the same way. The electronic signature (user identity and password, meaning `SignatureMeaning.ChangeControlGateChanged`, entity `QualitySectionSetting`, record ID = section ID) is created before the flag changes, and the audit reason is the entered reason. `GET api/quality/section-settings/{sectionId}` returns the stored value and version, or `false` with version 0 when no row exists. `ChangeControlHeaderMiddleware` reads `X-Change-Control` on every request before the endpoint runs. A request with more than one value, or with a value that does not match `^[A-Z0-9-]{1,20}$` after trimming and upper-casing, is refused with `ChangeControl.HeaderInvalid` (HTTP 400), whether or not the gate is on for the laboratory. A valid value is stored in the request-scoped change-control context.
**Code:** `backend/MicroLIMS.Application/Services/Quality/QualityConfigService.cs` — `GetSectionSettingAsync`, `SetSectionSettingAsync`; `backend/MicroLIMS.API/Controllers/Quality/QualityConfigController.cs` — section-settings routes; `backend/MicroLIMS.API/Middleware/ChangeControlHeaderMiddleware.cs` — `InvokeAsync`; `backend/MicroLIMS.API/Extensions/ApplicationBuilderExtensions.cs` — middleware registration
**Verified by:** reading (not run)
**Conflicts:** CONF-227

### FS-QLT-024
*(Draft — pending approval)*
**Satisfies:** URS-QLT-019
**Function:** Item deletion and affected specification links (D2 Task 9, CONF-227 D2 and D3). `ItemService.DeleteAsync` keeps its existing cascade to the item's specifications, without the gate. After the not-applicable check it refuses with `Item.DeleteBlockedByChangeControlGate` when any specification of the item has a test definition (matched by test code) in a laboratory whose gate is on. `ChangeControlService.BuildAffectedLinkAsync` builds every affected link, for both proposing and adding an affected record. Affected types are Item, Specification, TestDefinition, Equipment and DocumentMaster (`ChangeControl.InvalidAffectedType`). A Specification link without a `TargetId` is refused with `ChangeControl.SpecificationRequiresId` ("List a new specification by its item or test."); a link with a `TargetId` must be readable by the caller. A link of any type other than Specification without a `TargetId` needs a `NewRecordCode` of up to 100 characters (`RequiredText`); a link without either value is refused with `ChangeControl.InvalidAffectedTarget`.
**Code:** `backend/MicroLIMS.Application/Services/ItemService.cs` — `DeleteAsync`; `backend/MicroLIMS.Application/Services/Quality/ChangeControlService.cs` — `BuildAffectedLinkAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-227
