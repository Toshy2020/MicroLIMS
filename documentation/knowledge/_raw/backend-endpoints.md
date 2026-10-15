# Backend endpoint inventory (raw facts)

Generated from `backend/MicroLIMS.API/Controllers/**/*.cs` (77 controller classes, 558 HTTP actions). Read-only extraction; no code run.

## Legend
- Route = class-level `[Route]` + method-level `[HttpX("...")]` template, joined with `/`. Method route starting with `/` is absolute.
- Auth values: `Policy:<value>` = `[Authorize(Policy = PermissionConstants.X)]` resolved to its string (`PermissionConstants.cs`); `Roles:<value>` = role-string attribute; `Authenticated` = bare `[Authorize]`; `Anonymous` = `[AllowAnonymous]`; `NONE` = no `[Authorize]` on class or method; the app FallbackPolicy (RequireAuthenticatedUser, backend/MicroLIMS.API/Extensions/AuthorizationExtensions.cs:17-18) then still requires a signed-in user unless the action is `Anonymous`.
- Effective requirement = class-level AND method-level attributes (both apply in ASP.NET Core).
- `[L##]` = line of the attribute in the file. `src` on each row = line of the HTTP verb attribute.
- Password in body = the request DTO (or a bare `password` parameter) contains a `Password` member. This is the only signature/re-auth signal visible at controller level. The service layer (e.g. `ElectronicSignatureService.SignAsync`, which BCrypt-verifies the password) was NOT traced per action; "No" means no password field at controller level, not that no signature exists downstream.
- Unresolved = parameter DTO type not found by name in backend (none remain).

## Summary
- Controller classes: 77
- HTTP actions: 558
- Actions with a Password member in the request body: 41
- Actions with class-level NONE and no method auth (anonymous or unauthenticated): 6

## AfterCleaningController
File: `backend/MicroLIMS.API/Controllers/AfterCleaningController.cs` [src: backend/MicroLIMS.API/Controllers/AfterCleaningController.cs:L22] — class route: `/api/aftercleaning` — class auth: Authenticated [L21]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| POST | `/api/aftercleaning/receive` | - | - | - | [src: backend/MicroLIMS.API/Controllers/AfterCleaningController.cs:L33] |
| POST | `/api/aftercleaning/prepare` | - | - | - | [src: backend/MicroLIMS.API/Controllers/AfterCleaningController.cs:L46] |

## AfterCleaningMasterDataController
File: `backend/MicroLIMS.API/Controllers/AfterCleaningMasterDataController.cs` [src: backend/MicroLIMS.API/Controllers/AfterCleaningMasterDataController.cs:L22] — class route: `/api/masterdata` — class auth: Authenticated [L21]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/masterdata/machines` | - | - | - | [src: backend/MicroLIMS.API/Controllers/AfterCleaningMasterDataController.cs:L32] |
| POST | `/api/masterdata/machines` | Policy:MasterData.Manage [L36] | - | - | [src: backend/MicroLIMS.API/Controllers/AfterCleaningMasterDataController.cs:L37] |
| PUT | `/api/masterdata/machines/{id}` | Policy:MasterData.Manage [L41] | - | - | [src: backend/MicroLIMS.API/Controllers/AfterCleaningMasterDataController.cs:L42] |
| DELETE | `/api/masterdata/machines/{id}` | Policy:MasterData.Manage [L46] | - | - | [src: backend/MicroLIMS.API/Controllers/AfterCleaningMasterDataController.cs:L47] |
| POST | `/api/masterdata/machine-parts` | Policy:MasterData.Manage [L51] | - | - | [src: backend/MicroLIMS.API/Controllers/AfterCleaningMasterDataController.cs:L52] |
| PUT | `/api/masterdata/machine-parts/{id}` | Policy:MasterData.Manage [L56] | - | - | [src: backend/MicroLIMS.API/Controllers/AfterCleaningMasterDataController.cs:L57] |
| DELETE | `/api/masterdata/machine-parts/{id}` | Policy:MasterData.Manage [L61] | - | - | [src: backend/MicroLIMS.API/Controllers/AfterCleaningMasterDataController.cs:L62] |
| GET | `/api/masterdata/machine-part-configurations` | - | - | - | [src: backend/MicroLIMS.API/Controllers/AfterCleaningMasterDataController.cs:L67] |
| POST | `/api/masterdata/machine-part-configurations` | Policy:MasterData.Manage [L71] | - | - | [src: backend/MicroLIMS.API/Controllers/AfterCleaningMasterDataController.cs:L72] |
| PUT | `/api/masterdata/machine-part-configurations/{id}` | Policy:MasterData.Manage [L76] | - | - | [src: backend/MicroLIMS.API/Controllers/AfterCleaningMasterDataController.cs:L77] |
| DELETE | `/api/masterdata/machine-part-configurations/{id}` | Policy:MasterData.Manage [L81] | - | - | [src: backend/MicroLIMS.API/Controllers/AfterCleaningMasterDataController.cs:L82] |

## ArchivedRecordsController
File: `backend/MicroLIMS.API/Controllers/ArchivedRecordsController.cs` [src: backend/MicroLIMS.API/Controllers/ArchivedRecordsController.cs:L17] — class route: `/api/archived-records` — class auth: Authenticated [L16]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/archived-records` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ArchivedRecordsController.cs:L26] |
| GET | `/api/archived-records/{id}/download` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ArchivedRecordsController.cs:L41] |

## AuditController
File: `backend/MicroLIMS.API/Controllers/AuditController.cs` [src: backend/MicroLIMS.API/Controllers/AuditController.cs:L12] — class route: `/api/admin/audit` — class auth: Policy:Audit.View [L11]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/admin/audit` | - | - | - | [src: backend/MicroLIMS.API/Controllers/AuditController.cs:L24] |
| POST | `/api/admin/audit/search` | - | - | - | [src: backend/MicroLIMS.API/Controllers/AuditController.cs:L30] |
| GET | `/api/admin/audit/{auditLogId:int}/traceability` | - | - | - | [src: backend/MicroLIMS.API/Controllers/AuditController.cs:L35] |
| GET | `/api/admin/audit/traceability` | - | - | - | [src: backend/MicroLIMS.API/Controllers/AuditController.cs:L43] |
| GET | `/api/admin/audit/login-history` | - | - | - | [src: backend/MicroLIMS.API/Controllers/AuditController.cs:L51] |

## AuthenticationController
File: `backend/MicroLIMS.API/Controllers/AuthenticationController.cs` [src: backend/MicroLIMS.API/Controllers/AuthenticationController.cs:L22] — class route: `/api/auth` — class auth: NONE

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| POST | `/api/auth/login` | - | Anonymous (method); EnableRateLimiting(RateLimitingPolicies.Authentication) | LoginRequest.Password | [src: backend/MicroLIMS.API/Controllers/AuthenticationController.cs:L33] |
| POST | `/api/auth/refresh` | - | Anonymous (method); EnableRateLimiting(RateLimitingPolicies.TokenRefresh) | - | [src: backend/MicroLIMS.API/Controllers/AuthenticationController.cs:L44] |
| POST | `/api/auth/password-reset/request` | - | Anonymous (method); EnableRateLimiting(RateLimitingPolicies.Authentication) | - | [src: backend/MicroLIMS.API/Controllers/AuthenticationController.cs:L54] |
| POST | `/api/auth/password-reset/confirm` | - | Anonymous (method); EnableRateLimiting(RateLimitingPolicies.Authentication) | - | [src: backend/MicroLIMS.API/Controllers/AuthenticationController.cs:L71] |
| POST | `/api/auth/admin-password-recovery/confirm` | - | Anonymous (method); EnableRateLimiting(RateLimitingPolicies.Authentication) | - | [src: backend/MicroLIMS.API/Controllers/AuthenticationController.cs:L80] |
| POST | `/api/auth/change-password` | Authenticated [L97] | EnableRateLimiting(RateLimitingPolicies.PasswordChange) | - | [src: backend/MicroLIMS.API/Controllers/AuthenticationController.cs:L96] |
| POST | `/api/auth/logout` | Authenticated [L109] | - | - | [src: backend/MicroLIMS.API/Controllers/AuthenticationController.cs:L108] |
| GET | `/api/auth/me` | Authenticated [L119] | - | - | [src: backend/MicroLIMS.API/Controllers/AuthenticationController.cs:L118] |

## CryovialController
File: `backend/MicroLIMS.API/Controllers/CryovialController.cs` [src: backend/MicroLIMS.API/Controllers/CryovialController.cs:L24] — class route: `/api/cryovials` — class auth: Policy:Cryovials.Manage [L23]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/cryovials` | - | - | - | [src: backend/MicroLIMS.API/Controllers/CryovialController.cs:L39] |
| POST | `/api/cryovials/prepare` | - | - | - | [src: backend/MicroLIMS.API/Controllers/CryovialController.cs:L42] |
| POST | `/api/cryovials/{id}/approve` | Policy:Cryovials.Approve [L50] | - | ApproveRequest.Password | [src: backend/MicroLIMS.API/Controllers/CryovialController.cs:L49] |
| POST | `/api/cryovials/{id}/destroy` | - | - | - | [src: backend/MicroLIMS.API/Controllers/CryovialController.cs:L58] |
| POST | `/api/cryovials/{id}/thaw` | - | - | - | [src: backend/MicroLIMS.API/Controllers/CryovialController.cs:L66] |
| POST | `/api/cryovials/{id}/move-storage` | - | - | - | [src: backend/MicroLIMS.API/Controllers/CryovialController.cs:L74] |
| GET | `/api/cryovials/{id}/summary` | - | - | - | [src: backend/MicroLIMS.API/Controllers/CryovialController.cs:L81] |
| GET | `/api/cryovials/{id}/summary/pdf` | - | - | - | [src: backend/MicroLIMS.API/Controllers/CryovialController.cs:L90] |
| GET | `/api/cryovials/{id}/summary/word` | - | - | - | [src: backend/MicroLIMS.API/Controllers/CryovialController.cs:L99] |

## DashboardController
File: `backend/MicroLIMS.API/Controllers/DashboardController.cs` [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L14] — class route: `/api/dashboard` — class auth: Authenticated [L13]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/dashboard` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L43] |
| GET | `/api/dashboard/kpi-deltas` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L46] |
| GET | `/api/dashboard/monthly-trend` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L49] |
| GET | `/api/dashboard/category-distribution` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L52] |
| GET | `/api/dashboard/status-distribution` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L55] |
| GET | `/api/dashboard/notifications` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L58] |
| POST | `/api/dashboard/notifications/{id}/read` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L64] |
| POST | `/api/dashboard/notifications/read-all` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L71] |
| GET | `/api/dashboard/recent-activity` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L78] |
| GET | `/api/dashboard/my-tasks` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L83] |
| GET | `/api/dashboard/todays-work` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L90] |
| GET | `/api/dashboard/incubation-overview` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L93] |
| GET | `/api/dashboard/section-head` | Policy:Dashboards.LabOverview [L98] | - | - | [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L97] |
| GET | `/api/dashboard/reviewer` | Policy:Dashboards.Review [L103] | - | - | [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L102] |
| GET | `/api/dashboard/analyst-metrics` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DashboardController.cs:L107] |

## DiscussionController
File: `backend/MicroLIMS.API/Controllers/DiscussionController.cs` [src: backend/MicroLIMS.API/Controllers/DiscussionController.cs:L14] — class route: `/api/discussions` — class auth: Authenticated [L13]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/discussions` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DiscussionController.cs:L26] |
| GET | `/api/discussions/{id}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DiscussionController.cs:L39] |
| POST | `/api/discussions` | - | RequestSizeLimit(DiscussionAttachmentPolicy.MaxRequestBytes) | - | [src: backend/MicroLIMS.API/Controllers/DiscussionController.cs:L55] |
| PUT | `/api/discussions/{id}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DiscussionController.cs:L91] |
| PATCH | `/api/discussions/{id}/important` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DiscussionController.cs:L98] |
| DELETE | `/api/discussions/{id}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DiscussionController.cs:L105] |
| GET | `/api/discussions/{id}/history` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DiscussionController.cs:L112] |
| GET | `/api/discussions/{id}/attachments/{attachmentId}/download` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DiscussionController.cs:L119] |
| POST | `/api/discussions/{id}/comments` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DiscussionController.cs:L128] |
| PUT | `/api/discussions/{id}/comments/{commentId}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DiscussionController.cs:L135] |
| DELETE | `/api/discussions/{id}/comments/{commentId}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DiscussionController.cs:L142] |

## DocumentAcknowledgementController
File: `backend/MicroLIMS.API/Controllers/DocumentControl/DocumentAcknowledgementController.cs` [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentAcknowledgementController.cs:L14] — class route: `/api/document-control/acknowledgements` — class auth: Authenticated [L12]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/document-control/acknowledgements/assignments/{assignmentId:int}/context` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentAcknowledgementController.cs:L32] |
| POST | `/api/document-control/acknowledgements/assignments/{assignmentId:int}/submit` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentAcknowledgementController.cs:L39] |
| POST | `/api/document-control/acknowledgements/assignments/{assignmentId:int}/reading-progress` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentAcknowledgementController.cs:L57] |
| GET | `/api/document-control/acknowledgements/assignments/{assignmentId:int}/status` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentAcknowledgementController.cs:L69] |

