# SOP Draft Outline: Company sign-in (single sign-on)

> **Draft outline — not approved.** This is a working draft outline generated from the implemented company sign-in design (`2026-11-13`) for completion, review, and approval by Laboratory Management and Quality Assurance before go-live. All items described here are built; site values are still to be completed.

| Field | Value |
|---|---|
| Document number | |
| Title | Company sign-in, single sign-on (draft outline) |
| Version | 0.1 |
| Status | Draft (outline — not approved) |
| Author | |
| Reviewer | |
| Approver | |
| Effective date | |

## 1. Purpose and scope

- Define how laboratory staff sign in with a company account, how company accounts are linked to MicroLIMS users, how emergency administrator accounts are held, how electronic signatures work for company users, how company sign-in is switched on or off, and how leavers and directory outages are handled.
- Company sign-in changes only how a user proves identity. Roles, laboratory sections and permissions stay in MicroLIMS and are never read from the company directory.
- Electronic signatures are not changed: every signature still requires user ID and a **MicroLIMS signing password**. The company password is never used for signing.
- Modes: `Off` (default; local sign-in only), `OpenIdConnect` (built), and `ActiveDirectory` (built).

## 2. Responsibilities

- **All users:** Sign in only with your own company account. Keep your MicroLIMS signing password private. Your signing password must differ from your company password. Report sign-in problems to a System Administrator.
- **System Administrators (`Users.Manage`):** Link users to company usernames, re-link accounts with a reason (never your own account), maintain emergency accounts and their custody, test the connection, and switch company sign-in on or off. Verify identity before a re-link or an emergency account change `[SITE TO COMPLETE: approved identity verification method]`.
- **Company IT:** Register the MicroLIMS application with the provider (OpenID Connect) or create the service account (Active Directory), register the callback address, keep the client secret, and disable company accounts of leavers on the day they leave.
- **Quality Assurance (QA):** Approve this SOP, set the site values marked below, and review company sign-in security events.

## 3. Mode, provider configuration and session cap

- **Mode:** System Setting `security.sso.mode`, one value per installation: `Off` (default), `OpenIdConnect`, or `ActiveDirectory`. Changes follow section 7.
- **Provider configuration (server, not in the database):** `Sso:Oidc:Authority`, `Sso:Oidc:ClientId`, `Sso:Oidc:ClientSecret`, `Sso:Oidc:UsernameClaim` (default `preferred_username`), `Sso:Oidc:RequireMfaClaim` (default off), and `Sso:Oidc:RedirectUri` (the callback address registered with the provider when MicroLIMS runs behind a proxy).
- **Secret custody:** The client secret is kept outside source control in `[SITE TO COMPLETE: server secret store or vault, and who may read it]`.
- **Session cap:** System Setting `security.sso.sessionMaxHours`, default `12`, allowed `1` to `24`. Site value: `[SITE TO COMPLETE: approved hours]`. In OpenID Connect mode a company sign-in session must sign in again once this many hours have passed since sign-in.
- **Requirement for `RequireMfaClaim`:** `[SITE TO COMPLETE: whether the provider must show multi-factor sign-in; if on, the provider owns the second factor and MicroLIMS does not ask for one]`.

## 4. Linking users to company accounts

- **Order:** Create the MicroLIMS user first, with its role and sections. Then set the sign-in method to company sign-in and enter the company username exactly as the provider shows it.
- **Bulk linking:** Use the CSV import (`username, companyUsername`) on the Company sign-in page for a first roll-out or large changes. The import accepts at most `2000` rows and shows a preview with a status for each row (`Ok`, `UnknownUser`, `Duplicate`, `AlreadyLinked`, `Invalid`). Correct every `Invalid` and `Duplicate` row before importing, or tick "Skip invalid rows" only after reviewing each skipped row. A reason is required for each import.
- **Unknown accounts:** A company account with no linked MicroLIMS user is refused at sign-in. No user is created by signing in.
- **Sign-in method after import:** The import sets the company username but does not change the sign-in method. A user whose method is still local cannot sign in with the company account (open item `CONF-191`). `[SITE TO COMPLETE: until CONF-191 is decided, check each imported user's sign-in method is company sign-in before go-live]`.
- **First sign-in:** The company account's permanent identifier is stored at the first successful company sign-in. After that, the link follows that identifier.
- **Link states:** `NotLinked`, `LinkedNeverSignedIn`, `Linked`, `SignInRefused` (as shown on the Company sign-in page). Check that every intended user shows `Linked` after its first sign-in.

