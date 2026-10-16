## 4. User Roles and Permissions

### 4.1 System Security and Role Architecture
MicroLIMS controls user access through two distinct layers:
1. **Roles**: Named security profiles assigned to user accounts. Every role in the system is built upon one of four underlying base role types: `SystemAdministrator`, `SectionHead`, `Reviewer`, or `Analyst`.
2. **Permission Codes**: Functional privilege codes representing specific actions (such as `Samples.Review`, `MasterData.Manage`, or `Hplc.Operate`). Exactly 50 permission codes are declared in the system.

> **Administrative Role Editability:** The permission grants described for the four system roles are **defaults**, not fixed assignments. An administrator holding the *Role & permission management* permission (`Roles.Manage`) can create custom roles, rename existing roles, and modify the permissions assigned to any role (including the default system roles). System roles cannot be deleted, but their permissions can be edited. Live deployment permissions depend on the current configuration in the database.

#### Laboratory Membership and Scoping
In addition to role permissions, access to laboratory data is governed by organizational memberships:
- **Microbiology Laboratory (`MICRO`)**: Grants access to the Microbiology workspace, media preparation, reference cryovials, and microbiology configuration.
- **Physicochemical Laboratory (`FP`)**: Governs chemical testing. Memberships can be scoped to specific physical areas:
  - **Finished Product area (`fp`)**: Access to the FP Workspace.
  - **Raw Material & Packaging Material area (`rmpm`)**: Access to the RM & PM Workspace.
  - If no area is specified, both workspaces are displayed.
- **Zero Membership**: A user with no assigned laboratory membership sees an empty scope and cannot access test data ("Your account is not assigned to any laboratory section. Ask an administrator to assign one.").
- **System Administrator Scope Bypass**: Users with the base role type `SystemAdministrator` bypass laboratory scoping by default and have global visibility across both laboratories and all areas.

#### Permission Propagation and Token Timing
- **Permission Updates**: Permissions are embedded within the user's access token claim set. Changes made to a role's permissions take effect when the user next refreshes their access token (default access token lifetime is 15 minutes, configurable from 1 to 480 minutes).
- **Immediate Revalidation**: Changes to a user's base role type, account lockout, account deactivation, or password updates are evaluated on every request by the server middleware and take effect immediately.
- **Self-Modification Restrictions**: Users cannot modify their own role, cannot disable their own account, and cannot delete their own account.

---

### 4.2 Seeded System Roles

#### System Administrator (`ROLE-SYSTEMADMINISTRATOR`)
- **Base Role Type**: `SystemAdministrator`
- **Default Permissions**: All 50 permissions granted by default.
- **Primary Responsibilities**: Oversees user account provisioning, role and permission configuration, system audit trails, technical error monitoring, and global configuration.
- **Operational Boundaries**: Possesses global visibility across all laboratories. However, System Administrators have **no administrative exemption** from segregation-of-duties rules: they cannot review or approve any sample, test, media lot, or standard they performed or prepared. System Administrators are also restricted from changing media product codes (which is reserved exclusively for the Section Head role type) and cannot void Document Masters.

#### Section Head (`ROLE-SECTIONHEAD`)
- **Base Role Type**: `SectionHead`
- **Default Permissions**: 43 permissions granted by default.
- **Primary Responsibilities**: Directs laboratory operations, assigns tests to analysts, supervises testing workflows, authorizes incubation overrides, performs secondary technical reviews, and executes final sample release authorizations (Approve, Reject, Retest, New Sample).
- **Exclusive Operational Authorities**:
  - Exclusively permitted to execute signed media product code changes.
  - Exclusively permitted to void controlled Document Masters in Document Control.
  - Exclusively authorized to override proposed document revision numbers.
- **Restrictions**: Does not manage user accounts or system roles by default. Cannot approve any sample they reviewed or tested.

#### Reviewer (`ROLE-REVIEWER`)
- **Base Role Type**: `Reviewer`
- **Default Permissions**: 15 permissions granted by default.
- **Primary Responsibilities**: Conducts secondary scientific verification of test results, raw plate counts, and calculations. Returns count tests to analysts when revision is needed. Serves as assigned technical reviewer for controlled document revisions.
- **Restrictions**: Cannot execute final sample release decisions, release media lots, or approve cryovial batches. Cannot review any test or sample they performed or prepared.

#### Analyst (`ROLE-ANALYST`)
- **Base Role Type**: `Analyst`
- **Default Permissions**: 15 permissions granted by default.
- **Primary Responsibilities**: Bench execution across microbiology and physicochemical laboratories: receiving samples for own laboratory sections, sample preparation, plate count entry, qualitative observations, pathogen test sessions, chromatography operation (HPLC, GC), ICP spectroscopy, solution preparations, and inventory updates.
- **Restrictions**: Holds no technical review or release approval permissions. Within testing workspaces, analysts default to viewing test orders assigned to them. Cannot review or approve own testing.

