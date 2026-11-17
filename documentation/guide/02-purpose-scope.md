## 2. Purpose and Scope

### 2.1 Intended Audience and Scope
This guide covers the day-to-day operation of MicroLIMS. It is written for:
- **Laboratory Analysts**, who receive samples, prepare samples, run microbiological and physicochemical tests, operate instrument workspaces, prepare solutions and media, and update inventory.
- **Technical Reviewers**, who perform the secondary technical review of results and serve as technical reviewers of controlled documents.
- **Section Heads**, who assign work, record sample release decisions, release media lots and cryovial batches, manage laboratory configuration, and act as Document Controller in Document Control.
- **System Administrators**, who manage user accounts, laboratory memberships, roles and permissions, review the audit trail, and monitor technical errors.
- **Document Control and Quality personnel**, who follow the life of controlled documents (SOPs and similar), periodic review, and reading and training records.

Roles are editable by administrators. This guide names the default roles for each permission, and a laboratory may have changed them.

### 2.2 Operational Scope
MicroLIMS manages laboratory operations in two laboratories:
1. **Microbiology Laboratory**: count testing, pathogen testing, environmental monitoring, water monitoring, after-cleaning monitoring, media preparation and evaluation, growth promotion testing, and reference strain cryovials.
2. **Physicochemical Laboratory**, in two areas:
   - **FP**: finished product testing, including assay, dissolution, disintegration, and weight variation.
   - **RM & PM**: raw material and packaging material testing.

A user sees the laboratories and areas that an administrator has assigned to the account. A user with no assignment sees no laboratory data.

### 2.3 Records That Exist but Cannot Be Viewed on Screen
Some information is recorded by the system, but MicroLIMS has no screen that displays it. This guide gives no procedures for it:
- **Login history**: every sign-in attempt (success, unknown user, locked account, wrong password, disabled account) is recorded, but there is no screen to view it.
- **Security events**: events about sign-in, sessions, passwords, and role changes are recorded without a viewer. The permission *Security Audit Viewing* has no effect on access.
- **Electronic signature history**: there is no separate screen listing signatures. For samples, the signatures appear as steps in the traceability chain of the Audit Trail screen.
- **Administrative reporting backfill, analyst availability settings, KPI section settings, document escalations, and a general results list**: these exist as system functions without a screen.
- **Instrument report reading**: the system contains code to read some instrument reports, but no screen uses it. Results are entered by hand, and report files can be attached as evidence in the instrument workspaces.

> **Note for reviewers:** Auditors may expect login history, security events, and the signature trail to be viewable. Whether any of them is meant to get a screen has not been confirmed. (GAP-030)

### 2.4 Features Not Found in the Software
The following were not found in MicroLIMS. This guide gives no procedures for them.
- A separate **Final Release** step or permission. The Section Head's release decision on a sample (the *Submit Release Decision* card) is the release step in the application.
- A **sterility test** or sterility result entry. Some screen text in the media area mentions sterility.
- An **EM annual plan**, EM schedules, water sampling frequencies, or monitoring plans. Sampling is done on demand by receiving and preparing samples.
- **Reference strain passage tracking**. Cryovials have storage moves, thaw counts, destruction, approval, and an expiry date.
- **Equipment qualification records** (IQ, OQ, PQ). Equipment records show calibration due dates and calibration certificate documents.
- A **laboratory incident, deviation, or CAPA module**. The Error Monitoring screen shows technical errors only and states that it is not part of the GxP record.
- A separate **Titration Methods** master page. Titration settings are part of Test Master.
- An integration with **SAP** or any other ERP system.
- A **local AI assistant**.
- A **live data feed from instruments**. Results are entered by hand and report files are attached as evidence.
- Separate **Reception** or **QA** roles. Product, Raw Material and Packaging Material samples are received by users who hold a receiving permission. Environmental Monitoring, Water and After Cleaning samples can be received by any signed-in user (see §23–§25). QA staff need a custom role created by an administrator.

> **Note for reviewers:** Screen text in the media area mentions sterility, but no sterility test or result entry exists. (GAP-022)

<!-- Review log (Sonnet): 9 corrections; invented items removed: module ids, endpoint paths, background worker claim for escalations, "automated sampling schedule generator" and "no reference-strain passage tracking" overstatements, laboratory code names; callouts added: GAP-030, GAP-022 -->
<!-- Phase 13 (agy): applied F-A01 -->
<!-- Phase 13 review (Sonnet): checked against RECONCILIATION section 2 and frontend strings; no invented items -->
