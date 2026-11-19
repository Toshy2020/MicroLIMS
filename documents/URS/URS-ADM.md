# URS-ADM User Requirements Specification

| Field | Value |
|---|---|
| Document number | URS-ADM |
| Title | System administration user requirements |
| Version | 0.5 |
| Status | Draft |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |
| 0.2 | 2026-11-13 | AI coding agent, draft from code | Added QA dashboard routing and controlled-document QA approver requirements (URS-ADM-011, URS-ADM-012). |
| 0.3 | 2026-11-14 | AI coding agent, draft from code | Added keyboard shortcuts reference, form initial focus, server-paged table sort rule, and last-choice defaults toggle (URS-ADM-013). |
| 0.4 | 2026-11-17 | AI coding agent, draft from code | Added About dialog and server version requirement (URS-ADM-014). |
| 0.5 | 2026-11-19 | AI coding agent, draft from code | Added interface language (English / Arabic) requirement with English-only records and signatures (URS-ADM-015). |

## Requirements

Each requirement below is a need that the code as built serves. Gaps found in the code are not written as requirements here; they are recorded as conflicts (see `documents/_conflicts/CONF-task4.md`).

### URS-ADM-001
**Requirement:** The system shall let a user holding the user-management permission create a user account with a full name, user name, role, initial password and a reason, shall refuse a duplicate user name or an initial password that breaks the password rules, and shall require the new user to change the password at first use.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.300(b); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-ADM-002
**Requirement:** The system shall let a user holding the user-management permission change a user's name, user name, email address and job title with a reason, and shall refuse the change when the user's record was saved by someone else after the screen was loaded and the client sent the version it loaded.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(e) (to be confirmed by QA)
**Status:** Active

### URS-ADM-003
**Requirement:** The system shall let a user holding the user-management permission change another user's role only with a reason and the administrator's own password as an electronic signature, shall refuse a change to the administrator's own role, and shall refuse a change that would leave no active user able to manage users or roles.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(g), 11.200(a); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-ADM-004
**Requirement:** The system shall let a user holding the user-management permission disable and re-enable a user account with a reason, shall refuse disabling the administrator's own account, shall refuse a disable that would leave no active user able to manage users or roles, and shall end the sessions of a disabled account.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.300(b); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-ADM-005
**Requirement:** The system shall let a user holding the user-management permission unlock an account, force a password change at next use and start a password reset for another user, each with a reason.
**GxP:** Yes
**Serves:** 21 CFR 11.300(b), 11.300(c) (to be confirmed by QA)
**Status:** Active

### URS-ADM-006
**Requirement:** The system shall let a user holding the role-management permission create, rename and delete roles with a reason, shall require the administrator's password as a signature to delete a role, and shall refuse to delete a built-in role or a role that is assigned to a user.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.200(a); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-ADM-007
**Requirement:** The system shall let a user holding the role-management permission set the permissions of a role only with a reason and the administrator's own password as an electronic signature, shall refuse a set that holds both an administration permission and a laboratory execution or approval permission, shall refuse a change to the administrator's own role, shall refuse a change that would leave no active user able to manage users or roles, and shall make tokens issued before the change stop working.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(g), 11.200(a); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-ADM-008
**Requirement:** The system shall let a user holding the settings permission see the system settings and change one only with a valid value inside its allowed range and a reason, shall require the administrator's own password as an electronic signature for settings marked as GxP-relevant, shall keep every change as an unchangeable version with the old value, new value, user, time and reason, and shall show the history of a setting.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.10(k), 11.200(a); EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-ADM-009
**Requirement:** The system shall, while maintenance mode is on, refuse every changing request from a signed-in user or an instrument agent, except sign-in, sign-out, token refresh, password change and the settings screens used to switch the mode off, and shall answer with the configured maintenance message.
**GxP:** No
**Serves:** EU GMP Annex 11 clause 10 (to be confirmed by QA)
**Status:** Active

### URS-ADM-010
**Requirement:** The system shall record the acting administrator, the time, the reason, the previous value and the new value of every user, role and setting administration action in the audit trail.
**GxP:** Yes
**Serves:** 21 CFR 11.10(e); EU GMP Annex 11 clause 9 (to be confirmed by QA)
**Status:** Active

### URS-ADM-011
**Requirement:** The system shall route a signed-in Quality Assurance user to a QA landing dashboard showing audit trail reviews in progress, open findings, and controlled documents awaiting their approval, and shall build sidebar navigation from the user's granted permissions.
**GxP:** No
**Serves:** EU GMP Annex 11 clause 12 (to be confirmed by QA)
**Status:** Active

### URS-ADM-012
**Requirement:** The system shall allow a Quality Assurance user holding `Documents.Approve` to be assigned and electronically sign as approver of a controlled document (without granting Section Head override powers and preserving the author-cannot-approve rule), and shall let administrators configure per document type whether at least one Quality Assurance approver is required before approval routing and effective activation.
**GxP:** Yes
**Serves:** 21 CFR 11.10(d), 11.50, 11.200(a); EU GMP Annex 11 clauses 12, 14 (to be confirmed by QA)
**Status:** Active

### URS-ADM-013
**Requirement:** The system shall provide a keyboard shortcuts reference dialog accessible from the Help menu or by pressing `?` outside editable inputs, shall place initial focus on the first empty required field when opening standard form dialogs (unless disabled), shall keep client-side sorting disabled on server-paged register tables while supporting server-side sorting (and header sort controls) on allow-listed persisted columns, and shall let administrators enable or disable last-choice entry defaults via system settings.
**GxP:** No
**Serves:** Operational usability and accurate table behaviour under server-side pagination
**Status:** Active

### URS-ADM-014
**Requirement:** The system shall show the application version and the server version to a signed-in user in an About dialog opened from the Help menu, so that the version of the running system can be stated.
**GxP:** Yes
**Serves:** 21 CFR 11.10(a) (to be confirmed by QA)
**Status:** Active

### URS-ADM-015
**Requirement:** The system shall let each signed-in user choose English or Arabic from the account menu and shall save that choice to the user's own interface settings, switching the screen language and direction without signing out; before sign-in it shall use the browser's preferred supported language, and English otherwise. In Arabic the system shall show the screen right to left while keeping identifiers, codes, numbers, units, dates, times, user names and typed data left to right with Latin digits, with the time zone still shown on dates. Printed and exported records (certificate of analysis, sample report, media and cryovial reports, equipment history, CSV and PDF exports) shall always be in English. The electronic-signature dialog shall show the recorded English statement, and in Arabic an Arabic translation below it with a note that the English text is recorded. Server messages sent with a message code shall appear in the user's language.
**GxP:** Yes
**Serves:** 21 CFR 11.50 (the signed meaning is the English statement); operational usability for Arabic-speaking users (to be confirmed by QA)
**Status:** Active
