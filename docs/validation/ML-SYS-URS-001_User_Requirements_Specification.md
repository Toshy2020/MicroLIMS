# User Requirements Specification — MicroLIMS

| Field | Value |
|---|---|
| **Document No.** | ML-SYS-URS-001 |
| **Version** | 0.1 — Draft for review (Group A only) |
| **System** | MicroLIMS (Laboratory Information Management System) |
| **Related plan** | ML-SYS-VP-001 Validation Plan (deliverable 5) |
| **Companion document** | ML-SYS-FS-001 Functional Specification |
| **Software baseline** | Written from the source code at `main`, commit `cc492d3` (2026-11-02). Re-confirm against the frozen baseline before approval. |
| **GAMP 5 category** | Category 5 — custom application |
| **Regulatory basis** | EU GMP Annex 11, 21 CFR Part 11, GAMP 5 (2nd ed.), PIC/S PI 041, ALCOA+ |
| **Date** | 2026-11-02 |

> **Status of this draft.** Prepared with AI assistance from the source code. It carries no approval until reviewed and signed by the people named in section 10. Process Owners decide whether each requirement stands as written.

---

## 1. Purpose

This document states what MicroLIMS must do for the laboratory, in the laboratory's own terms. It is the top of the validation document set. The Risk Assessment (ML-SYS-RA-001), Functional Specification (ML-SYS-FS-001), OQ/PQ protocols and the Requirements Traceability Matrix (ML-SYS-RTM-001) all trace back to the requirement IDs defined here.

The system is already built. These requirements were therefore written **retrospectively**: each one describes a need the system is expected to meet, and each is marked with what the system does today (section 4). Where the system does not yet meet a need, the requirement is kept and marked as a gap rather than removed.

## 2. Scope and issue plan

The requirements follow the seven validation groups of ML-SYS-VP-001 section 2.1. They are issued group by group so each can be reviewed and approved on its own.

| Group | Subject | Issue status |
|---|---|---|
| **A** | Platform and security: sign-in, passwords, sessions, users, roles, e-signatures, audit trail, security events, archiving, error monitoring, system configuration, operations, access review | **Included in this issue (v0.1)** |
| B | Sample lifecycle | To follow |
| C | Microbiology testing | To follow |
| D | Physicochemical testing | To follow |
| E | Master data, inventory and laboratory organisation | To follow |
| F | Document Control | To follow (builds on the DC-URS baseline, Releases 1a to 1d) |
| G | Supporting functions: dashboards, KPIs, messages, notifications, reporting | To follow |

**Out of scope** (as in ML-SYS-VP-001 section 2.2): instrument data-acquisition software, ERP/SAP release, local development and test databases.

**Hosting.** This document is hosting-neutral. It states what the system must achieve (for example protected transport, backup and restore) and leaves the choice of platform to the Installation Qualification.

## 3. Users and roles

| Built-in role | Typical user | Main use of the system |
|---|---|---|
| **System Administrator** | IT / LIMS administrator | Accounts, roles, system settings, audit and error review, access review. Cannot execute or approve laboratory work (URS-A-038, URS-A-039). |
| **Section Head** | Laboratory head | Assigns and supervises work, approves results, laboratory dashboards |
| **Reviewer** | QC reviewer | Reviews completed tests and samples, returns work |
| **Analyst** | Laboratory analyst | Receives and prepares samples, executes tests, records results |

Administrators can define further roles. Every role is a set of named permissions (65 at this baseline, see FS-A-040). Users belong to one or more laboratory sections, which limits the records they can act on (URS-A-041).

## 4. How to read the requirements

**ID.** `URS-A-nnn`: group letter and a sequence number. IDs are never reused. A withdrawn requirement keeps its ID and is marked *Withdrawn*.

**Priority** (what happens if the need is not met):

| Priority | Meaning |
|---|---|
| **Must** | Needed for GxP compliance, data integrity or safe operation. A gap blocks release for GMP use unless QA approves a documented mitigation. |
| **Should** | Expected good practice or a significant control. A gap needs a documented risk acceptance. |
| **Could** | Useful. A gap does not affect compliance. |

**As-built status** (what the system does today, from the source code at the baseline above):

