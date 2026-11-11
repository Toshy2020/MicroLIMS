# B4 — Quality Assurance (QA) Role: Design Specification

**Roadmap package:** B4 (`improvement-roadmap.md`)
**Status:** Approved by the user 2026-11-11
**Date:** 2026-11-11

## 1. Purpose

Add a QA role that is independent of the laboratory: it can read everything, approve controlled documents, and perform and sign the periodic audit trail review and the user access review. It can never record, review or approve laboratory results, and never administer users.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | What QA sees | Everything, in all laboratories, without section membership. Read only |
| 2 | QA decisions | Approve controlled documents; perform and sign audit trail reviews and access reviews |
| 3 | QC Manager | Stays as it is. QC Manager is the laboratory head; QA is the independent quality unit |
| 4 | Independence | Enforced by the system: a QA role can never hold laboratory or administration permissions |

Not chosen, so not built: a QA final release step after sample approval, and QA closure of OOS investigations. The OOS question returns in package D1.

## 3. Out of scope

- Any change to sample review, approval or release.
- More than one role per user (a user still has exactly one role).
- Deviations, CAPA and change control (package D2).
- Changing what QC Manager may do.

## 4. Current state (from code, not run)

- Five system roles: System Administrator, Section Head, Reviewer, Analyst, QC Manager. A user has one role. Administrators can create custom roles based on a system role.
- Permissions decide almost everything (70 codes). The system already refuses a role that mixes administration permissions with laboratory execution or approval permissions.
- Reading across all sections needs the permission `Sections.ViewAll`, held today only by the System Administrator. Every other role sees only the sections it is a member of.
- **Document Control checks the role by name:** an approver must be a Reviewer or Section Head, and several override paths are open to the Section Head only. A QA user could not approve a document today even with the permission.
- The audit trail can be searched and exported. **There is no record that someone reviewed it.**
- An access review feature exists and belongs to the administrator's area.
- QC Manager was added by one migration that created the role and its grants; this is the pattern to follow.
- On screen, role names are written in about 28 files; a few screens show or hide things by role name.

## 5. Design

### 5.1 The role

- A new system role **QA** (display name "Quality Assurance"). It cannot be deleted. Custom roles can be based on it and inherit its limits.
- QA users have no section membership and need none.

### 5.2 Permissions

| QA has | QA never has |
|---|---|
| Read of every laboratory record, report, dashboard, KPI and trend, in all sections | Receive, test, enter or correct results, prepare media or solutions |
| Audit trail search and export | Review or approve samples, release media, close investigations |
| Read of users, roles and access (not manage) | Manage users, roles, sections or system settings |
| Document Control: read all, review and approve when assigned | Configure items, specifications, tests, equipment |
| Perform and sign audit trail reviews and access reviews (new) | |

- New permissions: `Audit.Review` (perform and sign an audit trail review) and `Access.Review.Sign` (decide and sign an access review).
- Document review and approval are counted today among the laboratory approval permissions that may not be mixed with administration. For QA they are allowed; every other laboratory execution or approval permission is forbidden.
- `Sections.ViewAll` is granted to QA. It gives reading only; writing still needs membership, which QA does not have.
- **Independence rule, enforced on the server:** a role of type QA, or any role holding `Audit.Review` or `Access.Review.Sign`, is refused any laboratory execution or approval permission and any administration permission. The check is added to the existing rule that already separates administration from the laboratory, so it applies to custom roles and to every later change.
- The rule is also checked at start-up against the stored grants: a violation is logged as a security event and reported in the access review, never silently repaired.

### 5.3 Reading across sections

Granting `Sections.ViewAll` only works where a screen asks the shared scope service. Any read path that looks at membership directly would show QA nothing. All read paths are listed and the ones that bypass the scope service are moved to it. No write path changes. The audit trail search is already not limited by section.

### 5.4 Controlled documents

