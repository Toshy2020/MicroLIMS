# Verified roles, permissions, approvals, signatures, scope (verifier output)

Verifier pass over `backend-permissions-roles-auth.md` (the "Haiku file"). Every fact re-read from source on 2026-10-15. Paths are relative to `E:\MicroLIMS\MicroLIMS`. Tags: CONFIRMED / WRONG / UNVERIFIABLE / REQUIRES CONFIRMATION.

## 0. Verdict on the Haiku file

- Permission catalogue (50 codes, descriptions, Enforced flags, the 10 non-enforced codes): CONFIRMED.
- Role grant lists and counts (SysAdmin 50, SectionHead 43, Reviewer 15, Analyst 15): CONFIRMED against DbSeeder (counted by script, not by eye) and consistent with the migrations.
- WRONG or incomplete (3):
  1. `Kpi.ViewTeam` "no controller policy use". Wrong. It is checked as a claim in `KpiDashboardController.cs:22` and `KpiWorkloadController.cs:30` (`User.HasClaim("permission", PermissionConstants.KpiViewTeam)`), not as an `[Authorize(Policy)]`. [src: backend/MicroLIMS.API/Controllers/KpiDashboardController.cs:L22]
  2. `Discussions.EditAny` "no controller policy use". Wrong. `DiscussionController.cs:24` checks the claim; the frontend also gates on it (`routes.ts:237`, `DiscussionDetailPage.tsx:49`). [src: backend/MicroLIMS.API/Controllers/DiscussionController.cs:L24]
  3. `Documents.Register` "no controller policy use". Technically true but misleading. It is enforced in `DocumentAuthorizationService.CanRegisterDocumentMasterAsync` by reading the role's DB grants. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L84-L87]
- Missed by Haiku: (a) the "migrations were not traced" gap, now traced in section 2; (b) roles are admin-editable, including system roles (section 3); (c) lockout counter is not reset when the 15 min lock expires (section 9); (d) `PUT media-products/{id}/code` is `[Authorize(Roles = SectionHead)]` so a System Administrator is refused (section 3).
- Line citations spot-checked (DbSeeder L452-L501 catalogue, L523-L575 grants, L516 guard; JwtConfiguration; AuthenticationService constants L20-L24; DashboardController L86/L110): all CONFIRMED.

## 1. Permission codes, enforced flags, and the 10 outside `Enforced`

- 50 codes declared in `PermissionConstants.All`. [src: backend/MicroLIMS.Shared/Constants/PermissionConstants.cs:L109-L128]
- `Enforced` is a `HashSet` of 40 codes. The seeder copies membership into `Permission.IsEnforced` for newly inserted rows. [src: backend/MicroLIMS.Shared/Constants/PermissionConstants.cs:L133-L143], [src: backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:L505-L510]
- Meaning (source comment): "The codes some endpoint or service actually checks. The rest are declared for the Roles screen but not yet enforced anywhere." [src: backend/MicroLIMS.Shared/Constants/PermissionConstants.cs:L130-L132]
- UI: the Roles matrix shows a green "Enforced" chip or a warning "Legacy-only" chip per code. [src: frontend/src/modules/roles/components/PermissionMatrix.tsx:L70-L83]
- For custom roles the UI says: the role "behaves like <base type> on screens not yet migrated ... Custom permissions take full effect on everything marked Enforced". [src: frontend/src/modules/roles/RoleDetailPage.tsx:L148-L152]
- Caveat: `IsEnforced` is only written when a permission row is first inserted (seeder) or by a migration UPDATE. Editing `Enforced` later does not update existing rows. REQUIRES CONFIRMATION on prod that all 40 rows are TRUE (migrations flip them: EnforcePermissionBasedAuthorization, AddWorkflowProfileVersions, SeedKpiViewTeamPermission; no migration found that flips Solutions.Prepare, Hplc.Operate, WorkingStandards.Qualify, Documents.Register, Documents.ConfigManage beyond the list in EnforcePermissionBasedAuthorization).

Classification of the 10 codes outside `Enforced`. Search covered all backend code except Tests and Migrations, plus `frontend/src` (including the `PERMISSIONS` map at `routes.ts:217-252`, which contains none of them). Result: none of the 10 is referenced anywhere outside the constants file and the seeder.

