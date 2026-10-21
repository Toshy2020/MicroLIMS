# Guide-section review brief (Sonnet reviewer of agy drafts)

The section files in `documentation/guide/` were drafted by a Gemini (agy) agent. In this project Gemini has invented validation messages, audit codes, screen labels and file paths. Your job: make each assigned section accurate, then leave it.

Read first: `documentation/guide/WRITING_BRIEF.md` (the rules the draft had to follow) and the sources it names for your sections (cluster `C<n>`, review `R<n>`, `documentation/knowledge/role-guide.md`).

For every assigned section file (edit IN PLACE; also its `_ss/NN.md`; modify nothing else):
1. Every UI label, button, tab, status, message, permission, default role, gate, calculation and rule must be supported by the cluster file (verified knowledge) — or by the code, checked with `git grep -F`. Remove or correct anything unsupported. Quoted messages must match verbatim.
2. Every relevant item in the R<n> "Guide callouts" list for the section's modules must appear as a `> **Note for reviewers:** … (GAP-NNN)` box. Add missing ones; remove reviewer notes that cite a gap id that does not exist in `documentation/DOCUMENTATION_GAPS.md`.
3. REQUIRES CONFIRMATION steps must not appear as plain procedure steps.
4. No compliance / validation claims, no "ALCOA+" claims, no code/class/enum/file names, no `[src:]` tags, no real product/batch/person names.
5. Format per WRITING_BRIEF (heading levels, `[SCREENSHOT SS-NNN]` numbering matching the section, `_ss` rows match placeholders).
6. Keep it readable for laboratory users; do not shorten correct content for its own sake.

Known patterns found by a scan of the drafts (fix wherever they occur in your sections):
- Module/screen ids in prose (`MOD-…`, `SCR-…`), endpoint paths (`GET /api/…`), component names (e.g. `SharedTsbEnrichmentPanel`), raw enum values (e.g. `InService` → use the UI label), "service"/"backend"/"database" wording → rewrite in user terms or remove.
- Screen text that itself claims compliance (sign-in footer "…Part 11 & EU Annex 11 Compliant", messages mentioning "ALCOA+"): do not present it as fact; if it must be mentioned, say the screen shows this text and add a reviewer note citing the existing DISCREPANCY gap for the sign-in claim (find it in DOCUMENTATION_GAPS.md).
- Permission codes (e.g. `Documents.Approve`) are acceptable only in §4 and §39 (administrator content), with a plain-language name next to each.

Append to the end of each section file an HTML comment block:
```
<!-- Review log (Sonnet): N corrections; invented items removed: …; callouts added: … -->
```

No builds, servers, databases, network, state-changing git. Scratch in `E:\MicroLIMS\rls-tmp`.
