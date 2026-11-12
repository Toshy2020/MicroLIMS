# MicroLIMS Document Control — Release 1b Risk Impact Assessment

**Document ID:** ML-DC-R1B-RA-001  
**Version:** 1.0  
**Status:** Approved Risk Assessment  
**Module:** Document Control (Release 1b)  
**System:** MicroLIMS Enterprise Laboratory Information Management System  
**Authoritative Baseline:** MicroLIMS Document Control Risk Assessment v1.0 (`MicroLIMS_Document_Control_Risk_Assessment_v1_0.xlsx`), URS v1.1, FRS ML-DC-FRS-1B-001  
**Regulatory Context:** ICH Q9 (Quality Risk Management), GAMP 5 (Second Edition), 21 CFR Part 11, EU GMP Annex 11  
**Date:** September 3, 2026  

---

## 1. Executive Summary & GAMP 5 Classification Discrepancy Flag

This Risk Impact Assessment evaluates the functional requirements of **Release 1b** against the approved MicroLIMS Document Control Risk Assessment baseline. It identifies affected failure modes, establishes control strategies, assesses residual risks, and dictates qualification testing intensity.

### 1.1 Mandatory QA/CSV Decision Flag — GAMP 5 System Classification
> [!WARNING]
> **FORMAL GAMP 5 CLASSIFICATION DISCREPANCY REQUIRING QA/CSV RULING:**
> - **Source 1 (`MicroLIMS_Document_Control_Risk_Assessment_v1_0.xlsx` & `scratch_fs_dump.txt`):** Defines the system category as **GAMP 5 Category 5 (Bespoke / Custom Developed Application)**.
> - **Source 2 (`ML-DC-FRS-1A-001` & `ML-DC-VSR-1A-001`):** References the system as **GAMP 5 Category 4 (Configured Software)**.
> 
> **Evaluation & Impact:**
> - Category 4 implies testing focusing on configuration parameters and vendor release verification.
> - Category 5 mandates source code review, white-box testing, extensive unit testing, architectural verification, and formal OQ/PQ of all custom logic.
> - **Architectural Position:** Because MicroLIMS Document Control is custom-coded C# / React software integrated into an enterprise LIMS database, the technical controls for Release 1b have been designed to the **stricter standard of Category 5** (full white-box testing, negative authorization testing, automated unit tests, and database trigger verification).
> - **Action:** This discrepancy is formally submitted to the Quality Assurance Lead and Lead CSV Specialist for final classification sign-off before Release 1b qualification baseline execution.

---

## 2. Risk Assessment Methodology (ICH Q9 / GAMP 5)

The assessment evaluates each requirement using a three-dimensional qualitative model:
1. **Severity (S):** Impact on product quality, patient safety, or data integrity if failure occurs (`High`, `Medium`, `Low`).
2. **Probability (P):** Likelihood of failure in custom business logic (`High`, `Medium`, `Low`).
3. **Detectability (D):** Likelihood the defect is detected prior to causing harm (`Low` = Silent failure, `Medium` = Detected via periodic checks, `High` = Obvious / workflow blocking).
4. **Risk Priority:** Evaluated as **Critical**, **High**, **Medium**, or **Low** to determine verification rigor.

---

## 3. Detailed Risk Analysis of Release 1b Functional Requirements

The 43 Release 1b requirements map directly to risk rows `RA-044` through `RA-079` and `RA-177` through `RA-183` in the master Risk Assessment workbook.

