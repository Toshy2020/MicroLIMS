# MicroLIMS Document Control — Release 1b
## Work Package 9: Formal Pre-Qualification Baseline Record

- **Document Identifier:** `ML-DC-WP9-BASE-001`
- **Release:** Release 1b
- **Work Package:** WP9 — Formal OQ/UAT Qualification & Release 1b Validation Closure
- **Date:** 2026-09-04
- **Qualification Lead:** Automated Controlled Systems Verification Agent
- **Baseline Authority:** MicroLIMS Document Control URS v1.1 / ML-DC-FRS-1B-001 / ML-DC-RTM-1B-001
- **Status:** **PRE-QUALIFICATION FROZEN**

---

### 1. Pre-Qualification Freeze & Environment Identification

Prior to executing formal Operational Qualification (OQ) and User Acceptance Testing (UAT), an authoritative qualification baseline snapshot was recorded and frozen.

#### 1.1 Software & Build Configuration
- **Application:** MicroLIMS Enterprise Laboratory Information Management System
- **Module:** Document Control (Release 1b)
- **Git Commit Baseline:** `6fe5a61` (`fix(frontend): enforce production Render API fallback on web hosts`)
- **Target Framework:** .NET 8.0 (`net8.0`)
- **Backend Build Status:** 100% Compiled, clean build
- **Frontend Build Status:** Vite v5.4.21 production build passed (`dist/assets/index-BJP3wpKa.js`, 0 errors)
- **Node.js / TypeScript:** TypeScript 5.5 (`tsc -b` clean pass)
- **Pre-Qualification Test Baseline:** **712 passed, 0 failed, 0 skipped** (45 seconds duration)

#### 1.2 Database & Data Store Baseline
- **Database Engine:** PostgreSQL 16 (Relational DB with relational foreign keys, database sequences, and PL/pgSQL audit immutability triggers)
- **Database Name:** `microlims_doccontrol_wp1_test`
- **Migration State (7 Migrations Fully Applied):**
  1. `20260902181229_AddDocumentControlEntities.cs`
  2. `20260902181230_AddAuditImmutabilityTriggers.cs`
  3. `20260902181231_AddDatabaseSequences.cs`
  4. `20260902211621_AddDocumentReviewEntities.cs`
  5. `20260902214555_AddRevisionManagementEntities.cs`
  6. `20260904072635_AddDocumentApprovalTaskEntity.cs`
  7. `20260904085622_AddPeriodicReviewEntities.cs`

#### 1.3 Authoritative Specifications & Protocols
- **URS:** MicroLIMS Document Control URS v1.1 (203 total requirements; 43 Release 1b requirements: `DC-URS-044` through `DC-URS-079`, `DC-URS-177` through `DC-URS-183`)
- **FRS:** `ML-DC-FRS-1B-001` (Functional Specification for Release 1b)
- **Risk Assessment:** `Release_1b_Risk_Impact_Assessment.md` (`ML-DC-R1B-RA-001`)
- **RTM:** `Release_1b_Requirements_Traceability_Matrix.md` (`ML-DC-RTM-1B-001`)
- **OQ/UAT Protocol Plan:** `Release_1b_OQ_UAT_Plan.md` (`ML-DC-R1B-OQ-UAT-PLAN-001`)
- **WP8 Completion Report:** `Release_1b_WP8_Integration_Verification_Report.md` (`ML-DC-WP8-RPT-001`)

#### 1.4 Test User Roles & Controlled Personas
- **Document Author:** `Analyst` role (`author_user`)
- **Technical Reviewer:** `Reviewer` role (`tech_reviewer`)
- **Document Controller / Approver:** `SectionHead` role (`controller_user`, `qa_approver`)
- **System Administrator:** `SystemAdministrator` role (`sys_admin`)

#### 1.5 System Time & UTC Synchronization
- Strict UTC evaluation across all lifecycle transactions (`DateTime.UtcNow`).

---
**Status:** BASELINE FROZEN FOR WP9 OQ/UAT QUALIFICATION EXECUTION.
