## 39. Users, Roles and Administration

### 39.1 User Account Administration

<!-- WF-USERS-001 -->
#### Purpose
Provision new laboratory user accounts, modify profile details, reassign security roles, manage account lockouts, initiate password recoveries, and enforce password change policies.

#### Who can use it
Requires the *Manage users* permission (`Users.Manage`, by default: System Administrator).

#### Before you start
- Ensure you have the user's full name, unique corporate username, and initial temporary password.
- Determine the appropriate system role and laboratory memberships (see §33) for the user.

#### What you see
[SCREENSHOT SS-391]
The **User Management** screen (`/users`) displays:
- Title: **User Management**
- Subtitle: "Manage system users, role assignments, security status, and account access."
- **Create New User** panel:
  - Input fields: **Full Name** (required), **Username** (required), **Email** (optional; helper text: "Password reset destination"), and **Password** (required; helper text: "Min 8 chars")
  - Role dropdown: aria **Role**, placeholder **Select Role ***
  - Action button: **Create User**
- **All System Users** table:
  - Columns: **User**, **Email**, **Role**, **Status**, **Security / Password**, **Last Login**, and **Actions**
  - Status indicators: **You** (current user row); status chips **Active** or **Disabled**; security chips **Account Locked**, **Must Change Password**, or **Normal**
  - Row action icons:
    - **Edit Profile**
    - **Laboratory Sections** (opens department scoping dialog, §33)
    - **Change Role**
    - **Disable Account** / **Enable Account**
    - **Unlock Account** (visible only when an account is locked)
    - **Reset Password via Email**
    - **Admin-Assisted Password Recovery**
    - **Force Password Change at Next Login**
    - **Permanently Delete User**

#### How to use it

##### 1. Creating a New User Account
1. Under **Create New User**, enter the user's **Full Name**, **Username**, and initial **Password** (must meet the 8-character complexity policy: uppercase, lowercase, digit, and special character).
2. Enter an optional **Email** address.
3. Select an assigned role from the **Select Role *** dropdown.
4. Click **Create User**.
5. The account is created with the security flag **Must Change Password** automatically enabled.

##### 2. Editing User Profile Information
1. On the user's row, click **Edit Profile**.
2. Modify the **Full Name**, **Username**, or **Email**.
3. Click **Save Profile**.

##### 3. Reassigning a User Role
1. Click **Change Role** on the user's row.
2. Select the **New Role** from the dropdown.
3. Enter a mandatory administrative justification in **Reason for Change**.
4. Click **Change Role**.

##### 4. Disabling or Enabling an Account
1. Click **Disable Account** (or **Enable Account**).
2. When disabling, enter a mandatory justification in **Reason for Disabling**.
3. Click **Disable Account** (or **Enable Account**). Disabling an account immediately revokes all active refresh tokens for that user.

##### 5. Unlocking a Locked Account
1. For accounts displaying the **Account Locked** chip (triggered by 5 failed password attempts), click **Unlock Account**.
2. Optionally enter an explanation in **Reason for Unlock (Optional)**.
3. Click **Unlock Account**. The failure counter and lockout timer are reset to zero.

##### 6. Administrator-Assisted Password Recovery
1. When a user is locked out and cannot receive reset emails, click **Admin-Assisted Password Recovery**.
2. Enter a mandatory reason in **Reason for Recovery (Mandatory)**.
3. Click **Generate Recovery Code**.
4. The dialog displays a one-time 12-character alphanumeric code (formatted as `XXXX-XXXX-XXXX`) with the chip **Expires in 15 minutes**.
5. Click **Copy Code** to copy the code to the clipboard, provide the code securely to the user, and click **Close & Done**.

##### 7. Forcing Password Change
1. Click **Force Password Change at Next Login**.
2. Confirm the action when prompted. The user will be redirected to the password change screen immediately upon their next sign-in.

##### 8. Permanently Deleting a User
1. Click **Permanently Delete User**.
2. Review the confirmation warning:
   > "This permanently removes the account and cannot be undone. It is only possible when the user has no activity history anywhere in the system. If this fails, deactivate the account instead."
3. Click **Permanently Delete**.

#### System behavior
- Every user administration action is recorded in the audit trail with the administrator's ID and entered justification.
- Modifying a user's role type, deactivating an account, or unlocking an account takes effect immediately on the next server request.

#### Validation
- Submitting empty required fields in user creation returns: `"Full Name, Username, Password, and Role are required."`
- Duplicate usernames return: `"Username '{username}' is already taken."`
- Changing roles without justification returns: `"Selected Role and a Reason are required for role change."`
- Disabling an account without justification returns: `"A reason is required to disable a user account."`
- Generating a recovery code without justification returns: `"A reason is required for admin-assisted password recovery."`

#### Status
- User status: **Active** or **Disabled**.
- Security status: **Normal**, **Account Locked**, or **Must Change Password**.

#### Important rules
- **Self-Account Operations Blocked**: Administrators cannot modify their own role, cannot disable their own account, cannot delete their own account, and cannot initiate administrator-assisted recovery on themselves:
  - `"System Administrators cannot modify their own role."`
  - `"System Administrators cannot disable their own account."`
  - `"You cannot permanently delete your own account."`
  - `"System Administrators cannot initiate admin-assisted password recovery for their own account."`