| Code | Class | Evidence |
|---|---|---|
| Discussions.View | NOT-ENFORCED | No controller/service/frontend reference. `DiscussionController` is plain `[Authorize]`. [src: backend/MicroLIMS.API/Controllers/DiscussionController.cs:L13] |
| Discussions.Create | NOT-ENFORCED | same |
| Messages.Use | NOT-ENFORCED | no reference |
| System.ViewSecurityAudit | NOT-ENFORCED | no reference (migration comment says "no endpoint checks it yet", [src: backend/MicroLIMS.Persistence/Migrations/20260909204150_AddSecurityAuditEvents.cs:L86-L90]). SysAdmin-only grant, so the security audit trail has no enforcing endpoint. |
| Documents.DraftEdit | NOT-ENFORCED | Document Control uses fixed RoleType sets and ownership, not this code. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L100-L135] |
| Documents.RevisionCreate | NOT-ENFORCED | same |
| Documents.Review | NOT-ENFORCED | review authority = assignment + RoleType sets (`DocumentReviewService`) |
| Documents.Approve | NOT-ENFORCED | approval authority = designated approver + SoD (`DocumentApprovalService`) |
| Documents.PeriodicReview | NOT-ENFORCED | `PeriodicReviewService` role sets |
| Documents.TrainingViewMatrix | NOT-ENFORCED | training matrix controller uses `Documents.TrainingAssign` only |

No code in this set is ENFORCED-ELSEWHERE or UI-ONLY. For the guide: do not tell users that granting or revoking these 10 changes anything.

Other enforcement details (corrections to how Haiku described "policy use"):
- Most enforced codes are `[Authorize(Policy = code)]` on controllers. [src: backend/MicroLIMS.API/Authorization/PermissionPolicyProvider.cs:L27-L36], handler checks the `permission` claim. [src: backend/MicroLIMS.API/Authorization/PermissionAuthorizationHandler.cs:L13]
- Claim-checked in code (not attribute): `Samples.Receive` (decides any-lab vs own-lab receipt) [src: backend/MicroLIMS.API/Controllers/SampleController.cs:L89, L102], `Kpi.ViewTeam`, `Discussions.EditAny`.
- DB-checked: `Documents.Register`.
- Per-controller code lists in the Haiku table: spot-checked for every one of the 50 codes by git grep; all match except the three above. `ItemsManage` also gates `PreparationConfigurationQueueController`, which lives in the same file as `ItemPreparationConfigurationController` [src: backend/MicroLIMS.API/Controllers/ItemPreparationConfigurationController.cs:L54]. CONFIRMED.

## 2. Effective grants after migrations plus DbSeeder

### 2.1 Mechanism
- `DbSeeder.Seed` runs at startup from Program.cs (twice in different branches). [src: backend/MicroLIMS.API/Program.cs:L154, L162]
- Roles are created only if `db.Roles` is empty. [src: backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:L23-L32]
- Permission rows: inserted per missing code on every startup. [src: backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:L504-L515]
- Role grants: inserted ONLY if `db.RolePermissions` is entirely empty. [src: backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:L516-L592]
- Therefore: DbSeeder does NOT re-sync grants at startup. It never overrides migrations and never overrides Roles-screen edits. On an existing database grants change only via migrations or the Roles screen. A fresh database gets the full seeder default set in one go (migrations ran earlier against empty Roles, so their role-grant INSERTs no-op; they say so in comments, e.g. [src: backend/MicroLIMS.Persistence/Migrations/20260908183413_AddErrorMonitoringEntities.cs:L122-L126]).
- Migration grants are idempotent `INSERT ... WHERE NOT EXISTS` joined on `Roles.Type`, so they hit every role of that base type, including custom roles. They never delete grants. [src: backend/MicroLIMS.Persistence/Migrations/20260928122856_EnforcePermissionBasedAuthorization.cs:L11-L21]

### 2.2 Migrations that touch permissions or role grants (all found)
| Migration | Effect | Role types granted |
|---|---|---|
| 20260729201340_InitialCreate | creates tables only | none |
| 20260823193318_AddRoleManagementFields | `IsSystemRole = TRUE, IsActive = TRUE` for all existing roles [src: ...L37] | n/a |
| 20260823200247_AddPermissionIsEnforced | adds `IsEnforced` column | n/a |
| 20260908183413_AddErrorMonitoringEntities | adds System.ViewErrorLog | 0 (SysAdmin) |
| 20260908210723_EnforceSystemViewErrorLogPermission | IsEnforced = TRUE for ViewErrorLog | n/a |
| 20260909204150_AddSecurityAuditEvents | adds System.ViewSecurityAudit (not enforced) | 0 |
| 20260911182155_SeedDocumentControlPermissions | adds 9 Documents.* codes. Grants: 0 gets all 9; 1 gets Register, DraftEdit, RevisionCreate, PeriodicReview, Approve, TrainingAssign, TrainingViewMatrix; 2 gets Review, PeriodicReview, Approve, TrainingViewMatrix; 3 gets Register, DraftEdit, RevisionCreate [src: ...L52-L80] | 0-3 |
| 20260924192621_LabSeparationPrivileges | Samples.Receive, Samples.TrackAll | 0, 1 |
| 20260928122856_EnforcePermissionBasedAuthorization | adds 10 codes; grants 59 (role,code) pairs; sets IsEnforced for 35 codes [src: ...L49-L117] | 0-3 |
| 20260929173324_AddSolutionPreparation | Solutions.Prepare | 0, 1, 3 |
| 20260929184541_AddHplcWorkspace | Hplc.Operate | 0, 1, 3 |
| 20261003105411_AddWorkingStandards | WorkingStandards.Qualify | 0, 1, 3 |
| 20261013204200_AddWorkflowProfileVersions | Kpi.ConfigureProfiles (+ enforced) | 0, 1 |
| 20261014160538_SeedKpiViewTeamPermission | Kpi.ViewTeam (+ enforced) | 0, 1 |

