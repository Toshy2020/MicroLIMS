## 7. Navigation

### 7.1 Sidebar Navigation Structure
The MicroLIMS navigation menu organizes the application into distinct operational groups. The items visible to any given user are dynamically determined by their granted permissions, laboratory section memberships, and assigned physicochemical areas:

| Group Heading | Menu Item (UI Label) | Route Path | Access Requirements |
|---|---|---|---|
| **OVERVIEW** | Dashboard | `/dashboard` | Any authenticated user |
| **RECEIVING** | Receive Sample | `/receiving` | Requires *Global sample receiving* (`Samples.Receive`) or *Own-lab sample receiving* (`Samples.ReceiveOwnLab`) |
| | Tracking Board | `/receiving/tracking` | Requires *Cross-lab sample tracking* (`Samples.TrackAll`) |
| **LABORATORIES** | **Microbiology Laboratory** | `/sections/microbiology-laboratory` | Requires active membership in `MICRO` |
| | – Workspace | `/microbiology/workspace` | `MICRO` membership |
| | – Media Preparation & Evaluation | `/laboratory-configuration/media` | Requires *Media lot preparation* (`Media.Prepare`) |
| | – Reference Cryovials | `/laboratory-configuration/cryovials` | Requires *Cryovial management* (`Cryovials.Manage`) |
| | – Materials Stock | `/inventory/materials?lab=MICRO` | Requires *Material stock management* (`Materials.Manage`) |
| | – Equipment Inventory | `/inventory/equipment?lab=MICRO` | Requires *Equipment inventory management* (`Equipment.Manage`) |
| **LABORATORIES** | **Microbiology Configuration** | `/sections/microbiology-configuration` | Requires `MICRO` membership AND *Master data management* (`MasterData.Manage`) |
| | – Test Master | `/laboratory-configuration/test-master` | `MICRO` membership AND `MasterData.Manage` |
| | – Organisms | `/laboratory-configuration/organisms` | `MICRO` membership AND `MasterData.Manage` |
| | – Media Configurations | `/laboratory-configuration/media-configurations` | `MICRO` membership AND `MasterData.Manage` |
| | – Water | `/laboratory-configuration/water` | `MICRO` membership AND `MasterData.Manage` |
| | – Environmental Monitoring | `/laboratory-configuration/environmental-monitoring` | `MICRO` membership AND `MasterData.Manage` |
| | – After Cleaning | `/laboratory-configuration/after-cleaning` | `MICRO` membership AND `MasterData.Manage` |
| | – Equipment | `/laboratory-configuration/equipment` | `MICRO` membership AND `MasterData.Manage` |
| **LABORATORIES** | **Physicochemical Laboratory** | `/sections/physicochemical-laboratory` | Requires active membership in `FP` |
| | – FP Workspace | `/physicochemical/workspace` | `FP` membership with assigned area `fp` |
| | – RM & PM Workspace | `/physicochemical/rm-pm-workspace` | `FP` membership with assigned area `rmpm` |
| | – Solution Preparation | `/preparation` | Requires *Solution preparation* (`Solutions.Prepare`) |
| | – Working Standards | `/working-standards` | `FP` membership |
| | – HPLC Workspace | `/hplc-workspace` | `FP` membership |
| | – GC Workspace | `/gc-workspace` | `FP` membership |
| | – ICP Workspace | `/icp-workspace` | `FP` membership |
| | – Materials Stock | `/inventory/materials?lab=FP` | Requires *Material stock management* (`Materials.Manage`) |
| | – Equipment Inventory | `/inventory/equipment?lab=FP` | Requires *Equipment inventory management* (`Equipment.Manage`) |
| **LABORATORIES** | **Physicochemical Configuration** | `/sections/physicochemical-configuration` | Requires `FP` membership AND *Master data management* (`MasterData.Manage`) |
| | – FP Test Master | `/laboratory-configuration/fp-test-master` | `FP` membership AND `MasterData.Manage` |
| | – RM & PM Test Master | `/laboratory-configuration/rm-pm-test-master` | `FP` membership AND `MasterData.Manage` |
| | – Equation Types | `/laboratory-configuration/equation-types` | `FP` membership AND `MasterData.Manage` |
| | – Physicochemical Instruments | `/laboratory-configuration/fp-instruments` | `FP` membership AND `MasterData.Manage` |
| | – Chromatography Columns | `/laboratory-configuration/columns` | `FP` membership AND `MasterData.Manage` |
| | – Reagents & Standards | `/laboratory-configuration/material-master` | `FP` membership AND `MasterData.Manage` |
| | – Solutions | `/laboratory-configuration/solution-master` | `FP` membership AND `MasterData.Manage` |
| | – HPLC Methods | `/laboratory-configuration/hplc-methods` | `FP` membership AND `MasterData.Manage` |
| | – ICP Methods | `/laboratory-configuration/icp-methods` | `FP` membership AND `MasterData.Manage` |
| **GENERAL LABORATORY CONFIGURATION** | Items | `/laboratory-configuration/items` | Requires *Items master management* (`Items.Manage`) |
| | Receiving Configuration | `/laboratory-configuration/receiving-configuration` | Requires *Master data management* (`MasterData.Manage`) |
| | Master Data Workbook | `/laboratory-configuration/master-data-workbook` | Requires *Master data management* (`MasterData.Manage`) |
| **DOCUMENT CONTROL** | Document Control (Landing) | `/sections/document-control` | Any authenticated user |
| | – Dashboard | `/document-control` | Any authenticated user |
| | – My Reading List | `/document-control/my-reading-list` | Any authenticated user |
| | – Training Matrix | `/document-control/training-matrix` | Any authenticated user (full matrix needs `Documents.TrainingAssign`) |
| | – Compliance Dashboard | `/document-control/compliance-dashboard` | Any authenticated user |
| | – Document Library | `/document-control/library` | Any authenticated user |
| | – Audit Trail | `/document-control/audit` | Role type `Reviewer`, `SectionHead`, or `SystemAdministrator` |
| | – Configuration | `/document-control/configuration` | Requires *Document Control configuration* (`Documents.ConfigManage`) |
| **ADMINISTRATION** | Users | `/users` | Requires *User account management* (`Users.Manage`) |
| | Roles | `/roles` | Requires *Role & permission management* (`Roles.Manage`) |
| **REPORTS** | Reports | `/reports` | Any authenticated user (KPI/Performance tab needs `Kpi.View`) |
| **AUDIT & COMPLIANCE** | Audit Search | `/audit-search` | Requires *Audit trail viewing* (`Audit.View`) |
| | OOS Tracking | `/oos-tracking` | Requires *OOS tracking management* (`Oos.Manage`) |
| **SYSTEM** | Error Monitoring | `/error-monitoring` | Requires *Error log monitoring* (`System.ViewErrorLog`) |