## 5. Signing password

- **First company sign-in:** The user must choose a MicroLIMS signing password before the application opens. The password policy and password history apply.
- **Must differ from the company password:** The signing password is not the company password. The system cannot compare the two, because under OpenID Connect it never receives the company password. Each user confirms at the time of choosing that the signing password is different. `[SITE TO COMPLETE: how this is recorded or checked]`.
- **Users moving from local sign-in:** A user who moves to company sign-in keeps the current MicroLIMS password as the signing password. The administrator must confirm that it differs from the company password, and the user changes it if it does not. `[SITE TO COMPLETE: check method]`.
- **Changing or resetting the signing password:** Use the same routes as the MicroLIMS password: change, reset by e-mail, or administrator recovery, as set in `[SITE TO COMPLETE: reference to the password SOP]`.
- **On screen:** The signature dialog labels the field "Signing password" for company users. The user is reminded that it is not the company password.

## 6. Emergency administrator accounts

- **Number and eligibility:** At most `3` emergency administrator accounts. Each must hold the System Administrator role, use local sign-in, and have an active second factor (see the Multi-factor authentication SOP outline). An administrator marks an existing account as an emergency account. A new user cannot be marked at creation (open item `CONF-196`); create the account, enrol its second factor, then mark it.
- **Sign-in while company sign-in is on:** Local sign-in is refused for every other account. An emergency account signs in with its MicroLIMS password and a second factor. Trusted computers do not apply to emergency accounts, and the second factor is always asked.
- **Notification:** Every completed emergency sign-in is recorded as `AUTH_EMERGENCY_LOGIN` and e-mailed to active System Administrators.
- **Custody:** `[SITE TO COMPLETE: who holds the emergency passwords and the second-factor devices; where sealed credentials are kept; the access log; who may open them; how often they are rotated]`.
- **Review:** Each emergency sign-in is reviewed by `[SITE TO COMPLETE: role and interval]` against a logbook entry giving the reason for the sign-in.

## 7. Switching company sign-in on or off

1. **Configure the provider** (section 3) and restart the service if the site requires it.
2. **Run Test connection** on the Company sign-in page. A passed test is recorded with its time. A failed test shows the reason and is recorded too. Run the test again after any change to the configuration, including the client secret: the system does not detect a change to the secret alone.
3. **Read the blockers.** Switching on is refused while any blocker remains:
   - "Run a successful connection test first."
   - "At least one emergency administrator account with a second factor is required."
   - "Link your own account to a company username first."
4. **Check the unlinked users list.** Each user without a company username will not be able to sign in after switching on. `[SITE TO COMPLETE: decision on users who will not be linked]`.
5. **Enter the reason for change** and complete the electronic signature with user ID and MicroLIMS password.
6. **Switching off** has no blockers and is always allowed. It is a change to access control: record it with a reason and signature, and follow `[SITE TO COMPLETE: change control SOP]`.

## 8. Re-link

- **When:** A company account has been replaced or re-issued, or a link was made to the wrong account. Re-link only when the company IT record confirms the change.
- **How:** Use Re-link on the user, enter a reason, and submit. Another administrator must do this; the server refuses a re-link of your own account.
- **Effect:** The stored identifier is cleared. The next company sign-in of the linked account binds the MicroLIMS user to that account. A company account that was re-issued to another person is then not matched to the earlier user.
- **Username changes:** Changing the company username on the user form does not clear the stored link (open item `CONF-197`). Until that is decided, use Re-link whenever the company username of a linked user changes. `[SITE TO COMPLETE: confirm this rule]`.