## DocumentApprovalController
File: `backend/MicroLIMS.API/Controllers/DocumentControl/DocumentApprovalController.cs` [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentApprovalController.cs:L16] — class route: `/api/document-control` — class auth: Authenticated [L14]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| POST | `/api/document-control/revisions/{revisionId:int}/approvals` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentApprovalController.cs:L28] |
| GET | `/api/document-control/revisions/{revisionId:int}/active-approval` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentApprovalController.cs:L36] |
| GET | `/api/document-control/approvals/{approvalTaskId:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentApprovalController.cs:L43] |
| GET | `/api/document-control/approvals` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentApprovalController.cs:L50] |
| GET | `/api/document-control/approvals/{approvalTaskId:int}/dossier` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentApprovalController.cs:L60] |
| GET | `/api/document-control/approvals/{approvalTaskId:int}/readiness` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentApprovalController.cs:L67] |
| POST | `/api/document-control/approvals/{approvalTaskId:int}/decide` | - | - | ExecuteApprovalDecisionRequest.Password | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentApprovalController.cs:L74] |
| POST | `/api/document-control/approvals/{approvalTaskId:int}/sign` | - | - | SignApprovalRequest.Password | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentApprovalController.cs:L89] |
| GET | `/api/document-control/approvals/{approvalTaskId:int}/signature` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentApprovalController.cs:L111] |

## DocumentAuditController
File: `backend/MicroLIMS.API/Controllers/DocumentControl/DocumentAuditController.cs` [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentAuditController.cs:L15] — class route: `/api/document-control` — class auth: Authenticated [L13]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/document-control/audit` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentAuditController.cs:L27] |
| GET | `/api/document-control/documents/{id:int}/audit` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentAuditController.cs:L34] |
| POST | `/api/document-control/audit/export` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentAuditController.cs:L41] |

## DocumentConfigurationController
File: `backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs` [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L15] — class route: `/api/document-control/config` — class auth: Authenticated [L13]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/document-control/config/types` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L29] |
| POST | `/api/document-control/config/types` | Policy:Documents.ConfigManage [L37] | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L36] |
| PUT | `/api/document-control/config/types/{id:int}` | Policy:Documents.ConfigManage [L56] | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L55] |
| GET | `/api/document-control/config/departments` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L72] |
| POST | `/api/document-control/config/departments` | Policy:Documents.ConfigManage [L80] | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L79] |
| PUT | `/api/document-control/config/departments/{id:int}` | Policy:Documents.ConfigManage [L99] | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L98] |
| GET | `/api/document-control/config/departments/{departmentId:int}/sections` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L113] |
| POST | `/api/document-control/config/sections` | Policy:Documents.ConfigManage [L121] | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L120] |
| PUT | `/api/document-control/config/sections/{id:int}` | Policy:Documents.ConfigManage [L140] | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L139] |
| GET | `/api/document-control/config/numbering` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L156] |
| PUT | `/api/document-control/config/numbering` | Policy:Documents.ConfigManage [L164] | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L163] |
| GET | `/api/document-control/config/settings` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L180] |
| PUT | `/api/document-control/config/settings/{key}` | Policy:Documents.ConfigManage [L188] | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentConfigurationController.cs:L187] |

## DocumentControlController
File: `backend/MicroLIMS.API/Controllers/DocumentControl/DocumentControlController.cs` [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentControlController.cs:L15] — class route: `/api/document-control` — class auth: Authenticated [L13]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/document-control/library` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentControlController.cs:L27] |
| GET | `/api/document-control/documents/{id:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentControlController.cs:L34] |
| POST | `/api/document-control/documents` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentControlController.cs:L41] |
| PUT | `/api/document-control/documents/{id:int}/metadata` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentControlController.cs:L59] |
| POST | `/api/document-control/documents/{id:int}/void` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentControlController.cs:L77] |
| POST | `/api/document-control/revisions/{revisionId:int}/cancel` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentControlController.cs:L91] |
| POST | `/api/document-control/documents/{id:int}/assignments` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentControlController.cs:L105] |
| POST | `/api/document-control/documents/{id:int}/assignments/{assignmentId:int}/deactivate` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentControlController.cs:L116] |

## DocumentEscalationController
File: `backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs` [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L19] — class route: `/api/document-control/escalations` — class auth: Authenticated [L17]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/document-control/escalations/assignments/{assignmentId:int}/history` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L38] |
| GET | `/api/document-control/escalations/summary/overdue` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L48] |
| POST | `/api/document-control/escalations/process-due` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L63] |
| POST | `/api/document-control/escalations/resolve` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L80] |

## DocumentFilesController
File: `backend/MicroLIMS.API/Controllers/DocumentControl/DocumentFilesController.cs` [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentFilesController.cs:L18] — class route: `/api/document-control` — class auth: Authenticated [L16]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| POST | `/api/document-control/revisions/{revisionId:int}/files` | - | RequestSizeLimit(50 * 1024 * 1024) | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentFilesController.cs:L36] |
| GET | `/api/document-control/files/{fileId:int}/download` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentFilesController.cs:L72] |
| GET | `/api/document-control/files/{fileId:int}/view` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentFilesController.cs:L95] |

## DocumentReviewController
File: `backend/MicroLIMS.API/Controllers/DocumentControl/DocumentReviewController.cs` [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentReviewController.cs:L15] — class route: `/api/document-control` — class auth: Authenticated [L13]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| POST | `/api/document-control/revisions/{id:int}/submit-review` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentReviewController.cs:L27] |
| GET | `/api/document-control/reviews/{id:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentReviewController.cs:L34] |
| GET | `/api/document-control/revisions/{id:int}/review-tasks` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentReviewController.cs:L41] |
| GET | `/api/document-control/reviews/my-tasks` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentReviewController.cs:L48] |
| POST | `/api/document-control/reviews/{id:int}/findings` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentReviewController.cs:L55] |
| POST | `/api/document-control/findings/{id:int}/respond` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentReviewController.cs:L62] |
| POST | `/api/document-control/findings/{id:int}/verify` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentReviewController.cs:L69] |
| POST | `/api/document-control/findings/{id:int}/resolve` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentReviewController.cs:L76] |
| POST | `/api/document-control/reviews/{id:int}/decide` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentReviewController.cs:L83] |

## DocumentRevisionController
File: `backend/MicroLIMS.API/Controllers/DocumentControl/DocumentRevisionController.cs` [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentRevisionController.cs:L15] — class route: `/api/document-control` — class auth: Authenticated [L13]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| POST | `/api/document-control/documents/{masterId:int}/propose-revision` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentRevisionController.cs:L27] |
| POST | `/api/document-control/documents/{masterId:int}/revisions` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentRevisionController.cs:L34] |
| GET | `/api/document-control/revisions/{revisionId:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentRevisionController.cs:L48] |
| GET | `/api/document-control/revisions/{revisionId:int}/change-items` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentRevisionController.cs:L55] |
| POST | `/api/document-control/revisions/{revisionId:int}/change-items` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentRevisionController.cs:L62] |
| PUT | `/api/document-control/change-items/{changeItemId:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentRevisionController.cs:L76] |
| POST | `/api/document-control/change-items/{changeItemId:int}/deactivate` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentRevisionController.cs:L93] |
| POST | `/api/document-control/revisions/{revisionId:int}/findings/{findingId:int}/convert` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentRevisionController.cs:L100] |
| GET | `/api/document-control/revisions/{revisionId:int}/impact-assessment` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentRevisionController.cs:L114] |
| POST | `/api/document-control/revisions/{revisionId:int}/impact-assessment` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentRevisionController.cs:L121] |

## DocumentTrainingAssignmentController
File: `backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingAssignmentController.cs` [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingAssignmentController.cs:L19] — class route: `/api/document-control/training-assignments` — class auth: Authenticated [L17]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/document-control/training-assignments` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingAssignmentController.cs:L38] |
| GET | `/api/document-control/training-assignments/{assignmentId:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingAssignmentController.cs:L54] |
| GET | `/api/document-control/training-assignments/my-assignments` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingAssignmentController.cs:L73] |
| GET | `/api/document-control/training-assignments/users/{userId:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingAssignmentController.cs:L83] |
| POST | `/api/document-control/training-assignments/manual-assign` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingAssignmentController.cs:L98] |
| POST | `/api/document-control/training-assignments/bulk-group-assign` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingAssignmentController.cs:L113] |
| GET | `/api/document-control/training-assignments/calculate-due-date` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingAssignmentController.cs:L128] |
| GET | `/api/document-control/training-assignments/superseded-gap-check` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingAssignmentController.cs:L138] |

