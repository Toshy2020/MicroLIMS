# MicroLIMS Document Control — Release 1c
## Master Implementation Plan: Training Matrix & Reading Lists

- **Document Identifier:** `ML-DC-R1C-IMP-001`
- **Release Version:** MicroLIMS v1.2.0-rel1c (Controlled Planning Baseline)
- **Module:** Document Control Module — Subsystem: Training & Reading Lists
- **Date:** 2026-09-04
- **Planning Status:** **PLANNING ONLY (NO ACTIVE CODE MODIFICATION)**
- **Authoritative Scope:** 35 Requirements (`DC-URS-080`..`091`, `DC-URS-111`..`122`, `DC-URS-170`..`176`, `DC-URS-189`..`192`)

---

### 1. Work Package Decomposition & Phasing

Release 1c is partitioned into eight (8) sequential, controlled work packages designed to maintain continuous regression protection and strict software boundaries:

```
[WP1: Reading Assignment Foundation]
                ↓
[WP2: Role Curricula & Group Distribution]
                ↓
[WP3: Training Cascade & Revision Retraining]
                ↓
[WP4: Training Matrix Engine]
                ↓
[WP5: "My Reading List" User Portal]
                ↓
[WP6: Compliance Reporting & Analytics]
                ↓
[WP7: Full Integration Verification]
                ↓
[WP8: Formal OQ/UAT Qualification & Closure]
```

---

### 2. Work Package Specifications

#### Work Package 1 (WP1): Reading Assignment Foundation & Persistence
- **Scope:** Domain entity `ReadingAssignment`, enum `ReadingAssignmentStatus`, EF Core configuration, migration script, repository interfaces, and core `ReadingAssignmentService`.
- **Target Requirements:** `DC-URS-080`, `DC-URS-081`, `DC-URS-083`, `DC-URS-086`, `DC-URS-189`, `DC-URS-191`, `DC-URS-192`.
- **Entry Criteria:** Release 1c implementation formally authorized; clean test baseline (712 passing backend tests).
- **Deliverables:** Entity models, migration, unit tests for assignment generation and acknowledgement validation.
- **Exit Criteria:** Database migration applied in test environment; unit tests 100% green; zero regressions.

#### Work Package 2 (WP2): Role Curricula & Group Assignment Distribution
- **Scope:** Domain entities `RoleCurriculum` and `CurriculumItem`; bulk assignment endpoints; automatic assignment upon user onboarding.
- **Target Requirements:** `DC-URS-082`, `DC-URS-115`, `DC-URS-117`.
- **Entry Criteria:** WP1 complete and verified.
- **Deliverables:** Curriculum configuration API, bulk distribution service, automated onboarding event hook.
- **Exit Criteria:** Integration tests verify bulk assignment across 100+ users without deadlocks.

#### Work Package 3 (WP3): Training Cascade & Revision Retraining Engine
- **Scope:** Integration with `DocumentEffectiveDateWorker` via controlled domain event; automatic creation of assignments when revision becomes `Effective`; terminal closure of open assignments on superseded revisions (`SupersededIncomplete`).
- **Target Requirements:** `DC-URS-090`, `DC-URS-170`, `DC-URS-171`, `DC-URS-172`, `DC-URS-173`, `DC-URS-175`, `DC-URS-176`, `DC-URS-184`.
- **Entry Criteria:** WP2 complete; Release 1b worker baseline locked.
- **Deliverables:** Controlled hook in `EffectiveDateWorker`, training cascade service, terminal closure logic.
- **Exit Criteria:** Live PostgreSQL tests verify that activating revision $N$ cascades to prior completers and closes pending assignments on revision $N-1$.

#### Work Package 4 (WP4): Training Matrix Multi-Axis Engine
- **Scope:** Service aggregating Users × Documents × Revisions into a unified compliance grid; calculation of cell states (`Qualified`, `Pending`, `Overdue`, `TrainedOnSupersededOnly`, `NotAssigned`).
- **Target Requirements:** `DC-URS-111`, `DC-URS-112`, `DC-URS-113`, `DC-URS-114`, `DC-URS-174`.
- **Entry Criteria:** WP3 complete.
- **Deliverables:** `TrainingMatrixService`, grid calculation logic, gap detection for superseded-only personnel.
- **Exit Criteria:** Matrix query returns aggregated results in <2s across 500 users × 100 documents.

#### Work Package 5 (WP5): "My Reading List" User Experience & Acknowledgement Modal
- **Scope:** React UI page `MyReadingListPage`, due date badges, countdown timers, direct link to `ControlledPdfViewer`, informational reading progress display, and formal Read-and-Understand acknowledgement modal.
- **Target Requirements:** `DC-URS-085`, `DC-URS-087`, `DC-URS-088`, `DC-URS-089`, `DC-URS-190`.
- **Entry Criteria:** WP4 API endpoints verified.
- **Deliverables:** Frontend components, navigation route `/document-control/my-reading`, acknowledgement modal.
- **Exit Criteria:** Frontend production build clean; UI passes cross-browser usability checks.

#### Work Package 6 (WP6): Compliance Reporting, Executive KPIs & Matrix UI
- **Scope:** Interactive Training Matrix frontend page (`TrainingMatrixPage`), department compliance rankings, top overdue documents widget, and self-auditing CSV/PDF export.
- **Target Requirements:** `DC-URS-091`, `DC-URS-116`, `DC-URS-118`, `DC-URS-119`, `DC-URS-120`, `DC-URS-121`, `DC-URS-122`.
- **Entry Criteria:** WP5 complete.
- **Deliverables:** Executive KPI dashboard widgets, matrix UI with sticky headers, export service.
- **Exit Criteria:** Export verification confirms CSV format and cryptographic SHA-256 audit logging.

#### Work Package 7 (WP7): Full Lifecycle Integration Verification
- **Scope:** Comprehensive end-to-end testing of WP1–WP6 against live PostgreSQL; verification of ALCOA+ data integrity, append-only immutability, and zero regression across Release 1a and 1b suites.
- **Target Requirements:** All 35 in-scope requirements.
- **Entry Criteria:** WP1–WP6 complete.
- **Deliverables:** Integration test suite, WP7 verification report, defect tracking log.
- **Exit Criteria:** 100% test pass rate (cumulative 750+ tests); zero open critical/major defects.

#### Work Package 8 (WP8): Formal OQ/UAT Qualification & Release 1c Closure
- **Scope:** Execution of formal Operational Qualification (OQ) and User Acceptance Testing (UAT) protocols; formal transition of requirements from `PLANNED` to `QUALIFIED`; Validation Summary Report (VSR).
- **Target Requirements:** All 35 requirements.
- **Entry Criteria:** WP7 verification green; formal OQ/UAT protocol approved by QA.
- **Deliverables:** Executed OQ/UAT reports, RTM v2.0, Release 1c VSR, Release Closure Record.
- **Exit Criteria:** 100% protocol pass rate; formal QA/System Owner release approval.

---

### 3. Release 1c Governance & Stop Control

**MANDATORY GOVERNANCE RESTRICTION:**  
*This implementation plan is an authorized planning baseline. Execution of Work Package 1 (WP1) remains strictly prohibited until formal organizational authorization is granted.*
