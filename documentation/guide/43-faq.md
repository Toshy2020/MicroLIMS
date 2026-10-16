## 43. Frequently Asked Questions

This section answers common user questions regarding roles, permissions, laboratory workflows, segregation of duties, and system boundaries in MicroLIMS.

### 43.1 Access, Accounts, and Navigation

#### 1. Why don't I see the Microbiology Laboratory menu?
Sidebar navigation menus are governed by your account's assigned laboratory memberships. If your user account is not assigned to the Microbiology Laboratory section (`MICRO`) or its parent department, the Microbiology menus remain hidden. Only System Administrators bypass laboratory scope and see all menus by default. To gain access, ask a System Administrator to open **Administration > Users**, select **Laboratory Sections** for your account, and assign membership in the Microbiology section.

#### 2. Why can't I see both the Finished Product and Raw Material & Packaging Material workspaces?
For users assigned to the Physicochemical Laboratory (`FP`), workspace visibility is controlled by an organizational area setting:
- **FP**: Displays only the Finished Product Workspace (`/physicochemical/workspace`).
- **RM & PM**: Displays only the Raw Material & Packaging Material Workspace (`/physicochemical/rm-pm-workspace`).
- **Both**: Displays both workspaces.

If an administrator restricted your membership to one area, only that workspace appears in your sidebar. If your responsibilities require both, request that an administrator update your area assignment to **Both**.

#### 3. When do newly granted roles or permissions take effect for a signed-in user?
Functional permissions are embedded in the access token issued during authentication. When an administrator updates a role's permissions, those changes take effect when your next access token is issued (which occurs automatically every 15 minutes), or immediately if you sign out and sign back in. In contrast, account locking, account deactivation, password changes, and base role type changes are checked on every request and take effect immediately.

#### 4. Why am I locked out of my account?
Entering an incorrect password five consecutive times triggers an automated security lock on your account for 15 minutes. During this period, all sign-in attempts fail with the message `"Invalid username or password."` The account unlocks automatically after 15 minutes. Alternatively, a System Administrator can unlock the account immediately on the **Users** screen using the **Unlock Account** action.

#### 5. How do I recover my password if I cannot sign in?
If you cannot sign in and cannot use email reset, request administrator-assisted password recovery:
1. Contact a System Administrator, who opens **Users**, selects **Admin-Assisted Password Recovery**, enters a mandatory reason, and generates a 12-character one-time recovery code valid for 15 minutes.
2. On the sign-in page, click **Admin-Assisted Password Recovery** (or navigate to `/admin-recovery`).
3. Enter your **Username**, the **One-Time Recovery Code** (in the format `XXXX-XXXX-XXXX`), your **New Password**, and **Confirm New Password**.
4. Click **Submit Password Recovery**. Once completed, sign in with your new password.

#### 6. Can a user account with historical activity be permanently deleted?
No. To preserve data integrity and complete audit trail history, any user account that has logged in or participated in laboratory workflows cannot be permanently deleted. The system blocks the deletion with the message: `"This user has activity history and cannot be permanently deleted. Deactivate instead."` Administrators must disable the account instead, which revokes all active sessions and prevents further logins.

#### 7. Can a user modify their own role, disable their own account, or delete their own account?
No. MicroLIMS prohibits self-modification of administrative accounts. The system refuses these actions with explicit messages:
- `"System Administrators cannot modify their own role."`
- `"System Administrators cannot disable their own account."`
- `"You cannot permanently delete your own account."`

Another System Administrator must perform these actions.

#### 8. Why did I receive a warning that my session will time out?
MicroLIMS monitors user activity in the browser. If no keyboard, mouse, or touch input is detected for 20 minutes, a dialog appears with a 60-second countdown: `"You will be signed out due to inactivity in {n} second(s)."` Clicking **Stay signed in** resets the timer. If the countdown reaches zero, you are automatically signed out, redirected to the sign-in screen, and any unsaved form inputs are lost.

---

### 43.2 Testing, Incubation, and Workflow Execution

#### 9. Why can't I enter results for an ongoing incubation test?
MicroLIMS enforces incubation time gates based on the minimum incubation hours defined in the Test Master or Media Configuration. Before the minimum duration has elapsed, result entry is locked, and the system displays the earliest completion time (for example: `"This incubation window needs at least {h} hours - not ready until {utc} UTC."` or `"Stage 1 incubation for step \"{step}\" requires at least {h} hours of incubation - not ready until {utc} UTC."`). Results can only be recorded once the status advances to **Ready to Read**. If an urgent supervisor intervention is required, an authorized supervisor with the *Supervise testing workflows* permission (by default: System Administrator) can use **Skip Wait** to bypass the gate.