No migration deletes a grant on Up (only Down methods delete). Role type numbers: 0 SystemAdministrator, 1 SectionHead, 2 Reviewer, 3 Analyst. [src: backend/MicroLIMS.Domain/Enums/RoleType.cs]

### 2.3 Result
Default grants from the migration path (existing databases) equal the DbSeeder path (fresh databases) for every code I could trace. The only untraceable part is the pre-migration baseline (original seeder rows for older codes such as Users.Manage, Samples.Approve, Items.Manage, Equipment.Manage, Discussions.*, Messages.Use, TestWorkflow.Execute): no migration inserts them, so they come from whichever seeder version first created the database. REQUIRES CONFIRMATION on prod (read-only `Roles`/`RolePermissions` query) and note any admin edits on the Roles screen are invisible to code analysis.

Counts (SectionHead 43 = 50 minus Users.Manage, Roles.Manage, Reporting.Admin, System.ViewErrorLog, System.ViewSecurityAudit, Documents.Review, Documents.ConfigManage):

| Role | Effective default permission codes | Count | Source |
|---|---|---|---|
| SystemAdministrator | all 50 | 50 | [src: backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:L523] plus migrations in 2.2 |
| SectionHead ("Document Controller" in Document Control comments) | Audit.View, Samples.Review, Samples.Approve, Samples.Receive, Samples.TrackAll, Signatures.Manage, TestWorkflow.Execute, TestWorkflow.BiochemicalDecision, Cryovials.Manage, Cryovials.Approve, Materials.Manage, Materials.DocumentControl, Equipment.Manage, Equipment.DocumentControl, Items.Manage, Items.DocumentUpload, MasterData.Manage, Discussions.View, Discussions.Create, Discussions.EditAny, Messages.Use, Documents.Register, Documents.DraftEdit, Documents.RevisionCreate, Documents.PeriodicReview, Documents.Approve, Documents.TrainingAssign, Documents.TrainingViewMatrix, Dashboards.LabOverview, Dashboards.Review, Kpi.View, Kpi.ViewTeam, Kpi.ConfigureProfiles, Media.Prepare, Media.Release, Oos.Manage, TestWorkflow.Supervise, Samples.ReceiveOwnLab, Samples.Correct, Samples.AssignAnalyst, Solutions.Prepare, Hplc.Operate, WorkingStandards.Qualify | 43 | [src: backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:L524-L546] |
| Reviewer | Samples.Review, TestWorkflow.Execute, TestWorkflow.BiochemicalDecision, Cryovials.Manage, Discussions.View, Discussions.Create, Messages.Use, Documents.Review, Documents.PeriodicReview, Documents.Approve, Documents.TrainingViewMatrix, Dashboards.Review, Media.Prepare, Samples.ReceiveOwnLab, Samples.Correct | 15 | [src: backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:L547-L559] |
| Analyst | TestWorkflow.Execute, Cryovials.Manage, Materials.Manage, Equipment.Manage, Discussions.View, Discussions.Create, Messages.Use, Documents.Register, Documents.DraftEdit, Documents.RevisionCreate, Media.Prepare, Samples.ReceiveOwnLab, Solutions.Prepare, Hplc.Operate, WorkingStandards.Qualify | 15 | [src: backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:L560-L576] |

Notable for the guide: Analyst has no Samples.Review, Samples.Approve, Samples.Correct, Samples.AssignAnalyst, Items.Manage, MasterData.Manage, Kpi.View, Dashboards.*. Reviewer has no Samples.Approve and no Samples.Receive. Only SectionHead and SysAdmin hold Samples.Approve. SysAdmin does not sign off by exception: SoD applies (section 5).

## 3. Are roles fixed or editable?