## DocumentTrainingMatrixController
File: `backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingMatrixController.cs` [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingMatrixController.cs:L17] — class route: `/api/document-control/training-matrix` — class auth: Authenticated [L15]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/document-control/training-matrix/grid` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingMatrixController.cs:L36] |
| GET | `/api/document-control/training-matrix/kpis` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingMatrixController.cs:L52] |
| GET | `/api/document-control/training-matrix/users/{userId:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingMatrixController.cs:L62] |
| GET | `/api/document-control/training-matrix/documents/{documentMasterId:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentTrainingMatrixController.cs:L80] |

## EMController
File: `backend/MicroLIMS.API/Controllers/EMController.cs` [src: backend/MicroLIMS.API/Controllers/EMController.cs:L15] — class route: `/api/em` — class auth: Authenticated [L14]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| POST | `/api/em/receive` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EMController.cs:L26] |
| POST | `/api/em/prepare` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EMController.cs:L33] |

## EnvironmentalMonitoringMasterDataController
File: `backend/MicroLIMS.API/Controllers/EnvironmentalMonitoringMasterDataController.cs` [src: backend/MicroLIMS.API/Controllers/EnvironmentalMonitoringMasterDataController.cs:L22] — class route: `/api/masterdata` — class auth: Authenticated [L21]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/masterdata/departments` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EnvironmentalMonitoringMasterDataController.cs:L32] |
| POST | `/api/masterdata/departments` | Policy:MasterData.Manage [L36] | - | - | [src: backend/MicroLIMS.API/Controllers/EnvironmentalMonitoringMasterDataController.cs:L37] |
| PUT | `/api/masterdata/departments/{id}` | Policy:MasterData.Manage [L41] | - | - | [src: backend/MicroLIMS.API/Controllers/EnvironmentalMonitoringMasterDataController.cs:L42] |
| DELETE | `/api/masterdata/departments/{id}` | Policy:MasterData.Manage [L46] | - | - | [src: backend/MicroLIMS.API/Controllers/EnvironmentalMonitoringMasterDataController.cs:L47] |
| GET | `/api/masterdata/rooms` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EnvironmentalMonitoringMasterDataController.cs:L51] |
| POST | `/api/masterdata/rooms` | Policy:MasterData.Manage [L55] | - | - | [src: backend/MicroLIMS.API/Controllers/EnvironmentalMonitoringMasterDataController.cs:L56] |
| PUT | `/api/masterdata/rooms/{id}` | Policy:MasterData.Manage [L60] | - | - | [src: backend/MicroLIMS.API/Controllers/EnvironmentalMonitoringMasterDataController.cs:L61] |
| DELETE | `/api/masterdata/rooms/{id}` | Policy:MasterData.Manage [L65] | - | - | [src: backend/MicroLIMS.API/Controllers/EnvironmentalMonitoringMasterDataController.cs:L66] |
| GET | `/api/masterdata/room-test-configurations` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EnvironmentalMonitoringMasterDataController.cs:L71] |
| POST | `/api/masterdata/room-test-configurations` | Policy:MasterData.Manage [L75] | - | - | [src: backend/MicroLIMS.API/Controllers/EnvironmentalMonitoringMasterDataController.cs:L76] |
| PUT | `/api/masterdata/room-test-configurations/{id}` | Policy:MasterData.Manage [L80] | - | - | [src: backend/MicroLIMS.API/Controllers/EnvironmentalMonitoringMasterDataController.cs:L81] |
| DELETE | `/api/masterdata/room-test-configurations/{id}` | Policy:MasterData.Manage [L85] | - | - | [src: backend/MicroLIMS.API/Controllers/EnvironmentalMonitoringMasterDataController.cs:L86] |

## EquipmentDocumentController
File: `backend/MicroLIMS.API/Controllers/EquipmentDocumentController.cs` [src: backend/MicroLIMS.API/Controllers/EquipmentDocumentController.cs:L19] — class route: `(none)` — class auth: NONE

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/inventory/equipment/{equipmentId:int}/documents` | Policy:Equipment.Manage [L32] | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentDocumentController.cs:L31] |
| POST | `/api/inventory/equipment/{equipmentId:int}/documents` | Policy:Equipment.Manage [L41] | RequestSizeLimit(30 * 1024 * 1024)] // 30 MB server transport ceiling | - | [src: backend/MicroLIMS.API/Controllers/EquipmentDocumentController.cs:L40] |
| GET | `/api/inventory/equipment-documents/{documentId:int}/content` | Policy:Equipment.Manage [L65] | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentDocumentController.cs:L64] |
| POST | `/api/inventory/equipment-documents/{documentId:int}/supersede` | Policy:Equipment.DocumentControl [L75] | RequestSizeLimit(30 * 1024 * 1024) | - | [src: backend/MicroLIMS.API/Controllers/EquipmentDocumentController.cs:L74] |
| POST | `/api/inventory/equipment-documents/{documentId:int}/void` | Policy:Equipment.DocumentControl [L102] | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentDocumentController.cs:L101] |

## EquipmentInventoryController
File: `backend/MicroLIMS.API/Controllers/EquipmentInventoryController.cs` [src: backend/MicroLIMS.API/Controllers/EquipmentInventoryController.cs:L27] — class route: `/api/inventory/equipment` — class auth: Policy:Equipment.Manage [L26]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/inventory/equipment` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentInventoryController.cs:L40] |
| GET | `/api/inventory/equipment/{id:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentInventoryController.cs:L44] |
| GET | `/api/inventory/equipment/print` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentInventoryController.cs:L52] |
| GET | `/api/inventory/equipment/active` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentInventoryController.cs:L56] |
| GET | `/api/inventory/equipment/{id:int}/activities` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentInventoryController.cs:L60] |
| GET | `/api/inventory/equipment/{id:int}/history` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentInventoryController.cs:L66] |
| GET | `/api/inventory/equipment/where-is-it` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentInventoryController.cs:L72] |
| POST | `/api/inventory/equipment` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentInventoryController.cs:L76] |
| PUT | `/api/inventory/equipment/{id:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentInventoryController.cs:L82] |
| GET | `/api/inventory/equipment/{id:int}/status-history` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentInventoryController.cs:L91] |

## EquipmentMasterDataController
File: `backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs` [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L22] — class route: `/api/masterdata` — class auth: Authenticated [L21]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/masterdata/equipment` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L40] |
| GET | `/api/masterdata/equipment/{id:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L44] |
| GET | `/api/masterdata/equipment/configured-summary` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L48] |
| POST | `/api/masterdata/equipment/link-inventory/{inventoryId:int}` | Policy:MasterData.Manage [L52] | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L53] |
| PUT | `/api/masterdata/equipment/{id:int}/set-point` | Policy:MasterData.Manage [L60] | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L61] |
| GET | `/api/masterdata/equipment/{id:int}/set-point-history` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L69] |
| GET | `/api/masterdata/equipment/{id:int}/autoclave-programs` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L76] |
| GET | `/api/masterdata/equipment/autoclave-programs/all` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L83] |
| POST | `/api/masterdata/equipment/{id:int}/autoclave-programs` | Policy:MasterData.Manage [L87] | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L88] |
| PUT | `/api/masterdata/equipment/autoclave-programs/{programId:int}` | Policy:MasterData.Manage [L97] | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L98] |
| PUT | `/api/masterdata/equipment/autoclave-programs/{programId:int}/status` | Policy:MasterData.Manage [L106] | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L107] |
| GET | `/api/masterdata/equipment/autoclave-programs/{programId:int}/history` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L114] |
| POST | `/api/masterdata/equipment` | Policy:MasterData.Manage [L118] | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L119] |
| PUT | `/api/masterdata/equipment/{id:int}` | Policy:MasterData.Manage [L123] | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L124] |
| GET | `/api/masterdata/columns` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L129] |
| GET | `/api/masterdata/columns/{id:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L133] |
| POST | `/api/masterdata/columns` | Policy:Equipment.Manage [L137] | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L138] |
| PUT | `/api/masterdata/columns/{id:int}` | Policy:Equipment.Manage [L142] | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L143] |
| PUT | `/api/masterdata/columns/{id:int}/deactivate` | Policy:Equipment.Manage [L147] | - | - | [src: backend/MicroLIMS.API/Controllers/EquipmentMasterDataController.cs:L148] |

## ErrorMonitoringController
File: `backend/MicroLIMS.API/Controllers/ErrorMonitoringController.cs` [src: backend/MicroLIMS.API/Controllers/ErrorMonitoringController.cs:L21] — class route: `/api/error-monitoring` — class auth: Policy:System.ViewErrorLog [L20]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/error-monitoring/incidents` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ErrorMonitoringController.cs:L33] |
| GET | `/api/error-monitoring/incidents/{id:guid}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ErrorMonitoringController.cs:L52] |
| POST | `/api/error-monitoring/incidents/{id:guid}/resolve` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ErrorMonitoringController.cs:L60] |
| POST | `/api/error-monitoring/incidents/{id:guid}/reopen` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ErrorMonitoringController.cs:L69] |
| POST | `/api/error-monitoring/error-logs/{id:guid}/severity-override` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ErrorMonitoringController.cs:L77] |
| GET | `/api/error-monitoring/incidents/export` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ErrorMonitoringController.cs:L86] |

## HplcMethodController
File: `backend/MicroLIMS.API/Controllers/HplcMethodController.cs` [src: backend/MicroLIMS.API/Controllers/HplcMethodController.cs:L16] — class route: `/api/masterdata/hplc-methods` — class auth: Authenticated [L15]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/masterdata/hplc-methods` | - | - | - | [src: backend/MicroLIMS.API/Controllers/HplcMethodController.cs:L22] |
| GET | `/api/masterdata/hplc-methods/{id:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/HplcMethodController.cs:L26] |
| GET | `/api/masterdata/hplc-methods/{id:int}/history` | - | - | - | [src: backend/MicroLIMS.API/Controllers/HplcMethodController.cs:L30] |
| POST | `/api/masterdata/hplc-methods` | Policy:MasterData.Manage [L34] | - | - | [src: backend/MicroLIMS.API/Controllers/HplcMethodController.cs:L35] |
| PUT | `/api/masterdata/hplc-methods/{id:int}` | Policy:MasterData.Manage [L39] | - | - | [src: backend/MicroLIMS.API/Controllers/HplcMethodController.cs:L40] |
| PUT | `/api/masterdata/hplc-methods/{id:int}/active` | Policy:MasterData.Manage [L44] | - | - | [src: backend/MicroLIMS.API/Controllers/HplcMethodController.cs:L45] |

## HplcWorkspaceController
File: `backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs` [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L16] — class route: `/api/hplc-workspace` — class auth: Authenticated [L15]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/hplc-workspace/instruments` | - | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L33] |
| GET | `/api/hplc-workspace/method-options` | - | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L37] |
| GET | `/api/hplc-workspace/instruments/{equipmentId:int}/runs` | - | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L41] |
| GET | `/api/hplc-workspace/runs/{id:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L45] |
| GET | `/api/hplc-workspace/runs/{id:int}/eligible-tests` | - | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L49] |
| GET | `/api/hplc-workspace/samples/{runSampleId:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L53] |
| GET | `/api/hplc-workspace/test-orders/{testOrderId:int}/evidence` | - | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L57] |
| GET | `/api/hplc-workspace/test-orders/{testOrderId:int}/dissolution-standard` | - | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L61] |
| GET | `/api/hplc-workspace/evidence/{id:int}/file` | - | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L65] |
| POST | `/api/hplc-workspace/runs` | Policy:Hplc.Operate [L74] | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L75] |
| PUT | `/api/hplc-workspace/runs/{id:int}/sst` | Policy:Hplc.Operate [L79] | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L80] |
| POST | `/api/hplc-workspace/runs/{id:int}/sst/confirm` | Policy:Hplc.Operate [L84] | - | ConfirmSstRequest.Password | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L85] |
| POST | `/api/hplc-workspace/runs/{id:int}/abandon` | Policy:Hplc.Operate [L89] | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L90] |
| POST | `/api/hplc-workspace/runs/{id:int}/complete` | Policy:Hplc.Operate [L94] | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L95] |
| POST | `/api/hplc-workspace/runs/{id:int}/samples` | Policy:Hplc.Operate [L101] | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L102] |
| POST | `/api/hplc-workspace/runs/{id:int}/samples/{runSampleId:int}/remove` | Policy:Hplc.Operate [L106] | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L107] |
| PUT | `/api/hplc-workspace/samples/{runSampleId:int}/replicates` | Policy:Hplc.Operate [L111] | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L112] |
| POST | `/api/hplc-workspace/samples/{runSampleId:int}/submit` | Policy:Hplc.Operate [L116] | - | SubmitHplcSampleRequest.Password | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L117] |
| GET | `/api/hplc-workspace/runs/{id:int}/eligible-qualifications` | - | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L123] |
| POST | `/api/hplc-workspace/runs/{id:int}/qualifications` | Policy:Hplc.Operate + Policy:WorkingStandards.Qualify [L127,L128] | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L129] |
| GET | `/api/hplc-workspace/qualification-samples/{runSampleId:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L133] |
| PUT | `/api/hplc-workspace/qualification-samples/{runSampleId:int}/replicates` | Policy:Hplc.Operate + Policy:WorkingStandards.Qualify [L137,L138] | - | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L139] |
| POST | `/api/hplc-workspace/qualification-samples/{runSampleId:int}/submit` | Policy:Hplc.Operate + Policy:WorkingStandards.Qualify [L143,L144] | - | SubmitHplcSampleRequest.Password | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L145] |
| POST | `/api/hplc-workspace/runs/{id:int}/evidence` | Policy:Hplc.Operate [L151] | RequestSizeLimit(MaxUploadBytes) | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L152] |
| POST | `/api/hplc-workspace/evidence/{id:int}/supersede` | Policy:Hplc.Operate [L165] | RequestSizeLimit(MaxUploadBytes) | - | [src: backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs:L166] |

## IcpMethodController
File: `backend/MicroLIMS.API/Controllers/IcpMethodController.cs` [src: backend/MicroLIMS.API/Controllers/IcpMethodController.cs:L15] — class route: `/api/masterdata/icp-methods` — class auth: Authenticated [L14]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/masterdata/icp-methods` | - | - | - | [src: backend/MicroLIMS.API/Controllers/IcpMethodController.cs:L21] |
| GET | `/api/masterdata/icp-methods/{id:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/IcpMethodController.cs:L25] |
| GET | `/api/masterdata/icp-methods/{id:int}/history` | - | - | - | [src: backend/MicroLIMS.API/Controllers/IcpMethodController.cs:L29] |
| POST | `/api/masterdata/icp-methods` | Policy:MasterData.Manage [L33] | - | - | [src: backend/MicroLIMS.API/Controllers/IcpMethodController.cs:L34] |
| PUT | `/api/masterdata/icp-methods/{id:int}` | Policy:MasterData.Manage [L38] | - | - | [src: backend/MicroLIMS.API/Controllers/IcpMethodController.cs:L39] |
| PUT | `/api/masterdata/icp-methods/{id:int}/active` | Policy:MasterData.Manage [L43] | - | - | [src: backend/MicroLIMS.API/Controllers/IcpMethodController.cs:L44] |

## IcpWorkspaceController
File: `backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs` [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L16] — class route: `/api/icp-workspace` — class auth: Authenticated [L15]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/icp-workspace/instruments` | - | - | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L33] |
| GET | `/api/icp-workspace/method-options` | - | - | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L37] |
| GET | `/api/icp-workspace/instruments/{equipmentId:int}/runs` | - | - | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L41] |
| GET | `/api/icp-workspace/runs/{id:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L45] |
| GET | `/api/icp-workspace/runs/{id:int}/eligible-tests` | - | - | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L49] |
| GET | `/api/icp-workspace/test-orders/{testOrderId:int}/evidence` | - | - | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L53] |
| GET | `/api/icp-workspace/samples/{runSampleId:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L57] |
| GET | `/api/icp-workspace/evidence/{id:int}/file` | - | - | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L61] |
| POST | `/api/icp-workspace/runs` | Policy:Hplc.Operate [L70] | - | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L71] |
| PUT | `/api/icp-workspace/runs/{id:int}/calibration` | Policy:Hplc.Operate [L75] | - | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L76] |
| POST | `/api/icp-workspace/runs/{id:int}/calibration/confirm` | Policy:Hplc.Operate [L80] | - | ConfirmIcpCalibrationRequest.Password | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L81] |
| POST | `/api/icp-workspace/runs/{id:int}/ccv` | Policy:Hplc.Operate [L85] | - | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L86] |
| POST | `/api/icp-workspace/runs/{id:int}/abandon` | Policy:Hplc.Operate [L90] | - | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L91] |
| POST | `/api/icp-workspace/runs/{id:int}/complete` | Policy:Hplc.Operate [L95] | - | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L96] |
| POST | `/api/icp-workspace/runs/{id:int}/samples` | Policy:Hplc.Operate [L102] | - | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L103] |
| POST | `/api/icp-workspace/runs/{id:int}/samples/{runSampleId:int}/remove` | Policy:Hplc.Operate [L107] | - | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L108] |
| PUT | `/api/icp-workspace/samples/{runSampleId:int}/replicates` | Policy:Hplc.Operate [L112] | - | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L113] |
| POST | `/api/icp-workspace/samples/{runSampleId:int}/submit` | Policy:Hplc.Operate [L117] | - | SubmitHplcSampleRequest.Password | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L118] |
| POST | `/api/icp-workspace/runs/{id:int}/evidence` | Policy:Hplc.Operate [L124] | RequestSizeLimit(MaxUploadBytes) | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L125] |
| POST | `/api/icp-workspace/evidence/{id:int}/supersede` | Policy:Hplc.Operate [L138] | RequestSizeLimit(MaxUploadBytes) | - | [src: backend/MicroLIMS.API/Controllers/IcpWorkspaceController.cs:L139] |

