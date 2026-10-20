## 43. Frequently Asked Questions

This section answers common questions about access, accounts, testing, review and approval, and what MicroLIMS does not do. Details are in the sections named in each answer.

### 43.1 Access, Accounts and Navigation

#### 1. Why don't I see the Microbiology Laboratory menu?
The menus you see follow your laboratory memberships. If your account is not assigned to the Microbiology section (or to its whole department), the Microbiology menus stay hidden. System Administrator accounts are not limited by laboratory and see all menus. Ask a System Administrator to open **Users**, choose **Laboratory Sections** on your row and assign the section (see §33).

#### 2. Why can't I see both the Finished Product and the Raw Material & Packaging Material workspaces?
For the Physicochemical Laboratory a membership can carry an area:
- **FP**: only the Finished Product workspace.
- **RM & PM**: only the Raw Material and Packaging Material workspace.
- **Both**: both workspaces.

If you need the other workspace, ask an administrator to change your area to **Both** (see §33).

#### 3. When does a new role or permission take effect for a signed-in user?
Permission changes reach a signed-in user within about 15 minutes, or earlier if the user signs out and signs in again. If a user's role is changed to a different role type, the open session is refused at the next request. Changing a password signs the user out of other sessions. Existing sessions are not ended by a role change itself.

#### 4. Why am I locked out of my account?
After 5 wrong passwords the account is locked for 15 minutes, and sign-in then fails with "Invalid username or password." The lock ends by itself after 15 minutes, or a System Administrator can use **Unlock Account** on the **Users** screen (see §39).

> **Note for reviewers:** After a lock ends the failed-attempt counter is not reset, so one further wrong password locks the account again. (GAP-012)

#### 5. How do I recover my password if I cannot sign in?
The e-mail reset ("Forgot password?") cannot be completed in the application. Use administrator-assisted recovery:
1. Ask a System Administrator to open **Users**, choose **Admin-Assisted Password Recovery**, enter a reason and generate the one-time code (12 characters, valid for 15 minutes).
2. On the sign-in page click **Admin-Assisted Password Recovery**.
3. Enter your **Username**, the **One-Time Recovery Code**, your **New Password** and **Confirm New Password**.
4. Click **Submit Password Recovery**, then sign in with the new password.

> **Note for reviewers:** "Forgot password?" cannot be completed in the application; ask an administrator for an admin-assisted recovery code. The on-screen text mentions a reset link, but none is sent. (GAP-100)

#### 6. Can a user account with activity be permanently deleted?
No. An account that has signed in or has any activity cannot be permanently deleted: "This user has activity history and cannot be permanently deleted. Deactivate instead." Disable the account instead; this signs the user out and prevents sign-in.

#### 7. Can an administrator change, disable or delete their own account?
No. The system refuses with "System Administrators cannot modify their own role.", "System Administrators cannot disable their own account." and "You cannot permanently delete your own account." Another System Administrator must do these.

#### 8. Why did I get a warning that I will be signed out?
The browser watches for activity. After 20 minutes without keyboard, mouse or touch input a countdown of 60 seconds appears: "You will be signed out due to inactivity in {n} second(s)." Click **Stay signed in** to continue. If it reaches zero you are signed out and unsaved entries are lost.

> **Note for reviewers:** Idle sign-out happens in the browser only; the server does not time out inactive sessions. (GAP-053)

---

### 43.2 Testing, Incubation and Workflow

#### 9. Why can't I enter results for a test that is incubating?
Each incubation has a minimum time set in the Test Master or media configuration. Until it has passed, result entry is locked and the message shows the earliest time, for example "This incubation window needs at least {h} hours - not ready until {utc} UTC." The test changes to **Ready to Read** when the time has passed. A user with the *Supervise testing workflows* permission (by default: Section Head, System Administrator) can use **Skip Wait** (see §42.6).

#### 10. Why does my electronic signature fail?
You must type your own current sign-in password in the **Electronic Signature** dialog. A wrong password gives "Password verification failed. The signature was not applied." After 5 wrong attempts within 15 minutes signing is refused for 15 minutes: "Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied." Your sign-in account is not locked by this (see §37).

