---
description: Dispatch deep codebase research, dependency mapping, or architectural investigation to Antigravity CLI (agy)
argument-hint: [research topic or question]
---

Investigate and research the following topic across the codebase by delegating the reading to Antigravity CLI (`agy`) through the **agy-bridge MCP tools**:

$ARGUMENTS

### Why not `agy -p` in the terminal
Headless `agy -p` auto-denies any shell command that is not already on agy's allowlist (which only holds exact, previously approved command strings), so research that needs one new command ends with "no output produced". The agy-bridge MCP server runs agy with permissions pre-approved, per-tool timeouts, and quota failover. Use it.

### Procedure
1. **Locate, don't read.** Find the candidate files yourself with Glob or `git grep -l` (fast, skips `node_modules`/`bin`/`obj`). Never ask agy to discover files across the whole repo.
2. **Read through `mcp__agy-bridge__analyze_files`** with an explicit `files` list and `cwd` set to the repo root:
   - at most **3 files or ~1,500 lines per call** - split larger sets into several calls and send them **in parallel**;
   - one focused question per call, asking for `file:line` references and a word limit.
3. **Search through `mcp__agy-bridge__deep_search`** only for grep- or history-shaped questions (usages of a symbol, when/why something changed). The query must:
   - name the exact command to run - `git grep -n ...` or `git log ...`. ripgrep and grep are **not installed** on this machine, and agy's fallback (recursive `Get-ChildItem | Select-String`) crawls `node_modules`, `bin` and `obj` and times out;
   - forbid broader recursive searches and ask for a short bullet list.
4. **Start every prompt with** "READ-ONLY: do not modify, create or delete files; no state-changing git or database commands."
5. **Iterate with `mcp__agy-bridge__follow_up`** using the returned `session_id` instead of resending context.
6. **On failure:**
   - `agy returned empty output` or a timeout means the job was too big - split it (fewer files, one question) and retry;
   - `INTERNAL (code 500)` or another transient API error - retry the same call once;
   - if a call fails twice, tell the user what failed rather than silently doing the whole reading job yourself.
7. **Verify** with `git status --short` that the research changed nothing, then synthesise the findings.

### Research Scope
- **Architecture Mapping:** Trace dependencies between Domain, Application, Persistence, and API layers.
- **Compliance & GMP Logic:** Trace electronic signatures, audit trails, and permission checks.
- **Data Flow:** Analyze request lifecycle from React frontend through API controllers to PostgreSQL.
- **Synthesis:** Review `agy` research findings and summarize actionable conclusions with `file:line` references.