[SCREENSHOT SS-071]

---

### 7.2 Navigation Visibility Rules and Route Guards
- **Dynamic Menu Construction**: The sidebar navigation builder inspects the user's token claims. It iterates through all declared navigation items, evaluates permissions, checks laboratory section memberships, and removes any parent group that has no remaining visible children.
- **Laboratory Scoping**:
  - The Microbiology Laboratory items are visible only to users holding active membership in `MICRO`.
  - The Physicochemical Laboratory items are visible only to users holding active membership in `FP`.
  - Users with area `fp` see the **FP Workspace**; users with area `rmpm` see the **RM & PM Workspace**. If no area restriction is set, both workspaces are shown.
  - System Administrators are treated as members of both laboratories and both areas, ensuring all navigation groups are accessible.
- **Silent Route Redirection**: Navigation guards protect every underlying page route. If a user manually types a URL or follows a bookmarked link for a page they lack permission to access, MicroLIMS intercepts the request and **silently redirects the browser to `/dashboard` without displaying an error message**.

---

### 7.3 Legacy and Redirect Routes
MicroLIMS maintains automated redirects for legacy URLs and route aliases to ensure backward compatibility:

| Legacy or Alternate Path | Target Redirect Path | Redirection Mechanism |
|---|---|---|
| `/` | `/dashboard` | Default route redirect |
| `/receiving-testing` | First accessible laboratory workspace (`/microbiology/workspace`, `/physicochemical/workspace`, or `/physicochemical/rm-pm-workspace`) | Workspace resolver component (preserves URL query parameters) |
| `/testing-workspace` | `/receiving-testing` | Legacy redirect |
| `/laboratory-configuration/cause-of-testing` | `/laboratory-configuration/receiving-configuration` | Legacy redirect |
| `/inventory/approved-media` | `/laboratory-configuration/media?tab=approved` | Direct query parameter redirect |
| `/inventory/approved-cryovials` | `/laboratory-configuration/cryovials?tab=approved` | Direct query parameter redirect |
| `/groups/:slug` | `/sections/:slug` | Structural URL alias |
