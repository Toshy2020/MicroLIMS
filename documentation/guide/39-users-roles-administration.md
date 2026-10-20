## 39. Users, Roles and Administration

### 39.1 User Account Administration

<!-- WF-USERS-001 -->
#### Purpose
Create user accounts, change profile details, change roles, disable or enable accounts, unlock accounts, start password recoveries and require a password change.

#### Who can use it
Requires the *Manage users* permission (`Users.Manage`, by default: System Administrator).

#### Before you start
- Have the user's full name, username and an initial password ready.
- Decide the role and the laboratory memberships (see §33) for the user.

#### What you see
[SCREENSHOT SS-391]
The **User Management** screen displays:
- Title: **User Management**
- Subtitle: "Manage system users, role assignments, security status, and account access."
- **Create New User** section:
  - Fields: **Full Name** (required), **Username** (required), **Email** (optional; helper text "Password reset destination"), **Password** (required; helper text "Min 8 chars")
  - Role selection with the placeholder **Select Role ***
  - Button: **Create User**
- **All System Users** table:
  - Columns: **User**, **Email**, **Role**, **Status**, **Security / Password**, **Last Login**, **Actions**
  - Chips: **You** (your own row); **Active** or **Disabled**; **Account Locked**, **Must Change Password** or **Normal**
  - Row actions: **Edit Profile**, **Laboratory Sections** (see §33), **Change Role**, **Disable Account** / **Enable Account**, **Unlock Account** (only when the account is locked), **Reset Password via Email**, **Admin-Assisted Password Recovery**, **Force Password Change at Next Login**, **Permanently Delete User**

#### How to use it

##### 1. Creating a new user account
1. Under **Create New User**, enter **Full Name**, **Username** and **Password**. The password needs at least 8 characters with an uppercase letter, a lowercase letter, a digit and a special character.
2. Optionally enter an **Email**.
3. Select the role under **Select Role ***.
4. Click **Create User**.
5. The account is created with **Must Change Password** switched on.

##### 2. Editing profile information
1. Click **Edit Profile** on the user's row.
2. Change **Full Name**, **Username** or **Email**.
3. Click **Save Profile**.

##### 3. Changing a user's role
1. Click **Change Role** on the user's row.
2. Select the **New Role**.
3. Enter the justification in **Reason for Change** (required).
4. Click **Change Role**.

##### 4. Disabling or enabling an account
1. Click **Disable Account** (or **Enable Account**) on the user's row.
2. When disabling, enter **Reason for Disabling** (required).
3. Click **Disable Account** (or **Enable Account**). Disabling an account signs the user out of all sessions and prevents sign-in.

##### 5. Unlocking a locked account
1. On a row showing **Account Locked** (an account locks after 5 wrong passwords), click **Unlock Account**.
2. Optionally enter **Reason for Unlock (Optional)**.
3. Click **Unlock Account**. The failed-attempt counter and the lock are cleared.

##### 6. Administrator-assisted password recovery
1. Click **Admin-Assisted Password Recovery** on the row of an active user other than yourself.
2. Enter **Reason for Recovery (Mandatory)**.
3. Click **Generate Recovery Code**.
4. The dialog shows a one-time 12-character code in the form XXXX-XXXX-XXXX with the chip **Expires in 15 minutes**. The code is shown once. Click **Copy Code**, then **Close & Done**.

> **Note for reviewers:** How the recovery code is handed to the user is not defined by any screen or rule in the application. (GAP-100)

##### 7. Requiring a password change
1. Click **Force Password Change at Next Login**.
2. Confirm. The user must change the password at the next sign-in. Sessions already open are not ended.

##### 8. Permanently deleting a user
1. Click **Permanently Delete User**.
2. Read the warning:
   > "This permanently removes the account and cannot be undone. It is only possible when the user has no activity history anywhere in the system. If this fails, deactivate the account instead."
3. Click **Permanently Delete**.

##### Other row actions
**Reset Password via Email** (button **Send Reset Link**) starts the e-mail reset. The confirmation is shown even when the user has no e-mail on file, and the reset cannot be completed in the application (see the reviewer notes below).

#### System behavior
- Each of these actions is recorded in the audit trail with the administrator and, where entered, the reason. None of them is signed.
- A role change to a different role type makes the user's open session refused at the next request. Other role or permission changes reach the user with their next access token (up to 15 minutes). Existing sessions are not ended.

#### Validation
- "Full Name, Username, Password, and Role are required."
- "Username '{username}' is already taken."
- "Selected Role and a Reason are required for role change."
- "A reason is required to change a user's role."
- "A reason is required to disable a user account."
- "A reason is required for admin-assisted password recovery."

#### Status
- Account status: **Active** or **Disabled**.
- Security status: **Normal**, **Account Locked** or **Must Change Password**.

