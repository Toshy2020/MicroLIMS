# A1 — Specification Set in `documents/`: Design Specification

**Roadmap package:** A1 (`improvement-roadmap.md`)
**Status:** Draft for user review
**Date:** 2026-11-11
**Kind:** Writing package. No product code changes, except a small document check in CI.

## 1. Purpose

Make `documents/` the real source of truth: a numbered User Requirements Specification (URS) and Functional Specification (FS) for every existing area, and one register of every calculation and limit rule the system applies.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Basis | Written from the system as built. The user and QA correct the drafts. Differences between the code and laboratory practice are listed as conflicts, never silently resolved |
| 2 | References | USP, Ph. Eur., BP and EU GMP Annex 1 |
| 3 | Format | Markdown in the repository; a signed PDF is exported for QA approval and stored in Document Control |
| 4 | Document Control documents | Moved from `docs/` into `documents/`, keeping their existing IDs |

## 3. Out of scope

- The traceability matrix, risk assessment and validation protocols (package A2).
- The user manual and SOP templates (package A3).
- Requirements for features not yet built (packages B to G). Each of those adds its own URS and FS entries when it is implemented.
- Changing the system to resolve a conflict. A conflict is recorded and decided by the user; any fix is separate work.

## 4. Current state (from the repository, not run)

- `documents/URS`, `FunctionalSpecification`, `SOP`, `Validation`, `UserManual` and `Screenshots` hold only a short README each.
- `documents/DesignSpecification/BusinessRules.md` (237 lines, 12 sections) and `WorkflowDiagrams.md` (116 lines) have real content, with no requirement IDs.
- Document Control has a full set under `docs/` (URS amendment, FRS 1a to 1c, delta URS/FRS 1d, matrices, risk assessment, OQ). Its IDs (`DC-URS-…`, `FS-1a-…`) are mentioned in comments in 18 test and source files.
- No other test carries a requirement ID. No CI step checks documents.
- A descriptive user guide exists (`documentation/guide`, 44 chapters). It is not authoritative and is used here only as a reading aid.
- The pharmacopoeia texts are not in the repository.

## 5. Design

### 5.1 Structure

```
documents/
  README.md                         index of all controlled documents and their status
  URS/URS-<AREA>.md                 one per area
  FunctionalSpecification/FS-<AREA>.md
  DesignSpecification/
    BusinessRules.md, WorkflowDiagrams.md   (kept)
    CalculationRegister.md          every formula, rounding rule, range and limit comparison
  Conflicts.md                      open and closed differences, one numbered entry each
  DocumentControl/                  the set moved from docs/
```

### 5.2 Areas and codes

| Code | Area | Code | Area |
|---|---|---|---|
| RCV | Sample receiving | INV | Inventory and materials |
| MIC | Microbiology count tests | EQP | Equipment |
| PAT | Pathogen tests | OOS | Out of specification |
| WAT | Water | MON | Monitoring plan |
| ENV | Environmental monitoring | REV | Review and approval |
| ACL | After cleaning | SIG | Electronic signatures |
| PHY | Physicochemical (finished product, raw and packaging material) | AUD | Audit trail |
| INS | HPLC / GC / ICP | RPT | Reports and certificates |
| MED | Media | MDM | Master data (items, specifications, Test Master) |
| SOL | Solutions | NTF | Dashboard and notifications |
| CRY | Cryovials and reference strains | ADM | Administration |
| WST | Working standards | SEC | Security |

Document Control keeps its own codes.

### 5.3 Requirement format

- IDs: `URS-<AREA>-nnn` and `FS-<AREA>-nnn`, three digits, never reused. A withdrawn requirement stays in the file marked "Withdrawn" with the date.
- **URS entry:** ID, one sentence beginning "The system shall …" or "The user shall be able to …", written in the user's terms, one testable statement each; a GxP flag (Yes / No); the regulation or pharmacopoeia chapter it serves, where there is one.
- **FS entry:** ID, the URS IDs it satisfies, what the system does (inputs, rule, result, who may do it, what is audited), and the code location (service and method) so A2 can link tests.
- Each document starts with a header table: document number, version, status (Draft / In review / Approved), author, reviewer, approver, effective date, change history.
- Every FS entry satisfies at least one URS entry, and every URS entry is satisfied by at least one FS entry or is listed in `Conflicts.md` as "required, not implemented".

