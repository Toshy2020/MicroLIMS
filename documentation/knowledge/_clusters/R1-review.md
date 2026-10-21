# R1 Domain and GMP review — C1 Platform & administration

Method: read C1 in full; spot-checked against code (AuthenticationService, ElectronicSignatureService, RoleService, UserService, AccessTokenRevalidator, useIdleTimeout, DashboardService KPI deltas, Login.tsx, ChatWindow.tsx). Application not run.

## Phase 5 domain checklist

### MOD-USERS-001 User management
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Users: create, edit, disable/enable, unlock, delete | DOCUMENTED | WF-USERS-001, BR-USERS-001..010 | Reason mandatory for role change, disable, recovery; optional for unlock/reset. |
| Account creation approval / who may hold Users.Manage beyond System Administrator | REQUIRES CONFIRMATION | WF-USERS-001 | No approval step in code. |
| Laboratory membership (department data scope) | DOCUMENTED | WF-USERS-002, BR-USERS-013/014 | Empty membership list is a valid save (user then sees no lab data): intent REQUIRES CONFIRMATION. |
| Segregation: admin cannot change/disable/delete self; last admin protected | DOCUMENTED | BR-USERS-003/004/010 | Role-edit controls are by user id only. |
| Role-change audit row correct | PARTIAL | BR-USERS-003, GAP-112 | Previous value stores the NEW role id (verified, UserService L173-L196). |
| Role change takes effect immediately | PARTIAL | WF-USERS-001 step 3 | Role-TYPE change refuses the old token on next request; change within same type or permission change reaches the user at next access token (up to 15 min). Existing sessions are not ended. |

### MOD-ROLES-001 Roles and permissions
| Item | Status | Where | Note |
|---|---|---|---|
| Roles and permission matrix | DOCUMENTED | WF-ROLES-001, BR-ROLES-001..009 | 4 system roles; 50 codes, 40 enforced, 10 "Legacy-only". |
| Permissions marked Legacy-only have no effect | DOCUMENTED | BR-ROLES-008, GAP-015 | Users may tick them believing they restrict access. |
| Guard against removing Roles.Manage from admin role | NOT IMPLEMENTED | BR-ROLES-003 | No guard (RoleService L126-L171). |
| Role edits are controlled changes (approval, signature, reason) | NOT IMPLEMENTED | WF-ROLES-001 | No signature, no reason field, no second person; only audit row ROLE_PERMISSIONS_UPDATED. Intended change control REQUIRES CONFIRMATION. |
| Atomic role creation | PARTIAL | GAP-107 | Role may keep base grants if second call fails. |
| Role type still drives checks | DOCUMENTED | BR-ROLES-009 | Custom role behaves as its base type for type checks. |

### MOD-AUTH-001 Session handling and passwords
| Item | Status | Where | Note |
|---|---|---|---|
| Sign-in, lockout (5 failures, 15 min) | DOCUMENTED | WF-AUTH-001, BR-AUTH-001..004 | Verified in code. Counter not reset when lock expires: first wrong password afterwards locks again (BR-AUTH-004, GAP-012). |
| Password policy and history (last 5) | DOCUMENTED | BR-AUTH-006/007 | No periodic expiry exists although Change Password subtitle mentions "periodic security policy". |
| Idle timeout | PARTIAL | WF-AUTH-005, BR-AUTH-017, GAP-053 | Browser-side only (20 min + 60 s warning). Server has no idle timeout; access token 15 min renewed by refresh token (7 days) while page is open. A client that does not run the page is not timed out by the server. |
| Forced password change enforced on server | DOCUMENTED | BR-AUTH-016 | 403 on all other calls. |
| E-mail password reset can be completed | NOT IMPLEMENTED | WF-AUTH-002, GAP-100 | No screen calls the confirm endpoint; e-mail carries a token, text says "link". Admin-assisted recovery is the working path. |
| Admin recovery code hand-over procedure | REQUIRES CONFIRMATION | WF-AUTH-003 step 5 | |
| Behaviour after password change (sign-in again?) | REQUIRES CONFIRMATION | GAP-113 | |
| Rate-limit refusal message | REQUIRES CONFIRMATION | BR-AUTH-019 | |

