---
description: Delegate heavy scaffolding, complex migrations, or multi-file features to Antigravity CLI (agy)
argument-hint: [task description]
---

Delegate the following task to Antigravity CLI (`agy`) through the **agy-bridge MCP tools**:

$ARGUMENTS

### Execution Instructions
Do not run `agy -p` in the terminal: headless agy auto-denies any shell command that isn't already on its exact-match allowlist, so the run ends with no output. The agy-bridge MCP server runs agy with permissions pre-approved, per-tool timeouts, and quota failover.

1. **Prepare the brief yourself.** Locate the files to change and the patterns to copy with Glob or `git grep -l`, and write down the acceptance criteria.
2. **Call `mcp__agy-bridge__delegate`** with `cwd` set to the repo root and a prompt containing:
   - the task, the explicit file list, the pattern files to follow, and the acceptance criteria;
   - "Search only with `git grep` / `git log` - ripgrep and grep are not installed; never run recursive Get-ChildItem/Select-String (it crawls node_modules and times out)";
   - the verification commands: `dotnet test backend/MicroLIMS.Tests --artifacts-path <temp dir>` (a running MicroLIMS.API locks the normal bin folders), `npm run build` in `frontend`;
   - "Do not commit or push, and do not run `dotnet ef database update` against LIMSV2 or any non-test database."
3. **EF Core migrations:** `dotnet ef migrations add <Name> --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release` (Release avoids the same bin lock), and the generated migration must contain only the intended change.
4. **Keep each call to one vertical slice.** Split bigger work into several `delegate` calls, and continue a slice with `mcp__agy-bridge__follow_up` and its `session_id`.
5. **On failure:** empty output or a timeout means the job was too big - split it and retry; a transient `INTERNAL (code 500)` - retry once; if it fails twice, report to the user.

### Delegation Guidelines
- **Scaffolding:** Multi-file features, full Clean Architecture slices, DTOs, controllers, and React UI components.
- **Testing:** Unit test suites (xUnit, Moq), integration tests, frontend test coverage (Vitest/React Testing Library).
- **Database:** EF Core migrations, database schemas, and seed data scripts.
- **Verification (always):** After `agy` completes, review `git status` / `git diff` and run `dotnet build` / `dotnet test` / `npm test` yourself before concluding.