Editable. Four system roles are seeded as defaults only; an administrator with `Roles.Manage` can:
- Create custom roles with a chosen base type (`BaseType` = one of the 4 RoleTypes); a new role starts with the permission set of the base system role and can then be narrowed or widened. [src: backend/MicroLIMS.Application/Services/RoleService.cs:L63-L89], [src: backend/MicroLIMS.API/Controllers/RoleController.cs:L41-L46]
- Rename any role (system roles included) and edit its description. [src: backend/MicroLIMS.Application/Services/RoleService.cs:L91-L106]
- Replace the permission set of any role, system roles included; no guard exists against changing the System Administrator or SectionHead set (including removing Roles.Manage). Writes a `ROLE_PERMISSIONS_UPDATED` audit entry with granted/revoked lists. [src: backend/MicroLIMS.Application/Services/RoleService.cs:L126-L171]
- Delete custom roles only; system roles refuse deletion; roles assigned to users refuse deletion. [src: backend/MicroLIMS.Application/Services/RoleService.cs:L108-L124]
- The UI hides the Delete button for system roles. [src: frontend/src/modules/roles/RoleDetailPage.tsx:L116]
- `GET api/permissions` lists codes with `isEnforced`. [src: backend/MicroLIMS.API/Controllers/RoleController.cs:L70]
So the guide must say "default grants" not "fixed". 
Limits of editing:
- Changing a user's role: cannot change your own role. [src: backend/MicroLIMS.Application/Services/UserService.cs:L163-L172]
- Permission edits reach signed-in users only on the next access token (default lifetime 15 min); role-type change, disable, lock, or password change take effect on the next request. [src: backend/MicroLIMS.API/Extensions/JwtConfiguration.cs:L30-L34]
- The JWT role claim is the role's base TYPE, not the custom role name. Everything that still checks the RoleType (below) treats a custom role like its base type.

Role-type (not permission) checks still in force, so permission edits do not change them:
- `[Authorize(Roles = SectionHead)]` on `PUT api/masterdata/media-products/{id}/code`: only role type SectionHead; System Administrator is refused (not in the role list). Also needs signed code change with password and reason. [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L52-L56]
- Dashboard "analyst" endpoints return Forbid unless role type is Analyst. [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L86, L110]
- Analyst-only filtering of own tests in the testing workspace (an Analyst-type user only sees tests assigned to them, unless scope is "all"). [src: backend/MicroLIMS.Application/Services/GroupedTestActionService.cs:L78, L477]
- Document Control authority: fixed RoleType sets (SystemAdministrator/SectionHead/Reviewer) plus per-document assignment. [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L100-L135] (Haiku line list for these services not individually re-verified; claim of "hard-coded role sets" CONFIRMED by the samples read.)
- Lab scope bypass for SystemAdministrator type (section 7); last-active-SystemAdministrator deletion guard [src: backend/MicroLIMS.Application/Services/UserDeletionService.cs:L65-L68]; notification audiences [src: backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:L171, L185]; analyst picker lists only Analyst-type users [src: backend/MicroLIMS.Application/Services/UserService.cs:L89, SampleApprovalService.cs:L274].

## 4. Segregation of duties: see section 5. (numbering below follows the brief)

## 5. Segregation of duties (exact rules and messages)

Core guard is role-agnostic: compares user ids only, so no role (SystemAdministrator included) can bypass it. [src: backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs:L6-L10]

"Performed a test" (DidUserPerformTestAsync) is true if the user: is the assigned analyst of the test order; prepared the sample (SamplePreparation); entered any Result, CountTestReading, TestAnalysis; observed a PathogenObservation; submitted a WorkflowStepResult; or recorded a ConfirmatoryPlateObservation on that order. [src: backend/MicroLIMS.Application/Services/SegregationOfDutiesGuard.cs:L25-L54]