| Status | Meaning |
|---|---|
| **Implemented** | The system provides it. FS-A references describe how. |
| **Partial** | Provided in part. The limitation is stated in the Note column. |
| **Not implemented** | The need is real but the system does not meet it. Listed again in section 8. |
| **Deployment control** | Met by the installation or procedure rather than by the software. Verified at IQ or by SOP. |

**Basis** gives the regulatory or good-practice reason. It is a pointer for reviewers, not a legal opinion.

---

## 5. Group A — Platform and security requirements

### A1. Sign-in

| ID | Requirement | Priority | Basis | Status | Note |
|---|---|---|---|---|---|
| URS-A-001 | Only registered, active users shall be able to use the system. Each user shall sign in with a unique username and a password. | Must | Part 11 §11.10(d), §11.100(a), §11.300(a); Annex 11 §12.1 | Implemented | Username uniqueness is checked in application code. No database unique index was found (O3). |
| URS-A-002 | A failed sign-in shall give the same generic message whatever the cause (unknown user, wrong password, locked or disabled account) and shall take a similar time to answer, so that account names and states cannot be discovered. | Must | Part 11 §11.300(d); Annex 11 §12.1 | Implemented | |
| URS-A-003 | After a configurable number of failed sign-ins (default 5) an account shall lock for a configurable time (default 15 minutes), and the sessions it already has shall end. | Must | Part 11 §11.300(d); Annex 11 §12.1 | Implemented | The lock ends by itself when the time passes. |
| URS-A-004 | An administrator shall be able to unlock a locked account before the lock time ends. | Must | Annex 11 §12.3 | Implemented | A reason is required (URS-A-033). |
| URS-A-005 | Every sign-in attempt shall be recorded, successful or not and including unknown usernames, with time, username, outcome, failure reason and source address. | Must | Part 11 §11.10(e), §11.300(d); Annex 11 §12.4 | Implemented | |
| URS-A-006 | Sign-in, token refresh and password-change requests shall be rate limited per source, so password guessing is slowed. | Should | Part 11 §11.300(d) | Implemented | |
| URS-A-007 | A user whose password must be changed (new account, administrator reset, forced change) shall not be able to use the system until it is changed, except to change the password, read their own identity and sign out. The server shall enforce this, not only the screen. | Must | Part 11 §11.300(b); Annex 11 §12.1 | Implemented | |
| URS-A-008 | The first administrator account shall be created at first start from a secret supplied at deployment, and shall be forced to change that password at first sign-in. | Must | Annex 11 §12.1 | Implemented | |

### A2. Passwords

| ID | Requirement | Priority | Basis | Status | Note |
|---|---|---|---|---|---|
| URS-A-009 | Passwords shall be stored only as one-way salted hashes, never readable. | Must | Part 11 §11.300(d); Annex 11 §12.1 | Implemented | |
| URS-A-010 | A new password shall meet a minimum length (configurable, default 8) and contain an uppercase letter, a lowercase letter, a digit and a special character. | Must | Part 11 §11.300(b) | Implemented | A change to the minimum length applies to new passwords only. |
| URS-A-011 | A user shall not be able to reuse their most recent passwords (configurable count, default 5). | Must | Part 11 §11.300(b) | Implemented | |
| URS-A-012 | A signed-in user shall be able to change their own password by giving the current one. | Must | Part 11 §11.300(b) | Implemented | |
| URS-A-013 | A user who has forgotten their password shall be able to request a reset. The reset shall use a single-use, time-limited token sent by email (default 60 minutes), and the answer shall not reveal whether the account exists. | Must | Part 11 §11.300(c) | Implemented | Needs a mail server and an email address on the account. |
| URS-A-014 | When an email reset is not possible, an administrator shall be able to start a recovery that gives the user a one-time code to set a new password. The administrator shall give a reason. | Should | Part 11 §11.300(c) | Implemented | |
| URS-A-015 | An administrator shall be able to force a user to change their password at the next sign-in. | Must | Part 11 §11.300(b), (c) | Implemented | |
| URS-A-016 | Changing or resetting a password shall end the user's existing sessions and make access tokens issued earlier unusable. | Must | Part 11 §11.300(c) | Implemented | |
| URS-A-017 | Passwords shall expire after a period set by site policy, and the user shall be asked to change them. | Should | Part 11 §11.300(b) | Not implemented | No expiry exists. The Process Owner and QA must decide whether site policy requires it (O1). |

