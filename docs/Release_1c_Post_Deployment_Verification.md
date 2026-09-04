# MicroLIMS Document Control — Release 1c
## Post-Deployment Verification Protocol & Pre-Release Smoke Test Plan

- **Document Identifier:** `ML-DC-R1C-SMK-001`
- **Release Version:** MicroLIMS v1.2.0-rel1c
- **Module:** Document Control Module (Release 1c)
- **Target Execution Environment:** Newly Deployed Production Environment
- **Controlled Change Reference:** `CC-DC-R1C-001`
- **Date:** September 4, 2026
- **Protocol Status:** **CONTROLLED POST-DEPLOYMENT SMOKE PROTOCOL SPECIFICATION**
- **Prerequisite:** Completion of Production Deployment Runbook (`ML-DC-R1C-DEP-001`) and Formal Release Approval

---

### 1. Protocol Purpose & Execution Rules

This protocol specifies the mandatory twelve-point (12-point) non-destructive post-deployment smoke test suite to be executed immediately following an authorized production deployment of Release 1c.

#### Execution Rules:
1. **Non-Destructive Invariant:** No production analytical data shall be overwritten or corrupted.
2. **Controlled Test Data:** Any test documents or reading assignments created for smoke testing must use the prefix `SMK-R1C-` and be assigned to dedicated smoke-test accounts.
3. **No Direct Trigger Testing in Production:** Immutability triggers must NOT be tested in production by executing failing SQL statements on live records; database trigger status shall be verified via system catalog inspection (`information_schema.triggers`).
4. **Pass Criterion:** 100% of the 12 smoke test checkpoints must achieve `PASS`. Any failure triggers immediate operational escalation and potential rollback.

---

### 2. Twelve-Point Post-Deployment Smoke Test Suite

| Test ID | Inspection Focus | Verification Action & Procedure | Expected Acceptance Criteria | Result | Initials & Date |
|:---:|:---|:---|:---|:---:|:---|
| **SMK-1C-01** | **API Health & Service Availability** | Send HTTP GET to `/health`. | Returns HTTP 200 OK with `{"status":"Healthy"}`. Database connection active. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-1C-02** | **Authentication & Scoping** | Authenticate via `/login` as Analyst (`analyst.qc`). | Valid JWT issued. User claims reflect assigned role and department. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-1C-03** | **My Reading List Navigation** | Navigate to `/document-control/my-reading`. | Page renders cleanly with tabbed views: "Pending Reading" and "Completed Reading". | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-1C-04** | **Controlled PDF Viewer Launch** | Click "Read Document" on assigned SOP. | Controlled viewer opens; watermarked PDF renders with correct revision number. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-1C-05** | **Informational Reading Progress** | Scroll through 100% of SOP pages. | Reading progress bar displays 100%; assignment status transitions to `Reading` (NOT completed). | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-1C-06** | **Conscious Electronic Acknowledgement** | Open acknowledgement dialog; check confirmation box; submit. | Immutable record persisted; assignment moves to "Completed"; cell turns Green (`Qualified`). | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-1C-07** | **Reading Assignment Scoping** | Query `/api/document-control/training-assignments/my-assignments`. | Returns strictly assignments where `AssignedUserId == CurrentUserId`. Zero cross-user leakage. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-1C-08** | **Overdue & Escalation Status** | Inspect assignment with past due date as Supervisor. | Red `Overdue` badge displayed; Level 1 escalation record queryable. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-1C-09** | **Multi-Axis Training Matrix** | Navigate to `/document-control/training-matrix`. | Personnel × Documents grid renders; color-coded cells visible; filter dropdowns populated. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-1C-10** | **Compliance Dashboard KPIs** | Navigate to `/document-control/compliance-dashboard`. | Executive KPI cards display overall compliance %, department rankings, and top overdue docs. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-1C-11** | **Semantic Audit Trail Attribution** | Open Audit Trail for acknowledged SOP. | Audit log captures user event with UTC timestamp, actor ID, and exact statement text. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |
| **SMK-1C-12** | **Background Worker Health** | Inspect application logs for `DocumentEffectiveDateWorker`. | Worker logs periodic evaluation cycle; zero unhandled lock contention or crash loops. | `[ ] PASS`<br>`[ ] FAIL` | Initials: ______<br>Date: _________ |

---

### 3. Post-Verification Sign-Off & Release Acceptance

```
====================================================================================================
POST-DEPLOYMENT SMOKE VERIFICATION DISPOSITION
====================================================================================================
Overall Smoke Result:   [ ] ACCEPTED (All 12 checkpoints PASS — System released for live use)
                        [ ] REJECTED (Defect detected — Halt release and initiate rollback)

Executed By (CSV / QA Tester): ________________________________________________ Date: ____________
Verified By (Lead QA Director): ________________________________________________ Date: ____________
====================================================================================================
```
