# URS-SEC User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-SEC |
| Title | Authentication and session security user requirements |
| Version | 0.1 |
| Status | Draft |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |

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