### A3. Sessions

| ID | Requirement | Priority | Basis | Status | Note |
|---|---|---|---|---|---|
| URS-A-018 | Access to the application shall use short-lived access tokens (default 15 minutes), so that a stolen token stops working quickly. | Must | Part 11 §11.10(d) | Implemented | Set in the deployment configuration, not in the application (range 1 to 480 minutes). |
| URS-A-019 | A session shall be renewed with a single-use refresh token, replaced at every renewal, stored only as a hash, and valid for a configurable time (default 7 days, 1 to 30). | Must | Part 11 §11.10(d) | Implemented | Reuse of an already-used token is refused but does not end the session chain (O4). |
| URS-A-020 | A session shall stop working at the user's next request when the account is disabled or locked, the user is moved to another role, the permissions of the user's role change, or the password changes. | Must | Part 11 §11.10(d), (g); Annex 11 §12.3 | Implemented | |
| URS-A-021 | An administrator shall be able to see a user's active sessions (start, source address, device, last use, expiry) and end them, giving a reason. | Should | Annex 11 §12.1 | Implemented | |
| URS-A-022 | Signing out shall end the session. | Must | Part 11 §11.10(d) | Implemented | |
| URS-A-023 | Expired and ended session tokens and used reset tokens shall be removed on a schedule, with a configurable retention (default 30 days). | Should | Annex 11 §12.1 | Implemented | |
| URS-A-024 | A session that has been idle for a period set by site policy shall be locked or ended. | Should | PIC/S PI 041 (access control); site policy | Not implemented | A session lasts until its refresh token expires (O2). |

### A4. User account management

| ID | Requirement | Priority | Basis | Status | Note |
|---|---|---|---|---|---|
| URS-A-025 | An administrator shall be able to create an account with username, full name, email, job title, role and an initial password. The user shall be forced to change the password at first sign-in. | Must | Annex 11 §12.3; Part 11 §11.10(d) | Implemented | |
| URS-A-026 | An administrator shall be able to edit a user's name, job title and email. | Must | Annex 11 §12.3 | Implemented | A username can also be changed (O3). |
| URS-A-027 | Each user shall hold one role. Changing it shall need a reason and the administrator's electronic signature, and an administrator shall not be able to change their own role. | Must | Part 11 §11.10(g); Annex 11 §12.3 | Implemented | |
| URS-A-028 | An administrator shall be able to disable and re-enable accounts. Disabling shall need a reason and end the user's sessions. An administrator shall not be able to disable their own account. | Must | Annex 11 §12.3 | Implemented | |
| URS-A-029 | Accounts shall never be deleted. A disabled account shall stay so the person who made, signed or approved a record stays identifiable for the whole retention period. | Must | Part 11 §11.10(e); ALCOA+ (Attributable) | Implemented | User hard delete was removed. |
| URS-A-030 | The user list shall show active users by default and shall offer a list of inactive users. | Should | Annex 11 §12.3 | Implemented | |
| URS-A-031 | At every moment at least one active user shall hold the permission to manage users and at least one shall hold the permission to manage roles. A change that would remove the last holder shall be refused. | Must | Annex 11 §16 | Implemented | |
| URS-A-032 | An administrator shall be able to assign a user to laboratory sections. Sections shall limit which laboratory records the user can act on (URS-A-041). | Must | Part 11 §11.10(d), (g) | Implemented | |
| URS-A-033 | Every administrative action on users and roles shall need a reason, and the reason shall be stored in the audit trail with the action. | Must | Annex 11 §9, §12.3 | Implemented | |
| URS-A-034 | Disabled users shall not be offered as assignees, roster members or notification recipients. Their history shall stay attributed to them. | Should | Annex 11 §12.3 | Implemented | Document responsibilities are covered in Group F. |

### A5. Roles, permissions and segregation of duties

