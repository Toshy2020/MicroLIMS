# MicroLIMS Document Control — Release 1c
## Controlled Baseline Analysis & Architectural Readiness

- **Document Identifier:** `ML-DC-R1C-BSA-001`
- **Release Version:** MicroLIMS v1.2.0-rel1c (Planning Baseline)
- **Module:** Document Control Module — Subsystem: Training & Reading Lists
- **Date:** 2026-09-04
- **Planning Status:** **CONTROLLED PLANNING BASELINE (NO IMPLEMENTATION)**
- **Authoritative Baseline:** MicroLIMS Document Control URS v1.1 (`DC-URS-080`..`091`, `DC-URS-111`..`122`, `DC-URS-170`..`176`, `DC-URS-189`..`192`), Release 1b Production Baseline (`6fe5a61439c12af1c8bcd0f0bdc1018b01f48cab`)

---

### 1. Purpose & Scope of Release 1c

This Baseline Analysis assesses the architectural, domain, persistence, and regulatory prerequisites for **Release 1c (Training Matrix & Reading Lists)**. 

While Release 1a established master document governance and Release 1b delivered formal review, approval, e-signatures, and periodic reviews, Release 1c addresses personnel qualification on controlled documents:
- Automated reading assignments upon revision activation.
- Role-based and individual curricula assignments.
- Revision-specific read-and-understand acknowledgement.
- Training Matrix visibility and gap identification (e.g., personnel trained only on superseded revisions).
- Overdue tracking, reminders, and executive compliance KPI reporting.

**Strict Release Boundary Controls:**
1. **Standalone Document Control Domain:** Release 1c is strictly confined to the Document Control module. It does **NOT** integrate with laboratory execution modules (Testing Workspace, GPT, Media Preparation, Water, EM, After Cleaning, Receiving).
2. **Exclusion of KAF:** Knowledge Assessment Forms, question banks, and automated quizzes belong to **Release 1d** (`DC-URS-092`..`110`) and are excluded from Release 1c.
3. **Exclusion of Migration Wizard:** Legacy data migration tools belong to **Release 1e** and are excluded.
4. **No LMS Bloat:** Generic Learning Management System features (e.g., classroom scheduling, third-party video streaming, external certificates) are out of scope.

---

### 2. Authoritative Requirements Identification (35 Requirements)

Release 1c encompasses thirty-five (35) formal requirements defined in MicroLIMS Document Control URS v1.1:

| Domain | URS Requirement Range | Total Count | Summary of Functional Scope |
|:---|:---|:---:|:---|
| **Reading Workflow & Assignments** | `DC-URS-080` .. `DC-URS-091` | 12 | Reading assignment rules, auto-generation on revision effective, due dates, My Reading List view, acknowledgement timestamping, revision-specific historical retention. |
| **Training Matrix & Compliance KPIs** | `DC-URS-111` .. `DC-URS-122` | 12 | Multi-axis Training Matrix (User × Document × Revision), role-curricula mapping, overdue status flags, compliance percentage KPIs, department-level reporting. |
| **Training Cascade & Retraining** | `DC-URS-170` .. `DC-URS-176` | 7 | Automatic retraining assignment upon new effective revision, configurable retraining necessity by doc type, terminal closure of incomplete superseded assignments, identification of personnel trained on superseded revision only. |
| **Reading Evidence & Integrity** | `DC-URS-189` .. `DC-URS-192` | 4 | Explicit user acknowledgement as primary legal evidence, informational progress indicators, configurable legal acknowledgement statement text, strict assignment-to-revision matching. |
| **Total In-Scope Requirements** | — | **35** | **100% Accounted For in Release 1c Baseline** |

---

### 3. Reusable Qualified Assets from Release 1b

Release 1c builds directly upon the qualified Release 1b production foundation without requiring structural redesign:

1. **Entity Models:**
   - `DocumentMaster`: Root document entity with permanent numbering (`DOC-XXXXXXX`).
   - `DocumentRevision`: Revision entity holding status, effective date, superseded pointers, and file links.
   - `DocumentType`: Classification entity supporting document-type level training configuration.
   - `User` & `Role`: Existing authentication and identity entities.