## ItemController
File: `backend/MicroLIMS.API/Controllers/ItemController.cs` [src: backend/MicroLIMS.API/Controllers/ItemController.cs:L15] — class route: `/api/items` — class auth: Authenticated [L14]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/items` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ItemController.cs:L24] |
| POST | `/api/items` | Policy:Items.Manage [L28] | - | - | [src: backend/MicroLIMS.API/Controllers/ItemController.cs:L27] |
| PUT | `/api/items/{id}` | Policy:Items.Manage [L32] | - | - | [src: backend/MicroLIMS.API/Controllers/ItemController.cs:L31] |
| PUT | `/api/items/{id}/freeze` | Policy:Items.Manage [L40] | - | - | [src: backend/MicroLIMS.API/Controllers/ItemController.cs:L39] |
| PUT | `/api/items/{id}/unfreeze` | Policy:Items.Manage [L48] | - | - | [src: backend/MicroLIMS.API/Controllers/ItemController.cs:L47] |
| DELETE | `/api/items/{id}` | Policy:Items.Manage [L56] | - | - | [src: backend/MicroLIMS.API/Controllers/ItemController.cs:L55] |

## ItemDocumentController
File: `backend/MicroLIMS.API/Controllers/ItemDocumentController.cs` [src: backend/MicroLIMS.API/Controllers/ItemDocumentController.cs:L14] — class route: `(none)` — class auth: Authenticated [L13]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/items/{itemId:int}/documents` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ItemDocumentController.cs:L34] |
| POST | `/api/items/{itemId:int}/documents` | Policy:Items.DocumentUpload [L49] | RequestSizeLimit(30 * 1024 * 1024) | - | [src: backend/MicroLIMS.API/Controllers/ItemDocumentController.cs:L48] |
| GET | `/api/item-documents/{documentId:int}/content` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ItemDocumentController.cs:L85] |

## ItemPreparationConfigurationController
File: `backend/MicroLIMS.API/Controllers/ItemPreparationConfigurationController.cs` [src: backend/MicroLIMS.API/Controllers/ItemPreparationConfigurationController.cs:L19] — class route: `/api/items/{itemId:int}/preparation-configuration` — class auth: Authenticated [L18]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/items/{itemId:int}/preparation-configuration` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ItemPreparationConfigurationController.cs:L30] |
| PUT | `/api/items/{itemId:int}/preparation-configuration` | Policy:Items.Manage [L35] | - | - | [src: backend/MicroLIMS.API/Controllers/ItemPreparationConfigurationController.cs:L34] |
| POST | `/api/items/{itemId:int}/preparation-configuration/approve` | Policy:Items.Manage [L46] | - | - | [src: backend/MicroLIMS.API/Controllers/ItemPreparationConfigurationController.cs:L45] |

## KpiDashboardController
File: `backend/MicroLIMS.API/Controllers/KpiDashboardController.cs` [src: backend/MicroLIMS.API/Controllers/KpiDashboardController.cs:L15] — class route: `/api/kpi` — class auth: Policy:Kpi.View [L14]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/kpi/dashboard` | - | - | - | [src: backend/MicroLIMS.API/Controllers/KpiDashboardController.cs:L24] |
| GET | `/api/kpi/{key}/records` | - | - | - | [src: backend/MicroLIMS.API/Controllers/KpiDashboardController.cs:L32] |
| GET | `/api/kpi/definitions` | - | - | - | [src: backend/MicroLIMS.API/Controllers/KpiDashboardController.cs:L41] |

## KpiWorkloadController
File: `backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs` [src: backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L16] — class route: `/api/kpi` — class auth: Policy:Kpi.View [L15]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/kpi/workload` | - | - | - | [src: backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L33] |
| GET | `/api/kpi/workload/tests` | - | - | - | [src: backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L42] |
| GET | `/api/kpi/settings/{sectionId:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L52] |
| PUT | `/api/kpi/settings/{sectionId:int}` | Policy:Kpi.ConfigureProfiles [L56] | - | - | [src: backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L57] |
| GET | `/api/kpi/availability` | - | - | - | [src: backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L61] |
| POST | `/api/kpi/availability` | Policy:Kpi.ConfigureProfiles [L65] | - | - | [src: backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L66] |
| PUT | `/api/kpi/availability/{id:int}` | Policy:Kpi.ConfigureProfiles [L70] | - | - | [src: backend/MicroLIMS.API/Controllers/KpiWorkloadController.cs:L71] |

## LaboratoryOrganizationController
File: `backend/MicroLIMS.API/Controllers/LaboratoryOrganizationController.cs` [src: backend/MicroLIMS.API/Controllers/LaboratoryOrganizationController.cs:L16] — class route: `/api/org` — class auth: Authenticated [L15]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/org/sections` | - | - | - | [src: backend/MicroLIMS.API/Controllers/LaboratoryOrganizationController.cs:L27] |
| GET | `/api/org/my-sections` | - | - | - | [src: backend/MicroLIMS.API/Controllers/LaboratoryOrganizationController.cs:L31] |
| GET | `/api/org/users/{userId}/memberships` | Policy:Users.Manage [L35] | - | - | [src: backend/MicroLIMS.API/Controllers/LaboratoryOrganizationController.cs:L36] |
| PUT | `/api/org/users/{userId}/memberships` | Policy:Users.Manage [L40] | - | - | [src: backend/MicroLIMS.API/Controllers/LaboratoryOrganizationController.cs:L41] |

## MasterDataWorkbookController
File: `backend/MicroLIMS.API/Controllers/MasterDataWorkbookController.cs` [src: backend/MicroLIMS.API/Controllers/MasterDataWorkbookController.cs:L14] — class route: `/api/masterdata/workbook` — class auth: Authenticated [L13]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/masterdata/workbook` | Policy:MasterData.Manage [L23] | - | - | [src: backend/MicroLIMS.API/Controllers/MasterDataWorkbookController.cs:L24] |
| POST | `/api/masterdata/workbook/validate` | Policy:MasterData.Manage [L31] | RequestSizeLimit(MasterDataWorkbookService.MaxFileBytes + 1024 * 1024) | - | [src: backend/MicroLIMS.API/Controllers/MasterDataWorkbookController.cs:L32] |
| POST | `/api/masterdata/workbook/import` | Policy:MasterData.Manage [L44] | RequestSizeLimit(MasterDataWorkbookService.MaxFileBytes + 1024 * 1024) | param password | [src: backend/MicroLIMS.API/Controllers/MasterDataWorkbookController.cs:L45] |
| GET | `/api/masterdata/workbook/imports` | Policy:MasterData.Manage [L50] | - | - | [src: backend/MicroLIMS.API/Controllers/MasterDataWorkbookController.cs:L51] |
| POST | `/api/masterdata/workbook/imports/{id}/verify` | Policy:MasterData.Manage [L55] | - | VerifyMasterDataImportRequest.Password | [src: backend/MicroLIMS.API/Controllers/MasterDataWorkbookController.cs:L56] |
| GET | `/api/masterdata/workbook/imports/{id}/file` | Policy:MasterData.Manage [L60] | - | - | [src: backend/MicroLIMS.API/Controllers/MasterDataWorkbookController.cs:L61] |