| URS ID | FRS ID | Functional Requirement | Potential Failure Scenario | S | P | D | Initial Risk Priority | Implemented System Control Strategy | Residual Risk | Required Verification Rigor |
|---|---|---|---|:---:|:---:|:---:|:---:|---|:---:|:---:|
| **DC-URS-044** | `FS-1b-044` | Automatic Periodic Review task creation | Review task not triggered; document remains in use beyond review cycle without assessment. | Med | High | Low | **High** | Background worker scans `NextReviewDate <= UtcNow.AddDays(30)` and generates tasks idempotently. | **Low** | Integration & Worker Test |
| **DC-URS-045** | `FS-1b-045` | Review workspace displaying controlled PDF | Incorrect or unverified file displayed during review. | Low | Low | High | **Low** | PDF loaded via verified SHA-256 retrieval stream with integrity badge. | **Low** | UI / Functional Test |
| **DC-URS-046** | `FS-1b-046` | Page/section review notes | Review notes lost or unlinked from specific sections. | Med | Med | Med | **Medium** | Relational `PeriodicReviewFinding` entity with page/section anchors. | **Low** | Unit & Integration Test |
| **DC-URS-047** | `FS-1b-047` | Review outcomes (`RemainsValid`, `RevisionRequired`, `Obsolescence`) | Ambiguous review outcome allows uncontrolled document lifecycle drift. | Med | Med | Med | **Medium** | Mandatory enum selection; service enforces distinct workflow paths. | **Low** | OQ Automated Test |
| **DC-URS-048** | `FS-1b-048` | `RemainsValid` creates no new revision | `RemainsValid` accidentally increments revision sequence. | Med | Med | Med | **Medium** | Business logic guard restricts revision creation strictly to `RevisionRequired`. | **Low** | OQ Automated Test |
| **DC-URS-049** | `FS-1b-049` | `RemainsValid` advances next review date | Next review date not updated or calculated incorrectly. | Med | High | Med | **High** | Automated UTC date calculation `NextReviewDate = CompletedAt.AddMonths(ReviewCycle)`. | **Low** | Unit & Date Test |
| **DC-URS-050** | `FS-1b-050` | `RevisionRequired` links new revision | Originating review findings detached from subsequent revision. | Med | Med | Med | **Medium** | Foreign key linking `DocumentRevision.OriginatingPeriodicReviewTaskId`. | **Low** | OQ Automated Test |
| **DC-URS-051** | `FS-1b-051` | Review findings transfer to change items | Review findings lost during transfer into new draft. | Med | Med | Med | **Medium** | Transactional copy of findings into `RevisionChangeItem` with `Source = PeriodicReview`. | **Low** | Integration Test |
| **DC-URS-052** | `FS-1b-052` | Obsolescence recommendation approval | Reviewer marks document obsolete without QA oversight. | High | Med | Med | **High** | Reviewer cannot obsolete directly; transitions task to obsolescence approval workflow. | **Low** | OQ Automated Test |
| **DC-URS-053** | `FS-1b-053` | Independent periodic review history | Review history overwritten upon new revision release. | Med | Med | Med | **Medium** | Review tasks stored in dedicated `PeriodicReviewTasks` table linked to Master. | **Low** | Database & UI Test |
| **DC-URS-054** | `FS-1b-054` | Overdue periodic review identification | Overdue reviews hidden from management oversight. | Med | Med | Med | **Medium** | Server-side computed flag `IsReviewOverdue` rendered with high-priority UI badges. | **Low** | UI & Library Test |
| **DC-URS-055** | `FS-1b-055` | Create new revision from effective | Unauthorized user initiates revision, disrupting active document. | Med | Med | Med | **Medium** | `DocumentAuthorizationService` verifies user is Owner, Author, or Controller. | **Low** | OQ Automated Test |
| **DC-URS-056** | `FS-1b-056` | Retain Master and MicroLIMS ID | New revision changes document ID, breaking sample/batch traceability. | High | Med | Low | **Critical** | `DocumentMasterId` is immutable foreign key; sequence number never regenerated. | **Low** | Database Integrity Test |
| **DC-URS-057** | `FS-1b-057` | Propose next revision number & override | Inconsistent or conflicting revision numbers across system. | Med | Med | Med | **Medium** | Automated numbering algorithm (`01` -> `02` or `01.1`); overrides restricted to Controller and audited. | **Low** | Unit & Audit Test |
| **DC-URS-058** | `FS-1b-058` | Mandatory change reason & summary | Revision created without regulatory justification. | Med | Med | Med | **Medium** | FluentValidation enforces minimum 10 non-whitespace characters. | **Low** | Unit Test |
| **DC-URS-059** | `FS-1b-059` | Change Reference field | Disconnect between LIMS revision and Change Control system. | Med | Med | Med | **Medium** | Dedicated `ChangeReference` string field populated during drafting. | **Low** | Functional Test |
| **DC-URS-060** | `FS-1b-060` | Major / Minor revision classification | Minor typo treated as major revision triggering excessive re-training. | Med | Med | Med | **Medium** | Explicit enum `RevisionType.Major` vs `RevisionType.Minor`. | **Low** | Functional Test |
| **DC-URS-061** | `FS-1b-061` | Structured affected section items | Unstructured change log makes technical review difficult to verify. | Med | Med | Med | **Medium** | Structured `RevisionChangeItem` records tracking Section, Description, and Rationale. | **Low** | UI & Service Test |
| **DC-URS-062** | `FS-1b-062` | Multi-category impact assessment | Changes to media or equipment missed during SOP revision. | Med | Med | Med | **Medium** | Mandatory multi-category checklist (Training, Method, Equipment, Regulatory). | **Low** | OQ Automated Test |
| **DC-URS-063** | `FS-1b-063` | Originating review findings traceability | Traceability from revision back to periodic review lost. | Med | Med | Med | **Medium** | Direct relational navigation property and audit trail attribution. | **Low** | Integration Test |
| **DC-URS-064** | `FS-1b-064` | Originating findings must be addressed | Draft submitted without addressing originating review findings. | Med | Med | Med | **Medium** | Submission guard validates all `Source == PeriodicReview` findings are marked `Addressed`. | **Low** | OQ Automated Test |
| **DC-URS-065** | `FS-1b-065` | Continuous availability of effective SOP | Drafting new revision blocks laboratory analysts from active SOP. | High | Med | Med | **High** | Library serves `Effective` revision; drafts isolated in workflow views. | **Low** | OQ Automated Test |
| **DC-URS-066** | `FS-1b-066` | Submission to Technical Review | Revision bypasses technical review and progresses to approval. | Med | Med | Med | **Medium** | Workflow state machine enforces `Draft` -> `InReview` -> `AwaitingApproval`. | **Low** | OQ Automated Test |
| **DC-URS-067** | `FS-1b-067` | Side-by-side revision inspection | Reviewer misses critical wording changes between revisions. | High | Low | High | **Medium** | Synchronized side-by-side viewer presenting current effective vs proposed PDF. | **Low** | UI Inspection Test |
| **DC-URS-068** | `FS-1b-068` | Page/section review comments | Review comments unstructured and difficult to address. | Med | Med | Med | **Medium** | `DocumentReviewFinding` records with page/section anchors and priority flags. | **Low** | Functional Test |
| **DC-URS-069** | `FS-1b-069` | Comment lifecycle tracking | Review comment thread lost or overwritten. | Med | Med | Med | **Medium** | Explicit lifecycle: `Open` -> `AuthorResponded` -> `ReviewerVerified` -> `Resolved`. | **Low** | State Machine Test |
| **DC-URS-070** | `FS-1b-070` | Author prohibited from closing comments | Author silently closes reviewer-required finding without concurrence. | High | Med | Med | **Critical** | Service method `ResolveFindingAsync` blocks non-reviewer; author cannot close comments. | **Low** | Negative SoD Test |
| **DC-URS-071** | `FS-1b-071` | Open mandatory comments block review | Technical review completed while critical objections remain open. | High | Med | Med | **Critical** | `CompleteTechnicalReviewAsync` queries open mandatory comments; throws exception if count > 0. | **Low** | Negative Guard Test |
| **DC-URS-072** | `FS-1b-072` | Technical review outcomes | Reviewer cannot return revision for author corrections. | Med | Med | Med | **Medium** | Explicit reviewer options: `ReturnForCorrection` (reverts to Draft) or `CompleteReview`. | **Low** | OQ Automated Test |
| **DC-URS-073** | `FS-1b-073` | Awaiting Approval transition | Revision enters approval prematurely or without review completion. | Med | Med | Med | **Medium** | Service transitions revision to `AwaitingApproval` only upon valid review sign-off. | **Low** | OQ Automated Test |
| **DC-URS-074** | `FS-1b-074` | Approver inspection dossier | Approver signs off without visibility of review findings or impact. | High | Med | Med | **High** | Approval dossier compiles PDF, impact assessment, review comments, and change log. | **Low** | UI & Workflow Test |
| **DC-URS-075** | `FS-1b-075` | Approver decision options | Approver forced to approve flawed revision due to lack of options. | Med | Med | Med | **Medium** | Approver can `Approve`, `ReturnForCorrection`, or `Decline`. | **Low** | OQ Automated Test |
| **DC-URS-076** | `FS-1b-076` | Authenticated e-signature for approval | Approval executed without Part 11 re-authentication. | High | Med | Low | **Critical** | Re-authenticates via BCrypt password check; writes immutable `ElectronicSignature` record. | **Low** | Part 11 OQ Test |
| **DC-URS-077** | `FS-1b-077` | E-signature record completeness | Signature record lacks user snapshot, timestamp, or meaning. | High | Med | Low | **Critical** | Captures User Full Name, Username, Role, UTC Time, Meaning (`Approved`), and IP. | **Low** | Part 11 Audit Test |
| **DC-URS-078** | `FS-1b-078` | Segregation of Duties enforcement | Author approves own revision or reviews own work. | High | Med | Med | **Critical** | Hard server-side guards enforce: **Author != Reviewer**, **Author != Approver**, **Reviewer != Approver**. | **Low** | Negative SoD Test |
| **DC-URS-079** | `FS-1b-079` | Complete audit trail for workflow actions | Review or approval actions missing from regulatory audit trail. | Med | Med | Med | **Medium** | Additive semantic audit logs capture every state transition and signature link. | **Low** | Audit Integration Test |
| **DC-URS-177** | `FS-1b-177` | Automatic activation of future effective | Approved revision fails to become effective on scheduled date. | High | High | Low | **Critical** | Background worker executes scheduled query; transitions status in database transaction. | **Low** | Worker Integration Test |
| **DC-URS-178** | `FS-1b-178` | Automatic supersession of prior effective | Old revision remains effective alongside new revision (split brain). | High | High | Low | **Critical** | Transactional update sets prior revision to `Superseded` when new revision becomes `Effective`. | **Low** | Worker Transaction Test |
| **DC-URS-179** | `FS-1b-179` | Automatic periodic review task creation | Reviews overlooked due to manual tracking. | Med | High | Low | **High** | Worker scans `NextReviewDate` and generates `PeriodicReviewTask`. | **Low** | Worker Test |
| **DC-URS-180** | `FS-1b-180` | System attribution in audit trail | Automated transitions falsely attributed to human user or anonymous. | High | High | Low | **High** | Sets `ActorType = ActorType.System`, `Username = "SYSTEM"`, and logs rule in comment. | **Low** | Audit Verification Test |
| **DC-URS-181** | `FS-1b-181` | Downtime recovery for scheduled actions | Server reboot or downtime causes missed effective date activation. | High | High | Low | **Critical** | Worker evaluates `<= UtcNow`; catches up missed actions on restart and flags `DelayedExecution`. | **Low** | Downtime Simulation Test |
| **DC-URS-182** | `FS-1b-182` | UTC time zone standardization | Time zone mismatch causes activation on wrong calendar day. | High | Med | Low | **High** | All timestamps stored and evaluated in UTC; zero local server time dependency. | **Low** | Unit / Time Test |
| **DC-URS-183** | `FS-1b-183` | Scheduled process failure alerting | Worker crashes silently without administrator awareness. | Med | High | Low | **High** | Per-document `try-catch` logging critical security/system incident in `AuditLogs`. | **Low** | Exception Test |

