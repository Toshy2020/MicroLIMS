# Release 1c Requirements Traceability Matrix (RTM)

**Document ID:** `ML-DC-RTM-1C-001`  
**Version:** 2.0 (Formal Qualification Baseline — WP9)  
**Status:** **FORMALLY QUALIFIED FOR IMPLEMENTED BASELINE (30 QUALIFIED / 5 PLANNED)**  
**Module:** Document Control (Release 1c: Training Matrix & Reading Lists)  
**Authoritative Baseline:** MicroLIMS Document Control URS v1.1 (`DC-URS-080`..`091`, `DC-URS-111`..`122`, `DC-URS-170`..`176`, `DC-URS-189`..`192`)  
**Functional Specification Baseline:** `ML-DC-FRS-1C-001`  
**Risk Assessment Baseline:** `ML-DC-R1C-RA-001`  
**Controlled Change Baseline:** `CC-DC-R1C-001`  
**Date:** September 4, 2026  

---

### 1. Traceability Methodology & Work Package Phasing Rule

This Requirements Traceability Matrix establishes bidirectional traceability for all **35 requirements** of Release 1c across the complete software development lifecycle:
$$\text{URS} \longrightarrow \text{FRS} \longrightarrow \text{Design / Component} \longrightarrow \text{API / Service} \longrightarrow \text{DB Behavior} \longrightarrow \text{Frontend UI} \longrightarrow \text{Automated Tests} \longrightarrow \text{OQ / UAT Target} \longrightarrow \text{Final Qualification Status}$$

**MANDATORY QUALIFICATION RULES:**  
1. A requirement is marked **QUALIFIED** only when:
   - It is fully implemented and tested.
   - It is verified in the integrated staging environment (WP8).
   - Its formal OQ protocol execution passes with documented evidence (`Release_1c_OQ_Execution_Record.md`).
   - Its operational UAT workflow passes with user acceptance (`Release_1c_UAT_Execution_Record.md`).
   - Zero open deviations exist against its functional scope.
2. Requirements not implemented in Release 1c remain strictly:
   $$\text{IMPLEMENTED = NO} \quad|\quad \text{INTEGRATION VERIFIED = NO} \quad|\quad \text{QUALIFIED = NO} \quad|\quad \text{STATUS = PLANNED}$$
3. Release 1b production baseline functionality remains frozen and completely protected.

---

### 2. Comprehensive Traceability Matrix (35 Requirements)

