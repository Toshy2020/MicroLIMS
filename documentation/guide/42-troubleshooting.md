## 42. Troubleshooting

This section explains messages and blocks that users meet in MicroLIMS and what to do about them. Messages are quoted as the application shows them; text in braces such as {n} is replaced by a value.

### 42.1 Sign-In, Lockout and Password Messages

#### "Invalid username or password."
- **What it means:** The username or password does not match, or the account is locked or disabled. The message is the same in all these cases and does not say which one applies.
- **What to do:** Re-type your username and password carefully. After 5 wrong attempts the account is locked for 15 minutes. If the account is locked or disabled, ask a System Administrator to unlock or enable it. If you have forgotten your password, ask an administrator for an administrator-assisted recovery code.

> **Note for reviewers:** "Forgot password?" cannot be completed in the application; ask an administrator for an admin-assisted recovery code. The on-screen text mentions a reset link, but none is sent. (GAP-100)

> **Note for reviewers:** After a lock ends the failed-attempt counter is not reset, so one further wrong password locks the account again. (GAP-012)

#### "Laboratory authentication service error. Please try again later."
- **What it means:** The sign-in could not be processed because of an unexpected problem.
- **What to do:** Wait a moment and try again. If the message stays, tell your System Administrator.

#### "Unable to reach the laboratory server. Check network connection."
- **What it means:** Your browser cannot reach the MicroLIMS server.
- **What to do:** Check your network connection and refresh the sign-in page.

#### "Login failed. Please check credentials."
- **What it means:** The sign-in was rejected without a more specific reason.
- **What to do:** Check your network connection and your credentials, then try again.

#### "Current password is incorrect."
- **What it means:** The **Current Password** you typed on the **Change Password** screen does not match your password.
- **What to do:** Type your existing password correctly.

#### "New password does not meet the policy requirements below."
- **What it means:** The new password breaks one or more password rules. The system can report:
  - "Password must be at least 8 characters long."
  - "Password must contain at least one uppercase letter."
  - "Password must contain at least one lowercase letter."
  - "Password must contain at least one digit."
  - "Password must contain at least one special (non-alphanumeric) character."
- **What to do:** Choose a password that meets all the rules.

#### "New password and confirmation do not match." / "New password and confirm password do not match."
- **What it means:** **New Password** and **Confirm New Password** are different.
- **What to do:** Type the same password in both fields.

#### "New password must not match any of your last 5 passwords."
- **What it means:** The system keeps your last 5 passwords and does not allow reuse.
- **What to do:** Choose a password you have not used among your last five.

#### "Recovery code has expired. Please request a new recovery code."
- **What it means:** The one-time recovery code is valid for 15 minutes and that time has passed.
- **What to do:** Ask a System Administrator for a new code and use it within 15 minutes.

#### "Invalid recovery code."
- **What it means:** The code does not match the active code for your account.
- **What to do:** Check the code your administrator gave you (format XXXX-XXXX-XXXX) and type it again.

#### "Maximum failed recovery attempts exceeded. Recovery code invalidated."
- **What it means:** The code was entered wrongly 5 times and is no longer valid.
- **What to do:** Ask your System Administrator for a new recovery code.

#### "Cannot initiate password recovery for a disabled user account. Please enable the account first."
- **What it means:** An administrator tried to generate a recovery code for a disabled account.
- **What to do:** Enable the account on the **Users** screen first, then generate the code.

#### "System Administrators cannot initiate admin-assisted password recovery for their own account."
- **What it means:** An administrator cannot generate a recovery code for their own account.
- **What to do:** Another administrator must do it.

---

### 42.2 Inactivity and Page Redirects

#### "You will be signed out due to inactivity in {n} second(s)."
- **What it means:** The browser saw no activity for 20 minutes and shows a 60-second countdown before signing you out.
- **What to do:** Click **Stay signed in** or use the page before the countdown ends. If it reaches zero you are signed out and unsaved entries are lost.

> **Note for reviewers:** Idle sign-out happens in the browser only; the server does not time out inactive sessions. (GAP-053)

#### Page opens the Dashboard instead of the page you asked for
- **What it means:** You opened a page (for example Users, Roles or Error Monitoring) that your role does not have permission for. The application sends you to the Dashboard without a message.
- **What to do:** Ask a System Administrator to check your role. If your permissions were just changed, they can take up to 15 minutes to reach you; signing out and in again also works.

---

### 42.3 Permission and Laboratory Scope Messages

#### "Your account is not assigned to any laboratory section. Ask an administrator to assign one."
- **What it means:** Your account has no laboratory section membership, so you see no laboratory data.
- **What to do:** Ask a System Administrator to open **Users**, choose **Laboratory Sections** on your row and assign your sections (see §33).

