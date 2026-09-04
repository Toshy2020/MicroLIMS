# MicroLIMS Document Control — Release 1c
## Controlled Planning Readiness Summary: Training Matrix & Reading Lists

- **Document Identifier:** `ML-DC-R1C-RDY-001`
- **Release Version:** MicroLIMS v1.2.0-rel1c (Controlled Planning Package)
- **Module:** Document Control Module — Subsystem: Training & Reading Lists
- **Date:** 2026-09-04
- **Planning Status:** **PLANNING COMPLETE — AWAITING FORMAL IMPLEMENTATION AUTHORIZATION**
- **Controlled Baseline:** 35 In-Scope Requirements (`DC-URS-080`..`091`, `DC-URS-111`..`122`, `DC-URS-170`..`176`, `DC-URS-189`..`192`)

---

### 1. Executive Planning Summary

This Readiness Summary consolidates the controlled planning package prepared for **MicroLIMS Document Control Release 1c**.

Release 1c establishes personnel qualification on controlled documents, encompassing reading assignments, the "My Reading List" user portal, controlled read-and-understand acknowledgement ceremonies, a multi-axis Training Matrix, automated retraining cascades, and compliance KPI reporting.

**Key Governance Finding:**  
The planning package is complete, internally consistent, and fully reconciled against MicroLIMS Document Control URS v1.1. No implementation code, database migrations, or production changes have been introduced.

---

### 2. Consolidated Planning Scorecard

| Planning Dimension | Baseline Specification / Evaluation Criteria | Finding / Strategy Prepared | Readiness Ruling |
|:---|:---|:---|:---:|
| **1. Requirements Scope** | 35 Authoritative URS requirements identified from URS v1.1 | All 35 requirements specified in FRS and mapped in RTM | **COMPLETE** |
| **2. Architectural Design** | Clean Architecture; entity models and persistence structures | Proposed `ReadingAssignment`, `RoleCurriculum` models | **READY** |
| **3. Reusable 1b Assets** | Maximum reuse of validated Release 1b foundation | Reuses `DocumentMaster`, `DocumentRevision`, `EffectiveDateWorker` | **READY** |
| **4. Release 1b Boundary** | Zero unauthorized modification of qualified Release 1b baseline | Single minimal hook in worker identified as formal CC | **PROTECTED** |
| **5. Cross-Module Isolation**| No coupling to laboratory execution modules | Zero dependencies on Testing, Water, EM, Media, Receiving | **ENFORCED** |
| **6. GxP Risk Management** | FMEA assessment across failure modes (proxy, evidence loss) | 11 hazards assessed; all residual risks rated LOW | **COMPLIANT** |
| **7. Work Package Phasing** | Controlled decomposition with entry/exit gates | 8 sequential work packages (WP1 through WP8) | **DEFINED** |
| **8. Validation Rigor** | Validation strategy aligned with GAMP 5 Category 5 | Risk-based testing, 10 OQ protocols, 3 UAT scenarios | **ESTABLISHED** |
| **9. Traceability Status** | Bidirectional traceability across all 35 requirements | 100% of requirements baselined as `PLANNED` | **VERIFIED** |

---

### 3. Release 1c Work Package Implementation Sequence

The recommended order of execution upon receipt of formal implementation authorization is:

1. **WP1 — Reading Assignment Foundation & Persistence:** Entity models, schema migrations, and core assignment service.
2. **WP2 — Role Curricula & Group Assignment Distribution:** Curricula mapping and automated onboarding assignments.
3. **WP3 — Training Cascade & Revision Retraining Engine:** Automatic retraining generation upon status $\rightarrow$ `Effective` and terminal closure of superseded open assignments.
4. **WP4 — Training Matrix Multi-Axis Engine:** High-performance aggregation service computing qualification states across Users × Documents × Revisions.
5. **WP5 — "My Reading List" Experience & Acknowledgement Modal:** User portal, countdown badges, and controlled read-and-understand modal.
6. **WP6 — Compliance Reporting, Executive KPIs & Matrix UI:** Interactive Training Matrix frontend page, department rankings, and self-auditing CSV/PDF export.
7. **WP7 — Full Lifecycle Integration Verification:** Comprehensive automated regression testing (750+ tests) against live PostgreSQL.
8. **WP8 — Formal OQ/UAT Qualification & Release 1c Closure:** Formal execution of `ML-DC-R1C-OQP-001`, RTM baselining to `QUALIFIED`, and final validation closure.

---

### 4. Technical Dependencies & Release 1b Protection

#### 4.1 Reusable Release 1b Components
- `DocumentMaster` & `DocumentRevision` relational models.
- `MicroLimsDbContext` with PostgreSQL 16 persistence provider.
- `IAuditEventService` semantic audit logging with UTC standardization.
- `ControlledPdfViewer` with watermarked header streaming.
- JWT bearer authentication and role-based permission policies.

#### 4.2 Release 1b Controlled Change Requirement
- **Identified Change:** Hook in `DocumentEffectiveDateWorker` or domain event dispatching when a revision becomes `Effective` to trigger the Release 1c Training Cascade.
- **Controlled Change Record:** To be logged as `CC-DC-R1C-001`.
- **Regression Control:** The worker hook will be designed defensively with null checks, ensuring zero impact on Release 1b core operations if Release 1c training services are not registered.

---

### 5. Unresolved Governance Decisions & Open Items

Prior to initiating active code changes in Work Package 1, organizational stakeholders must formalize the following configuration decisions:
1. **Default Reading Grace Period:** Standard number of calendar days allowed for reading completion (e.g., 14 days vs. 30 days) before an assignment is flagged as `Overdue`.
2. **Mandatory Acknowledgement Text:** Formal corporate legal statement text to be displayed and captured during acknowledgement.
3. **Department Notification Policies:** Frequency and escalation recipients for overdue training alerts (e.g., weekly summary to Section Heads).

---

### 6. Controlled Planning Conclusion

The planning package for **MicroLIMS Document Control Release 1c** is fully assembled, internally consistent, and ready for review.

**MANDATORY GOVERNANCE RESTRICTION:**  
*No implementation, coding, database migrations, or production changes have been performed. Implementation of Release 1c remains strictly stopped pending formal organizational authorization.*