| ID | Requirement | Priority | Basis | Status | Note |
|---|---|---|---|---|---|
| URS-A-035 | Every function shall be protected by a named permission, and users shall receive permissions only through their role. A request without the permission shall be refused. A function with no declared permission shall be refused by default. | Must | Part 11 §11.10(d), (g); Annex 11 §12.1 | Implemented | |
| URS-A-036 | An administrator shall be able to create roles, rename them, change their permissions and delete roles that are custom and unassigned. Built-in roles shall not be deletable. | Must | Annex 11 §12.3 | Implemented | |
| URS-A-037 | Changing a role's permissions, and deleting a role, shall need a reason and the administrator's electronic signature. | Must | Part 11 §11.10(g); Annex 11 §12.3 | Implemented | |
| URS-A-038 | No role shall hold both an administration permission (manage users, manage roles) and a permission that executes, reviews or approves GxP work. The system shall refuse the combination, name the conflicting pair, and show the conflict on the Roles screen. | Must | Part 11 §11.10(d), (g); Annex 11 §12.1; PIC/S PI 041 | Implemented | |
| URS-A-039 | The built-in System Administrator role shall hold only administration, configuration and read-only permissions. It shall not be able to execute, review, approve, correct or release laboratory work. | Must | Part 11 §11.10(d), (g); ALCOA+ | Implemented | |
| URS-A-040 | A user shall not be able to change the permissions of a role they hold themselves. | Must | Part 11 §11.10(g) | Implemented | |
| URS-A-041 | A user shall be able to act on laboratory records only in the sections they belong to. An administrator shall be able to read records in all sections and to configure master data in all sections, without gaining the ability to act on laboratory records. | Must | Part 11 §11.10(d), (g) | Implemented | |
| URS-A-042 | The Roles screen shall show each permission with its description and whether the system enforces it, so an administrator knows what a grant really allows. | Should | Annex 11 §12.3 | Implemented | |

### A6. Electronic signatures

| ID | Requirement | Priority | Basis | Status | Note |
|---|---|---|---|---|---|
| URS-A-043 | Each electronic signature shall need the signer to enter their own password at the moment of signing. | Must | Part 11 §11.200(a); Annex 11 §14 | Implemented | |
| URS-A-044 | Each signature shall record the signer's full name, username, role (type and name at the time of signing), the meaning of the signature, the date and time, the record signed, an optional comment and the source address. | Must | Part 11 §11.50, §11.70; Annex 11 §14 | Implemented | |
| URS-A-045 | The meaning of a signature (for example reviewed, approved, role changed) shall be chosen from a controlled list. | Must | Part 11 §11.50(a)(3) | Implemented | |
| URS-A-046 | A signature shall not be changeable or deletable by anyone, including database administrators using the application. | Must | Part 11 §11.70 | Implemented | Enforced by database triggers. |
| URS-A-047 | A signature shall be saved together with the action it authorises. If one fails, neither shall be saved. | Must | Part 11 §11.70 | Implemented | |
| URS-A-048 | A failed signature attempt shall be recorded. After a configurable number of failures in a configurable period (default 5 in 15 minutes) further attempts shall be refused without checking the password. The signer's login account shall not be locked by this. The failure message shall not say why it failed. | Must | Part 11 §11.300(d) | Implemented | |
| URS-A-049 | Authorised users shall be able to see the signature trail of a record, and a list of signatures. | Should | Part 11 §11.10(b) | Implemented | |
| URS-A-050 | A signature shall be bound to the content of the record it signs, so a later change to the record can be detected. | Should | Part 11 §11.70 | Not implemented | A signature is linked to its record by type and identifier only (O5). |

### A7. Audit trail

