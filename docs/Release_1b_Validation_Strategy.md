# Release 1b Validation Strategy & Compliance Framework

**Document ID:** ML-DC-R1B-VAL-001  
**Version:** 1.0  
**Status:** Approved Validation Strategy  
**Module:** Document Control (Release 1b)  
**System:** MicroLIMS Enterprise Laboratory Information Management System  
**Authoritative Baseline:** MicroLIMS Document Control Project Charter, URS v1.1, FRS ML-DC-FRS-1B-001, Risk Impact Assessment ML-DC-R1B-RA-001  
**Regulatory Context:** 21 CFR Part 11, EU GMP Annex 11, PIC/S PE 009-17, GAMP 5 (Second Edition)  
**Date:** September 3, 2026  

---

## 1. Purpose & Validation Scope

This **Validation Strategy** establishes the formal quality and regulatory compliance approach for **Release 1b** of the MicroLIMS Document Control module. It defines the validation lifecycle, risk-based testing strategy, evidence management, defect/deviation handling protocols, and final acceptance criteria required for formal regulatory closure.

### Validation Scope:
- **In-Scope for Release 1b:**
  - Technical Review workflow, findings management, and reviewer concurrence gates (`DC-URS-066`..`073`, `078`, `079`).
  - Revision creation from effective documents, numbering proposals, and structured impact assessments (`DC-URS-055`..`065`).
  - Formal approval routing, evidence dossiers, and 21 CFR Part 11 electronic signatures (`DC-URS-073`..`077`).
  - Idempotent effective date automation and supersession background processing (`DC-URS-177`, `DC-URS-178`, `DC-URS-180`..`183`).
  - Periodic review engine, outcome processing, finding transfers, and overdue monitoring (`DC-URS-044`..`054`, `DC-URS-179`).
  - Hard server-side Segregation of Duties (SoD) enforcement (`DC-URS-164`..`169`).
- **Strictly Out-of-Scope (Deferred):**
  - Reading Lists and Training Matrix (Release 1c).
  - Knowledge Assessment Forms (Release 1d).
  - Legacy Migration Wizard (Release 1e).
  - Analytical Testing Execution Workspaces integration (`FS-1a-105` Phase 1 independence preserved).

---

## 2. Risk-Based Testing Strategy (GAMP 5 / ICH Q9)