### MOD-SIG-001 Electronic signatures / re-authentication / segregation of duties
| Item | Status | Where | Note |
|---|---|---|---|
| Re-authentication by password | DOCUMENTED | BR-SIG-001, SCR-SIG-001 | |
| Signature record content (name, role, meaning, time, IP, comment) | DOCUMENTED | BR-SIG-003 | |
| Failure throttle | DOCUMENTED | BR-SIG-002 | 5 failures / 15 min; does not lock the login account. |
| Signature atomic with action | DOCUMENTED | BR-SIG-004 | |
| Signature trail viewable in UI | NOT IMPLEMENTED | MOD-SIG-002 | Endpoint only; per-record signatures appear as nodes in the audit traceability chain for samples. |
| Signatures on microbiology plate-count entry, pathogen steps, return to analyst, user/role administration | NOT IMPLEMENTED | WF-USERS-001, GAP-009/010/028 | Verified not signed. |
| Uniform meaning/comment rule | REQUIRES CONFIRMATION | BR-SIG-006, GAP-027 | |
| Segregation of duties | DOCUMENTED | BR-SIG-007/008 | Compares user ids; System Administrator not exempt. |

### MOD-AUDIT-001 / -002 / SECAUDIT-001 / SIG-002 Audit trail and traceability
| Item | Status | Where | Note |
|---|---|---|---|
| Audit search, history, diff, traceability drawer | DOCUMENTED | WF-AUDIT-001 | Cap 300 rows in search. |
| Export / print of audit trail | NOT IMPLEMENTED | reports | None in this screen. |
| Filter by platform actions (USER_*, ROLE_*) | NOT IMPLEMENTED | GAP-101 | |
| Login history and security events visible | NOT IMPLEMENTED | MOD-AUDIT-002, MOD-SECAUDIT-001, GAP-030 | Recorded, no screen; `System.ViewSecurityAudit` unenforced. |
| Entities excluded from audit capture | PARTIAL | GAP-111 | 9 types excluded (incl. incident, error log, refresh token, notification read state). |
| Audit log append-only / not deletable | PARTIAL | BR-AUDIT-009 | Database triggers (migration 20260902181230) refuse UPDATE/DELETE on AuditLogs, AuditEventChanges, ElectronicSignatures; presence in live database not checked; other tables not covered. |
| Traceability beyond sample/media/material/cryovial/equipment/item | NOT IMPLEMENTED | GAP-108 | Others show "General" node; no stand-alone traceability page. |
| Archived record integrity (SHA-256) | DOCUMENTED | BR-AUDIT-008 | Verified on every download; archive access has no lab-scope check (REQUIRES CONFIRMATION). |
| PDF generation failure behaviour | REQUIRES CONFIRMATION | BR-ARCHIVE-002 | |

### MOD-ERR-001 Incidents / Error Log
| Item | Status | Where | Note |
|---|---|---|---|
| Incident list, severity override, resolve/reopen, CSV export | DOCUMENTED | WF-ERR-001 | |
| Resolve/reopen/override audited | NOT IMPLEMENTED | audit-trail | No audit row; resolver and time on incident only. |
| Business-rule and permission refusals recorded as incidents | PARTIAL | BR-ERR-005/006, GAP-110 | List may contain routine refusals. |
| Critical-error alert e-mail | REQUIRES CONFIRMATION | BR-ERR-007 | |
| Triage process (who/when) | REQUIRES CONFIRMATION | WF-ERR-001 | Page states it is not part of the GxP record. This is a technical log, not a deviation/CAPA record. |

### MOD-DASH-001 Dashboards (platform)
See "Reports and dashboards" below.

### MOD-NOTIF-001, MOD-MSG-001, MOD-DISC-001, MOD-NAV-001
| Item | Status | Where | Note |
|---|---|---|---|
| Notifications | PARTIAL | BR-NOTIF-010/011, GAP-103/104 | Review/Approval waiting triggers differ from spec; alert e-mail goes to `{username}@microlims.local`, flag EmailSent set before sending. |
| Messages / Discussions | DOCUMENTED | WF-MSG-001, WF-DISC-001 | No signature; messages not audited; `Messages.Use`, `Discussions.View/Create` unenforced. Whether discussions may be cited as GMP decisions: REQUIRES CONFIRMATION. |
| Navigation guards | DOCUMENTED | BR-NAV-001..005 | Permission failure = silent redirect. |

