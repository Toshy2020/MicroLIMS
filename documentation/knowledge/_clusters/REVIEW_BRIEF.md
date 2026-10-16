# Phase 5–6 review brief (read fully before starting)

Governing prompt: `C:\Users\moham\OneDrive\Desktop\user_guide_prompt_v2.md` — read "CRITICAL PRINCIPLE", "PHASE 5" (the sub-sections for your modules), "PHASE 6", "MOST IMPORTANT RULE".
Safety rules: the "Safety" section of `documentation/knowledge/_clusters/BRIEF.md` applies in full (write only your allowed files, no builds/servers/DB/network/state-changing git, scratch in `E:\MicroLIMS\rls-tmp`, no real data, no compliance claims).

## Inputs
- Your cluster files `documentation/knowledge/_clusters/C<n>-*.md` (verified knowledge + Phase 4 workflows). These are the source of truth for the guide.
- `documentation/DOCUMENTATION_GAPS.md`, `documentation/01_APPLICATION_MAP.md`, `documentation/knowledge/_raw/verified-roles-permissions.md`.
- The code, to settle any doubt.

## Allowed edits
1. Create `documentation/knowledge/_clusters/R<n>-review.md` for each cluster you review.
2. Correct factual errors you find in your cluster files IN PLACE (keep the 12 level-2 headings exactly; mark the fix "(corrected in Phase 5)"). Never add unverified content.

## R<n>-review.md format
```
# R<n> Domain and GMP review — <cluster title>
## Phase 5 domain checklist
### MOD-XXX-NNN <name>
| Checklist item (from prompt Phase 5) | Status: DOCUMENTED / PARTIAL / NOT IMPLEMENTED / REQUIRES CONFIRMATION | Where (BR/WF/SCR id) | Note |
## Phase 6 GMP / data-integrity facts
### MOD-XXX-NNN <name>
Objective statements only ("The system records … in the audit trail"; "Approval requires password re-entry with a meaning"; "No signature is required to …"). Cover: user accountability, role separation / SoD, review, approval, audit trail (what is / is not recorded), traceability, record history, status control, e-signatures, controlled changes to configuration and master data, result modification, rework, retest, investigation, session security. Each with [src:] or BR/WF id.
## Mock / placeholder / unverified data
## Guide callouts
Numbered list of items the user guide MUST show in a "Note for reviewers" box (REQUIRES CONFIRMATION or DISCREPANCY), each: short user-facing wording + GAP id. Keep it to what a user operating the screen needs to know.
## Corrections made to the cluster file
```
Rules: do not invent acceptance criteria, limits or values; only document values present in the verified cluster files or code. Never write "compliant", "validated", "Part 11", "ALCOA+ compliant".