---

### 4.3 Permission Code Catalogue

Of the 50 permission codes declared in MicroLIMS, 40 are actively **ENFORCED** by backend policies or services. The remaining 10 codes are **Legacy-only / Not Enforced** in the software; toggling them in the permission matrix has no functional effect on access.

| # | Permission Code | Plain-Language Name | Default Roles | Status | Functional Scope |
|---|---|---|:---:|:---:|---|
| 1 | `Users.Manage` | User Account Management | SA | **ENFORCED** | Create, edit, lock/unlock, deactivate, force password change, and manage lab memberships. |
| 2 | `Roles.Manage` | Role & Permission Management | SA | **ENFORCED** | Create custom roles, rename roles, and update assigned permission sets. |
| 3 | `Audit.View` | Audit Trail Viewing | SA, SH | **ENFORCED** | View audit logs, change diffs, record history, and traceability chains. |
| 4 | `Reporting.Admin` | Administrative Reporting | SA | **ENFORCED** | Execute administrative reporting operations and result record backfills. |
| 5 | `Samples.Review` | Sample Technical Review | SA, SH, REV | **ENFORCED** | Submit technical reviews on sample sections and return tests to analysts. |
| 6 | `Samples.Approve` | Sample Release Approval | SA, SH | **ENFORCED** | Submit sample release decisions (Approve, Reject, Retest, New Sample) and close testing. |
| 7 | `Samples.Receive` | Global Sample Receiving | SA, SH | **ENFORCED** | Receive samples for any laboratory section globally and add laboratories to existing samples. |
| 8 | `Samples.TrackAll` | Cross-Lab Sample Tracking | SA, SH | **ENFORCED** | View the global Tracking Board across all laboratory sections. |
| 9 | `Signatures.Manage` | Signature Trail Viewing | SA, SH | **ENFORCED** | View electronic signature audit records. |
| 10 | `TestWorkflow.Execute` | Test Workflow Execution | SA, SH, REV, ANA | **ENFORCED** | Execute plate counts, qualitative observations, pathogen testing, and test results. |
| 11 | `TestWorkflow.BiochemicalDecision` | Biochemical Decision Recording | SA, SH, REV | **ENFORCED** | Record biochemical confirmation decisions on pathogen test steps. |
| 12 | `Cryovials.Manage` | Cryovial Management | SA, SH, REV, ANA | **ENFORCED** | Prepare batches, thaw, move storage, and destroy reference cryovials. |
| 13 | `Cryovials.Approve` | Cryovial Batch Approval | SA, SH | **ENFORCED** | Approve (release) or reject prepared reference cryovial batches. |
| 14 | `Materials.Manage` | Material Stock Management | SA, SH, ANA | **ENFORCED** | Receive inventory material lots, update details, consume stock, and upload documents. |
| 15 | `Materials.DocumentControl` | Material Document Control | SA, SH | **ENFORCED** | Supersede or void material inventory documents. |
| 16 | `Equipment.Manage` | Equipment Inventory Management | SA, SH, ANA | **ENFORCED** | Register equipment, update inventory details, and upload maintenance certificates. |
| 17 | `Equipment.DocumentControl` | Equipment Document Control | SA, SH | **ENFORCED** | Supersede or void equipment documents. |
| 18 | `Items.Manage` | Items Master Management | SA, SH | **ENFORCED** | Manage item master data, specifications, and item preparation configurations. |
| 19 | `Items.DocumentUpload` | Item Document Upload | SA, SH | **ENFORCED** | Upload controlled documents (SOPs, verification reports) to Items. |
| 20 | `MasterData.Manage` | Master Data Management | SA, SH | **ENFORCED** | Manage lab configurations across EM, Water, After Cleaning, Organisms, Media, and Workbooks. |
| 21 | `Discussions.View` | Discussions Viewing | SA, SH, REV, ANA | Legacy-only | Open to all signed-in users in code; permission code is not evaluated. |
| 22 | `Discussions.Create` | Discussions Creation | SA, SH, REV, ANA | Legacy-only | Open to all signed-in users in code; permission code is not evaluated. |
| 23 | `Discussions.EditAny` | Discussions Moderation | SA, SH | **ENFORCED** | Edit or delete any discussion post or comment, or mark posts important. |
| 24 | `Messages.Use` | Messaging Use | SA, SH, REV, ANA | Legacy-only | Open to all signed-in users in code; permission code is not evaluated. |
| 25 | `System.ViewErrorLog` | Error Log Monitoring | SA | **ENFORCED** | View technical incident logs and system health error monitoring. |
| 26 | `System.ViewSecurityAudit` | Security Audit Viewing | SA | Legacy-only | Security events are captured in the database; no viewer endpoint checks this code. |
| 27 | `Documents.Register` | Document Master Registration | SA, SH, ANA | **ENFORCED** | Register new controlled Document Masters in Document Control. |
| 28 | `Documents.DraftEdit` | Document Draft Editing | SA, SH, ANA | Legacy-only | Service checks role type and document author assignment directly. |
| 29 | `Documents.RevisionCreate` | Document Revision Creation | SA, SH, ANA | Legacy-only | Service checks role type and document author assignment directly. |
| 30 | `Documents.Review` | Document Technical Review | SA, REV | Legacy-only | Service checks assigned technical reviewer assignment and role type directly. |
| 31 | `Documents.Approve` | Document Revision Approval | SA, SH, REV | Legacy-only | Service checks assigned approver assignment and segregation of duties directly. |
| 32 | `Documents.PeriodicReview` | Document Periodic Review | SA, SH, REV | Legacy-only | Service checks assigned periodic reviewer and role type directly. |
| 33 | `Documents.TrainingAssign` | Document Training Assignment | SA, SH | **ENFORCED** | Assign reading and training curricula to personnel; view full training matrix. |
| 34 | `Documents.TrainingViewMatrix` | Training Matrix Viewing | SA, SH, REV | Legacy-only | Controller checks `Documents.TrainingAssign` to view full matrix. |
| 35 | `Documents.ConfigManage` | Document Control Configuration | SA | **ENFORCED** | Manage document types, departments, sections, numbering, and settings (requires SystemAdministrator type). |
| 36 | `Dashboards.LabOverview` | Lab Overview Dashboard | SA, SH | **ENFORCED** | View the Section Head laboratory overview dashboard. |
| 37 | `Dashboards.Review` | Reviewer Dashboard | SA, SH, REV | **ENFORCED** | View the Reviewer command center dashboard. |
| 38 | `Kpi.View` | KPI Dashboard Viewing | SA, SH | **ENFORCED** | View the KPI / Performance reporting tab. |
| 39 | `Kpi.ViewTeam` | Team KPI & Workload Viewing | SA, SH | **ENFORCED** | View KPI metrics and workload figures for other analysts across the section. |
| 40 | `Kpi.ConfigureProfiles` | KPI Profile Configuration | SA, SH | **ENFORCED** | Create and sign workflow profile versions (work units, stage targets). |
| 41 | `Media.Prepare` | Media Lot Preparation | SA, SH, REV, ANA | **ENFORCED** | Prepare media lots, record storage moves, and mark lots out of stock. |
| 42 | `Media.Release` | Media Lot Release | SA, SH | **ENFORCED** | Release or reject prepared and evaluated media lots under electronic signature. |
| 43 | `Oos.Manage` | OOS Tracking Management | SA, SH | **ENFORCED** | Manage OOS tracking groups and upload laboratory investigation documents. |
| 44 | `TestWorkflow.Supervise` | Test Workflow Supervision | SA, SH | **ENFORCED** | Supervisor overrides on minimum incubation times and pathogen session resets. |
| 45 | `Samples.ReceiveOwnLab` | Own-Lab Sample Receiving | SA, SH, REV, ANA | **ENFORCED** | Receive samples for laboratory sections where user holds active membership. |
| 46 | `Samples.Correct` | Sample Correction & Voiding | SA, SH, REV | **ENFORCED** | Correct sample metadata and void sample records under electronic signature. |
| 47 | `Samples.AssignAnalyst` | Sample Analyst Assignment | SA, SH | **ENFORCED** | Assign responsible analysts to sample test orders. |
| 48 | `Solutions.Prepare` | Solution Preparation | SA, SH, ANA | **ENFORCED** | Prepare solutions and mobile phases, and standardize volumetric titrants. |
| 49 | `Hplc.Operate` | HPLC/GC/ICP Operation | SA, SH, ANA | **ENFORCED** | Start instrument runs, confirm system suitability (SST), and submit runs. |
| 50 | `WorkingStandards.Qualify` | Working Standard Qualification | SA, SH, ANA | **ENFORCED** | Prepare working standards, upload documents, and enter qualification replicates. |

