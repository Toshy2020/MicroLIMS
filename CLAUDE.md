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
Delegate heavy, multi-file, or autonomous tasks to `agy` using the slash commands or terminal:
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

## Direct Terminal Delegation (`agy`)

When delegating from terminal or tool calls:
```bash
# General task delegation
agy -p "Scaffold xUnit test suite for MicroLIMS.Application.Services with full mock coverage"

# Codebase research
agy -p "Research all electronic signature enforcement points in backend controllers"

# Code review & verification
agy -p "Review git diff against GMP compliance and Clean Architecture constraints"
```

---

## Core Architecture Principles (Strict)

1. **Clean Architecture Boundary:** Domain -> Application -> Persistence / Infrastructure -> API. Domain and Application must remain free of UI/database concerns.
2. **Laboratory Logic on Backend:** Frontend never enforces GMP or microbiology rules directly; validation and business logic live in the backend Application layer.
3. **Auditability & Traceability:** Every sensitive lab action must record electronic signatures and audit trail entries.
4. **Validation After Delegation:** Always verify `agy` changes with `dotnet build` / `npm test` before concluding tasks.