2. **Service & Domain Infrastructure:**
   - `MicroLimsDbContext`: Entity Framework Core relational store with PostgreSQL Npgsql provider.
   - `IAuditEventService`: Semantic audit framework supporting custom action codes and actor attribution.
   - `ControlledPdfViewer`: Frontend PDF viewer component capable of displaying controlled revision files.
   - `EffectiveDateWorker`: Background hosted service capable of dispatching training cascade domain events upon status promotion to `Effective`.
3. **Authentication & Security:**
   - ASP.NET Core JWT bearer authentication with idle session timeout.
   - Dynamic permission evaluation policy provider (`PermissionPolicyProvider`).

---

### 4. Required New Domain Entities & Architecture

To support Release 1c requirements while maintaining Clean Architecture and ALCOA+ data integrity, the following new domain entities are proposed:

```
+------------------+         1..*         +------------------------+
|  DocumentMaster  |--------------------->|    DocumentRevision    |
+------------------+                      +------------------------+
                                                       | 1
                                                       |
                                                       | 1..*
                                          +------------------------+
                                          |   ReadingAssignment    |
                                          +------------------------+
                                          | - Id                   |
                                          | - DocumentRevisionId   |
                                          | - AssignedUserId       |
                                          | - AssignmentType       |
                                          | - DueDateUtc           |
                                          | - Status               |
                                          | - AcknowledgedAtUtc    |
                                          | - StatementText        |
                                          +------------------------+
                                                       | 1..*
                                                       |
                                          +------------------------+
                                          |   TrainingMatrixView   |
                                          +------------------------+
```

1. **`ReadingAssignment`:**
   - Bound directly to `DocumentRevisionId` and `AssignedUserId`.
   - Lifecycle Statuses: `Assigned`, `InReview`, `Acknowledged`, `Overdue`, `SupersededIncomplete`, `Cancelled`.
   - Immutability: Once `Acknowledged`, `AcknowledgedAtUtc`, and `StatementText` become read-only.
2. **`DocumentTrainingConfiguration`:**
   - Controls whether a document or document type requires mandatory reading, retraining on new revision, grace period days, and custom acknowledgement text.
3. **`RoleCurriculum` & `RoleCurriculumItem`:**
   - Maps specific `DocumentMaster` records to functional `Roles` or `Departments` for automatic onboarding assignments.

---

### 5. Revision-Specific Evidence Invariant

A fundamental regulatory requirement of Release 1c is **strict revision-specificity**:
$$\text{Document Master} \longrightarrow \text{Document Revision (v02)} \longrightarrow \text{Reading Assignment} \longrightarrow \text{User} \longrightarrow \text{Evidence Record}$$

- **Historical Integrity:** When revision `v02` is superseded by `v03`, existing completed training records for `v02` remain **permanently untouched and retrievable**.
- **Superseded Incomplete Handling:** If a user had an open assignment for `v02` when `v03` becomes effective, `v02`'s assignment is closed with status `SupersededIncomplete` (never deleted) and an audit event is generated.
- **Superseded Gap Identification:** The Training Matrix must compute and highlight users who are qualified on `v02` but have not completed `v03` (`TrainedOnSupersededOnly`).

---

### 6. Release 1b Protection & Controlled Change Analysis

**RULE:** No Release 1b production code or schema may be modified without a documented Change Control.

#### Evaluation of Potential Release 1b Dependencies:
- **`EffectiveDateWorker` Activation Hook:** When `EffectiveDateWorker` transitions a revision from `FutureEffective` to `Effective`, it must trigger the Release 1c Training Cascade.
- **Classification:** **`RELEASE 1b CONTROLLED CHANGE REQUIRED (MINIMAL EXTENSION)`**.
- **Proposed Architectural Solution:** In Release 1c, the worker will publish an internal domain event `DocumentRevisionBecameEffectiveEvent` or invoke an optional `ITrainingCascadeService`. If `ITrainingCascadeService` is null or unconfigured, the worker executes exactly as in Release 1b.
- **Regression Risk:** Low. Protected by automated unit and integration tests.

---

### 7. Baseline Analysis Conclusion & Approval

Release 1c requirements are well-defined, architecturally compatible with Release 1b, and partitioned to prevent cross-module contamination.

**Status:** **APPROVED CONTROLLED PLANNING BASELINE — READY FOR FRS ELABORATION**