> **Note for reviewers:** Ten permissions marked "Legacy-only" have no effect in the application; do not rely on them to restrict access. (GAP-015)

---

### 4.4 Segregation of Duties (SoD) Rules
MicroLIMS enforces strict segregation of duties to safeguard data integrity and prevent self-approval. The segregation guard evaluates user IDs directly across transactions and provides **no exemption for System Administrators or Section Heads**:

1. **Sample Technical Review**:
   - A user cannot perform a technical review on a sample section if they performed or prepared any test in that section.
   - Refusal message: `"You cannot review a sample you tested."`
2. **Sample Approval**:
   - An approver cannot approve a sample if they acted as the technical reviewer for that section.
   - Refusal message: `"You cannot approve a sample you reviewed."`
   - An approver cannot approve a sample if they performed or prepared any test in that section.
   - Refusal message: `"You cannot approve a sample you tested."`
3. **Pathogen Confirmation**:
   - A reviewer cannot record a biochemical confirmation decision on a pathogen test they performed.
   - Refusal message: `"A reviewer cannot decide on a result they performed."`
4. **Media Lot Release**:
   - A user cannot approve or release a media lot they prepared or evaluated.
   - Refusal message: `"You cannot approve a media lot you prepared or evaluated."`
5. **Cryovial Batch Approval**:
   - A user cannot approve a cryovial batch they prepared.
   - Refusal message: `"You cannot approve a cryovial batch you prepared."`
