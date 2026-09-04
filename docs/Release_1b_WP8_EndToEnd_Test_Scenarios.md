# MicroLIMS Document Control — Release 1b
## Work Package 8: End-to-End Test Scenarios & Execution Matrix

- **Document Identifier:** `ML-DC-WP8-E2E-001`
- **Release:** Release 1b
- **Work Package:** WP8 — Integration Verification Suite
- **Date:** 2026-09-04
- **Verification Authority:** MicroLIMS Document Control URS v1.1 / ML-DC-FRS-1B-001 / ML-DC-RTM-1B-001

---

### 1. Test Execution Overview

The Work Package 8 Integration Verification Suite executes realistic, end-to-end multi-user document lifecycles against a live PostgreSQL database container.

All integration test suites executed cleanly on .NET 8.0:
- **Total Tests Passed:** 712
- **Total Tests Failed:** 0
- **Total Tests Skipped:** 0
- **Live Database:** PostgreSQL 16
- **Test Class:** `MicroLIMS.Tests.IntegrationTests.DocumentControlLifecycleEndToEndPostgresIntegrationTests`

---

### 2. Detailed End-to-End Scenarios

#### Scenario 1: Unbroken Complete Lifecycle (WP1 $\rightarrow$ WP7)
- **Test Method:** `WP8_EndToEnd_UnbrokenDocumentControlLifecycle_Postgres`
- **Scope & Flow:**
  1. *Registration:* Author registers Document Master (`SOP-E2E-...`) and attaches Controlled PDF for initial Revision 01. Revision 01 is established as `Effective`.
  2. *Revision Creation:* Author creates Major Revision 02 from effective Revision 01. Attaches updated Controlled PDF, adds structured change items, and completes 9-category impact assessment.
  3. *Technical Review:* Revision 02 is submitted for technical review (`Draft` $\rightarrow$ `InReview`). Reviewer records mandatory finding on Section 4.1. Author responds with clarification. Reviewer verifies and resolves finding. Reviewer completes technical review (`InReview` $\rightarrow$ `AwaitingApproval`).
  4. *Approval Submission:* Author creates Approval Task with 14-day future effective date and assigns QA Approver.
  5. *21 CFR Part 11 Electronic Signature:* Approver attempts signing with wrong password (strictly rejected with `InvalidOperationException`). Approver enters valid credentials; system records immutable `ElectronicSignature` with manifest snapshot and transitions revision to `FutureEffective`.
  6. *Effective Date Worker:* `DocumentEffectiveDateService` evaluates pending revisions. Dry run before target date activates 0. Simulated clock advance triggers atomic transaction: Revision 02 becomes `Effective`, `DocumentMaster.CurrentEffectiveRevisionId` is repointed, Revision 01 becomes `Superseded`, and system audit event (`ActorType.System`) is persisted.
  7. *Periodic Review:* Next review date is artificially advanced. Periodic review engine discovers due document and generates task. Reviewer completes review with outcome `RemainsValid`. System automatically advances `NextReviewDate` by 12 months without creating unwanted revisions.
- **Result:** **PASSED** (Full relational database verification in PostgreSQL).

---

#### Scenario 2: Periodic Review $\rightarrow$ Revision Required Handoff
- **Test Method:** `WP8_PeriodicReview_RevisionRequired_Handoff_Postgres`
- **Scope & Flow:**
  1. Effective document reaches periodic review date.
  2. Periodic review engine generates task. Reviewer adds finding regarding outdated reagent sourcing and completes review with outcome `RevisionRequired`.
  3. Author creates minor revision referencing `OriginatingPeriodicReviewTaskId`.
  4. System assigns revision number `01.1` and establishes traceability back to originating review task.
  5. Finding is transferred to structured `RevisionChangeItem`.
- **Result:** **PASSED** (Traceability verified).

---

#### Scenario 3: Periodic Review $\rightarrow$ Obsolescence Recommendation
- **Test Method:** `WP8_PeriodicReview_ObsolescenceRecommended_Approval_Postgres`
- **Scope & Flow:**
  1. Reviewer determines SOP is no longer needed (decommissioned process) and selects `ObsolescenceRecommended`.
  2. Review engine does not directly obsolete document; instead, it automatically generates a formal `DocumentApprovalTask` routed to approval workflow.
  3. Revision remains in `Effective` status pending formal sign-off.
- **Result:** **PASSED** (Regulatory protection against premature obsolescence verified).

---

#### Scenario 4: Segregation of Duties Cross-Role Enforcement
- **Test Method:** `WP8_SegregationOfDuties_CrossRoleEnforcement_Postgres`
- **Scope & Flow:**
  1. Author attempts to designate themselves as Technical Reviewer $\rightarrow$ Blocked with `InvalidOperationException`.
  2. Author attempts to assign themselves as Approver on approval task $\rightarrow$ Blocked with `InvalidOperationException`.
  3. System Administrator authors document and attempts to approve own document $\rightarrow$ Blocked with `InvalidOperationException` (confirms System Administrator is non-exempt from 21 CFR Part 11 / GAMP 5 SoD rules).
- **Result:** **PASSED** (Zero SoD bypasses).

---

#### Scenario 5: Concurrency & Idempotency Safety
- **Test Method:** `WP8_Concurrency_DoubleWorkerActivation_Postgres`
- **Scope & Flow:**
  1. FutureEffective revision matures.
  2. Worker processes activation scan. Revision becomes `Effective`.
  3. Worker immediately executes a second activation scan.
  4. Idempotency guard detects revision is no longer `FutureEffective`; exactly 0 revisions activated on second run.
  5. Database assertion confirms exactly 1 activation audit event exists in PostgreSQL `AuditLogs`.
- **Result:** **PASSED** (Zero double-activation, zero duplicate audit entries).

---
**Summary:** All 5 core end-to-end lifecycle integration scenarios passed 100% with live PostgreSQL relational constraints and trigger immutability verified.