## MaterialController
File: `backend/MicroLIMS.API/Controllers/MaterialController.cs` [src: backend/MicroLIMS.API/Controllers/MaterialController.cs:L23] — class route: `/api/inventory/materials` — class auth: Policy:Materials.Manage [L22]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/inventory/materials` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MaterialController.cs:L36] |
| GET | `/api/inventory/materials/usable-reference-standards` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MaterialController.cs:L41] |
| GET | `/api/inventory/materials/print` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MaterialController.cs:L46] |
| GET | `/api/inventory/materials/type-options` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MaterialController.cs:L51] |
| GET | `/api/inventory/materials/default-unit` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MaterialController.cs:L55] |
| POST | `/api/inventory/materials` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MaterialController.cs:L59] |
| PUT | `/api/inventory/materials/{id}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MaterialController.cs:L67] |

## MaterialDocumentController
File: `backend/MicroLIMS.API/Controllers/MaterialDocumentController.cs` [src: backend/MicroLIMS.API/Controllers/MaterialDocumentController.cs:L23] — class route: `(none)` — class auth: Policy:Materials.Manage [L22]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/inventory/materials/{materialId:int}/documents` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MaterialDocumentController.cs:L37] |
| POST | `/api/inventory/materials/{materialId:int}/documents` | - | RequestSizeLimit(30 * 1024 * 1024) | - | [src: backend/MicroLIMS.API/Controllers/MaterialDocumentController.cs:L52] |
| GET | `/api/inventory/material-documents/{documentId:int}/content` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MaterialDocumentController.cs:L77] |
| GET | `/api/inventory/materials/{materialId:int}/coa-eligibility` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MaterialDocumentController.cs:L102] |
| POST | `/api/inventory/material-documents/{documentId:int}/supersede` | Policy:Materials.DocumentControl [L118] | RequestSizeLimit(30 * 1024 * 1024) | - | [src: backend/MicroLIMS.API/Controllers/MaterialDocumentController.cs:L117] |
| POST | `/api/inventory/material-documents/{documentId:int}/void` | Policy:Materials.DocumentControl [L143] | - | - | [src: backend/MicroLIMS.API/Controllers/MaterialDocumentController.cs:L142] |

## MaterialMasterController
File: `backend/MicroLIMS.API/Controllers/MaterialMasterController.cs` [src: backend/MicroLIMS.API/Controllers/MaterialMasterController.cs:L16] — class route: `/api/masterdata/material-masters` — class auth: Authenticated [L15]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/masterdata/material-masters` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MaterialMasterController.cs:L22] |
| POST | `/api/masterdata/material-masters` | Policy:MasterData.Manage [L26] | - | - | [src: backend/MicroLIMS.API/Controllers/MaterialMasterController.cs:L27] |
| PUT | `/api/masterdata/material-masters/{id:int}` | Policy:MasterData.Manage [L31] | - | - | [src: backend/MicroLIMS.API/Controllers/MaterialMasterController.cs:L32] |
| PUT | `/api/masterdata/material-masters/{id:int}/active` | Policy:MasterData.Manage [L36] | - | - | [src: backend/MicroLIMS.API/Controllers/MaterialMasterController.cs:L37] |

## MediaController
File: `backend/MicroLIMS.API/Controllers/MediaController.cs` [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L25] — class route: `/api/media` — class auth: Policy:Media.Prepare [L24]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/media` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L47] |
| GET | `/api/media/expiring` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L51] |
| GET | `/api/media/released` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L55] |
| POST | `/api/media` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L59] |
| GET | `/api/media/awaiting-approval` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L67] |
| POST | `/api/media/{id}/release` | Policy:Media.Release [L74] | - | DecideMediaReleaseRequest.Password | [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L73] |
| POST | `/api/media/{id}/mark-out-of-stock` | Policy:Media.Prepare [L84] | - | - | [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L83] |
| GET | `/api/media/{id}/storage` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L92] |
| POST | `/api/media/{id}/storage` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L99] |
| POST | `/api/media/{id}/storage/{entryId}/move` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L106] |
| GET | `/api/media/{id}/summary` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L113] |
| GET | `/api/media/{id}/summary/pdf` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L122] |
| GET | `/api/media/{id}/summary/word` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaController.cs:L131] |

## MediaEvaluationController
File: `backend/MicroLIMS.API/Controllers/MediaEvaluationController.cs` [src: backend/MicroLIMS.API/Controllers/MediaEvaluationController.cs:L29] — class route: `/api/media-evaluations` — class auth: Policy:TestWorkflow.Execute [L28]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/media-evaluations` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaEvaluationController.cs:L42] |
| GET | `/api/media-evaluations/{id}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaEvaluationController.cs:L46] |
| POST | `/api/media-evaluations/challenges/{challengeId}/cryovial` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaEvaluationController.cs:L53] |
| POST | `/api/media-evaluations/challenges/{challengeId}/lyophilized-disk` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaEvaluationController.cs:L62] |
| POST | `/api/media-evaluations/challenges/{challengeId}/incubation` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaEvaluationController.cs:L70] |
| POST | `/api/media-evaluations/challenges/{challengeId}/result` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaEvaluationController.cs:L77] |

## MediaMasterDataController
File: `backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs` [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L22] — class route: `/api/masterdata` — class auth: Authenticated [L21]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/masterdata/media-products` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L38] |
| POST | `/api/masterdata/media-products` | Policy:MasterData.Manage [L42] | - | - | [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L43] |
| PUT | `/api/masterdata/media-products/{id}` | Policy:MasterData.Manage [L47] | - | - | [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L48] |
| PUT | `/api/masterdata/media-products/{id}/code` | Roles:SectionHead [L52] | - | ChangeMediaProductCodeRequest.Password | [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L53] |
| DELETE | `/api/masterdata/media-products/{id}` | Policy:MasterData.Manage [L58] | - | - | [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L59] |
| GET | `/api/masterdata/media-incubation-conditions` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L67] |
| POST | `/api/masterdata/media-incubation-conditions` | Policy:MasterData.Manage [L71] | - | - | [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L72] |
| PUT | `/api/masterdata/media-incubation-conditions/{id}` | Policy:MasterData.Manage [L77] | - | - | [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L78] |
| DELETE | `/api/masterdata/media-incubation-conditions/{id}` | Policy:MasterData.Manage [L83] | - | - | [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L84] |
| GET | `/api/masterdata/media-configurations` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L92] |
| POST | `/api/masterdata/media-configurations` | Policy:MasterData.Manage [L96] | - | - | [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L97] |
| PUT | `/api/masterdata/media-configurations/{id}` | Policy:MasterData.Manage [L101] | - | - | [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L102] |
| DELETE | `/api/masterdata/media-configurations/{id}` | Policy:MasterData.Manage [L106] | - | - | [src: backend/MicroLIMS.API/Controllers/MediaMasterDataController.cs:L107] |

## MessageController
File: `backend/MicroLIMS.API/Controllers/MessageController.cs` [src: backend/MicroLIMS.API/Controllers/MessageController.cs:L12] — class route: `/api/messages` — class auth: Authenticated [L11]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/messages/conversations` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MessageController.cs:L23] |
| GET | `/api/messages/conversations/{id}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MessageController.cs:L30] |
| POST | `/api/messages/conversations` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MessageController.cs:L37] |
| GET | `/api/messages/conversations/{id}/messages` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MessageController.cs:L44] |
| POST | `/api/messages/conversations/{id}/messages` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MessageController.cs:L51] |
| POST | `/api/messages/conversations/{id}/read` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MessageController.cs:L58] |
| GET | `/api/messages/unread-count` | - | - | - | [src: backend/MicroLIMS.API/Controllers/MessageController.cs:L65] |

## OosInvestigationDocumentController
File: `backend/MicroLIMS.API/Controllers/OosInvestigationDocumentController.cs` [src: backend/MicroLIMS.API/Controllers/OosInvestigationDocumentController.cs:L15] — class route: `(none)` — class auth: Policy:Oos.Manage [L14]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/oos-tracking/{oosGroupCode}/investigation-documents` | - | - | - | [src: backend/MicroLIMS.API/Controllers/OosInvestigationDocumentController.cs:L29] |
| POST | `/api/oos-tracking/{oosGroupCode}/investigation-documents` | - | RequestSizeLimit(30 * 1024 * 1024)] // 30 MB server transport ceiling | - | [src: backend/MicroLIMS.API/Controllers/OosInvestigationDocumentController.cs:L38] |
| GET | `/api/oos-investigation-documents/{documentId:int}/content` | - | - | - | [src: backend/MicroLIMS.API/Controllers/OosInvestigationDocumentController.cs:L61] |
| POST | `/api/oos-investigation-documents/{documentId:int}/supersede` | - | RequestSizeLimit(30 * 1024 * 1024) | - | [src: backend/MicroLIMS.API/Controllers/OosInvestigationDocumentController.cs:L70] |
| POST | `/api/oos-investigation-documents/{documentId:int}/void` | - | - | - | [src: backend/MicroLIMS.API/Controllers/OosInvestigationDocumentController.cs:L96] |

## OosTrackingController
File: `backend/MicroLIMS.API/Controllers/OosTrackingController.cs` [src: backend/MicroLIMS.API/Controllers/OosTrackingController.cs:L16] — class route: `/api/oos-tracking` — class auth: Policy:Oos.Manage [L15]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/oos-tracking` | - | - | - | [src: backend/MicroLIMS.API/Controllers/OosTrackingController.cs:L27] |