6. **Master Data Workbook Verification**:
   - The user who loaded a master data workbook cannot verify it.
   - Refusal message: `"The person who loaded the workbook cannot verify it. Ask a second person."`
7. **Document Control Governance**:
   - The author of a document revision cannot act as its technical reviewer, cannot create reviewer findings, and cannot approve the revision.
   - A technical reviewer who participated in a revision review cannot approve that revision.

---

### 4.5 Role Management Procedures

<!-- WF-ROLES-001 -->
### 4.5.1 Create a Custom Role
#### Purpose
Define a new security profile with tailored permissions based on an underlying system role type.

#### Who can use it
Requires the *Role & permission management* permission (by default: System Administrator).

#### Before you start
Determine the underlying base role type (`SystemAdministrator`, `SectionHead`, `Reviewer`, or `Analyst`) that reflects the role's operational duties.

#### What you see
- Navigation: **ADMINISTRATION** > **Roles** (`/roles`).
- Roles list with columns **Role**, **Type**, **Status**, and **Users**, and button **Create Role**.
- Create Role wizard (`/roles/new`) with four stepper stages: **Name**, **Base Type**, **Permissions**, and **Review**.

[SCREENSHOT SS-041]

#### How to use it
1. Navigate to **ADMINISTRATION** > **Roles** in the sidebar.
2. Click **Create Role** to launch the wizard.
3. On the **Name** step, enter the **Role Name** (mandatory) and optional **Description**. Click **Next**.
4. On the **Base Type** step, select the underlying base role type radio button: **SystemAdministrator**, **SectionHead**, **Reviewer**, or **Analyst** (default is Analyst). Click **Next**.
5. On the **Permissions** step, check each permission code to grant. Optionally select an existing role from the **Or clone permissions from any existing role:** dropdown to copy its grants. Click **Next**.
6. On the **Review** step, verify the summary details and granted permission chips. Click **Create Role**.

#### System behavior
The system registers the role, sets its base type, and saves the permission set. The newly created role appears in the Roles list.

> **Note for reviewers:** Role and user changes are audited but not signed and need no second person; nothing prevents removing Roles.Manage from an administrative role. (GAP-009)

#### Validation
- If the role name is blank, the system blocks progression with: `"Role name is required."`
- If an invalid code is supplied, the server returns: `"Unknown permission code(s): {codes}."`

#### Errors
- If role creation fails, the system displays: `"Could not create this role."`

---

### 4.5.2 Edit Role Permissions and Details
#### Purpose
Update the description or modify granted permission codes for an existing role.

#### Who can use it
Requires the *Role & permission management* permission (by default: System Administrator).

#### What you see
- Role detail page (`/roles/:id`) displaying role name, **Base Type**, **System Role** indicator, and editable fields **Name** and **Description**.
- **Permissions** matrix with checkboxes, accompanied by **Enforced** and **Legacy-only** indicator chips.
- Users section displaying active and disabled role holders.
- Action buttons: **Save Changes** and **Delete Role** (for custom roles).

[SCREENSHOT SS-042]

#### How to use it
1. In the **Roles** list, click on the role to open its detail page.
2. Modify the **Name** or **Description** fields if needed.
3. In the **Permissions** matrix, check or uncheck individual permission codes.
4. Click **Save Changes**.

#### System behavior
The system updates the role metadata and writes a `ROLE_PERMISSIONS_UPDATED` entry to the audit trail recording all granted and revoked permission codes. Assigned users acquire new permissions upon their next access token refresh (up to 15 minutes).

> **Note for reviewers:** The role-change audit entry shows the new role id as the previous value. (GAP-112)

#### Validation
- System roles cannot be deleted. Clicking delete on a system role is prevented, and the server returns: `"System roles cannot be deleted."`
- If a custom role is assigned to active users, deletion is refused with: `"Role '{name}' is currently assigned to {n} user(s) and cannot be deleted."`