#### "You are not a member of this laboratory."
- **What it means:** You tried to open a laboratory dashboard or workspace for a laboratory you do not belong to.
- **What to do:** Select the laboratory you are assigned to, or ask an administrator to update your memberships.

#### "This test belongs to a laboratory section you are not assigned to."
- **What it means:** You tried to change a test definition that belongs to a laboratory section outside your memberships.
- **What to do:** Ask an administrator to update your laboratory memberships, or have someone from the owning section make the change.

#### "You can receive samples only for your own laboratory."
- **What it means:** You hold the *Own-lab sample receiving* permission rather than the global *Receive samples* permission, and you selected a laboratory where you are not a member.
- **What to do:** In the receiving dialog select only your own laboratories. Receiving for all laboratories needs the *Receive samples* permission (by default: Section Head, System Administrator).

#### "You do not have permission to query the global audit trail."
- **What it means:** This message belongs to the Document Control audit trail (see §38), not to the Audit Trail screen of §35. Your account is not allowed to search the Document Control audit trail.
- **What to do:** Ask a System Administrator to confirm whether you should have access.

---

### 42.4 Segregation-of-Duties Messages

MicroLIMS does not let the same person perform and then review or approve. System Administrators are not exempt.

#### "You cannot review a sample you tested."
- **What it means:** You performed testing on this sample (assigned analyst, sample preparer, entered a result or reading, or recorded a workflow or pathogen step), so you cannot review it.
- **What to do:** Another user with the review permission, who did not test the sample, must review it.

#### "You cannot approve a sample you reviewed."
- **What it means:** You performed the review of this sample.
- **What to do:** A different user with the approval permission must approve.

#### "You cannot approve a sample you tested."
- **What it means:** You performed testing on this sample.
- **What to do:** A different user with the approval permission, who neither tested nor reviewed it, must approve.

#### "A reviewer cannot decide on a result they performed."
- **What it means:** You performed this pathogen test and cannot make the biochemical review decision on it.
- **What to do:** Another authorised user makes the decision.

#### "You cannot approve a media lot you prepared or evaluated."
- **What it means:** You prepared the lot or read a challenge of its growth promotion evaluation.
- **What to do:** Another authorised user must approve the lot on the **Media Preparation & Evaluation** page.

#### "You cannot approve a cryovial batch you prepared."
- **What it means:** You prepared this cryovial batch.
- **What to do:** Another authorised user must approve it.

#### "The person who loaded the workbook cannot verify it. Ask a second person."
- **What it means:** The user who loaded the Master Data Workbook cannot also verify it.
- **What to do:** A second authorised user verifies the workbook (see §32).

#### "The analyst who submitted the qualification cannot review it." / "The analyst who submitted the qualification cannot approve it." / "The reviewer cannot also approve the qualification." / "The reviewer cannot also reject the qualification at approval."
- **What it means:** For a working standard qualification, the submitter, the reviewer and the approver must be different people.
- **What to do:** Let different authorised users submit, review and approve (see §28).

---

### 42.5 Electronic Signature Messages

#### "Password verification failed. The signature was not applied."
- **What it means:** The password typed in the **Electronic Signature** dialog was not accepted (wrong password, or the account is inactive or locked).
- **What to do:** Type your own sign-in password again. The action is not carried out until the signature succeeds. The failed attempt is written to the audit trail without the password.

#### "Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied."
- **What it means:** 5 wrong passwords within 15 minutes were entered in signature dialogs.
- **What to do:** Wait 15 minutes. Failed signatures do not lock your sign-in account. If you have forgotten your password, contact your administrator.

#### "Password re-authentication is required to apply an electronic signature for approval (21 CFR Part 11)."
- **What it means:** The password was left empty when signing a document approval in Document Control.
- **What to do:** Type your password in the signature window before clicking **Sign & Approve**.

> **Note for reviewers:** This message text names a regulation. It is quoted exactly as the system shows it; it is not a statement that the system meets that regulation. (GAP-023)

> **Note for reviewers:** The wording of this message refers to a regulation; it is text shown by the application and is not evidence that the system has been validated against it. (GAP-023)

---

### 42.6 Incubation Time Gates

#### "This incubation window needs at least {h} hours - not ready until {utc} UTC." / "Stage 1 incubation for step "{step}" requires at least {h} hours of incubation - not ready until {utc} UTC." / "Stage 2 incubation for step "{step}" requires at least {h} hours of incubation - not ready until {utc} UTC."
- **What it means:** The minimum incubation time set in the Test Master or media configuration has not passed, so the result cannot be entered yet.
- **What to do:** Wait until the stated UTC time. The test then changes to **Ready to Read**. A user with the *Supervise testing workflows* permission (by default: Section Head, System Administrator) can use **Skip Wait**; the screen then records that the minimum wait was skipped.

#### "Minimum incubation time not elapsed. Available from: {utc} UTC."
- **What it means:** In a pathogen test the minimum incubation time of the step has not passed.
- **What to do:** Wait until the stated UTC time before recording observations.

