# Validation Plan — MicroLIMS New Production Deployment

| Field | Value |
|---|---|
| **Document No.** | ML-SYS-VP-001 |
| **Version** | 0.1 — Draft for review |
| **System** | MicroLIMS (Laboratory Information Management System) |
| **Scope of this plan** | Prospective validation of a **new production deployment** of the whole system |
| **Software baseline** | Git commit to be frozen at the start of IQ (recorded in the IQ protocol) |
| **GAMP 5 category** | Category 5 — custom application; hosting platforms Category 1 (infrastructure) |
| **Regulatory basis** | EU GMP Annex 11, 21 CFR Part 11, GAMP 5 (2nd ed.), PIC/S PI 041 (data integrity), ALCOA+ |
| **Date** | 2026-10-05 |

---

## 1. Purpose

This plan defines how MicroLIMS will be validated before its first production use in a newly deployed environment. It names the deliverables, who is responsible for them, the order in which they are produced, and the criteria that must be met before the system is released for GMP use.

The system is already developed. Validation therefore specifies requirements from the system as built, assesses risk, and then verifies the **new installation** against those requirements. The earlier Document Control release records in `docs/` (Releases 1a–1d) are treated as **development history only**. They are not approved records for this deployment and do not replace any deliverable in this plan.

## 2. Scope

### 2.1 In scope

All functions in the software baseline, grouped for validation as:

| Group | Functions |
|---|---|
| **A. Platform & security** | Login, password policy, lockout, session timeout, roles and permissions, user management, electronic signatures, audit trail, audit search, security audit events, archived records, error monitoring |
| **B. Sample lifecycle** | Sample receiving (raw material, finished product, water, environmental monitoring, after-cleaning), sample tracking, correction and voiding, analyst assignment, review, approval, reports / CoA |
| **C. Microbiology testing** | Test definitions and workflow steps, media (products, lots, preparation, release, growth promotion), incubation, readings, pathogen and biochemical decisions, cryovials and reference strains, OOS tracking |
| **D. Physicochemical testing** | Specifications and stages, HPLC methods and runs (incl. SST), GC, ICP, titration (incl. titrant standardisation), dissolution, disintegration, weight variation, other calculations, working standards, solution preparation |
| **E. Master data & inventory** | Items, materials and material master, equipment and equipment inventory, rooms, water sampling points, organisms, reference lists, laboratory organisation |
| **F. Document Control** | Document register, revisions, review and approval, periodic review, training and reading lists |
| **G. Supporting functions** | Dashboards and KPIs, discussions and messages, notifications / e-mail, reporting |

### 2.2 Out of scope

- Instrument data acquisition software (HPLC/GC/ICP CDS). MicroLIMS stores results typed in or attached as evidence; the instruments' own systems are validated separately.
- ERP / SAP release: no such interface exists.
- Local development and test databases.

### 2.3 Environments

| Environment | Purpose | Build |
|---|---|---|
| **DEV** | Development, automated tests | Local PC (PostgreSQL `LIMSV2`) |
| **VAL** | IQ/OQ execution; must be built with the same deployment procedure as PROD | Separate API service + separate database (or Neon branch) + separate file bucket |
| **PROD** | GMP use after release | Cloudflare (frontend), Render (API container), Neon (PostgreSQL), Backblaze B2 (files) |

PQ/UAT is executed in VAL with realistic data, or in PROD before go-live if the risk assessment allows it. Data created in PROD during PQ is either kept as genuine records or removed by documented procedure before go-live.

## 3. Validation approach

1. **Risk-based.** Each function is assessed for its impact on product quality, patient safety and data integrity (ML-SYS-RA-001). Testing effort follows the risk: high-risk functions get scripted tests with recorded evidence; low-risk functions may rely on vendor-style evidence and unscripted testing.
2. **Leverage developer testing.** The automated suites (about 2,160 backend test methods in xUnit, run against PostgreSQL, and about 160 frontend tests in Vitest) are evidence for low- and medium-risk functions. Each OQ document must reference the suite run (commit, date, pass count) and the tests that cover each requirement. The suite must pass in full on the frozen baseline.
3. **Test the new installation, not the code once.** IQ proves this specific deployment matches the documented configuration. OQ is executed on VAL, which is built the same way as PROD.
4. **Configuration is validated separately from code.** Lab master data (test definitions, specifications, media configuration, methods, equipment) is GMP configuration. It is specified in the Configuration Specification and verified during PQ.

## 4. Roles and responsibilities

| Role | Responsibility | Name |
|---|---|---|
| **System Owner** | Owns the system, its availability and maintenance; approves all deliverables | _TBD_ |
| **Process Owner(s)** | One per lab (Microbiology, Physicochemical); owns URS and PQ; approves fitness for intended use | _TBD_ |
| **Quality Assurance** | Approves this plan, risk assessment, protocols and final report; approves deviations | _TBD_ |
| **Validation Lead** | Authors protocols and reports; coordinates execution | _TBD_ |
| **Developer / SME** | Provides design information, fixes defects, supports IQ | _TBD_ |
| **System Administrator** | Installs and configures the environments; manages users and backups | _TBD_ |
| **Testers** | Execute OQ/PQ scripts; must not test functions they developed | _TBD_ |

Drafts may be prepared with AI assistance. Every document is reviewed and approved by the named humans above. A draft carries no approval until signed.