| Where | Rule | Error text | src |
|---|---|---|---|
| Sample technical review | Reviewer must not have performed any test in the section being reviewed | "You cannot review a sample you tested." | backend/MicroLIMS.Application/Services/SampleReviewService.cs:L109-L112 |
| Sample approval | Approver must not be the section's reviewer | "You cannot approve a sample you reviewed." | backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L162-L163 |
| Sample approval | Approver must not have performed any test in the section | "You cannot approve a sample you tested." | backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L167-L170 |
| Pathogen biochemical review decision | Reviewer must not have performed the test | "A reviewer cannot decide on a result they performed." (error code SEGREGATION_OF_DUTIES_VIOLATION) | backend/MicroLIMS.Application/Workflows/TestWorkflow/PathogenConfirmationRecorder.cs:L780-L782 |
| Media lot release | Approver must not have prepared the lot or read any challenge on its evaluation | "You cannot approve a media lot you prepared or evaluated." | backend/MicroLIMS.Application/Services/MediaReleaseService.cs:L81-L82 |
| Cryovial batch approval | Approver must not have prepared it | "You cannot approve a cryovial batch you prepared." | backend/MicroLIMS.Application/Services/CryovialService.cs:L151-L152 |
| Working standard qualification | Submitter cannot review; submitter cannot approve; reviewer cannot approve; reviewer cannot reject at approval | "The analyst who submitted the qualification cannot review it." / "...cannot approve it." / "The reviewer cannot also approve the qualification." / "The reviewer cannot also reject the qualification at approval." | backend/MicroLIMS.Application/Services/WorkingStandardService.SignOff.cs:L22-L23, L85-L88, L76 |
| Master data workbook | Person who loaded the workbook cannot verify it | "The person who loaded the workbook cannot verify it. Ask a second person." | backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L199-L200 |
| Document revision (assigning approver) | Author and technical reviewers cannot be assigned as approver | "Segregation of Duties Violation: The author of a document revision cannot be assigned as its approver." / "...A technical reviewer for this revision cannot be assigned as its approver." | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L646-L665 |
| Document revision (approval decision) | Author or reviewer cannot decide | "Segregation of Duties Violation: The author of a document revision cannot approve or decide upon their own revision." / "...A technical reviewer for this revision cannot approve or decide upon this revision." | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L667-L687 |
| Document review | Author cannot be assigned as, or act as, technical reviewer; cannot add or verify reviewer findings | messages "Segregation of Duties Violation: The author ... " (several) | backend/MicroLIMS.Application/Services/DocumentControl/DocumentReviewService.cs:L104-L110, L283-L285, L418-L420, L480, L553-L555 |
| Periodic review | Author cannot be periodic reviewer ("System Administrator is NOT exempt") | "Segregation of Duties Violation: The author cannot perform or complete the periodic review." | backend/MicroLIMS.Application/Services/DocumentControl/PeriodicReviewService.cs:L552-L554 |
| Account | cannot change own role, cannot disable self, cannot permanently delete self | "You cannot permanently delete your own account." | backend/MicroLIMS.Application/Services/UserService.cs:L165, L208; UserDeletionService.cs:L63 |

Chain implied for a sample: whoever performed or prepared a test (analyst) cannot review it; whoever reviewed cannot approve it; whoever performed cannot approve it. Reviewer and approver may be any two different eligible users who did not perform. Not found: a check that the reviewer and the approver of an HPLC run/other non-sample record differ beyond the working-standard rules. REQUIRES CONFIRMATION if the guide needs HPLC run submit/review SoD (grep of SegregationOfDutiesGuard users shows only the five consumers above).

## 6. Electronic signatures

Capture: the user re-types their login password; the service checks BCrypt against the stored hash, refuses inactive or locked users, and writes an immutable `ElectronicSignature` row. Same generic failure for unknown, inactive, locked, wrong password: "Password verification failed. The signature was not applied." [src: backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs:L65-L81]
- Throttle: after 5 failed signature attempts in 15 min, further attempts are refused without checking the password: "Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied." Signature failures never lock the login account. [src: backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs:L12-L18, L40-L57, L67]
- Stored: user id; full name, username and role-type snapshots; `MeaningOfSignature` (enum); `EntityType`, `EntityId`; `SignedAt`; optional `Comment`; `IpAddress`. Append-only by design. [src: backend/MicroLIMS.Domain/Entities/ElectronicSignature.cs:L4-L30], [src: backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs:L83-L97]
- Meanings (17): Reviewed, Approved, Rejected, RetestRequested, InvestigationOrdered, PreparationConfirmed, SampleCorrected, SampleVoided, MasterDataChanged, SuitabilityRunPerformed, ResultRecorded, CalibrationRunPerformed (retired), CalibrationRunWithdrawn (retired), TestingClosed, LaboratoryAdded, TitrantStandardized, TitrantDueAcknowledged. [src: backend/MicroLIMS.Domain/Enums/SignatureMeaning.cs]
- "Comment/reason" is a free-text field per endpoint; many require a reason (corrections, voids, code change, add laboratory). Whether every action requires a non-empty comment is not uniform: REQUIRES CONFIRMATION per action.
- Signing is done inside the same transaction as the state change; a failed password throws before the change is written (ReviewGateService comment). [src: backend/MicroLIMS.Application/Services/ReviewGateService.cs:L30-L37]
- Trail: `GET` signature trail for a record requires `Signatures.Manage`. [src: backend/MicroLIMS.Application/Services/ElectronicSignatureService.cs:L101-L106]