## 9. Leavers and disabled accounts

- **Company account disabled:** Company IT disables the company account on the day the person leaves. The MicroLIMS user is then deactivated by a System Administrator and its active sessions are ended from User Sessions.
- **OpenID Connect mode:** MicroLIMS is not told when a company account is disabled. Deactivate the leaver in MicroLIMS on the leaving day; the session cap (section 3) only limits how long an existing session can last.
- **Active Directory mode:** A background check runs every `60` minutes against each linked active account. A disabled or missing account is deactivated and its sessions ended automatically, and System Administrators are notified. A failed directory lookup changes nothing.
- **Reactivation:** Reactivation is always a manual action by a System Administrator, after the person's status is confirmed. A company account being enabled again does not reactivate the MicroLIMS user.

## 10. Directory or provider outage

- **What users see:** Company sign-in fails with a plain message. Users who are already signed in continue until their session needs the provider. `[SITE TO COMPLETE: sessions allowed during an outage]`.
- **Emergency accounts:** While company sign-in is on, emergency accounts remain the only way to sign in with a password. Use them only for `[SITE TO COMPLETE: approved uses, for example restoring provider access or correcting a configuration]`.
- **Logging:** Record each emergency use in the logbook with its reason, the person who used it, and the time, and check it against the e-mail notice.
- **Switching the mode off during an outage** lets every local account sign in with a password. It is a change to access control and must follow section 7 and `[SITE TO COMPLETE: QA approval rule]`.
- **Leaver decisions during an outage:** Do not deactivate or reactivate users because the directory cannot be reached. Base those decisions on the confirmed company account status.

## 11. Periodic checks

- At `[SITE TO COMPLETE: monthly / quarterly]` intervals, review:
  - Users not linked, and users with link state `LinkedNeverSignedIn`.
  - Security audit events: `AUTH_SSO_LOGIN_FAILED_NOT_LINKED`, `AUTH_SSO_LOGIN_FAILED_TOKEN_INVALID`, `AUTH_SSO_LOGIN_FAILED_SUBJECT_MISMATCH`, `AUTH_LOCAL_LOGIN_REFUSED_SSO_ON`, `AUTH_EMERGENCY_LOGIN`, `SSO_CONNECTION_TEST_FAILED`, `USER_DIRECTORY_RELINKED`.
  - Emergency accounts: second factor status, custody records, and logbook entries.
  - Provider client secret expiry and the date of the last passed connection test.
- Time synchronisation: The server clock must be synchronised to `[SITE TO COMPLETE: internal NTP server]`. Company sign-in accepts up to `2` minutes of clock difference from the provider.

## 12. Upgrade notes

- **Database migration:** Apply migration `20261113075423_AddSingleSignOn`. It adds the company sign-in fields to users and the tables for the pending sign-in requests and the one-time exchange codes.
- **Default is off:** The default mode is `Off`. Upgrading does not change how existing users sign in until an administrator switches company sign-in on.
- **Configuration:** Set the `Sso:Oidc` values (section 3) before the first Test connection. Nothing is required for mode `Off`.
- **Verification before go-live:** Active Directory sign-in, the session cap and the leaver check (FS-SEC-035 to FS-SEC-037) are built and tested against PostgreSQL; the tests against a real directory server run in CI only. Verify them on site before switching on a mode that depends on them.
- **Open conflicts before go-live:** `CONF-191` (sign-in method for linked accounts), `CONF-192` (connection-test security events), `CONF-193` (pending requests and exchange codes in the audit trail), `CONF-194` (reason for bulk import), `CONF-195` (provider shown when the mode is Off), `CONF-196` (emergency status at creation), and `CONF-197` (company username change and the stored link).

## 13. Review and approval

| Role | Name | Signature | Date |
|---|---|---|---|
| Author | | | |
| Reviewer (IT / System Administrator) | | | |
| Approver (Quality Assurance) | | | |