#### "Incubation is still in progress - earliest reading time is {yyyy-MM-dd HH:mm} UTC."
- **What it means:** In a growth promotion evaluation the challenged media has not finished its incubation.
- **What to do:** Come back to the evaluation after the stated reading time.

---

### 42.7 Media, Material and Equipment Availability

#### "Media lot "{lot}" is not released for use, out of stock, or rejected."
- **What it means:** The selected media lot has not been released, is out of stock or was rejected.
- **What to do:** Select a released lot. If none exists, prepare, evaluate and release a new lot.

#### "Media lot #{lot} expired on {date}." / "Cryovial batch {code} is expired and cannot be used." / "Material {name} (batch {batch}) is expired and cannot be used."
- **What it means:** The media lot, cryovial batch or material lot is past its expiry date and cannot be selected.
- **What to do:** Select an in-date lot and handle the expired item according to your laboratory procedures.

#### "A current Certificate of Analysis (COA) is required before {name} (batch {batch}) can be used. Please upload a valid COA in the Inventory module first."
- **What it means:** The material lot has no current Certificate of Analysis.
- **What to do:** Open **Materials Stock** in your laboratory menu, find the lot and upload the Certificate of Analysis.

#### "Insufficient stock: {name} (batch {batch}) has {remaining} {unit} remaining, {requested} requested."
- **What it means:** The quantity you entered is more than the lot has left.
- **What to do:** Correct the quantity, select another lot, or receive more stock in **Materials Stock**.

#### "Equipment {code} is not currently in service."
- **What it means:** The storage equipment you selected is not marked **In Service** in the Equipment Inventory (it is **Out of Service** or **Retired**).
- **What to do:** Select equipment that is in service, or ask an authorised user to correct its status in **Equipment Inventory**.

---

### 42.8 Missing Master Data

#### "Item '{name}' has no assigned tests. Configuration must be completed by the Section Head before samples can be received."
- **What it means:** The item chosen on the receiving screen has no tests assigned.
- **What to do:** An authorised user (by default Section Head, System Administrator) assigns the tests to the item (see §31).

#### "Item '{name}' is frozen and cannot be used to receive new samples."
- **What it means:** The item has been frozen so that no new samples can be received for it.
- **What to do:** If samples must be received, an authorised user unfreezes the item on the **Items** page.

#### "No Dilution Factor is configured for "{code}" on this item's Specifications. Configure it before recording a result."
- **What it means:** The item's specification for this test has no dilution factor.
- **What to do:** An authorised user sets the dilution factor in the item's specifications (see §31).

#### "No reporting Unit is configured for "{code}" on this item's Specifications. Configure it before recording a result."
- **What it means:** The item's specification for this test has no reporting unit.
- **What to do:** An authorised user sets the unit in the item's specifications.

#### "The incubation window for {medium} on step {step} is not configured - set its incubation hours and temperature in Test Master."
- **What it means:** The test step has no incubation hours or temperature for the chosen medium.
- **What to do:** An authorised user sets them in the **Test Master**.

#### "No Media Configuration exists yet for "{product}" - configure it in Laboratory Configuration before preparing a lot."
- **What it means:** The media product has no media configuration.
- **What to do:** An authorised user creates it under **Media Configurations** (see §22).

---

### 42.9 Old Bookmarks and Retired Features

Old addresses are redirected automatically:

| Old address | Goes to |
|---|---|
| Home address ("/") | The Dashboard |
| Receiving and testing | The first laboratory workspace you can use (Microbiology first, then the Physicochemical areas) |
| Testing workspace | Receiving and testing, which then redirects as above |
| Cause of testing (Laboratory Configuration) | Receiving Configuration |
| Approved media (Inventory) | The Approved tab of Media Preparation & Evaluation |
| Approved cryovials (Inventory) | The Approved tab of Reference Cryovials |
| Group pages | The matching section page |

#### Retired features
- The older stand-alone HPLC System Suitability screen and the older calibration-run path for ICP and AAS are retired. System suitability and calibration are done inside the HPLC and ICP runs.
- When a retired assay type is selected in Test Master the application shows "HPLC Assay, HPLC Multi-Analyte and Standard-Comparison are retired; use HPLC method assay." or "Elemental assay tests are retired; use an ICP method assay test."

<!-- Review log (Sonnet): 31 corrections; invented items removed: hash/"cryptographic" wording, laboratory code names, routes, wrong meaning of the global audit trail message (it is the Document Control audit trail), claim that failed signatures are not audited, "Maintenance" equipment status, cryovial identity-confirmation SoD cause, Skip Wait default holders corrected (Section Head, System Administrator), invented menu paths; callouts added: GAP-100, GAP-012, GAP-053, GAP-023. All quoted messages checked with git grep -F. -->
