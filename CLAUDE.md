# MicroLIMS — Claude Code Orchestration & Guidelines

## Overview
MicroLIMS is a GMP-compliant Laboratory Information Management System for pharmaceutical microbiology laboratories.
- **Backend:** ASP.NET Core (Clean Architecture: API, Application, Domain, Infrastructure, Persistence, Shared, Tests) with PostgreSQL and EF Core.
- **Frontend:** React + TypeScript + Material UI.

---

## Delegation Rules with Antigravity CLI (`agy`)

To optimize efficiency and token economy, tasks should be divided between Claude Code and `agy`:

### 1. What Claude Code Handles Directly
- Quick file edits and localized bug fixes (1–3 files).
- Interactive debugging and clarifying requirements with the user.
- Explaining code, reviewing snippets, and running targeted CLI commands.
- Orchestrating tasks and synthesizing `agy` results.

### 2. What to Delegate to `agy`
Delegate heavy, multi-file, or autonomous tasks to `agy` using the slash commands or the agy-bridge MCP tools (see below):
- **Heavy Scaffolding:** Creating full vertical slices (Entity + DTOs + Controller + Service + Repository + React UI components).
- **Test Generation:** Writing comprehensive test suites (xUnit/Moq backend unit/integration tests, Vitest/RTL frontend tests).
- **Database Migrations & Scaffolding:** EF Core migration generation, schema synchronization, seed data scripts, and verification.
- **Deep Codebase Research:** Architecture audits, dependency mapping, and regulatory/GMP compliance verification.
- **Large Refactoring:** Cross-cutting concerns, permission restructuring, or API contract updates.

---

## Slash Commands Reference

Custom commands are configured under `.claude/commands/`:

| Command | Usage | Description |
|---|---|---|
| `/delegate <task>` | `/delegate Implement Sample Preparation batch workflow` | Hands off heavy scaffolding, migrations, or test generation to `agy`. |
| `/research <topic>` | `/research Map out permission checks across all API controllers` | Dispatches codebase-wide research and dependency tracing to `agy`. |
| `/review <scope>` | `/review Check recent git commits for Clean Architecture violations` | Triggers a full review for GMP compliance, architecture, and tests with `agy`. |

---

## Delegating to `agy` Reliably (agy-bridge MCP)

Delegate through the **agy-bridge MCP tools**, not raw `agy -p` in the terminal. Headless `agy -p` auto-denies any shell command that isn't already on agy's exact-match allowlist, so the run ends with no output. The bridge runs agy with permissions pre-approved, per-tool timeouts, and quota failover.

| Need | Tool |
|---|---|
| Read / analyse files | `mcp__agy-bridge__analyze_files` — explicit file list, at most 3 files (~1,500 lines) per call; send parallel calls for more |
| Usages of a symbol, git history | `mcp__agy-bridge__deep_search` — the query names the exact `git grep` / `git log` command to run |
| Plan or code review | `mcp__agy-bridge__adversarial_review` |
| Scaffolding, tests, migrations | `mcp__agy-bridge__delegate` — one vertical slice per call |
| Continue a previous job | `mcp__agy-bridge__follow_up` with the returned `session_id` |

Rules that keep delegation from failing:
- **Find the files yourself first** (Glob, `git grep -l`) and hand agy explicit paths — never "search the whole codebase".
- **Searches use `git grep` / `git log` only.** ripgrep and grep are not installed on this machine; agy's fallback (recursive `Get-ChildItem | Select-String`) crawls `node_modules`, `bin` and `obj` and times out with empty output.
- **One focused question per call**, with a word limit and a request for `file:line` references. Research prompts start with "READ-ONLY: do not modify files".
- **Empty output or a timeout** means the job was too big — split it and retry. **`INTERNAL (code 500)`** — retry once. **Fails twice** — report it to the user.
- **Builds and tests while MicroLIMS.API is running** (it locks the normal bin folders): `dotnet test backend/MicroLIMS.Tests --artifacts-path <temp dir>`, and `dotnet ef migrations add ... --configuration Release`.
- Bridge timeouts and the default model live in the user-scope MCP config (`claude mcp get agy-bridge`); changes apply after restarting Claude Code.

---

## Core Architecture Principles (Strict)

1. **Clean Architecture Boundary:** Domain -> Application -> Persistence / Infrastructure -> API. Domain and Application must remain free of UI/database concerns.
2. **Laboratory Logic on Backend:** Frontend never enforces GMP or microbiology rules directly; validation and business logic live in the backend Application layer.
3. **Auditability & Traceability:** Every sensitive lab action must record electronic signatures and audit trail entries.
4. **Validation After Delegation:** Always verify `agy` changes with `dotnet build` / `npm test` before concluding tasks.
