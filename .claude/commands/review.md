---
description: Trigger an autonomous code review, quality check, and test verification with Antigravity CLI (agy)
argument-hint: [scope or diff to review]
---

Perform a comprehensive code review and quality verification with Antigravity CLI (`agy`) through the **agy-bridge MCP tools**:

$ARGUMENTS

### Execution Instructions
Do not run `agy -p` in the terminal: headless agy auto-denies any shell command that isn't already on its exact-match allowlist, so the run ends with no output.

1. **Prepare the material yourself:** write the diff to a scratchpad file (e.g. `git diff main...HEAD > <scratchpad>/review.diff`) and list the changed files with `git diff --name-only`.
2. **Call `mcp__agy-bridge__adversarial_review`** (a different model family) with `cwd` set to the repo root, naming the diff file and at most ~10 changed files. For larger changes, split by layer (backend / frontend / migrations) into parallel calls.
3. **Start the prompt with** "READ-ONLY: do not modify files", tell it to search only with `git grep` / `git log` (ripgrep and grep are not installed), and ask for findings grouped as Critical / Warning / Suggestion with `file:line`.
4. **On failure:** empty output or a timeout means the scope was too big - split it and retry; a transient `INTERNAL (code 500)` - retry once; if it fails twice, report to the user.
5. **Verify every finding against the code** before reporting it, and drop the ones that don't hold.

### Review Criteria
- **Clean Architecture Adherence:** Verify boundaries (Domain -> Application -> Persistence / Infrastructure -> API).
- **GMP & 21 CFR Part 11 Compliance:** Verify audit trails, electronic signatures, and data integrity safeguards.
- **Business Logic Placement:** Ensure laboratory logic remains strictly in backend Application services, never in frontend UI.
- **Robustness & Tests:** Verify exception handling, validation filters, and unit/integration test coverage.
- **Summary:** Categorize findings into Critical, Warning, and Suggestion.