#### 10. Why does my electronic signature fail when I enter my password?
Electronic signatures require password re-authentication for every signed action. You must enter your current login password in the **Electronic Signature** dialog. If the password is typed incorrectly, the system displays: `"Password verification failed. The signature was not applied."` If five consecutive incorrect attempts occur, electronic signatures are locked for 15 minutes with the message: `"Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied."`

#### 11. Why was my test returned to me by the reviewer?
During technical review of a sample, a reviewer can return a count test order to the analyst using the **Return to Analyst** action. When returned, the test order moves back to the **Under Testing** state and the assigned analyst must correct and re-submit the count results. The **Return to Analyst** action is available only while the sample is under review and before the review is signed off. After a sample has been reviewed, corrections cannot be returned; any rework must be handled through retest or rejection decisions at approval.

#### 12. What is the difference between "Retest Retained Sample" and "New Sample Required"?
When non-conforming or out-of-specification results occur, a Section Head can order a retest during approval:
- **Retest Retained Sample**: Generates one new sample record linked to the original sample for the selected tests. It uses the retained sample material already in the laboratory and keeps the original analyst assignment.
- **New Sample Required**: Indicates that fresh physical sample material must be drawn from the manufacturing area. The system generates two new sample records for the selected tests and mandates assigning them to two different eligible analysts.

---

### 43.3 Review, Approval, and Segregation of Duties

#### 13. Why can't I review a test I entered?
MicroLIMS enforces segregation of duties between testing and review. An analyst who entered or signed test results cannot act as the technical reviewer for that sample. The system blocks the review with the message: `"You cannot review a sample you tested."` An independent Reviewer, Section Head, or System Administrator who did not enter test results on that sample must perform the technical review.

#### 14. Why can't I approve a sample I reviewed?
Segregation of duties requires independent verification between technical review and final release. A user who signed the technical review cannot submit the release decision for the same sample. The system rejects the attempt with: `"You cannot approve a sample you reviewed."` Another Section Head or System Administrator who did not perform the review must submit the approval decision.

#### 15. Can a System Administrator override segregation-of-duties rules to approve their own testing?
No. System Administrators possess no administrative exemption from segregation-of-duties rules. A System Administrator who entered results, prepared a sample, evaluated a media lot, or prepared a cryovial batch cannot review or approve that record.

#### 16. Why can't I verify a Master Data Workbook that I imported?
Master Data Workbook uploads enforce dual-control verification. The user who uploaded the workbook and performed the import cannot execute the second-person verification. The system displays: `"The person who loaded the workbook cannot verify it. Ask a second person."` A second qualified Section Head or System Administrator must inspect the staged changes and sign to complete verification.

---

### 43.4 System Capabilities and Unimplemented Features

#### 17. Is there a separate Final Release step or permission in MicroLIMS?
No. MicroLIMS does not have a separate Final Release step or permission. The release decision submitted by the Section Head (or authorized System Administrator) during the sample approval workflow (**Submit Release Decision**) serves as the final release decision. Approving the release decision closes testing for that section and makes the Certificate of Analysis (CoA) viewable and exportable.

#### 18. Does MicroLIMS include separate Reception or QA roles?
No. MicroLIMS does not include separate Reception or Quality Assurance (QA) role types. System roles are built upon four underlying base role types: System Administrator, Section Head, Reviewer, and Analyst. Sample receiving is performed by users holding the sample receiving permissions (by default: Analyst, Reviewer, Section Head, System Administrator), and release decisions are made by Section Heads or System Administrators.

#### 19. Does MicroLIMS support live instrument data feeds or ERP integrations?
No. Live instrument data feeds and SAP / ERP integrations are not implemented. Instrumental data (such as HPLC system suitability results, ICP-OES Syngistix calibration reports, and moisture/purity data) are entered manually or imported via structured report files.

#### 20. Does MicroLIMS provide annual Environmental Monitoring schedules, water sampling plans, or reference strain passage tracking?
No. The following capabilities are not implemented in MicroLIMS:
- Sterility testing as a dedicated testing workflow.
- Annual Environmental Monitoring plans and automated schedules.
- Water sampling frequencies and scheduled sampling plans.
- Reference strain passage tracking (MicroLIMS tracks reference cryovial lots, storage locations, expiry, and thaw events, but does not track serial passage numbers).
- Equipment qualification records (IQ/OQ/PQ).
- Dedicated incident, deviation, or CAPA modules.
