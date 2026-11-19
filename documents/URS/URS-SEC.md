# URS-SEC User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-SEC |
| Title | Authentication and session security user requirements |
| Version | 0.4 |
| Status | Draft |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |
| 0.2 | 2026-11-13 | AI coding agent, draft from code | Added Quality Assurance role independence, cross-section reading, and access review signing requirements (URS-SEC-013, URS-SEC-014). |
| 0.3 | 2026-11-13 | AI coding agent, draft from code | Added multi-factor authentication requirements (URS-SEC-015 to URS-SEC-022). |
| 0.4 | 2026-11-13 | AI coding agent, draft from code | Added company sign-in (single sign-on) requirements (URS-SEC-023 to URS-SEC-031). Some functions are planned and not yet built; see FS-SEC-035 to FS-SEC-037. |

## Requirements

Each requirement below is a need that the code as built serves. Gaps found in the code are not written as requirements here; they are recorded as conflicts (see `documents/_conflicts/CONF-task4.md`).

### URS-SEC-001
**Requirement:** The system shall let a user sign in with a user name and password, and shall give the same refusal message for an unknown user, a wrong password, a disabled account and a locked account.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.300(a); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-002
**Requirement:** The system shall lock a user account for a configured time after a configured number of failed sign-in attempts, shall end the sessions of the account when it locks, and shall let an administrator unlock it.
**GxP:** Yes
**Serves:** 21 CFR 11.300(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-003
**Requirement:** The system shall keep a user signed in with a short-lived access token and a renewable session token, shall replace the session token each time it is used, and shall end the session at sign-out.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-004
**Requirement:** The system shall refuse a request that carries an access token issued before the account was disabled or locked, before the user was moved to another role, before the role's permissions were changed, or before the password was changed.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(g); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-005
**Requirement:** The system shall require every new password to meet a configured minimum length and to contain an upper-case letter, a lower-case letter, a digit and a special character, and shall refuse a password that matches one of the user's recent passwords.
**GxP:** Yes
**Serves:** 21 CFR 11.300(b); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-006
**Requirement:** The system shall let a signed-in user change their own password only after entering the current password, shall end all their sessions when the password changes, and shall limit how often the change can be tried.
**GxP:** Yes
**Serves:** 21 CFR 11.300(b); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-007
**Requirement:** The system shall let a user who forgot the password reset it with a single-use token that expires after a configured time, and shall not show whether the user name exists.
**GxP:** Yes
**Serves:** 21 CFR 11.300(b), 11.300(c) (to be confirmed by QA)
**Status:** Active

### URS-SEC-008
**Requirement:** The system shall let an administrator issue a one-time recovery code for another user's account with a reason, shall not let an administrator issue one for their own account, shall let the code be used once within a short time and with a limited number of wrong tries, and shall record each step.
**GxP:** Yes
**Serves:** 21 CFR 11.300(c); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-009
**Requirement:** The system shall refuse every request from a user whose password is marked "must change", except changing the password, reading the user's own identity and signing out.
**GxP:** Yes
**Serves:** 21 CFR 11.300(b) (to be confirmed by QA)
**Status:** Active

### URS-SEC-010
**Requirement:** The system shall record sign-in, session, password and account events in a security audit trail that is separate from the laboratory audit trail, without storing passwords or tokens in it, and shall record each sign-in attempt in a sign-in history.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e), 11.300(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-011
**Requirement:** The system shall let an administrator see the active sessions of a user and end them all, with a reason, and shall record the action.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-012
**Requirement:** The system shall limit the number of requests per time window to the sign-in, token refresh, password-reset, recovery-confirm and password-change endpoints.
**GxP:** No
**Serves:** 21 CFR 11.300(d) (to be confirmed by QA)
**Status:** Active

### URS-SEC-013
**Requirement:** The system shall provide a built-in Quality Assurance system role (`QA`) that reads all laboratory sections without section membership, shall refuse every laboratory write requiring section membership, and shall prevent any Quality Assurance role (or any role holding `Audit.Review` or `Access.Review.Sign`) from holding administration permissions, system/master-data configuration permissions, or laboratory execution/approval permissions other than `Documents.Review` and `Documents.Approve`.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(g); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-014
**Requirement:** The system shall require `Access.Review.Sign` to record findings on or electronically sign a user access review whenever at least one active, unlocked user holds `Access.Review.Sign`, and shall allow an administrator holding `Access.Review` to decide and sign only while no active, unlocked `Access.Review.Sign` holder exists.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(g), 11.200(a); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-015
**Requirement:** The system shall support a second sign-in factor using 6-digit RFC 6238 time-based one-time passcodes (TOTP) for authenticator applications and assigned hardware tokens, shall tolerate limited clock drift, and shall reject replayed codes already accepted for the same or earlier time step.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.300(a); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-016
**Requirement:** The system shall encrypt stored TOTP secrets and hardware token seeds with authenticated encryption supporting key rotation, shall clear plaintext secrets from memory after use, and shall refuse to start when MFA is enabled or stored credentials exist without a configured encryption key.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.300(b) (to be confirmed by QA)
**Status:** Active

### URS-SEC-017
**Requirement:** The system shall provide configurable MFA policy (`Off`, `Optional`, `RequiredForRoles`, `RequiredForAll`), required role list, and enrolment grace period, and shall block switching MFA policy from non-required to required unless at least two active System Administrators have an active second factor.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.300(a); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-018
**Requirement:** The system shall issue a short-lived single-use challenge when MFA verification or forced enrolment is required at sign-in, shall allow users to enrol or remove an authenticator app from their profile when not required by role, and shall record whether a session was verified with MFA.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.300(a) (to be confirmed by QA)
**Status:** Active

### URS-SEC-019
**Requirement:** The system shall generate a set of single-use recovery codes when MFA enrolment is confirmed, shall store only their cryptographic hashes, shall allow an authenticated user to regenerate them using password and current TOTP code, and shall notify the user by e-mail when a recovery code is used.
**GxP:** Yes
**Serves:** 21 CFR 11.300(c); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-020
**Requirement:** The system shall allow a user to trust a browser for a configured number of hours after MFA verification, shall allow the user and administrators to revoke trusted computers, and shall automatically revoke all trusted computers on password change, password reset, factor removal, or administrator MFA reset.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.300(b) (to be confirmed by QA)
**Status:** Active

### URS-SEC-021
**Requirement:** The system shall allow an administrator holding `Users.Manage` to reset another user's MFA or assign a hardware token with a required documented reason, shall block self-reset and self-assignment, and shall record the action in both the laboratory audit trail and security audit trail.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(e), 11.300(c); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-022
**Requirement:** The system shall keep electronic signature verification unchanged (user ID and password only), shall mark MFA secret entities as audit-redacted, and shall count failed second-factor attempts toward challenge expiry and account lockout while recording security audit and sign-in history events.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(e), 11.200(a), 11.300(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-023
**Requirement:** The system shall let a System Administrator set one company sign-in mode for the installation (`Off`, `OpenIdConnect`, or `ActiveDirectory`), shall keep provider connection details and secrets in server configuration rather than in the database, and, when a company sign-in mode is on, shall sign users in through the company provider.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.300(a); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-024
**Requirement:** The system shall verify each electronic signature by user ID and a MicroLIMS signing password for users who sign in with a company account, shall require such a user to choose a signing password before using the application, and shall keep that signing password separate from the company password.
**GxP:** Yes
**Serves:** 21 CFR 11.100(a), 11.200(a) (to be confirmed by QA)
**Status:** Active

### URS-SEC-025
**Requirement:** While a company sign-in mode is on, the system shall refuse local sign-in for every user except at most three emergency administrator accounts, shall accept a sign-in to an emergency account only with a second factor, and shall record each emergency sign-in and notify System Administrators by e-mail.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.300(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-026
**Requirement:** The system shall refuse to switch a company sign-in mode on unless a connection test has passed for that mode, at least one emergency administrator account has a second factor, and the administrator making the change is linked to a company username. Switching the mode off shall always be allowed.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(g) (to be confirmed by QA)
**Status:** Active

### URS-SEC-027
**Requirement:** The system shall sign in a company account only when an administrator has linked it to an active MicroLIMS user, shall refuse any other company account without creating a user, and shall let an administrator link users one at a time or by importing a CSV file of up to 2000 rows, with a row-by-row preview and a reason for the import.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.300(a) (to be confirmed by QA)
**Status:** Active

### URS-SEC-028
**Requirement:** The system shall let an administrator clear a user's company link with a documented reason so that the user can be linked again, and shall not let a company account that was re-issued to another person sign in as the previous MicroLIMS user.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.300(a) (to be confirmed by QA)
**Status:** Active

### URS-SEC-029
**Requirement:** The system shall not store, log, or write to the audit trail any company password, and shall use a company password only to verify it with the company directory or provider at sign-in.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.300(b) (to be confirmed by QA)
**Status:** Active

### URS-SEC-030
**Requirement:** The system shall end the MicroLIMS session of a company sign-in user when the session cap set by the administrator is reached (default 12 hours, allowed range 1 to 24 hours). Under Active Directory sign-in it shall check each linked active account against the directory at least every 60 minutes, and shall deactivate the user and end its sessions when the directory account is disabled or missing; a failed directory lookup shall change nothing.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.300(d); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-SEC-031
**Requirement:** The system shall take each user's role, laboratory sections and permissions only from MicroLIMS configuration, and shall not read them from the company directory or map directory groups to roles or sections.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(g) (to be confirmed by QA)
**Status:** Active
