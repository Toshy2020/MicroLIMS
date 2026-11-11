# A2 — Traceability Matrix and Validation Pack: Design Specification

**Roadmap package:** A2 (`improvement-roadmap.md`)
**Status:** Approved by the user 2026-11-11
**Date:** 2026-11-11
**Depends on:** A1 (URS and FS with IDs).

## 1. Purpose

Show, for every requirement, how risky it is and which test proves it, and give QA the documents needed to qualify an installation and release the system.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Risk method | The same as Document Control (Release 1c scales) |
| 2 | Manual OQ | High-risk requirements only; medium and low rely on automated tests |
| 3 | Test link | The requirement ID is on the test itself; the matrix is generated |
| 4 | Evidence | Generated matrix with test results; IQ record per installation; PQ in the live laboratory; validation summary report template |

## 3. Out of scope

- Executing OQ, IQ or PQ. A2 delivers protocols and tools; people at the site execute and sign.
- Writing or changing product behaviour. Missing tests found by the matrix are listed as gaps; closing them is follow-up work, sized after the first matrix exists.
- Supplier assessment, infrastructure qualification of the hosting providers, data migration validation.
- Rewriting the executed Document Control records. They stay as they are.

## 4. Current state (from the repository, not run)

- A system validation plan exists as a draft: `docs/validation/ML-SYS-VP-001_Validation_Plan.md` (GAMP category 5, all role names "TBD"). It names IQ and PQ documents that do not exist. Its test counts are out of date.
- Document Control has matrices, risk assessments and OQ protocols for Releases 1a to 1c. Their matrices name test classes by hand; five requirements in 1c are still "planned".
- Release 1c risk scales: Severity 1–3, Probability 1–3, Detectability 1–3 (3 = hard to detect), priority number = S × P × D; High ≥ 12, Medium 6–11, Low < 6. One row in that document is labelled Medium although its number is 4.
- The 1a OQ protocol has actual results, status and a sign-off table; the 1c protocol has neither.
- No test carries a requirement ID as data (a few mention one in a comment). About 435 backend test files and 178 frontend test files.
- CI keeps backend results (`.trx`) as an artifact. Frontend test results are not kept.

## 5. Design

### 5.1 Documents

```
documents/Validation/
  ML-SYS-VP-001_Validation_Plan.md      moved here, updated
  RiskAssessment/RA-<AREA>.md           one per area
  Traceability/RTM.md                   generated, never edited by hand
  Traceability/Gaps.md                  generated: requirements without proof
  OQ/OQ-<AREA>.md                       manual scripts for high-risk requirements
  IQ/ML-SYS-IQ-001_Protocol.md          installation checklist
  PQ/ML-SYS-PQ-001_Protocol.md          live-laboratory scripts
  Templates/                            OQ test case, deviation form, summary report
  Evidence/README.md                    what a release evidence pack contains
```

### 5.2 Risk assessment

- One row per URS requirement: failure mode (what goes wrong if it fails), S, P, D, priority number, class, existing controls, verification method.
- Scales and classes exactly as in Release 1c (section 4). Severity is judged by effect on patient safety, product quality and data integrity.
- The class is **computed** from S × P × D by the document check; a row whose written class does not match its numbers fails CI.
- I draft the rows from the FS. **Risk scores are QA's decision**: every score is a proposal until QA reviews it, and the header says so.
- Requirements not flagged GxP get one line: "Not GxP — not assessed".

### 5.3 Linking tests to requirements

- A test names the FS entries it proves:
  - Backend: `[Trait("Requirement", "FS-MIC-004")]` on the test method or class, one attribute per ID.
  - Frontend: the ID in square brackets at the end of the test title, for example `it("refuses a count above the range [FS-MIC-004]")`.