#### 11. Why was my test returned to me by the reviewer?
During review a reviewer can use **Return to Analyst** on a count test or a physicochemical result-entry test. For a count test the readings are set aside and the incubation step is reopened; for a physicochemical test the active entries are set aside and the test goes back to **Running**. The test returns to the assigned analyst. This is possible only before the review of the laboratory section is signed. After that the system refuses: "Cannot return a test to the analyst after the sample has been reviewed. Use Reject or Retest at approval instead." (see §18).

#### 12. What is the difference between "Retest Retained Sample" and "New Sample Required"?
Both are release-decision options chosen by the Section Head at approval (see §18 and §19):
- **Retest Retained Sample: Return to testing** creates one new sample linked to the original, holding new copies of the selected tests.
- **New Sample Required: Close and request new sample** creates two new samples holding copies of the selected tests; two different analysts must be chosen for them.

---

### 43.3 Review, Approval and Segregation of Duties

#### 13. Why can't I review a test I entered?
The person who performed the testing cannot review it: "You cannot review a sample you tested." Another user with the review permission must review it (see §42.4).

#### 14. Why can't I approve a sample I reviewed?
The reviewer cannot also give the release decision: "You cannot approve a sample you reviewed." A different user with the *Sample Release Approval* permission must approve. A person who tested the sample cannot approve it either: "You cannot approve a sample you tested."

#### 15. Can a System Administrator override these rules?
No. The segregation-of-duties rules (§37.4) compare user accounts only and System Administrators are not exempt. A System Administrator who prepared or tested a record covered by those rules cannot review or approve it.

#### 16. Why can't I verify a Master Data Workbook that I loaded?
The person who loaded the workbook cannot verify it: "The person who loaded the workbook cannot verify it. Ask a second person." A second authorised user must verify it (see §32).

---

### 43.4 What MicroLIMS Does Not Do

#### 17. Is there a separate Final Release step?
No. MicroLIMS does not have a separate Final Release step or permission. The Section Head's signed release decision on a laboratory section is the last approval step and **Approved** is the final status (see §19).

#### 18. Does MicroLIMS have separate Reception or QA roles?
No. The four system roles are System Administrator, Section Head, Reviewer and Analyst. Product, Raw Material and Packaging Material samples are received by users who hold a receiving permission; Environmental Monitoring, Water and After Cleaning samples by any signed-in user, and release decisions by users who hold the *Sample Release Approval* permission, as set by the administrator.

#### 19. Does MicroLIMS take data directly from instruments or connect to an ERP system?
No. There is no live instrument feed, no screen for importing instrument reports, no SAP or ERP connection and no AI assistant. Instrument results are entered in the testing workspaces (see §41).

#### 20. Which functions are not available?
The following are not implemented:
- Sterility testing as a separate workflow.
- Annual Environmental Monitoring plans and schedules.
- Water sampling frequencies and sampling plans.
- Reference strain passage tracking (reference cryovial lots, storage, expiry and thaw events are tracked, but not passage numbers).
- Equipment qualification records.
- Incident, deviation or CAPA modules (Error Monitoring is a technical log only, see §40).

<!-- Review log (Sonnet): 18 corrections; invented items removed: routes, "MICRO"/"FP" codes, menu paths, token wording, "imported via structured report files" and instrument-specific names, retained-sample material claims, CoA export claim, "Under Testing" state, IQ/OQ/PQ, Skip Wait default holders; answers aligned with §18, §19, §33, §37, §39, §42; callouts added: GAP-012, GAP-100, GAP-053 -->
<!-- Phase 13 (agy): applied F-A01, F-C04 -->

<!-- Phase 13 review (Sonnet): agy corrections verified against RECONCILIATION and code; GAP ids exist; nothing invented; no change needed. -->

<!-- Phase 13 (Opus orchestrator): Return to Analyst scope aligned with §18.1 (F-B08). -->