Signature-required actions (every call site of `SignAsync`, code search over Application, API, Infrastructure):

1. Sample technical review (Reviewed) [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L121-L123]
2. Sample approval decision: Approve / Reject / New sample request / Retest retained sample (Approved, Rejected, RetestRequested) [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L294-L305]
3. Section testing closed (TestingClosed) [src: backend/MicroLIMS.Application/Services/SectionClosureService.cs:L62-L64]
4. Add a laboratory to a sample (LaboratoryAdded) [src: backend/MicroLIMS.Application/Services/AddLaboratoryService.cs:L53-L54]
5. Sample correction (SampleCorrected) [src: backend/MicroLIMS.Application/Services/SampleCorrectionService.cs:L218]
6. Sample void (SampleVoided) [src: backend/MicroLIMS.Application/Services/SampleCorrectionService.cs:L265]
7. Sample preparation confirmation (PreparationConfirmed) [src: backend/MicroLIMS.Application/Services/SamplePreparationService.cs:L184-L185]
8. Cryovial batch approve / reject (Approved / Rejected) [src: backend/MicroLIMS.Application/Services/CryovialService.cs:L157-L159]
9. Media lot release approve / reject [src: backend/MicroLIMS.Application/Services/MediaReleaseService.cs:L87-L89]
10. Media product code change (MasterDataChanged) [src: backend/MicroLIMS.Application/Services/MediaProductService.cs:L137-L140]
11. Solution preparation completion (PreparationConfirmed) [src: backend/MicroLIMS.Application/Services/SolutionPreparationService.cs:L280-L281]
12. Titrant standardization (TitrantStandardized) [src: backend/MicroLIMS.Application/Services/TitrantStandardizationService.cs:L244-L245]
13. Titrant-due acknowledgement when running a titration (TitrantDueAcknowledged) [src: backend/MicroLIMS.Application/Workflows/TestWorkflow/TitrationRecorder.cs:L349-L353]
14. HPLC system suitability confirmation (SuitabilityRunPerformed) [src: backend/MicroLIMS.Application/Services/HplcRunService.cs:L524-L525]
15. ICP calibration (SuitabilityRunPerformed) [src: backend/MicroLIMS.Application/Services/IcpRunService.Calibration.cs:L131-L132]
16. Working standard qualification: replicate submission (ResultRecorded) [src: backend/MicroLIMS.Application/Services/HplcRunService.WorkingStandards.cs:L159-L160]
17. Working standard review (Reviewed) [src: backend/MicroLIMS.Application/Services/WorkingStandardService.SignOff.cs:L26]
18. Working standard approval (Approved) [src: backend/MicroLIMS.Application/Services/WorkingStandardService.SignOff.cs:L91]
19. Working standard rejection (Rejected) [src: backend/MicroLIMS.Application/Services/WorkingStandardService.SignOff.cs:L135]
20. KPI workflow profile: sign a draft version and a profile creation (MasterDataChanged) [src: backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs:L80-L81, L116-L117]
21. Master data workbook: load (MasterDataChanged) and second-person verify (Reviewed) [src: backend/MicroLIMS.Application/Services/MasterDataWorkbook/MasterDataWorkbookService.cs:L150, L216]
22. Document revision approval (Approved), password re-authentication required, error "Password re-authentication is required to apply an electronic signature for approval (21 CFR Part 11)." [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:L530, L557-L560]
23. Recording a result for physicochemical tests (ResultRecorded), single shared path used by HPLC method assay, ICP method assay, titration, and the single-result recorders [src: backend/MicroLIMS.Application/Workflows/TestWorkflow/TestWorkflowSupport.cs:L691-L694; callers HplcMethodAssayRecorder.cs:L138, IcpMethodAssayRecorder.cs:L115, SingleResultRecorder.cs:L92, L227, L314, TitrationRecorder.cs:L349]
24. Dissolution result (ResultRecorded), 2 call sites [src: backend/MicroLIMS.Application/Workflows/TestWorkflow/DissolutionRecorder.cs:L119, L308]
25. Disintegration result (ResultRecorded), 2 call sites [src: backend/MicroLIMS.Application/Workflows/TestWorkflow/DisintegrationRecorder.cs:L88, L304]
26. Weight variation result (ResultRecorded), 2 call sites [src: backend/MicroLIMS.Application/Workflows/TestWorkflow/WeightVariationRecorder.cs:L101, L352]

