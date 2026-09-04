# MicroLIMS Document Control — Release 1c
## Master Test Strategy: Training Matrix & Reading Lists

- **Document Identifier:** `ML-DC-R1C-TST-001`
- **Release Version:** MicroLIMS v1.2.0-rel1c (Controlled Planning Baseline)
- **Module:** Document Control Module — Subsystem: Training & Reading Lists
- **Date:** 2026-09-04
- **Verification Authority:** MicroLIMS Document Control URS v1.1 / `ML-DC-FRS-1C-001` / `ML-DC-R1C-RA-001`
- **Scope:** Automated & Manual Test Suites for Release 1c (35 Requirements)

---

### 1. Multi-Tier Testing Pyramid

The testing strategy implements a layered, automated verification architecture to ensure defect prevention, regulatory compliance, and non-regression of the qualified Release 1a and Release 1b baselines:

```
                  ┌──────────────────────┐
                  │   Formal OQ / UAT    │  (10 OQ Protocols / 3 UAT Scenarios)
                  ├──────────────────────┤
                  │ Frontend E2E / Spec  │  (Component & Integration specs)
                  ├──────────────────────┤
                  │ Live DB Integration  │  (PostgreSQL transactions, triggers)
                  ├──────────────────────┤
                  │ Application & API    │  (Controller, Auth, SoD guards)
                  ├──────────────────────┤
                  │      Unit Tests      │  (Domain entities, date calculations)
                  └──────────────────────┘
```

---

### 2. Test Suite Specifications

#### 2.1 Unit Test Layer (`MicroLIMS.Tests/UnitTests/`)
- **Focus:** Domain entity validation, state machines, business rule logic, formula correctness.
- **Key Target Areas:**
  - `ReadingAssignment_CalculatesDueDate_FromGracePeriod`: Verifies exact UTC due date calculation.
  - `ReadingAssignment_ValidatesMatchingRevision`: Enforces that an acknowledgement matches the assigned revision ID.
  - `ComplianceRate_CalculatesAccuratePercentage`: Validates mathematical formula ($C = \frac{\text{Completed}}{\text{Required}} \times 100\%$) across edge cases (e.g., zero required documents).
  - `InformationalProgress_DoesNotAlterStatus`: Confirms scroll tracking does not mutate assignment status.

#### 2.2 Application Service & Authorization Tests (`MicroLIMS.Tests/ApplicationTests/`)
- **Focus:** Service boundary guards, permission policies, parameter validation.
- **Key Target Areas:**
  - `MyReadingList_ReturnsOnlyAssignedUserRecords`: Enforces data segregation.
  - `AcknowledgeReading_RejectsUnauthenticatedOrProxyUser`: Prevents proxy acknowledgement.
  - `AssignReading_RequiresDocumentControllerPermission`: Blocks unauthorized assignment.
  - `Negative_AdminCannotBypassAcknowledgement`: Confirms administrative users cannot force completion without user acknowledgement.

#### 2.3 Database Integration Tests (`MicroLIMS.Tests/IntegrationTests/`)
- **Focus:** Live PostgreSQL persistence, transactions, trigger enforcement, sequence handling.
- **Key Target Areas:**
  - `Postgres_ReadingAssignment_CompletedRecord_IsImmutable`: Confirms database trigger rejects `UPDATE` or `DELETE` on `Status == Acknowledged`.
  - `Postgres_SupersededRevision_ClosesIncompleteAssignments`: Verifies that activating revision $N$ updates in-flight revision $N-1$ assignments to `SupersededIncomplete`.
  - `Postgres_HistoricalTrainingEvidence_RetainedIndefinitely`: Verifies queries for historical revisions return original records unmodified.
  - `Postgres_TrainingCascade_ConcurrencyLock`: Simulates concurrent status activations, ensuring zero duplicate assignments.

#### 2.4 Audit Trail & Semantic Verification Tests
- **Focus:** Attributable, tamper-evident audit logging.
- **Key Target Areas:**
  - `Audit_ReadingAcknowledged_CapturesExactStatementTextAndUtcTime`: Verifies audit event details.
  - `Audit_AutomatedCascade_AttributedToSystemWorker`: Verifies `ActorType = System` and `ActorId = system:effective-date-worker`.
  - `Audit_TrainingMatrixExport_LogsSha256Hash`: Verifies self-auditing export logs.

#### 2.5 Frontend Component & Build Tests (`frontend/src/modules/documentControl/`)
- **Focus:** Responsive rendering, matrix interaction, PDF launcher integration, accessibility.
- **Key Target Areas:**
  - `MyReadingListPage`: Renders pending, overdue, and completed tabs; status badges render correct colors.
  - `TrainingMatrixPage`: Renders sticky headers, scrollable multi-axis grid, and drill-down drawer.
  - `AcknowledgeDialog`: Disables submit button until legal confirmation checkbox is clicked.
  - Production build: `tsc -b && vite build` clean pass with zero errors.

---

### 3. Automated Regression Suite Baseline Protection

All new Release 1c tests will run alongside the existing qualified test suite:
- **Baseline Requirement:** All **712 Release 1b backend tests** must remain **100% green** at every phase.
- **Performance Budget:** Full cumulative test suite execution duration must remain under 60 seconds.
- **Zero Drift:** No Release 1c test may mock or alter Release 1b database fixtures or trigger definitions.

---

### 4. Planned Qualification Protocol (OQ / UAT) Outline

Formal qualification will be executed under controlled protocols defined in `ML-DC-R1C-OQ-001` and `ML-DC-R1C-UAT-001`:
- **OQ Protocols (OQ-1C-01 through OQ-1C-10):** Rigorous boundary and negative challenge testing across all 35 requirements.
- **UAT Protocols (UAT-1C-01 through UAT-1C-03):** End-to-end user journeys executed by laboratory analysts and QA managers.

**Test Strategy Conclusion:**  
This multi-tier strategy provides complete verification coverage for all 35 requirements while protecting the frozen Release 1b production baseline.
