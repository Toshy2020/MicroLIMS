## 33. Laboratories, Departments and Data Segregation

### 33.1 Laboratory Scoping and Department Organization

#### Purpose
MicroLIMS enforces multi-tier organizational data segregation to ensure laboratory personnel only access records, test orders, instrument workspaces, and inventories relevant to their operational domain.

#### Organizational Architecture
The system organizes laboratory operations into departments and laboratory sections:
- **Microbiology Laboratory (`MICRO`)**: Covers microbiological testing, media preparation and evaluation, reference cryovials, organism catalogs, and microbiological environmental/water monitoring.
- **Physicochemical Laboratory (`FP`)**: Covers analytical chemistry, chromatography (HPLC, GC), spectroscopy (ICP-OES), volumetric solutions, titrations, and chemical test workflows.

#### Scoping Rules and Boundaries
Access to laboratory test data is controlled by user organization memberships:
1. **Department-Level Membership**: Assigning a user to a department grants access across all active laboratory sections within that department.
2. **Section-Level Membership**: Restricts the user's data visibility strictly to the assigned laboratory section.
3. **Zero Membership**: A user account created without laboratory memberships has an empty data scope. When navigating to testing workspaces or dashboards, the system blocks access and reports:
   > "Your account is not assigned to any laboratory section. Ask an administrator to assign one."
4. **System Administrator Scope Bypass**: User accounts configured with the base role type `SystemAdministrator` bypass laboratory scoping by default. They possess global visibility across both laboratory sections, all departments, and all testing workspaces.

#### Physicochemical Operational Areas
Within the Physicochemical Laboratory (`FP`), testing activities are further divided by physical work area:
- **Finished Product Area (`fp`)**: Grants access to the Finished Product Workspace.
- **Raw Material & Packaging Material Area (`rmpm`)**: Grants access to the RM & PM Workspace.
- **Both Areas**: When no specific area restriction is selected ("Both"), the user has access to both workspaces.

#### Sample Receiving and Tracking Boundaries
- **Global Sample Receiving**: Requires the *Receive samples* permission (`Samples.Receive`, by default: Section Head, System Administrator). This allows receiving samples into any laboratory section across the organization.
- **Section-Restricted Receiving**: Requires the *Receive samples for own laboratory* permission (`Samples.ReceiveOwnLab`, by default: Analyst, Reviewer, Section Head). Receiving is permitted only for sections where the user holds active membership. Attempting to receive for another section is blocked with:
   > "You can receive samples only for your own laboratory."
- **Cross-Laboratory Tracking**: The global sample tracking board requires the *Track all samples* permission (`Samples.TrackAll`, by default: Section Head, System Administrator).

#### Document Control Sections
In the Document Control module, controlled documents, templates, and reading lists are categorized by document departments and sections to ensure controlled distribution.

---

### 33.2 Assigning Laboratory Sections and Memberships

<!-- WF-USERS-002 -->
#### Purpose
Assign one or more laboratory sections and operational areas to a user account to establish their data access boundaries.

#### Who can use it
Requires the *Manage users* permission (`Users.Manage`, by default: System Administrator).

#### Before you start
- The user account must already be created in the system.
- Active departments and laboratory sections must be configured.

#### What you see
[SCREENSHOT SS-331]
The **Laboratory Sections** dialog displays:
- Title: **Laboratory Sections: {fullName}**
- Department checkboxes: **{department}: All sections (entire department)**
- Individual section checkboxes: **{section} ({code})**
- For the Physicochemical section: **Area** dropdown with options **FP**, **RM & PM**, and **Both**
- Action buttons: **Cancel** and **Save Memberships**

#### How to use it
1. Navigate to **Administration** > **Users**.
2. Locate the user in the table and click the row action icon **Laboratory Sections**.
3. In the dialog, select the appropriate department or section checkboxes:
   - To grant access to all sections in a department, select the department checkbox.
   - To grant individual sections, check specific section boxes.
4. If assigning the Physicochemical Laboratory (`FP`), select the desired **Area**:
   - Select **FP** for Finished Products only.
   - Select **RM & PM** for Raw Materials and Packaging Materials only.
   - Select **Both** for unrestricted physicochemical access.
5. Click **Save Memberships**.

#### System behavior
- The system replaces all existing memberships for the user with the newly selected configuration.
- Membership updates are captured in the immutable audit trail under the acting administrator.
- Upon saving, the user's sidebar navigation, dashboards, and test assignment pickers update to reflect the assigned sections.

#### Validation
The server validates the submitted configuration:
- If a department is invalid: `"One or more departments were not found or are inactive."`
- If a section is invalid: `"Laboratory section {id} was not found or is inactive."`
- If section and department mismatch: `"Laboratory section {id} does not belong to department {id}."`
- If an area is assigned to a non-FP section: `"An area can only be set on a Physicochemical Laboratory membership."`

#### Status
- Changes take effect immediately upon database commit.

#### Important rules
- A user account can be assigned to both `MICRO` and `FP` simultaneously. Users with multi-lab memberships see laboratory switcher tabs on their dashboard (**Microbiology Laboratory** and **Physicochemical Laboratory**).
- Requesting a laboratory dashboard for an unassigned section returns: `"You are not a member of this laboratory."`

#### Errors
- If loading sections fails: `"Could not load laboratory sections or user memberships."`
- If saving fails: `"Could not update user memberships."`
- If no sections exist: `"No laboratory sections are configured in the system."`

#### What happens next
The user gains immediate access to the assigned workspaces, inventory catalogs, and test queues upon their next navigation.

> **Note for reviewers:** Empty membership list is a valid save, leaving the user with an empty scope where they cannot see any laboratory test data. Confirmation is required whether this behavior is intended. (GAP-014)

> **Note for reviewers:** Archived-record download has no laboratory scope check; a failed integrity check shows a warning toast. (GAP-108)
