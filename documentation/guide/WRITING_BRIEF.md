# Phase 9 writing brief (read fully before writing)

You write sections of the MicroLIMS **end-user guide** for laboratory analysts, reviewers, Section Heads, administrators, QA and Document Control users. Readers have no programming knowledge.

## Safety
- Write ONLY the section files you are assigned (`documentation/guide/NN-slug.md`) and your screenshot fragment (`documentation/guide/_ss/NN.md`). Modify nothing else.
- No builds, servers, databases, network, npm/dotnet, state-changing git. Search only with `git grep` / `git log` (no ripgrep/grep; never recursive Get-ChildItem/Select-String).
- No real sample/batch/product/person names, no credentials. Use fake placeholders: "SMP-0001", "Product A", "Lot L-0001", "Analyst A".
- Never write "compliant", "validated", "21 CFR Part 11", "ALCOA+ compliant", "GMP compliant". Describe what the system does ("The system records … in the audit trail").

## Sources (only these; never invent)
- Verified knowledge: `documentation/knowledge/_clusters/C<n>-*.md` named in `documentation/USER_GUIDE_STRUCTURE.md` for your sections — screens, roles, workflows (`## workflows`), business rules, validations (exact messages), statuses, audit-trail.
- Phase 5–6 reviews: `documentation/knowledge/_clusters/R<n>-review.md` — **use its "Guide callouts" list**: every callout for your modules must appear as a reviewer note.
- `documentation/knowledge/role-guide.md` for who can do what.
- If a fact is not in these files, do not write it. If a step is marked REQUIRES CONFIRMATION, keep it out of the numbered procedure and put it in a reviewer note.

## Language and style
- English. Short paragraphs, numbered procedures, tables where useful. Use the application's own UI labels in **bold** exactly as quoted in the knowledge files (button **Approve**, tab **Pending Review**).
- No API, database, class, enum or file names. No `[src: …]` tags. No BR/WF/SCR ids in the text (an HTML comment `<!-- WF-SAMPLE-001 -->` before a procedure is allowed and wanted, for traceability).
- Error messages: quote them exactly as in the knowledge files, in quotation marks.
- Permissions: name the permission in plain words and the default roles holding it, e.g. "Requires the *Review samples* permission (by default: Reviewer, Section Head, System Administrator)." Roles are editable by administrators, so always say "by default".

## Section file format
File starts with `## <§>. <Title>` (e.g. `## 12. Sample Receiving`). For each major screen / procedure use these level-4 headings, skipping any that would be empty:

```
### <§>.<k> <Screen or procedure name>
#### Purpose
#### Who can use it
#### Before you start
#### What you see
#### How to use it        (numbered steps)
#### System behavior
#### Validation           (exact messages)
#### Status               (status labels and what changes them)
#### Important rules
#### Errors
#### What happens next
```

Screenshot placeholders: `[SCREENSHOT SS-NNN]` where NNN = two-digit section number + one digit (§12 → SS-121 … SS-129). At most 4 per section unless the section is large; only for important screens.

Reviewer notes (for REQUIRES CONFIRMATION and DISCREPANCY items only):
```
> **Note for reviewers:** <one or two plain sentences>. (GAP-NNN)
```

Not-implemented features: one plain sentence ("MicroLIMS does not have a separate Final Release step.") — never a procedure.

## Screenshot fragment `documentation/guide/_ss/NN.md`
One table row per placeholder you used:
`| SS-NNN | §NN.k | Screen | Purpose | Controls that must be visible | Areas to highlight | ss-NNN-<slug>.png | Procedure | Test data (fake) |`

## When done
Reply with: files written, number of procedures, reviewer notes, screenshots, and anything you could not support from the sources (left out).