### Reports and dashboards (C1 scope: SCR-DASH-001..004)
| Item | Status | Where | Note |
|---|---|---|---|
| KPI tiles use live database counts | DOCUMENTED | BR-DASH-001..011 | No mock data found in dashboard modules (grep of frontend/src/modules/dashboard). Month-over-month deltas are computed from data but not shown on screen. |
| "Overdue" logic | PARTIAL | GAP-102 | Three different definitions (24 h from receipt; 24 h in review; 7-day analyst SLA). |
| "Due today" label | PARTIAL | GAP-105 | Counts submissions today. |
| "Total samples/tests" | PARTIAL | GAP-106 | All-time, not today. |
| On-Time Reading Rate shows 100 with no data | PARTIAL | BR-DASH-010 | Default 100, indistinguishable from real 100 %. |
| Dashboard variant follows role type, data follows permissions | PARTIAL | GAP-055 | |
| Export of dashboard data | NOT IMPLEMENTED | reports | |

## Phase 6 GMP / data-integrity facts

### MOD-AUTH-001 Authentication, session
- The system identifies users by unique username; every sign-in outcome (success, unknown user, locked, wrong password, inactive) is written to login history with IP address [BR-AUTH-014].
- The system locks an account for 15 minutes after 5 failed attempts and revokes its refresh tokens; the lock is an automatic event with no user actor [BR-AUTH-001].
- The failure counter is not cleared when the lock expires [BR-AUTH-004].
- Idle sign-out is performed by the browser page only (20 min, 60 s warning); the server does not time out idle sessions [BR-AUTH-017]. Access tokens last 15 min; refresh tokens 7 days, rotated [BR-AUTH-005].
- Sign-out revokes the refresh token through a best-effort call; a failed call is ignored on screen [WF-AUTH-005].
- Password changes and resets revoke all refresh tokens; a token issued before the change is refused [BR-AUTH-008, AccessTokenRevalidator].
- Login history and security events are recorded but cannot be viewed in the application [GAP-030].
- The sign-in page footer reads "FDA 21 CFR Part 11 & EU Annex 11 Compliant" (Login.tsx L204); this statement is text in the page and is not evidence of compliance or validation [GAP-023].

### MOD-USERS-001 Accountability and controlled account changes
- User creation, profile edit, role change, disable/enable, unlock, forced password change, e-mail reset request and hard delete are recorded in the audit trail with the acting user and, where entered, a reason [WF-USERS-001].
- No electronic signature is required to create users, change roles or disable accounts [WF-USERS-001].
- The role-change audit row's previous value contains the new role id [GAP-112].
- Permanent deletion is refused for any user with activity history, including any user who has signed in [BR-USERS-010].

### MOD-ROLES-001 Role separation and role edits
- The system records granted and revoked permission codes for every role permission save in the audit trail [BR-ROLES-007].
- Role permission edits require `Roles.Manage` only; no reason, signature or second person is required [WF-ROLES-001].
- System roles' permissions can be edited; no guard prevents removing `Roles.Manage` [BR-ROLES-003].
- 10 of 50 permission codes have no effect on access (Legacy-only) [BR-ROLES-008].
- A custom role is treated as its base type for role-type rules [BR-ROLES-009].

### MOD-SIG-001 Electronic signatures and segregation of duties
- A signature requires the signer to re-type their own login password; the password is checked against the stored hash [BR-SIG-001].
- The signature record stores user id, name, username and role-type snapshots, meaning, entity, time, comment and IP address and is append-only [BR-SIG-003].
- A failed or throttled signature attempt is written to the audit trail without the password; after 5 failures in 15 minutes further attempts are refused [BR-SIG-002].
- The signature is saved in the same transaction as the action; a failed password stops the action [BR-SIG-004].
- 26 actions are signed (see C1 roles); microbiology count entry, pathogen steps, return to analyst, user and role administration are not signed [WF-USERS-001, GAP-009/010/028].
- The system refuses review/approval by the person who performed the earlier step; System Administrator is not exempt [BR-SIG-007].