## OrganismMasterDataController
File: `backend/MicroLIMS.API/Controllers/OrganismMasterDataController.cs` [src: backend/MicroLIMS.API/Controllers/OrganismMasterDataController.cs:L22] — class route: `/api/masterdata` — class auth: Authenticated [L21]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/masterdata/organisms` | - | - | - | [src: backend/MicroLIMS.API/Controllers/OrganismMasterDataController.cs:L33] |
| POST | `/api/masterdata/organisms` | Policy:MasterData.Manage [L37] | - | - | [src: backend/MicroLIMS.API/Controllers/OrganismMasterDataController.cs:L38] |
| PUT | `/api/masterdata/organisms/{id}` | Policy:MasterData.Manage [L42] | - | - | [src: backend/MicroLIMS.API/Controllers/OrganismMasterDataController.cs:L43] |
| DELETE | `/api/masterdata/organisms/{id}` | Policy:MasterData.Manage [L47] | - | - | [src: backend/MicroLIMS.API/Controllers/OrganismMasterDataController.cs:L48] |

## PathogenSessionController
File: `backend/MicroLIMS.API/Controllers/PathogenSessionController.cs` [src: backend/MicroLIMS.API/Controllers/PathogenSessionController.cs:L16] — class route: `/api/pathogen-session` — class auth: Policy:TestWorkflow.Execute [L15]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/pathogen-session/{sampleId:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/PathogenSessionController.cs:L29] |
| POST | `/api/pathogen-session/{sampleId:int}/start-tsb` | - | - | - | [src: backend/MicroLIMS.API/Controllers/PathogenSessionController.cs:L40] |
| POST | `/api/pathogen-session/{sampleId:int}/save-matrix` | - | - | - | [src: backend/MicroLIMS.API/Controllers/PathogenSessionController.cs:L59] |
| POST | `/api/pathogen-session/{sampleId:int}/save-primary-observations` | - | - | - | [src: backend/MicroLIMS.API/Controllers/PathogenSessionController.cs:L78] |
| GET | `/api/pathogen-session/{sampleId:int}/eligible-confirmations` | - | - | - | [src: backend/MicroLIMS.API/Controllers/PathogenSessionController.cs:L97] |
| POST | `/api/pathogen-session/{sampleId:int}/start-confirmatory-setup` | - | - | - | [src: backend/MicroLIMS.API/Controllers/PathogenSessionController.cs:L112] |
| POST | `/api/pathogen-session/{sampleId:int}/save-confirmatory-readings` | - | - | - | [src: backend/MicroLIMS.API/Controllers/PathogenSessionController.cs:L131] |
| POST | `/api/pathogen-session/{sampleId:int}/complete` | - | - | - | [src: backend/MicroLIMS.API/Controllers/PathogenSessionController.cs:L150] |
| POST | `/api/pathogen-session/{sampleId:int}/reset` | Policy:TestWorkflow.Supervise [L172] | - | - | [src: backend/MicroLIMS.API/Controllers/PathogenSessionController.cs:L171] |

## PeriodicReviewController
File: `backend/MicroLIMS.API/Controllers/DocumentControl/PeriodicReviewController.cs` [src: backend/MicroLIMS.API/Controllers/DocumentControl/PeriodicReviewController.cs:L13] — class route: `/api/document-control/periodic-reviews` — class auth: Authenticated [L12]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/document-control/periodic-reviews/tasks` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/PeriodicReviewController.cs:L30] |
| GET | `/api/document-control/periodic-reviews/tasks/{id}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/PeriodicReviewController.cs:L41] |
| GET | `/api/document-control/periodic-reviews/tasks/{id}/workspace` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/PeriodicReviewController.cs:L49] |
| POST | `/api/document-control/periodic-reviews/tasks/{id}/findings` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/PeriodicReviewController.cs:L57] |
| PUT | `/api/document-control/periodic-reviews/tasks/{id}/findings/{findingId}/resolve` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/PeriodicReviewController.cs:L67] |
| POST | `/api/document-control/periodic-reviews/tasks/{id}/assign` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/PeriodicReviewController.cs:L77] |
| POST | `/api/document-control/periodic-reviews/tasks/{id}/complete` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/PeriodicReviewController.cs:L87] |
| GET | `/api/document-control/periodic-reviews/masters/{masterId}/history` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/PeriodicReviewController.cs:L97] |
| POST | `/api/document-control/periodic-reviews/trigger-worker` | - | - | - | [src: backend/MicroLIMS.API/Controllers/DocumentControl/PeriodicReviewController.cs:L105] |

## PreparationConfigurationQueueController
File: `backend/MicroLIMS.API/Controllers/ItemPreparationConfigurationController.cs` [src: backend/MicroLIMS.API/Controllers/ItemPreparationConfigurationController.cs:L55] — class route: `/api/preparation-configurations` — class auth: Policy:Items.Manage [L54]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/preparation-configurations/pending` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ItemPreparationConfigurationController.cs:L64] |

## ReferenceListMasterDataController
File: `backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs` [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L22] — class route: `/api/masterdata` — class auth: Authenticated [L21]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/masterdata/causes-of-testing` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L32] |
| POST | `/api/masterdata/causes-of-testing` | Policy:MasterData.Manage [L36] | - | - | [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L37] |
| PUT | `/api/masterdata/causes-of-testing/{id}` | Policy:MasterData.Manage [L41] | - | - | [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L42] |
| DELETE | `/api/masterdata/causes-of-testing/{id}` | Policy:MasterData.Manage [L46] | - | - | [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L47] |
| GET | `/api/masterdata/samplers` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L54] |
| POST | `/api/masterdata/samplers` | Policy:MasterData.Manage [L58] | - | - | [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L59] |
| PUT | `/api/masterdata/samplers/{id}` | Policy:MasterData.Manage [L63] | - | - | [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L64] |
| DELETE | `/api/masterdata/samplers/{id}` | Policy:MasterData.Manage [L68] | - | - | [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L69] |
| GET | `/api/masterdata/production-stages` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L76] |
| POST | `/api/masterdata/production-stages` | Policy:MasterData.Manage [L80] | - | - | [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L81] |
| PUT | `/api/masterdata/production-stages/{id}` | Policy:MasterData.Manage [L85] | - | - | [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L86] |
| DELETE | `/api/masterdata/production-stages/{id}` | Policy:MasterData.Manage [L90] | - | - | [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L91] |
| GET | `/api/masterdata/diluent-types` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L96] |
| POST | `/api/masterdata/diluent-types` | Policy:MasterData.Manage [L100] | - | - | [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L101] |
| GET | `/api/masterdata/neutralizers` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L106] |
| POST | `/api/masterdata/neutralizers` | Policy:MasterData.Manage [L110] | - | - | [src: backend/MicroLIMS.API/Controllers/ReferenceListMasterDataController.cs:L111] |

## ReportingAdminController
File: `backend/MicroLIMS.API/Controllers/ReportingAdminController.cs` [src: backend/MicroLIMS.API/Controllers/ReportingAdminController.cs:L16] — class route: `/api/admin/reporting` — class auth: Policy:Reporting.Admin [L15]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| POST | `/api/admin/reporting/backfill` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingAdminController.cs:L25] |

## ReportingController
File: `backend/MicroLIMS.API/Controllers/ReportingController.cs` [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L24] — class route: `/api/reporting` — class auth: Authenticated [L23]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/reporting/results` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L53] |
| GET | `/api/reporting/results/{id}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L89] |
| GET | `/api/reporting/overview` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L108] |
| GET | `/api/reporting/qualitative-events` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L112] |
| GET | `/api/reporting/trend` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L118] |
| GET | `/api/reporting/compare` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L122] |
| GET | `/api/reporting/completed-by-month` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L128] |
| GET | `/api/reporting/filter-options` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L135] |
| GET | `/api/reporting/results/export` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L139] |
| GET | `/api/reporting/media-gpt` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L185] |
| GET | `/api/reporting/media-gpt/{id}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L207] |
| GET | `/api/reporting/media-gpt/summary` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L217] |
| GET | `/api/reporting/media-gpt/filter-options` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L227] |
| GET | `/api/reporting/media-gpt/export` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L234] |
| GET | `/api/reporting/reference-strains` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L273] |
| GET | `/api/reporting/reference-strains/{id}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L297] |
| GET | `/api/reporting/reference-strains/filter-options` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L307] |
| GET | `/api/reporting/reference-strains/export` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ReportingController.cs:L314] |

## ResultController
File: `backend/MicroLIMS.API/Controllers/ResultController.cs` [src: backend/MicroLIMS.API/Controllers/ResultController.cs:L11] — class route: `/api/results` — class auth: Authenticated [L10]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/results` | - | - | - | [src: backend/MicroLIMS.API/Controllers/ResultController.cs:L22] |

## RoleController
File: `backend/MicroLIMS.API/Controllers/RoleController.cs` [src: backend/MicroLIMS.API/Controllers/RoleController.cs:L21] — class route: `/api/roles` — class auth: Policy:Roles.Manage [L20]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/roles` | - | - | - | [src: backend/MicroLIMS.API/Controllers/RoleController.cs:L32] |
| GET | `/api/roles/{id}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/RoleController.cs:L35] |
| POST | `/api/roles` | - | - | - | [src: backend/MicroLIMS.API/Controllers/RoleController.cs:L42] |
| PUT | `/api/roles/{id}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/RoleController.cs:L49] |
| DELETE | `/api/roles/{id}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/RoleController.cs:L56] |
| PUT | `/api/roles/{id}/permissions` | - | - | - | [src: backend/MicroLIMS.API/Controllers/RoleController.cs:L63] |
| GET | `/api/permissions` | - | - | - | [src: backend/MicroLIMS.API/Controllers/RoleController.cs:L70] |

## SampleApprovalController
File: `backend/MicroLIMS.API/Controllers/SampleApprovalController.cs` [src: backend/MicroLIMS.API/Controllers/SampleApprovalController.cs:L35] — class route: `/api/samples/{id}/approval` — class auth: Policy:Samples.Approve [L34]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| POST | `/api/samples/{id}/approval/decide` | - | - | DecideSampleApprovalRequest.Password | [src: backend/MicroLIMS.API/Controllers/SampleApprovalController.cs:L48] |
| POST | `/api/samples/{id}/approval/sections/{sectionId:int}/close` | - | - | CloseSectionTestingRequest.Password | [src: backend/MicroLIMS.API/Controllers/SampleApprovalController.cs:L58] |

## SampleController
File: `backend/MicroLIMS.API/Controllers/SampleController.cs` [src: backend/MicroLIMS.API/Controllers/SampleController.cs:L54] — class route: `/api/samples` — class auth: Authenticated [L53]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| POST | `/api/samples` | Policy:Samples.ReceiveOwnLab [L82] | - | - | [src: backend/MicroLIMS.API/Controllers/SampleController.cs:L81] |
| GET | `/api/samples/receipt-labs` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SampleController.cs:L99] |
| PUT | `/api/samples/{id}/correct` | Policy:Samples.Correct [L107] | - | CorrectSampleRequest.Password | [src: backend/MicroLIMS.API/Controllers/SampleController.cs:L106] |
| PUT | `/api/samples/{id}/assign-analyst` | Policy:Samples.AssignAnalyst [L127] | - | - | [src: backend/MicroLIMS.API/Controllers/SampleController.cs:L126] |
| POST | `/api/samples/{id}/void` | Policy:Samples.Correct [L136] | - | VoidSampleRequest.Password | [src: backend/MicroLIMS.API/Controllers/SampleController.cs:L135] |
| POST | `/api/samples/{id}/laboratories` | Policy:Samples.Receive [L148] | - | AddLaboratoryRequest.Password | [src: backend/MicroLIMS.API/Controllers/SampleController.cs:L147] |

## SamplePreparationController
File: `backend/MicroLIMS.API/Controllers/SamplePreparationController.cs` [src: backend/MicroLIMS.API/Controllers/SamplePreparationController.cs:L28] — class route: `/api/sample-preparation` — class auth: Authenticated [L27]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| POST | `/api/sample-preparation` | - | - | PrepareSampleHttpRequest.Password | [src: backend/MicroLIMS.API/Controllers/SamplePreparationController.cs:L42] |
| POST | `/api/sample-preparation/confirm` | - | - | ConfirmPreparationHttpRequest.Password | [src: backend/MicroLIMS.API/Controllers/SamplePreparationController.cs:L53] |
| GET | `/api/sample-preparation/groups` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SamplePreparationController.cs:L66] |
| POST | `/api/sample-preparation/batch-confirm` | - | - | BatchConfirmPreparationHttpRequest.Password | [src: backend/MicroLIMS.API/Controllers/SamplePreparationController.cs:L83] |
| GET | `/api/sample-preparation/{sampleId}/is-prepared` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SamplePreparationController.cs:L101] |

## SampleReviewController
File: `backend/MicroLIMS.API/Controllers/SampleReviewController.cs` [src: backend/MicroLIMS.API/Controllers/SampleReviewController.cs:L20] — class route: `/api/samples/{id}/review` — class auth: Policy:Samples.Review [L19]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| POST | `/api/samples/{id}/review/complete` | - | - | CompleteSampleReviewRequest.Password | [src: backend/MicroLIMS.API/Controllers/SampleReviewController.cs:L33] |
| POST | `/api/samples/{id}/review/return-test` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SampleReviewController.cs:L48] |

## SampleSummaryController
File: `backend/MicroLIMS.API/Controllers/SampleSummaryController.cs` [src: backend/MicroLIMS.API/Controllers/SampleSummaryController.cs:L17] — class route: `/api/samples` — class auth: Authenticated [L16]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/samples/{id}/summary` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SampleSummaryController.cs:L30] |
| GET | `/api/samples/{id}/summary/pdf` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SampleSummaryController.cs:L44] |
| GET | `/api/samples/{id}/summary/word` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SampleSummaryController.cs:L53] |

## SampleTrackingController
File: `backend/MicroLIMS.API/Controllers/SampleTrackingController.cs` [src: backend/MicroLIMS.API/Controllers/SampleTrackingController.cs:L17] — class route: `/api/samples/tracking` — class auth: Policy:Samples.TrackAll [L16]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/samples/tracking` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SampleTrackingController.cs:L26] |

