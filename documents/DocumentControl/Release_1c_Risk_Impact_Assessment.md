# MicroLIMS Document Control — Release 1c
## Risk & Impact Assessment: Training Matrix & Reading Lists

- **Document Identifier:** `ML-DC-R1C-RA-001`
- **Release Version:** MicroLIMS v1.2.0-rel1c (Planning Baseline)
- **Module:** Document Control Module — Subsystem: Training & Reading Lists
- **Date:** 2026-09-04
- **Methodology:** GAMP 5 Second Edition / ICH Q9 Quality Risk Management
- **Scope:** 35 In-Scope Requirements (`DC-URS-080`..`091`, `DC-URS-111`..`122`, `DC-URS-170`..`176`, `DC-URS-189`..`192`)

---

### 1. Risk Management Framework & Scoring Criteria

In accordance with GAMP 5 and ICH Q9, risk is evaluated across three primary dimensions:

- **Severity of Harm (S):**
  - High (3): Direct GxP regulatory non-compliance, release of un-qualified personnel to execute analytical testing, falsification of training records.
  - Medium (2): Delayed notification of overdue training, inaccurate non-critical management metrics, administrative inconvenience.
  - Low (1): Cosmetic UI anomalies, minor informational display issues.
- **Probability of Occurrence (P):**
  - High (3): High likelihood under standard operations without systematic automated controls.
  - Medium (2): Moderate likelihood; requires specific sequence of user actions or concurrency.
  - Low (1): Unlikely; requires failure of core database constraints or multiple barriers.
- **Detectability (D):**
  - High Detectability (1): Anomaly immediately obvious to user or trapped by automated system guards.
  - Medium Detectability (2): Anomaly detectable during supervisor review or periodic audit.
  - Low Detectability (3): Silent failure; anomaly hidden from standard UI views and audits.

$$\text{Initial Risk Class} = \text{Severity} \times \text{Probability} \times \text{Detectability}$$
- **High Risk:** Class 1 (RPN $\ge 12$) $\rightarrow$ Requires rigorous automated unit, integration, and individual OQ challenge testing.
- **Medium Risk:** Class 2 ($6 \le \text{RPN} < 12$) $\rightarrow$ Requires automated test coverage and grouped OQ/UAT verification.
- **Low Risk:** Class 3 ($\text{RPN} < 6$) $\rightarrow$ Standard functional testing.

---

### 2. Failure Mode and Effects Analysis (FMEA)