#### Important rules
- **Administrators cannot do these to their own account**:
  - "System Administrators cannot modify their own role."
  - "System Administrators cannot disable their own account."
  - "You cannot permanently delete your own account."
  - "System Administrators cannot initiate admin-assisted password recovery for their own account."
- **Last administrator**: "Cannot delete the last active System Administrator account. Promote another user to System Administrator first."
- **History**: a user who has signed in or has any activity cannot be permanently deleted: "This user has activity history and cannot be permanently deleted. Deactivate instead."

#### Errors
- "User {id} not found."
- Failure messages: "Could not create user.", "Could not update profile.", "Could not change role.", "Could not update account status.", "Could not unlock account.", "Could not delete user."

#### What happens next
The user table refreshes and shows the new status and role.

> **Note for reviewers:** Role and user changes are audited but not signed and need no second person; account creation has no approval step. (GAP-009)

> **Note for reviewers:** "Forgot password?" cannot be completed in the application; ask an administrator for an admin-assisted recovery code. The on-screen text mentions a reset link, but none is sent. (GAP-100)

> **Note for reviewers:** The role-change audit entry shows the new role id as the previous value. (GAP-112)

---

### 39.2 Roles and Permissions

<!-- WF-ROLES-001 -->
#### Purpose
Create custom roles, choose which permissions each role has, and edit existing roles.

#### Who can use it
Requires the *Role & permission management* permission (`Roles.Manage`, by default: System Administrator).

#### Before you start
- Decide which of the four base role types (System Administrator, Section Head, Reviewer, Analyst) the new role should behave like.

#### What you see
[SCREENSHOT SS-392]
The **Roles** screen displays:
- Title: **Roles**
- Subtitle: "System Administrator, Section Head, Reviewer, Analyst, and any custom roles."
- Search box: **Search by role name...**
- Button: **Create Role**
- Table columns: **Role**, **Type**, **Status**, **Users**, with chips **System** / **Custom** and **Active** / **Inactive**

Clicking a role opens its detail screen:
- Subtitle: "Edit this role's name, description, and granted permissions."
- Chips: **System Role** and **Base Type: {type}**
- Fields: **Name** and **Description**
- A permission checklist with a description for each permission and a chip **Enforced** or **Legacy-only**. **Enforced** means the permission itself controls access. **Legacy-only** means access on those screens still follows the role's base type, not the checkbox.
- A list of the users who hold the role
- Buttons: **Save Changes** and **Delete Role** (not shown for system roles)

#### How to use it

##### 1. Creating a custom role
1. On the **Roles** screen, click **Create Role**.
2. **Name**: enter the **Role Name** (required) and optionally a **Description**. Click **Next**.
3. **Base Type**: choose **SystemAdministrator**, **SectionHead**, **Reviewer** or **Analyst** (default **Analyst**). Click **Next**.
4. **Permissions**: optionally select a role under **Or clone permissions from any existing role:**, then tick or untick permissions. Click **Next**.
5. **Review**: check the summary and click **Create Role**.

##### 2. Editing a role
1. Click the role name to open it.
2. Change **Name** or **Description**, and tick or untick permissions.
3. Click **Save Changes**.

##### 3. Deleting a custom role
1. Open a custom role that no user holds.
2. Click **Delete Role**.
3. Confirm: "Delete role "{name}"? This cannot be undone."

#### System behavior
- Saving permissions writes an audit entry listing the permissions granted and revoked. No reason, signature or second person is required.
- Permission changes reach signed-in users with their next access token (up to 15 minutes).

#### Validation
- "Role name is required."
- "Unknown permission code(s): {codes}."

#### Status
- Roles show **System** or **Custom**, and **Active** or **Inactive**.

#### Important rules
- The base type chosen at creation is not changed by later edits. It decides the dashboard type, laboratory scope and other checks that permissions do not change; a custom role behaves like its base type for those checks.
- System roles can be renamed and their permissions edited, but not deleted.
- Nothing prevents removing the *Role & permission management* permission from the System Administrator or Section Head role.

#### Errors
- "System roles cannot be deleted."
- "Role '{name}' is currently assigned to {n} user(s) and cannot be deleted."
- "Role {id} not found."
- "Could not create this role."

> **Note for reviewers:** Roles: 10 permissions marked "Legacy-only" have no effect; do not rely on them to restrict access. (GAP-015)

> **Note for reviewers:** Role and user changes are audited but not signed and need no second person; nothing prevents removing the *Role & permission management* permission from an administrative role. (GAP-009)

> **Note for reviewers:** Creating a role makes two separate saves; if the second fails, the role keeps the default permissions of its base type. (GAP-107)

<!-- Review log (Sonnet): 22 corrections; invented items removed: routes, aria/component names, 480-minute token claim, "takes effect immediately" claim, Delete Role visibility rule, "Users Holding This Role" label, hand-over of recovery code as a plain step, audit code names; callouts added: GAP-009, GAP-100 (recovery hand-over); server validation message and Reset by email paragraph added -->