- A test may be tagged only if it would fail when the described behaviour is broken (the project's mutation self-check). Presentation-only frontend tests are not evidence for backend rules.
- Tagging is done area by area in the A1 order. The first pass tags existing tests; it writes no new ones.
- The comments that mention Document Control IDs are turned into the same tags, with their existing IDs.

### 5.4 Generated matrix

A tool run in CI builds `RTM.md`:

`URS ID → risk class → FS ID(s) → automated tests (name, kind: PostgreSQL / unit / frontend, last result) → OQ script → status`

- Status per requirement: **Covered**, **Covered — manual only**, **Gap**, or **Not GxP**.
- A high-risk requirement is Covered only when it has at least one passed **PostgreSQL or unit backend** test and a manual OQ script. Medium and low need at least one passed automated test.
- The tool reads the tags from the compiled test assembly and the frontend sources, and the results from the CI run. A skipped test counts as **not passed**.
- `Gaps.md` lists every requirement with status Gap, grouped by risk class.

### 5.5 CI rules

| Rule | Effect |
|---|---|
| A tag names an ID that does not exist | Build fails |
| The committed matrix differs from the generated one | Build fails ("regenerate the matrix") |
| A requirement that was Covered becomes a Gap | Build fails |
| A high-risk requirement is a Gap | Listed in `Gaps.md`; fails the build only after the user switches on "high-risk gaps block" (once the first gap list is closed) |
| A risk class does not match its numbers | Build fails |

The third rule is a ratchet: coverage can only rise, the same idea as the existing test-debt check.

### 5.6 Manual OQ

- One script per high-risk requirement or small group of them, in the fuller Release 1a format: ID (`OQ-<AREA>-nn`), requirements covered, objective, preconditions and test data, numbered steps, expected result per step, actual result, pass / fail, evidence reference (screenshot or record number), executed by and date, reviewed by and date.
- Scripts cover what automation cannot show a person: the signature shown on screen and on the printed report, the audit trail as a reviewer reads it, refusal messages for self-review, access by role in the real interface.
- A failed step is recorded on the deviation form and is never overwritten.

### 5.7 IQ

A checklist executed for each installation (test and production) and signed: application version and build identifier; database server version; all migrations applied and none pending; every append-only trigger present and enabled; database roles and grants as in the roles runbook; required settings present with secrets not shown; time zone setting; HTTPS and security headers; backup job configured and one restore test recorded; clock synchronisation; a health-check call. Each line has expected value, actual value, evidence, pass / fail.

Where a check can be answered by a query or command, the protocol gives it, so two people get the same answer.

### 5.8 PQ

Scripts executed by laboratory staff on real work during an agreed period after go-live: one full sample per sample type from receipt to certificate; one environmental monitoring and one water session; a review and approval by different people; one media lot from preparation to release; one out-of-limit result followed through; a backup and restore during the period. Acceptance: each script completed with no open critical deviation.

### 5.9 Validation plan and summary report

- The plan is updated: current test counts are replaced by a pointer to the generated matrix (so they cannot go stale), the IQ / OQ / PQ documents are named as they now exist, the risk method is stated once, the role table stays for the site to fill.
- Summary report template: scope and version, deviations and their closure, matrix totals by status and risk class, IQ / OQ / PQ results, open items with justification, release statement, signatures.
- **Evidence pack per release:** the matrix as generated by that release's CI run, the test result files, the executed IQ and OQ, the summary report. `Evidence/README.md` lists them. CI uploads the matrix and both result sets as one artifact named with the version.

## 6. Risks

| Risk | Handling |
|---|---|
| A test is tagged to a requirement it does not really prove | Tagging rule in 5.3; reviewer checks each tag; QA samples high-risk rows |
| The first matrix shows many gaps | Expected and useful; gaps are listed by risk, closed in follow-up work, and the block is switched on afterwards |
| Risk scores taken as approved | Marked as proposals until QA signs the risk assessment |
| Generated file edited by hand | CI regenerates and compares |

## 7. Done when

Every GxP requirement from A1 has a risk row; the matrix is generated in CI and its rules are active; every high-risk requirement has a manual OQ script; IQ and PQ protocols and the summary report template exist; the gap list has been given to the user.

## 8. Open items

1. Names for the roles in the validation plan (system owner, QA, validation lead).
2. The length of the PQ period.
3. Whether your site's deviation form must be used instead of the template here.
