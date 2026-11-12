# MicroLIMS Document Control — Release 1c
## Validation Strategy: Training Matrix & Reading Lists

- **Document Identifier:** `ML-DC-R1C-VAL-001`
- **Release Version:** MicroLIMS v1.2.0-rel1c (Controlled Planning Baseline)
- **Module:** Document Control Module — Subsystem: Training & Reading Lists
- **Date:** 2026-09-04
- **Methodology:** GAMP 5 Second Edition Risk-Based Validation Framework
- **Scope:** 35 In-Scope Requirements (`DC-URS-080`..`091`, `DC-URS-111`..`122`, `DC-URS-170`..`176`, `DC-URS-189`..`192`)

---

### 1. Validation Scope & Governance Hierarchy

This Validation Strategy defines the principles, controls, and acceptance criteria governing the qualification of **Release 1c**.

```
[URS v1.1 Baseline] ──> [Functional Specification (FRS)] ──> [Risk Assessment (RA)]
                                                                    │
┌───────────────────────────────────────────────────────────────────┘
▼
[Automated Test Suites (Unit / Integration)] ──> [OQ/UAT Protocol] ──> [Traceability Matrix (RTM)] ──> [VSR & Closure]
```

- **Validation Rigor:** In alignment with Release 1b precedent, all validation activities will be conducted to **GAMP 5 Category 5 (Custom Application Software)** standards to ensure full compliance with 21 CFR Part 11 and EU GMP Annex 11.
- **Controlled Lifecycle Progression:** Requirements remain `PLANNED` throughout implementation and transition to `QUALIFIED` exclusively upon formal execution and approval of the Release 1c OQ/UAT protocol.

---

### 2. Risk-Based Testing Strategy

Validation testing intensity is directly mapped to the Risk Priority Numbers (RPN) established in `ML-DC-R1C-RA-001`:

1. **High-Risk Functions (Individual OQ Challenge Testing):**
   - Read-and-understand acknowledgement ceremony and non-repudiation.
   - Permanent immutability and retention of revision-specific training evidence.
   - Strict matching of assigned revision ID to the displayed and acknowledged revision.
   - Automated training cascade execution upon status transition to `Effective`.
   - Terminal closure (never deletion) of open assignments against superseded revisions.
   - Segregation of duties preventing administrative bypass of training requirements.
2. **Medium-Risk Functions (Grouped OQ & Integration Testing):**
   - Due date calculations, grace periods, and overdue status evaluations.
   - Role curricula mappings and automated onboarding assignments.
   - System actor attribution (`system:effective-date-worker`) for automated cascade actions.
   - Training Matrix grid calculations and `TrainedOnSupersededOnly` gap flags.
   - Self-audited CSV/PDF report generation with cryptographic hashes.
3. **Low-Risk Functions (Functional & Usability Testing):**
   - Informational reading progress bars in the PDF viewer.
   - UI layout, sorting, filtering, and responsive matrix styling.

---

### 3. Data Integrity & Regulatory Controls (ALCOA+)

Release 1c validation enforces the complete ALCOA+ data integrity framework:
- **Attributable:** Every assignment acknowledgement is bound to an authenticated `UserId`, server UTC timestamp, and exact legal statement text. Automated cascade actions record actor `system:effective-date-worker`.
- **Legible:** Human-readable training records, employee transcripts, and Training Matrix views.
- **Contemporaneous:** All timestamps captured in server-authoritative UTC at transaction time.
- **Original:** PostgreSQL append-only database triggers prohibit `UPDATE` or `DELETE` on completed reading assignment records.
- **Accurate:** Idempotent generation prevents duplicate assignments; exact revision IDs prevent misattribution.
- **Complete, Consistent, Enduring, Available:** Revision-specific historical evidence is preserved permanently across all future document revisions and obsolescence events.

---

### 4. Segregation of Duties (SoD) Invariants

Validation protocols will explicitly test negative SoD scenarios:
- A user cannot acknowledge reading assignments on behalf of another user (`PROXY_ACKNOWLEDGEMENT_PROHIBITED`).
- System Administrator accounts cannot bypass reading requirements, mark assignments complete via administrative override, or delete non-completed historical records (`NO_ADMIN_TRAINING_BYPASS`).

---

### 5. Qualification Protocols (OQ / UAT) Expectations

- **Operational Qualification (OQ):** Formal technical protocol testing individual system boundary conditions, API guards, database immutability triggers, and negative failure modes. Executed by CSV engineers against a controlled test database.
- **User Acceptance Testing (UAT):** Realistic business scenarios representing daily microbiology laboratory workflows:
  1. Microbiologist logs into "My Reading List", opens newly effective SOP, views controlled PDF, and acknowledges reading.
  2. Document Controller updates SOP to next revision; system cascades retraining to prior completers while closing in-flight assignments as superseded.
  3. Quality Assurance Manager opens Training Matrix, filters by Department, identifies personnel with `Trained on Superseded Only` gap, and downloads self-audited compliance report.

---

### 6. Deviation Handling & Final Release Criteria

- **Deviation Classification:**
  - *Critical:* Data integrity compromise, SoD failure, historical evidence loss, or unhandled crash. (Zero permitted for release).
  - *Major:* Inaccurate calculation or functional defect without workaround. (Must be resolved before release).
  - *Minor:* Cosmetic UI imperfection with documented workaround. (May be accepted with formal QA justification).
- **Final Release Criteria:**
  - 100% of Release 1c requirements marked `QUALIFIED` in RTM v2.0.
  - Zero open Critical or Major deviations.
  - 100% pass rate across automated regression suite (Release 1a, 1b, and 1c tests).
  - Formal sign-off on Validation Summary Report by Technical Lead, QA Lead, and System Owner.