Count: 26 distinct signature-required actions (32 `SignAsync` call sites; several actions have two sites). Not found signed: microbiology plate-count entry, pathogen steps, biochemical confirmation decision, incubator or equipment changes, user or role administration (role permission edits are audit-logged with the acting user, but not e-signed; role change needs a typed reason only). REQUIRES CONFIRMATION that the micro count/pathogen workflows really never prompt for a password (searches for SignAsync callers show none, and `PersistTestAnalysisAndFinalize` is used by physchem recorders only).

## 7. Lab membership and scoping

- Two laboratory section codes: `MICRO` (Microbiology) and `FP` (Physicochemical). [src: backend/MicroLIMS.Shared/Constants/LaboratoryCodes.cs:L7-L8] They are `DocumentSection` rows. A user's lab = their `UserOrgMembership` rows (department, optional section, optional physchem area). Memberships are set by an administrator holding `Users.Manage` (`LaboratoryOrganizationController`), replaced wholesale, audit-captured under the acting user. [src: backend/MicroLIMS.Application/Services/LaboratoryOrganizationService.cs:L61-L98], [src: backend/MicroLIMS.API/Controllers/LaboratoryOrganizationController.cs:L9]
- A department-only membership grants all active sections of that department; a section membership grants that section. [src: backend/MicroLIMS.Application/Services/UserSectionScopeService.cs:L42-L77]
- No membership: scope is empty (user sees no lab data). [src: backend/MicroLIMS.Application/Services/UserSectionScopeService.cs:L37-L40]
- SystemAdministrator ROLE TYPE: unrestricted (null scope). [src: backend/MicroLIMS.Application/Services/UserSectionScopeService.cs:L26-L29] Custom roles based on SystemAdministrator type inherit this. 
- Physicochemical "area" on an FP membership: `fp` or `rmpm` (finished product / raw and packing material); only allowed on FP sections. [src: backend/MicroLIMS.Application/Services/LaboratoryOrganizationService.cs:L87 region; UserSectionScopeService.cs:L81-L95]
- Where scope is applied (callers of `GetAccessibleSectionIdsAsync`): sample review/approval/section closure, dashboards and notifications, KPI/workload, receiving, testing workspace, HPLC/ICP runs and methods, master data (equipment, specifications, test definitions), materials, solutions, working standards, media/cryovial/OOS/water/reporting controllers, My Tasks (about 45 services/controllers). [src: git grep -l GetAccessibleSectionIdsAsync backend]
- Receiving: `Samples.Receive` (any lab, main Receiving page) vs `Samples.ReceiveOwnLab` (own labs only). Error: "You can receive samples only for your own laboratory." [src: backend/MicroLIMS.Application/Services/ReceiptLabGuard.cs:L25-L30], [src: backend/MicroLIMS.API/Controllers/SampleController.cs:L82-L102]
- Dashboards are per lab; a user may only request a lab they belong to: "You are not a member of this laboratory." [src: backend/MicroLIMS.Application/Services/DashboardLabScopeService.cs:L12-L60]
- Frontend: `useMyLabs` calls `getMySections()`; System Administrator is forced to see both `MICRO` and `FP` plus both physchem areas, others only their sections (menus follow). [src: frontend/src/hooks/useMyLabs.ts:L11-L66]; used by `useMenuGroups.ts:L10`, `MainLayout.tsx:L27`, `DashboardPage.tsx:L18`.
- Sample-tracking board across labs requires `Samples.TrackAll` (SectionHead, SysAdmin by default). [src: backend/MicroLIMS.API/Controllers/SampleTrackingController.cs via PermissionConstants.SamplesTrackAll]

## 8. Session timeout (frontend)

- Idle timeout 20 minutes, then a 60-second warning countdown, then automatic sign-out and redirect to `/login`. Tracked activity events: mousemove, keydown, click, scroll. [src: frontend/src/hooks/useIdleTimeout.ts:L6-L8, L41-L45], [src: frontend/src/layouts/MainLayout.tsx:L56-L61]
- Dialog title "Session Timeout Warning"; text "You will be signed out due to inactivity in {N} second(s)."; single button "Stay signed in". Clicking outside or pressing Escape does not close it. [src: frontend/src/layouts/MainLayout.tsx:L153-L170]
- Detail contradicting the code comment: the same activity listeners remain attached while the warning is shown, so any mouse move, key press, click or scroll during the 60 s countdown also cancels the warning (the comment says only the button does). Hook behaviour read directly: `resetIdleTimer` hides the warning. [src: frontend/src/hooks/useIdleTimeout.ts:L41-L54]
- Forced password change: MainLayout redirects every route to the change-password page while `mustChangePassword` is set. [src: frontend/src/layouts/MainLayout.tsx:L63-L66]
- Server side: access token default 15 min (1-480 allowed), refresh token 7 days with rotation; frontend refreshes transparently on 401. No server-side idle timeout. [src: backend/MicroLIMS.API/Extensions/JwtConfiguration.cs:L30-L37, L68-L72], [src: backend/MicroLIMS.Application/Services/AuthenticationService.cs:L23, L392-L399]