| URS ID | Requirement Title & Functional Summary | Responsible WP | Implementation Status | Integration Verification (WP8) | OQ Protocol / Result | UAT Protocol / Result | Final Qualification Status | Evidence Reference |
|:---|:---|:---:|:---:|:---:|:---:|:---:|:---:|:---|
| **DC-URS-080** | Auto-generation of reading assignments upon revision effective | WP2 | **YES** | **PASS (Scenario A, H)** | `OQ-1C-01` / **PASS** | `UAT-1C-09` / **ACCEPTED** | **QUALIFIED** | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario A) |
| **DC-URS-081** | Manual training assignment to individual users | WP2/WP5 | **YES** | **PASS (Scenario A, F)** | `OQ-1C-02` / **PASS** | `UAT-1C-01` / **ACCEPTED** | **QUALIFIED** | `DocumentControlTrainingAssignmentEngineUnitTests` |
| **DC-URS-082** | Group/department bulk reading assignments | WP2/WP5 | **YES** | **PASS (Scenario A)** | `OQ-1C-02` / **PASS** | `UAT-1C-01` / **ACCEPTED** | **QUALIFIED** | `DocumentControlTrainingAssignmentEnginePostgresIntegrationTests` |
| **DC-URS-083** | Grace period hierarchy calculation for due dates | WP2/WP5 | **YES** | **PASS (Scenario A, C)** | `OQ-1C-03` / **PASS** | `UAT-1C-01` / **ACCEPTED** | **QUALIFIED** | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario A) |
| **DC-URS-084** | Automatic overdue status identification & escalation trigger | WP4/WP5 | **YES** | **PASS (Scenario C, H)** | `OQ-1C-04` / **PASS** | `UAT-1C-06` / **ACCEPTED** | **QUALIFIED** | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario C) |
| **DC-URS-085** | "My Reading List" authenticated personal workspace view | WP5/WP6 | **YES** | **PASS (Scenario B, F)** | `OQ-1C-05` / **PASS** | `UAT-1C-02` / **ACCEPTED** | **QUALIFIED** | `MyReadingListPage.tsx`, `Release1cIntegrationVerificationPostgresTests.cs` |
| **DC-URS-086** | Read-and-understand electronic acknowledgement ceremony | WP3/WP5/WP6 | **YES** | **PASS (Scenario B, F)** | `OQ-1C-07` / **PASS** | `UAT-1C-05` / **ACCEPTED** | **QUALIFIED** | `DocumentControlAcknowledgementPostgresIntegrationTests` |
| **DC-URS-087** | Direct launch of Controlled PDF Viewer from reading list | WP6 | **YES** | **PASS (Scenario B)** | `OQ-1C-06` / **PASS** | `UAT-1C-03` / **ACCEPTED** | **QUALIFIED** | `ControlledPdfViewer.tsx`, Frontend Production Build |
| **DC-URS-088** | Reading list status filtering & document search | WP5/WP6 | **YES** | **PASS (Scenario B)** | `OQ-1C-05` / **PASS** | `UAT-1C-02` / **ACCEPTED** | **QUALIFIED** | `MyReadingListPage.tsx`, `DocumentControlRestApiUnitTests` |
| **DC-URS-089** | Visual due-date countdown and overdue status badges | WP5/WP6 | **YES** | **PASS (Scenario B, C)** | `OQ-1C-08` / **PASS** | `UAT-1C-02` / **ACCEPTED** | **QUALIFIED** | `DocumentTrainingAssignmentDto`, `MyReadingListPage.tsx` |
| **DC-URS-090** | Historical retention of completed revision reading evidence | WP3/WP5/WP6 | **YES** | **PASS (Scenario D, E)** | `OQ-1C-09` / **PASS** | `UAT-1C-09`, `UAT-1C-10` / **ACCEPTED** | **QUALIFIED** | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario D) |
| **DC-URS-091** | Training completion metrics export utility | Post-R1c / Tools | **NO** | **NO** | N/A | N/A | **PLANNED** | *Out of Current Implemented Baseline* |
| **DC-URS-111** | Multi-axis Training Matrix view (Users × Documents) | WP7 | **YES** | **PASS (Scenario D, F)** | `OQ-1C-14` / **PASS** | `UAT-1C-07` / **ACCEPTED** | **QUALIFIED** | `TrainingMatrixPage.tsx`, `TrainingMatrixService.cs` |
| **DC-URS-112** | Color-coded matrix qualification status & cell drilldowns | WP7 | **YES** | **PASS (Scenario D)** | `OQ-1C-14` / **PASS** | `UAT-1C-07` / **ACCEPTED** | **QUALIFIED** | `TrainingMatrixPage.tsx`, `DocumentControlTrainingMatrixUnitTests` |
| **DC-URS-113** | Training Matrix filtering by department and role | WP7 | **YES** | **PASS (Scenario D, F)** | `OQ-1C-14` / **PASS** | `UAT-1C-07` / **ACCEPTED** | **QUALIFIED** | `TrainingMatrixPage.tsx`, `DocumentControlTrainingMatrixUnitTests` |
| **DC-URS-114** | Explicit identification of superseded-only qualification status | WP2/WP5/WP6/WP7 | **YES** | **PASS (Scenario D)** | `OQ-1C-12` / **PASS** | `UAT-1C-09` / **ACCEPTED** | **QUALIFIED** | `TrainingMatrixService.cs`, `Release1cIntegrationVerificationPostgresTests.cs` |
| **DC-URS-115** | Role-based required training curricula evaluation | WP2 | **YES** | **PASS (Scenario A)** | `OQ-1C-03` / **PASS** | `UAT-1C-01` / **ACCEPTED** | **QUALIFIED** | `DocumentControlTrainingAssignmentEnginePostgresIntegrationTests` |
| **DC-URS-116** | Overdue training alerts in manager/supervisor view | WP4/WP5/WP7 | **YES** | **PASS (Scenario C)** | `OQ-1C-04` / **PASS** | `UAT-1C-06` / **ACCEPTED** | **QUALIFIED** | `ComplianceDashboardPage.tsx`, `DocumentControlEscalationPostgresIntegrationTests` |
| **DC-URS-117** | Automated training assignment on user onboarding | Post-R1c / Tools | **NO** | **NO** | N/A | N/A | **PLANNED** | *Out of Current Implemented Baseline* |
| **DC-URS-118** | Organizational training compliance percentage KPI card | WP7 | **YES** | **PASS (Scenario B, D)** | `OQ-1C-15` / **PASS** | `UAT-1C-08` / **ACCEPTED** | **QUALIFIED** | `ComplianceDashboardPage.tsx`, `TrainingMatrixService.cs` |
| **DC-URS-119** | Department-level training compliance ranking and breakdown | WP7 | **YES** | **PASS (Scenario D)** | `OQ-1C-15` / **PASS** | `UAT-1C-08` / **ACCEPTED** | **QUALIFIED** | `ComplianceDashboardPage.tsx`, `TrainingMatrixService.cs` |
| **DC-URS-120** | Top overdue documents reporting widget | WP7 | **YES** | **PASS (Scenario C, D)** | `OQ-1C-15` / **PASS** | `UAT-1C-08` / **ACCEPTED** | **QUALIFIED** | `ComplianceDashboardPage.tsx`, `TrainingMatrixService.cs` |
| **DC-URS-121** | Audit logging of training report generation | Post-R1c / Tools | **NO** | **NO** | N/A | N/A | **PLANNED** | *Out of Current Implemented Baseline* |
| **DC-URS-122** | Self-auditing CSV/PDF matrix export with SHA-256 integrity | Post-R1c / Tools | **NO** | **NO** | N/A | N/A | **PLANNED** | *Out of Current Implemented Baseline* |
| **DC-URS-170** | Automatic retraining cascade upon new revision effective date | WP2 | **YES** | **PASS (Scenario D, H)** | `OQ-1C-01`, `OQ-1C-11` / **PASS** | `UAT-1C-09` / **ACCEPTED** | **QUALIFIED** | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario D) |
| **DC-URS-171** | Configurable retraining necessity rules by document type | WP2 | **YES** | **PASS (Scenario D)** | `OQ-1C-01` / **PASS** | `UAT-1C-01` / **ACCEPTED** | **QUALIFIED** | `DocumentControlTrainingAssignmentEnginePostgresIntegrationTests` |
| **DC-URS-172** | Terminal closure of incomplete superseded assignments | WP2/WP6 | **YES** | **PASS (Scenario D)** | `OQ-1C-10` / **PASS** | `UAT-1C-09` / **ACCEPTED** | **QUALIFIED** | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario D) |
| **DC-URS-173** | Permanent retention of completed superseded training records | WP3/WP5/WP6 | **YES** | **PASS (Scenario D, E, G)** | `OQ-1C-09`, `OQ-1C-17` / **PASS** | `UAT-1C-09`, `UAT-1C-10` / **ACCEPTED** | **QUALIFIED** | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario D, G) |
| **DC-URS-174** | Explicit badge & gap tracking for superseded-only training | WP2/WP5/WP6/WP7 | **YES** | **PASS (Scenario D)** | `OQ-1C-12` / **PASS** | `UAT-1C-09` / **ACCEPTED** | **QUALIFIED** | `TrainingMatrixService.cs`, `Release1cIntegrationVerificationPostgresTests.cs` |
| **DC-URS-175** | Obsolete document master transition cancels open training | WP2/WP6 | **YES** | **PASS (Scenario E)** | `OQ-1C-13` / **PASS** | `UAT-1C-10` / **ACCEPTED** | **QUALIFIED** | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario E) |
| **DC-URS-176** | Re-issuance of assignment on failed state | Post-R1c / Tools | **NO** | **NO** | N/A | N/A | **PLANNED** | *Out of Current Implemented Baseline* |
| **DC-URS-189** | Explicit conscious acknowledgement is primary legal evidence | WP3/WP5/WP6 | **YES** | **PASS (Scenario B, G)** | `OQ-1C-07`, `OQ-1C-17` / **PASS** | `UAT-1C-05` / **ACCEPTED** | **QUALIFIED** | `DocumentControlAcknowledgementPostgresIntegrationTests` |
| **DC-URS-190** | Informational-only reading progress tracking (no auto-sign) | WP3/WP5/WP6 | **YES** | **PASS (Scenario B)** | `OQ-1C-06` / **PASS** | `UAT-1C-04` / **ACCEPTED** | **QUALIFIED** | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario B) |
| **DC-URS-191** | Configurable legal acknowledgement statement text | WP3/WP5/WP6 | **YES** | **PASS (Scenario B)** | `OQ-1C-07` / **PASS** | `UAT-1C-05` / **ACCEPTED** | **QUALIFIED** | `DocumentControlAcknowledgementPostgresIntegrationTests` |
| **DC-URS-192** | Acknowledgement ceremony strictly bound to assigned revision | WP3/WP5/WP6 | **YES** | **PASS (Scenario B, F)** | `OQ-1C-07`, `OQ-1C-16` / **PASS** | `UAT-1C-05` / **ACCEPTED** | **QUALIFIED** | `Release1cIntegrationVerificationPostgresTests.cs` (Scenario B, F) |