Testing rigor is directly dictated by the failure risk classification established in [`ML-DC-R1B-RA-001`](file:///E:/MicroLIMS/MicroLIMS/docs/Release_1b_Risk_Impact_Assessment.md):

```mermaid
quadrantChart
    title Risk Priority vs. Verification Rigor
    x-axis Low Severity --> High Severity
    y-axis High Detectability (Obvious) --> Low Detectability (Silent)
    quadrant-1 Critical (Negative + Part 11 + DB Tests)
    quadrant-2 High (Formal OQ + Integration)
    quadrant-3 Low (UI Display Checks)
    quadrant-4 Medium (Functional Tests)
    "E-Signature Password Re-auth": [0.85, 0.85]
    "SoD Author!=Approver": [0.90, 0.80]
    "Mandatory Comment Gate": [0.80, 0.75]
    "Worker Auto-Activation": [0.85, 0.88]
    "Review Findings Transfer": [0.55, 0.45]
    "Major/Minor Numbering": [0.45, 0.35]
    "Overdue Badge Rendering": [0.25, 0.15]
```

- **Critical Controls (Tier 1):** Verified through automated negative integration tests under live PostgreSQL, proving that invalid actions are physically blocked by business guards and database triggers.
- **Functional Workflows (Tier 2):** Verified through structured OQ test cases demonstrating deterministic state machine progression and audit trail completeness.
- **User Experience (Tier 3):** Verified through realistic laboratory UAT scripts executed by simulated QC Microbiology roles.

---

## 3. Evidence Collection & ALCOA+ Integrity Standards

To meet the requirements of 21 CFR Part 11 and EU Annex 11, all qualification activities must generate contemporaneous, verifiable evidence:
1. **Automated Test Logs:** Every test run outputs machine-readable logs and summary outputs with exact UTC execution timestamps, test names, and assertion outcomes.
2. **Database Traceability:** State transitions, foreign key linkages, and trigger immutability verified through direct SQL query assertions against PostgreSQL.
3. **Artifact Immutability:** Generated evidence files, test reports, and completion documents are version-controlled and stored under `./docs` with baseline status metadata.
4. **Audit Trail Verification:** Every test validating a business workflow must query `AuditLogs` and `AuditEventChanges` to verify that the generated audit records accurately capture the actor, timestamp, action code, and field diffs.

---

## 4. Defect and Deviation Handling Protocol

Any unexpected behavior, assertion failure, or specification variance during Release 1b verification must follow the established defect governance procedure:

### 4.1 Severity Classification
| Severity Tier | Definition | Resolution Mandate | Release Blocker? |
|---|---|---|:---:|
| **Critical** | Security vulnerability, data loss, audit failure, or SoD bypass. | Immediate code fix, root cause analysis, full regression test. | **YES (Mandatory)** |
| **High** | Functional failure blocking a requirement workflow with no workaround. | Code fix and verification before advancing to next work package. | **YES (Mandatory)** |
| **Medium** | Functional defect with an approved manual workaround; UI misalignment. | Resolved within current release before OQ completion. | **YES (for Closure)** |
| **Low** | Minor cosmetic, typo, or non-functional documentation inconsistency. | Evaluated by QA; can be resolved or documented as known limitation. | No |

### 4.2 Deviation Protocol
- Any variance between actual test execution and planned protocol steps must be recorded in the formal **Deviation Log**.
- The deviation must document: Description, Root Cause, Impact Assessment on GxP Data Integrity, Corrective Action, and QA Lead Sign-Off.
- Unresolved Critical or High deviations prohibit release baseline sign-off.

---

## 5. Bidirectional Traceability Expectations (RTM)

The Requirements Traceability Matrix ([`ML-DC-RTM-1B-001`](file:///E:/MicroLIMS/MicroLIMS/docs/Release_1b_Requirements_Traceability_Matrix.md)) serves as the central validation index.
- Every Release 1b requirement (`DC-URS-044`..`079`, `177`..`183`) must map bidirectionally:
  $$\text{URS} \longleftrightarrow \text{FRS} \longleftrightarrow \text{Code / Entity} \longleftrightarrow \text{API} \longleftrightarrow \text{Automated Test} \longleftrightarrow \text{OQ Case} \longleftrightarrow \text{UAT Scenario}$$
- **Zero Orphaned Code:** Every new endpoint, service method, and database table must trace back to a specific approved requirement.
- **Zero Unmapped Requirements:** All 43 requirements must have documented test verification evidence.

---

## 6. Formal Validation Closure & Release Acceptance Criteria

Release 1b will be formally accepted and baselined only when all of the following conditions are satisfied:
1. **100% Work Package Completion:** Work Packages 1 through 9 completed and reported.
2. **Automated Test Pass Rate:** 100% pass rate on full regression suite (both Release 1a and Release 1b tests green; 0 failures).
3. **Operational Qualification:** All 28 OQ test cases passed with documented evidence.
4. **User Acceptance Testing:** All 8 laboratory UAT scenarios accepted without reservation.
5. **Zero Open Defects:** 0 Critical, 0 High, and 0 Medium defects remaining in the defect log.
6. **Production Build Cleanliness:** Frontend production bundle compiles cleanly with 0 TypeScript errors.
7. **Traceability Reconciliation:** 100% of requirements marked `QUALIFIED` in the RTM.
8. **Formal Approvals:** Validation Summary Report signed by System Owner, Lead Architect, and Quality Assurance Lead.
