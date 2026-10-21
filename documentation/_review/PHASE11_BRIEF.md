# Phase 11 — Independent quality review brief

You are an independent reviewer of a draft end-user guide for MicroLIMS, a laboratory information system. You did not write it. Judge it only against evidence.

## What you may use
- The repository source code at `E:\MicroLIMS\MicroLIMS` (backend/, frontend/) — the ground truth for IMPLEMENTED behaviour.
- The knowledge base: `documentation/knowledge/*.md` (thematic files), `documentation/knowledge/_clusters/C*.md` (per-module verified facts with `[src:]` evidence), `documentation/knowledge/role-guide.md`, `documentation/01_APPLICATION_MAP.md`, `documentation/COVERAGE_MATRIX.md`, `documentation/DOCUMENTATION_GAPS.md`, `documentation/USER_GUIDE_STRUCTURE.md`.
- Specifications (INTENDED behaviour only): `documents/DesignSpecification/*.md`, `docs/*.md`, `docs/superpowers/specs/*.md`.
- The guide: `documentation/USER_GUIDE.md`.

## What you must ignore
- HTML comments in the guide (`<!-- … -->`): they are the writers' notes.
- `documentation/guide/*BRIEF.md`, `documentation/knowledge/_clusters/R*-review.md` and `BRIEF.md` files: writer process material.
The knowledge base may itself be wrong. When guide and knowledge base agree but the code disagrees, the code wins — report it.

## Checks (prompt Phase 11)
accuracy (does each documented feature/label/message exist?), completeness (against COVERAGE_MATRIX and USER_GUIDE_STRUCTURE), consistency (terms, statuses, roles across sections), workflow correctness (against actual state transitions in code), permission correctness (can the stated role really do the action, by default grants?), business-rule correctness (calculations, limits, gates), GMP-sensitive areas (approval, audit trail, traceability, accountability, signatures), hallucinated or unsupported claims, contradictions between related workflows/sections, outdated or retired features described as current, any figure or claim based on mock/placeholder data, and any compliance/validation claim (forbidden).

Method: you cannot verify every sentence. Prioritise GMP-sensitive and permission content, then sample at least 3 concrete claims per sub-section (labels, messages with `git grep -F`, statuses, roles) against code. Record what you sampled.

## Safety
Modify nothing except your own output file. No builds, servers, databases, network, state-changing git. Scratch in `E:\MicroLIMS\rls-tmp`. Search with Grep/Glob or `git grep`.

## Output: `documentation/_review/review-<X>.md`
```
# Phase 11 review — part <X> (§a–§b)
## Method and coverage
(sub-sections reviewed, number of claims sampled, what was not checked)
## Findings
### F-<X>NN <short title>
- Severity: CRITICAL | HIGH | MEDIUM | LOW
- Section: §n.k
- Problem:
- Evidence: (code path:line, knowledge-base reference, or spec quote)
- Recommended correction:
## Summary
(counts by severity; overall assessment of the part)
```
Severity: CRITICAL = wrong statement that could cause a GMP-relevant error (wrong approval/signature/permission/status/result rule) or a compliance claim; HIGH = wrong procedure step, label or rule a user would follow; MEDIUM = incomplete, inconsistent or unclear; LOW = wording/format.
Write findings to the file as you go.
