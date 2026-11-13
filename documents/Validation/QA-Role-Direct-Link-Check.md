# Manual Check: Quality Assurance Role, Direct Links to Laboratory Actions

**Status:** Draft - awaiting execution

**Prepared by:** ____________________  **Date:** ____________
**Executed by (tester):** ____________________  **Date:** ____________
**Reviewed / Approved by:** ____________________  **Date:** ____________

## Purpose

The Quality Assurance (QA) role reads every laboratory but must not perform any laboratory action (rule: `SegregationOfDutiesRules.QaForbiddenCodes`, `Shared/Constants/SegregationOfDutiesRules.cs`; Business Rules section 10.1). Hiding a menu item proves nothing, so this check signs in as QA and types each address into the browser. A refusal is the expected result for the first list. The second list proves that QA can open what it is meant to use.

The route guards below are read from `frontend/src/routes/AppRoutes.tsx` (`PermissionRoute`). The browser guard only decides what to show; the server enforces every permission, so section C also lists server refusals.

## QA Permissions Under Test

The system role `QA` holds exactly the codes in `PermissionConstants.QaGrants` (`Shared/Constants/PermissionConstants.cs`):

`Discussions.View`, `MonitoringPlan.View`, `Audit.View`, `Audit.Review`, `Access.Review`, `Access.Review.Sign`, `Sections.ViewAll`, `Samples.TrackAll`, `Dashboards.LabOverview`, `Dashboards.Review`, `Kpi.View`, `Kpi.ViewTeam`, `Messages.Use`, `System.ViewSecurityAudit`, `Documents.Review`, `Documents.Approve`, `Documents.PeriodicReview`, `Documents.TrainingViewMatrix`.

QA holds no section membership. `Sections.ViewAll` gives read access to both laboratories, so laboratory workspaces open for reading.

## Preconditions

- [ ] A test environment (not production) with the QA role migration applied.
- [ ] A QA test user: active, role `QA`, signing password set, no other role. Username: ____________________
- [ ] A second user holding the System Administrator role, to confirm the same links work for someone who is allowed (optional control).
- [ ] At least one sample, one solution preparation, one HPLC instrument with a run, one audit trail review (in progress or signed), one controlled document, and one access review exist. Write the ids here and use them in place of the `{...}` placeholders below.
  - sample id: ________  solution preparation id: ________  instrument id: ________  run id: ________
  - audit trail review id: ________  controlled document id: ________  role id: ________
- [ ] Sign in as the QA user in a fresh browser session. The sidebar must not show the laboratory action pages below; this check does not rely on the sidebar.

## Section A. Links QA Must Not Be Able To Use

How to run each row: paste the address (host and port of the test system, then the path) into the address bar of the signed-in QA session and press Enter.

**Expected result for every row in A1:** the address is replaced by `/dashboard` and the QA dashboard is shown (`PermissionRoute` redirects with `<Navigate to="/dashboard" replace />`). No page content from the target appears, not even briefly with data.

### A1. Pages guarded by a permission