| ID | Requirement | Priority | Basis | Status | Note |
|---|---|---|---|---|---|
| URS-A-051 | The system shall keep an automatic, computer-generated, time-stamped audit trail of every creation, change and deletion of a GxP record, with the previous and the new value. | Must | Part 11 §11.10(e); Annex 11 §9 | Implemented | |
| URS-A-052 | Each audit entry shall identify who acted: a named user, a named system process, or a named instrument agent. | Must | Part 11 §11.10(e); Annex 11 §12.4; ALCOA+ (Attributable) | Implemented | |
| URS-A-053 | No user, including an administrator, shall be able to change or delete an audit entry. The protection shall hold at database level, not only in the application. | Must | Part 11 §11.10(e); Annex 11 §9 | Implemented | Update, delete and truncate are blocked by database triggers. The database owner can still disable triggers (O6). |
| URS-A-054 | Passwords, token hashes and other secrets shall never be written to the audit trail. | Must | Part 11 §11.300(d) | Implemented | Entries written before this control hold password hashes (deviation DEV-2026-AUD-001, O7). |
| URS-A-055 | The audit trail shall leave out only operational data that is not a GxP record (for example error logs, session tokens, job-run logs, backup status). | Must | Annex 11 §9 | Implemented | |
| URS-A-056 | Where an action needs a reason, the reason shall be stored with the audit entry. | Must | Annex 11 §9 | Implemented | |
| URS-A-057 | Authorised users shall be able to search the audit trail by date range, record type, action, user and the reference keys of laboratory records (sample, batch, control, media lot, strain, cryovial, test order). | Must | Annex 11 §9; Part 11 §11.10(b) | Implemented | |
| URS-A-058 | Search results shall be paged and the number returned per request shall be limited, so a search cannot exhaust the system. | Should | Annex 11 §9 | Implemented | |
| URS-A-059 | Authorised users shall be able to export audit results to a spreadsheet file. The export shall be limited in size, shall be recorded itself, and shall not allow spreadsheet formula injection. | Should | Part 11 §11.10(b) | Implemented | |
| URS-A-060 | From an audit entry, users shall be able to follow the related records (traceability). | Should | Annex 11 §9 | Implemented | |
| URS-A-061 | Historical audit entries with an unresolved record identifier shall be identifiable, and any correction shall be added as a new entry, never by editing the old one. | Should | ALCOA+ (Accurate, Original) | Implemented | The detection screen and an append-only correction script exist. The script has not been run on a production database (O8). |
| URS-A-062 | Only users holding the audit-view permission shall be able to read the audit trail. | Must | Part 11 §11.10(d) | Implemented | |
| URS-A-063 | Time stamps shall be stored in UTC and shown in the laboratory time zone. | Must | Annex 11 §12.4; ALCOA+ (Contemporaneous) | Implemented | |
| URS-A-064 | When two users edit the same record, the second save shall not silently overwrite the first. The user shall be told the record has changed. | Must | ALCOA+ (Accurate) | Implemented | |

### A8. Security event trail

| ID | Requirement | Priority | Basis | Status | Note |
|---|---|---|---|---|---|
| URS-A-065 | The system shall record security events in a separate trail: sign-in success and failure, lockout and unlock, token issue, refresh, rejection and revocation, sign-out, password change and reset, account disable and enable, role change, manual job runs, test alerts and emails, storage probes, maintenance-mode changes and instrument-agent authentication failures. | Must | Part 11 §11.10(e); Annex 11 §12.4 | Implemented | 26 event codes (FS-A-060). |
| URS-A-066 | Each event shall record the time, the actor (or that the system acted), the target user, the outcome and a reason where one applies. | Must | Part 11 §11.10(e) | Implemented | |
| URS-A-067 | Security events shall not be changeable or deletable. | Must | Part 11 §11.10(e) | Implemented | Database triggers block update, delete and truncate. |
| URS-A-068 | Authorised users shall be able to view the security events and the login history. The permission shall be separate from the permission to view the audit trail. | Must | Part 11 §11.10(d) | Implemented | |

### A9. Record archiving

| ID | Requirement | Priority | Basis | Status | Note |
|---|---|---|---|---|---|
| URS-A-069 | When the final decision on a record is signed, the system shall keep a PDF of the record as issued, so the released version can be reproduced exactly even if the underlying data is later amended. | Must | Annex 11 §17; Part 11 §11.10(c); ALCOA+ (Original) | Implemented | Which records are archived is stated in Groups B and C. |
| URS-A-070 | Each archived record shall carry a SHA-256 fingerprint, the person who generated it, the date and the reason. Archived records shall not be changed or deleted. | Must | Part 11 §11.10(c), (e) | Partial | No endpoint changes or deletes an archive, but unlike the audit trail no database trigger enforces it (O9). |
| URS-A-071 | The fingerprint shall be checked every time an archived record is downloaded, and a mismatch shall be shown to the user. | Must | Part 11 §11.10(c) | Implemented | |
| URS-A-072 | A failure to produce the archive shall not undo a decision that has already been signed. The failure shall be logged and the archive shall be re-creatable. | Should | Annex 11 §17 | Implemented | |

### A10. Error monitoring and incidents

