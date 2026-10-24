# MicroLIMS — Application Map (Phase 2)

| Item | Value |
|---|---|
| Document type | Knowledge file for the documentation team (NOT end-user text). Evidence tags `[src: ...]` are kept on facts. |
| Repository / branch | `E:\MicroLIMS\MicroLIMS`, branch `docs/user-guide`, commit described: `f773042c` (refactor/consolidation base, PR #86 not yet merged to main) |
| Status | DRAFT. Phase 2 output. Awaiting user approval before Phase 3. |
| Evidence labels | IMPLEMENTED = seen in code. INTENDED = from specification sources only. REQUIRES CONFIRMATION = cannot be verified. |
| Behaviour source | Code only. The application was NOT run for this phase, so every screen-behaviour statement is lower confidence. |
| Verification rule | Roles, permissions, approval rules and signature rules come from `knowledge/_raw/verified-roles-permissions.md` (Sonnet-verified). Where `backend-permissions-roles-auth.md` (Haiku, unverified) differs, the verified file wins. |
| Inputs | `_raw/frontend-core-screens.md`, `_raw/frontend-testing-workspace.md`, `_raw/frontend-config-physchem-dc-screens.md`, `_raw/backend-endpoints.md`, `_raw/backend-enums.md`, `_raw/spec-sources.md`, plus direct reads of `frontend/src/routes/menuConfig.ts`, `frontend/src/routes/AppRoutes.tsx`. |
| Companion files | `COVERAGE_MATRIX.md`, `DOCUMENTATION_GAPS.md`, `PROGRESS.md` |

Shorthand: "Roles screen" = the Roles page in the application. `[raw: file §n]` points to a Phase 1 raw inventory where the full tagged fact sits.

---

## 1. Application overview (plain language)

MicroLIMS is a web application for two kinds of laboratory: a **Microbiology Laboratory** (code `MICRO`) and a **Physicochemical Laboratory** (code `FP`, split into the *FP* area for finished product and the *RM & PM* area for raw and packaging material) [src: backend/MicroLIMS.Shared/Constants/LaboratoryCodes.cs:L7-L8; frontend/src/routes/menuConfig.ts:L161-L187].

What a user can do with it, as implemented:

- **Receive samples** (finished product, raw material, packaging material, water, environmental monitoring, after cleaning), after which the system creates the test orders configured for the item automatically [src: documents/DesignSpecification/BusinessRules.md:L33-L48 (INTENDED); frontend/src/modules/receiving/ReceivingPage.tsx:L83 (IMPLEMENTED)].
- **Test samples** in a laboratory workspace: microbiology count tests, pathogen tests, environmental and water tests, and physicochemical tests (HPLC, GC, ICP-OES, titration, dissolution, disintegration, weight variation, measurement, gravimetric, qualitative) [raw: frontend-testing-workspace §2-§5; frontend-config §4-§5].
- **Review and approve** results per laboratory section: an analyst enters results, a reviewer signs a technical review, a Section Head signs an approval decision, and an approved or rejected sample can have a Certificate of Analysis [src: frontend/src/modules/testingWorkspace/sampleSummary/ApprovalSignaturesCard.tsx:L235-L475].
- **Prepare and track** media lots, reference-strain cryovials, reagents and materials, equipment, solutions and working standards.
- **Maintain master data** (items, tests, specifications, organisms, media, equipment, methods, solutions) and load some of it from one Excel workbook.
- **Control documents** (SOPs and other controlled documents, revisions, review, approval, periodic review, reading and training evidence) in a Document Control area.
- **Report**: reports and trend pages, a KPI / Performance page, exports, audit-trail search, OOS tracking and a technical Error Monitoring page.
- **Collaborate**: direct and group Messages and Discussions.

Every sensitive action that the code protects asks the user to type their login password again (an "electronic signature"); 26 distinct signed actions were found [src: knowledge/_raw/verified-roles-permissions.md §6]. This is a description of implemented behaviour only. No statement here claims regulatory compliance or validation (see GAP-023).

Frontend: React + TypeScript + Material UI. Backend: ASP.NET Core with PostgreSQL. Authentication: JWT bearer, access token default 15 minutes, refresh token 7 days [src: backend/MicroLIMS.API/Extensions/JwtConfiguration.cs:L36-L37; backend/MicroLIMS.Application/Services/AuthenticationService.cs:L23].

---

## 2. Module inventory

Status labels: **COVERED** (screens and rules inventoried to field level in Phase 1), **PARTIAL** (module exists; some labels, fields or dialogs not yet read or read only by pattern search), **LEGACY/RETIRED**, **NOT-EXPOSED-IN-UI** (backend capability with no frontend caller found), **REQUIRES CONFIRMATION**.

| Module ID | Module | What it is | Status | Main evidence |
|---|---|---|---|---|
| MOD-AUTH-001 | Authentication, session, password | Sign in, forgot password, admin-assisted recovery, change password (also forced), profile, idle timeout | COVERED | [raw: frontend-core §1; verified §8-§9] |
| MOD-NAV-001 | Application shell and navigation | Sidebar, header, section landing pages, breadcrumbs, dark mode, collapsible sidebar, page-not-found | COVERED | [src: frontend/src/layouts/MainLayout.tsx; frontend/src/components/Header.tsx; frontend/src/components/Sidebar.tsx] |
| MOD-NOTIF-001 | Notifications | In-app bell (read / read-all), unread message count; e-mail for some alerts if SMTP configured | PARTIAL | [src: frontend/src/components/Header.tsx:L117-L154; backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:L16-L20] |
| MOD-DASH-001 | Dashboards | Four role-based dashboards (Administrator, Analyst, Reviewer, Section Head) with per-laboratory tabs | PARTIAL | [raw: frontend-core §2] Analyst tile labels not enumerated |
| MOD-USERS-001 | User management | Create users, roles, lock/unlock, disable, password reset/recovery, force password change, delete, laboratory sections | PARTIAL | [raw: frontend-core §12] Laboratory Sections dialog only partly read |
| MOD-ROLES-001 | Roles and permissions | Role list, Create Role wizard, role detail, permission matrix (Enforced / Legacy-only chips) | COVERED | [raw: frontend-core §13] |
| MOD-SIG-001 | Electronic signatures and segregation of duties | Shared "Electronic Signature" password dialog, 26 signed actions, SoD rules | COVERED | [src: knowledge/_raw/verified-roles-permissions.md §5-§6] |
| MOD-AUDIT-001 | Audit Trail (Audit Search) and traceability chain | Search audit events, drawer with diff and traceability chain | PARTIAL | [raw: frontend-core §7] diff/raw-JSON/chain labels not extracted |
| MOD-ERR-001 | Error Monitoring | Technical incident list, severity override, resolve / reopen, CSV export | COVERED | [raw: frontend-core §9] |
| MOD-MSG-001 | Messages | Direct and group conversations | COVERED | [raw: frontend-core §10] |
| MOD-DISC-001 | Discussions | Posts, comments, attachments, important flag, version history | COVERED | [raw: frontend-core §11] |
| MOD-ARCHIVE-001 | Archived records | Archived (frozen) copies of approved records shown on the sample report; download | PARTIAL | [src: frontend/src/modules/testingWorkspace/services/ArchivedRecordsService.ts:L22-L26] |
| MOD-SAMPLE-001 | Sample receiving and register | Receive Sample page, New Sample dialog, register, edit (signed), void (signed), add laboratory (signed), assign analyst | PARTIAL | [raw: frontend-core §3] register table, KPI cards, selection bar not read |
| MOD-TRACK-001 | Tracking Board | Cross-laboratory view of every sample with each lab's stage | COVERED | [raw: frontend-core §3.2] |
| MOD-PREP-001 | Sample preparation | Prepare Sample dialog (product/RM/PM from item preparation configuration; EM/AC/Water grids), signed confirmation, grouped preparation, item preparation configuration approval | PARTIAL | [raw: frontend-testing §5; frontend-core §4.3] strings not extracted |
| MOD-TESTING-001 | Laboratory workspaces | Microbiology Workspace, FP Workspace, RM & PM Workspace: register (table, card, kanban), selected sample panel, assigned tests, grouped actions, KPI tiles | PARTIAL | [raw: frontend-testing §1] |
| MOD-COUNT-001 | Count tests | Media/incubator selection, two-stage incubation, plate readings, dilution factor, TNTC/Uncountable, calculated result | COVERED | [raw: frontend-testing §2] |
| MOD-OBS-001 | Observation and location-grid tests | Qualitative (complies / does not comply), per-location Detected/Absent grid, EM/AC and water count grids | COVERED | [raw: frontend-testing §2.4, §3] |
| MOD-PHYSRESULT-001 | Physicochemical result panels | Measurement, Gravimetric, Qualitative, Titration, Dissolution, Disintegration, Weight Variation panels (signed) | PARTIAL | [raw: frontend-testing §11.2, §12] read by pattern search only |
| MOD-PATH-001 | Pathogen testing | Single-test step chain and the six-step Pathogen Testing Session Workspace | COVERED | [raw: frontend-testing §4] |
| MOD-EM-001 | Environmental Monitoring | EM configuration (departments, rooms, grades, per-room limits), receiving, room preparation, per-location results | PARTIAL | [raw: frontend-config §1.15; frontend-testing §5.2] |
| MOD-WATER-001 | Water Monitoring | Water configuration (systems, sampling points, tests, limits), receiving, preparation with storage, per-point results | PARTIAL | [raw: frontend-config §1.14; frontend-testing §5.4] |
| MOD-AC-001 | After Cleaning monitoring | Machines, parts, swab/rinse/pathogen tests, receiving, preparation, results | PARTIAL | [raw: frontend-config §1.16; frontend-testing §5.3] |
| MOD-REVIEW-001 | Technical review and return to analyst | Sample Summary dialog "Submit Technical Review" (signed); "Return to Analyst" for count tests (no signature) | COVERED | [raw: frontend-testing §7.1-§7.3] |
| MOD-APPROVAL-001 | Approval, retest, new sample, close testing | "Submit Release Decision" (signed): Approve, Not Conform, New Sample Required, Retest Retained Sample; "Close Testing" (signed) | COVERED | [raw: frontend-testing §7.2, §7.4] |
| MOD-COA-001 | Sample report and Certificate of Analysis | Printable sample summary report, Certificate of Analysis (combined or per section) | PARTIAL | [raw: frontend-core §5; frontend-testing §8] CoA body read by headings only |
| MOD-OOS-001 | OOS Tracking | OOS investigation groups, retest spin-offs, investigation document upload / supersede | COVERED | [raw: frontend-core §8] |
| MOD-MEDIA-001 | Media preparation, evaluation, release, storage | Media lots, evaluations (GPT and others), release decision (signed), storage moves, out-of-stock, approved list, media lot report | PARTIAL | [raw: frontend-config §1.1, §1.2, §1.5, §2.3] |
| MOD-MEDIACFG-001 | Media Configurations | Media products, incubation conditions, media configurations, signed product-code change | PARTIAL | [raw: frontend-config §1.3, §1.4] |
| MOD-CRYO-001 | Reference cryovials (reference strains) | Prepare cryovial batches, approve/reject (signed), thaw, move storage, destroy, approved list, cryovial report | PARTIAL | [raw: frontend-config §1.6, §1.7, §2.3] |
| MOD-ORG-001 | Organisms master | Canonical organism list | COVERED | [raw: frontend-config §1.11] |
| MOD-MATERIAL-001 | Materials Stock | Receive and track lots of reagents, media, standards, supplies; documents | PARTIAL | [raw: frontend-config §2.1] |
| MOD-EQUIP-001 | Equipment Inventory | Equipment register, active equipment, calibration due, certificate documents, status history | PARTIAL | [raw: frontend-config §2.2] |
| MOD-EQUIP-002 | Equipment configuration | Laboratory Configuration: Equipment (set points, autoclave programs) and Physicochemical Instruments | PARTIAL | [raw: frontend-config §1.18, §1.19] |
| MOD-COLUMN-001 | Chromatography Columns | Column master, parameters, deactivate | PARTIAL | [raw: frontend-config §1.20] |
| MOD-ITEM-001 | Items | Item master, assigned tests, preparation configuration, item specifications, item documents, freeze/unfreeze | PARTIAL | [raw: frontend-config §1.8] |
| MOD-SPEC-001 | Specifications (stand-alone page) | Routed page `/laboratory-configuration/specifications`, not linked from the menu | REQUIRES CONFIRMATION | [src: frontend/src/routes/AppRoutes.tsx:L229]; see GAP-034 |
| MOD-TESTMASTER-001 | Test Master | Test definitions with workflow steps, media, stage replicates, titration configuration — Microbiology, FP, RM & PM | PARTIAL | [raw: frontend-config §1.9] |
| MOD-EQTYPE-001 | Equation Types | Predefined calculation equations (list) | PARTIAL | [raw: frontend-config §1.10] read-only or not UNCLEAR |
| MOD-RECVCFG-001 | Receiving Configuration | Samplers and Cause of Testing lists | COVERED | [raw: frontend-config §1.17] |
| MOD-WORKBOOK-001 | Master Data Workbook | Download / verify / load an Excel workbook (signed), second-person verify | COVERED | [raw: frontend-config §1.12] |
| MOD-REAGENT-001 | Reagents & Reference Standards (material master) | Master definitions of reagents, indicators, reference standards | PARTIAL | [raw: frontend-config §1.21] |
| MOD-SOLNM-001 | Solutions Master | Recipes for mobile phases, diluents, titrants, standardization rules | PARTIAL | [raw: frontend-config §1.22] |
| MOD-HPLCM-001 | Chromatography Methods Master (HPLC and GC) | HPLC/GC methods, column, elution/detection, analytes, SST criteria | PARTIAL | [raw: frontend-config §1.23] |
| MOD-ICPM-001 | ICP Methods Master | ICP-OES methods, calibration, optional checks, sample prep, elements | PARTIAL | [raw: frontend-config §1.24] |
| MOD-HPLC-001 | HPLC Workspace | Instrument overview, run wizard, system suitability (signed), sample assignment, replicate entry, send for review (signed), evidence upload, run history | PARTIAL | [raw: frontend-config §4] |
| MOD-GC-001 | GC Workspace | Same screens as HPLC under `/gc-workspace` | PARTIAL | [src: frontend/src/routes/AppRoutes.tsx:L186-L197; frontend/src/modules/hplcWorkspace/useTechnique.ts:L18-L45] |
| MOD-ICP-001 | ICP-OES Workspace | Instrument overview, run, calibration (signed), CCV, samples, send for review (signed), evidence | PARTIAL | [raw: frontend-config §5] |
| MOD-SOLN-001 | Solution Preparation and titrant standardization | Preparation list, wizard, record, titrant standardization (signed) | PARTIAL | [raw: frontend-config §6] |
| MOD-WS-001 | Working Standards | Lots, qualifications, review / approve / reject sign-off | PARTIAL | [raw: frontend-config §7] |
| MOD-TITR-001 | Titration tests | Titration configuration in Test Master, Titration panel (signed), titrant due acknowledgement | PARTIAL | [raw: frontend-config §1.26; frontend-testing §11.2] |
| MOD-REPORTS-001 | Reports | Tabs: Overview, Record Search, Media / GPT, Reference Strains, Trending & Analysis; CSV and PDF exports | PARTIAL | [raw: frontend-core §6] contains hardcoded fallbacks (GAP-002, GAP-003) |
| MOD-KPI-001 | KPI / Performance and Workflow Profiles | KPI tab (needs Kpi.View), workload table, definitions, signed workflow profile versions | PARTIAL | [raw: frontend-core §6] |
| MOD-DC-001 | Document Control: library and document lifecycle | Dashboard, Document Library, document detail, revisions, technical review, approval (signed), periodic review, void | PARTIAL | [raw: frontend-config §3.1, §3.5, §3.6] |
| MOD-DC-002 | Document Control: reading and training | My Reading List (acknowledgement), Training Matrix, Training Compliance Dashboard | PARTIAL | [raw: frontend-config §3.2-§3.4] |
| MOD-DC-003 | Document Control: audit trail | Document Control Audit Trail (global and per-document), export | PARTIAL | [raw: frontend-config §3.7] |
| MOD-DC-004 | Document Control: configuration | Document types, departments & sections, numbering, module settings | COVERED | [raw: frontend-config §3.8] |
| MOD-AUDIT-002 | Login history (backend) | `GET /api/admin/audit/login-history` | NOT-EXPOSED-IN-UI | [src: backend/MicroLIMS.API/Controllers/AuditController.cs:L51]; no frontend caller found |
| MOD-SECAUDIT-001 | Security audit events (backend) | Security events (login, lockout, role change) are recorded; permission `System.ViewSecurityAudit` is NOT-ENFORCED and no viewer endpoint was found | NOT-EXPOSED-IN-UI | [src: backend/MicroLIMS.Persistence/Migrations/20260909204150_AddSecurityAuditEvents.cs:L86-L90] |
| MOD-SIG-002 | Signature trail (backend) | `GET /api/signatures` (Signatures.Manage) | NOT-EXPOSED-IN-UI | [src: backend/MicroLIMS.API/Controllers/SignaturesController.cs:L25]; no frontend caller found |
| MOD-REPORTS-002 | Reporting backfill (backend) | `POST /api/admin/reporting/backfill` (Reporting.Admin) | NOT-EXPOSED-IN-UI | [src: backend/MicroLIMS.API/Controllers/ReportingAdminController.cs:L25] |
| MOD-KPI-002 | Analyst availability and KPI section settings (backend + service) | `/api/kpi/availability`, `/api/kpi/settings/{sectionId}` | NOT-EXPOSED-IN-UI (service exists in frontend, no screen caller found) | [src: frontend/src/modules/kpi/services/kpiWorkloadService.ts:L54-L65] |
| MOD-DC-005 | Document escalations (backend) | `/api/document-control/escalations/*` | NOT-EXPOSED-IN-UI | [src: backend/MicroLIMS.API/Controllers/DocumentControl/DocumentEscalationController.cs:L38-L80] |
| MOD-RESULT-001 | Results list (backend) | `GET /api/results` | NOT-EXPOSED-IN-UI | [src: backend/MicroLIMS.API/Controllers/ResultController.cs:L22] |
| MOD-INSTIMPORT-001 | Instrument report reading (OCR and parsers) | Empower-style and ICP report parsers and a Tesseract OCR reader exist; only the text reader is registered; no controller or UI calls them | NOT-EXPOSED-IN-UI | [src: backend/MicroLIMS.Application/Services/InstrumentImport/EmpowerReportParser.cs; backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs:L299] |
| MOD-LEGACY-001 | Legacy and retired features | Retired legacy SST module, retired ICP/AAS calibration-run path, retired assay equation values, legacy redirects, unused enum stages (see §12) | LEGACY/RETIRED | [src: docs/superpowers/specs/2026-10-02-retire-legacy-sst-design.md:L1; docs/superpowers/plans/2026-10-05-r-retire-calibration-runs.md:L1] |

Module count by status is given in the final report and in `COVERAGE_MATRIX.md`.

Modules found in code and not in the Step B list: Messages, Discussions, Tracking Board, OOS Tracking, Master Data Workbook, Working Standards, Receiving Configuration, Organisms, Chromatography Columns, GC Workspace, KPI / Workflow Profiles, Archived records, section landing pages, admin-assisted password recovery, security audit events, login history, Document Control training and compliance pages.

---

## 3. Navigation structure

### 3.1 Shell (always present after sign-in)

- **Header**: sidebar toggle ("Toggle navigation menu"), dark/light switch ("Switch to dark mode" / "Switch to light mode"), **Notifications** bell, account menu with "My Profile", "Messages", "Discussions", "Change Password", "Sign Out" [src: frontend/src/components/Header.tsx:L184-L363].
- **Sidebar**: collapsible rail (state remembered in the browser), landmark "Main navigation" [src: frontend/src/components/Sidebar.tsx:L357-L394; frontend/src/layouts/MainLayout.tsx:L35-L54].
- Breadcrumbs above each page; page errors are caught per page [src: frontend/src/layouts/MainLayout.tsx:L145-L148].
- Forced redirect to Change Password while `mustChangePassword` is set [src: frontend/src/layouts/MainLayout.tsx:L63-L66].
- Pages the user may not open silently return to `/dashboard` (no message) [src: frontend/src/routes/PermissionRoute.tsx:L12].

### 3.2 How the sidebar is built

`getGroupedMenu({ role, permissions, labCodes, physchemAreas })` builds one flat list, removes every item whose permission the user lacks, removes any parent left with no children, then groups items by `group` in first-appearance order [src: frontend/src/routes/menuConfig.ts:L216-L266].

Visibility rules:

1. **Permission**: an item with `permission` is shown only if the user holds that code (any one of an array).
2. **Laboratory membership** (`useMyLabs`): "Microbiology Laboratory" and "Microbiology Configuration" appear only if the user belongs to `MICRO`; the Physicochemical pair only if the user belongs to `FP`. A System Administrator is treated as a member of both laboratories and both physchem areas; everyone else sees only their own memberships [src: frontend/src/routes/menuConfig.ts:L255-L260; frontend/src/hooks/useMyLabs.ts:L11-L66 (via verified §7)].
3. **Physchem area** (`fp` or `rmpm`, set on an FP membership): "FP Workspace" needs area `fp`; "RM & PM Workspace" needs area `rmpm`. If the area list is not supplied, both are shown [src: frontend/src/routes/menuConfig.ts:L161-L168, L258].
4. **Role type** affects only the Document Control "Audit Trail" link (Reviewer, Section Head, System Administrator) [src: frontend/src/routes/menuConfig.ts:L101-L103, L118]. The server also restricts it by role.
5. The sidebar only hides links. Every page route is also guarded, and every API call is checked again on the server.

### 3.3 Sidebar groups, in the order built

| Group heading | Item (UI label) | Path | Needs |
|---|---|---|---|
| OVERVIEW | Dashboard | `/dashboard` | any signed-in user |
| RECEIVING | Receive Sample | `/receiving` | `Samples.Receive` or `Samples.ReceiveOwnLab` |
| RECEIVING | Tracking Board | `/receiving/tracking` | `Samples.TrackAll` |
| LABORATORIES | Microbiology Laboratory (landing `/sections/microbiology-laboratory`) | | MICRO membership |
| | – Workspace | `/microbiology/workspace` | membership only |
| | – Media Preparation & Evaluation | `/laboratory-configuration/media` | `Media.Prepare` |
| | – Reference Cryovials | `/laboratory-configuration/cryovials` | `Cryovials.Manage` |
| | – Materials Stock | `/inventory/materials?lab=MICRO` | `Materials.Manage` |
| | – Equipment Inventory | `/inventory/equipment?lab=MICRO` | `Equipment.Manage` |
| LABORATORIES | Microbiology Configuration (landing `/sections/microbiology-configuration`) | | MICRO membership AND `MasterData.Manage` |
| | – Test Master | `/laboratory-configuration/test-master` | (parent permission) |
| | – Organisms | `/laboratory-configuration/organisms` | |
| | – Media Configurations | `/laboratory-configuration/media-configurations` | |
| | – Water | `/laboratory-configuration/water` | |
| | – Environmental Monitoring | `/laboratory-configuration/environmental-monitoring` | |
| | – After Cleaning | `/laboratory-configuration/after-cleaning` | |
| | – Equipment | `/laboratory-configuration/equipment` | |
| LABORATORIES | Physicochemical Laboratory (landing `/sections/physicochemical-laboratory`) | | FP membership |
| | – FP Workspace | `/physicochemical/workspace` | area `fp` |
| | – RM & PM Workspace | `/physicochemical/rm-pm-workspace` | area `rmpm` |
| | – Solution Preparation | `/preparation` | `Solutions.Prepare` |
| | – Working Standards | `/working-standards` | membership only (no menu permission) |
| | – HPLC Workspace | `/hplc-workspace` | membership only |
| | – GC Workspace | `/gc-workspace` | membership only |
| | – ICP Workspace | `/icp-workspace` | membership only |
| | – Materials Stock | `/inventory/materials?lab=FP` | `Materials.Manage` |
| | – Equipment Inventory | `/inventory/equipment?lab=FP` | `Equipment.Manage` |
| LABORATORIES | Physicochemical Configuration (landing `/sections/physicochemical-configuration`) | | FP membership AND `MasterData.Manage` |
| | – FP Test Master | `/laboratory-configuration/fp-test-master` | |
| | – RM & PM Test Master | `/laboratory-configuration/rm-pm-test-master` | |
| | – Equation Types | `/laboratory-configuration/equation-types` | |
| | – Physicochemical Instruments | `/laboratory-configuration/fp-instruments` | |
| | – Chromatography Columns | `/laboratory-configuration/columns` | |
| | – Reagents & Standards | `/laboratory-configuration/material-master` | |
| | – Solutions | `/laboratory-configuration/solution-master` | |
| | – HPLC Methods | `/laboratory-configuration/hplc-methods` | |
| | – ICP Methods | `/laboratory-configuration/icp-methods` | |
| GENERAL LABORATORY CONFIGURATION | Items | `/laboratory-configuration/items` | `Items.Manage` |
| GENERAL LABORATORY CONFIGURATION | Receiving Configuration | `/laboratory-configuration/receiving-configuration` | `MasterData.Manage` |
| GENERAL LABORATORY CONFIGURATION | Master Data Workbook | `/laboratory-configuration/master-data-workbook` | `MasterData.Manage` |
| DOCUMENT CONTROL | Document Control (landing `/sections/document-control`) | | all signed-in users |
| | – Dashboard | `/document-control` | none |
| | – My Reading List | `/document-control/my-reading-list` | none |
| | – Training Matrix | `/document-control/training-matrix` | none (menu) |
| | – Compliance Dashboard | `/document-control/compliance-dashboard` | none (menu) |
| | – Document Library | `/document-control/library` | none |
| | – Audit Trail | `/document-control/audit` | role type Reviewer, Section Head or System Administrator |
| | – Configuration | `/document-control/configuration` | `Documents.ConfigManage` |
| ADMINISTRATION | Users | `/users` | `Users.Manage` |
| ADMINISTRATION | Roles | `/roles` | `Roles.Manage` |
| REPORTS | Reports | `/reports` | any signed-in user (KPI tab needs `Kpi.View`) |
| AUDIT & COMPLIANCE | Audit Search | `/audit-search` | `Audit.View` |
| AUDIT & COMPLIANCE | OOS Tracking | `/oos-tracking` | `Oos.Manage` |
| SYSTEM | Error Monitoring | `/error-monitoring` | `System.ViewErrorLog` |

[src for the whole table: frontend/src/routes/menuConfig.ts:L76-L266]

Notes: (a) The Document Control "Training Matrix" and "Compliance Dashboard" menu items carry no permission in the menu and no `PermissionRoute`; the permission `Documents.TrainingViewMatrix` is NOT-ENFORCED (see §5.3), so the server-side gating of the training matrix page is not established by the permission list (REQUIRES CONFIRMATION, GAP-025). (b) The HPLC, GC, ICP and Working Standards links have no menu permission; write actions inside them need `Hplc.Operate` / `WorkingStandards.Qualify`, and the HPLC pages also test the System Administrator role in the page [src: frontend/src/modules/hplcWorkspace/overview/HplcWorkspacePage.tsx:L26; frontend/src/modules/icpWorkspace/entry/IcpSampleEntryPage.tsx:L44].

### 3.4 Pages reachable but not in the sidebar

| Page | Path | How a user reaches it | Note |
|---|---|---|---|
| My Profile | `/profile` | Account menu | [src: Header.tsx:L339] |
| Change Password | `/change-password` | Account menu; forced redirect | |
| Messages | `/messages` | Account menu ("Messages (N unread)") | no permission guard in frontend; `Messages.Use` NOT-ENFORCED |
| Discussions, Discussion detail | `/discussions`, `/discussions/:id` | Account menu; notification links | no guard; `Discussions.View/Create` NOT-ENFORCED |
| Section landing pages | `/sections/:slug`, `/groups/:slug` | Click a parent menu item | cards are the permission-filtered children [src: frontend/src/pages/Section.tsx:L8-L35] |
| Printable sample report | `/samples/:id/report` | Sample Summary dialog "Printable Report" | outside MainLayout |
| Certificate of Analysis | `/samples/:id/coa` | Sample Summary dialog "View COA" | outside MainLayout |
| Media lot report / Cryovial report | `/media/:id/report`, `/cryovials/:id/report` | report links in the media and cryovial lists | outside MainLayout |
| Admin password recovery | `/admin-recovery` | Link on the sign-in page | public route |
| Document detail | `/document-control/documents/:id` | Library / dashboard rows | |
| Create Role, Role detail | `/roles/new`, `/roles/:id` | Roles page | |
| Preparation wizard / record | `/preparation/new`, `/preparation/:id/edit`, `/preparation/:id` | Solution Preparation list | |
| Specifications (stand-alone) | `/laboratory-configuration/specifications` | **No link found** | GAP-034 |
| Media Evaluation | `/laboratory-configuration/media-evaluation` | **No link found** (evaluations are also reached from the media lot workspace "Evaluations" tab) | GAP-034 |

---

## 4. Screen inventory

Columns: **Screen ID** | **Name (UI label)** | **Route / location** [src] | **Module** | **Purpose** | **Roles / permissions allowed** | **Main actions** | **Important fields** | **Related workflows (names)** | **Related entities**.

"Allowed" lists the route guard and, where known, the server policy. Default role grants are in §5. Where the allowed column says "all signed-in users", the page has no `PermissionRoute`; individual actions may still be checked by the server.

### 4.1 Authentication and shell

| Screen ID | Name | Route / location | Module | Purpose | Allowed | Main actions | Important fields | Workflows | Entities |
|---|---|---|---|---|---|---|---|---|---|
| SCR-AUTH-001 | Sign in | `/login` [src: AppRoutes.tsx:L97; pages/Login.tsx:L12] | MOD-AUTH-001 | Authenticate; split screen - brand panel ("Pharmaceutical Quality Control", Laboratory Information Management System, FDA 21 CFR Part 11 / EU GMP Annex 11 badges, laboratory line art) beside the form; footer "Authorised users only. Every sign-in attempt is recorded." | Public (signed-in users are sent to `/dashboard`) | Sign In; Need help signing in? | Username, Password | Sign in; Request password reset | User, RefreshToken, LoginHistory |
| SCR-AUTH-002 | Need help signing in (mode in the sign-in card) | `/login` [src: Login.tsx:L99-L125] | MOD-AUTH-001 | Ask for a reset link | Public | Send Reset Link; Back to login; Have a recovery code from your administrator? | Username | Request password reset | PasswordResetToken |
| SCR-AUTH-003 | Password Recovery (Administrator-Assisted Account Access Recovery) | `/admin-recovery` [src: AppRoutes.tsx:L98; pages/AdminPasswordRecovery.tsx:L7] | MOD-AUTH-001 | Reset password with a one-time code from an administrator | Public | Submit Password Recovery | Username, One-Time Recovery Code (12 characters), New Password, Confirm New Password | Admin-assisted password recovery | AdminPasswordRecovery |
| SCR-AUTH-004 | Change Password | `/change-password` [src: AppRoutes.tsx:L111; pages/ChangePassword.tsx:L20] | MOD-AUTH-001 | Change own password; target of forced change | All signed-in users | Change Password | Current Password, New Password, Confirm New Password; live policy checklist | Change password | User, PasswordHistory |
| SCR-AUTH-005 | My Profile | `/profile` [src: pages/Profile.tsx:L20] | MOD-AUTH-001 | Read-only account facts | All signed-in users | (none) | Full Name, User ID, Role, Last Login, Password Last Changed | (none) | User |
| SCR-AUTH-006 | Session Timeout Warning (dialog) | any MainLayout page [src: MainLayout.tsx:L153-L170] | MOD-AUTH-001 | Warn 60 s before automatic sign-out after 20 min idle | All signed-in users | Stay signed in | countdown seconds | Session timeout | (client only) |
| SCR-NAV-001 | Section landing page | `/sections/:slug`, `/groups/:slug` [src: AppRoutes.tsx:L116-L117; pages/Section.tsx:L11] | MOD-NAV-001 | Cards for the pages the user can open in a section; on a laboratory section (MICRO / FP) each card also shows live figures from `GET /api/section-counters?lab=` (e.g. overdue, awaiting review, expiring media, expired stock, calibration overdue, open runs), refreshed every minute; on Document Control, My Reading List shows the caller's "to read" and "overdue" counts (same split as the reading list tabs) from `GET /api/section-counters/document-control` | All signed-in users; cards permission-filtered; figures only for cards the user may open, in their own laboratory | open a card | "{n} page(s) you can open here" | (navigation) | (menu config); Sample, Media, Cryovial, Material, EquipmentInventory, SolutionPreparation, HplcRun, IcpRun, DocumentTrainingAssignment |
| SCR-NAV-002 | Page not found | `*` [src: pages/NotFound.tsx:L13] | MOD-NAV-001 | Unknown address | All signed-in users | Go to Dashboard | | (navigation) | |
| SCR-NOTIF-001 | Notifications panel (bell) | Header [src: components/Header.tsx:L216-L259] | MOD-NOTIF-001 | List and mark notifications read | All signed-in users | mark read; read all | notification text, severity | View notifications | NotificationLog |

### 4.2 Dashboards

| Screen ID | Name | Route / location | Module | Purpose | Allowed | Main actions | Important fields | Workflows | Entities |
|---|---|---|---|---|---|---|---|---|---|
| SCR-DASH-001 | Administrator Command Center | `/dashboard` for role type SystemAdministrator [src: DashboardPage.tsx:L40-L44; AdminDashboardPage.tsx:L84] | MOD-DASH-001 | Totals and queues | role type System Administrator (variant chosen by role TYPE) | Refresh; tile click filters the workspace | tiles: Total samples, Total tests, Pending tests, Review queue, Approval queue, Prep configs pending, Overdue (>24h) | View dashboard | Sample, TestOrder |
| SCR-DASH-002 | Analyst dashboard ("Welcome back, …") | `/dashboard` for role type Analyst [src: AnalystDashboardPage.tsx:L24-L116] | MOD-DASH-001 | Own prioritized work | role type Analyst (server returns Forbid for the analyst endpoints otherwise [src: DashboardController.cs:L86, L110]) | Refresh; Retry | panels: My Operational Performance, Completed Today, Attention Required, Today's Laboratory Work, incubation panel (Microbiology only) | View dashboard | TestOrder, Incubation |
| SCR-DASH-003 | Reviewer Command Center | `/dashboard` for role type Reviewer [src: ReviewerDashboardPage.tsx:L84] | MOD-DASH-001 | Review queue | role type Reviewer | tile click filters the workspace | Pending review, Overdue review, Due today, Retests in progress, Completed today | View dashboard | Sample |
| SCR-DASH-004 | Section Head Command Center | `/dashboard` for any other role type (Section Head) [src: SectionHeadDashboardPage.tsx:L106] | MOD-DASH-001 | Workload, pipeline, analyst load | role type Section Head | tile click filters the workspace | Active tests, Incubating, Ready to read, Pending review, Pending approval, Overdue, Attention items; pipeline strip; charts | View dashboard | Sample, TestOrder |

A user in more than one laboratory sees lab tabs "Microbiology Laboratory" / "Physicochemical Laboratory" (`?lab=`) [src: DashboardPage.tsx:L48-L52; DashboardLabContext.tsx:L6-L9].

### 4.3 Sample receiving, tracking, preparation

| Screen ID | Name | Route / location | Module | Purpose | Allowed | Main actions | Important fields | Workflows | Entities |
|---|---|---|---|---|---|---|---|---|---|
| SCR-SAMPLE-001 | Receive Sample | `/receiving` [src: AppRoutes.tsx:L140-L142; ReceivingPage.tsx:L29] | MOD-SAMPLE-001 | Register incoming samples and send them to laboratories; sample register | `Samples.Receive` or `Samples.ReceiveOwnLab` | Receive; Refresh; row menu: Assign Analyst, Add Laboratory, Prepare Sample, Void Sample, View Full Report, Audit History, edit | filters (sample status, item type); register columns | Receive sample; Correct sample; Void sample; Add laboratory; Assign analyst | Sample, TestOrder, Item |
| SCR-SAMPLE-002 | New Sample (dialog) | opened from Receive Sample and from lab workspaces [src: NewSampleDialog.tsx:L320] | MOD-SAMPLE-001 | Choose category and enter one or more samples | as above | Next / Back; Add Another Sample; Save All Samples | Item, Production Stage, Department, Previous Product, Machine, Cause of Testing, Sampled By, Batch No., Control No., Quantity, Mfg Date, Exp Date; categories Product, Raw Material, Packaging Material, Water, Environmental Monitoring, After Cleaning | Receive sample | Sample |
| SCR-SAMPLE-003 | Edit Sample Details (dialog) | register row / selected sample panel [src: EditSampleDetailsDialog.tsx:L216] | MOD-SAMPLE-001 | Signed correction of details | `Samples.Correct`; locked once testing/review started ("Locked: testing has started for this sample") | Save (with password) | Batch Number, Control Number, Cause of Testing, Sampled By, Sample Quantity, Production Stage, Mfg/Exp Date, Previous Product, Storage Condition, Storage Time, Reason for Correction (required) | Correct sample details | Sample, ElectronicSignature |
| SCR-SAMPLE-004 | Void Sample Confirmation (dialog) | register row / selected sample panel [src: VoidSampleConfirmationDialog.tsx:L87] | MOD-SAMPLE-001 | Strike a sample from the register (signed) | `Samples.Correct` | Sign & Void | Reason for Voiding (required) | Void sample | Sample, ElectronicSignature |
| SCR-SAMPLE-005 | Add Laboratory (dialog) | register row [src: AddLaboratoryDialog.tsx:L121] | MOD-SAMPLE-001 | Send an existing sample to another laboratory (signed) | `Samples.Receive` (any lab) | save with password | Laboratory, Reason | Add laboratory to a sample | Sample, SampleSectionSignoff |
| SCR-SAMPLE-006 | Assign Responsible Analyst (dialog) | register row [src: AssignAnalystDialog.tsx:L110] | MOD-SAMPLE-001 | Assign or reassign analyst | `Samples.AssignAnalyst` | Assign | Select Analyst (or Unassigned), Reason (audit trail) | Assign analyst | TestOrder, User |
| SCR-TRACK-001 | Tracking Board | `/receiving/tracking` [src: AppRoutes.tsx:L143-L144; TrackingBoardPage.tsx:L105] | MOD-TRACK-001 | Every sample with each lab's stage | `Samples.TrackAll` | filter; reset | Laboratory, Overall Status, Product / Item, Received From/To; columns Reference, Product, Batch, Received, Overall, Microbiology, Physicochemical | View tracking board | Sample, SampleSectionSignoff |
| SCR-PREP-001 | Preparation (dialog: Prepare Sample) | opened from workspace, register and grouped panel [src: PreparationDialog.tsx:L70] | MOD-PREP-001 | Record and sign sample preparation; EM room, After Cleaning machine-part, Water point grids | lab workspace users; confirm is signed (product/RM/PM) | Confirm & Start Testing; Start Testing | Diluent, Neutralizer (required); water: Storage Condition, Storage Time | Confirm sample preparation; Item preparation configuration | SamplePreparation, ItemPreparationConfiguration |

### 4.4 Laboratory workspaces and result entry

| Screen ID | Name | Route / location | Module | Purpose | Allowed | Main actions | Important fields | Workflows | Entities |
|---|---|---|---|---|---|---|---|---|---|
| SCR-TESTING-001 | Microbiology Workspace ("{lab} Workspace") | `/microbiology/workspace` [src: AppRoutes.tsx:L130; ReceivingTestingWorkspacePage.tsx:L142] | MOD-TESTING-001 | Receive, test, review, approve microbiology samples in one page | MICRO members (others see "You are not a member of the …") | + New Sample; Refresh; Export (CSV); table / card / kanban; check samples for grouped actions | KPI tiles: Needs Preparation, Ready to Read, Awaiting Review, Overdue, Assigned to Me, Unassigned | Receive sample; Group start of incubation | Sample, TestOrder |
| SCR-TESTING-002 | FP Workspace | `/physicochemical/workspace` [src: AppRoutes.tsx:L131] | MOD-TESTING-001 | Same, FP area (finished product) | FP members with area `fp` | as above | as above | as above | as above |
| SCR-TESTING-003 | RM & PM Workspace | `/physicochemical/rm-pm-workspace` [src: AppRoutes.tsx:L132] | MOD-TESTING-001 | Same, RM & PM area | FP members with area `rmpm` | as above | as above | as above | as above |
| SCR-TESTING-004 | Selected sample panel (embedded) | right pane of the workspace [src: SelectedSampleTestingPanel.tsx:L104-L242] | MOD-TESTING-001 | Sample details and Assigned Tests | lab members | Sample Summary; Audit History; Edit Details (`Samples.Correct`); Void Sample; Prepare Sample; Open Pathogen Workflow; Open Workflow per test | ASSIGNED TO, PREP STATUS, CAUSE OF TESTING, BATCH NUMBER, … | Test a sample | Sample, TestOrder |
| SCR-TESTING-005 | Grouped Actions (embedded) | right pane when 2+ samples are checked [src: GroupedActionsPanel.tsx:L35-L363] | MOD-TESTING-001 | Start incubation or prepare many samples at once | lab members | Start Incubation (N Tests); Transfer & Start Incubation; Confirm & Start Testing (signed preparation) | Media Lot, Incubator | Group start of incubation; Confirm sample preparation | TestOrder, Media, Equipment |
| SCR-COUNT-001 | "{testCode} Workflow" dialog — count test | opened from Open Workflow [src: FloatingDialogs.tsx:L44-L79; TestWorkflowDialog.tsx:L37-L830] | MOD-COUNT-001 | Select media/incubator, incubate, transfer to stage 2, enter plate counts | `TestWorkflow.Execute` (server) | Start Incubation; Transfer to Stage 2; Record Result; Calculate; Skip Wait (`TestWorkflow.Supervise`) | Media batch, Incubator, Dilution Factor, Plate fields (count or TNTC/Uncountable), justification for dilution override | Count test; Skip minimum incubation wait | TestOrder, CountTestReading, Incubation |
| SCR-OBS-001 | Location result grids (dialogs) | opened from count/pathogen tests of EM, After Cleaning, Water [src: LocationResultGridDialog.tsx; WaterLocationResultGridDialog.tsx; PathogenLocationResultGridDialog.tsx] | MOD-OBS-001 | One result per location | `TestWorkflow.Execute` | Submit Results | per location: Plate Readings, Calculated/Average, Status; pathogen grid: Growth Observed? Yes/No | Batch location results | SampleLocation, ResultRecord |
| SCR-PHYSRESULT-001 | Physicochemical result panels (Measurement, Gravimetric, Qualitative, Titration, Dissolution, Disintegration, Weight Variation) | inside the workflow dialog [src: TestWorkflowDialog.tsx:L304-L427] | MOD-PHYSRESULT-001 | Enter and sign physchem results | lab members (server policy per endpoint REQUIRES CONFIRMATION) | "Sign and record result" | Equipment (Physicochemical), Analysis time, Observation, Comment, readings (varies by panel) | Record physicochemical result | TestAnalysis, ResultRecord, ElectronicSignature |
| SCR-PATH-001 | Pathogen step dialog | opened from Open Workflow for a pathogen test [src: PathogenStepDialog.tsx:L143-L166] | MOD-PATH-001 | Broth enrichment, selective plating, confirmatory plating, biochemical test | `TestWorkflow.Execute` | Start Incubation; Record Observation; Submit Observations; Conclude as Detected; Proceed to Biochemical Confirmation | Media lot, Incubator, observation options (No growth / Growth, does not match expected appearance / Growth matching expected appearance) | Pathogen test step chain | WorkflowStepResult, PathogenObservation, ConfirmatoryPlateObservation |
| SCR-PATH-002 | Pathogen Testing Session Workspace | button "Open Pathogen Workflow" [src: PathogenSessionDialog.tsx:L191-L197] | MOD-PATH-001 | Six steps: Overview, Shared TSB, Test Workflows, Primary Matrix, Confirmatory Plating, Review & Complete | `TestWorkflow.Execute`; Reset Session needs `TestWorkflow.Supervise` | Start TSB Enrichment; Save Observations; Start Confirmatory Incubation; Complete Testing Session; Reset Session | TSB media lot, incubator, location × test matrix | Pathogen session; Reset pathogen session | PathogenObservation, LocationPathogenObservation |

### 4.5 Review, approval, reports

| Screen ID | Name | Route / location | Module | Purpose | Allowed | Main actions | Important fields | Workflows | Entities |
|---|---|---|---|---|---|---|---|---|---|
| SCR-REVIEW-001 | Sample Summary (dialog) | status badge or "Sample Summary" button [src: SampleSummaryDialog.tsx:L357] | MOD-REVIEW-001 | Review results, sign review, sign approval decision, close testing, open reports | review: `Samples.Review`; approval/close: `Samples.Approve`; return: `Samples.Review` | Submit Review; Submit Decision; Close Testing; Return to Analyst; Printable Report; View COA; Export PDF | section chips, Workflow History, test results, signatures | Technical review; Approval decision; Close testing | Sample, SampleSectionSignoff, ElectronicSignature |
| SCR-REVIEW-002 | Return Test to Analyst (dialog) | Sample Summary [src: components/ReturnToAnalystDialog.tsx:L62] | MOD-REVIEW-001 | Send a count test back for re-entry | `Samples.Review`; count tests with status Result Entered; **no signature** | Confirm Return | Reason for Return (optional) | Return count test to analyst | TestOrder, TestReturnEvent |
| SCR-APPROVAL-001 | Approval & Electronic Signatures (card) | inside Sample Summary [src: ApprovalSignaturesCard.tsx:L143-L475] | MOD-APPROVAL-001 | Submit Technical Review / Submit Release Decision | as above | Submit Review; Submit Decision | decisions: Approve; Not Conform (Final Conclusion); New Sample Required; Retest Retained Sample; Tests to Retest; two analysts; Certificate Remarks | Technical review; Approval decision; Retest or new sample | SampleSectionSignoff |
| SCR-APPROVAL-002 | Close Testing (dialog) | Sample Summary [src: dialogs/CloseTestingDialog.tsx:L67] | MOD-APPROVAL-001 | A lab closes its remaining tests after another lab rejected the sample (signed) | `Samples.Approve` | Sign & Close | Reason | Close testing | SampleSectionSignoff |
| SCR-SIG-001 | Electronic Signature (shared dialog) | many screens [src: components/SignatureDialog.tsx:L52-L75] | MOD-SIG-001 | Re-type password to sign | any user performing a signed action | Sign | Password, Comment (optional) | (all signed workflows) | ElectronicSignature |
| SCR-COA-001 | Sample summary report ("sample summary report") | `/samples/:id/report` [src: AppRoutes.tsx:L102; SampleReportPage.tsx:L108] | MOD-COA-001 | Printable record of a sample | all signed-in users (no route guard; server scope not verified) | Print / Save PDF | sections: sample identity, dates & personnel, sample preparation, test results, workflow timeline, electronic signatures; footer "Any printed copy is uncontrolled." | Print sample report | Sample, ArchivedRecord |
| SCR-COA-002 | Certificate of Analysis | `/samples/:id/coa` [src: AppRoutes.tsx:L103; SampleCoaPage.tsx:L273] | MOD-COA-001 | Certificate for an approved or rejected sample | all signed-in users; only once approved or rejected | variant toggle (Combined / per section); print | Test Results, Remarks, Overall Conclusion, Reviewer, Approver | View certificate of analysis | Sample |
| SCR-OOS-001 | Out-of-Specification Tracking | `/oos-tracking` [src: AppRoutes.tsx:L216-L217; OosTrackingPage.tsx:L64] | MOD-OOS-001 | OOS groups and their retests; investigation documents | `Oos.Manage` | Upload Investigation; Manage Investigation; Supersede; download | Retest Sample, Parent / Origin, Retest Type, Status; documents Current/Superseded/Voided | OOS investigation documents | OosInvestigationDocument |

### 4.6 Microbiology media, reference strains, organisms

| Screen ID | Name | Route / location | Module | Purpose | Allowed | Main actions | Important fields | Workflows | Entities |
|---|---|---|---|---|---|---|---|---|---|
| SCR-MEDIA-001 | Media Preparation & Evaluation | `/laboratory-configuration/media` [src: AppRoutes.tsx:L157-L158; MediaPage.tsx:L21] | MOD-MEDIA-001 | Media lots, evaluations, release, storage; tabs "Media Lots" / "Approved Media" | `Media.Prepare`; release decision `Media.Release` | prepare lot; record evaluations; release/reject (signed); Mark out of stock; storage Move | lot register; workspace tabs Preparation Overview, Evaluations, Release & Audit Trail, Storage | Prepare media lot; Evaluate media lot; Release media lot; Media storage | Media, MediaEvaluation, MediaStorageEntry |
| SCR-MEDIA-002 | Media lot report | `/media/:id/report` [src: AppRoutes.tsx:L104; MediaReportPage.tsx:L31] | MOD-MEDIA-001 | Printable media lot record | all signed-in users (no guard) | print | summary, signatures | Print media report | Media |
| SCR-MEDIA-003 | Media Evaluation | `/laboratory-configuration/media-evaluation` [src: AppRoutes.tsx:L160-L161; MediaEvaluationPage.tsx:L26] | MOD-MEDIA-001 | List evaluations (Assigned / InProgress / Completed) | `TestWorkflow.Execute`; **not linked from the menu** | Select; Record Incubation; Record Result | challenge organisms, cryovial / lyophilized disk, incubation, result | Evaluate media lot | MediaEvaluation |
| SCR-MEDIACFG-001 | Media Configurations | `/laboratory-configuration/media-configurations` [src: AppRoutes.tsx:L230; MediaConfigurationPage.tsx:L21] | MOD-MEDIACFG-001 | Media products, conditions, configurations | `MasterData.Manage`; product code change = role type Section Head only | Add / Rename / Delete product; change code (signed) | product code, name; incubation conditions | Configure media; Change media product code | MediaProduct, MediaConfiguration, MediaIncubationCondition |
| SCR-CRYO-001 | Cryovials ("Reference Cryovials") | `/laboratory-configuration/cryovials` [src: AppRoutes.tsx:L163-L164; CryovialsPage.tsx:L25] | MOD-CRYO-001 | Prepare working cryovial batches from approved lyophilized material; tabs "Cryovials" / "Approved Cryovials" | `Cryovials.Manage`; approve `Cryovials.Approve` | prepare; approve/reject (signed); thaw; Change Storage Location; destroy | code, organism, storage, vials remaining, expiry | Prepare cryovial batch; Approve cryovial batch; Thaw / move / destroy | Cryovial |
| SCR-CRYO-002 | Cryovial report | `/cryovials/:id/report` [src: AppRoutes.tsx:L105; CryovialReportPage.tsx:L27] | MOD-CRYO-001 | Printable cryovial record | all signed-in users (no guard) | print | summary, signatures | Print cryovial report | Cryovial |
| SCR-ORG-001 | Organisms | `/laboratory-configuration/organisms` [src: AppRoutes.tsx:L227; OrganismsPage.tsx:L15] | MOD-ORG-001 | Canonical organism list | `MasterData.Manage` | add / edit / delete | scientificName, atccNumber, commonName, description | Maintain organisms | Organism |

### 4.7 Inventory and equipment

| Screen ID | Name | Route / location | Module | Purpose | Allowed | Main actions | Important fields | Workflows | Entities |
|---|---|---|---|---|---|---|---|---|---|
| SCR-MATERIAL-001 | Materials Stock | `/inventory/materials` [src: AppRoutes.tsx:L254-L255; MaterialsPage.tsx:L53] | MOD-MATERIAL-001 | Receive and track lots; documents | `Materials.Manage`; supersede/void documents `Materials.DocumentControl` | add material; upload / supersede / void documents | Type, Material Name, Manufacturer, Batch/Lot No., Received, Expiry, Code, Location, Qty Received | Receive material lot | Material, MaterialDocument |
| SCR-EQUIP-001 | Equipment Inventory ("Equipment") | `/inventory/equipment` [src: AppRoutes.tsx:L257-L258; EquipmentInventoryPage.tsx:L41] | MOD-EQUIP-001 | Instrument register; tabs "Equipment Register" / "Active Equipment" | `Equipment.Manage`; supersede/void documents `Equipment.DocumentControl` | register equipment; details and documents | Type, Manufacturer, Serial No., Firmware, Code, Location, Calibration Due, Laboratory, Status | Register equipment | EquipmentInventory, EquipmentDocument |
| SCR-EQUIP-002 | Laboratory Configuration: Equipment | `/laboratory-configuration/equipment` [src: AppRoutes.tsx:L236; EquipmentPage.tsx:L135] | MOD-EQUIP-002 | Link inventory assets, set points, autoclave programs; tabs Configured Equipment, Autoclave Programs / Loads, Configuration History | `MasterData.Manage` | Edit Incubator Set Point; Select Equipment from Master Inventory | set point, program | Configure equipment | Equipment, AutoclaveProgram, IncubatorSetPointHistory |
| SCR-EQUIP-003 | Physicochemical Instruments | `/laboratory-configuration/fp-instruments` [src: AppRoutes.tsx:L237; FpInstrumentsPage.tsx:L109] | MOD-EQUIP-002 | Instrument types for physchem tests | `MasterData.Manage` | add / edit | instrument type, linked asset | Configure equipment | Equipment |
| SCR-COLUMN-001 | Chromatography Columns | `/laboratory-configuration/columns` [src: AppRoutes.tsx:L238; ChromatographyColumnsPage.tsx:L45] | MOD-COLUMN-001 | Column master | `MasterData.Manage` | add / edit / Deactivate Chromatography Column | column parameters, instrument compatibility | Maintain columns | ChromatographyColumn |

### 4.8 Master data and configuration

| Screen ID | Name | Route / location | Module | Purpose | Allowed | Main actions | Important fields | Workflows | Entities |
|---|---|---|---|---|---|---|---|---|---|
| SCR-ITEM-001 | Items | `/laboratory-configuration/items` [src: AppRoutes.tsx:L219-L220; ItemsPage.tsx:L11] | MOD-ITEM-001 | Items and the tests auto-assigned on receipt; preparation configuration; specifications; documents (SOP, Verification Report) | `Items.Manage`; document upload `Items.DocumentUpload` | Add item; edit; freeze / unfreeze; delete; approve preparation configuration | workspace tabs Overview, Preparation Configuration, Audit History; specification parameter dialog | Configure item and tests; Item preparation configuration; Item specifications | Item, Specification, ItemPreparationConfiguration |
| SCR-SPEC-001 | Specifications (stand-alone) | `/laboratory-configuration/specifications` [src: AppRoutes.tsx:L229; SpecificationsPage.tsx:L4] | MOD-SPEC-001 | UNCLEAR (not read); not linked | `MasterData.Manage` | UNCLEAR | UNCLEAR | Item specifications | Specification |
| SCR-TESTMASTER-001 | Test Master (Microbiology) | `/laboratory-configuration/test-master` [src: AppRoutes.tsx:L223; TestMasterPage.tsx:L35] | MOD-TESTMASTER-001 | Test definitions, steps, media, limits | `MasterData.Manage` | Add Test; add step; edit; freeze | steps, media per step, stage replicates | Define a test | TestDefinition, TestWorkflowStep |
| SCR-TESTMASTER-002 | FP Test Master | `/laboratory-configuration/fp-test-master` [src: AppRoutes.tsx:L224] | MOD-TESTMASTER-001 | Same, finished product | `MasterData.Manage` | as above | + titration configuration | Define a test | as above |
| SCR-TESTMASTER-003 | RM & PM Test Master | `/laboratory-configuration/rm-pm-test-master` [src: AppRoutes.tsx:L225] | MOD-TESTMASTER-001 | Same, raw and packaging material | `MasterData.Manage` | as above | as above | Define a test | as above |
| SCR-EQTYPE-001 | Equation Types | `/laboratory-configuration/equation-types` [src: AppRoutes.tsx:L226; EquationTypesPage.tsx:L67] | MOD-EQTYPE-001 | Calculation equations used by tests | `MasterData.Manage` | list (create/update calls not found) | | (view only?) | EquationType (enum) |
| SCR-RECVCFG-001 | Receiving Configuration | `/laboratory-configuration/receiving-configuration` [src: AppRoutes.tsx:L234; ReceivingConfigurationPage.tsx:L290] | MOD-RECVCFG-001 | Samplers and Cause of Testing lists | `MasterData.Manage` | add / rename / delete | Samplers, Cause of Testing | Maintain receiving lists | Sampler, CauseOfTesting |
| SCR-WORKBOOK-001 | Master Data Workbook | `/laboratory-configuration/master-data-workbook` [src: AppRoutes.tsx:L228; MasterDataWorkbookPage.tsx:L15] | MOD-WORKBOOK-001 | Load master data from Excel | `MasterData.Manage`; verifier must differ from loader | Download workbook; Verify; Load and sign | workbook file, comment | Load workbook; Verify workbook | MasterDataImport |
| SCR-WATER-001 | Water (configuration) | `/laboratory-configuration/water` [src: AppRoutes.tsx:L231; WaterConfigPage.tsx:L24] | MOD-WATER-001 | Water systems, sampling points, tests, limits | `MasterData.Manage` | add / edit / delete | system, location, limits | Configure water | WaterDepartment, WaterSamplingPoint, SamplingConfiguration |
| SCR-EM-001 | Environmental Monitoring (configuration) | `/laboratory-configuration/environmental-monitoring` [src: AppRoutes.tsx:L232; EMConfigPage.tsx:L24] | MOD-EM-001 | Departments, rooms, grades A-D, per-room limits | `MasterData.Manage` | add / edit / delete | grade, test type, alert/action limits | Configure EM | Department, EMRoom/Room, RoomTestConfiguration |
| SCR-AC-001 | After Cleaning (configuration) | `/laboratory-configuration/after-cleaning` [src: AppRoutes.tsx:L233; AfterCleaningConfigPage.tsx:L25] | MOD-AC-001 | Machines, parts, swab/rinse/pathogen tests | `MasterData.Manage` | add / edit / delete | part tests, pathogens | Configure After Cleaning | Machine, MachinePart, MachinePartConfiguration |
| SCR-REAGENT-001 | Reagents & Reference Standards | `/laboratory-configuration/material-master` [src: AppRoutes.tsx:L239; MaterialMasterPage.tsx:L57] | MOD-REAGENT-001 | Master definitions of reagents, indicators, reference standards | `MasterData.Manage` | add / edit / set active | category, unit | Maintain reagents and standards | MaterialMasterEntry |
| SCR-SOLNM-001 | Solutions Master | `/laboratory-configuration/solution-master` [src: AppRoutes.tsx:L240; SolutionMasterPage.tsx:L36] | MOD-SOLNM-001 | Recipes for mobile phases, diluents, titrants | `MasterData.Manage` | add / edit with "Reason for Modification" | components, shelf life, pH, titrant standardization | Maintain solution recipes | SolutionMaster, SolutionComponent |
| SCR-HPLCM-001 | Chromatography Methods Master | `/laboratory-configuration/hplc-methods` [src: AppRoutes.tsx:L241; HplcMethodsPage.tsx:L32] | MOD-HPLCM-001 | HPLC and GC methods | `MasterData.Manage` | add / edit; history | tabs Column, Elution & Detection, Solutions, Analytes (HPLC); Column, GC Conditions & Oven, Analytes (GC) | Maintain chromatography methods | HplcMethod |
| SCR-ICPM-001 | ICP Methods Master | `/laboratory-configuration/icp-methods` [src: AppRoutes.tsx:L242; IcpMethodsPage.tsx:L28] | MOD-ICPM-001 | ICP-OES methods | `MasterData.Manage` | add / edit; set active (reason); history | tabs General, Calibration, Optional Checks, Sample Prep, Elements | Maintain ICP methods | IcpMethod |

### 4.9 Physicochemical operations

| Screen ID | Name | Route / location | Module | Purpose | Allowed | Main actions | Important fields | Workflows | Entities |
|---|---|---|---|---|---|---|---|---|---|
| SCR-HPLC-001 | HPLC Workspace (instruments overview) | `/hplc-workspace` [src: AppRoutes.tsx:L174; HplcWorkspacePage.tsx:L21] | MOD-HPLC-001 | Instruments and their state; tabs All / In use / Available / Unavailable | all signed-in users (no route guard); operating needs `Hplc.Operate` | open instrument; start run | | HPLC run | Equipment, HplcRun |
| SCR-HPLC-002 | HPLC instrument workspace | `/hplc-workspace/:instrumentId[/run/:runId[/sst, /samples, /evidence or /history]]` [src: AppRoutes.tsx:L175-L184; HplcInstrumentWorkspace.tsx:L55] | MOD-HPLC-001 | Run overview; tabs Overview, System Suitability, Samples, Evidence, Run History | `Hplc.Operate` for writes | confirm SST (signed); assign / remove samples; upload evidence; Complete run; abandon run | SST values, standard entries, evidence files | HPLC run | HplcRun |
| SCR-HPLC-003 | Start HPLC Run wizard | `/hplc-workspace/:instrumentId/new-run` [src: AppRoutes.tsx:L176; StartHplcRunWizard.tsx:L39] | MOD-HPLC-001 | Select method, column, mobile phases, review | `Hplc.Operate` | Start run | method, column, mobile phase preparations | HPLC run | HplcRun |
| SCR-HPLC-004 | HPLC sample entry | `/hplc-workspace/:instrumentId/run/:runId/sample/:runSampleId` [src: AppRoutes.tsx:L182; HplcSampleEntryPage.tsx:L45] | MOD-HPLC-001 | Replicate entry, calculation preview, send for review (signed) | `Hplc.Operate` | Save replicates; Send for review | Replicate, Actual Weight (mg), Peak Response | HPLC run | HplcRun, TestAnalysis |
| SCR-HPLC-005 | Working standard qualification entry | `/hplc-workspace/:instrumentId/run/:runId/qualification/:runSampleId` [src: AppRoutes.tsx:L183; QualificationEntryPage.tsx:L27] | MOD-HPLC-001 | Qualification replicates | `Hplc.Operate` / `WorkingStandards.Qualify` | save; submit (signed per verified §6 item 16) | replicates | Working standard qualification | WorkingStandardQualification |
| SCR-GC-001 | GC Workspace (same screens as SCR-HPLC-001..005) | `/gc-workspace/*` [src: AppRoutes.tsx:L186-L197] | MOD-GC-001 | GC runs, shares HPLC components | as HPLC | as HPLC | GC conditions and oven program | GC run | HplcRun (technique) |
| SCR-ICP-001 | ICP-OES Workspace (instruments overview) | `/icp-workspace` [src: AppRoutes.tsx:L200; IcpWorkspacePage.tsx:L22] | MOD-ICP-001 | Instruments, KPI tiles (Instruments, Available, In use, Cal. pending, Unavailable) | all signed-in users (no route guard) | open instrument; Start ICP Run | | ICP run | Equipment, IcpRun |
| SCR-ICP-002 | ICP run workspace | `/icp-workspace/:instrumentId[/run/:runId[/calibration, /ccv, /samples, /evidence or /history]]` [src: AppRoutes.tsx:L201-L209; IcpRunWorkspace.tsx:L38] | MOD-ICP-001 | Tabs Overview, Calibration, CCV, Samples, Evidence, Run History | `Hplc.Operate` for writes | confirm calibration (signed); CCV; assign samples; abandon run | calibration lots, CCV measured concentration | ICP run | IcpRun |
| SCR-ICP-003 | ICP sample entry | `/icp-workspace/:instrumentId/run/:runId/sample/:runSampleId` [src: AppRoutes.tsx:L208; IcpSampleEntryPage.tsx:L40] | MOD-ICP-001 | Replicates, server preview, send for review (signed) | `Hplc.Operate` | Save Replicates; Send for review | Amount Unit, replicates | ICP run | IcpRun |
| SCR-SOLN-001 | Solution Preparation (list) | `/preparation` [src: AppRoutes.tsx:L166-L167; PreparationListPage.tsx:L17] | MOD-SOLN-001 | Preparations of mobile phases, diluents, titrants | `Solutions.Prepare` | Start Preparation; Resume Preparation; View Record | Code, Type, Solution Recipe, HPLC Method, Status, Prepared At, Expires At, Prepared By | Solution preparation | SolutionPreparation |
| SCR-SOLN-002 | Preparation wizard | `/preparation/new`, `/preparation/:id/edit` [src: AppRoutes.tsx:L168-L169; PreparationWizardPage.tsx:L26] | MOD-SOLN-001 | Choose recipe, stock lots, final volume, pH; sign | `Solutions.Prepare` | complete (signed) | Solution Type, Solution Recipe, HPLC Method, Final Volume, Measured pH | Solution preparation | SolutionPreparationComponent |
| SCR-SOLN-003 | Preparation record | `/preparation/:id` [src: AppRoutes.tsx:L170; PreparationRecordPage.tsx:L24] | MOD-SOLN-001 | Record, history, current factor, standardization | `Solutions.Prepare` | standardize titrant (signed); cancel / discard (reason) | Code, Started By / At, Prepared By / At, Expires At | Titrant standardization | SolutionPreparation, TitrantStandardization |
| SCR-WS-001 | Working Standards | `/working-standards` [src: AppRoutes.tsx:L211; WorkingStandardsPage.tsx:L27] | MOD-WS-001 | Lots and qualifications; tabs "Lots" / "Qualifications" | all signed-in users (no route guard); create needs `WorkingStandards.Qualify`; review `Samples.Review`; approve `Samples.Approve` (server) | new qualification; review / return / reject / approve (signed) | KPI: Active Lots, Valid, Due Soon, Expired / Depleted, Pending Quals | Working standard qualification | WorkingStandardQualification |

### 4.10 Reports, audit, errors, collaboration

| Screen ID | Name | Route / location | Module | Purpose | Allowed | Main actions | Important fields | Workflows | Entities |
|---|---|---|---|---|---|---|---|---|---|
| SCR-REPORTS-001 | Reports — Overview tab | `/reports` [src: AppRoutes.tsx:L115; pages/Reports.tsx:L30] | MOD-REPORTS-001 | Recent results overview | all signed-in users | View in Search | status legend (Out of Spec, Alert / Action Level, Pending Approval, Pending Review); chip "Live Database Data" (static text) | Search, trend and export | ResultRecord |
| SCR-REPORTS-002 | Reports — Record Search tab | `/reports?tab=search` | MOD-REPORTS-001 | Search recorded results | all signed-in users | Search Records; Export CSV; Export PDF | Result Type, Result Level, Category, Test, Location / Point, Status, Approval Status, Date Range | Search, trend and export | ResultRecord |
| SCR-REPORTS-003 | Reports — Media / GPT tab | `/reports?tab=media-gpt` | MOD-REPORTS-001 | Media / GPT results | all signed-in users | Export CSV (Flattened) | labels not extracted | Search, trend and export | MediaEvaluation |
| SCR-REPORTS-004 | Reports — Reference Strains tab | `/reports?tab=reference-strains` | MOD-REPORTS-001 | Reference-strain records | all signed-in users | Export CSV | labels not extracted | Search, trend and export | Cryovial |
| SCR-REPORTS-005 | Reports — Trending & Analysis tab | `/reports?tab=trending` | MOD-REPORTS-001 | Trend charts and comparison | all signed-in users | export CSV | Parameter / Test, Product / Item, Category, Date Range, Compare With; **contains hardcoded fallback options and default criteria (GAP-002)** | Search, trend and export | ResultRecord |
| SCR-KPI-001 | Reports — KPI / Performance tab | `/reports?tab=performance` [src: Reports.tsx:L34-L41, L152-L154] | MOD-KPI-001 | KPI tiles (Flow, Quality, Micro), workload, definitions | `Kpi.View` (tab hidden otherwise); other analysts' figures need `Kpi.ViewTeam` | Export PDF; Definitions; Workflow Profiles; Retry | Category, From, To, Location / subject, Test | View KPIs | WorkflowProfileVersion, WorkloadWeight |
| SCR-KPI-002 | Workflow Profiles (dialog/panel) | button on KPI tab [src: WorkflowProfilesDialog.tsx:L15; WorkflowProfilesPanel.tsx:L56] | MOD-KPI-001 | View / add signed workflow profile versions | `Kpi.View`; edit `Kpi.ConfigureProfiles` | add version (signed); history | Workflow type, Work units, Target (days), Status | Workflow profile versions | WorkflowProfileVersion |
| SCR-AUDIT-001 | Audit Trail ("Audit Search") | `/audit-search` [src: AppRoutes.tsx:L213-L214; AuditSearchPage.tsx:L27] | MOD-AUDIT-001 | Search audit events; inspect diff and traceability chain | `Audit.View` | Search Audit Trail; Reset; Inspect Event & Traceability Chain; View Complete History | From/To Date, Action, Entity Category, Sample Reference, Batch Number, Control Number, Media Lot Number, Cryovial / RS Code, User ID; capped at 300 latest | Audit search | AuditLog, AuditEventChange |
| SCR-ERR-001 | Error Monitoring | `/error-monitoring` [src: AppRoutes.tsx:L264-L265; ErrorMonitoringPage.tsx:L11] | MOD-ERR-001 | Technical incidents | `System.ViewErrorLog` | Export CSV; Mark resolved; Reopen; severity Override | Severity, Source, Status, Active from/to, Resolution notes | Review technical incidents | Incident, ErrorLog |
| SCR-MSG-001 | Messages ("Direct & Group Messages") | `/messages` [src: AppRoutes.tsx:L114; MessagesPage.tsx:L22] | MOD-MSG-001 | Conversations | all signed-in users | New Message; send | recipients, Group Title, message | Messaging | Conversation, DirectMessage |
| SCR-DISC-001 | Discussions & Knowledge Sharing | `/discussions` [src: AppRoutes.tsx:L112; DiscussionsFeedPage.tsx:L30] | MOD-DISC-001 | Posts feed | all signed-in users | New Discussion; Mark Important; delete | 11 categories | Discussions | DiscussionPost |
| SCR-DISC-002 | Discussion detail | `/discussions/:id` [src: AppRoutes.tsx:L113; DiscussionDetailPage.tsx:L43] | MOD-DISC-001 | Read, edit (versioned), comment, attachments | all signed-in users; edit-any needs `Discussions.EditAny` | Edit Post; Post Comment; delete | Title, Category, Content | Discussions | DiscussionPost, DiscussionComment |

### 4.11 Administration

| Screen ID | Name | Route / location | Module | Purpose | Allowed | Main actions | Important fields | Workflows | Entities |
|---|---|---|---|---|---|---|---|---|---|
| SCR-USERS-001 | User Management | `/users` [src: AppRoutes.tsx:L271-L272; UsersPage.tsx:L33] | MOD-USERS-001 | Accounts | `Users.Manage` | Create User; Edit Profile; Change Role (reason); Disable / Enable Account (reason); Unlock Account; Reset Password via Email; Admin-Assisted Password Recovery; Force Password Change; Permanently Delete User | Full Name, Username, Email, Password, Role | Manage user accounts | User, Role |
| SCR-USERS-002 | Laboratory Sections (dialog) | row action [src: UserSectionsDialog.tsx:L188] | MOD-USERS-001 | Set the user's laboratory memberships (department, section, physchem area) | `Users.Manage` | Save | "All sections (entire department)", Area | Assign laboratory membership | UserOrgMembership |
| SCR-ROLES-001 | Roles | `/roles` [src: AppRoutes.tsx:L275; RolesPage.tsx:L15] | MOD-ROLES-001 | Role list | `Roles.Manage` | Create Role; open role | Role, Type, Status, Users | Manage roles | Role |
| SCR-ROLES-002 | Create Role | `/roles/new` [src: AppRoutes.tsx:L276; CreateRolePage.tsx:L18] | MOD-ROLES-001 | Four-step wizard Name, Base Type, Permissions, Review | `Roles.Manage` | Create Role | Role Name, Description, Base Type, permissions | Manage roles | Role, RolePermission |
| SCR-ROLES-003 | Role detail | `/roles/:id` [src: AppRoutes.tsx:L277; RoleDetailPage.tsx:L18] | MOD-ROLES-001 | Edit name, description, permissions; delete custom roles | `Roles.Manage` | Save Changes; Delete Role (custom only) | permission matrix (Enforced / Legacy-only chips), holders list | Manage roles | Role, RolePermission |

### 4.12 Document Control

| Screen ID | Name | Route / location | Module | Purpose | Allowed | Main actions | Important fields | Workflows | Entities |
|---|---|---|---|---|---|---|---|---|---|
| SCR-DC-001 | Document Control Dashboard | `/document-control` [src: AppRoutes.tsx:L246; DocumentControlDashboardPage.tsx:L47] | MOD-DC-001 | Governance overview | all signed-in users (no route guard) | register document (opens dialog) | content not enumerated | Register document | DocumentMaster |
| SCR-DC-002 | My Reading List | `/document-control/my-reading-list` [src: AppRoutes.tsx:L247; MyReadingListPage.tsx:L86] | MOD-DC-002 | Documents the user must read and acknowledge | all signed-in users | open controlled PDF; acknowledge | acknowledgement checkbox "I confirm the evidentiary statement above." (no password) | Reading acknowledgement | DocumentAcknowledgementRecord, DocumentTrainingAssignment |
| SCR-DC-003 | Training Matrix | `/document-control/training-matrix` [src: AppRoutes.tsx:L248; TrainingMatrixPage.tsx:L59] | MOD-DC-002 | Who is trained on which document | all signed-in users (UI); server gate REQUIRES CONFIRMATION | view grid | | Training matrix | DocumentTrainingAssignment |
| SCR-DC-004 | Training Compliance Dashboard | `/document-control/compliance-dashboard` [src: AppRoutes.tsx:L249; ComplianceDashboardPage.tsx:L37] | MOD-DC-002 | Compliance metrics | all signed-in users (UI) | view | content not enumerated | Training matrix | |
| SCR-DC-005 | Document Library | `/document-control/library` [src: AppRoutes.tsx:L250; DocumentLibraryPage.tsx:L57] | MOD-DC-001 | Search all controlled documents | all signed-in users | open document; View Controlled PDF; Register; Void Document Master | Company Document Code, Title | Register document; Void document | DocumentMaster |
| SCR-DC-006 | Document detail | `/document-control/documents/:id` [src: AppRoutes.tsx:L251; DocumentDetailPage.tsx:L82] | MOD-DC-001 | Document, revisions, audit trail; revision lifecycle dialogs | all signed-in users; actions governed by fixed role-type rules and assignments (§5.4) | edit draft metadata; create revision; upload files; submit for technical review; assign approver; approval workspace (signed); periodic review; cancel draft; void | tabs Document, Revisions, Audit trail | Revision, review and approval; Periodic review; Void document | DocumentRevision, DocumentReviewTask, DocumentApprovalTask |
| SCR-DC-007 | Document Control Audit Trail | `/document-control/audit` [src: AppRoutes.tsx:L252; DocumentAuditPage.tsx:L36] | MOD-DC-003 | Append-only log of document actions | role type Reviewer, Section Head, System Administrator (menu and server) | filter; export | | Document audit | AuditLog |
| SCR-DC-008 | Document Control Configuration | `/document-control/configuration` [src: AppRoutes.tsx:L268-L269; DocumentConfigurationPage.tsx:L46] | MOD-DC-004 | Types, departments & sections, numbering, settings | `Documents.ConfigManage` (route); service also checks role type System Administrator [src: DocumentAuthorizationService.cs:L229-L233] | add / edit | tabs Document Types, Departments & Sections, Numbering Configuration, Module Settings | Configure Document Control | DocumentType, DocumentDepartment, DocumentSection |

**Screen count:** counted in the final report (see end of file §14). Dialogs listed as screens are those that carry their own purpose and signature; minor dialogs (confirmations, uploads) are folded into their parent screen row.

---

## 5. Role and permission inventory

### 5.1 Roles (default grants)

Roles are **admin-editable**. Four system roles are created on a fresh database; an administrator holding `Roles.Manage` can create custom roles, rename any role, and replace the permission set of any role including system roles; only custom roles that no user holds can be deleted [src: knowledge/_raw/verified-roles-permissions.md §3; backend/MicroLIMS.Application/Services/RoleService.cs:L63-L171]. Everything below is therefore a **default**, not a fixed rule. Real grants live in database rows (REQUIRES CONFIRMATION on the target system, GAP-015).

| Role ID | Role (UI name) | Base role type | Default permission codes | Notes |
|---|---|---|---|---|
| ROLE-SYSTEMADMINISTRATOR | System Administrator | SystemAdministrator (0) | all 50 | Treated as a member of every laboratory (no scope restriction) [src: UserSectionScopeService.cs:L26-L29]. Segregation-of-duties still applies. |
| ROLE-SECTIONHEAD | Section Head | SectionHead (1) | 43 (all except `Users.Manage`, `Roles.Manage`, `Reporting.Admin`, `System.ViewErrorLog`, `System.ViewSecurityAudit`, `Documents.Review`, `Documents.ConfigManage`) | The Document Control comments call this the "Document Controller" [src: DocumentAuthorizationService.cs:L152]. |
| ROLE-REVIEWER | Reviewer | Reviewer (2) | 15 | No `Samples.Approve`, no `Samples.Receive`. |
| ROLE-ANALYST | Analyst | Analyst (3) | 15 | No review, approval, correction, assign-analyst, `Items.Manage`, `MasterData.Manage`, `Kpi.*` or `Dashboards.*`. |
| (custom roles) | any name | one of the four base types | starts as a copy of the base role's set, then editable | Anything still checked by role TYPE treats a custom role like its base type [src: verified §3]. |

Candidate roles from the prompt: **Reception** and **QA** roles are NOT FOUND as seeded roles (searched `DbSeeder.cs` and the role enum; no match). They could only exist as custom roles created on a deployment (REQUIRES CONFIRMATION). "Document Control roles" do not exist as separate roles: Document Control uses the four role types plus per-document assignments (§5.4).

Role changes: a user cannot change their own role, cannot disable themselves and cannot delete themselves [src: UserService.cs:L163-L172, L208; UserDeletionService.cs:L63]. A token's permission set changes only at the next access token (default 15 minutes); role-type change, disable, lock or password change take effect on the next request [src: JwtConfiguration.cs:L30-L34; AccessTokenRevalidator.cs:L55-L72].

### 5.2 Permission codes (50), default grants and enforcement

`Enforced` = the code is in `PermissionConstants.Enforced` (40 codes); the Roles screen shows a green "Enforced" chip or a warning "Legacy-only" chip [src: backend/MicroLIMS.Shared/Constants/PermissionConstants.cs:L109-L143; frontend/src/modules/roles/components/PermissionMatrix.tsx:L70-L83]. "NOT-ENFORCED" codes are referenced nowhere outside the constants file and the seeder (verified §1). Granting or revoking them changes nothing. Y = default grant, - = not granted. Sys.Admin holds all 50.

| Permission ID | Code | Enforcement | SysAdmin | SectionHead | Reviewer | Analyst | Purpose (seed description) |
|---|---|---|---|---|---|---|---|
| PERM-USERS-MANAGE | Users.Manage | Enforced | Y | - | - | - | Manage user accounts (create, edit, lock/unlock, password resets). |
| PERM-ROLES-MANAGE | Roles.Manage | Enforced | Y | - | - | - | Manage roles and their granted permissions. |
| PERM-AUDIT-VIEW | Audit.View | Enforced | Y | Y | - | - | View audit logs and traceability records. |
| PERM-REPORTING-ADMIN | Reporting.Admin | Enforced | Y | - | - | - | Run administrative reporting operations (e.g. ResultRecord backfill). |
| PERM-SAMPLES-REVIEW | Samples.Review | Enforced | Y | Y | Y | - | Submit a technical review decision on a sample. |
| PERM-SAMPLES-APPROVE | Samples.Approve | Enforced | Y | Y | - | - | Submit a release/approval decision on a sample. |
| PERM-SAMPLES-RECEIVE | Samples.Receive | Enforced | Y | Y | - | - | Receive samples for any laboratory on the main Receiving page and add a laboratory to a sample. |
| PERM-SAMPLES-TRACKALL | Samples.TrackAll | Enforced | Y | Y | - | - | View the sample tracking board across every laboratory. |
| PERM-SIGNATURES-MANAGE | Signatures.Manage | Enforced | Y | Y | - | - | View the electronic signature trail for a record. |
| PERM-TESTWORKFLOW-EXECUTE | TestWorkflow.Execute | Enforced | Y | Y | Y | Y | Execute pathogen session and media evaluation workflow steps. |
| PERM-TESTWORKFLOW-BIOCHEMICALDECISION | TestWorkflow.BiochemicalDecision | Enforced | Y | Y | Y | - | Record a biochemical confirmation decision. |
| PERM-CRYOVIALS-MANAGE | Cryovials.Manage | Enforced | Y | Y | Y | Y | General cryovial operations (prepare, destroy, thaw, view summaries). |
| PERM-CRYOVIALS-APPROVE | Cryovials.Approve | Enforced | Y | Y | - | - | Approve (release) a cryovial batch. |
| PERM-MATERIALS-MANAGE | Materials.Manage | Enforced | Y | Y | - | Y | Manage inventory materials (receive, update). |
| PERM-MATERIALS-DOCUMENTCONTROL | Materials.DocumentControl | Enforced | Y | Y | - | - | Supersede or void a material document. |
| PERM-EQUIPMENT-MANAGE | Equipment.Manage | Enforced | Y | Y | - | Y | Manage inventory equipment. |
| PERM-EQUIPMENT-DOCUMENTCONTROL | Equipment.DocumentControl | Enforced | Y | Y | - | - | Supersede or void an equipment document. |
| PERM-ITEMS-MANAGE | Items.Manage | Enforced | Y | Y | - | - | Manage Items master data (create, update, freeze/unfreeze, delete). |
| PERM-ITEMS-DOCUMENTUPLOAD | Items.DocumentUpload | Enforced | Y | Y | - | - | Upload a controlled document to an Item. |
| PERM-MASTERDATA-MANAGE | MasterData.Manage | Enforced | Y | Y | - | - | Manage laboratory configuration master data (water, EM, after-cleaning, specs, equipment config, media, organisms, test definitions). |
| PERM-DISCUSSIONS-VIEW | Discussions.View | NOT-ENFORCED | Y | Y | Y | Y | View discussions and posts. |
| PERM-DISCUSSIONS-CREATE | Discussions.Create | NOT-ENFORCED | Y | Y | Y | Y | Create discussion posts and add comments. |
| PERM-DISCUSSIONS-EDITANY | Discussions.EditAny | Enforced | Y | Y | - | - | Edit or delete any discussion post or comment. |
| PERM-MESSAGES-USE | Messages.Use | NOT-ENFORCED | Y | Y | Y | Y | Send and receive direct/group messages. |
| PERM-SYSTEM-VIEWERRORLOG | System.ViewErrorLog | Enforced | Y | - | - | - | View the technical error log and monitoring page. |
| PERM-SYSTEM-VIEWSECURITYAUDIT | System.ViewSecurityAudit | NOT-ENFORCED | Y | - | - | - | View the security audit trail (authentication, session and account security events). |
| PERM-DOCUMENTS-REGISTER | Documents.Register | Enforced | Y | Y | - | Y | Register a new controlled Document Master. |
| PERM-DOCUMENTS-DRAFTEDIT | Documents.DraftEdit | NOT-ENFORCED | Y | Y | - | Y | Edit draft metadata and controlled files, and cancel a draft revision. |
| PERM-DOCUMENTS-REVISIONCREATE | Documents.RevisionCreate | NOT-ENFORCED | Y | Y | - | Y | Create a new revision from the current effective revision. |
| PERM-DOCUMENTS-REVIEW | Documents.Review | NOT-ENFORCED | Y | - | Y | - | Perform technical review of a document revision (still requires being the assigned reviewer). |
| PERM-DOCUMENTS-APPROVE | Documents.Approve | NOT-ENFORCED | Y | Y | Y | - | Execute an approval decision on a document revision (still requires being the designated approver). |
| PERM-DOCUMENTS-PERIODICREVIEW | Documents.PeriodicReview | NOT-ENFORCED | Y | Y | Y | - | Perform a periodic review of an effective document. |
| PERM-DOCUMENTS-TRAININGASSIGN | Documents.TrainingAssign | Enforced | Y | Y | - | - | Assign document reading and training curricula to personnel. |
| PERM-DOCUMENTS-TRAININGVIEWMATRIX | Documents.TrainingViewMatrix | NOT-ENFORCED | Y | Y | Y | - | View and export the document training matrix. |
| PERM-DOCUMENTS-CONFIGMANAGE | Documents.ConfigManage | Enforced | Y | - | - | - | Manage Document Control configuration (types, departments, sections, numbering, settings). |
| PERM-DASHBOARDS-LABOVERVIEW | Dashboards.LabOverview | Enforced | Y | Y | - | - | View the laboratory overview dashboard (section workload, approvals, overdue work). |
| PERM-DASHBOARDS-REVIEW | Dashboards.Review | Enforced | Y | Y | Y | - | View the reviewer dashboard. |
| PERM-KPI-VIEW | Kpi.View | Enforced | Y | Y | - | - | View laboratory KPIs. |
| PERM-KPI-VIEWTEAM | Kpi.ViewTeam | Enforced | Y | Y | - | - | See the KPI and workload figures of other analysts |
| PERM-KPI-CONFIGUREPROFILES | Kpi.ConfigureProfiles | Enforced | Y | Y | - | - | Create and sign workflow profile versions (work units and analyst-stage targets) used by the KPI page. |
| PERM-MEDIA-PREPARE | Media.Prepare | Enforced | Y | Y | Y | Y | Prepare media lots and mark them out of stock. |
| PERM-MEDIA-RELEASE | Media.Release | Enforced | Y | Y | - | - | Release a prepared media lot for use. |
| PERM-OOS-MANAGE | Oos.Manage | Enforced | Y | Y | - | - | Track out-of-specification groups and manage their investigation documents. |
| PERM-TESTWORKFLOW-SUPERVISE | TestWorkflow.Supervise | Enforced | Y | Y | - | - | Supervisor interventions in a running test: override an incubation's minimum duration, reset a pathogen session. |
| PERM-SAMPLES-RECEIVEOWNLAB | Samples.ReceiveOwnLab | Enforced | Y | Y | Y | Y | Receive samples for the laboratories the user belongs to. |
| PERM-SAMPLES-CORRECT | Samples.Correct | Enforced | Y | Y | Y | - | Correct or void a sample record. |
| PERM-SAMPLES-ASSIGNANALYST | Samples.AssignAnalyst | Enforced | Y | Y | - | - | Assign an analyst to a sample. |
| PERM-SOLUTIONS-PREPARE | Solutions.Prepare | Enforced | Y | Y | - | Y | Start, edit, complete, cancel or discard a solution preparation (HPLC chain). |
| PERM-HPLC-OPERATE | Hplc.Operate | Enforced | Y | Y | - | Y | Operate the HPLC workspace: start runs, confirm system suitability, assign samples, enter replicates and submit for review. |
| PERM-WORKINGSTANDARDS-QUALIFY | WorkingStandards.Qualify | Enforced | Y | Y | - | Y | Create and prepare working standard qualifications, attach documents, enter and submit qualification replicates. |

[src for grants: backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:L523-L575; verified §2.3. Counts: 50 / 43 / 15 / 15.] Grant defaults are the seeder defaults; migrations add the same sets to existing databases (verified §2.2-§2.3). The older baseline codes (Users.Manage, Samples.Approve, Items.Manage, Equipment.Manage, Discussions.*, Messages.Use, TestWorkflow.Execute) were not traceable to a migration (REQUIRES CONFIRMATION on production).

### 5.3 The 10 NOT-ENFORCED codes

`Discussions.View`, `Discussions.Create`, `Messages.Use`, `System.ViewSecurityAudit`, `Documents.DraftEdit`, `Documents.RevisionCreate`, `Documents.Review`, `Documents.Approve`, `Documents.PeriodicReview`, `Documents.TrainingViewMatrix` [src: verified §1]. Messages and Discussions are plain `[Authorize]` (any signed-in user). The Document Control codes are replaced by fixed role-type rules and assignments (§5.4). The guide must not tell users that these 10 change anything (GAP-011).

Enforcement mechanics (verified §1): most enforced codes are `[Authorize(Policy = code)]` on controllers; `Samples.Receive` / `Samples.ReceiveOwnLab`, `Kpi.ViewTeam` and `Discussions.EditAny` are checked as claims in code; `Documents.Register` is checked against the role's database grants in `DocumentAuthorizationService`.

### 5.4 Document Control: fixed role-type rules (not permission codes)

Document Control controllers carry only `[Authorize]`; authority is decided in services from the user's **role type** plus per-document assignments [src: backend/MicroLIMS.Application/Services/DocumentControl/DocumentAuthorizationService.cs:L84-L260; knowledge/_raw/backend-permissions-roles-auth.md §6]:

| Action | Rule |
|---|---|
| Register a document master | permission `Documents.Register` (default: System Administrator, Section Head, Analyst) |
| View a voided document | System Administrator or Section Head |
| Edit draft metadata, upload / replace draft file, cancel a draft revision | System Administrator or Section Head, or the document owner / assigned author |
| Void a document master | **Section Head only** (not System Administrator) [src: DocumentAuthorizationService.cs:L147-L153] |
| Open a source (editable) file | System Administrator, Section Head, owner / author, active technical reviewer, or active approver of that revision |
| Open a controlled PDF | System Administrator, Section Head or Reviewer role type (plus other conditions not fully read) [src: DocumentAuthorizationService.cs:L206-L227] |
| Manage Document Control configuration | route needs `Documents.ConfigManage`; the service check is role type System Administrator [src: DocumentAuthorizationService.cs:L229-L233] |
| Global Document Control audit trail | System Administrator, Section Head or Reviewer [src: DocumentAuthorizationService.cs:L235-L242] |
| Technical review / approval | assigned reviewer or designated approver; author and technical reviewers cannot be assigned or act as approver; author cannot review (messages in verified §5) |
| Approval signature | password re-authentication required [src: DocumentApprovalService.cs:L530, L557-L560] |

Other role-type checks still in force (permission edits do not change them): media product code change = role type Section Head [src: MediaMasterDataController.cs:L52-L56]; Analyst dashboard endpoints; Analyst-type users see only their own assigned tests in the workspace unless scope is "all"; analyst pickers list Analyst-type users only; last active System Administrator cannot be deleted (verified §3).

### 5.5 Laboratory scope

Two lab section codes `MICRO` and `FP`. A user's laboratory is their organisation memberships (department, optional section, optional physchem area `fp` / `rmpm`), set by an administrator with `Users.Manage`. No membership means no laboratory data. System Administrator role type is unrestricted. Receiving: `Samples.Receive` allows any lab; `Samples.ReceiveOwnLab` own labs only ("You can receive samples only for your own laboratory.") [src: verified §7; ReceiptLabGuard.cs:L25-L30].

### 5.6 Segregation of duties and signatures (summary)

- Sample chain: whoever performed or prepared a test cannot technically review it ("You cannot review a sample you tested."); the approver cannot be the reviewer ("You cannot approve a sample you reviewed.") nor have tested ("You cannot approve a sample you tested."). The guard compares user ids only, so no role bypasses it [src: SegregationOfDutiesGuard.cs:L6-L54; SampleReviewService.cs:L109-L112; SampleApprovalService.cs:L162-L170].
- Other SoD rules exist for pathogen biochemical decisions, media lot release, cryovial approval, working standard qualification, workbook verification, document revisions, periodic review (verified §5).
- 26 signed actions (verified §6). Signature password failures: 5 in 15 minutes are throttled; they never lock the login account [src: ElectronicSignatureService.cs:L12-L57].
- Not found signed: microbiology plate-count entry, pathogen steps, biochemical decision, return to analyst, incubator changes, user and role administration (verified §6; GAP-009, GAP-010, GAP-028).

---

## 6. Major entities (user-relevant)

| Entity (user meaning) | Notes | Source |
|---|---|---|
| Sample | Received item with reference number, category, status (Received, InTesting, UnderReview, UnderApproval, Approved, Rejected, RetestRequested, Cancelled, Voided) and overall status | [src: backend/MicroLIMS.Domain/Enums/SampleStatus.cs; OverallSampleStatus.cs] |
| Test order | One test on one sample; created automatically on receipt; status values Pending, InProgress, ResultEntered, Reviewed, Approved, Rejected, RetestRequested, Voided, Cancelled | [src: ApprovalStatus.cs; BusinessRules.md:L42-L48] |
| Sample section sign-off | Per laboratory section state of a sample (InTesting … Voided) | [src: SectionSignoffStatus.cs] |
| Item / Specification / Test definition | Master data driving auto-assigned tests, limits, workflow steps | [src: Item.cs, Specification.cs, TestDefinition.cs] |
| Media lot, Media product, Media evaluation | Prepared media, product master, GPT and other evaluations | [src: Media.cs, MediaProduct.cs, MediaEvaluation.cs] |
| Cryovial | Reference-strain working batch | [src: Cryovial.cs] |
| Material lot / Material master entry | Stock lots and their master definitions | [src: Material.cs, MaterialMasterEntry.cs] |
| Equipment (configuration) / Equipment inventory | Configured equipment and inventory asset | [src: Equipment.cs, EquipmentInventory.cs] |
| HPLC run, ICP run | Chromatography / ICP runs with samples, SST or calibration | [src: HplcRun.cs, IcpRun.cs] |
| Solution preparation, Titrant standardization | Prepared solutions and titrant factors | [src: SolutionPreparation.cs, TitrantStandardization.cs] |
| Working standard qualification | Draft, Assayed, Reviewed, Approved, Rejected | [src: WorkingStandardQualificationStatus.cs] |
| Electronic signature | Append-only record of each signed action | [src: ElectronicSignature.cs] |
| Audit log / security audit event / login history | Change capture, security events, login attempts | [src: AuditLog.cs, SecurityAuditEvent.cs, LoginHistory.cs] |
| Document master / revision / review task / approval task / training assignment | Document Control records; revision statuses Draft, Cancelled, InReview, AwaitingApproval, FutureEffective, Effective, Superseded, Obsolete | [src: DocumentRevisionStatus.cs] |
| Incident / error log | Technical error monitoring | [src: Incident.cs, ErrorLog.cs] |
| User, Role, Permission, Organisation membership | Accounts and access | [src: User.cs, Role.cs, UserOrgMembership.cs] |
| OOS investigation document | Files attached to an OOS group | [src: OosInvestigationDocument.cs] |
| Workflow profile version | Signed KPI profile versions | [src: WorkflowProfileVersion.cs] |
| Notification log | Persisted notifications | [src: NotificationLog.cs] |

Status vocabularies in full are in `_raw/backend-enums.md` and will be carried into `knowledge/statuses.md` in Phase 3.

---

## 7. Major workflows (names and IDs only; detail is Phase 4)

| Workflow ID | Name | Module | Signed? (per verified §6 / raw) |
|---|---|---|---|
| WF-AUTH-001 | Sign in | MOD-AUTH-001 | no |
| WF-AUTH-002 | Request password reset | MOD-AUTH-001 | no |
| WF-AUTH-003 | Admin-assisted password recovery | MOD-AUTH-001 | no |
| WF-AUTH-004 | Change password (including forced change) | MOD-AUTH-001 | no |
| WF-AUTH-005 | Session timeout and sign-out | MOD-AUTH-001 | no |
| WF-SAMPLE-001 | Receive sample(s) (all six categories) | MOD-SAMPLE-001 | no |
| WF-SAMPLE-002 | Add laboratory to a sample | MOD-SAMPLE-001 | yes |
| WF-SAMPLE-003 | Assign responsible analyst | MOD-SAMPLE-001 | no (reason recorded in audit trail) |
| WF-SAMPLE-004 | Correct sample details | MOD-SAMPLE-001 | yes |
| WF-SAMPLE-005 | Void sample | MOD-SAMPLE-001 | yes |
| WF-PREP-001 | Confirm sample preparation (single and grouped) | MOD-PREP-001 | yes (product / RM / PM); EM, AC, Water forms: no signature found |
| WF-PREP-002 | Item preparation configuration and approval | MOD-PREP-001 | REQUIRES CONFIRMATION |
| WF-TESTING-001 | Start incubation for several samples (grouped actions) | MOD-TESTING-001 | no |
| WF-TESTING-002 | Skip minimum incubation wait (supervisor) | MOD-TESTING-001 | no |
| WF-COUNT-001 | Count test: media, incubation, plate counts | MOD-COUNT-001 | no |
| WF-COUNT-002 | Batch location results (EM, After Cleaning, Water) | MOD-OBS-001 | no |
| WF-OBS-001 | Qualitative observation result | MOD-OBS-001 | yes |
| WF-PHYSRESULT-001 | Record measurement, gravimetric or qualitative result | MOD-PHYSRESULT-001 | yes |
| WF-PHYSRESULT-002 | Record titration result | MOD-TITR-001 | yes |
| WF-PHYSRESULT-003 | Record dissolution, disintegration or weight variation result (staged) | MOD-PHYSRESULT-001 | yes |
| WF-PATH-001 | Pathogen test step chain (single test) | MOD-PATH-001 | no |
| WF-PATH-002 | Pathogen testing session (shared TSB, matrix, confirmatory plating, complete) | MOD-PATH-001 | no |
| WF-PATH-003 | Reset pathogen session (supervisor) | MOD-PATH-001 | no (reason only) |
| WF-REVIEW-001 | Technical review of a sample section | MOD-REVIEW-001 | yes |
| WF-REVIEW-002 | Return count test to analyst | MOD-REVIEW-001 | no |
| WF-APPROVAL-001 | Approval decision (Approve, Not Conform, New Sample Required, Retest Retained Sample) | MOD-APPROVAL-001 | yes |
| WF-APPROVAL-002 | Retest or new-sample spin-off | MOD-APPROVAL-001 | yes (part of decision) |
| WF-APPROVAL-003 | Close testing after another lab rejected | MOD-APPROVAL-001 | yes |
| WF-COA-001 | Print sample report / view Certificate of Analysis | MOD-COA-001 | no |
| WF-OOS-001 | OOS investigation documents (upload, supersede, void) | MOD-OOS-001 | no |
| WF-MEDIA-001 | Prepare media lot | MOD-MEDIA-001 | REQUIRES CONFIRMATION |
| WF-MEDIA-002 | Evaluate media lot (GPT and other evaluations) | MOD-MEDIA-001 | no |
| WF-MEDIA-003 | Release or reject media lot | MOD-MEDIA-001 | yes |
| WF-MEDIA-004 | Media storage move / mark out of stock | MOD-MEDIA-001 | no |
| WF-MEDIACFG-001 | Configure media products, conditions and configurations | MOD-MEDIACFG-001 | no |
| WF-MEDIACFG-002 | Change media product code | MOD-MEDIACFG-001 | yes |
| WF-CRYO-001 | Prepare cryovial batch | MOD-CRYO-001 | no |
| WF-CRYO-002 | Approve or reject cryovial batch | MOD-CRYO-001 | yes |
| WF-CRYO-003 | Thaw, move or destroy cryovials | MOD-CRYO-001 | no |
| WF-MATERIAL-001 | Receive material lot and manage documents | MOD-MATERIAL-001 | no |
| WF-EQUIP-001 | Register equipment, status and certificates | MOD-EQUIP-001 | no |
| WF-EQUIP-002 | Configure equipment set points and autoclave programs | MOD-EQUIP-002 | no |
| WF-ITEM-001 | Configure an item and its tests | MOD-ITEM-001 | no |
| WF-ITEM-002 | Maintain item specifications | MOD-ITEM-001 | no |
| WF-TESTMASTER-001 | Define a test (steps, media, replicates, titration config) | MOD-TESTMASTER-001 | no |
| WF-WORKBOOK-001 | Load master data workbook | MOD-WORKBOOK-001 | yes |
| WF-WORKBOOK-002 | Verify loaded workbook (second person) | MOD-WORKBOOK-001 | yes |
| WF-HPLC-001 | HPLC run (setup, system suitability, samples, replicates, review, complete or abandon) | MOD-HPLC-001 | yes (SST, send for review) |
| WF-GC-001 | GC run (same workflow on GC route) | MOD-GC-001 | yes |
| WF-ICP-001 | ICP-OES run (calibration, CCV, samples, review) | MOD-ICP-001 | yes (calibration, send for review) |
| WF-SOLN-001 | Solution preparation | MOD-SOLN-001 | yes |
| WF-SOLN-002 | Titrant standardization | MOD-SOLN-001 | yes |
| WF-WS-001 | Working standard qualification (create, review, approve / reject) | MOD-WS-001 | yes |
| WF-REPORTS-001 | Search, trend and export results | MOD-REPORTS-001 | no |
| WF-KPI-001 | View KPIs and workload | MOD-KPI-001 | no |
| WF-KPI-002 | Workflow profile version (create, sign) | MOD-KPI-001 | yes |
| WF-AUDIT-001 | Audit search and traceability chain | MOD-AUDIT-001 | no |
| WF-USERS-001 | Manage user accounts | MOD-USERS-001 | no (reason typed for role change / disable) |
| WF-USERS-002 | Assign laboratory membership | MOD-USERS-001 | no |
| WF-ROLES-001 | Create and edit roles and permissions | MOD-ROLES-001 | no (audit entry written) |
| WF-DC-001 | Register a controlled document | MOD-DC-001 | no |
| WF-DC-002 | Revision, technical review and approval | MOD-DC-001 | yes (approval) |
| WF-DC-003 | Periodic review | MOD-DC-001 | REQUIRES CONFIRMATION |
| WF-DC-004 | Reading acknowledgement | MOD-DC-002 | no (checkbox only) |
| WF-DC-005 | Training matrix and compliance review | MOD-DC-002 | no |
| WF-DC-006 | Configure Document Control | MOD-DC-004 | no |
| WF-DC-007 | Void document / cancel draft | MOD-DC-001 | no password found |
| WF-ERR-001 | Review and resolve technical incidents | MOD-ERR-001 | no |
| WF-MSG-001 | Send and read messages | MOD-MSG-001 | no |
| WF-DISC-001 | Post and discuss | MOD-DISC-001 | no |
| WF-NOTIF-001 | Receive and read notifications | MOD-NOTIF-001 | no |

Workflow count is given in the final report. Whether a step is signed comes from verified §6 and the raw signature tables; unverified rows say REQUIRES CONFIRMATION.

---

## 8. External integrations (only what the code implements)

| Integration | Status | Evidence |
|---|---|---|
| **SMTP e-mail** | IMPLEMENTED when `Smtp:Host` is set; empty host means e-mail is silently disabled. Used for password-reset links, error-severity dashboard notifications, and Critical incident alerts. Critical incident alerts are off unless `ErrorMonitoring:CriticalAlerts:Enabled` is true (default false). Whether production is configured: REQUIRES CONFIRMATION (GAP-047). | [src: backend/MicroLIMS.Infrastructure/Email/SmtpOptions.cs:L9-L20; backend/MicroLIMS.API/BackgroundServices/CriticalAlertWorker.cs:L29-L33; backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:L16-L20] |
| **File storage (S3-compatible bucket)** | IMPLEMENTED: documents and evidence files stored through an S3-compatible storage service (comment names Backblaze B2 for production) | [src: backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs:L256-L286] |
| **Instrument report reading (OCR and parsers)** | PARTIAL, NOT-EXPOSED-IN-UI: parsers and a Tesseract OCR text reader exist in code; no controller or screen calls them | [src: backend/MicroLIMS.Application/Services/InstrumentImport/; backend/MicroLIMS.Infrastructure/InstrumentImport/TesseractOcr.cs; ServiceCollectionExtensions.cs:L299] |
| **Instrument evidence upload** | IMPLEMENTED (manual): HPLC / GC / ICP workspaces accept uploaded report files as "Evidence"; results are typed in by hand | [raw: frontend-config §4.2, §5.6] |
| **Instrument data feed (pH meter, HPLC/CDS direct integration)** | NOT FOUND | searched backend for listener / watcher / import controllers; none (`git grep -i FileSystemWatcher` no match) |
| **SAP / ERP** | NOT FOUND | whole-word search for SAP and for ERP over backend and frontend source returned no match |
| **Local AI assistant** | NOT FOUND | case-insensitive whole-word search for openai, anthropic, chatbot, ollama, llm and copilot over backend and frontend source returned no match |
| **PDF / Word export** | IMPLEMENTED: sample summary export as PDF; the Word export service exists but no screen calls it | [src: frontend/src/modules/testingWorkspace/services/SampleSummaryService.ts:L28-L31] |
| **Background workers** | IMPLEMENTED: document effective-date activation, database health monitor, error-log retention, critical alerts, solution-preparation expiry | [src: backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs:L153-L157] |

---

## 9. Reports

- **Reports page** (`/reports`): tabs Overview, Record Search, Media / GPT, Reference Strains, Trending & Analysis, and KPI / Performance (only with `Kpi.View`) [src: frontend/src/pages/Reports.tsx:L16-L41].
- Exports: Record Search "Export CSV" and "Export PDF"; Media / GPT "Export CSV (Flattened)"; Reference Strains "Export CSV"; Trending data CSV; KPI "Export PDF". The record CSV export has a 10,000-row cap message [src: frontend/src/modules/reports/services/ReportingService.ts:L73-L95; ReportResultsTable.tsx:L320-L329].
- Sample-level documents: printable sample summary report, Certificate of Analysis, media lot report, cryovial report, HPLC / ICP report sections.
- Workspace register CSV ("receiving-testing-workspace-{date}.csv") and Error Monitoring CSV [src: ReceivingTestingWorkspacePage.tsx:L81-L103; ErrorMonitoringPage.tsx:L51].
- **Mock / placeholder data in reports**: Trending tab hardcoded fallback options and default criteria; static "Live Database Data" chip (GAP-002, GAP-003). Figures from these controls must not be presented as reliable.
- KPI definitions, SLA and overdue logic: the dashboards use ">24h" overdue wording; KPI definitions are served by the backend (`/kpi/definitions`); exact SLA logic is Phase 3 work (REQUIRES CONFIRMATION).

---

## 10. Configuration areas

- **Microbiology Configuration**: Test Master, Organisms, Media Configurations, Water, Environmental Monitoring, After Cleaning, Equipment.
- **Physicochemical Configuration**: FP Test Master, RM & PM Test Master, Equation Types, Physicochemical Instruments, Chromatography Columns, Reagents & Standards, Solutions, HPLC Methods (also GC), ICP Methods.
- **General Laboratory Configuration**: Items (with preparation configuration and specifications), Receiving Configuration, Master Data Workbook.
- **Document Control Configuration**: types, departments & sections, numbering, module settings.
- **Administration**: Users (including laboratory memberships), Roles.
- **Per-section KPI settings and analyst availability**: backend only (MOD-KPI-002).
- Existing user documentation: `docs/validation/ML-SOP-MDW-001_Master_Data_Workbook_User_Guide.md` (Draft v0.1) covers the workbook only [src: knowledge/_raw/spec-sources.md §11].

---

## 11. Audit and traceability capabilities

| Capability | State | Evidence |
|---|---|---|
| Automatic audit capture of data changes | IMPLEMENTED: `SaveChanges` writes an audit entry (entity, id, action, previous and new values as JSON, user, UTC time) for adds, updates and deletes | [src: backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:L270-L337]; documented intent [src: documents/DesignSpecification/BusinessRules.md:L206-L218] |
| Workflow history | IMPLEMENTED (entity `WorkflowHistory`; sample "Workflow History" strip) | [src: WorkflowHistory.cs; SampleSummaryDialog.tsx:L39-L51] |
| Audit Trail search UI | IMPLEMENTED (`Audit.View`), capped at 300 latest events | [raw: frontend-core §7] |
| Traceability chain | IMPLEMENTED inside the audit event drawer (no stand-alone Traceability page) | [src: AuditEventDrawer.tsx:L36; AuditSearchService.ts:L15-L18] |
| Electronic signatures | IMPLEMENTED for 26 actions; append-only record with name, username and role snapshots, meaning, IP address | [src: verified §6] |
| Signature trail viewer | Backend endpoint only (NOT-EXPOSED-IN-UI) | MOD-SIG-002 |
| Security audit events (login, lockout, session, role change) | Recorded; no viewer; permission NOT-ENFORCED | MOD-SECAUDIT-001 |
| Login history | Recorded; endpoint without UI | MOD-AUDIT-002 |
| Archived (frozen) records | IMPLEMENTED: archived copies listed on the sample report | MOD-ARCHIVE-001 |
| Document Control audit trail | IMPLEMENTED (append-only wording in UI) | MOD-DC-003 |
| Role permission changes | IMPLEMENTED: `ROLE_PERMISSIONS_UPDATED` audit entry with granted / revoked lists | [src: RoleService.cs:L126-L171] |
| Device clock | Several start times default to the browser clock (confirmatory plating, TSB start) | [raw: frontend-testing §10] |

No statement here is a claim of regulatory compliance. See GAP-023 for the sign-in page footer that makes such a claim in the UI.

---

## 12. Legacy, retired and redirect routes

| From | To / state | Mechanism | Source |
|---|---|---|---|
| `/` | `/dashboard` | redirect | AppRoutes.tsx:L108 |
| `/receiving-testing` | first lab workspace (Microbiology first, then FP areas) | redirect component | AppRoutes.tsx:L133; LabWorkspaceRoute.tsx:L128-L141 |
| `/testing-workspace` | `/receiving-testing` (query preserved) | legacy redirect | AppRoutes.tsx:L151 |
| `/laboratory-configuration/cause-of-testing` | `/laboratory-configuration/receiving-configuration` | legacy redirect | AppRoutes.tsx:L235 |
| `/inventory/approved-media` | `/laboratory-configuration/media?tab=approved` | redirect | AppRoutes.tsx:L261 |
| `/inventory/approved-cryovials` | `/laboratory-configuration/cryovials?tab=approved` | redirect | AppRoutes.tsx:L262 |
| `/groups/:slug` | same page as `/sections/:slug` | alias | AppRoutes.tsx:L117 |
| Retired: legacy System Suitability (HPLC) module | retired per design spec | spec | docs/superpowers/specs/2026-10-02-retire-legacy-sst-design.md:L1 |
| Retired: old ICP-OES / AAS calibration-run path | retired per plan; signature meanings CalibrationRunPerformed and CalibrationRunWithdrawn marked retired | spec, enum | docs/superpowers/plans/2026-10-05-r-retire-calibration-runs.md:L1; verified §6 |
| Retired assay equation values (`HplcAssayResult`, `HplcAssay`, `HplcMultiAnalyte`) | retired per build spec; enum values still present | spec, enum | docs/FP_Standard_Comparison_Assay_Build_Spec.md:L11, L19; backend-enums.md |
| Unused enum `GptStage` (Preparation, Sterility, Recovery, Release, Rejected) | declared; no use found outside the declaration; replaced by `MediaEvaluationEngine` | code | backend/MicroLIMS.Domain/Enums/GptStage.cs; MediaEvaluationEngine.cs:L23 |
| Deprecated endpoint `submit-selective-plating` | marked deprecated in the frontend service | code | TestWorkflowService.ts:L239 |
| Endpoints with no UI caller: biochemical reviewer decision, Word export of sample summary | present, unused | code | TestWorkflowService.ts:L268-L270; SampleSummaryService.ts:L30 |
| Roles "Legacy-only" permissions | NOT-ENFORCED codes | UI chip | PermissionMatrix.tsx:L70-L83 |
| Routed but not linked | Specifications, Media Evaluation | routes | GAP-034 |

---

## 13. Step B cross-check: candidate module checklist

Each candidate from the prompt's Step B list is marked **EXISTS (status)**, **PARTIAL**, **NOT FOUND** or **REQUIRES CONFIRMATION**. "Searched" notes what was looked for when nothing was found.

### 13.1 Core laboratory workflow

| # | Candidate | Result | Evidence / what was searched |
|---|---|---|---|
| 1 | Authentication, login, session handling | EXISTS (COVERED) | MOD-AUTH-001 |
| 2 | Dashboard(s) | EXISTS (PARTIAL) | MOD-DASH-001: four dashboards |
| 3 | Search and filters | PARTIAL | No global search field. Per-screen filters: sample register, Tracking Board, Reports Record Search, Audit Search, Error Monitoring, Document Library. Searched routes and header for a search box: none |
| 4 | Sample receiving and sample management (identification, classification, status) | EXISTS (PARTIAL) | MOD-SAMPLE-001, MOD-TRACK-001 |
| 5 | Test order generation and assignment to tests and analysts | EXISTS | Automatic on receipt [src: BusinessRules.md:L33-L48 (INTENDED); ReceivingTestingWorkspacePage.tsx:L629-L634 text "Test orders generated automatically." (IMPLEMENTED)]; analyst assignment via "Assign Responsible Analyst" |
| 6 | Testing workspace | EXISTS (PARTIAL) | MOD-TESTING-001 |
| 7 | Result entry for count tests (replicates, dilution factor, TNTC / Uncountable, calculated result) | EXISTS (COVERED) | MOD-COUNT-001 |
| 8 | Result entry for observation tests | EXISTS (COVERED) | MOD-OBS-001 (qualitative, growth observation in pathogen steps, Detected / Absent grids) |
| 9 | Review workflow (Reviewer) | EXISTS (COVERED) | MOD-REVIEW-001 |
| 10 | Return to Analyst / rework / retest | EXISTS | Return to Analyst for count tests only (no signature); retest via approval decisions "Retest Retained Sample" and "New Sample Required" |
| 11 | Approval workflow (Section Head) | EXISTS (COVERED) | MOD-APPROVAL-001 |
| 12 | Final Release (global permission) and final report / CoA | **Final Release as a separate step or permission: NOT FOUND.** CoA: EXISTS (PARTIAL) | Searched all 50 permission codes, menu and routes for "Final Release": no match. The Section Head "Submit Release Decision" (Samples.Approve) is the release decision. CoA available after approval or rejection. See GAP-014 |
| 13 | Resample / retest / investigation-related functions incl. OOS investigation | EXISTS (PARTIAL) | Retest and new-sample decisions; OOS Tracking with investigation document upload. The decisions `Investigation` and `OOSInvestigation` named in BusinessRules.md are not reachable from sample approval (GAP-017) |

### 13.2 Microbiology

| # | Candidate | Result | Evidence / what was searched |
|---|---|---|---|
| 14 | Pathogen testing (session, shared enrichment, time-gated selective plating, multi-media confirmation, multi-location matrix, review, completion) | EXISTS (COVERED) | MOD-PATH-001 |
| 15 | Growth Promotion Testing and media evaluation | EXISTS (PARTIAL) | MOD-MEDIA-001; evaluation types GrowthPromotion, IndicationInhibition, EnrichmentCharacteristics [src: EvaluationType.cs] |
| 16 | Media Configuration | EXISTS (PARTIAL) | MOD-MEDIACFG-001 |
| 17 | Media Preparation, batches, activation, status | EXISTS (PARTIAL) | MOD-MEDIA-001; media status values Prepared, Active, Expired, QuarantineFailed, Destroyed, OutOfStock [src: MediaStatus.cs] |
| 18 | Sterility testing | **NOT FOUND as a distinct test or result.** UI text mentions sterility | The evaluation type enum has no Sterility value; `GptStage.Sterility` is unused; no sterility result entity or field found (`git grep -i sterility` shows UI text and one pathogen-session status field only). DISCREPANCY GAP-022 |
| 19 | Environmental Monitoring (annual plan, schedules, locations, sampling, results, status synchronization) | PARTIAL | Locations (departments, rooms, grades), limits, receiving, preparation and per-location results exist (MOD-EM-001). **Annual plan and schedules: NOT FOUND** (searched entities for plan / schedule; only document review and escalation schedules exist). GAP-036 |
| 20 | Water Monitoring (sampling points, frequencies, plan, results) | PARTIAL | Sampling points, test configuration and limits, results exist (MOD-WATER-001). **Frequencies and plan: NOT FOUND**. GAP-036 |
| 21 | After Cleaning monitoring (departments, locations, sampling, results) | EXISTS (PARTIAL) | MOD-AC-001 (machines and parts rather than departments) |
| 22 | Reference strains (receipt, lot, passage, cryopreservation, expiry, usage, traceability) | PARTIAL | Lyophilized microorganism material lots (Materials Stock), cryovial batches with storage location, expiry date, thaw, destroy, approved list, cryovial report, Reference Strains report tab (MOD-CRYO-001). **Passage tracking: NOT FOUND** (searched "passage": only a comment). GAP-037 |

### 13.3 Physicochemical / Finished Product / Raw Material

| # | Candidate | Result | Evidence / what was searched |
|---|---|---|---|
| 23 | Physicochemical lab workspace | EXISTS (PARTIAL) | FP Workspace, RM & PM Workspace |
| 24 | HPLC (method master, system suitability run, standard and sample entry, calculated assay) | EXISTS (PARTIAL) | MOD-HPLCM-001, MOD-HPLC-001 |
| 25 | ICP-OES / Calibration Curve tests | EXISTS (PARTIAL) | MOD-ICPM-001, MOD-ICP-001. The older "Calibration Curve" path is retired (MOD-LEGACY-001) |
| 26 | Titration tests and Titration Methods / titrant standardization | PARTIAL | Titration configuration lives inside Test Master; titrant standardization in Solution Preparation. A separate "Titration Methods" master page: NOT FOUND (menu and routes searched) |
| 27 | Equation Types and Test creation | EXISTS (PARTIAL) | MOD-EQTYPE-001, MOD-TESTMASTER-001 |
| 28 | Solution Preparation (mobile phases, diluents, titrants, working standards), reagent / indicator / standard master | EXISTS (PARTIAL) | MOD-SOLN-001, MOD-SOLNM-001, MOD-REAGENT-001, MOD-WS-001 |
| 29 | Material Stock | EXISTS (PARTIAL) | MOD-MATERIAL-001 |

### 13.4 Masters and configuration

| # | Candidate | Result | Evidence / what was searched |
|---|---|---|---|
| 30 | Item / Product master incl. Preparation configuration | EXISTS (PARTIAL) | MOD-ITEM-001 |
| 31 | Test Master | EXISTS (PARTIAL) | MOD-TESTMASTER-001 |
| 32 | Specifications (limit types, alert / action / specification limits, dilution factor) | EXISTS (PARTIAL) | Inside Items (specification parameter dialog); stand-alone page not linked (MOD-SPEC-001). Limit types: Range, NotMoreThan, NotLessThan, TargetWithTolerance, CountTiered, Qualitative, PresenceAbsence, MultiStage, DissolutionQ, DisintegrationTime, WeightVariation [src: LimitType.cs] |
| 33 | Laboratory Configuration pages | EXISTS | §3.3 |
| 34 | Equipment master, Equipment Inventory, calibration and qualification, Column master | PARTIAL | Master, inventory, "Calibration Due" and calibration certificate documents, columns exist. **Equipment qualification (IQ / OQ / PQ) records: NOT FOUND** (searched menu, routes, enums). GAP-038 |
| 35 | Departments / sections and department-scoped data segregation | EXISTS | User laboratory memberships (department, section, area) and scope service; Document Control departments and sections |
| 36 | Locations, sampling points, monitoring plans | PARTIAL | Rooms, machine parts, water sampling points exist; monitoring plans NOT FOUND (see #19, #20) |

### 13.5 Quality system and administration

| # | Candidate | Result | Evidence / what was searched |
|---|---|---|---|
| 37 | Users | EXISTS | MOD-USERS-001 |
| 38 | Roles and permissions (matrix, role wizard) | EXISTS (COVERED) | MOD-ROLES-001 |
| 39 | Audit trail | EXISTS | MOD-AUDIT-001 |
| 40 | Traceability | PARTIAL | A traceability chain inside the audit event drawer; no separate Traceability page or menu item |
| 41 | Electronic signature / re-authentication, segregation of duties | EXISTS | MOD-SIG-001 |
| 42 | Document Control / EDMS | EXISTS (PARTIAL) | MOD-DC-001 to MOD-DC-004. Per-file integrity (hash) checking: REQUIRES CONFIRMATION (not searched in Phase 1) |
| 43 | Incidents / Error Log and monitoring | EXISTS as **Error Monitoring** (technical incidents, "not part of the GxP record"). **A laboratory incident, deviation or CAPA module: NOT FOUND** | [src: ErrorMonitoringPage.tsx:L60-L61]; searched menu and entities for deviation / CAPA: none |
| 44 | Notifications / email alerts | EXISTS (PARTIAL) | MOD-NOTIF-001; e-mail depends on SMTP configuration (GAP-047) |

### 13.6 Reporting and integrations

| # | Candidate | Result | Evidence / what was searched |
|---|---|---|---|
| 45 | Reports module: KPIs / Performance, Trend Analysis, export (CSV, PDF), SLA / overdue logic | EXISTS (PARTIAL) | MOD-REPORTS-001, MOD-KPI-001; contains hardcoded fallbacks |
| 46 | Printed or exported records (pinned light theme, print views) | EXISTS (PARTIAL) | Print views with `@media print` styles for sample report, CoA, reports; "pinned light theme" for print: REQUIRES CONFIRMATION (not verified) |
| 47 | Instrument integration (pH meter, HPLC, CDS) | PARTIAL / NOT-EXPOSED-IN-UI | Manual entry plus evidence upload; report parsers and OCR exist without a caller (MOD-INSTIMPORT-001); no live instrument feed |
| 48 | SAP or other ERP integration | **NOT FOUND** | see §8 |
| 49 | Local AI assistant | **NOT FOUND** | see §8 |

### 13.7 Specific items the brief asked to check

| Question | Answer |
|---|---|
| Final Release (global permission)? | NOT FOUND (see #12). |
| Sterility testing? | NOT FOUND as a distinct test (see #18). |
| Notifications / email alerts? | EXISTS; email conditional on SMTP. |
| SAP / ERP? | NOT FOUND. |
| Local AI assistant? | NOT FOUND. |
| Incidents vs Error Monitoring? | Only technical Error Monitoring exists; no GMP incident module. |
| Traceability page? | No separate page; chain inside Audit Trail drawer. |
| OOS investigation? | OOS Tracking page exists (groups, retests, document upload). No investigation workflow engine beyond documents. |
| Messages / Discussions? | EXISTS (both), reachable from the account menu. |
| Archived records? | EXISTS (PARTIAL): archived copies list on the sample report. |
| Sample corrections / void? | EXISTS, both signed, need `Samples.Correct`. |
| Working standards? | EXISTS (MOD-WS-001). |
| GC workspace? | EXISTS (MOD-GC-001), reuses HPLC components. |

---

## 14. Counts (for the final report)

Counted from the tables above (re-checkable by counting rows):

- Modules: 66 (COVERED 18, PARTIAL 38, REQUIRES CONFIRMATION 1, NOT-EXPOSED-IN-UI 8, LEGACY/RETIRED 1).
- Screens: 104 screen IDs (SCR-GC-001 stands for the whole GC route family that reuses HPLC components).
- Workflows: 71 workflow IDs (names only; detail in Phase 4).
- Roles: 4 default system roles (custom roles are deployment data).
- Permission codes: 50 (40 enforced, 10 NOT-ENFORCED).

## 15. Open items

All open questions are in `DOCUMENTATION_GAPS.md` (single gaps file).