| # | Path | Guard (QA does not hold it) | Expected | Result (Pass / Fail) | Notes |
|---|---|---|---|---|---|
| 1 | `/receiving` | `Samples.Receive` or `Samples.ReceiveOwnLab` | Redirect to `/dashboard` | | |
| 2 | `/laboratory-configuration/media` | `Media.Prepare` | Redirect to `/dashboard` | | |
| 3 | `/inventory/approved-media` | `Media.Prepare` (redirects to media page, then guarded) | Ends on `/dashboard` | | |
| 4 | `/laboratory-configuration/media-evaluation` | `TestWorkflow.Execute` | Redirect to `/dashboard` | | |
| 5 | `/laboratory-configuration/cryovials` | `Cryovials.Manage` | Redirect to `/dashboard` | | |
| 6 | `/inventory/approved-cryovials` | `Cryovials.Manage` (redirects, then guarded) | Ends on `/dashboard` | | |
| 7 | `/laboratory-configuration/biological-waste` | `Cryovials.Manage` | Redirect to `/dashboard` | | |
| 8 | `/preparation` | `Solutions.Prepare` | Redirect to `/dashboard` | | |
| 9 | `/preparation/new` | `Solutions.Prepare` | Redirect to `/dashboard` | | |
| 10 | `/preparation/{solution preparation id}/edit` | `Solutions.Prepare` | Redirect to `/dashboard` | | |
| 11 | `/preparation/{solution preparation id}` | `Solutions.Prepare` | Redirect to `/dashboard` | | |
| 12 | `/oos-tracking` | `Oos.Manage` | Redirect to `/dashboard` | | |
| 13 | `/laboratory-configuration/items` | `Items.Manage` or `tests.not-applicable.final-approve` | Redirect to `/dashboard` | | |
| 14 | `/laboratory-configuration/test-master` | `MasterData.Manage` | Redirect to `/dashboard` | | |
| 15 | `/laboratory-configuration/fp-test-master` | `MasterData.Manage` | Redirect to `/dashboard` | | |
| 16 | `/laboratory-configuration/rm-pm-test-master` | `MasterData.Manage` | Redirect to `/dashboard` | | |
| 17 | `/laboratory-configuration/equation-types` | `MasterData.Manage` | Redirect to `/dashboard` | | |
| 18 | `/laboratory-configuration/organisms` | `MasterData.Manage` | Redirect to `/dashboard` | | |
| 19 | `/laboratory-configuration/master-data-workbook` | `MasterData.Manage` | Redirect to `/dashboard` | | |
| 20 | `/laboratory-configuration/specifications` | `MasterData.Manage` | Redirect to `/dashboard` | | |
| 21 | `/laboratory-configuration/master-testing-materials` | `MasterData.Manage` | Redirect to `/dashboard` | | |
| 22 | `/laboratory-configuration/media-configurations` | `MasterData.Manage` (redirects, then guarded) | Ends on `/dashboard` | | |
| 23 | `/laboratory-configuration/material-master` | `MasterData.Manage` (redirects, then guarded) | Ends on `/dashboard` | | |
| 24 | `/laboratory-configuration/water` | `MasterData.Manage` | Redirect to `/dashboard` | | |
| 25 | `/laboratory-configuration/environmental-monitoring` | `MasterData.Manage` | Redirect to `/dashboard` | | |
| 26 | `/laboratory-configuration/after-cleaning` | `MasterData.Manage` | Redirect to `/dashboard` | | |
| 27 | `/laboratory-configuration/receiving-configuration` | `MasterData.Manage` | Redirect to `/dashboard` | | |
| 28 | `/laboratory-configuration/cause-of-testing` | `MasterData.Manage` (redirects, then guarded) | Ends on `/dashboard` | | |
| 29 | `/laboratory-configuration/equipment` | `MasterData.Manage` | Redirect to `/dashboard` | | |
| 30 | `/laboratory-configuration/fp-instruments` | `MasterData.Manage` | Redirect to `/dashboard` | | |
| 31 | `/laboratory-configuration/columns` | `MasterData.Manage` | Redirect to `/dashboard` | | |
| 32 | `/laboratory-configuration/solution-master` | `MasterData.Manage` | Redirect to `/dashboard` | | |
| 33 | `/laboratory-configuration/hplc-methods` | `MasterData.Manage` | Redirect to `/dashboard` | | |
| 34 | `/laboratory-configuration/label-formats` | `Labels.Configure` | Redirect to `/dashboard` | | |
| 35 | `/inventory/materials` | `Materials.Manage` | Redirect to `/dashboard` | | |
| 36 | `/inventory/equipment` | `Equipment.Manage` | Redirect to `/dashboard` | | |
| 37 | `/document-control/configuration` | `Documents.ConfigManage` | Redirect to `/dashboard` | | |
| 38 | `/document-control/reassignments` | `Documents.ConfigManage` or `Documents.Reassign` | Redirect to `/dashboard` | | |
| 39 | `/admin/users` | `Users.Manage` | Redirect to `/dashboard` | | |
| 40 | `/admin/company-sign-in` | `Users.Manage` | Redirect to `/dashboard` | | |
| 41 | `/admin/settings` | `System.ConfigureSettings` | Redirect to `/dashboard` | | |
| 42 | `/admin/integrations` | `System.ConfigureSettings` | Redirect to `/dashboard` | | |
| 43 | `/admin/organization` | `Documents.ConfigManage` | Redirect to `/dashboard` | | |
| 44 | `/admin/jobs` | `System.ManageJobs` | Redirect to `/dashboard` | | |
| 45 | `/admin/roles` | `Roles.Manage` | Redirect to `/dashboard` | | |
| 46 | `/admin/roles/new` | `Roles.Manage` | Redirect to `/dashboard` | | |
| 47 | `/admin/roles/{role id}` | `Roles.Manage` | Redirect to `/dashboard` | | |
| 48 | `/admin/error-log` | `System.ViewErrorLog` | Redirect to `/dashboard` | | |
| 49 | `/admin/overview` | `System.ViewErrorLog` | Redirect to `/dashboard` | | |

### A2. Pages with no route guard: they open for reading, every action must be absent or refused

These routes have no `PermissionRoute` in `AppRoutes.tsx`. Laboratory workspaces open for QA because `Sections.ViewAll` counts as membership for reading (`frontend/src/hooks/useMyLabs.ts`). The expected result is therefore not a redirect: the page opens, shows data read-only, and offers no way to record, receive, review, approve, start a run or qualify. For each row, also try every button and field visible on the page and note any that lets QA change data. A change that is accepted is a Fail; a button that is shown but answered with a server refusal is recorded in Notes (usability finding, not a security Fail).