---

### 3. Final Traceability Reconciliation Summary

- **Total Release 1c Requirements:** **35 Requirements (100%)**
- **Total Requirements Implemented (WP1–WP8):** **30 of 35 (85.7%)**
- **Total Requirements Integration-Verified (WP8):** **30 of 30 (100% of Implemented)**
- **Total Requirements Operational-Qualified (WP9 OQ):** **30 of 30 (100% of Implemented)**
- **Total Requirements User-Acceptance Tested (WP9 UAT):** **30 of 30 (100% of Implemented)**
- **Final Implemented Scope Qualification Status:** **30 of 30 Implemented Requirements QUALIFIED**
- **Qualification Gaps on Implemented Scope:** **0 (Zero)**
- **Requirements Remaining Out of Baseline (PLANNED):** **Exactly 5 of 35 Requirements (14.3%)**
  1. `DC-URS-091` — Training completion metrics export utility
  2. `DC-URS-117` — Automated training assignment on user onboarding
  3. `DC-URS-121` — Audit logging of training report generation
  4. `DC-URS-122` — Self-auditing CSV/PDF matrix export with SHA-256 integrity
  5. `DC-URS-176` — Retraining assignment re-issuance rules on failed state
- **Planned Requirements Qualification Status:** **0 Qualified (Strictly Excluded from Release 1c Qualification)**
- **Release 1b Production Baseline Protection:** **100% FROZEN AND VERIFIED UNREGRESSED**