## SignaturesController
File: `backend/MicroLIMS.API/Controllers/SignaturesController.cs` [src: backend/MicroLIMS.API/Controllers/SignaturesController.cs:L16] — class route: `/api/signatures` — class auth: Policy:Signatures.Manage [L15]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/signatures` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SignaturesController.cs:L25] |

## SolutionMasterController
File: `backend/MicroLIMS.API/Controllers/SolutionMasterController.cs` [src: backend/MicroLIMS.API/Controllers/SolutionMasterController.cs:L16] — class route: `/api/masterdata/solution-masters` — class auth: Authenticated [L15]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/masterdata/solution-masters` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SolutionMasterController.cs:L22] |
| GET | `/api/masterdata/solution-masters/{id:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SolutionMasterController.cs:L26] |
| POST | `/api/masterdata/solution-masters` | Policy:MasterData.Manage [L30] | - | - | [src: backend/MicroLIMS.API/Controllers/SolutionMasterController.cs:L31] |
| PUT | `/api/masterdata/solution-masters/{id:int}` | Policy:MasterData.Manage [L35] | - | - | [src: backend/MicroLIMS.API/Controllers/SolutionMasterController.cs:L36] |
| PUT | `/api/masterdata/solution-masters/{id:int}/active` | Policy:MasterData.Manage [L40] | - | - | [src: backend/MicroLIMS.API/Controllers/SolutionMasterController.cs:L41] |

## SolutionPreparationController
File: `backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs` [src: backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs:L16] — class route: `/api/solution-preparations` — class auth: Authenticated [L15]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/solution-preparations` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs:L29] |
| GET | `/api/solution-preparations/available` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs:L36] |
| GET | `/api/solution-preparations/by-lot/{materialId:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs:L40] |
| GET | `/api/solution-preparations/{id:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs:L44] |
| GET | `/api/solution-preparations/{id:int}/components/{componentId:int}/lots` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs:L48] |
| POST | `/api/solution-preparations` | Policy:Solutions.Prepare [L52] | - | - | [src: backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs:L53] |
| PUT | `/api/solution-preparations/{id:int}` | Policy:Solutions.Prepare [L57] | - | - | [src: backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs:L58] |
| POST | `/api/solution-preparations/{id:int}/complete` | Policy:Solutions.Prepare [L62] | - | CompletePreparationRequest.Password | [src: backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs:L63] |
| POST | `/api/solution-preparations/{id:int}/cancel` | Policy:Solutions.Prepare [L67] | - | - | [src: backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs:L68] |
| POST | `/api/solution-preparations/{id:int}/discard` | Policy:Solutions.Prepare [L72] | - | - | [src: backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs:L73] |
| GET | `/api/solution-preparations/{id:int}/standardizations` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs:L80] |
| GET | `/api/solution-preparations/{id:int}/standardizations/standard-lots` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs:L84] |
| GET | `/api/solution-preparations/{id:int}/standardizations/reference-options` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs:L88] |
| POST | `/api/solution-preparations/{id:int}/standardizations` | Policy:Solutions.Prepare [L92] | - | StandardizeRequest.Password | [src: backend/MicroLIMS.API/Controllers/SolutionPreparationController.cs:L93] |

## SpecificationMasterDataController
File: `backend/MicroLIMS.API/Controllers/SpecificationMasterDataController.cs` [src: backend/MicroLIMS.API/Controllers/SpecificationMasterDataController.cs:L22] — class route: `/api/masterdata` — class auth: Authenticated [L21]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/masterdata/specifications` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SpecificationMasterDataController.cs:L34] |
| GET | `/api/masterdata/specifications/for-test-order/{testOrderId}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/SpecificationMasterDataController.cs:L41] |
| POST | `/api/masterdata/specifications` | Policy:MasterData.Manage [L45] | - | - | [src: backend/MicroLIMS.API/Controllers/SpecificationMasterDataController.cs:L46] |
| PUT | `/api/masterdata/specifications/{id}` | Policy:MasterData.Manage [L50] | - | - | [src: backend/MicroLIMS.API/Controllers/SpecificationMasterDataController.cs:L51] |
| DELETE | `/api/masterdata/specifications/{id}` | Policy:MasterData.Manage [L55] | - | - | [src: backend/MicroLIMS.API/Controllers/SpecificationMasterDataController.cs:L56] |

## SystemController
File: `backend/MicroLIMS.API/Controllers/SystemController.cs` [src: backend/MicroLIMS.API/Controllers/SystemController.cs:L17] — class route: `/api/system` — class auth: NONE

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| POST | `/api/system/client-errors` | - | Anonymous (method); EnableRateLimiting(RateLimitingPolicies.ClientErrors); RequestSizeLimit(32 * 1024) | - | [src: backend/MicroLIMS.API/Controllers/SystemController.cs:L44] |

## TestDefinitionMasterDataController
File: `backend/MicroLIMS.API/Controllers/TestDefinitionMasterDataController.cs` [src: backend/MicroLIMS.API/Controllers/TestDefinitionMasterDataController.cs:L22] — class route: `/api/masterdata` — class auth: Authenticated [L21]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/masterdata/test-definitions` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestDefinitionMasterDataController.cs:L34] |
| POST | `/api/masterdata/test-definitions` | Policy:MasterData.Manage [L38] | - | - | [src: backend/MicroLIMS.API/Controllers/TestDefinitionMasterDataController.cs:L39] |
| PUT | `/api/masterdata/test-definitions/{id}` | Policy:MasterData.Manage [L43] | - | - | [src: backend/MicroLIMS.API/Controllers/TestDefinitionMasterDataController.cs:L44] |
| PUT | `/api/masterdata/test-definitions/{id}/freeze` | Policy:MasterData.Manage [L48] | - | - | [src: backend/MicroLIMS.API/Controllers/TestDefinitionMasterDataController.cs:L49] |
| PUT | `/api/masterdata/test-definitions/{id}/unfreeze` | Policy:MasterData.Manage [L53] | - | - | [src: backend/MicroLIMS.API/Controllers/TestDefinitionMasterDataController.cs:L54] |
| GET | `/api/masterdata/equation-types` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestDefinitionMasterDataController.cs:L59] |

## TestStageReplicateMasterDataController
File: `backend/MicroLIMS.API/Controllers/TestStageReplicateMasterDataController.cs` [src: backend/MicroLIMS.API/Controllers/TestStageReplicateMasterDataController.cs:L22] — class route: `/api/masterdata` — class auth: Authenticated [L21]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/masterdata/test-definitions/{id:int}/stage-replicates` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestStageReplicateMasterDataController.cs:L35] |
| POST | `/api/masterdata/test-definitions/{id:int}/stage-replicates` | Policy:MasterData.Manage [L39] | - | - | [src: backend/MicroLIMS.API/Controllers/TestStageReplicateMasterDataController.cs:L40] |
| PUT | `/api/masterdata/test-definitions/{id:int}/stage-replicates/{replicateId:int}` | Policy:MasterData.Manage [L44] | - | - | [src: backend/MicroLIMS.API/Controllers/TestStageReplicateMasterDataController.cs:L45] |
| DELETE | `/api/masterdata/test-definitions/{id:int}/stage-replicates/{replicateId:int}` | Policy:MasterData.Manage [L49] | - | - | [src: backend/MicroLIMS.API/Controllers/TestStageReplicateMasterDataController.cs:L50] |

## TestWorkflowController
File: `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs` [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L24] — class route: `/api/test-workflow` — class auth: Policy:TestWorkflow.Execute [L23]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/test-workflow/actionable-groups` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L50] |
| POST | `/api/test-workflow/batch-select-media` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L74] |
| GET | `/api/test-workflow/{testOrderId}/current-step` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L107] |
| GET | `/api/test-workflow/{testOrderId}/sibling-pathogen-orders` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L114] |
| GET | `/api/test-workflow/{testOrderId}/eligible-incubators/{stepMediaId}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L122] |
| GET | `/api/test-workflow/{testOrderId}/permitted-confirmatory-media` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L129] |
| POST | `/api/test-workflow/{testOrderId}/select-media` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L136] |
| POST | `/api/test-workflow/{testOrderId}/start-stage-2-incubation` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L154] |
| POST | `/api/test-workflow/{testOrderId}/record-result` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L169] |
| POST | `/api/test-workflow/{testOrderId}/record-measurement-result` | Policy:TestWorkflow.Execute [L195] | - | RecordMeasurementResultRequest.Password | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L194] |
| POST | `/api/test-workflow/{testOrderId}/record-gravimetric-result` | Policy:TestWorkflow.Execute [L214] | - | RecordGravimetricResultRequest.Password | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L213] |
| GET | `/api/test-workflow/{testOrderId}/titration-context` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L233] |
| POST | `/api/test-workflow/{testOrderId}/record-titration-result` | Policy:TestWorkflow.Execute [L238] | - | RecordTitrationResultRequest.Password | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L237] |
| POST | `/api/test-workflow/{testOrderId}/record-qualitative-result` | Policy:TestWorkflow.Execute [L256] | - | RecordQualitativeResultRequest.Password | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L255] |
| POST | `/api/test-workflow/{testOrderId}/record-dissolution-result` | Policy:TestWorkflow.Execute [L276] | - | RecordDissolutionResultRequest.Password | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L275] |
| POST | `/api/test-workflow/{testOrderId}/record-dissolution-stage` | Policy:TestWorkflow.Execute [L296] | - | RecordDissolutionStageRequest.Password | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L295] |
| POST | `/api/test-workflow/{testOrderId}/record-disintegration-result` | Policy:TestWorkflow.Execute [L311] | - | RecordDisintegrationResultRequest.Password | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L310] |
| POST | `/api/test-workflow/{testOrderId}/record-disintegration-stage` | Policy:TestWorkflow.Execute [L329] | - | RecordDisintegrationStageRequest.Password | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L328] |
| POST | `/api/test-workflow/{testOrderId}/record-weight-variation-result` | Policy:TestWorkflow.Execute [L344] | - | RecordWeightVariationResultRequest.Password | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L343] |
| POST | `/api/test-workflow/{testOrderId}/record-weight-variation-stage` | Policy:TestWorkflow.Execute [L365] | - | RecordWeightVariationStageRequest.Password | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L364] |
| GET | `/api/test-workflow/{testOrderId}/locations` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L384] |
| POST | `/api/test-workflow/{testOrderId}/batch-results` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L391] |
| POST | `/api/test-workflow/{testOrderId}/water-batch-readings` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L405] |
| POST | `/api/test-workflow/{testOrderId}/close-incubation-window` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L420] |
| POST | `/api/test-workflow/{testOrderId}/batch-pathogen-results` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L431] |
| POST | `/api/test-workflow/{testOrderId}/submit-broth` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L444] |
| POST | `/api/test-workflow/{testOrderId}/start-selective-plating-incubation` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L451] |
| POST | `/api/test-workflow/{testOrderId}/submit-selective-plating-observation` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L470] |
| POST | `/api/test-workflow/{testOrderId}/submit-selective-plating` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L478] |
| POST | `/api/test-workflow/{testOrderId}/submit-confirmatory-setup` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L494] |
| POST | `/api/test-workflow/{testOrderId}/submit-confirmatory-observations` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L503] |
| POST | `/api/test-workflow/{testOrderId}/analyst-decision` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L512] |
| POST | `/api/test-workflow/{testOrderId}/submit-biochemical` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L519] |
| POST | `/api/test-workflow/results/{workflowStepResultId}/biochemical-decision` | Policy:TestWorkflow.BiochemicalDecision [L527] | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L528] |
| POST | `/api/test-workflow/{testOrderId}/override-minimum-duration` | Policy:TestWorkflow.Supervise [L543] | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:L544] |