| # | Path | In-page control that gates the action | Expected | Result (Pass / Fail) | Notes |
|---|---|---|---|---|---|
| 50 | `/receiving-testing` | none on the route (redirects to the first workspace) | Redirects to `/microbiology/workspace` (or the Physicochemical one); same as row 51 | | |
| 51 | `/microbiology/workspace?sampleId={sample id}` | Not determined from the route code (see "Guards not determined") | Opens read-only; no result is accepted; no review or approval is accepted | | |
| 52 | `/physicochemical/workspace` | Not determined from the route code | Opens read-only; no change accepted | | |
| 53 | `/physicochemical/rm-pm-workspace` | Not determined from the route code | Opens read-only; no change accepted | | |
| 54 | `/hplc-workspace` | `Hplc.Operate` (`canOperate` in `HplcWorkspacePage.tsx`) | Opens; no start-run action offered | | |
| 55 | `/hplc-workspace/{instrument id}/new-run` | none on the route | A run cannot be created: server refuses with 403 | | |
| 56 | `/hplc-workspace/{instrument id}/run/{run id}` | `Hplc.Operate` (`canOperate`) | Opens read-only; no entry or save offered | | |
| 57 | `/hplc-workspace/{instrument id}/run/{run id}/sst` | `Hplc.Operate` | Opens read-only | | |
| 58 | `/hplc-workspace/{instrument id}/run/{run id}/samples` | `Hplc.Operate` | Opens read-only | | |
| 59 | `/hplc-workspace/{instrument id}/run/{run id}/sample/{run sample id}` | `Hplc.Operate` (`canOperate` in `HplcSampleEntryPage.tsx`) | Opens read-only; no result entry | | |
| 60 | `/hplc-workspace/{instrument id}/run/{run id}/qualification/{run sample id}` | `Hplc.Operate` | Opens read-only; no entry accepted | | |
| 61 | `/gc-workspace/{instrument id}/run/{run id}` | `Hplc.Operate` (same pages as HPLC) | Opens read-only | | |
| 62 | `/gc-workspace/{instrument id}/new-run` | none on the route | A run cannot be created: server refuses with 403 | | |
| 63 | `/icp-workspace` | `Hplc.Operate` (`canOperate` in `IcpWorkspacePage.tsx`) | Opens; no start-run action offered | | |
| 64 | `/icp-workspace/{instrument id}/run/{run id}` | `Hplc.Operate` | Opens read-only | | |
| 65 | `/icp-workspace/{instrument id}/run/{run id}/sample/{run sample id}` | `Hplc.Operate` | Opens read-only; no result entry | | |
| 66 | `/working-standards` | `WorkingStandards.Qualify` (`canQualify` in `WorkingStandardsPage.tsx`) | Opens; no qualify action offered | | |

## Section B. Links QA Must Be Able To Open

Expected for every row: the page opens (no redirect to `/dashboard`) and shows data. Sign in as QA.

| # | Path | Permission | Expected | Result (Pass / Fail) | Notes |
|---|---|---|---|---|---|
| 1 | `/dashboard` | none | QA dashboard (audit trail reviews in progress, open findings, documents waiting for approval) | | |
| 2 | `/audit-trail-reviews` | `Audit.View` | List of reviews; a "start review" action is offered (`Audit.Review`) | | |
| 3 | `/audit-trail-reviews/{audit trail review id}` | `Audit.View` | Review opens with scope, period, cut-off, entries; finding and sign actions offered while In Progress | | |
| 4 | `/admin` | first visible admin tab | Redirects to the first admin page QA can see (not `/dashboard` as a refusal) | | |
| 5 | `/admin/audit` | `Audit.View` | Audit trail search opens | | |
| 6 | `/admin/access-review` | `Access.Review` | Access reviews list opens; finding and sign actions offered (`Access.Review.Sign`) | | |
| 7 | `/receiving/tracking` | `Samples.TrackAll` | Tracking board opens across both laboratories | | |
| 8 | `/monitoring-plan` | `MonitoringPlan.View` | Monitoring plan opens read-only | | |
| 9 | `/document-control` | none on the route | Document Control dashboard opens | | |
| 10 | `/document-control/library` | none on the route | Library opens | | |
| 11 | `/document-control/documents/{controlled document id}` | none on the route | Document opens; review and approval actions offered where the document is waiting | | |
| 12 | `/document-control/training-matrix` | none on the route (`Documents.TrainingViewMatrix` held) | Matrix opens | | |
| 13 | `/document-control/compliance-dashboard` | none on the route | Opens | | |
| 14 | `/document-control/audit` | `Audit.View` (server: `CanQueryGlobalAuditAsync`) | Document audit trail opens | | |
| 15 | `/document-control/my-reading-list` | none on the route | Opens | | |
| 16 | `/samples/{sample id}/report` | none on the route | Sample report opens (read) | | |
| 17 | `/profile` | none | Own profile opens | | |