## 5. Deliverables

Documents are produced in this order. A document may not be approved before the documents it depends on.

| # | Document | ID | Depends on | Owner |
|---|---|---|---|---|
| 1 | Validation Plan (this document) | ML-SYS-VP-001 | — | Validation Lead |
| 2 | System Description | ML-SYS-SD-001 | 1 | Developer |
| 3 | GxP & Part 11 / Annex 11 Assessment | ML-SYS-GXP-001 | 2 | QA |
| 4 | Supplier Assessments (Cloudflare, Render, Neon, Backblaze) | ML-SYS-SA-001 | 2 | QA |
| 5 | User Requirements Specification (groups A–G) | ML-SYS-URS-001 | 2 | Process Owners |
| 6 | Risk Assessment (functional FMEA) | ML-SYS-RA-001 | 5 | Validation Lead + QA |
| 7 | Functional Specification | ML-SYS-FS-001 | 5 | Developer |
| 8 | Configuration Specification (environment + lab master data) | ML-SYS-CS-001 | 7 | Developer + Process Owners |
| 9 | Installation Qualification protocol + report | ML-SYS-IQ-001 | 8 | Validation Lead |
| 10 | Operational Qualification protocol + report | ML-SYS-OQ-001 | 6, 7, 9 | Validation Lead |
| 11 | Performance Qualification / UAT protocol + report | ML-SYS-PQ-001 | 8, 10 | Process Owners |
| 12 | Requirements Traceability Matrix | ML-SYS-RTM-001 | 5, 6, 10, 11 | Validation Lead |
| 13 | SOPs (see §9) | ML-SOP-xxx | 2 | System Owner |
| 14 | Validation Summary Report | ML-SYS-VSR-001 | all | Validation Lead + QA |

## 6. Risk management

Risk assessment follows GAMP 5 / ICH Q9. For each function:

- **Severity** (impact on patient, product quality or data integrity): High / Medium / Low
- **Probability** of the failure occurring: High / Medium / Low
- **Detectability** before harm: High / Medium / Low

The resulting risk class sets the test rigour:

| Risk class | Testing required |
|---|---|
| **High** | Scripted OQ test with pass/fail criteria and screenshot or record evidence; negative tests included; covered again in PQ |
| **Medium** | Scripted or unscripted OQ test; automated test evidence accepted if it maps to the requirement |
| **Low** | Automated test evidence or exploratory testing record |

The following are pre-classified **High**: e-signatures, audit trail, access control, result calculation (all physicochemical calculations, microbial counts, specification evaluation / pass-fail), OOS flagging, sample approval and CoA content, data backup and restore.

## 7. Acceptance criteria

The system is released for GMP use when:

1. All deliverables in §5 are approved.
2. IQ shows the installed environment matches the Configuration Specification.
3. All OQ and PQ tests pass, or each failure is closed through a deviation with an approved rationale.
4. No open deviation of **critical** or **major** classification remains. Minor deviations may stay open with an approved action plan and due date.
5. The RTM shows every URS requirement traced to at least one passed test.
6. The SOPs in §9 are effective and users are trained before they receive access.

## 8. Deviations and defects

Any departure from a protocol, unexpected result or defect is recorded as a deviation with: ID, test step, description, classification (critical / major / minor), root cause, correction, retest result and QA approval. A code fix during qualification changes the software baseline. QA assesses which completed tests must be repeated, and IQ records the new commit.

## 9. Maintaining the validated state

These procedures must be effective before go-live:

| SOP | Covers |
|---|---|
| Change control | Any code, configuration or infrastructure change; impact and re-test assessment; deployment approval |
| User access management | Granting, changing and removing access; periodic access review |
| Backup, restore and disaster recovery | Database and file backups, restore testing, recovery time and point objectives |
| Audit trail review | Who reviews which audit trails, how often, and how findings are handled |
| Incident and problem management | System errors, outages, data issues |
| Periodic review | Yearly review that the system is still in a validated state |
| Electronic signature policy | Users' accountability for their signatures (Part 11 §11.100(c) certification) |
| Data archiving and retention | Retention periods; retrieval of archived records |

## 10. Known risks to be addressed during validation

| # | Risk | Where it is addressed |
|---|---|---|
| R1 | **Free-tier hosting.** Render, Neon, Cloudflare and Backblaze free tiers give no SLA, no quality agreement and limited backup retention. The Render free instance sleeps after 15 minutes idle and has 0.1 CPU. | Supplier Assessment; formal risk acceptance by System Owner + QA, or upgrade before go-live |
| R2 | **Single developer, AI-assisted code.** Independent code review evidence is limited. | Risk Assessment; heavier scripted OQ for High-risk functions |
| R3 | **Backup and restore never tested end-to-end** in a new deployment. | IQ/OQ restore test; Backup SOP |
| R4 | **Lab master data entered manually** in the new database. | Configuration Specification + PQ verification of each configured test |
| R5 | **Lab clock and time zone** (`Lab:TimeZoneId` = Africa/Cairo) drive due dates and incubation times. | IQ check; OQ time-zone tests |

## 11. Approvals

| Role | Name | Signature | Date |
|---|---|---|---|
| Author (Validation Lead) | | | |
| System Owner | | | |
| Process Owner — Microbiology | | | |
| Process Owner — Physicochemical | | | |
| Quality Assurance | | | |