| ID | Requirement | Priority | Basis | Status | Note |
|---|---|---|---|---|---|
| URS-A-073 | The system shall capture unhandled server errors and crash reports from the user's browser, each with a correlation identifier. | Must | Annex 11 §13 | Implemented | |
| URS-A-074 | Captured errors shall be grouped into incidents with a severity. Authorised users shall be able to list, filter and open them. | Must | Annex 11 §13 | Implemented | |
| URS-A-075 | Authorised users shall be able to resolve and reopen an incident and override its severity, and the change shall be recorded. | Should | Annex 11 §13 | Implemented | |
| URS-A-076 | Authorised users shall be able to export incidents to a spreadsheet file. | Could | Annex 11 §13 | Implemented | |
| URS-A-077 | The system shall email critical incidents to configured recipients (up to 20), and an administrator shall be able to send a test alert. | Should | Annex 11 §13, §16 | Implemented | Switched off by default and enabled in the deployment configuration. |
| URS-A-078 | Error logs shall be kept for a configurable period (default 180 days for low severity, 365 for high) and then removed. This shall never apply to GxP records. | Should | Annex 11 §13 | Implemented | |
| URS-A-079 | The system shall watch the database for slow and blocked queries and report them as incidents. | Could | Annex 11 §13 | Implemented | |

### A11. System configuration

| ID | Requirement | Priority | Basis | Status | Note |
|---|---|---|---|---|---|
| URS-A-080 | Security, retention, monitoring, alerting, maintenance and access-review settings shall be held in one registry, editable without a new deployment, each with a type, an allowed range and a default. | Must | Annex 11 §10 | Implemented | 22 settings at this baseline. |
| URS-A-081 | Every change to a setting shall need a reason. A change to a GxP-relevant setting shall also need the administrator's electronic signature. | Must | Annex 11 §10; Part 11 §11.10(k) | Implemented | |
| URS-A-082 | Every change shall be kept in a history (previous value, new value, who, when, why) that cannot be changed or deleted. | Must | Annex 11 §10; Part 11 §11.10(k) | Implemented | |
| URS-A-083 | Secrets (token signing key, mail and storage credentials, database connection) shall not be editable in the application and shall be shown only masked. | Must | Annex 11 §12.1 | Implemented | |
| URS-A-084 | The laboratory time zone shall be set at deployment and shall drive due dates and incubation times. | Must | ALCOA+ (Contemporaneous) | Implemented | Configuration value, not in the registry. |

### A12. System operations

| ID | Requirement | Priority | Basis | Status | Note |
|---|---|---|---|---|---|
| URS-A-085 | Every run of a background job shall be recorded with start, end, outcome, error and number of items processed, and shown to administrators. | Should | Annex 11 §13 | Implemented | |
| URS-A-086 | An administrator shall be able to run housekeeping jobs by hand, with a reason. Jobs that change GxP state shall not be runnable by hand. Each manual run shall be recorded as a security event. | Should | Annex 11 §10, §12 | Implemented | |
| URS-A-087 | Administrators shall be able to see the database size, the largest tables and the recent backup runs. | Should | Annex 11 §7.2, §16 | Implemented | Backup status is written by the backup script. |
| URS-A-088 | The database and the stored files shall be backed up regularly, and restoring from the backup shall be tested. | Must | Annex 11 §7.2, §16; Part 11 §11.10(c) | Partial | Backup and restore scripts and a status record exist. A restore test on a new deployment is part of IQ/OQ (VP risk R3). |
| URS-A-089 | Administrators shall be able to see the state of integrations (instrument agents, mail server, file storage) and to test them. Each test shall need a reason and be limited in rate and recorded. | Should | Annex 11 §12 | Implemented | |
| URS-A-090 | An administrator shall be able to put the system into maintenance mode with an electronic signature. While it is on, every change made through the application shall be refused with a message, reading shall stay possible, and sign-in, sign-out and the administration area shall stay available. Background jobs that change GxP state shall pause. | Should | Annex 11 §10, §16 | Implemented | |
| URS-A-091 | An administrator shall be able to show an announcement banner on every page. A public status endpoint shall report maintenance mode and the banner. | Could | Annex 11 §16 | Implemented | |
| URS-A-092 | The system shall offer liveness and readiness checks that report whether the application and the database schema are available. | Should | Annex 11 §16 | Implemented | |