### B2. Actions QA must be able to perform (spot checks)

| # | Action | Expected | Result (Pass / Fail) | Notes |
|---|---|---|---|---|
| 1 | Start an audit trail review at `/audit-trail-reviews` | Review is created with a cut-off and entry count | | |
| 2 | Sign that review with the QA signing password | Signature shows printed name, UTC time with zone, meaning "Audit trail reviewed"; review becomes read-only | | |
| 3 | Sign an access review at `/admin/access-review` where the QA user is not named in a finding | Accepted; a person named in a finding is refused | | |
| 4 | Approve a controlled document assigned to QA | Accepted with signature; the author of that revision cannot approve it | | |
| 5 | As the System Administrator (control), open `/admin/access-review` and try to sign while the QA user is active | Refused: "Access reviews are decided and signed by Quality Assurance." | | |

## Section C. Server Refusals (QA token, write requests)

For these, the browser guard is not the control. Using the browser developer tools (Network, "Copy as fetch") or an API client with the QA user's access token, send a write request (POST, PUT or DELETE) to a route under each prefix. A GET is not asserted here. **Expected: HTTP 403** and no change in the record. Controllers and policies are from `backend/MicroLIMS.API/Controllers/`.

| # | Route prefix | Policy | Result (Pass / Fail) | Notes |
|---|---|---|---|---|
| 1 | `POST api/samples` (register sample) | `Samples.ReceiveOwnLab` | | |
| 2 | `POST api/samples/{id}/laboratories` | `Samples.Receive` | | |
| 3 | `PUT api/samples/{id}/correct`, `POST api/samples/{id}/void` | `Samples.Correct` | | |
| 4 | `PUT api/samples/{id}/assign-analyst` | `Samples.AssignAnalyst` | | |
| 5 | `api/samples/{id}/review` | `Samples.Review` | | |
| 6 | `api/samples/{id}/approval` | `Samples.Approve` | | |
| 7 | `api/sample-preparation` | `samples.prepare` | | |
| 8 | `api/test-workflow` (result entry, steps) | `TestWorkflow.Execute` (supervise and biochemical decision under their own codes) | | |
| 9 | `api/media` (prepare) and media release | `Media.Prepare`, `Media.Release` | | |
| 10 | `api/media-evaluations` | `TestWorkflow.Execute` | | |
| 11 | `api/solution-preparations` | `Solutions.Prepare`, `Solutions.Release` | | |
| 12 | `api/hplc-workspace` | `Hplc.Operate`, `WorkingStandards.Qualify` | | |
| 13 | `api/icp-workspace` | `Hplc.Operate` | | |
| 14 | `api/working-standards` | `Samples.Review`, `Samples.Approve`, `WorkingStandards.Qualify` | | |
| 15 | `api/cryovials` | `Cryovials.Manage`, `Cryovials.Approve` | | |
| 16 | `api/materials` (release) | `Materials.Release` | | |
| 17 | `api/inventory/materials` | `Materials.Manage` | | |
| 18 | `api/inventory/equipment` | `Equipment.Manage` | | |
| 19 | `api/oos-tracking` | `Oos.Manage` | | |
| 20 | Equipment work records (`EquipmentWorkController`) | `EquipmentWork.Record` | | |

## Guards Not Determined From the Route Code

- Rows 51 to 53 (laboratory workspaces): the route has no guard and the page offers many actions. Only `Samples.AssignAnalyst` is visibly checked in `ReceivingTestingWorkspacePage.tsx`; which buttons are hidden for QA was not determined. The tester records what is visible; the server decides what is accepted (section C).
- Rows 55 and 62 (new-run wizard): the route has no guard and no in-page `canOperate` check was identified. The expected result is the server refusal.
- Section C: the table gives the policy declared on the controller or its methods. Which individual methods carry which policy was not enumerated; a request that returns anything other than 403 is recorded as a Fail and investigated.

## Result

- [ ] All rows in A1 redirected to `/dashboard`.
- [ ] No row in A2 accepted a change by QA.
- [ ] All rows in section B opened and B2 actions were accepted or refused as expected.
- [ ] All rows in section C returned 403.

**Overall result (Pass / Fail):** ____________  **Tester:** ____________________  **Date:** ____________

Failures are raised as deviations; a Fail in A1, A2 or C is a segregation-of-duties finding and blocks release of the QA role.
