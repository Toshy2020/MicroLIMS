# Release 1b Test Strategy & Verification Plan

**Document ID:** ML-DC-R1B-TS-001  
**Version:** 1.0  
**Status:** Approved Test Strategy  
**Module:** Document Control (Release 1b)  
**System:** MicroLIMS Enterprise Laboratory Information Management System  
**Authoritative Baseline:** MicroLIMS Document Control URS v1.1, FRS ML-DC-FRS-1B-001, Risk Impact Assessment ML-DC-R1B-RA-001  
**Regulatory Context:** GAMP 5 (Category 5 testing rigor), 21 CFR Part 11, EU Annex 11, ALCOA+ Principles  
**Date:** September 3, 2026  

---

## 1. Test Strategy Overview & Principles

This Test Strategy defines the multi-tiered verification framework for **Release 1b** of the MicroLIMS Document Control module.

### Core Testing Principles:
1. **Risk-Weighted Testing Rigor (ICH Q9 / GAMP 5):** High-risk and critical controls (Segregation of Duties, 21 CFR Part 11 Electronic Signatures, Revision Immutability, and Automated Worker Transitions) receive rigorous white-box, negative, and database-level verification.
2. **Hard Negative Verification:** Tests must not merely prove that happy paths succeed; explicit negative tests must prove that:
   - Authors cannot review their own revisions.
   - Authors cannot approve their own revisions.
   - Technical reviewers cannot approve revisions they reviewed.
   - System Administrators cannot bypass SoD invariants.
   - Unauthorized users cannot sign or transition workflows.
   - Technical review cannot be completed while mandatory findings remain open.
   - Authors cannot close reviewer-required comments.
3. **Live PostgreSQL Integration Testing:** All workflow state machines, database constraints, relational foreign keys, and scheduler loops must be verified against a live PostgreSQL 16 engine (`microlims_doccontrol_wp1_test`), not merely mock/in-memory contexts.
4. **100% Regression Integrity:** All 603 existing Release 1a and core laboratory tests must remain green across all test execution cycles.

---

## 2. Testing Levels & Scope

```
+-------------------------------------------------------------+
|               User Acceptance Testing (UAT)                |
|      (Realistic Microbiological Laboratory QC Scenarios)    |
+-------------------------------------------------------------+
|              Operational Qualification (OQ)                 |
|     (Formal Functional Requirement & Invariant Testing)     |
+-------------------------------------------------------------+
|            Database Integration Verification Tests          |
|    (PostgreSQL Engine, FKs, Worker Loops, Triggers, SoD)    |
+-------------------------------------------------------------+
|             Application & Service Unit Tests                |
|     (Domain Invariants, State Machines, Input Validators)   |
+-------------------------------------------------------------+
```

---

## 3. Detailed Verification Modules

### 3.1 Unit Testing (`MicroLIMS.Tests/UnitTests/DocumentControl/`)
- **Domain Invariants & Entities:**
  - `DocumentRevisionStatus` transitions.
  - Revision incrementation logic (`Major` -> `02`, `Minor` -> `01.1`).
  - Next review date calculation (`UtcNow.AddMonths(Cycle)`).
- **Request Validators (FluentValidation):**
  - Reason for Revision (mandatory >= 10 non-whitespace characters).
  - Review Finding validation (mandatory page/section, text >= 5 chars).
  - Custom Revision Number regex format validation.
  - Impact Assessment completeness validation.

### 3.2 Service & Application Logic Testing
- **`DocumentReviewServiceTests`:**
  - Transition from `Draft` to `InReview`.
  - Comment lifecycle state machine (`Open` -> `AuthorResponded` -> `ReviewerVerified` -> `Resolved`).
  - Return for Correction resets status to `Draft` and unlocks draft file replacement.
  - Complete Technical Review transitions to `AwaitingApproval`.
- **`DocumentApprovalServiceTests`:**
  - Approver dossier compilation aggregates PDF, change summary, impact checklist, and comments.
  - Rejection / Decline transitions status to `Cancelled` with mandatory justification.
- **`PeriodicReviewServiceTests`:**
  - Automated task generation logic.
  - `RemainsValid` advances `NextReviewDate` by review cycle months without altering revision count.
  - `RevisionRequired` transfers findings into `RevisionChangeItem`.
  - `ObsolescenceRecommended` generates formal obsolescence approval task.

### 3.3 Segregation of Duties (SoD) Negative Test Suite
Mandatory explicit negative test cases in `DocumentControlSoDNegativeTests.cs`:

| Test Method Name | Scenario Tested | Actor Role | Invariant Enforced | Expected Result |
|---|---|---|---|:---:|
| `SoD_AuthorCannotAssignSelfAsReviewer` | Author attempts to submit draft assigning self as Technical Reviewer. | Author | **Author != Reviewer** | Throws `UnauthorizedAccessException` |
| `SoD_AuthorCannotCompleteReview` | Author attempts to call `CompleteTechnicalReviewAsync`. | Author | **Author != Reviewer** | Throws `UnauthorizedAccessException` |
| `SoD_AuthorCannotCloseReviewerComment` | Author attempts to resolve a reviewer-mandated comment without reviewer concurrence. | Author | **Reviewer Concurrence Gate** | Throws `UnauthorizedAccessException` |
| `SoD_AuthorCannotApproveOwnRevision` | Author attempts to submit approval sign-off for own revision. | Author | **Author != Approver** | Throws `UnauthorizedAccessException` |
| `SoD_ReviewerCannotApproveSameRevision` | User who completed technical review attempts to approve revision. | Reviewer | **Reviewer != Approver** | Throws `UnauthorizedAccessException` |
| `SoD_AdministratorCannotBypassSoDRules` | User with `SystemAdministrator` role attempts to bypass SoD and approve own authored revision. | Admin | **Hard Invariant (No Admin Exemption)** | Throws `UnauthorizedAccessException` |
| `SoD_UnauthorizedUserCannotSign` | User without `Approver` role attempts to invoke e-signature endpoint. | Analyst | **Role Authorization** | Throws `UnauthorizedAccessException` |

### 3.4 21 CFR Part 11 Electronic Signature Testing
- **Credential Verification:**
  - Submitting valid username and password returns success and records signature.
  - Submitting incorrect password fails immediately (`UnauthorizedAccessException`) and writes security audit event.
- **Immutable Snapshot Inspection:**
  - Verifies that `UserFullNameSnapshot`, `UsernameSnapshot`, and `RoleSnapshot` capture the exact state at signing time.
  - Simulates subsequent user renaming/role change; confirms signature snapshots remain unchanged.
- **Database Immutability:**
  - Proves that SQL `UPDATE` and `DELETE` on `"ElectronicSignatures"` throw PostgreSQL trigger exceptions.

### 3.5 Automation Worker & Scheduler Testing (`DocumentEffectiveDateWorkerTests`)
- **Activation of Future Effective Revisions:**
  - Seeds revision in `FutureEffective` status with `EffectiveDate = UtcNow.Date`.
  - Executes worker iteration; confirms revision transitions to `Effective`.
- **Simultaneous Supersession:**
  - Confirms prior `Effective` revision transitions to `Superseded` in the exact same transaction.
- **Downtime Recovery & Delayed Execution:**
  - Seeds revision with `EffectiveDate = UtcNow.AddDays(-2)` (simulating 48 hours server downtime).
  - Starts worker; confirms revision is activated, audit log records `DelayedExecution = true`, and rationale is logged.
- **Idempotency Verification:**
  - Executes worker iteration 5 times consecutively.
  - Confirms zero duplicate state changes, zero duplicate review tasks, and zero duplicate audit entries.
- **Exception Isolation:**
  - Seeds corrupted document record causing exception; confirms worker logs error, alerts admins, and continues processing remaining documents.

### 3.6 Frontend Automated & Component Testing
- **TypeScript Type-Check:** `tsc -b` must pass with 0 diagnostics.
- **Vite Production Build:** `vite build` must build cleanly with 0 errors.
- **Side-by-Side PDF Viewer:** Verifies concurrent rendering of current effective and proposed draft PDFs.
- **Electronic Signature Dialog:** Verifies masked password input, validation, and error alert rendering.

---

## 4. Test Environment & Automation Tools

- **Target Database:** PostgreSQL 16 on `localhost:5432` (`microlims_doccontrol_wp1_test`).
- **Test Frameworks:** xUnit, FluentAssertions, Moq, Microsoft.AspNetCore.Mvc.Testing.
- **Test Execution Command:** `dotnet test backend/MicroLIMS.Tests --logger "console;verbosity=detailed"`.
- **Frontend Build Verification Command:** `npm run build` in `frontend/`.

---

## 5. Test Pass/Fail & Exit Criteria

Release 1b testing will be deemed complete and acceptable when:
1. **100% Pass Rate:** 0 failed tests, 0 skipped tests across full solution test suite.
2. **0 Open High / Critical Defects:** All functional defects resolved and verified.
3. **Traceability Closure:** 100% of Release 1b requirements (`DC-URS-044`..`079`, `177`..`183`) mapped to passing tests in the RTM.
