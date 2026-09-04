# MicroLIMS Document Control — Release 1b
## Post-Deployment Smoke Test Protocol & Verification Checklist

- **Document Identifier:** `ML-DC-R1B-SMK-001`
- **Release Version:** MicroLIMS v1.1.0-rel1b
- **Module:** Document Control Module (Release 1b)
- **Target Execution Environment:** Freshly Deployed Production / Pre-Production Environment
- **Date:** 2026-09-04
- **Verification Protocol Status:** **CONTROLLED SMOKE TEST PROTOCOL SPECIFICATION**
- **Prerequisite:** Successful execution of Deployment Runbook (`ML-DC-R1B-DEP-001`) and Formal Release Approval (`ML-DC-R1B-APP-001`)

---

### 1. Protocol Purpose & Scope

This protocol defines the mandatory 14-point controlled post-deployment smoke test suite to be executed immediately following production deployment of Release 1b. 

The objective is to rapidly verify that critical GxP, 21 CFR Part 11, and operational paths function correctly in the live environment without introducing data corruption or unhandled errors, prior to releasing the system for general laboratory user access.

---

### 2. Test Execution Governance & Acceptance Criteria

- **Execution Mode:** Manual verification by designated QA / CSV test engineer using controlled test accounts.
- **Pass Criterion:** 100% of test checkpoints (14 of 14) must achieve `PASS`.
- **Failure Procedure:** Any deviation or failure halts production release. The System Owner and Technical Lead must be notified immediately to initiate the Rollback Procedure in `ML-DC-R1B-DEP-001`.
- **Test Data Policy:** All records created during smoke testing must be prefixed with `SMK-PROD-` and formally archived or marked as test records per standard operating procedures.

---

### 3. Fourteen-Point Controlled Smoke Test Suite

| Test Step # | Test Domain / Inspection Focus | Test Procedure & Actions | Expected Result & Acceptance Criteria | Execution Result (P/F) | Verification Signature & Date |
|:---:|:---|:---|:---|:---:|:---|
| **SMK-01** | **User Authentication & Session** | Navigate to `/login`. Authenticate using valid GxP user credentials with password. | User successfully authenticated; valid JWT token issued; user redirected to dashboard. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-02** | **Role-Based Access Control** | Log in sequentially with `DocControl_Admin`, `Author`, `Reviewer`, and `Reader` roles. | UI elements, buttons, and API routes correctly restricted based on assigned role permissions. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-03** | **Document Library Navigation** | Navigate to Document Control Library. Test search filter, category dropdown, and pagination. | Document list renders cleanly; existing Release 1a documents display correct metadata and state. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-04** | **Document Details Inspection** | Select an existing document. Inspect metadata, current revision, and revision history tab. | Document details, lifecycle status, version number, and revision chain render accurately. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-05** | **Controlled File Retrieval** | Click to download / view primary document artifact file attachment. | File streams successfully; HTTP security headers present; file content matches hash. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-06** | **Draft Revision Initiation** | As Author, create new revision `SMK-PROD-REV-01` on an existing effective document. | Revision created in `Draft` state; parent document remains `Effective`; change summary recorded. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-07** | **Technical Review Workflow** | Submit draft for technical review; log in as Reviewer; add review feedback comment. | Review task created; review comments recorded with reviewer identity and UTC timestamp. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-08** | **Approval Routing & Dossier** | Complete review; route revision to formal approval workflow with designated Approver. | Approval dossier generated; approval tasks created for designated approver. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-09** | **21 CFR Part 11 E-Signature** | As Approver, initiate approval. Enter password, select signing reason `Approval`, submit. | Dual-factor challenge verified; signature manifest displays printed name, UTC time, and reason. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-10** | **Segregation of Duties Check** | Attempt to sign/approve revision using the authoring user's account. | System blocks action with hard error: `AUTHOR_CANNOT_APPROVE_OWN_DOCUMENT`; SoD enforced. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-11** | **Future Effective Date State** | Set effective date to 2 hours in future during final approval. | Document status transitions to `Approved`; status remains `Approved` (not yet `Effective`). | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-12** | **Effective Date Worker Trigger** | Verify background worker log output and simulated arrival of effective timestamp. | `EffectiveDateWorker` transitions status to `Effective`; prior revision marked `Superseded`. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-13** | **Periodic Review Engine** | Open Document Details for newly effective document. Inspect Periodic Review panel. | Next review date automatically calculated based on document category cycle (e.g., +12 months). | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-14** | **Audit Trail & Attribution** | Open system Audit Trail for test document. Inspect all generated audit events. | All smoke actions recorded chronologically with UTC times, user IDs, and worker actor `system:effective-date-worker`. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |

---

### 4. Post-Smoke Test Disposition & Release Sign-Off

Upon completion of all 14 smoke test checkpoints, the CSV Test Engineer and Quality Assurance Lead must record formal disposition below:

```
====================================================================================================
SMOKE TEST PROTOCOL EXECUTION DISPOSITION
====================================================================================================

Overall Result:   [ ] ACCEPTED (All 14 checkpoints PASSED - System released to production users)
                  [ ] REJECTED (Failures detected - Initiate immediate rollback protocol)

Deviations Observed: _______________________________________________________________________________

Executed By:      ____________________________________________________ [CSV Test Engineer]
Date & Time:      ____________________________________________________

Verified By:      ____________________________________________________ [QA Operations Lead]
Date & Time:      ____________________________________________________
====================================================================================================
```