| Risk ID | Requirement Ref | Potential Failure Mode / Hazard | S | P | D | Initial Risk | Proposed Technical & Procedural Mitigations | Res. Risk | Verification Method |
|:---:|:---|:---|:---:|:---:|:---:|:---:|:---|:---:|:---|
| **RA-1C-01** | `DC-URS-086`<br>`DC-URS-189` | **False Completion / Proxy Acknowledgement:** A supervisor or peer acknowledges reading on behalf of an analyst without authorization. | 3 | 2 | 2 | **HIGH** | Strict API guard: `AssignedUserId == CurrentUserId`. Database trigger rejects non-owner completion. | **LOW** | Integration Test & OQ-1C-04 |
| **RA-1C-02** | `DC-URS-090`<br>`DC-URS-173` | **Evidence Loss Across Revisions:** A new effective revision retroactively overwrites or modifies historical training completion evidence for prior revisions. | 3 | 2 | 3 | **HIGH** | Append-only relational architecture; `ReadingAssignment` records are bound to specific `DocumentRevisionId`; DB triggers prohibit UPDATE on completed records. | **LOW** | DB Trigger Test & OQ-1C-06 |
| **RA-1C-03** | `DC-URS-192` | **Wrong Revision Acknowledged:** User acknowledges an outdated or draft revision while believing they are acknowledging the effective revision. | 3 | 2 | 2 | **HIGH** | Acknowledgement endpoint validates that assigned `DocumentRevisionId` matches current `Effective` state and the exact displayed document. | **LOW** | Unit & OQ Test |
| **RA-1C-04** | `DC-URS-170`<br>`DC-URS-171` | **Cascade Failure / Missing Retraining:** When a new revision becomes effective, the automated cascade fails to generate assignments, leaving personnel unqualified. | 3 | 2 | 2 | **HIGH** | Idempotent batch transaction in `EffectiveDateWorker`; database transaction wraps status update and assignment creation; failure logs critical alert. | **LOW** | Worker Test & OQ-1C-01 |
| **RA-1C-05** | `DC-URS-172`<br>`DC-URS-184` | **Incomplete Assignment Deletion:** Open assignments for superseded revisions are deleted rather than closed, destroying non-compliance audit evidence. | 3 | 2 | 2 | **HIGH** | Terminal state `SupersededIncomplete`; database delete triggers block hard deletes; audit trail records closure reason. | **LOW** | Immutability Test & OQ-1C-07 |
| **RA-1C-06** | `DC-URS-114`<br>`DC-URS-174` | **Silent Training Lag:** Laboratory personnel qualified only on a superseded revision execute testing without being identified as overdue on the current revision. | 3 | 3 | 2 | **HIGH** | Training Matrix computes `TrainedOnSupersededOnly` state and renders high-visibility orange warning badge; export includes compliance gap filter. | **LOW** | Matrix Test & OQ-1C-09 |
| **RA-1C-07** | `DC-URS-190` | **Progress Bar Mistaken for Acceptance:** An informational 100% scroll progress indicator is mistakenly interpreted by auditor as regulatory proof of comprehension without formal acknowledgement. | 2 | 2 | 1 | **MED** | Progress indicator explicitly labeled *"Informational Only"*; API enforces that only explicit signed statement constitutes completion. | **LOW** | UI Inspection & OQ-1C-05 |
| **RA-1C-08** | `DC-URS-083`<br>`DC-URS-084` | **Timezone Drift in Due Dates:** Inaccurate local machine time causes premature or delayed overdue status transitions. | 2 | 2 | 2 | **MED** | All due dates and comparisons use server-authoritative UTC clock (`DateTime.UtcNow`). | **LOW** | Timezone Unit Test |
| **RA-1C-09** | `DC-URS-180` | **Audit Attribution Corruption:** Background worker automated training assignments are attributed to an arbitrary user or lack actor details. | 2 | 2 | 2 | **MED** | Automated cascade explicitly sets `ActorType = System` and `ActorId = system:effective-date-worker`. | **LOW** | Audit Trail Verification |
| **RA-1C-10** | `DC-URS-166` | **Administrative Bypass of SoD:** System Administrator overrides reading assignments or marks users completed without acknowledgement. | 3 | 2 | 2 | **HIGH** | Service layer enforces that administrator role cannot invoke acknowledgement endpoint; bypass blocked by domain guard. | **LOW** | Negative Admin Test |
| **RA-1C-11** | `DC-URS-121`<br>`DC-URS-122` | **Report Export Manipulation:** Downloaded Training Matrix CSV is altered without detection. | 2 | 2 | 2 | **MED** | Self-auditing export generates SHA-256 hash of exported data and logs event `TrainingMatrixExported` with record count and filter parameters. | **LOW** | Export Verification Test |

---

### 3. Risk Mitigation Summary & Verification Strategy

- **Critical High-Risk Focus Areas:**
  1. Preventing false or proxy acknowledgements (`RA-1C-01`).
  2. Guaranteeing permanent, immutable retention of revision-specific evidence (`RA-1C-02`).
  3. Ensuring automated retraining cascades execute reliably without orphaned records (`RA-1C-04`).
  4. Exposing training lag via the `TrainedOnSupersededOnly` status in the Training Matrix (`RA-1C-06`).
- **Residual Risk Ruling:** With all proposed database triggers, API guards, and audit trail controls in place, all eleven (11) identified hazards achieve a **LOW residual risk** rating.

**Risk Assessment Conclusion:**  
The risk profile for Release 1c is fully manageable under GAMP 5 Category 5 verification rigor.