---

## 4. Newly Identified Operational & Technical Risks in Release 1b

In addition to the 43 baseline risks, four new technical risk modes specific to asynchronous automation, concurrency, and electronic signatures were identified:

| New Risk ID | Risk Mode Description | Severity | Probability | Detectability | Control Mechanism | Residual Risk |
|---|---|:---:|:---:|:---:|---|:---:|
| **NEW-R1B-01** | **Concurrent Worker Execution Race Condition:** In a scaled or multi-instance deployment, two worker threads run simultaneously and attempt to activate the same revision. | High | Medium | Medium | Implement database row-level locking (`FOR UPDATE SKIP LOCKED`) or distributed transaction lock ensuring single worker processing. | **Low** |
| **NEW-R1B-02** | **Content Modification After Review Approval:** Author replaces file after technical reviewer has signed off, resulting in approval of unreviewed content. | Critical | Low | Low | Revision files are locked against upload/replacement as soon as revision transitions to `InReview` or `AwaitingApproval`. Any return to Draft invalidates prior review approvals. | **Low** |
| **NEW-R1B-03** | **Electronic Signature Replay / Mismatch:** A signature record generated for an earlier revision is attached to a subsequent revision. | Critical | Low | Low | `ElectronicSignature` record immutably binds `EntityId = revision.Id` and `EntityType = "DocumentRevision"`. Database trigger prohibits updating `EntityId`. | **Low** |
| **NEW-R1B-04** | **Database Sequence Drift During Fast Revisions:** Multiple authors initiating drafts simultaneously create overlapping sequence numbers. | Medium | Low | High | Revisions sequence calculation is protected by database table transaction locks on `DocumentRevisions`. | **Low** |