## 9. Authentication, lockout, password facts (re-verified)

| Fact | Status | src |
|---|---|---|
| JWT bearer, HMAC-SHA256, issuer/audience/lifetime validated; clock skew 30 s | CONFIRMED | backend/MicroLIMS.API/Extensions/JwtAuthenticationExtensions.cs:L25-L38 |
| Access token default 15 min, range 1-480 | CONFIRMED | backend/MicroLIMS.API/Extensions/JwtConfiguration.cs:L36-L37, L68-L72 |
| Refresh token 7 days, rotated on each refresh | CONFIRMED | backend/MicroLIMS.Application/Services/AuthenticationService.cs:L23, L196-L206 |
| Reset token 1 hour | CONFIRMED | AuthenticationService.cs:L24, L247 |
| Lockout: 5 failures, 15 minutes, locks revoke all refresh tokens, successful login resets counter | CONFIRMED | AuthenticationService.cs:L20, L22, L106-L137 |
| Wrong password and inactive account both count as a failure; locked accounts are refused with the same generic message "Invalid username or password." | CONFIRMED | AuthenticationService.cs:L29, L94-L104 |
| NEW: the failure counter is NOT cleared when the 15 min lock expires, so after expiry one wrong password (counter 6, still >= 5) re-locks for 15 min; only a success, admin unlock, or admin recovery resets it | NEW, derived from code (counter reset only at AuthenticationService.cs:L136, UserService.cs:L275, AdminPasswordRecoveryService.cs:L220) | |
| Admin unlock endpoint clears lock and counter | CONFIRMED | backend/MicroLIMS.Application/Services/UserService.cs:L273-L289 |
| Password policy: min 8 chars, upper, lower, digit, non-alphanumeric; no max length | CONFIRMED | backend/MicroLIMS.Shared/Validation/PasswordPolicy.cs:L8-L24 |
| Password history last 5 | CONFIRMED | AuthenticationService.cs:L21, L313-L323 |
| No password expiry or max age (PasswordChangedAt is used only to invalidate older tokens and display) | CONFIRMED (grep) | backend/MicroLIMS.API/Authorization/AccessTokenRevalidator.cs:L70 |
| Per-request revalidation: user exists, active, not locked, role type unchanged, token not older than last password change. Permission changes are not checked here (token carries permission claims until expiry) | CONFIRMED | AccessTokenRevalidator.cs:L55-L72 |
| Admin recovery code: 5 failed attempts invalidates the code ("Maximum failed recovery attempts exceeded. Recovery code invalidated.") | CONFIRMED | AdminPasswordRecoveryService.cs:L164-L192 |
| Rate limits: auth 20 per 60 s per IP; refresh 60 per 60 s per IP; password change 5 per 900 s per user | CONFIRMED | backend/MicroLIMS.API/Extensions/RateLimitingExtensions.cs:L33-L41, L54-L80 |
| Token claims: NameIdentifier, Role (RoleType name), iat, one `permission` claim per grant | CONFIRMED | backend/MicroLIMS.Infrastructure/Authentication/JwtTokenService.cs:L36-L43 |
| Initial admin only when no users, password from `Seed:InitialAdminPassword`, must meet policy, MustChangePassword true | NOT RE-VERIFIED in detail (UNVERIFIABLE here; Program.cs L138-L178 and DbSeeder L413-L436 not re-read) | |
| Security-event code list | NOT RE-VERIFIED (UNVERIFIABLE here) | |

## 10. Items needing a human or a data check (REQUIRES CONFIRMATION)

1. Prod grants: the 4 default counts above are code defaults; real grants are DB rows, possibly edited on the Roles screen. Run a read-only `RolePermissions` query on the target database before publishing role tables as facts; the guide should call them "default grants".
2. Pre-migration baseline of older codes (see 2.3).
3. `IsEnforced` values on prod rows for the 40 enforced codes (see section 1).
4. Whether any non-sample record (HPLC run review, OOS) has an additional SoD not routed through `SegregationOfDutiesGuard` (section 5).
5. Per-action reason/comment requirements on signatures (section 6).
6. Document Control per-service role sets (DocumentReviewService, DocumentApprovalService, DocumentFileService, DocumentRevisionService, PeriodicReviewService line lists from Haiku were not individually re-read); only DocumentAuthorizationService and the SoD blocks were verified.