### A13. Periodic access review

| ID | Requirement | Priority | Basis | Status | Note |
|---|---|---|---|---|---|
| URS-A-093 | An authorised reviewer shall be able to generate a snapshot of every user: role, effective permissions, section memberships, last sign-in and 12 months of status changes. | Must | Annex 11 §12.3 | Implemented | |
| URS-A-094 | The snapshot shall flag users who hold a segregation-of-duties conflict, users who have not signed in for a long time (dormant) and accounts that never signed in. | Should | Annex 11 §12.3 | Implemented | |
| URS-A-095 | The reviewer shall be able to record findings while the review is in draft. | Should | Annex 11 §12.3 | Implemented | |
| URS-A-096 | The reviewer shall sign the review with an electronic signature. The reviewer shall not be able to sign a review that concerns their own access, and the snapshot shall be verified unchanged at the moment of signing. | Must | Part 11 §11.200; Annex 11 §12.3 | Implemented | |
| URS-A-097 | A signed review shall not be changeable or deletable. Exporting it shall be recorded. | Must | Part 11 §11.10(e) | Implemented | |
| URS-A-098 | The number of days without sign-in after which a user counts as dormant shall be configurable (default 90). | Could | Annex 11 §12.3 | Implemented | |

### A14. Platform protection

| ID | Requirement | Priority | Basis | Status | Note |
|---|---|---|---|---|---|
| URS-A-099 | All communication between the user's browser and the server shall be protected in transit. | Must | Part 11 §11.10(c), §11.30; Annex 11 §12.1 | Deployment control | Provided by the hosting set-up. Verified at IQ. |
| URS-A-100 | The server shall send protective response headers that prevent content sniffing, framing and referrer leakage. | Should | Annex 11 §12.1 | Implemented | |
| URS-A-101 | The server shall accept browser requests only from the configured front-end address. | Should | Annex 11 §12.1 | Implemented | |
| URS-A-102 | The application shall refuse to start without a strong token-signing key, and shall refuse a known placeholder key outside development. | Must | Annex 11 §12.1 | Implemented | |

---

## 6. Groups B to G

Requirements for groups B to G follow in later issues of this document, in the same format, continuing the numbering within each group (`URS-B-001`, `URS-C-001` and so on). The scope of each group is listed in section 2.

## 7. Assumptions and dependencies

1. A mail server is available for password resets and critical alerts. Without one, URS-A-013 and URS-A-077 cannot be met, and administrators use URS-A-014 instead.
2. The server clock is kept accurate by the host (time synchronisation). The application stores UTC and relies on the host clock.
3. The database is PostgreSQL and the application connects with a role that cannot disable triggers or truncate audit tables in production (runbook `docs/operations/database-roles-runbook.md`). This is a deployment control for URS-A-053.
4. Instrument software, and the instruments themselves, are validated separately.

## 8. Known gaps found during the retrospective

These requirements are real needs that the system does not yet meet. Each needs a decision from the Process Owner and QA: build it, or accept the risk with a documented mitigation.

| Req | Gap | Suggested decision |
|---|---|---|
| URS-A-017 | No password expiry | Decide whether site policy requires periodic change. Part 11 §11.300(b) expects periodic checking or revision of identification codes and passwords. |
| URS-A-024 | No idle-session lock | Decide the idle period, or mitigate with a workstation lock policy in the user-access SOP. |
| URS-A-050 | Signatures not bound to record content | Accept with the audit trail and record locks as mitigation, or add a content fingerprint to signatures. |
| URS-A-070 | Archived records are protected by application convention only, with no database trigger | Add a trigger like the audit tables, or accept with the download integrity check as mitigation. |
| URS-A-088 | Restore never tested end to end | Execute the restore test at IQ/OQ. |

Further observations that are not requirement gaps are listed in ML-SYS-FS-001 Annex 1 (O1 to O9).

## 9. Revision history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-02 | AI-assisted draft for the Validation Lead | First issue: front matter and Group A (102 requirements). |

## 10. Approvals

| Role | Name | Signature | Date |
|---|---|---|---|
| Author (Validation Lead) | | | |
| System Owner | | | |
| Process Owner — Microbiology | | | |
| Process Owner — Physicochemical | | | |
| Quality Assurance | | | |