---

## 5. Risk-Based Verification Intensity Strategy

Based on the risk analysis, verification testing for Release 1b is stratified into three intensity tiers:

1. **Tier 1 — High-Risk & Critical Controls (Requires Explicit Negative & White-Box Testing):**
   - Segregation of Duties enforcement (Negative tests proving Author cannot review/approve, Reviewer cannot approve).
   - 21 CFR Part 11 Electronic Signature re-authentication and credential failure tests.
   - Reviewer comment gate (Proving author cannot close mandatory comments and review cannot complete with open comments).
   - Automated effective date worker activation, supersession, downtime recovery, and idempotency.
2. **Tier 2 — Medium-Risk Workflows (Requires Standard OQ & Functional Testing):**
   - Periodic review outcome routing (`RemainsValid` vs `RevisionRequired` vs `Obsolescence`).
   - Revision numbering calculation and controlled override.
   - Multi-category impact assessment completeness.
   - Side-by-side revision viewer rendering.
3. **Tier 3 — Low-Risk UI & Display Controls:**
   - Visual badges, review task dashboard indicators, and filter dropdowns.

---

## 6. Risk Assessment Conclusion

All 43 functional requirements and 4 newly identified technical risk modes have robust, deterministic control strategies. With the implementation of server-side SoD guards, 21 CFR Part 11 electronic signature verification, and an idempotent background scheduler, the residual risk profile for Release 1b is **Low** across all domains.