- **Last Administrator Guard**: The system prevents deleting the final active administrator:
  - `"Cannot delete the last active System Administrator account. Promote another user to System Administrator first."`
- **Activity History Deletion Guard**: Accounts that have ever signed in or performed actions cannot be permanently deleted:
  - `"This user has activity history and cannot be permanently deleted. Deactivate instead."`

#### Errors
- If user lookup fails: `"User {id} not found."`
- Operation failure toasts include: `"Could not create user."`, `"Could not update profile."`, `"Could not change role."`, `"Could not update account status."`, `"Could not unlock account."`, and `"Could not delete user."`.

#### What happens next
The user table refreshes immediately to display the updated account status and role assignments.

---

### 39.2 Role Architecture and Permission Configuration

<!-- WF-ROLES-001 -->
#### Purpose
Configure system and custom security roles, assign granular functional permissions, and maintain organizational access boundaries.

#### Who can use it
Requires the *Role & permission management* permission (`Roles.Manage`, by default: System Administrator).

#### Before you start
- Determine the required operational responsibilities and identify the appropriate underlying base role type.

#### What you see
[SCREENSHOT SS-392]
The **Roles** screen (`/roles`) displays:
- Title: **Roles**
- Subtitle: "System Administrator, Section Head, Reviewer, Analyst, and any custom roles."
- Filter input: **Search by role name...**
- Action button: **Create Role** (navigates to `/roles/new`)
- Table columns: **Role**, **Type**, **Status**, and **Users**
- Row chips: **System** / **Custom** and **Active** / **Inactive**

Clicking a role opens the **Role Detail** screen (`/roles/:id`):
- Header: Role Name, with subtitle: "Edit this role's name, description, and granted permissions."
- Chips: **System Role** (or **Custom Role**) and **Base Type: {type}**
- Input fields: **Name** and **Description**
- **PermissionMatrix** checklist:
  - Displays each declared permission code with its description.
  - Status chips: **Enforced** (green chip indicating the permission directly controls API authorization) or **Legacy-only** (warning chip indicating access still follows the underlying base role type).
- **Users Holding This Role** list
- Action buttons: **Save Changes** and **Delete Role** (visible only for custom roles with zero assigned users)

#### How to use it

##### 1. Creating a Custom Role
1. On the **Roles** screen, click **Create Role**.
2. **Step 1 (Name)**: Enter a unique **Role Name** and optional **Description**. Click **Next**.
3. **Step 2 (Base Type)**: Select the underlying base type (**SystemAdministrator**, **SectionHead**, **Reviewer**, or **Analyst**). Click **Next**.
4. **Step 3 (Permissions)**: Optionally choose an existing role under **Or clone permissions from any existing role:** to pre-populate selections, then check or uncheck individual permission codes in the matrix. Click **Next**.
5. **Step 4 (Review)**: Review the configuration summary and click **Create Role**.

##### 2. Modifying Role Permissions
1. On the **Roles** screen, click on the target role name to open its details.
2. Update the **Name** or **Description** if needed.
3. In the **PermissionMatrix**, select or deselect the desired permission checkboxes.
4. Click **Save Changes**.

##### 3. Deleting a Custom Role
1. Open the details of an unassigned custom role.
2. Click **Delete Role**.
3. Confirm the prompt: `"Delete role \"{name}\"? This cannot be undone."`

#### System behavior
- Creating or editing a role generates a `ROLE_PERMISSIONS_UPDATED` entry in the audit trail, logging the exact list of granted and revoked permission codes.
- Permission changes take effect when the user next refreshes their access token (default token lifetime is 15 minutes, configurable up to 480 minutes).

#### Validation
- Saving without a name returns: `"Role name is required."`
- Attempting to delete a built-in role returns: `"System roles cannot be deleted."`
- Attempting to delete a role currently assigned to users returns: `"Role '{name}' is currently assigned to {n} user(s) and cannot be deleted."`

#### Status
- Roles display chips for **System** or **Custom**, and **Active** or **Inactive**.

#### Important rules
- **Base Role Type Rules**: The underlying base type chosen during creation permanently dictates hardcoded system behaviors, such as dashboard command centers, laboratory scope bypass, and analyst own-test filtering.
- **System Role Editability**: System roles cannot be deleted, but their permissions can be edited by an administrator holding `Roles.Manage`.
- **Administrative Guard Notice**: No system guard prevents an administrator from removing `Roles.Manage` from the System Administrator or Section Head role.

#### Errors
- If creation fails: `"Could not create this role."`
- If lookup fails: `"Role {id} not found."`

> **Note for reviewers:** Roles: 10 permissions marked "Legacy-only" have no effect; do not rely on them to restrict access. (GAP-015)

> **Note for reviewers:** Role and user changes are audited but not signed and need no second person; nothing prevents removing Roles.Manage from an administrative role. (GAP-009)

> **Note for reviewers:** The role-change audit entry shows the new role id as the previous value. (GAP-112)

> **Note for reviewers:** "Forgot password?" cannot be completed in the application; ask an administrator for an admin-assisted recovery code. The on-screen text mentions a reset link, but none is sent. (GAP-100)

> **Note for reviewers:** Creating a role executes two separate calls; if the second call fails, the role keeps the default grants of its base type. (GAP-107)