### MOD-AUDIT-001 Audit trail and traceability
- Add, change and delete of tracked entities are written to the audit trail with previous and new values, user and UTC time [BR-AUDIT-001].
- Nine entity types are not written to the audit trail (incidents, error logs, refresh tokens, notification state, security events, access logs, the audit tables themselves) [GAP-111].
- The Audit Trail screen shows at most 300 events per search; it has no export and cannot filter on platform-specific actions [BR-AUDIT-002/004].
- Database triggers refuse UPDATE and DELETE on AuditLogs, AuditEventChanges and ElectronicSignatures (migration 20260902181230); that they exist in the live database was not checked [BR-AUDIT-009].
- Archived PDFs are frozen at final decisions with a SHA-256 hash; a mismatch at download is flagged in the interface [BR-AUDIT-008].

### MOD-ERR-001 Incidents
- Resolving, reopening and overriding severity of an incident writes no audit trail row [audit-trail].
- The page states the log is not part of the GxP record; no deviation, investigation or CAPA link exists [SCR-ERR-001].

### Status control, result modification, rework, retest, investigation
- Not in C1 scope (see C3, C8). Notifications for returned tests (TestReturnedForRevision) are generated but returns themselves are not signed [BR-NOTIF-006].

## Mock / placeholder / unverified data
- No mock data found in C1 dashboards: tiles read live counts (DashboardService). The `kpi-deltas` month-over-month percentages are computed (0 when last month is empty) but are not displayed on the Administrator dashboard.
- Placeholder-like defaults: "On-Time Reading Rate" shows 100 when no completed incubations exist [BR-DASH-010].
- Page text claims not backed by behaviour: sign-in footer "Part 11 & Annex 11 Compliant" (GAP-023); Change Password subtitle "periodic security policy" (no expiry); sign-in "a reset link has been generated" (no link, GAP-100); Admin dialog "send it via email" even with no e-mail on file.
- Alert e-mail goes to a synthetic address and is flagged sent before delivery (GAP-104).
- Notification push service: no consumer traced (GAP-113).

## Guide callouts
1. Sign-in page footer claims compliance; this is text only and is not a validation statement. (GAP-023)
2. Idle sign-out happens in the browser only; the server does not time out inactive sessions. (GAP-053)
3. After 5 wrong passwords the account locks 15 min; the counter is not reset when the lock ends, so one further wrong password locks again. (GAP-012)
4. "Forgot password?" cannot be completed in the application; ask an administrator for an admin-assisted recovery code. The on-screen text mentions a reset link, but none is sent. (GAP-100)
5. Passwords do not expire; the Change Password subtitle mentions a periodic policy that does not exist in the code. (verified-roles §9)
6. Roles: 10 permissions marked "Legacy-only" have no effect; do not rely on them to restrict access. (GAP-015)
7. Role and user changes are audited but not signed and need no second person; nothing prevents removing Roles.Manage from an administrative role. (GAP-009)
8. Audit Trail shows 300 events at most, has no export, cannot filter on user/role actions, and does not show login history or security events. (GAP-030, GAP-101)
9. Some records are not audited (incidents, error log entries, refresh tokens, notification state). (GAP-111)
10. The role-change audit entry shows the new role id as the previous value. (GAP-112)
11. Error Monitoring is a technical log, not a deviation or investigation record; resolving an incident is not audited. (GAP-110)
12. "Overdue" has three different meanings on the dashboards; read each tile's rule. (GAP-102)
13. Administrator "Total samples/tests" are all-time counts; Reviewer "Due today" counts submissions made today. (GAP-105, GAP-106)
14. Analyst "On-Time Reading Rate" shows 100 when there is no data. (BR-DASH-010)
15. Alert e-mails go to `{username}@microlims.local`, not the e-mail on the profile, and are marked sent even if no mail server is set up. (GAP-104)
16. Review/approval waiting notifications use section sign-off status, not the status described in the specification. (GAP-103)
17. Archived-record download has no laboratory scope check; a failed integrity check shows a warning toast. (GAP-108)

## Corrections made to the cluster file
- BR-AUDIT-009 (corrected in Phase 5): added the immutability-trigger fact (migration 20260902181230).
- WF-MSG-001 step 4 (corrected in Phase 5): conversation is marked read each time messages are loaded in the chat window (ChatWindow.tsx L39-L45); previously "not traced".
- All other spot-checked facts (lockout, signature throttle, role-change audit overwrite, token revalidation, footer text) were confirmed.