## TestWorkflowStepMasterDataController
File: `backend/MicroLIMS.API/Controllers/TestWorkflowStepMasterDataController.cs` [src: backend/MicroLIMS.API/Controllers/TestWorkflowStepMasterDataController.cs:L22] — class route: `/api/masterdata` — class auth: Authenticated [L21]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| PUT | `/api/masterdata/test-definitions/{id}/workflow-type` | Policy:MasterData.Manage [L34] | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowStepMasterDataController.cs:L35] |
| GET | `/api/masterdata/test-definitions/{id}/steps` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowStepMasterDataController.cs:L39] |
| POST | `/api/masterdata/test-definitions/{id}/steps` | Policy:MasterData.Manage [L43] | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowStepMasterDataController.cs:L44] |
| PUT | `/api/masterdata/test-definitions/steps/{stepId}` | Policy:MasterData.Manage [L48] | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowStepMasterDataController.cs:L49] |
| PUT | `/api/masterdata/test-definitions/steps/{stepId}/move` | Policy:MasterData.Manage [L53] | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowStepMasterDataController.cs:L54] |
| DELETE | `/api/masterdata/test-definitions/steps/{stepId}` | Policy:MasterData.Manage [L58] | - | - | [src: backend/MicroLIMS.API/Controllers/TestWorkflowStepMasterDataController.cs:L59] |

## TestingWorkspaceController
File: `backend/MicroLIMS.API/Controllers/TestingWorkspaceController.cs` [src: backend/MicroLIMS.API/Controllers/TestingWorkspaceController.cs:L12] — class route: `/api/testorders` — class auth: Authenticated [L11]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/testorders` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestingWorkspaceController.cs:L28] |
| GET | `/api/testorders/page` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestingWorkspaceController.cs:L29] |
| GET | `/api/testorders/counts` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestingWorkspaceController.cs:L36] |
| GET | `/api/testorders/{id:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/TestingWorkspaceController.cs:L43] |

## UserAnalystController
File: `backend/MicroLIMS.API/Controllers/UserAnalystController.cs` [src: backend/MicroLIMS.API/Controllers/UserAnalystController.cs:L12] — class route: `/api/users/analysts` — class auth: Policy:Samples.AssignAnalyst [L11]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/users/analysts` | - | - | - | [src: backend/MicroLIMS.API/Controllers/UserAnalystController.cs:L21] |

## UserController
File: `backend/MicroLIMS.API/Controllers/UserController.cs` [src: backend/MicroLIMS.API/Controllers/UserController.cs:L23] — class route: `/api/users` — class auth: Policy:Users.Manage [L22]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/users` | - | - | - | [src: backend/MicroLIMS.API/Controllers/UserController.cs:L38] |
| GET | `/api/users/{id}` | Policy:Users.Manage [L42] | - | - | [src: backend/MicroLIMS.API/Controllers/UserController.cs:L41] |
| POST | `/api/users` | Policy:Users.Manage [L56] | - | CreateUserRequest.Password | [src: backend/MicroLIMS.API/Controllers/UserController.cs:L55] |
| PUT | `/api/users/{id}` | Policy:Users.Manage [L65] | - | - | [src: backend/MicroLIMS.API/Controllers/UserController.cs:L64] |
| PUT | `/api/users/{id}/role` | Policy:Users.Manage [L73] | - | - | [src: backend/MicroLIMS.API/Controllers/UserController.cs:L72] |
| PUT | `/api/users/{id}/status` | Policy:Users.Manage [L81] | - | - | [src: backend/MicroLIMS.API/Controllers/UserController.cs:L80] |
| PUT | `/api/users/{id}/deactivate` | Policy:Users.Manage [L89] | - | - | [src: backend/MicroLIMS.API/Controllers/UserController.cs:L88] |
| PUT | `/api/users/{id}/email` | Policy:Users.Manage [L97] | - | - | [src: backend/MicroLIMS.API/Controllers/UserController.cs:L96] |
| PUT | `/api/users/{id}/unlock` | Policy:Users.Manage [L105] | - | - | [src: backend/MicroLIMS.API/Controllers/UserController.cs:L104] |
| POST | `/api/users/{id}/password-reset` | Policy:Users.Manage [L113] | - | - | [src: backend/MicroLIMS.API/Controllers/UserController.cs:L112] |
| POST | `/api/users/{id}/admin-password-recovery` | Policy:Users.Manage [L121] | - | - | [src: backend/MicroLIMS.API/Controllers/UserController.cs:L120] |
| PUT | `/api/users/{id}/force-password-change` | Policy:Users.Manage [L129] | - | - | [src: backend/MicroLIMS.API/Controllers/UserController.cs:L128] |
| DELETE | `/api/users/{id}` | Policy:Users.Manage [L140] | - | - | [src: backend/MicroLIMS.API/Controllers/UserController.cs:L139] |

## UserDirectoryController
File: `backend/MicroLIMS.API/Controllers/UserDirectoryController.cs` [src: backend/MicroLIMS.API/Controllers/UserDirectoryController.cs:L11] — class route: `/api/users/directory` — class auth: Authenticated [L10]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/users/directory` | - | - | - | [src: backend/MicroLIMS.API/Controllers/UserDirectoryController.cs:L20] |

## WaterController
File: `backend/MicroLIMS.API/Controllers/WaterController.cs` [src: backend/MicroLIMS.API/Controllers/WaterController.cs:L17] — class route: `/api/water` — class auth: Authenticated [L16]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| POST | `/api/water/receive` | - | - | - | [src: backend/MicroLIMS.API/Controllers/WaterController.cs:L30] |
| POST | `/api/water/prepare` | - | - | - | [src: backend/MicroLIMS.API/Controllers/WaterController.cs:L37] |
| POST | `/api/water/calculate` | - | - | - | [src: backend/MicroLIMS.API/Controllers/WaterController.cs:L42] |
| GET | `/api/water/daily-report` | - | - | - | [src: backend/MicroLIMS.API/Controllers/WaterController.cs:L49] |

## WaterMasterDataController
File: `backend/MicroLIMS.API/Controllers/WaterMasterDataController.cs` [src: backend/MicroLIMS.API/Controllers/WaterMasterDataController.cs:L22] — class route: `/api/masterdata` — class auth: Authenticated [L21]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/masterdata/water-sampling-points` | - | - | - | [src: backend/MicroLIMS.API/Controllers/WaterMasterDataController.cs:L32] |
| POST | `/api/masterdata/water-sampling-points` | Policy:MasterData.Manage [L36] | - | - | [src: backend/MicroLIMS.API/Controllers/WaterMasterDataController.cs:L37] |
| PUT | `/api/masterdata/water-sampling-points/{id}` | Policy:MasterData.Manage [L41] | - | - | [src: backend/MicroLIMS.API/Controllers/WaterMasterDataController.cs:L42] |
| DELETE | `/api/masterdata/water-sampling-points/{id}` | Policy:MasterData.Manage [L46] | - | - | [src: backend/MicroLIMS.API/Controllers/WaterMasterDataController.cs:L47] |
| GET | `/api/masterdata/water-departments` | - | - | - | [src: backend/MicroLIMS.API/Controllers/WaterMasterDataController.cs:L52] |
| POST | `/api/masterdata/water-departments` | Policy:MasterData.Manage [L56] | - | - | [src: backend/MicroLIMS.API/Controllers/WaterMasterDataController.cs:L57] |
| PUT | `/api/masterdata/water-departments/{id}` | Policy:MasterData.Manage [L61] | - | - | [src: backend/MicroLIMS.API/Controllers/WaterMasterDataController.cs:L62] |
| DELETE | `/api/masterdata/water-departments/{id}` | Policy:MasterData.Manage [L66] | - | - | [src: backend/MicroLIMS.API/Controllers/WaterMasterDataController.cs:L67] |
| GET | `/api/masterdata/water-sampling-configurations` | - | - | - | [src: backend/MicroLIMS.API/Controllers/WaterMasterDataController.cs:L72] |
| POST | `/api/masterdata/water-sampling-configurations` | Policy:MasterData.Manage [L76] | - | - | [src: backend/MicroLIMS.API/Controllers/WaterMasterDataController.cs:L77] |
| PUT | `/api/masterdata/water-sampling-configurations/{id}` | Policy:MasterData.Manage [L81] | - | - | [src: backend/MicroLIMS.API/Controllers/WaterMasterDataController.cs:L82] |
| DELETE | `/api/masterdata/water-sampling-configurations/{id}` | Policy:MasterData.Manage [L86] | - | - | [src: backend/MicroLIMS.API/Controllers/WaterMasterDataController.cs:L87] |

## WorkflowProfilesController
File: `backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs` [src: backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs:L17] — class route: `/api/kpi/workflow-profiles` — class auth: Policy:Kpi.View [L16]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/kpi/workflow-profiles` | - | - | - | [src: backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs:L26] |
| GET | `/api/kpi/workflow-profiles/overview` | - | - | - | [src: backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs:L34] |
| GET | `/api/kpi/workflow-profiles/coverage` | - | - | - | [src: backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs:L38] |
| POST | `/api/kpi/workflow-profiles` | Policy:Kpi.ConfigureProfiles [L42] | - | CreateWorkflowProfileVersionRequest.Password | [src: backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs:L43] |
| POST | `/api/kpi/workflow-profiles/{id:int}/sign` | Policy:Kpi.ConfigureProfiles [L47] | - | SignWorkflowProfileDraftRequest.Password | [src: backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs:L48] |

## WorkingStandardsController
File: `backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs` [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L14] — class route: `/api/working-standards` — class auth: Authenticated [L13]

| Verb | Route | Method auth | Other attrs | Password in body | src |
|---|---|---|---|---|---|
| GET | `/api/working-standards/lots` | - | - | - | [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L30] |
| GET | `/api/working-standards/qualifications` | - | - | - | [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L34] |
| GET | `/api/working-standards/qualifications/{id:int}` | - | - | - | [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L38] |
| GET | `/api/working-standards/source-samples` | - | - | - | [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L42] |
| POST | `/api/working-standards/qualifications` | Policy:WorkingStandards.Qualify [L48] | - | - | [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L49] |
| PUT | `/api/working-standards/qualifications/{id:int}` | Policy:WorkingStandards.Qualify [L53] | - | - | [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L54] |
| POST | `/api/working-standards/qualifications/{id:int}/documents` | Policy:WorkingStandards.Qualify [L60] | RequestSizeLimit(MaxUploadBytes) | - | [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L61] |
| GET | `/api/working-standards/documents/{id:int}/file` | - | - | - | [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L73] |
| POST | `/api/working-standards/qualifications/{id:int}/review` | Policy:Samples.Review [L82] | - | WorkingStandardSignRequest.Password | [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L83] |
| POST | `/api/working-standards/qualifications/{id:int}/return` | Policy:Samples.Review [L87] | - | - | [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L88] |
| POST | `/api/working-standards/qualifications/{id:int}/reject-review` | Policy:Samples.Review [L92] | - | WorkingStandardReasonRequest.Password | [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L93] |
| POST | `/api/working-standards/qualifications/{id:int}/approve` | Policy:Samples.Approve [L97] | - | WorkingStandardSignRequest.Password | [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L98] |
| POST | `/api/working-standards/qualifications/{id:int}/reject-approval` | Policy:Samples.Approve [L102] | - | WorkingStandardReasonRequest.Password | [src: backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs:L103] |