### 5.4 Writing method (per area)

1. Read the controllers, services, entities and tests of the area, and the matching section of `BusinessRules.md`.
2. Write the FS entries from what the code does. Nothing is written from memory of how such systems usually work.
3. Derive the URS entries from the FS entries and `BusinessRules.md`.
4. Where the code, `BusinessRules.md` and the guide disagree, or where a rule looks scientifically doubtful, add a `Conflicts.md` entry: what each source says, where, and a question for the user. The FS states what the code does today and points to the conflict.
5. Mark each statement that could only be read, not observed running, so QA knows what was verified how.

### 5.5 Calculation register

One table row per rule the backend applies: ID (`CALC-nnn`), what is calculated or compared, the exact rule as coded (formula, rounding mode and precision, countable range, averaging, comparison operator), where it is configured or coded, the FS entry, the reference, and status.

**References.** The reference column names the chapter (for example USP <61>, Ph. Eur. 2.6.12, BP Appendix XVI B, EU GMP Annex 1) as a pointer only. The pharmacopoeia texts are licensed and not in the repository, so **I do not confirm that a coded rule matches the chapter text.** Each row carries the status "Reference to be confirmed by QA" until someone with the current edition confirms it and records the edition. A rule set by configuration, not by code, is listed as "Configured by the Section Head" with the place it is configured.

Known items that go straight into `Conflicts.md`: the count preview on screen rounds differently from the server; My Tasks and the workspace use different "ready to read" rules; incubator selection checks temperature range but not calibration status; sample numbers can collide under concurrent receipt.

### 5.6 Document Control move

- The Document Control URS, FRS, matrices, risk assessment, validation plan and OQ files move to `documents/DocumentControl/` with file history kept. IDs are unchanged.
- Each old path under `docs/` keeps a one-line pointer file so existing links work.
- Word and Excel files are moved as they are; they are not converted.

### 5.7 Review, approval and status

- Order of writing and review: the highest-risk areas first — MIC, PAT, REV, SIG, AUD, SEC — then the rest.
- Each area goes to the user as one pull request with its URS, FS, register rows and conflicts. The user and QA correct it; corrections are made in the same pull request.
- A document is "Approved" only after QA signs the exported PDF in Document Control. The markdown header then records the approved version and date. Until then `CLAUDE.md`'s precedence rule treats a Draft as descriptive.
- PDF export uses the print function of the existing markdown viewer or a simple script; no new product dependency.

### 5.8 Document check in CI

A small script run in CI fails when: an ID is duplicated; an ID does not match the pattern; an FS entry names a URS ID that does not exist; a URS entry has no FS entry and no conflict entry; a document header is missing a field. It does not judge content.

## 6. Risks

| Risk | Handling |
|---|---|
| A draft describes the code wrongly | Every FS entry names its code location; QA review; A2 links each entry to a test |
| A draft is taken as approved | Status in every header; index page lists status; drafts are not authoritative |
| A wrong pharmacopoeia claim | References are pointers only, each confirmed by QA with edition recorded |
| Volume (24 areas) stalls | One area per pull request, highest risk first; each is useful alone |
| Documents drift from code afterwards | A2's matrix check; later packages must add their entries in the same pull request |

## 7. Done when

Every area in 5.2 has a URS and FS with unique IDs passing the CI check; the calculation register covers every calculation site found in the backend; every conflict has an owner decision or is open with a named owner; Document Control documents are in `documents/`; QA has reviewed each document.

## 8. Open items

1. Who is the QA reviewer and approver named in the document headers?
2. The document numbering your site uses (for example `ML-URS-001`), if it must follow an existing scheme.
3. The editions of USP, Ph. Eur. and BP in force at your site.
