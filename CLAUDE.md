# CLAUDE.md — MicroLIMS v2 AI Engineering & Quality Constitution

This document is the authoritative engineering and GMP domain constitution for AI coding agents working on **MicroLIMS v2** ([Toshy2020/MicroLIMS](https://github.com/Toshy2020/MicroLIMS/tree/main)). Every rule in this file is a binding constraint. Do not bypass, weaken, or work around these rules to make builds, linters, or tests appear green.

> **Regulatory Scope & Design Intent:** MicroLIMS v2 is engineered to support GMP-regulated pharmaceutical quality control (QC) laboratory operations, including microbiology and physicochemical testing, and the technical controls relevant to FDA 21 CFR Part 11, EU GMP Annex 11, and ALCOA+ data integrity principles. Formal regulatory compliance for a specific intended use is established through documented risk assessment, computer system validation (CSV), approved SOPs, and operational controls.

## 1. System Architecture & Frozen Guiding Principles

### Repository Layout
```text
MicroLIMS/
├── backend/
│   ├── MicroLIMS.API/            # REST Controllers, Middleware, Extensions, Program.cs
│   ├── MicroLIMS.Application/    # Business Services, Workflows, DTOs, Validators
│   ├── MicroLIMS.Domain/         # Core Entities, Enums, Value Objects (Zero external dependencies)
│   ├── MicroLIMS.Infrastructure/ # External integrations (PDF, S3, Email, JWT, SkiaSharp)
│   ├── MicroLIMS.Persistence/    # EF Core 10 (MicroLimsDbContext), Configurations, Migrations
│   ├── MicroLIMS.Shared/         # Cross-cutting primitives, ApiResponse<T>, Exceptions
│   └── MicroLIMS.Tests/          # xUnit, Architecture, Workflow, Inventory & Postgres Integration Tests
├── frontend/                     # React 19 + TypeScript 6 + Vite 8 + Material UI 9 + Vitest 5
├── database/                     # ERD, SQL scripts, seed data, backups
├── docs/ & documentation/        # Architecture & technical reference docs (non-authoritative for domain rules)
├── documents/                    # URS, FS, DS, Validation, SOPs (AUTHORITATIVE source for domain rules)
└── scripts/                      # Deployment, migration, and backup automation
```

### Source-of-Truth Precedence
When documents disagree, resolve in this order and flag the conflict to the user:
1. Approved specifications in `documents/` (URS → FS → DS)
2. This file
3. `docs/` and `documentation/` (descriptive only; never authoritative for laboratory formulas, limits, or workflow rules)

Before asking the user about a scientific or workflow rule (Rule 3.2, Rule 7.2), first search `documents/` for it. Ask only if it is absent, ambiguous, or contradictory.

### Frozen Guiding Principles (Never Violate)
1. **Configuration Drives Behavior:** Laboratory configuration created by the Section Head determines workflows, specifications, and test orders. Never hardcode item-specific test assignments.
   - **Engine vs. configuration boundary:** The backend engine enforces *generic* invariants (predecessor completion, incubation windows, eligibility, SoD, traceability checks). *Which* steps, media, incubation times, and limits make up a given workflow comes from configuration. Never hardcode a specific organism chain, medium, or limit into a service; examples in this file illustrate invariants and are not seed data.
2. **Backend Owns All Laboratory & GMP Logic:** The React frontend is strictly for presentation, user input, and workflow navigation. The backend is the sole authority for:
   - Dilution factors, CFU calculations, averaging, rounding, and precision rules
   - Specification evaluation, OOS/OOT classification, and Alert/Action limit comparisons
   - Workflow state transitions, incubation stage readiness, and test eligibility
   - Permission, section-scope, and Segregation of Duties (SoD) enforcement
   Every consequential input received from the frontend must be re-validated server-side.
3. **End-to-End Segregation of Duties (SoD):**
   - System Administration (`SystemAdministrator`) and Laboratory Administration (`SectionHead`) remain strictly separated.
   - Across the regulated record lifecycle (sample registration, test preparation, result entry/modification, technical review, QA/Section Head approval, OOS investigation, and retesting), an actor who performs or records a step **cannot** review or approve their own work.
   - Frontend button hiding/disabling is a usability aid only; the backend must reject unauthorized or self-approving requests.

## 2. Build, Lint & Test Commands

Always run verification commands from the repository root before claiming a task is finished:

### Backend (.NET 10)
```bash
dotnet restore backend/MicroLIMS.sln
dotnet build backend/MicroLIMS.sln --configuration Release --no-restore
dotnet test backend/MicroLIMS.Tests --configuration Release --no-build
```
> `--artifacts-path` changes the output layout. If you use it, pass the **same** value to both `dotnet build` and `dotnet test`; otherwise `--no-build` will not find the compiled test assembly.

### Backend with PostgreSQL integration tests (required for regulated changes)
```bash
export MICROLIMS_TEST_POSTGRES="Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres"
dotnet test backend/MicroLIMS.Tests --configuration Release --no-build --logger "console;verbosity=normal"
```
- `MICROLIMS_TEST_POSTGRES` names a **maintenance** database (e.g. `postgres`), not the test database. `PostgresTestFixture` creates its own `microlims_test_<32 hex>` database, applies the committed EF migrations, runs the suite, and drops it. The role therefore needs `CREATEDB`.
- **Cloud agent sessions** (environment "MicroLIMS"): the setup script installs the .NET 10 SDK, PostgreSQL, and Tesseract OCR, enables `trust` auth on localhost only, and creates the least-privilege role `microlims_ci` (`LOGIN CREATEDB`, not superuser), mirroring CI. It does not leave PostgreSQL running, so start it before running the suite:
  ```bash
  service postgresql start
  export Jwt__Key="$(openssl rand -base64 48)"   # test-only, per run, never committed
  export MICROLIMS_TEST_POSTGRES="Host=localhost;Port=5432;Database=postgres;Username=microlims_ci"
  ```
  The container has PostgreSQL 16, while CI and deployment use 18. A failure that shows up on only one of the two versions must be reported, not ignored.
- CI (`.github/workflows/ci.yml`) runs the suite twice: once with no database (every `[PostgresFact]` must report Skipped), then against PostgreSQL (the real gate). Mirror the second run locally.
- Any change touching persistence, migrations, audit trail, e-signatures, concurrency, or workflow state **must** be verified with this command.
- For such changes, a reported `Skipped` count greater than 0 for `[PostgresFact]` tests is a **failed verification**, not a pass (see Rule 4.2).

### Frontend (Node 22 / Vite 8 / Vitest 5)
```bash
cd frontend
npm ci
npm run lint
npx tsc -b
npm run test
npm run build
```

## 3. GMP Domain Invariants, Historical Record Immutability & Data Integrity

AI agents must ensure that code is not merely syntactically valid, but scientifically and regulatorily sound:

### Rule 3.1 — Historical Context & Specification Versioning (Immutable Evaluation Context)
Modifying master data (such as updating an Item Specification, STM, Alert/Action limit, Media formula, or Workflow Profile) **must never retroactively alter or re-evaluate previously recorded or approved historical results**.
* Historical `Sample`, `TestOrder`, and `Result` records must remain bound to the immutable specification version/snapshot, units, precision, and acceptance criteria that were active at the time the test was performed and approved.
* Corrections, invalidations, retests, and OOS investigations must append new linked records or versioned states while preserving the original recorded values, attribution, and reason for change.

### Rule 3.2 — Scientific & Analytical Workflow Invariants
Never invent, guess, or simplify a microbiological or analytical rule. If a formula or workflow rule is ambiguous, stop and ask the user before coding. Formulas, limits, and step sequences come from the approved method/test configuration (Principle 1); the backend engine applies them generically. Examples below illustrate *test types*, not seed data or special cases:
* **Microbial Enumeration (e.g., TAMC, TYMC, bioburden, environmental monitoring counts):** Enforce whole-number colony counts and apply the configured dilution-factor, countable-range, averaging, and rounding rules strictly in backend domain/application services.
* **Pathogen Workflows (e.g., *Salmonella*, *E. coli*, *P. aeruginosa*, *S. aureus*):** All pathogens run through the **same generic, configuration-driven pathogen workflow**; no organism is special-cased in code. Step order, media (enrichment, selective broth, selective/confirmatory plating), number of confirmatory media, and incubation windows come from the test's configured workflow steps (`TestWorkflowStep`). The engine only enforces the invariants: a step cannot be recorded before its predecessor step and required incubation are completed, and a confirmatory or review step cannot precede its prerequisites. Never branch on a test code or organism name (e.g., `TestCode.Contains("SALMONELLA")`).
* **Physicochemical & Instrumental Analysis (e.g., HPLC, GC, ICP, UV spectrophotometry, titration, dissolution, disintegration, weight variation, gravimetric loss/residue):** System suitability, calibration, assay, residual-solvent, elemental-impurity, and other calculations are performed in the backend from the approved method master (e.g., `HplcMethod`, `IcpMethod`) and specification, using its configured equation type, replicate rules, units, precision, and acceptance criteria. Never hardcode a method-specific constant, limit, or formula in a service or in the frontend.
* **Media & Reference Strain Traceability:** Media batches used in testing must have passed Growth Promotion Testing (GPT) and sterility checks, be within expiry, and be linked by prepared lot number to the test execution.
* **Equipment, Columns & Standards Traceability:** Active equipment, chromatography columns, and working/primary/reference standards linked to test execution must be within valid calibration/qualification status and expiry.

### Rule 3.3 — ALCOA+ Audit Trail & Electronic Signature Integrity
0. **What Counts as Regulated (audit-by-default):** There is no opt-in marker. `MicroLimsDbContext.SaveChanges`/`SaveChangesAsync` automatically audits **every** tracked insert/update/delete. The only exclusions live in `MicroLimsDbContext.AuditCaptureExcludedTypes`, each with a justification comment (append-only logs, operational monitoring, or entities audited explicitly by their own service). Adding a type to that list requires explicit user approval, and never for these types, which are always regulated: `Sample`, `TestOrder`, `Result` and its readings/plates, reviews/approvals, electronic signatures, OOS/OOT investigations, retests, specifications/STMs/limits, media batches & GPT/sterility records, reference strains, equipment calibration status, inventory release/deduction, controlled documents, users/roles/section assignments, and system configuration. One of these may be excluded from automatic capture **only** when a dedicated service writes an equivalent explicit audit record in the same transaction (as `SystemSettingsService` and `AccessReviewService` do today). Never infer non-regulated status from an entity's name.
1. **Transactional Atomicity:** Audit trail generation (`AuditLogs`) and the business state change it describes must succeed or fail together within the same database transaction (`MicroLimsDbContext.SaveChangesAsync`). If audit persistence fails, the business transaction must roll back. See Rule 5.4 for how this interacts with the retrying execution strategy.
2. **Required Audit Attributes:** Every regulated mutation must record: unique Audit ID, attributable `UserId` (or explicit agent/system identity), UTC timestamp (from injected clock/`TimeProvider`), `EntityName`, `RecordId`, `Action`, `OldValues`, `NewValues`, `CorrelationId`, and (where required by GxP workflow) the `Reason` and electronic signature linkage.
3. **Secret Redaction in Audit Payloads:** `OldValues` / `NewValues` must **never** contain password hashes, password salts, refresh/access tokens, MFA/TOTP secrets, recovery codes, API keys, or signing keys. Such fields are recorded only as a masked change indicator (e.g., `"PasswordHash": "[REDACTED – changed]"`). Redaction is applied centrally in `MicroLimsDbContext` by marking the property `[AuditRedacted]` (never ad hoc per service), and is covered by `AuditCredentialRedactionTests`. Any new secret-bearing property must carry `[AuditRedacted]`.
4. **Database-Level Append-Only Protection:** Regulated `AuditLogs` and signed records are protected against `UPDATE` and `DELETE` at both the application layer and PostgreSQL trigger level (behaviour verified by `AppendOnlyAuditHardeningPostgresTests` and the per-table `*PostgresTests`; trigger presence, shape and enabled state after all migrations verified by `AppendOnlyTriggerCatalogPostgresTests`). Never bypass, drop, or disable append-only triggers. Migrations that rebuild, rename, or re-create a protected table must explicitly re-create its triggers (see Rule 7.4).
5. **Electronic Signatures (21 CFR Part 11 Subpart C / Annex 11 §14):** Actions requiring an electronic signature (e.g., Document Control approval/acknowledgment, Sample/Result final approval, OOS closure, Admin critical overrides) must:
   - Verify the signer's identity using **both** components (user ID + password) at signing; never accept a session token or cached credential alone as the signature.
   - Capture the **meaning** of the signature (e.g., Review, Approval, Responsibility) and the reason.
   - Permanently bind the signature to the exact signed record version so it cannot be excised, copied, or transferred to another record or a later version.
   - **Signature manifestation (§11.50):** Every human-readable display and every generated PDF/report of a signed record must show the signer's printed full name, the UTC date/time of signing (displayed with explicit time zone), and the meaning of the signature.
   - Never let the frontend construct or assert signature content; the backend generates and binds it.

## 4. Mandatory Anti-Fake Testing Rules (CRITICAL)

AI agents frequently write "theater tests" designed to pass quickly rather than catch real defects. **Any test that violates the following 7 rules is considered a broken build:**

> **Scope for existing tests:** These rules bind every **new or modified** test. The suite predates them (many tests use `UseInMemoryDatabase`, `fireEvent`, or `vi.mock`). Do not mass-rewrite legacy tests as a side effect of another task; bring a test file into compliance when you change it, and never add new violations.

### Rule 4.1 — Ban Shared `DbContext` Instances Across Arrange / Act / Assert
Never reuse the same `MicroLimsDbContext` instance to seed data (`Arrange`), execute a service (`Act`), and query results (`Assert`). Reusing a single `DbContext` reads from EF Core's in-memory change tracker and passes even if `.SaveChangesAsync()` was never called.
* **Required Pattern:** Always create separate `DbContext` instances for `Arrange`, `Act`, and `Assert`, and always query with `.AsNoTracking()` inside the `Assert` phase:
  ```csharp
  await using (var arrangeDb = CreateDbContext()) { /* seed data */ }
  await using (var actDb = CreateDbContext()) { /* invoke service under test */ }
  await using (var assertDb = CreateDbContext())
  {
      var persisted = await assertDb.Samples.AsNoTracking().SingleAsync(s => s.Id == sampleId);
      Assert.Equal(SampleStatus.Approved, persisted.Status);
  }
  ```

### Rule 4.2 — Ban Silent Test Skipping (`[PostgresFact]` Must Execute)
Never claim a `[PostgresFact]` integration test in `backend/MicroLIMS.Tests/IntegrationTests/` passed if `MICROLIMS_TEST_POSTGRES` was unset and xUnit reported `Skipped: 1`.
* A skipped test is **not** a passing test.
* Any test involving foreign keys, unique constraints, optimistic concurrency, raw SQL, migrations, or append-only audit triggers **must** run against a real PostgreSQL instance.

### Rule 4.3 — Risk-Based Database Selection (`PostgresFact` vs. `InMemory`)
`Microsoft.EntityFrameworkCore.InMemory` ignores foreign keys, unique indexes, check constraints, relational transactions, and PostgreSQL triggers.
* Never use `UseInMemoryDatabase` to test relational constraints, transactions, concurrency tokens, raw SQL, or database triggers.
* Reserve in-memory or pure unit tests strictly for isolated domain calculations, validators, and pure projection logic where relational storage behavior is not part of what is being proved.

### Rule 4.4 — Risk-Based GMP Triad Verification for Regulated State Mutations
Whenever a test exercises a regulated state mutation (sample lifecycle, test results, reviews, approvals, electronic signatures, inventory/media release, document control, or user/role security):
1. **Persisted State:** Query the entity from a fresh `DbContext` with `.AsNoTracking()` and verify all mutated fields (status, actor ID, UTC timestamp, version/concurrency state).
2. **Audit Trail Entry:** Query `assertDb.AuditLogs.AsNoTracking()` and assert that the corresponding audit record was persisted with the expected `UserId`, `Action`, `RecordId`, `OldValues`, and `NewValues`.
3. **Negative & Boundary Cases:** Include negative tests (`Assert.ThrowsAsync<...>`) covering the workflow's actual failure modes: unauthorized role/section scope, Segregation of Duties self-review/self-approval, stale concurrency version, invalid e-signature password, or out-of-order workflow transition.

### Rule 4.5 — Ban `vi.mock()` on Behavior Under Test & Child UI Components
In `frontend/src/**/*.test.tsx`, **never** mock the component, hook, or child tab under test with fake placeholder `<div>` strings (such as mocking `ControlChartTab`, `RecordSearchTab`, or `UserSectionsDialog` to return dummy text or `null` and then asserting `expect(screen.getByText("...")).toBeTruthy()`).
* Mount the real component tree inside `<PinnedLightTheme>` and `<MemoryRouter>`.
* Isolate only external network/HTTP boundaries (via `apiClient`/service-module boundary mocks; MSW is not installed — add it only as a deliberate change) rather than replacing child components or domain hooks with tautological stubs.
* **Narrow exception:** A thin wrapper around a heavy third-party renderer that cannot run in jsdom (e.g., a canvas/chart library wrapper) may be stubbed **only if** it is not the behavior under test, the stub preserves the props contract, and the test asserts on the props/data passed to it rather than on placeholder text. Document the reason in a comment above the `vi.mock()` call. Never use this exception for a project component, tab, dialog, or domain hook.

### Rule 4.6 — Use `@testing-library/user-event` Instead of `fireEvent`
In frontend interactive tests, use `const user = userEvent.setup()` (`await user.click(...)`, `await user.type(...)`) rather than `fireEvent.change` or `fireEvent.click`. `fireEvent` bypasses browser focus, pointer-events, and disabled/readOnly checks.

### Rule 4.7 — The Mutation Self-Check Before Completing Any Task
Before reporting that a test is finished, ask yourself:
> *"If I comment out `await _db.SaveChangesAsync()` or invert the core `if` condition in the production method I just wrote, will my test fail?"*
If the answer is **No**, your test is tautological and must be rewritten before finishing.

## 5. .NET 10 Backend, Security & Concurrency Standards (`backend/`)

1. **SDK & Compiler Strictness:**
   - All backend projects target `net10.0` (verified in `*.csproj`) with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.
   - Centralize common build properties in `backend/Directory.Build.props` and package versions in `backend/Directory.Packages.props`. *(Not yet introduced — see `claude-md-adoption-plan.md` Phase 1. Until then, versions live in each `.csproj`; keep a package at the same version in every project that references it.)*
   - Never suppress nullable compiler warnings with `!` (null-forgiving operator) in production code unless guarded by an explicit prior check.
   - **Exception — EF Core entity declarations:** The conventional `= null!;` initializer on required reference navigation properties and EF-materialized non-nullable properties in `MicroLIMS.Domain` / `MicroLIMS.Persistence` entity classes is permitted. Do not "fix" these by making them nullable or adding constructors unless the task requires it. The exception covers declarations only, never dereferences in logic.
2. **Resource- & Section-Scoped Authorization (Anti-IDOR):**
   - Role/permission attributes (`[Authorize(Policy = ...)]`) are necessary but not sufficient. Backend services must also verify **resource-level and laboratory-section scope** (e.g., verifying the user is assigned to the target `LaboratorySection` / `PhyschemArea` or holds `Sections.ViewAll`) before reading or mutating a record by ID.
3. **Optimistic Concurrency & Idempotency Protection:**
   - Critical state transitions (result entry, review, approval, document sign-off, stock deduction) must enforce optimistic concurrency (`RecordVersionMiddleware` / row version tokens) so two users submitting against the same record cannot silently overwrite each other or create duplicate approvals.
4. **EF Core 10 & PostgreSQL Discipline:**
   - **Current state:** `EnableRetryOnFailure` is **not** enabled (`Program.cs` calls plain `UseNpgsql`). Never enable it as a side effect of another task: doing so requires wrapping every existing `BeginTransaction` call site first (see `claude-md-adoption-plan.md` Phase 4). The next two rules apply in full once it is enabled; the no-nested-transaction rule applies now.
   - **Execution Strategy + Transactions:** When `EnableRetryOnFailure` (`NpgsqlRetryingExecutionStrategy`) is active, any manual `await Database.BeginTransactionAsync(ct)` call (including inside `MicroLimsDbContext.SaveChangesAsync`) **must** be wrapped inside `await Database.CreateExecutionStrategy().ExecuteAsync(async ct => { ... }, cancellationToken)`.
   - **No Nested Transactions:** Before beginning a transaction (including inside the `SaveChangesAsync` override), check `Database.CurrentTransaction`. If a transaction is already active, enlist in it and do **not** begin, commit, or roll back a new one — the outer owner controls commit/rollback. Beginning a second transaction on the same connection throws.
   - **Retry Replays the Whole Delegate:** The execution strategy re-runs the entire `ExecuteAsync` delegate on transient failure. Therefore the delegate must be idempotent and must contain **only** database work. Never place emails, S3/object-storage uploads, PDF generation, notifications, or other external side effects inside it; perform them after a successful commit (or via an outbox record written inside the transaction). Re-load or reset tracked state at the start of the delegate so a replay does not apply changes twice.
   - **Read Query Optimization:** Every read-only query in dashboards, reports, workspace listings, and search services **must** use `.AsNoTracking()` and project directly to DTOs via `.Select(...)` in SQL. Never materialize unbounded `.ToListAsync()` entity graphs into memory.
   - **Pagination Caps:** Clamp all list endpoint page sizes (`Math.Clamp(pageSize, 1, 100)`) before applying `.Skip(...).Take(...)`.
   - **Split Queries:** Avoid relying on global `QuerySplittingBehavior.SplitQuery` for single-collection queries; apply `.AsSplitQuery()` explicitly only where multi-collection `.Include()` Cartesian explosion would occur.
5. **Middleware & DI Lifetimes:**
   - Conventional middleware classes (`RequestDelegate _next`) are instantiated as **Singletons**. Never inject `Scoped` services (such as `MicroLimsDbContext` or `IErrorCaptureService`) into a middleware constructor; inject them into `InvokeAsync(HttpContext context, IMyScopedService service)` or implement `IExceptionHandler`.
   - **Rate Limiting Order & Partitioning:** `app.UseRateLimiter()` must run **after** `app.UseAuthentication()`. Partition rate limits by authenticated user ID (`ClaimTypes.NameIdentifier`) or fallback to client IP (`httpContext.Connection.RemoteIpAddress?.ToString()`), **never** by `httpContext.Request.Headers.Host`.
6. **Async & Deterministic Time:**
   - Every async controller action, application service method, and EF Core call (`ToListAsync`, `SingleOrDefaultAsync`, `SaveChangesAsync`) must accept and forward `CancellationToken cancellationToken = default`.
   - Never call `DateTime.Now`, `DateTime.UtcNow`, `DateTime.Today`, `DateTimeOffset.Now`, or `DateTimeOffset.UtcNow` directly in domain/application/persistence workflows; use the injected clock / `TimeProvider` abstraction (enforced by `ClockUsageTests.cs` — extend it if it does not already cover all of these) so time-dependent incubation, expiry, and SLA rules are deterministically testable.
   - Persist all timestamps in UTC (`timestamp with time zone`). Convert to local time only at the presentation layer, and always display the time zone alongside regulated timestamps.

## 6. React 19 + TypeScript 6 Frontend Engineering Standards (`frontend/`)

1. **Zero New `any` Types & Strict ESLint Compliance:**
   - **Never** weaken rules in `frontend/eslint.config.js` (e.g., changing `"error"` to `"warn"`) to make `npm run lint` pass.
   - Do not introduce `any` (`useState<any>`, `as any`, `: any`) in any new or modified `.ts` / `.tsx` file. Define explicit TypeScript interfaces matching the backend DTOs in the relevant `services/` file or the module's own types file. Existing `any` usages are legacy debt: remove them in files you touch, never add new ones.
   - Always satisfy `react-hooks/exhaustive-deps` properly rather than suppressing hook dependency warnings.
2. **Lifecycle Cancellation & Race Condition Prevention:**
   - Direct `useEffect` data-fetching hooks that trigger on mount or when filter/tab/sample IDs change must protect against out-of-order responses and unmount leaks (using `AbortController` with `{ signal: controller.signal }` passed to `apiClient`, or an approved query lifecycle manager).
3. **Authentication & Permission Integrity:**
   - UI permission checks (`useMenuGroups`, `<PermissionRoute>`, action buttons) are for user experience only; the backend enforces all security policies.
   - When handling `401 Unauthorized` in `apiClient.ts`, preserve the coalesced `refreshPromise` pattern so concurrent requests share a single token refresh call.
4. **Error Boundaries & User Feedback:**
   - Wrap lazy-loaded route trees (`React.lazy` + `<Suspense>`) in an `<ErrorBoundary>` so network or chunk-loading failures present a clean recovery UI.
   - Always surface API validation and domain errors via `sonner` toast notifications (`toast.error(...)`) or inline form helper text—never swallow errors with empty `catch {}` blocks.

## 7. AI Agent Operating Rules & Change Control

1. **Inspect Before Coding:** Always read the existing controller, service, entity configuration, and test files before proposing or implementing changes. Never invent repository structures, DTO property names, database columns, or endpoints from memory.
2. **Ask on Scientific or GMP Ambiguity:** Never guess a laboratory formula, specification rule, incubation window, or approval workflow transition. First look for it in `documents/` (see Source-of-Truth Precedence). If it is still ambiguous, unspecified, or contradictory, stop and ask the user, citing the document and section you checked.
3. **No Mock Business Data in Production Code:** Never hardcode fake/mock laboratory records or stubbed calculation outputs inside production controllers, services, or React components to make an unfinished feature appear functional.
4. **Controlled Migrations & Schema Changes:** Every EF Core entity change must be accompanied by an explicit migration in `MicroLIMS.Persistence` and verified with `dotnet ef migrations has-pending-model-changes`. Never modify or delete already-applied historical migrations without explicit instruction.
   - **Trigger Preservation:** Before finalizing a migration, inspect the generated `Up()` for `DropTable`, `RenameTable`, table rebuilds, or column-type changes on any append-only/protected table. If present, the migration must explicitly re-create the append-only triggers via `migrationBuilder.Sql(...)`, and `Down()` must leave the table protected as well.
   - **Post-Migration Verification:** A `[PostgresFact]` test must apply all migrations to an empty database and then assert that every expected append-only trigger exists (query `pg_trigger` / `information_schema.triggers`) and that `UPDATE` / `DELETE` on each protected table is rejected. Run this test for every migration change. This is `AppendOnlyTriggerCatalogPostgresTests`. A new protected table must be added to its `ProtectedTables` list (the test fails if a table gets an `_immutable` trigger without an entry) and must get its own behavioural UPDATE/DELETE/TRUNCATE rejection test.
5. **Honest Verification Reporting:** In your final summary, explicitly distinguish between:
   - Commands and tests you **actually executed** and their exact pass/fail/skip counts
   - Checks that could **not** be executed in the current environment (e.g., if a PostgreSQL daemon was unavailable)
   Never claim tests passed if they were skipped or unexecuted.