- QA can be assigned as reviewer or approver of a document, and can approve with an electronic signature, like a Reviewer today.
- Document Control lets only a Reviewer or Section Head be an approver, by role name. QA is added to that rule. The Section Head override powers are **not** given to QA. Documents are already readable without section membership.
- The existing rule stays: the author of a revision cannot approve it.
- Whether a document type **must** have a QA approver is a setting per document type, off by default, switched on by the site.

### 5.5 Audit trail review (new)

A record that the audit trail was reviewed:

- QA starts a review by choosing a period and a scope (all, a section, a record type, or one sample or batch). The system fixes the list of audit entries in that scope at that moment (count and identifying range), so later entries are not silently included.
- QA reads the entries with the existing search tools, marks entries of concern as **findings** with a note, and concludes: "No findings" or "Findings recorded".
- Completion requires an electronic signature (user and password) with the meaning "Audit trail review". The signed review is permanent; it can be read and exported as a PDF showing name, date and time with time zone, and meaning.
- A finding can be linked later to a deviation (package D2); for now it is a note with a status (Open / Closed with comment).
- The dashboard shows QA when the next review is due, from a "review every N days" setting per scope (blank until the site sets it).
- Reviewing does not change any audit entry.

### 5.6 Access review

- The access review already exists: a fixed snapshot of users and access, findings, and an electronic signature. Today the System Administrator does all of it.
- The administrator still generates the review. **Recording findings and signing move to QA.** The administrator carries out the requested changes.
- Until a site has an active QA user, the administrator can still sign as today, so nothing is blocked on the day of upgrade. From the moment one exists, an administrator's signature is refused.
- The existing rule stays: a person named in a finding cannot sign.

### 5.7 Screens

- QA lands on My Work (package C1) or, before C1, a QA dashboard: reviews due, documents waiting for QA, open findings.
- Menus follow permissions. The few screens that test a role name are changed to test permissions.
- QA sees laboratory screens read-only; action buttons that QA cannot use are not shown (or, after package C8, shown disabled with the reason).

### 5.8 Upgrade

- One migration adds the role, the two permissions and the grants, with an audit entry for each grant, following the QC Manager migration.
- No existing user or role changes. An administrator creates QA users afterwards.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Administrator tries to add a laboratory or administration permission to a QA role | Refused with the reason |
| QA calls an action that changes laboratory data | Refused by permission, as for any user without it |
| QA signs with a wrong password | Refused; counted like any failed signature |
| Audit entries are added during a review | Not part of the fixed scope; the review shows "N newer entries exist after this review's cut-off" |
| No QA user exists | Access review works as today; audit trail review is not available and the dashboard says why |

## 7. Testing

- **Backend `[PostgresFact]`:** migration creates the role and grants with audit entries; independence rule refuses each forbidden permission on create and on update, for the system role and a custom role based on it; QA reads samples, results and audit entries of a section it is not a member of, and is refused every write; a non-QA user without membership still sees nothing; document approval by QA with signature, refused for the author, existing Reviewer and Section Head behaviour unchanged; audit trail review: scope fixed at start, findings, signature required and bound to the review, signed review immutable, wrong password refused, audit entry written; access review signed by QA, refused for the administrator once a QA user exists; all with the state, audit and negative checks the project requires.
- **Frontend (`userEvent`):** QA menu and dashboard; read-only laboratory screens; review flow with findings and signature dialog; role label; role creation based on QA shows the limits.
- **Manual:** sign in as QA and try each laboratory action by direct link.

## 8. Delivery stages

1. Role, permissions, independence rule, migration.
2. Cross-section reading through the scope service.
3. Document Control by permission; QA approver.
4. Audit trail review.
5. Access review signature.
6. Screens and labels.
7. Documents: requirements (A1 areas SEC and ADM), role guide for QA (A3), SOP template notes.

## 9. Open items

1. Review frequencies for the audit trail (per scope) — set by the site.
2. Which document types must have a QA approver — set by the site.
3. Whether QA should close OOS investigations — decided in package D1.
