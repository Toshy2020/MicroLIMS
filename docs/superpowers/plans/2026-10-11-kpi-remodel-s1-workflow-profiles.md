# KPI Remodel S1 - Workflow Profiles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.
> **Repository state note (checked 2026-10-13, see `2026-10-11-kpi-remodel-index.md`):** commit `570cf02a` has since committed the Media/Cryovial migration `20261011103646_...` and its snapshot hunks, so the "UNTRACKED migration / dirty snapshot" statements below describe the earlier state. Only `AnalystKpiTab.tsx`, `AnalystKpiService.ts` and `reportingTypes.ts` carry the uncommitted 2026-10-11 KPI page patch. Always re-run `git status` first; if shared files are dirty with someone else's hunks, the `git add -p` protocol in these plans still applies.

**Goal:** Give every live workflow type (and any single test) an append-only, e-signed profile of work units and analyst-stage target days, with a per-field resolver, a configuration API and a configuration panel, without changing the legacy KPI maths.

**Architecture:** One append-only table `WorkflowProfileVersions` (a Draft may be signed in place once; a DB trigger blocks every other change). A pure `WorkflowProfileResolver` picks each field independently at a given approval time; `WorkflowProfileService` validates, signs (`SignatureMeaning.MasterDataChanged`), audits and exposes list/overview/coverage reads; `WorkflowProfilesController` fronts it under `Kpi.View` + the new `Kpi.ConfigureProfiles`. The React `WorkflowProfilesPanel` replaces the old weights dialog inside the existing KPI tab.

**Tech Stack:** ASP.NET Core + EF Core (Npgsql, InMemory for unit tests), xUnit, React + TypeScript + MUI, Vitest + React Testing Library.

**Spec:** `docs/superpowers/specs/2026-10-11-kpi-performance-remodel-design.md` - section 0 (D1-D5), section 2 (2.1-2.5), the `Kpi.ConfigureProfiles` permission from section 6, and the e-signature + audit rule of section 6 / D4. Names, signatures and routes follow the shared contract (S1 = contract section 2).

---

## Global Constraints

- 1 work unit = 1 standard analyst-hour of hands-on work (D3). `WorkUnits` is `numeric(8,2)`, `TargetAnalystDays` is `numeric(6,2)`; each must be > 0 when set, with at most 2 decimal places, and at least one of the two must be non-null.
- Retired workflow types `HplcAssay`, `ElementalAssay`, `HplcMultiAnalyte`, `StandardComparison` are rejected with 400. Live types (`WorkflowProfileService.LiveTypes`): `CountTest, Observation, Measurement, Gravimetric, Qualitative, Dissolution, Disintegration, WeightVariation, HplcMethodAssay, Titration, IcpMethodAssay`.
- Resolution is per field (units and target resolve independently): latest `Effective` row for the test code with `EffectiveFrom <= DateOnly.FromDateTime(approvedAt)` and the field non-null, else the same for the type default, else unresolved. "Latest" = `EffectiveFrom` desc, then `Id` desc. `Draft` rows are ignored. An unresolved field is never defaulted to 1.0 (D5).
- Versions are append-only (D4): never edited or deleted; the only permitted mutation is `Draft` -> `Effective` by signing. A correction is a new version.
- `EffectiveFrom >= today` where today = `DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime)`. Baseline exception: the first `Effective` version of a scope (same `WorkflowType` AND same `TestCode`) may use any earlier date, once. Drafts never count as a baseline.
- `ReasonForChange` is required, trimmed, max 500 chars. Every `Effective` version carries an e-signature (`SignatureMeaning.MasterDataChanged`, entity type `ReviewEntityTypes.WorkflowProfile`) and a semantic audit event (`AuditActionCategory.Configuration`). Wrong password rolls everything back (`UnitOfWork.RunAsync`, `_db.CurrentUserId = userId` first).
- A non-null `TestCode` must exist in `TestDefinitions`; the stored `WorkflowType` is overwritten with that definition's current type.
- Permission `Kpi.ConfigureProfiles` is held by SystemAdministrator and SectionHead by default. Write endpoints require BOTH `Kpi.View` (class level) and `Kpi.ConfigureProfiles` (action level). The server enforces; the frontend only hides buttons.
- No ranking, no leaderboard, no default sort by a performance value (D6), analysts see only their own numbers (D8), `MinSampleForRate = 5`, 7-day target fallback: these belong to S2 to S4 and S1 must not introduce anything that contradicts them (S1 shows configuration values only, never analyst figures).
- The legacy `GET /api/kpi/workload-weights` and the legacy maths stay untouched; only the legacy WRITE path closes (Task 7). S1 must ship without breaking the old KPI page.
- Dates travel as `"yyyy-MM-dd"` (`DateOnly`), enums as strings, responses as `ApiResponse<object>.Ok(...)`, errors read `response.data.message`.
- Git hygiene: ~41 unrelated files are dirty (including an UNTRACKED migration `20261011103646_AddMediaStorageEntriesAndCryovialStorageEquipment` and a modified `MicroLimsDbContextModelSnapshot.cs`). Stage ONLY the paths a task names with `git add <explicit path>`. Never `git add -A`, `git add .` or `git commit -a`. For shared files (`MicroLimsDbContextModelSnapshot.cs`, `ServiceCollectionExtensions.cs`, `IMicroLimsDbContext.cs`, `MicroLimsDbContext.cs`, `DbSeeder.cs`, `UserReferenceRegistry.cs`, `ReviewGateService.cs`, `PermissionConstants.cs`, `routes.ts`, and the frontend `AnalystKpiTab.tsx` / `AnalystKpiService.ts`, which carry the uncommitted 2026-10-11 KPI page patch) run `git diff -- <file>` first; if any hunk is not yours use `git add -p <file>` and answer `y` only to hunks mentioning WorkflowProfile / Kpi. Never revert other work.
- Tests run with `--artifacts-path` because a running `MicroLIMS.API` locks the bin folders: `dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "<filter>"` (run from `E:\MicroLIMS\MicroLIMS`). `grep`/`rg` are not installed: use the Grep tool or `git grep`.
- Every commit message ends with the footer line `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>` (blank line before it).
- Code style: match surrounding code; comments explain WHY, sparingly.

## Review Focus

Inputs and conditions the spec implies that a real user will hit first (most likely first). Each is pinned by a named test in the owning task.

1. **A test override that sets only one field.** An override with only `WorkUnits` must take its target from the type default (and the reverse); a later type-default row that sets only the target must not blank the units set by an earlier row. Pinned: Task 2 `Resolve_OverrideWithOnlyUnits_TakesTargetFromTypeDefault`, `Resolve_LaterDefaultWithOnlyTarget_DoesNotBlankEarlierUnits`.
2. **Signing an imported Draft days after the migration.** The import stamps `EffectiveFrom = CURRENT_DATE` on migration day; signing later with that stale date must be refused with a clear message when the scope already has an Effective version, accepted as the baseline when it does not, and the dialog must pre-fill today instead of the stale date. Pinned: Task 3 `SignDraft_StaleImportDate_RejectedWhenScopeHasEffective`, `SignDraft_StaleImportDate_AllowedAsBaseline`; Task 9 `prefills today when the draft date is in the past`.
3. **Someone edits a test's workflow type after profiles were set.** The resolver reads the current type, so the type default for the NEW type applies and an override by test code is unaffected; the overview must follow the same rule. Pinned: Task 2 `Resolve_AfterTestWorkflowTypeEdit_UsesNewTypeDefault_OverrideUnaffected`; Task 4 `Overview_FollowsCurrentTestWorkflowType`.
4. **Wrong password, double-click on Sign, or a form left open.** A wrong password leaves no signature, no audit event and (on PostgreSQL) no row; signing an already Effective version is refused; a stale `If-Match` is a 409. Pinned: Task 3 `AddVersion_WrongPassword_*`, `SignDraft_AlreadyEffective_Throws`, `SignDraft_StaleIfMatch_ThrowsConcurrency`; Task 6 `AddVersion_WrongPassword_LeavesNoRowSignatureOrAuditEvent`, `SignDraft_SecondSign_Rejected_AndStaleVersion_Conflicts`.
5. **Numbers the database would round or overflow.** `12.345` units (silently rounded to 12.35) or `1000000` units (numeric overflow = 500) must be a 400 with a readable message. Pinned: Task 3 `AddVersion_InvalidNumbers_Throw`.

## File Structure

| Path | Action | Single responsibility |
|---|---|---|
| `backend/MicroLIMS.Domain/Enums/WorkflowProfileStatus.cs` | Create | `Draft = 0, Effective = 1` |
| `backend/MicroLIMS.Domain/Entities/WorkflowProfileVersion.cs` | Create | One append-only profile version row |
| `backend/MicroLIMS.Persistence/Configurations/WorkflowProfileVersionConfiguration.cs` | Create | Table, precision, index, check constraints, signature FK |
| `backend/MicroLIMS.Application/Abstractions/Persistence/IMicroLimsDbContext.cs` | Modify | `DbSet<WorkflowProfileVersion>` |
| `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs` | Modify | `DbSet<WorkflowProfileVersion>` |
| `backend/MicroLIMS.Application/Services/UserReferenceRegistry.cs` | Modify | Registers `CreatedByUserId` (Blocks) |
| `backend/MicroLIMS.Application/Services/ReviewGateService.cs` | Modify | `ReviewEntityTypes.WorkflowProfile` |
| `backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileDtos.cs` | Create | DTOs and request records (contract 2.4) |
| `backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileResolver.cs` | Create | Pure per-field resolution |
| `backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs` | Create | Validation, sign + audit, list/overview/coverage |
| `backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs` | Create | HTTP surface under `/api/kpi/workflow-profiles` |
| `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs` | Modify | Register `WorkflowProfileService` (not the resolver) |
| `backend/MicroLIMS.Shared/Constants/PermissionConstants.cs` | Modify | `KpiConfigureProfiles` const, `All`, `Enforced` |
| `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs` | Modify | Catalog tuple + SectionHead grant |
| `backend/MicroLIMS.Persistence/Migrations/<ts>_AddWorkflowProfileVersions.cs` (+ `.Designer.cs`, snapshot) | Create / Modify | Table, immutability trigger, legacy import as Draft, permission SQL |
| `backend/MicroLIMS.API/Controllers/KpiController.cs`, `backend/MicroLIMS.Application/Services/KpiService.cs` | Modify | Remove the legacy `PUT workload-weights/{testCode}` and `UpdateWorkloadWeightAsync` |
| `backend/MicroLIMS.Tests/Fixtures/FixedTimeProvider.cs` | Create | Fixed clock for KPI tests |
| `backend/MicroLIMS.Tests/TestServiceFactory.cs` | Modify | `WorkflowProfile(db, time)` |
| `backend/MicroLIMS.Tests/UnitTests/WorkflowProfileModelTests.cs` | Create | Proves `date` column mapping, `DateOnly` JSON, request binding |
| `backend/MicroLIMS.Tests/UnitTests/WorkflowProfileResolverTests.cs` | Create | Resolver rules |
| `backend/MicroLIMS.Tests/UnitTests/WorkflowProfileServiceTests.cs` | Create | Service rules, read models |
| `backend/MicroLIMS.Tests/IntegrationTests/WorkflowProfileMigrationPostgresTests.cs` | Create | Migration Up/Down, trigger, rollback, xmin conflicts |
| `backend/MicroLIMS.Tests/UnitTests/RolePermissionSeedDataTests.cs`, `IntegrationTests/PermissionEnforcementMigrationPostgresTests.cs`, `ArchitectureTests/authorization-matrix.txt`, `WorkflowTests/WorkloadWeightsTests.cs` | Modify | Permission counts / matrix / drop legacy update test |
| `frontend/src/modules/kpi/types/workflowProfileTypes.ts` | Create | Wire types + label helper |
| `frontend/src/modules/kpi/services/workflowProfileService.ts` | Create | `apiClient` calls |
| `frontend/src/modules/kpi/hooks/useWorkflowProfiles.ts` | Create | Overview + coverage loading |
| `frontend/src/modules/kpi/components/AddProfileVersionDialog.tsx` | Create | Form then `SignatureDialog` |
| `frontend/src/modules/kpi/components/ProfileHistoryDrawer.tsx` | Create | All versions of one scope, Draft sign entry |
| `frontend/src/modules/kpi/components/WorkflowProfilesPanel.tsx` | Create | Type table, expandable tests, needs-configuration filter |
| `frontend/src/modules/kpi/components/WorkflowProfilesDialog.tsx` | Create | Floating dialog hosting the panel |
| `frontend/src/modules/kpi/**/*.test.ts(x)` | Create | Component / service / hook tests |
| `frontend/src/routes/routes.ts` | Modify | `PERMISSIONS.KPI_CONFIGURE_PROFILES` |
| `frontend/src/modules/reports/components/AnalystKpiTab.tsx` | Modify | Button + dialog swap |
| `frontend/src/modules/reports/services/AnalystKpiService.ts` | Modify | Remove `updateWorkloadWeight` |
| `frontend/src/modules/reports/components/WorkloadWeightsDialog.tsx` | Delete | Replaced by the panel |

---

### Task 1: Domain model, EF mapping, registry, clock fixture

**Files:**
- Create: `backend/MicroLIMS.Domain/Enums/WorkflowProfileStatus.cs`
- Create: `backend/MicroLIMS.Domain/Entities/WorkflowProfileVersion.cs`
- Create: `backend/MicroLIMS.Persistence/Configurations/WorkflowProfileVersionConfiguration.cs`
- Modify: `backend/MicroLIMS.Application/Abstractions/Persistence/IMicroLimsDbContext.cs:95` (after `WorkloadWeightHistories`)
- Modify: `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs:95` (after `WorkloadWeightHistories`)
- Modify: `backend/MicroLIMS.Application/Services/UserReferenceRegistry.cs:235-236` (before the closing `};`)
- Modify: `backend/MicroLIMS.Application/Services/ReviewGateService.cs:108` (inside `ReviewEntityTypes`)
- Create: `backend/MicroLIMS.Tests/Fixtures/FixedTimeProvider.cs`
- Test: `backend/MicroLIMS.Tests/UnitTests/WorkflowProfileModelTests.cs`

**Interfaces:**
- Consumes: `IVersionedEntity` (`uint Version`), `WorkflowType`, `ElectronicSignature`, `MicroLimsDbContext`.
- Produces: `WorkflowProfileStatus`, `WorkflowProfileVersion` (contract 2.2), `IMicroLimsDbContext.WorkflowProfileVersions`, `ReviewEntityTypes.WorkflowProfile = "WorkflowProfile"`, `MicroLIMS.Tests.Fixtures.FixedTimeProvider(DateTimeOffset)` with `SetUtcNow(DateTimeOffset)`.

- [ ] **Step 1: Preflight - see what is dirty in the shared files**

Run from `E:\MicroLIMS\MicroLIMS`:
```
git status --short | head -60
git diff --stat -- backend/MicroLIMS.Application/Abstractions/Persistence/IMicroLimsDbContext.cs backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs backend/MicroLIMS.Application/Services/UserReferenceRegistry.cs backend/MicroLIMS.Application/Services/ReviewGateService.cs
```
Expected: you see other features' changes. Note which of these four files already carry foreign hunks; you will use `git add -p` for those at commit time.

- [ ] **Step 2: Write the failing tests**

Create `backend/MicroLIMS.Tests/Fixtures/FixedTimeProvider.cs`:
```csharp
namespace MicroLIMS.Tests.Fixtures;

// A clock tests control. KPI tests must never read the wall clock.
public sealed class FixedTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public FixedTimeProvider(DateTimeOffset now) => _now = now;

    public void SetUtcNow(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now;
}
```

Create `backend/MicroLIMS.Tests/UnitTests/WorkflowProfileModelTests.cs`:
```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Json;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// DateOnly has no precedent in the Domain layer. These tests prove the two
// places it could go wrong before anything else is built on it.
public class WorkflowProfileModelTests
{
    [Fact]
    public void WorkflowProfileVersions_MapsToTheExpectedColumns()
    {
        // GenerateCreateScript builds the Npgsql model without opening a connection.
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=x;Password=x")
            .Options;
        using var db = new MicroLimsDbContext(options);

        var script = db.Database.GenerateCreateScript();
        var start = script.IndexOf("CREATE TABLE \"WorkflowProfileVersions\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "WorkflowProfileVersions is not part of the model.");
        var block = script.Substring(start, script.IndexOf(");", start, StringComparison.Ordinal) - start);

        Assert.Contains("\"EffectiveFrom\" date NOT NULL", block);
        Assert.Contains("\"WorkUnits\" numeric(8,2)", block);
        Assert.Contains("\"TargetAnalystDays\" numeric(6,2)", block);
        Assert.Contains("\"TestCode\" character varying(50)", block);
        Assert.Contains("\"ReasonForChange\" character varying(500) NOT NULL", block);
        Assert.Contains("CK_WorkflowProfileVersions_UnitsPositive", block);
        Assert.Contains("CK_WorkflowProfileVersions_TargetPositive", block);
        Assert.Contains("CK_WorkflowProfileVersions_AnyValue", block);
        Assert.Contains("IX_WorkflowProfileVersions_WorkflowType_TestCode_EffectiveFrom", script);
    }

    private sealed record DateHolder(DateOnly EffectiveFrom);

    [Fact]
    public void DateOnly_RoundTripsAsIsoDate_ThroughTheApiJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJsonOptions.Configure(options);

        var json = JsonSerializer.Serialize(new DateHolder(new DateOnly(2026, 10, 11)), options);
        Assert.Equal("{\"effectiveFrom\":\"2026-10-11\"}", json);

        var back = JsonSerializer.Deserialize<DateHolder>("{\"effectiveFrom\":\"2026-10-11\"}", options);
        Assert.Equal(new DateOnly(2026, 10, 11), back!.EffectiveFrom);
    }
}
```

- [ ] **Step 3: Run the tests, expect a compile failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~WorkflowProfileModelTests"
```
Expected: the build succeeds. `WorkflowProfileVersions_MapsToTheExpectedColumns` FAILS with "WorkflowProfileVersions is not part of the model."; the DateOnly JSON test PASSES. If the JSON test fails, `DateOnly` does not round-trip: STOP, switch `EffectiveFrom` to `DateTime` with `HasColumnType("date")` everywhere in this plan, and report the deviation.
Fallback: `GenerateCreateScript()` is not used elsewhere in the test project. If it throws without a live connection, assert on `db.Model.FindEntityType(typeof(WorkflowProfileVersion))` instead, checking the relational column types and check constraints.

- [ ] **Step 4: Implement the domain types and mapping**

Create `backend/MicroLIMS.Domain/Enums/WorkflowProfileStatus.cs`:
```csharp
namespace MicroLIMS.Domain.Enums;

// Stored as int. A Draft has no effect on any calculation until it is signed.
public enum WorkflowProfileStatus
{
    Draft = 0,
    Effective = 1
}
```

Create `backend/MicroLIMS.Domain/Entities/WorkflowProfileVersion.cs`:
```csharp
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One version of a workflow profile (KPI remodel S1, spec section 2). Rows are
// append-only: a Draft is signed in place once (Draft -> Effective) and then
// never changes; a correction is a new version. IVersionedEntity exists only so
// that signing a Draft can be guarded by If-Match; Version maps to xmin.
public class WorkflowProfileVersion : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }

    public WorkflowType WorkflowType { get; set; }

    // null = the default for the whole WorkflowType; set = override for that one test.
    public string? TestCode { get; set; }

    // 1 unit = 1 standard analyst-hour. null = inherit from the type default.
    public decimal? WorkUnits { get; set; }

    // Analyst-stage target in days. null = inherit from the type default.
    public decimal? TargetAnalystDays { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public WorkflowProfileStatus Status { get; set; }
    public string ReasonForChange { get; set; } = string.Empty;

    public int? ElectronicSignatureId { get; set; }
    public ElectronicSignature? ElectronicSignature { get; set; }

    // Plain int, no FK (registered in UserReferenceRegistry). 0 = created by a migration.
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

Create `backend/MicroLIMS.Persistence/Configurations/WorkflowProfileVersionConfiguration.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class WorkflowProfileVersionConfiguration : IEntityTypeConfiguration<WorkflowProfileVersion>
{
    public void Configure(EntityTypeBuilder<WorkflowProfileVersion> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.TestCode).HasMaxLength(50);
        builder.Property(v => v.WorkUnits).HasPrecision(8, 2);
        builder.Property(v => v.TargetAnalystDays).HasPrecision(6, 2);
        builder.Property(v => v.EffectiveFrom).HasColumnType("date");
        builder.Property(v => v.ReasonForChange).HasMaxLength(500).IsRequired();

        builder.HasIndex(v => new { v.WorkflowType, v.TestCode, v.EffectiveFrom })
            .HasDatabaseName("IX_WorkflowProfileVersions_WorkflowType_TestCode_EffectiveFrom");

        builder.HasOne(v => v.ElectronicSignature).WithMany()
            .HasForeignKey(v => v.ElectronicSignatureId).OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_WorkflowProfileVersions_UnitsPositive", "\"WorkUnits\" IS NULL OR \"WorkUnits\" > 0");
            t.HasCheckConstraint("CK_WorkflowProfileVersions_TargetPositive", "\"TargetAnalystDays\" IS NULL OR \"TargetAnalystDays\" > 0");
            t.HasCheckConstraint("CK_WorkflowProfileVersions_AnyValue", "\"WorkUnits\" IS NOT NULL OR \"TargetAnalystDays\" IS NOT NULL");
        });
    }
}
```

In `IMicroLimsDbContext.cs` after line 95 (`DbSet<WorkloadWeightHistory> WorkloadWeightHistories { get; }`) add:
```csharp
    DbSet<WorkflowProfileVersion> WorkflowProfileVersions { get; }
```
In `MicroLimsDbContext.cs` after line 95 (`public DbSet<WorkloadWeightHistory> WorkloadWeightHistories => ...`) add:
```csharp
    public DbSet<WorkflowProfileVersion> WorkflowProfileVersions => Set<WorkflowProfileVersion>();
```
In `UserReferenceRegistry.cs`, immediately before the closing `};` that follows the `MasterDataImport` entries (after the `VerifiedByUserId` line, ~235) add:
```csharp

        // KPI remodel - S1
        new UserReferenceEntry(typeof(WorkflowProfileVersion), nameof(WorkflowProfileVersion.CreatedByUserId), UserReferenceDisposition.Blocks, "No DB FK - GMP record, author of a workflow profile version (0 = migration)"),
```
In `ReviewGateService.cs`, inside `ReviewEntityTypes` after `MasterDataImport`:
```csharp
    public const string WorkflowProfile = "WorkflowProfile";
```

- [ ] **Step 5: Run the tests and the registry guards, expect pass**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~WorkflowProfileModelTests|FullyQualifiedName~UserReferenceRegistry"
```
Expected: all PASS (`UserReferenceRegistry_AccountsForEveryUserIdNamedPropertyInTheModel` proves the registry line is complete).

- [ ] **Step 6: Commit**

```
git diff -- backend/MicroLIMS.Application/Abstractions/Persistence/IMicroLimsDbContext.cs backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs backend/MicroLIMS.Application/Services/UserReferenceRegistry.cs backend/MicroLIMS.Application/Services/ReviewGateService.cs
git add backend/MicroLIMS.Domain/Enums/WorkflowProfileStatus.cs backend/MicroLIMS.Domain/Entities/WorkflowProfileVersion.cs backend/MicroLIMS.Persistence/Configurations/WorkflowProfileVersionConfiguration.cs backend/MicroLIMS.Tests/Fixtures/FixedTimeProvider.cs backend/MicroLIMS.Tests/UnitTests/WorkflowProfileModelTests.cs
git add -p backend/MicroLIMS.Application/Abstractions/Persistence/IMicroLimsDbContext.cs backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs backend/MicroLIMS.Application/Services/UserReferenceRegistry.cs backend/MicroLIMS.Application/Services/ReviewGateService.cs
git commit -m "feat(kpi): WorkflowProfileVersion entity, mapping and clock fixture

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
(`git add -p`: answer `y` only for the WorkflowProfile hunks. If a file has no foreign hunks, plain `git add <file>` is fine.)

---

### Task 2: WorkflowProfileResolver (pure per-field resolution)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileResolver.cs`
- Test: `backend/MicroLIMS.Tests/UnitTests/WorkflowProfileResolverTests.cs`

**Interfaces:**
- Consumes: `WorkflowProfileVersion`, `WorkflowProfileStatus`, `WorkflowType` (Task 1).
- Produces:
  - `public sealed record ResolvedProfile(decimal? WorkUnits, decimal? TargetAnalystDays, int? WorkUnitsVersionId, int? TargetVersionId)` with `bool IsWeighted`, `bool IsTargetAware`.
  - `public sealed class WorkflowProfileResolver(IEnumerable<WorkflowProfileVersion> versions, IReadOnlyDictionary<string, WorkflowType> workflowTypeByTestCode)` with `ResolvedProfile Resolve(string testCode, DateTime at)`, plus two additive helpers used by the overview: `ResolvedProfile ResolveDefault(WorkflowType type, DateTime at)` and `bool HasOverride(string testCode, DateTime at)`.

- [ ] **Step 1: Write the failing tests**

Create `backend/MicroLIMS.Tests/UnitTests/WorkflowProfileResolverTests.cs`:
```csharp
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class WorkflowProfileResolverTests
{
    private static WorkflowProfileVersion V(int id, WorkflowType type, string? code, decimal? units, decimal? target,
        string from, WorkflowProfileStatus status = WorkflowProfileStatus.Effective) => new()
    {
        Id = id, WorkflowType = type, TestCode = code, WorkUnits = units, TargetAnalystDays = target,
        EffectiveFrom = DateOnly.Parse(from), Status = status, ReasonForChange = "test"
    };

    private static readonly IReadOnlyDictionary<string, WorkflowType> Types = new Dictionary<string, WorkflowType>
    {
        ["TAMC"] = WorkflowType.CountTest,
        ["TYMC"] = WorkflowType.CountTest,
        ["PH"] = WorkflowType.Measurement
    };

    private static readonly DateTime Oct11 = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Resolve_PicksLatestEffectiveVersionAtOrBeforeApproval()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-01-01"),
            V(2, WorkflowType.CountTest, null, 2m, 9m, "2026-10-01"),
            V(3, WorkflowType.CountTest, null, 3m, 11m, "2026-10-20")
        }, Types);

        var p = r.Resolve("TAMC", Oct11);

        Assert.Equal(2m, p.WorkUnits);
        Assert.Equal(9m, p.TargetAnalystDays);
        Assert.Equal(2, p.WorkUnitsVersionId);
        Assert.Equal(2, p.TargetVersionId);
        Assert.True(p.IsWeighted);
        Assert.True(p.IsTargetAware);
    }

    [Fact]
    public void Resolve_UsesTheDateOfApproval_NotTheTimeOfDay()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-10-01"),
            V(2, WorkflowType.CountTest, null, 2m, 9m, "2026-10-12")
        }, Types);

        Assert.Equal(1m, r.Resolve("TAMC", new DateTime(2026, 10, 11, 23, 59, 59, DateTimeKind.Utc)).WorkUnits);
        Assert.Equal(2m, r.Resolve("TAMC", new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc)).WorkUnits);
    }

    [Fact]
    public void Resolve_FutureOnlyVersion_IsUnresolved()
    {
        var r = new WorkflowProfileResolver(new[] { V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-10-12") }, Types);

        var p = r.Resolve("TAMC", Oct11);

        Assert.False(p.IsWeighted);
        Assert.False(p.IsTargetAware);
        Assert.Null(p.WorkUnitsVersionId);
    }

    [Fact]
    public void Resolve_OverrideBeatsTypeDefault()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-01-01"),
            V(2, WorkflowType.CountTest, "TAMC", 4m, 14m, "2026-01-01")
        }, Types);

        Assert.Equal(4m, r.Resolve("TAMC", Oct11).WorkUnits);
        Assert.Equal(1m, r.Resolve("TYMC", Oct11).WorkUnits);
    }

    [Fact]
    public void Resolve_OverrideWithOnlyUnits_TakesTargetFromTypeDefault()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-01-01"),
            V(2, WorkflowType.CountTest, "TAMC", 4m, null, "2026-01-01")
        }, Types);

        var p = r.Resolve("TAMC", Oct11);

        Assert.Equal(4m, p.WorkUnits);
        Assert.Equal(2, p.WorkUnitsVersionId);
        Assert.Equal(7m, p.TargetAnalystDays);
        Assert.Equal(1, p.TargetVersionId);
    }

    [Fact]
    public void Resolve_OverrideWithOnlyTarget_TakesUnitsFromTypeDefault()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-01-01"),
            V(2, WorkflowType.CountTest, "TAMC", null, 14m, "2026-01-01")
        }, Types);

        var p = r.Resolve("TAMC", Oct11);

        Assert.Equal(1m, p.WorkUnits);
        Assert.Equal(14m, p.TargetAnalystDays);
    }

    [Fact]
    public void Resolve_LaterDefaultWithOnlyTarget_DoesNotBlankEarlierUnits()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 2m, null, "2026-01-01"),
            V(2, WorkflowType.CountTest, null, null, 10m, "2026-09-01")
        }, Types);

        var p = r.Resolve("TAMC", Oct11);

        Assert.Equal(2m, p.WorkUnits);
        Assert.Equal(1, p.WorkUnitsVersionId);
        Assert.Equal(10m, p.TargetAnalystDays);
        Assert.Equal(2, p.TargetVersionId);
    }

    [Fact]
    public void Resolve_IgnoresDrafts()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 9m, 9m, "2026-01-01", WorkflowProfileStatus.Draft),
            V(2, WorkflowType.CountTest, "TAMC", 9m, 9m, "2026-01-01", WorkflowProfileStatus.Draft)
        }, Types);

        Assert.False(r.Resolve("TAMC", Oct11).IsWeighted);
    }

    [Fact]
    public void Resolve_SameEffectiveFrom_HigherIdWins()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(5, WorkflowType.CountTest, null, 3m, 7m, "2026-10-01"),
            V(9, WorkflowType.CountTest, null, 4m, 7m, "2026-10-01")
        }, Types);

        Assert.Equal(4m, r.Resolve("TAMC", Oct11).WorkUnits);
    }

    [Fact]
    public void Resolve_UnknownTestCode_ResolvesToNothing()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-01-01"),
            V(2, WorkflowType.CountTest, "GHOST", 5m, 5m, "2026-01-01")
        }, Types);

        Assert.False(r.Resolve("GHOST", Oct11).IsWeighted);
    }

    [Fact]
    public void Resolve_AfterTestWorkflowTypeEdit_UsesNewTypeDefault_OverrideUnaffected()
    {
        var versions = new[]
        {
            V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-01-01"),
            V(2, WorkflowType.Measurement, null, 3m, 5m, "2026-01-01"),
            V(3, WorkflowType.CountTest, "TYMC", 8m, null, "2026-01-01")
        };

        var before = new WorkflowProfileResolver(versions, Types);
        Assert.Equal(1m, before.Resolve("TAMC", Oct11).WorkUnits);

        // An administrator re-types TAMC (and TYMC) as Measurement in the test master.
        var edited = new Dictionary<string, WorkflowType>(Types)
        {
            ["TAMC"] = WorkflowType.Measurement,
            ["TYMC"] = WorkflowType.Measurement
        };
        var after = new WorkflowProfileResolver(versions, edited);

        Assert.Equal(3m, after.Resolve("TAMC", Oct11).WorkUnits);
        // The override is keyed by test code, so it keeps winning for units;
        // its missing target now falls back to the NEW type's default.
        var tymc = after.Resolve("TYMC", Oct11);
        Assert.Equal(8m, tymc.WorkUnits);
        Assert.Equal(5m, tymc.TargetAnalystDays);
    }

    [Fact]
    public void ResolveDefault_AndHasOverride_ReadOnlyTheirOwnScope()
    {
        var r = new WorkflowProfileResolver(new[]
        {
            V(1, WorkflowType.CountTest, null, 1m, 7m, "2026-01-01"),
            V(2, WorkflowType.CountTest, "TAMC", 4m, null, "2026-01-01"),
            V(3, WorkflowType.CountTest, "TYMC", 4m, null, "2026-12-01")
        }, Types);

        Assert.Equal(1m, r.ResolveDefault(WorkflowType.CountTest, Oct11).WorkUnits);
        Assert.False(r.ResolveDefault(WorkflowType.Gravimetric, Oct11).IsWeighted);
        Assert.True(r.HasOverride("TAMC", Oct11));
        Assert.False(r.HasOverride("TYMC", Oct11));
        Assert.False(r.HasOverride("PH", Oct11));
    }
}
```

- [ ] **Step 2: Run the tests, expect a compile failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~WorkflowProfileResolverTests"
```
Expected: build error `The type or namespace name 'WorkflowProfileResolver' could not be found`.

- [ ] **Step 3: Implement the resolver**

Create `backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileResolver.cs`:
```csharp
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

public sealed record ResolvedProfile(decimal? WorkUnits, decimal? TargetAnalystDays, int? WorkUnitsVersionId, int? TargetVersionId)
{
    public bool IsWeighted => WorkUnits.HasValue;
    public bool IsTargetAware => TargetAnalystDays.HasValue;
}

// Resolves a test's profile per field (spec 2.2). Pure after construction:
// callers load the Effective versions and the TestCode -> WorkflowType map once
// and resolve any number of tests without touching the database. The type is the
// test definition's CURRENT type (spec 2.2), so re-typing a test moves its history
// to the new type's default; an override keyed by test code is unaffected.
public sealed class WorkflowProfileResolver
{
    private static readonly ResolvedProfile Unresolved = new(null, null, null, null);

    private readonly ILookup<string, WorkflowProfileVersion> _overrides;
    private readonly ILookup<WorkflowType, WorkflowProfileVersion> _defaults;
    private readonly IReadOnlyDictionary<string, WorkflowType> _typeByTestCode;

    public WorkflowProfileResolver(
        IEnumerable<WorkflowProfileVersion> versions,
        IReadOnlyDictionary<string, WorkflowType> workflowTypeByTestCode)
    {
        _typeByTestCode = workflowTypeByTestCode;

        // Newest first; ToLookup keeps that order inside each group.
        var effective = versions
            .Where(v => v.Status == WorkflowProfileStatus.Effective)
            .OrderByDescending(v => v.EffectiveFrom)
            .ThenByDescending(v => v.Id)
            .ToList();
        _overrides = effective.Where(v => v.TestCode != null).ToLookup(v => v.TestCode!, StringComparer.Ordinal);
        _defaults = effective.Where(v => v.TestCode == null).ToLookup(v => v.WorkflowType);
    }

    // `at` is the approval time in UTC; only its calendar date is compared with EffectiveFrom.
    public ResolvedProfile Resolve(string testCode, DateTime at)
    {
        if (!_typeByTestCode.TryGetValue(testCode, out var type)) return Unresolved;
        var day = DateOnly.FromDateTime(at);
        return Pick(InForce(_overrides[testCode], day), InForce(_defaults[type], day));
    }

    // The type default alone, for the configuration overview.
    public ResolvedProfile ResolveDefault(WorkflowType type, DateTime at) =>
        Pick(Array.Empty<WorkflowProfileVersion>(), InForce(_defaults[type], DateOnly.FromDateTime(at)));

    public bool HasOverride(string testCode, DateTime at)
    {
        var day = DateOnly.FromDateTime(at);
        return _overrides[testCode].Any(v => v.EffectiveFrom <= day);
    }

    private static ResolvedProfile Pick(IReadOnlyList<WorkflowProfileVersion> own, IReadOnlyList<WorkflowProfileVersion> def)
    {
        var units = own.FirstOrDefault(v => v.WorkUnits.HasValue) ?? def.FirstOrDefault(v => v.WorkUnits.HasValue);
        var target = own.FirstOrDefault(v => v.TargetAnalystDays.HasValue) ?? def.FirstOrDefault(v => v.TargetAnalystDays.HasValue);
        return new ResolvedProfile(units?.WorkUnits, target?.TargetAnalystDays, units?.Id, target?.Id);
    }

    private static IReadOnlyList<WorkflowProfileVersion> InForce(IEnumerable<WorkflowProfileVersion> rows, DateOnly day) =>
        rows.Where(v => v.EffectiveFrom <= day).ToList();
}
```

- [ ] **Step 4: Run the tests, expect pass**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~WorkflowProfileResolverTests"
```
Expected: 12 PASS.

- [ ] **Step 5: Commit**

```
git add backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileResolver.cs backend/MicroLIMS.Tests/UnitTests/WorkflowProfileResolverTests.cs
git commit -m "feat(kpi): per-field WorkflowProfileResolver

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### Task 3: WorkflowProfileService write path (validate, sign, audit)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileDtos.cs`
- Create: `backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs`
- Modify: `backend/MicroLIMS.Tests/TestServiceFactory.cs` (add `using MicroLIMS.Application.Services.Kpi;` near line 5 and a factory method after `MediaProduct`, line ~184)
- Test: `backend/MicroLIMS.Tests/UnitTests/WorkflowProfileServiceTests.cs`

**Interfaces:**
- Consumes: `IMicroLimsDbContext.WorkflowProfileVersions` and `ReviewEntityTypes.WorkflowProfile` (Task 1); `IElectronicSignatureService.SignAsync(int userId, string password, SignatureMeaning meaning, string entityType, int entityId, string? comment, string? ipAddress)` (existing); `IAuditEventService.RecordUserEventAsync(string actionCode, AuditActionCategory category, string recordType, int? documentMasterId = null, int? documentRevisionId = null, string? reason = null, Guid? correlationId = null, string? sourceContext = null, IEnumerable<AuditFieldChange>? changes = null, string? entityId = null, CancellationToken ct = default, int? sampleId = null)` (existing); `UnitOfWork.RunAsync<T>(IMicroLimsDbContext, Func<Task<T>>)`, `RecordVersion.EnsureCurrent(db, record)` (existing).
- Produces:
  - DTOs exactly as contract 2.4: `WorkflowProfileVersionDto`, `CreateWorkflowProfileVersionRequest`, `SignWorkflowProfileDraftRequest`, `ProfileCoverage`, `WorkflowProfileTestRowDto`, `WorkflowProfileTypeRowDto`, `WorkflowProfileOverviewDto`, `WorkflowProfileCoverageDto`.
  - `WorkflowProfileService(IMicroLimsDbContext db, IElectronicSignatureService sig, IAuditEventService audit, TimeProvider? timeProvider = null)`; constants `VersionAddedActionCode = "WorkflowProfileVersionAdded"`, `DraftSignedActionCode = "WorkflowProfileDraftSigned"`; `public static readonly WorkflowType[] LiveTypes`; `Task<WorkflowProfileVersionDto> AddVersionAsync(CreateWorkflowProfileVersionRequest r, int userId, string? ip, CancellationToken ct = default)`; `Task<WorkflowProfileVersionDto> SignDraftAsync(int id, SignWorkflowProfileDraftRequest r, int userId, string? ip, CancellationToken ct = default)`.
  - `TestServiceFactory.WorkflowProfile(MicroLimsDbContext db, TimeProvider? time = null)`.

- [ ] **Step 1: Write the failing tests**

Create `backend/MicroLIMS.Tests/UnitTests/WorkflowProfileServiceTests.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class WorkflowProfileServiceTests
{
    private const string Password = "Correct-Password-1!";
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 11);

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        db.CurrentUserId = 1;
        return db;
    }

    private static WorkflowProfileService NewService(MicroLimsDbContext db) =>
        TestServiceFactory.WorkflowProfile(db, new FixedTimeProvider(Now));

    private static async Task<User> SeedUserAsync(MicroLimsDbContext db, string fullName = "Test SectionHead")
    {
        var role = new Role { Type = RoleType.SectionHead, Name = "Section Head" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User
        {
            FullName = fullName, Username = "sectionhead", RoleId = role.Id,
            PasswordHash = TestPasswords.Hash(Password)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task SeedTestAsync(MicroLimsDbContext db, string code, WorkflowType type, bool active = true)
    {
        db.TestDefinitions.Add(new TestDefinition
        {
            Code = code, DisplayName = code + " name", SectionId = 1, WorkflowType = type, IsActive = active
        });
        await db.SaveChangesAsync();
    }

    private static async Task<WorkflowProfileVersion> SeedRowAsync(MicroLimsDbContext db, WorkflowType type, string? code,
        decimal? units, decimal? target, DateOnly from, WorkflowProfileStatus status, int createdBy = 0)
    {
        var row = new WorkflowProfileVersion
        {
            WorkflowType = type, TestCode = code, WorkUnits = units, TargetAnalystDays = target,
            EffectiveFrom = from, Status = status, ReasonForChange = "Imported from legacy workload weight",
            CreatedByUserId = createdBy, CreatedAt = Now.UtcDateTime
        };
        db.WorkflowProfileVersions.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    private static CreateWorkflowProfileVersionRequest Create(
        WorkflowType type = WorkflowType.CountTest, string? testCode = null, decimal? units = 1.5m,
        decimal? target = 7m, DateOnly? from = null, string reason = "SOP time study 2026", string password = Password) =>
        new(type, testCode, units, target, from ?? Today, reason, password);

    private static SignWorkflowProfileDraftRequest Sign(
        decimal? units = 2m, decimal? target = 10m, DateOnly? from = null, string reason = "Reviewed against the SOP", string password = Password) =>
        new(units, target, from ?? Today, reason, password);

    // ---- AddVersionAsync -------------------------------------------------

    [Fact]
    public async Task AddVersion_CorrectPassword_CreatesEffectiveRowSignatureAndAuditEvent()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = NewService(db);

        var dto = await service.AddVersionAsync(Create(), user.Id, "10.0.0.1");

        Assert.Equal(WorkflowProfileStatus.Effective, dto.Status);
        Assert.Equal(WorkflowType.CountTest, dto.WorkflowType);
        Assert.Null(dto.TestCode);
        Assert.Equal(1.5m, dto.WorkUnits);
        Assert.Equal(7m, dto.TargetAnalystDays);
        Assert.Equal(Today, dto.EffectiveFrom);
        Assert.Equal("SOP time study 2026", dto.ReasonForChange);
        Assert.Equal(user.Id, dto.CreatedByUserId);
        Assert.Equal("Test SectionHead", dto.CreatedByName);
        Assert.NotNull(dto.ElectronicSignatureId);
        Assert.NotNull(dto.SignedAt);

        var signature = Assert.Single(await db.ElectronicSignatures.ToListAsync());
        Assert.Equal(SignatureMeaning.MasterDataChanged, signature.MeaningOfSignature);
        Assert.Equal(ReviewEntityTypes.WorkflowProfile, signature.EntityType);
        Assert.Equal(dto.Id, signature.EntityId);
        Assert.Equal("10.0.0.1", signature.IpAddress);
        Assert.Contains("CountTest type default", signature.Comment);
        Assert.Contains("SOP time study 2026", signature.Comment);

        var audit = Assert.Single(await db.AuditLogs.Include(a => a.Changes)
            .Where(a => a.ActionCode == WorkflowProfileService.VersionAddedActionCode).ToListAsync());
        Assert.Equal(ReviewEntityTypes.WorkflowProfile, audit.EntityName);
        Assert.Equal(AuditActionCategory.Configuration, audit.ActionCategory);
        Assert.Equal(dto.Id.ToString(), audit.EntityId);
        Assert.Equal("SOP time study 2026", audit.Reason);
        Assert.Contains(audit.Changes, c => c.FieldName == "WorkUnits" && c.PreviousValue == null && c.NewValue == "1.5");
        Assert.Contains(audit.Changes, c => c.FieldName == "TargetAnalystDays" && c.NewValue == "7");
        Assert.Contains(audit.Changes, c => c.FieldName == "EffectiveFrom" && c.NewValue == "2026-10-11");
    }

    [Fact]
    public async Task AddVersion_WrongPassword_ThrowsAndWritesNoSignatureOrAuditEvent()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = NewService(db);

        await Assert.ThrowsAsync<SignatureVerificationException>(
            () => service.AddVersionAsync(Create(password: "WrongPassword!"), user.Id, "127.0.0.1"));

        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
        Assert.Empty(await db.AuditLogs.Where(a => a.ActionCode == WorkflowProfileService.VersionAddedActionCode).ToListAsync());
        // The row itself is removed by the PostgreSQL transaction (UnitOfWork is a no-op on
        // InMemory); WorkflowProfileMigrationPostgresTests pins that half.
    }

    [Fact]
    public async Task AddVersion_Today_IsAllowed_AndYesterdayIsRejectedOnceTheScopeHasAnEffectiveVersion()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = NewService(db);
        await service.AddVersionAsync(Create(from: Today), user.Id, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddVersionAsync(Create(units: 2m, from: Today.AddDays(-1)), user.Id, null));

        Assert.Contains("cannot be in the past", ex.Message);
        Assert.Contains("2026-10-11", ex.Message);
    }

    [Fact]
    public async Task AddVersion_BaselineMayBeBackDated_ExactlyOnce()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = NewService(db);

        var first = await service.AddVersionAsync(Create(from: Today.AddDays(-400)), user.Id, null);
        Assert.Equal(Today.AddDays(-400), first.EffectiveFrom);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddVersionAsync(Create(units: 2m, from: Today.AddDays(-400)), user.Id, null));
    }

    [Fact]
    public async Task AddVersion_DraftDoesNotCountAsTheBaseline()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        await SeedRowAsync(db, WorkflowType.CountTest, null, 2m, null, Today.AddDays(-30), WorkflowProfileStatus.Draft);
        var service = NewService(db);

        var dto = await service.AddVersionAsync(Create(from: Today.AddDays(-60)), user.Id, null);

        Assert.Equal(Today.AddDays(-60), dto.EffectiveFrom);
    }

    [Fact]
    public async Task AddVersion_BaselineIsPerScope_TypeDefaultAndTestOverrideAreIndependent()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        await SeedTestAsync(db, "TAMC", WorkflowType.CountTest);
        var service = NewService(db);
        await service.AddVersionAsync(Create(from: Today), user.Id, null);

        var overrideRow = await service.AddVersionAsync(
            Create(testCode: "TAMC", units: 4m, target: null, from: Today.AddDays(-90)), user.Id, null);

        Assert.Equal("TAMC", overrideRow.TestCode);
        Assert.Equal(Today.AddDays(-90), overrideRow.EffectiveFrom);
    }

    [Theory]
    [InlineData(0.0, 7.0)]
    [InlineData(-1.0, 7.0)]
    [InlineData(1.0, 0.0)]
    [InlineData(1.0, -3.0)]
    [InlineData(12.345, 7.0)]        // would be silently rounded to 12.35 by numeric(8,2)
    [InlineData(1.0, 7.001)]         // numeric(6,2)
    [InlineData(1000000.0, 7.0)]     // numeric(8,2) overflow would be a 500
    [InlineData(1.0, 10000.0)]       // numeric(6,2) overflow
    public async Task AddVersion_InvalidNumbers_Throw(double units, double target)
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = NewService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddVersionAsync(Create(units: (decimal)units, target: (decimal)target), user.Id, null));

        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task AddVersion_NeitherValueSet_Throws()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(db).AddVersionAsync(Create(units: null, target: null), user.Id, null));
        Assert.Contains("work units, a target, or both", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddVersion_BlankReason_Throws(string reason)
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(db).AddVersionAsync(Create(reason: reason), user.Id, null));
        Assert.Contains("reason", ex.Message);
    }

    [Fact]
    public async Task AddVersion_ReasonOver500Characters_Throws()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(db).AddVersionAsync(Create(reason: new string('x', 501)), user.Id, null));
    }

    [Theory]
    [InlineData(WorkflowType.HplcAssay)]
    [InlineData(WorkflowType.ElementalAssay)]
    [InlineData(WorkflowType.HplcMultiAnalyte)]
    [InlineData(WorkflowType.StandardComparison)]
    public async Task AddVersion_RetiredWorkflowType_Throws(WorkflowType retired)
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(db).AddVersionAsync(Create(type: retired), user.Id, null));
        Assert.Contains("retired", ex.Message);
    }

    [Fact]
    public async Task AddVersion_UnknownTestCode_Throws()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(db).AddVersionAsync(Create(testCode: "GHOST"), user.Id, null));
        Assert.Contains("GHOST", ex.Message);
    }

    [Fact]
    public async Task AddVersion_TestCode_OverwritesTheRequestedTypeWithTheDefinitionsCurrentType()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        await SeedTestAsync(db, "TITR1", WorkflowType.Titration);

        var dto = await NewService(db).AddVersionAsync(
            Create(type: WorkflowType.CountTest, testCode: "TITR1"), user.Id, null);

        Assert.Equal(WorkflowType.Titration, dto.WorkflowType);
        Assert.Equal("TITR1 name", dto.TestName);
    }

    [Fact]
    public async Task AddVersion_OverrideOfATestWhoseTypeIsRetired_Throws()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        await SeedTestAsync(db, "OLDHPLC", WorkflowType.HplcAssay);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(db).AddVersionAsync(Create(testCode: "OLDHPLC"), user.Id, null));
    }

    // ---- SignDraftAsync --------------------------------------------------

    [Fact]
    public async Task SignDraft_CorrectPassword_AppliesFinalValuesSignsAndAudits()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var draft = await SeedRowAsync(db, WorkflowType.CountTest, null, 1m, null, Today, WorkflowProfileStatus.Draft);
        var service = NewService(db);

        var dto = await service.SignDraftAsync(draft.Id, Sign(units: 2.5m, target: 10m), user.Id, "10.0.0.2");

        Assert.Equal(WorkflowProfileStatus.Effective, dto.Status);
        Assert.Equal(2.5m, dto.WorkUnits);
        Assert.Equal(10m, dto.TargetAnalystDays);
        Assert.Equal("Reviewed against the SOP", dto.ReasonForChange);
        Assert.NotNull(dto.SignedAt);
        Assert.Equal(0, dto.CreatedByUserId); // the importer stays the creator
        Assert.Equal("System (legacy import)", dto.CreatedByName);

        var signature = Assert.Single(await db.ElectronicSignatures.ToListAsync());
        Assert.Equal(draft.Id, signature.EntityId);
        Assert.Equal(ReviewEntityTypes.WorkflowProfile, signature.EntityType);

        var audit = Assert.Single(await db.AuditLogs.Include(a => a.Changes)
            .Where(a => a.ActionCode == WorkflowProfileService.DraftSignedActionCode).ToListAsync());
        Assert.Contains(audit.Changes, c => c.FieldName == "WorkUnits" && c.PreviousValue == "1" && c.NewValue == "2.5");
    }

    [Fact]
    public async Task SignDraft_WrongPassword_LeavesTheDraftUntouched()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var draft = await SeedRowAsync(db, WorkflowType.CountTest, null, 1m, null, Today, WorkflowProfileStatus.Draft);

        await Assert.ThrowsAsync<SignatureVerificationException>(
            () => NewService(db).SignDraftAsync(draft.Id, Sign(password: "nope"), user.Id, null));

        var reloaded = await db.WorkflowProfileVersions.AsNoTracking().SingleAsync(v => v.Id == draft.Id);
        Assert.Equal(WorkflowProfileStatus.Draft, reloaded.Status);
        Assert.Equal(1m, reloaded.WorkUnits);
        Assert.Null(reloaded.ElectronicSignatureId);
        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task SignDraft_AlreadyEffective_Throws()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var draft = await SeedRowAsync(db, WorkflowType.CountTest, null, 1m, null, Today, WorkflowProfileStatus.Draft);
        var service = NewService(db);
        await service.SignDraftAsync(draft.Id, Sign(), user.Id, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SignDraftAsync(draft.Id, Sign(units: 3m), user.Id, null));

        Assert.Contains("already Effective", ex.Message);
        Assert.Single(await db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task SignDraft_StaleIfMatch_ThrowsConcurrency()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var draft = await SeedRowAsync(db, WorkflowType.CountTest, null, 1m, null, Today, WorkflowProfileStatus.Draft);
        db.ExpectedVersion = draft.Version + 5; // the form was loaded before someone else saved

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => NewService(db).SignDraftAsync(draft.Id, Sign(), user.Id, null));

        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task SignDraft_UnknownId_ThrowsNotFound()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        await Assert.ThrowsAsync<MicroLIMS.Shared.Exceptions.NotFoundException>(
            () => NewService(db).SignDraftAsync(999, Sign(), user.Id, null));
    }

    [Fact]
    public async Task SignDraft_StaleImportDate_RejectedWhenScopeHasEffective()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        await SeedRowAsync(db, WorkflowType.CountTest, null, 1m, 7m, Today.AddDays(-100), WorkflowProfileStatus.Effective, user.Id);
        var draft = await SeedRowAsync(db, WorkflowType.CountTest, null, 2m, null, Today.AddDays(-5), WorkflowProfileStatus.Draft);
        var service = NewService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SignDraftAsync(draft.Id, Sign(from: Today.AddDays(-5)), user.Id, null));
        Assert.Contains("cannot be in the past", ex.Message);

        var ok = await service.SignDraftAsync(draft.Id, Sign(from: Today), user.Id, null);
        Assert.Equal(WorkflowProfileStatus.Effective, ok.Status);
        Assert.Equal(Today, ok.EffectiveFrom);
    }

    [Fact]
    public async Task SignDraft_StaleImportDate_AllowedAsBaseline()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var draft = await SeedRowAsync(db, WorkflowType.CountTest, null, 2m, null, Today.AddDays(-5), WorkflowProfileStatus.Draft);

        var dto = await NewService(db).SignDraftAsync(draft.Id, Sign(from: Today.AddDays(-5)), user.Id, null);

        Assert.Equal(Today.AddDays(-5), dto.EffectiveFrom);
    }

    [Fact]
    public async Task SignDraft_FollowsTheTestsCurrentWorkflowType()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        await SeedTestAsync(db, "TAMC", WorkflowType.Measurement); // re-typed since the import
        var draft = await SeedRowAsync(db, WorkflowType.CountTest, "TAMC", 2m, null, Today, WorkflowProfileStatus.Draft);

        var dto = await NewService(db).SignDraftAsync(draft.Id, Sign(), user.Id, null);

        Assert.Equal(WorkflowType.Measurement, dto.WorkflowType);
    }

    [Fact]
    public async Task SignDraft_InvalidNumbers_Throw()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var draft = await SeedRowAsync(db, WorkflowType.CountTest, null, 2m, null, Today, WorkflowProfileStatus.Draft);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(db).SignDraftAsync(draft.Id, Sign(units: 12.345m), user.Id, null));
    }
}
```

- [ ] **Step 2: Run the tests, expect a compile failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~WorkflowProfileServiceTests"
```
Expected: build error `'TestServiceFactory' does not contain a definition for 'WorkflowProfile'` / missing `WorkflowProfileService`.

- [ ] **Step 3: Implement DTOs, service and factory**

Create `backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileDtos.cs`:
```csharp
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Application.Services.Kpi;

public sealed record WorkflowProfileVersionDto(
    int Id, WorkflowType WorkflowType, string? TestCode, string? TestName,
    decimal? WorkUnits, decimal? TargetAnalystDays, DateOnly EffectiveFrom, WorkflowProfileStatus Status,
    string ReasonForChange, int? ElectronicSignatureId, int CreatedByUserId, string CreatedByName,
    DateTime CreatedAt, DateTime? SignedAt, uint Version);

// WorkUnits / TargetAnalystDays are spelled identically on the wire and here: the
// legacy PUT bound "weight" to a differently named property and silently saved 0.
public sealed record CreateWorkflowProfileVersionRequest(
    WorkflowType WorkflowType, string? TestCode, decimal? WorkUnits,
    decimal? TargetAnalystDays, DateOnly EffectiveFrom, string ReasonForChange, string Password);

public sealed record SignWorkflowProfileDraftRequest(
    decimal? WorkUnits, decimal? TargetAnalystDays,
    DateOnly EffectiveFrom, string ReasonForChange, string Password);

public enum ProfileCoverage { Configured, Partial, NeedsConfiguration }

public sealed record WorkflowProfileTestRowDto(
    string TestCode, string TestName, decimal? EffectiveWorkUnits,
    decimal? EffectiveTargetAnalystDays, bool HasOverride, ProfileCoverage Coverage);

public sealed record WorkflowProfileTypeRowDto(
    WorkflowType WorkflowType, decimal? EffectiveWorkUnits,
    decimal? EffectiveTargetAnalystDays, DateOnly? LastEffectiveFrom, DateTime? LastChangeAt, string? LastChangeByName,
    ProfileCoverage Coverage, int DraftCount, IReadOnlyList<WorkflowProfileTestRowDto> Tests);

public sealed record WorkflowProfileOverviewDto(IReadOnlyList<WorkflowProfileTypeRowDto> Types);

public sealed record WorkflowProfileCoverageDto(
    IReadOnlyList<WorkflowType> TypesWithoutUnits, IReadOnlyList<WorkflowType> TypesWithoutTarget,
    IReadOnlyList<WorkflowProfileTestRowDto> TestsNeedingConfiguration);
```

Create `backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs`:
```csharp
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Exceptions;

namespace MicroLIMS.Application.Services.Kpi;

// Workflow profiles (KPI remodel S1, spec section 2): validation, e-signature and
// audit for the append-only WorkflowProfileVersions table, plus the read models
// behind the configuration panel. The legacy WorkloadWeight maths are untouched.
public class WorkflowProfileService
{
    public const string VersionAddedActionCode = "WorkflowProfileVersionAdded";
    public const string DraftSignedActionCode = "WorkflowProfileDraftSigned";

    // Retired types (HplcAssay, ElementalAssay, HplcMultiAnalyte, StandardComparison) are
    // deliberately absent: they cannot carry a profile.
    public static readonly WorkflowType[] LiveTypes =
    {
        WorkflowType.CountTest, WorkflowType.Observation, WorkflowType.Measurement, WorkflowType.Gravimetric,
        WorkflowType.Qualitative, WorkflowType.Dissolution, WorkflowType.Disintegration, WorkflowType.WeightVariation,
        WorkflowType.HplcMethodAssay, WorkflowType.Titration, WorkflowType.IcpMethodAssay
    };

    private const decimal MaxWorkUnits = 999999.99m; // numeric(8,2)
    private const decimal MaxTargetDays = 9999.99m;  // numeric(6,2)

    private readonly IMicroLimsDbContext _db;
    private readonly IElectronicSignatureService _sig;
    private readonly IAuditEventService _audit;
    private readonly TimeProvider _time;

    public WorkflowProfileService(
        IMicroLimsDbContext db,
        IElectronicSignatureService sig,
        IAuditEventService audit,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _sig = sig;
        _audit = audit;
        _time = timeProvider ?? TimeProvider.System;
    }

    // ---- Writes ----------------------------------------------------------

    public Task<WorkflowProfileVersionDto> AddVersionAsync(
        CreateWorkflowProfileVersionRequest r, int userId, string? ip, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, () => AddVersionCoreAsync(r, userId, ip, ct));

    private async Task<WorkflowProfileVersionDto> AddVersionCoreAsync(
        CreateWorkflowProfileVersionRequest r, int userId, string? ip, CancellationToken ct)
    {
        var (type, code, reason) = await ValidateAsync(
            r.WorkflowType, r.TestCode, r.WorkUnits, r.TargetAnalystDays, r.ReasonForChange, ct);
        await EnsureEffectiveFromAsync(type, code, r.EffectiveFrom, ct);

        _db.CurrentUserId = userId;
        var row = new WorkflowProfileVersion
        {
            WorkflowType = type,
            TestCode = code,
            WorkUnits = r.WorkUnits,
            TargetAnalystDays = r.TargetAnalystDays,
            EffectiveFrom = r.EffectiveFrom,
            // Inserted as Draft: the append-only trigger (Task 6) rejects any UPDATE of an Effective row,
            // and attaching the signature is an update. Draft -> Effective is the one permitted mutation.
            Status = WorkflowProfileStatus.Draft,
            ReasonForChange = reason,
            CreatedByUserId = userId,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        };
        _db.WorkflowProfileVersions.Add(row);
        await _db.SaveChangesAsync(ct); // the signature needs the row id; the transaction undoes this on a bad password

        row.ElectronicSignature = await _sig.SignAsync(
            userId, r.Password, SignatureMeaning.MasterDataChanged, ReviewEntityTypes.WorkflowProfile, row.Id,
            SignatureComment(type, code, r.WorkUnits, r.TargetAnalystDays, r.EffectiveFrom, reason), ip);
        row.Status = WorkflowProfileStatus.Effective; // flipped before the audit event's save, in the same update

        await _audit.RecordUserEventAsync(
            VersionAddedActionCode, AuditActionCategory.Configuration, ReviewEntityTypes.WorkflowProfile,
            reason: reason, changes: Changes(null, null, null, row), entityId: row.Id.ToString(),
            cancellationToken: ct);

        return await ToDtoAsync(row, ct);
    }

    public Task<WorkflowProfileVersionDto> SignDraftAsync(
        int id, SignWorkflowProfileDraftRequest r, int userId, string? ip, CancellationToken ct = default) =>
        UnitOfWork.RunAsync(_db, () => SignDraftCoreAsync(id, r, userId, ip, ct));

    private async Task<WorkflowProfileVersionDto> SignDraftCoreAsync(
        int id, SignWorkflowProfileDraftRequest r, int userId, string? ip, CancellationToken ct)
    {
        var draft = await _db.WorkflowProfileVersions.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw new NotFoundException($"Workflow profile version {id} was not found.");
        if (draft.Status != WorkflowProfileStatus.Draft)
            throw new InvalidOperationException("This version is already Effective and cannot be signed again.");
        RecordVersion.EnsureCurrent(_db, draft);

        var (type, code, reason) = await ValidateAsync(
            draft.WorkflowType, draft.TestCode, r.WorkUnits, r.TargetAnalystDays, r.ReasonForChange, ct);
        await EnsureEffectiveFromAsync(type, code, r.EffectiveFrom, ct);

        var previousUnits = draft.WorkUnits;
        var previousTarget = draft.TargetAnalystDays;
        var previousFrom = draft.EffectiveFrom;

        // Sign first: a wrong password throws before any field of the draft changes.
        _db.CurrentUserId = userId;
        var signature = await _sig.SignAsync(
            userId, r.Password, SignatureMeaning.MasterDataChanged, ReviewEntityTypes.WorkflowProfile, draft.Id,
            SignatureComment(type, code, r.WorkUnits, r.TargetAnalystDays, r.EffectiveFrom, reason), ip);

        draft.WorkflowType = type;
        draft.WorkUnits = r.WorkUnits;
        draft.TargetAnalystDays = r.TargetAnalystDays;
        draft.EffectiveFrom = r.EffectiveFrom;
        draft.ReasonForChange = reason;
        draft.Status = WorkflowProfileStatus.Effective;
        draft.ElectronicSignature = signature;

        await _audit.RecordUserEventAsync(
            DraftSignedActionCode, AuditActionCategory.Configuration, ReviewEntityTypes.WorkflowProfile,
            reason: reason, changes: Changes(previousUnits, previousTarget, previousFrom, draft),
            entityId: draft.Id.ToString(), cancellationToken: ct);

        return await ToDtoAsync(draft, ct);
    }

    // ---- Validation ------------------------------------------------------

    private async Task<(WorkflowType Type, string? TestCode, string Reason)> ValidateAsync(
        WorkflowType requestedType, string? testCode, decimal? units, decimal? target, string? reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason for the change is required.");
        var trimmedReason = reason.Trim();
        if (trimmedReason.Length > 500)
            throw new InvalidOperationException("The reason must be 500 characters or fewer.");

        if (units is null && target is null)
            throw new InvalidOperationException("Enter work units, a target, or both.");
        CheckValue("Work units", units, MaxWorkUnits);
        CheckValue("Target analyst days", target, MaxTargetDays);

        var code = string.IsNullOrWhiteSpace(testCode) ? null : testCode.Trim();
        var type = requestedType;
        if (code is not null)
        {
            var definition = await _db.TestDefinitions.AsNoTracking().FirstOrDefaultAsync(t => t.Code == code, ct)
                ?? throw new InvalidOperationException($"Test '{code}' does not exist in the test master.");
            type = definition.WorkflowType; // the definition decides, not the request
        }

        if (!LiveTypes.Contains(type))
            throw new InvalidOperationException($"Workflow type {type} is retired and cannot carry a profile.");

        return (type, code, trimmedReason);
    }

    private static void CheckValue(string label, decimal? value, decimal max)
    {
        if (value is null) return;
        if (value <= 0) throw new InvalidOperationException($"{label} must be greater than 0.");
        if (value > max) throw new InvalidOperationException($"{label} must not exceed {max.ToString(CultureInfo.InvariantCulture)}.");
        if (decimal.Round(value.Value, 2) != value.Value)
            throw new InvalidOperationException($"{label} allows at most 2 decimal places.");
    }

    private async Task EnsureEffectiveFromAsync(WorkflowType type, string? code, DateOnly effectiveFrom, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
        if (effectiveFrom >= today) return;

        // Baseline exception: the first Effective version of a scope may reach back, once.
        // ponytail: no lock between this check and the insert; the UnitOfWork transaction is
        // enough for one Section Head, add a unique partial index if concurrent baselines appear.
        var scopeHasEffective = await _db.WorkflowProfileVersions.AnyAsync(
            v => v.Status == WorkflowProfileStatus.Effective && v.WorkflowType == type && v.TestCode == code, ct);
        if (scopeHasEffective)
            throw new InvalidOperationException(
                $"The effective date cannot be in the past ({effectiveFrom:yyyy-MM-dd}). Only the first version of a profile may be back-dated; " +
                $"add this version with an effective date of {today:yyyy-MM-dd} or later.");
    }

    // ---- Mapping ---------------------------------------------------------

    private static string ScopeLabel(WorkflowType type, string? code) =>
        code is null ? $"{type} type default" : $"Test {code} override";

    private static string SignatureComment(WorkflowType type, string? code, decimal? units, decimal? target, DateOnly from, string reason) =>
        $"{ScopeLabel(type, code)}: units {Fmt(units) ?? "inherit"}, target days {Fmt(target) ?? "inherit"}, effective {from:yyyy-MM-dd}: {reason}";

    private static string? Fmt(decimal? value) => value?.ToString(CultureInfo.InvariantCulture);

    private static List<AuditFieldChange> Changes(decimal? previousUnits, decimal? previousTarget, DateOnly? previousFrom, WorkflowProfileVersion now) => new()
    {
        new("Scope", null, ScopeLabel(now.WorkflowType, now.TestCode)),
        new("WorkUnits", Fmt(previousUnits), Fmt(now.WorkUnits)),
        new("TargetAnalystDays", Fmt(previousTarget), Fmt(now.TargetAnalystDays)),
        new("EffectiveFrom", previousFrom?.ToString("yyyy-MM-dd"), now.EffectiveFrom.ToString("yyyy-MM-dd"))
    };

    private async Task<WorkflowProfileVersionDto> ToDtoAsync(WorkflowProfileVersion row, CancellationToken ct) =>
        (await MapAsync(new[] { row }, ct))[0];

    private async Task<List<WorkflowProfileVersionDto>> MapAsync(IReadOnlyCollection<WorkflowProfileVersion> rows, CancellationToken ct)
    {
        var codes = rows.Where(v => v.TestCode != null).Select(v => v.TestCode!).Distinct().ToList();
        var testNames = await _db.TestDefinitions.AsNoTracking().Where(t => codes.Contains(t.Code))
            .ToDictionaryAsync(t => t.Code, t => t.DisplayName, ct);
        var creatorIds = rows.Select(v => v.CreatedByUserId).Distinct().ToList();
        var creators = await _db.Users.AsNoTracking().Where(u => creatorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var signatures = await LoadSignaturesAsync(rows, ct);

        return rows.Select(v =>
        {
            string? testName = null;
            if (v.TestCode is not null) testNames.TryGetValue(v.TestCode, out testName);
            DateTime? signedAt = null;
            if (v.ElectronicSignatureId is int sid && signatures.TryGetValue(sid, out var s)) signedAt = s.SignedAt;
            return new WorkflowProfileVersionDto(
                v.Id, v.WorkflowType, v.TestCode, testName, v.WorkUnits, v.TargetAnalystDays, v.EffectiveFrom,
                v.Status, v.ReasonForChange, v.ElectronicSignatureId, v.CreatedByUserId,
                CreatorName(v.CreatedByUserId, creators), v.CreatedAt, signedAt, v.Version);
        }).ToList();
    }

    private static string CreatorName(int userId, IReadOnlyDictionary<int, string> users) =>
        userId == 0 ? "System (legacy import)" : users.TryGetValue(userId, out var name) ? name : "Unknown user";

    private async Task<Dictionary<int, (DateTime SignedAt, string SignerName)>> LoadSignaturesAsync(
        IEnumerable<WorkflowProfileVersion> rows, CancellationToken ct)
    {
        var ids = rows.Where(v => v.ElectronicSignatureId.HasValue).Select(v => v.ElectronicSignatureId!.Value).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, (DateTime, string)>();
        return await _db.ElectronicSignatures.AsNoTracking().Where(s => ids.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => (SignedAt: s.SignedAt, SignerName: s.UserFullNameSnapshot), ct);
    }
}
```

In `backend/MicroLIMS.Tests/TestServiceFactory.cs` add `using MicroLIMS.Application.Services.Kpi;` after line 5 (`using MicroLIMS.Application.Services;`) and, after the `MediaProduct` factory method (lines 182-184), add:
```csharp

    public static WorkflowProfileService WorkflowProfile(MicroLimsDbContext db, TimeProvider? time = null) =>
        new(db, new ElectronicSignatureService(db),
            new AuditEventService(db, new MicroLIMS.Persistence.Helpers.DatabaseSequenceHelper(db)), time);
```

- [ ] **Step 4: Run the tests, expect pass**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~WorkflowProfileServiceTests"
```
Expected: all PASS. If `ToDictionaryAsync` with the named-tuple selector warns/fails to convert, drop the tuple names (`(s.SignedAt, s.UserFullNameSnapshot)`); behaviour is identical.

- [ ] **Step 5: Commit**

```
git diff -- backend/MicroLIMS.Tests/TestServiceFactory.cs
git add backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileDtos.cs backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs backend/MicroLIMS.Tests/UnitTests/WorkflowProfileServiceTests.cs
git add -p backend/MicroLIMS.Tests/TestServiceFactory.cs
git commit -m "feat(kpi): sign and audit workflow profile versions

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Service read model (resolver loader, list, overview, coverage)

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs` (insert read methods before `private static void CheckValue`)
- Test: `backend/MicroLIMS.Tests/UnitTests/WorkflowProfileServiceTests.cs` (append tests before the final closing brace)

**Interfaces:**
- Consumes: Task 2 `WorkflowProfileResolver` (`Resolve`, `ResolveDefault`, `HasOverride`), Task 3 `MapAsync`, `LoadSignaturesAsync`, `LiveTypes`, DTOs.
- Produces:
  - `Task<WorkflowProfileResolver> LoadResolverAsync(CancellationToken ct = default)` (used by S2 and S3)
  - `Task<List<WorkflowProfileVersionDto>> ListAsync(WorkflowType? workflowType, string? testCode, bool defaultOnly, WorkflowProfileStatus? status, CancellationToken ct = default)`
  - `Task<WorkflowProfileOverviewDto> GetOverviewAsync(CancellationToken ct = default)`
  - `Task<WorkflowProfileCoverageDto> GetCoverageAsync(CancellationToken ct = default)`

Semantics: "effective" = resolved as of today (UTC). A type's coverage is `Configured` when both fields resolve, `Partial` when one does, else `NeedsConfiguration`; a test row uses the same rule on its own resolved values. `Tests` lists ACTIVE test definitions of that type ordered by test code. `LastEffectiveFrom/LastChangeAt/LastChangeByName` come from the most recently created Effective type-default row (change time = signature time when signed; name = signer when signed, else creator). `DraftCount` counts Drafts of the type including test overrides. Tests of retired types are not shown anywhere (they cannot be configured). `TestsNeedingConfiguration` = every test row whose coverage is not `Configured`.

- [ ] **Step 1: Write the failing tests**

Append inside the `WorkflowProfileServiceTests` class (before its final `}`):
```csharp
    // ---- Reads -----------------------------------------------------------

    private static Task<WorkflowProfileVersion> Effective(MicroLimsDbContext db, WorkflowType type, string? code,
        decimal? units, decimal? target, string from = "2026-01-01", int createdBy = 1) =>
        SeedRowAsync(db, type, code, units, target, DateOnly.Parse(from), WorkflowProfileStatus.Effective, createdBy);

    [Fact]
    public async Task LoadResolver_ResolvesFromEffectiveRowsAndCurrentTestTypes()
    {
        await using var db = NewDb();
        await SeedTestAsync(db, "TAMC", WorkflowType.CountTest);
        await Effective(db, WorkflowType.CountTest, null, 1.5m, 7m);
        await SeedRowAsync(db, WorkflowType.CountTest, "TAMC", 9m, 9m, Today, WorkflowProfileStatus.Draft);

        var resolver = await NewService(db).LoadResolverAsync();
        var p = resolver.Resolve("TAMC", Now.UtcDateTime);

        Assert.Equal(1.5m, p.WorkUnits);
        Assert.Equal(7m, p.TargetAnalystDays);
    }

    [Fact]
    public async Task List_FiltersAndOrdersNewestFirst()
    {
        await using var db = NewDb();
        await SeedTestAsync(db, "TAMC", WorkflowType.CountTest);
        var a = await Effective(db, WorkflowType.CountTest, null, 1m, 7m);
        var b = await Effective(db, WorkflowType.CountTest, "TAMC", 4m, null);
        var c = await Effective(db, WorkflowType.Measurement, null, 2m, null);
        var d = await SeedRowAsync(db, WorkflowType.CountTest, "TAMC", 5m, null, Today, WorkflowProfileStatus.Draft);
        var service = NewService(db);

        var all = await service.ListAsync(null, null, false, null);
        Assert.Equal(new[] { d.Id, c.Id, b.Id, a.Id }, all.Select(v => v.Id));

        Assert.Equal(new[] { b.Id, a.Id }.Concat(new[] { d.Id }).OrderByDescending(i => i),
            (await service.ListAsync(WorkflowType.CountTest, null, false, null)).Select(v => v.Id));
        Assert.Equal(new[] { d.Id, b.Id }, (await service.ListAsync(null, "TAMC", false, null)).Select(v => v.Id));
        Assert.Equal(new[] { c.Id, a.Id }, (await service.ListAsync(null, null, true, null)).Select(v => v.Id));
        Assert.Equal(new[] { d.Id }, (await service.ListAsync(null, null, false, WorkflowProfileStatus.Draft)).Select(v => v.Id));
        Assert.Equal("TAMC name", (await service.ListAsync(null, "TAMC", false, null)).First().TestName);
    }

    [Fact]
    public async Task Overview_ListsEveryLiveTypeInOrder_WithCoverageFromTheTypeDefault()
    {
        await using var db = NewDb();
        await Effective(db, WorkflowType.CountTest, null, 1m, 7m);        // Configured
        await Effective(db, WorkflowType.Measurement, null, 2m, null);    // Partial
        // Gravimetric: nothing -> NeedsConfiguration

        var overview = await NewService(db).GetOverviewAsync();

        Assert.Equal(WorkflowProfileService.LiveTypes, overview.Types.Select(t => t.WorkflowType));
        Assert.Equal(ProfileCoverage.Configured, overview.Types.Single(t => t.WorkflowType == WorkflowType.CountTest).Coverage);
        var measurement = overview.Types.Single(t => t.WorkflowType == WorkflowType.Measurement);
        Assert.Equal(ProfileCoverage.Partial, measurement.Coverage);
        Assert.Equal(2m, measurement.EffectiveWorkUnits);
        Assert.Null(measurement.EffectiveTargetAnalystDays);
        Assert.Equal(ProfileCoverage.NeedsConfiguration, overview.Types.Single(t => t.WorkflowType == WorkflowType.Gravimetric).Coverage);
    }

    [Fact]
    public async Task Overview_FutureDatedVersion_IsNotEffectiveToday()
    {
        await using var db = NewDb();
        await Effective(db, WorkflowType.CountTest, null, 1m, 7m, "2026-01-01");
        await Effective(db, WorkflowType.CountTest, null, 9m, 9m, "2026-10-12");

        var row = (await NewService(db).GetOverviewAsync()).Types.Single(t => t.WorkflowType == WorkflowType.CountTest);

        Assert.Equal(1m, row.EffectiveWorkUnits);
    }

    [Fact]
    public async Task Overview_CountsDrafts_AndDraftsDoNotChangeEffectiveValues()
    {
        await using var db = NewDb();
        await SeedTestAsync(db, "TAMC", WorkflowType.CountTest);
        await Effective(db, WorkflowType.CountTest, null, 1m, 7m);
        await SeedRowAsync(db, WorkflowType.CountTest, "TAMC", 9m, null, Today, WorkflowProfileStatus.Draft);
        await SeedRowAsync(db, WorkflowType.CountTest, null, 8m, null, Today, WorkflowProfileStatus.Draft);

        var row = (await NewService(db).GetOverviewAsync()).Types.Single(t => t.WorkflowType == WorkflowType.CountTest);

        Assert.Equal(2, row.DraftCount);
        Assert.Equal(1m, row.EffectiveWorkUnits);
        Assert.Equal(1m, row.Tests.Single().EffectiveWorkUnits);
        Assert.False(row.Tests.Single().HasOverride);
    }

    [Fact]
    public async Task Overview_TestRowsInheritFromTheTypeDefault_AndFlagOverrides()
    {
        await using var db = NewDb();
        await SeedTestAsync(db, "TAMC", WorkflowType.CountTest);
        await SeedTestAsync(db, "TYMC", WorkflowType.CountTest);
        await SeedTestAsync(db, "OLD", WorkflowType.CountTest, active: false);
        await Effective(db, WorkflowType.CountTest, null, 1m, 7m);
        await Effective(db, WorkflowType.CountTest, "TAMC", 4m, null);

        var tests = (await NewService(db).GetOverviewAsync()).Types.Single(t => t.WorkflowType == WorkflowType.CountTest).Tests;

        Assert.Equal(new[] { "TAMC", "TYMC" }, tests.Select(t => t.TestCode)); // inactive test excluded
        var tamc = tests[0];
        Assert.True(tamc.HasOverride);
        Assert.Equal(4m, tamc.EffectiveWorkUnits);
        Assert.Equal(7m, tamc.EffectiveTargetAnalystDays);
        Assert.Equal(ProfileCoverage.Configured, tamc.Coverage);
        Assert.False(tests[1].HasOverride);
        Assert.Equal(1m, tests[1].EffectiveWorkUnits);
    }

    [Fact]
    public async Task Overview_FollowsCurrentTestWorkflowType()
    {
        await using var db = NewDb();
        await SeedTestAsync(db, "TAMC", WorkflowType.Measurement); // was CountTest when profiles were set
        await Effective(db, WorkflowType.CountTest, null, 1m, 7m);
        await Effective(db, WorkflowType.Measurement, null, 3m, 5m);
        var types = (await NewService(db).GetOverviewAsync()).Types;

        Assert.Empty(types.Single(t => t.WorkflowType == WorkflowType.CountTest).Tests);
        var tamc = types.Single(t => t.WorkflowType == WorkflowType.Measurement).Tests.Single();
        Assert.Equal(3m, tamc.EffectiveWorkUnits);
    }

    [Fact]
    public async Task Overview_LastChange_UsesTheSignerForASignedDraft()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db, "Sam Signer");
        var draft = await SeedRowAsync(db, WorkflowType.CountTest, null, 1m, null, Today, WorkflowProfileStatus.Draft);
        var service = NewService(db);
        await service.SignDraftAsync(draft.Id, Sign(units: 2m, target: 8m), user.Id, null);

        var row = (await service.GetOverviewAsync()).Types.Single(t => t.WorkflowType == WorkflowType.CountTest);

        Assert.Equal("Sam Signer", row.LastChangeByName);
        Assert.Equal(Today, row.LastEffectiveFrom);
        Assert.NotNull(row.LastChangeAt);
        Assert.Equal(0, row.DraftCount);
    }

    [Fact]
    public async Task Overview_TypeWithNoEffectiveVersion_HasNoLastChange()
    {
        await using var db = NewDb();

        var row = (await NewService(db).GetOverviewAsync()).Types.First();

        Assert.Null(row.LastEffectiveFrom);
        Assert.Null(row.LastChangeAt);
        Assert.Null(row.LastChangeByName);
    }

    [Fact]
    public async Task Coverage_ListsTypesWithoutUnitsOrTarget_AndTestsNeedingConfiguration()
    {
        await using var db = NewDb();
        await SeedTestAsync(db, "TAMC", WorkflowType.CountTest);
        await SeedTestAsync(db, "PH", WorkflowType.Measurement);
        await Effective(db, WorkflowType.CountTest, null, 1m, 7m);       // fully configured
        await Effective(db, WorkflowType.Measurement, null, 2m, null);   // units only

        var coverage = await NewService(db).GetCoverageAsync();

        Assert.DoesNotContain(WorkflowType.CountTest, coverage.TypesWithoutUnits);
        Assert.DoesNotContain(WorkflowType.Measurement, coverage.TypesWithoutUnits);
        Assert.Contains(WorkflowType.Gravimetric, coverage.TypesWithoutUnits);
        Assert.Contains(WorkflowType.Measurement, coverage.TypesWithoutTarget);
        Assert.DoesNotContain(WorkflowType.CountTest, coverage.TypesWithoutTarget);
        var needing = Assert.Single(coverage.TestsNeedingConfiguration);
        Assert.Equal("PH", needing.TestCode);
        Assert.Equal(ProfileCoverage.Partial, needing.Coverage);
    }
```

- [ ] **Step 2: Run the tests, expect a compile failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~WorkflowProfileServiceTests"
```
Expected: build error - `'WorkflowProfileService' does not contain a definition for 'LoadResolverAsync'` (and `ListAsync`, `GetOverviewAsync`, `GetCoverageAsync`).

- [ ] **Step 3: Implement the read methods**

In `WorkflowProfileService.cs`, use Edit to replace the line `    private static void CheckValue(string label, decimal? value, decimal max)` (unique) with the block below followed by that same line. The block adds a `// ---- Reads ----` section:
```csharp
    // ---- Reads -----------------------------------------------------------

    // Every Effective version plus the CURRENT type of every test definition. S2 and S3
    // resolve any number of tests from the returned object without further queries.
    public async Task<WorkflowProfileResolver> LoadResolverAsync(CancellationToken ct = default)
    {
        var versions = await _db.WorkflowProfileVersions.AsNoTracking()
            .Where(v => v.Status == WorkflowProfileStatus.Effective).ToListAsync(ct);
        var types = await _db.TestDefinitions.AsNoTracking().ToDictionaryAsync(t => t.Code, t => t.WorkflowType, ct);
        return new WorkflowProfileResolver(versions, types);
    }

    public async Task<List<WorkflowProfileVersionDto>> ListAsync(
        WorkflowType? workflowType, string? testCode, bool defaultOnly, WorkflowProfileStatus? status, CancellationToken ct = default)
    {
        var query = _db.WorkflowProfileVersions.AsNoTracking();
        if (workflowType.HasValue) query = query.Where(v => v.WorkflowType == workflowType.Value);
        if (!string.IsNullOrWhiteSpace(testCode))
        {
            var code = testCode.Trim();
            query = query.Where(v => v.TestCode == code);
        }
        if (defaultOnly) query = query.Where(v => v.TestCode == null);
        if (status.HasValue) query = query.Where(v => v.Status == status.Value);

        var rows = await query.OrderByDescending(v => v.Id).ToListAsync(ct);
        return await MapAsync(rows, ct);
    }

    public async Task<WorkflowProfileOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var all = await _db.WorkflowProfileVersions.AsNoTracking().ToListAsync(ct);
        var effective = all.Where(v => v.Status == WorkflowProfileStatus.Effective).ToList();
        var tests = await _db.TestDefinitions.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Code).ToListAsync(ct);
        var typeByCode = await _db.TestDefinitions.AsNoTracking().ToDictionaryAsync(t => t.Code, t => t.WorkflowType, ct);
        var resolver = new WorkflowProfileResolver(effective, typeByCode);

        // The latest change per type = the newest Effective type-default row.
        var lastByType = effective.Where(v => v.TestCode == null)
            .GroupBy(v => v.WorkflowType)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id).First());
        var signatures = await LoadSignaturesAsync(lastByType.Values, ct);
        var creatorIds = lastByType.Values.Select(v => v.CreatedByUserId).Distinct().ToList();
        var creators = await _db.Users.AsNoTracking().Where(u => creatorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var rows = new List<WorkflowProfileTypeRowDto>();
        foreach (var type in LiveTypes)
        {
            var resolved = resolver.ResolveDefault(type, now);

            DateOnly? lastFrom = null;
            DateTime? lastAt = null;
            string? lastBy = null;
            if (lastByType.TryGetValue(type, out var last))
            {
                lastFrom = last.EffectiveFrom;
                lastAt = last.CreatedAt;
                lastBy = CreatorName(last.CreatedByUserId, creators);
                if (last.ElectronicSignatureId is int sid && signatures.TryGetValue(sid, out var sig))
                {
                    lastAt = sig.SignedAt;      // the signature is the change; the signer is accountable
                    lastBy = sig.SignerName;
                }
            }

            var testRows = tests.Where(t => t.WorkflowType == type).Select(t =>
            {
                var p = resolver.Resolve(t.Code, now);
                return new WorkflowProfileTestRowDto(
                    t.Code, t.DisplayName, p.WorkUnits, p.TargetAnalystDays,
                    resolver.HasOverride(t.Code, now), CoverageOf(p.WorkUnits, p.TargetAnalystDays));
            }).ToList();

            var drafts = all.Count(v => v.Status == WorkflowProfileStatus.Draft && v.WorkflowType == type);
            rows.Add(new WorkflowProfileTypeRowDto(
                type, resolved.WorkUnits, resolved.TargetAnalystDays, lastFrom, lastAt, lastBy,
                CoverageOf(resolved.WorkUnits, resolved.TargetAnalystDays), drafts, testRows));
        }

        return new WorkflowProfileOverviewDto(rows);
    }

    public async Task<WorkflowProfileCoverageDto> GetCoverageAsync(CancellationToken ct = default)
    {
        var overview = await GetOverviewAsync(ct);
        return new WorkflowProfileCoverageDto(
            overview.Types.Where(t => t.EffectiveWorkUnits is null).Select(t => t.WorkflowType).ToList(),
            overview.Types.Where(t => t.EffectiveTargetAnalystDays is null).Select(t => t.WorkflowType).ToList(),
            overview.Types.SelectMany(t => t.Tests).Where(t => t.Coverage != ProfileCoverage.Configured).ToList());
    }

    private static ProfileCoverage CoverageOf(decimal? units, decimal? target) =>
        (units.HasValue, target.HasValue) switch
        {
            (true, true) => ProfileCoverage.Configured,
            (false, false) => ProfileCoverage.NeedsConfiguration,
            _ => ProfileCoverage.Partial
        };

    private static void CheckValue(string label, decimal? value, decimal max)
```

Note for the `List_FiltersAndOrdersNewestFirst` expectation: the second assertion compares the CountTest rows (a, b, d) ordered by descending id; it is written as `new[] { b.Id, a.Id }.Concat(new[] { d.Id }).OrderByDescending(i => i)` so it does not depend on insertion order.

- [ ] **Step 4: Run the tests, expect pass**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~WorkflowProfile"
```
Expected: all `WorkflowProfile*` tests PASS (model, resolver, service).

- [ ] **Step 5: Commit**

```
git add backend/MicroLIMS.Application/Services/Kpi/WorkflowProfileService.cs backend/MicroLIMS.Tests/UnitTests/WorkflowProfileServiceTests.cs
git commit -m "feat(kpi): workflow profile list, overview and coverage reads

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### Task 5: `Kpi.ConfigureProfiles` permission, controller, DI and authorization matrix

Permission plumbing and the controller are one task on purpose: the architecture test `EveryEnforcedCode_IsCheckedSomewhere` fails for any `Enforced` code that no controller attribute references, so the const cannot land without its first use.

**Files:**
- Modify: `backend/MicroLIMS.Shared/Constants/PermissionConstants.cs:84` (const), `:118` (`All`), `:131` (`Enforced`)
- Modify: `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:490` (catalog tuple), `:543` (SectionHead grants)
- Create: `backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs`
- Modify: `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs:97` (after `KpiService`)
- Modify: `frontend/src/routes/routes.ts:241`
- Modify: `backend/MicroLIMS.Tests/UnitTests/RolePermissionSeedDataTests.cs` (array ~:107, counts `:116`, `:198`, `:208`, comment `:195`)
- Modify: `backend/MicroLIMS.Tests/IntegrationTests/PermissionEnforcementMigrationPostgresTests.cs:28-33`
- Modify: `backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt` (regenerate)
- Test: `backend/MicroLIMS.Tests/UnitTests/WorkflowProfileModelTests.cs` (append request-binding tests)

**Interfaces:**
- Consumes: `WorkflowProfileService` (Tasks 3-4), `PermissionConstants.KpiView`, `ApiResponse<object>.Ok`.
- Produces: `PermissionConstants.KpiConfigureProfiles = "Kpi.ConfigureProfiles"`; frontend `PERMISSIONS.KPI_CONFIGURE_PROFILES`; routes
  - `GET /api/kpi/workflow-profiles?workflowType&testCode&defaultOnly&status` -> `WorkflowProfileVersionDto[]` (`Kpi.View`)
  - `GET /api/kpi/workflow-profiles/overview` -> `WorkflowProfileOverviewDto` (`Kpi.View`)
  - `GET /api/kpi/workflow-profiles/coverage` -> `WorkflowProfileCoverageDto` (`Kpi.View`)
  - `POST /api/kpi/workflow-profiles` body `CreateWorkflowProfileVersionRequest` -> `WorkflowProfileVersionDto` (`Kpi.View` + `Kpi.ConfigureProfiles`)
  - `POST /api/kpi/workflow-profiles/{id}/sign` body `SignWorkflowProfileDraftRequest` + `If-Match: "<version>"` -> `WorkflowProfileVersionDto` (`Kpi.View` + `Kpi.ConfigureProfiles`)

- [ ] **Step 1: Confirm the starting numbers**

```
git grep -n "Assert.Equal(41\|Assert.Equal(119" -- backend/MicroLIMS.Tests/UnitTests/RolePermissionSeedDataTests.cs
git grep -n "AddedByLaterMigrations" -- backend/MicroLIMS.Tests/IntegrationTests/PermissionEnforcementMigrationPostgresTests.cs
```
Expected: SectionHead count `41` (line ~116), grant totals `119` twice (~198, ~208). If any number differs, use the numbers you find and add +1 (SectionHead) / +2 (totals).

- [ ] **Step 2: Write the failing tests**

In `RolePermissionSeedDataTests.cs`:
- In the SectionHead `expected` array replace `PermissionConstants.KpiView, PermissionConstants.MediaPrepare, PermissionConstants.MediaRelease` with `PermissionConstants.KpiView, PermissionConstants.KpiConfigureProfiles, PermissionConstants.MediaPrepare, PermissionConstants.MediaRelease`.
- `Assert.Equal(41, codes.Count);` -> `Assert.Equal(42, codes.Count);`
- In `TotalGrantCount_MatchesTheCatalog` extend the comment (`WorkingStandards.Qualify: +3, for 119.` -> `WorkingStandards.Qualify: +3, for 119. Kpi.ConfigureProfiles (KPI remodel S1) is granted to SystemAdministrator and SectionHead only: +2, for 121.`) and change `Assert.Equal(119, total);` to `Assert.Equal(121, total);`.
- In `SeedingIsIdempotent_RunningTwiceDoesNotDuplicateRows` change `Assert.Equal(119, await db.RolePermissions.CountAsync());` to `Assert.Equal(121, ...)`.

In `PermissionEnforcementMigrationPostgresTests.cs` append to `AddedByLaterMigrations`:
```csharp
    private static readonly string[] AddedByLaterMigrations =
    {
        PermissionConstants.SolutionsPrepare,
        PermissionConstants.HplcOperate,
        PermissionConstants.WorkingStandardsQualify,
        PermissionConstants.KpiConfigureProfiles
    };
```

Append to `WorkflowProfileModelTests.cs` (add `using MicroLIMS.Application.Services.Kpi;` and `using MicroLIMS.Domain.Enums;` at the top, then these tests inside the class):
```csharp
    private static JsonSerializerOptions ApiOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJsonOptions.Configure(options);
        return options;
    }

    // The legacy PUT bound "weight" to a property named WorkloadWeight and saved 0 without a word.
    // These pin the exact wire names the frontend sends.
    [Fact]
    public void CreateRequest_BindsEveryWireField()
    {
        const string body = """
            {"workflowType":"CountTest","testCode":null,"workUnits":1.5,"targetAnalystDays":7,
             "effectiveFrom":"2026-10-11","reasonForChange":"SOP time study","password":"pw"}
            """;

        var r = JsonSerializer.Deserialize<CreateWorkflowProfileVersionRequest>(body, ApiOptions())!;

        Assert.Equal(WorkflowType.CountTest, r.WorkflowType);
        Assert.Null(r.TestCode);
        Assert.Equal(1.5m, r.WorkUnits);
        Assert.Equal(7m, r.TargetAnalystDays);
        Assert.Equal(new DateOnly(2026, 10, 11), r.EffectiveFrom);
        Assert.Equal("SOP time study", r.ReasonForChange);
        Assert.Equal("pw", r.Password);
    }

    [Fact]
    public void SignRequest_BindsEveryWireField()
    {
        const string body = """
            {"workUnits":null,"targetAnalystDays":9.5,"effectiveFrom":"2026-10-12","reasonForChange":"r","password":"pw"}
            """;

        var r = JsonSerializer.Deserialize<SignWorkflowProfileDraftRequest>(body, ApiOptions())!;

        Assert.Null(r.WorkUnits);
        Assert.Equal(9.5m, r.TargetAnalystDays);
        Assert.Equal(new DateOnly(2026, 10, 12), r.EffectiveFrom);
    }

    [Fact]
    public void VersionDto_SerializesCamelCaseWithStringEnumsAndIsoDate()
    {
        var dto = new WorkflowProfileVersionDto(1, WorkflowType.HplcMethodAssay, null, null, 2m, null,
            new DateOnly(2026, 10, 11), WorkflowProfileStatus.Draft, "r", null, 0, "System (legacy import)",
            new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc), null, 3);

        var json = JsonSerializer.Serialize(dto, ApiOptions());

        Assert.Contains("\"workflowType\":\"HplcMethodAssay\"", json);
        Assert.Contains("\"status\":\"Draft\"", json);
        Assert.Contains("\"effectiveFrom\":\"2026-10-11\"", json);
        Assert.Contains("\"workUnits\":2", json);
    }
```

- [ ] **Step 3: Run the tests, expect a compile failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~WorkflowProfileModelTests|FullyQualifiedName~RolePermissionSeedDataTests"
```
Expected: build error `'PermissionConstants' does not contain a definition for 'KpiConfigureProfiles'`.

- [ ] **Step 4: Implement the permission layers**

`PermissionConstants.cs`:
- After `public const string KpiView = "Kpi.View";` add
```csharp
    // KPI remodel S1 - create and sign workflow profile versions (work units, analyst-stage targets).
    public const string KpiConfigureProfiles = "Kpi.ConfigureProfiles";
```
- In `All`, replace `DashboardsLabOverview, DashboardsReview, KpiView,` with `DashboardsLabOverview, DashboardsReview, KpiView, KpiConfigureProfiles,`.
- In `Enforced`, replace `EquipmentDocumentControl, EquipmentManage, ItemsDocumentUpload, ItemsManage, KpiView,` with `EquipmentDocumentControl, EquipmentManage, ItemsDocumentUpload, ItemsManage, KpiView, KpiConfigureProfiles,`.

`DbSeeder.cs`:
- After the `PermissionConstants.KpiView` catalog tuple (line 490) add
```csharp
            (PermissionConstants.KpiConfigureProfiles, "Create and sign workflow profile versions (work units and analyst-stage targets) used by the KPI page."),
```
- In the SectionHead list replace `PermissionConstants.KpiView, PermissionConstants.MediaPrepare, PermissionConstants.MediaRelease` with `PermissionConstants.KpiView, PermissionConstants.KpiConfigureProfiles, PermissionConstants.MediaPrepare, PermissionConstants.MediaRelease` (SystemAdministrator receives `All` automatically).

Create `backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs`:
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Workflow profiles (KPI remodel S1). Reads need Kpi.View; every write also needs
// Kpi.ConfigureProfiles - the two policies intersect, so a writer needs both.
// A Draft is signed with If-Match: "<version>" (RecordVersionMiddleware).
[ApiController]
[Route("api/kpi/workflow-profiles")]
[Authorize(Policy = PermissionConstants.KpiView)]
public class WorkflowProfilesController : ControllerBase
{
    private readonly WorkflowProfileService _service;

    public WorkflowProfilesController(WorkflowProfileService service) => _service = service;

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] WorkflowType? workflowType,
        [FromQuery] string? testCode,
        [FromQuery] bool defaultOnly = false,
        [FromQuery] WorkflowProfileStatus? status = null) =>
        Ok(ApiResponse<object>.Ok(await _service.ListAsync(workflowType, testCode, defaultOnly, status)));

    [HttpGet("overview")]
    public async Task<IActionResult> Overview() =>
        Ok(ApiResponse<object>.Ok(await _service.GetOverviewAsync()));

    [HttpGet("coverage")]
    public async Task<IActionResult> Coverage() =>
        Ok(ApiResponse<object>.Ok(await _service.GetCoverageAsync()));

    [Authorize(Policy = PermissionConstants.KpiConfigureProfiles)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowProfileVersionRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.AddVersionAsync(request, CurrentUserId, ClientIp)));

    [Authorize(Policy = PermissionConstants.KpiConfigureProfiles)]
    [HttpPost("{id:int}/sign")]
    public async Task<IActionResult> Sign(int id, [FromBody] SignWorkflowProfileDraftRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.SignDraftAsync(id, request, CurrentUserId, ClientIp)));
}
```

`ServiceCollectionExtensions.cs`: add `using MicroLIMS.Application.Services.Kpi;` with the other usings and, directly after `services.AddScoped<KpiService>();` (line 97):
```csharp
        services.AddScoped<WorkflowProfileService>();
```
(Do NOT register `WorkflowProfileResolver`; callers obtain it from `LoadResolverAsync`.)

`frontend/src/routes/routes.ts`, after `KPI_VIEW: "Kpi.View",`:
```ts
  KPI_CONFIGURE_PROFILES: "Kpi.ConfigureProfiles",
```

- [ ] **Step 5: Regenerate and review the authorization matrix**

```
MICROLIMS_WRITE_AUTH_MATRIX=1 dotnet test backend/MicroLIMS.Tests --filter AuthorizationMatrixTests --artifacts-path "$TEMP/kpi-art"
git diff -- backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt
```
Expected diff: exactly five ADDED lines and nothing else (verify the parameter lists match what the compiler produced):
```
WorkflowProfilesController.Coverage(): SystemAdministrator,SectionHead
WorkflowProfilesController.Create(CreateWorkflowProfileVersionRequest): SystemAdministrator,SectionHead
WorkflowProfilesController.List(Nullable`1, String, Boolean, Nullable`1): SystemAdministrator,SectionHead
WorkflowProfilesController.Overview(): SystemAdministrator,SectionHead
WorkflowProfilesController.Sign(Int32, SignWorkflowProfileDraftRequest): SystemAdministrator,SectionHead
```
If the diff shows anything else (for example other controllers' lines), another feature changed the matrix: keep only the five lines above.

- [ ] **Step 6: Run the tests, expect pass**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~WorkflowProfile|FullyQualifiedName~RolePermissionSeedDataTests|FullyQualifiedName~Permission|FullyQualifiedName~AuthorizationMatrix"
```
Expected: all PASS (including `EveryEnforcedCode_IsCheckedSomewhere`, `EveryPolicyInUse_IsMarkedEnforced`, `EveryCatalogPermissionIsSeeded`). Postgres-only tests are skipped without `MICROLIMS_TEST_POSTGRES`; Task 6 runs them.

- [ ] **Step 7: Commit**

```
git diff -- backend/MicroLIMS.Shared/Constants/PermissionConstants.cs backend/MicroLIMS.Persistence/Seed/DbSeeder.cs backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs frontend/src/routes/routes.ts
git add backend/MicroLIMS.API/Controllers/WorkflowProfilesController.cs backend/MicroLIMS.Tests/UnitTests/RolePermissionSeedDataTests.cs backend/MicroLIMS.Tests/IntegrationTests/PermissionEnforcementMigrationPostgresTests.cs backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt backend/MicroLIMS.Tests/UnitTests/WorkflowProfileModelTests.cs
git add -p backend/MicroLIMS.Shared/Constants/PermissionConstants.cs backend/MicroLIMS.Persistence/Seed/DbSeeder.cs backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs frontend/src/routes/routes.ts
git commit -m "feat(kpi): Kpi.ConfigureProfiles permission and workflow profiles API

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
(If a file carries only your hunk, plain `git add <file>` replaces `git add -p`. `RolePermissionSeedDataTests.cs` and the matrix may also carry foreign hunks: check `git diff` first and use `git add -p` for them too.)

---

### Task 6: Migration `AddWorkflowProfileVersions` (table, immutability trigger, legacy import, permission SQL)

**Files:**
- Create (generated, then hand-edited): `backend/MicroLIMS.Persistence/Migrations/<timestamp>_AddWorkflowProfileVersions.cs` and `.Designer.cs`
- Modify (generated): `backend/MicroLIMS.Persistence/Migrations/MicroLimsDbContextModelSnapshot.cs`
- Test: `backend/MicroLIMS.Tests/IntegrationTests/WorkflowProfileMigrationPostgresTests.cs`

**Interfaces:**
- Consumes: Task 1 model, Task 5 permission code, Task 3 service (for the rollback tests).
- Produces: table `WorkflowProfileVersions`; trigger `trg_workflowprofileversions_immutable` (function `prevent_workflowprofileversion_modification`); permission row `Kpi.ConfigureProfiles` with grants for role types 0 and 1; Draft rows imported from `WorkloadWeights` (`CreatedByUserId = 0`, reason `Imported from legacy workload weight`, one `AuditLogs` row each with `ActionCode = 'WorkflowProfileDraftImported'`).

Two deliberate details: (1) the import skips legacy rows whose test definition has a retired workflow type (`WorkflowType` ints 2, 3, 10, 11), because the service rejects retired types and such a Draft could never be signed; (2) the import has no effect on any calculation, because Drafts are ignored by the resolver.

- [ ] **Step 1: Preflight - the snapshot hazard**

```
git status --short backend/MicroLIMS.Persistence/Migrations | head -20
ls backend/MicroLIMS.Persistence/Migrations | sort | tail -6
```
Expected: the newest migration is `20261011103646_AddMediaStorageEntriesAndCryovialStorageEquipment` (untracked) and `MicroLimsDbContextModelSnapshot.cs` is modified. Prefer to wait until the Media/Cryovial work is committed. If it is not, continue, but at commit time stage only the WorkflowProfile hunks of the snapshot with `git add -p`. The new migration must sort AFTER the newest timestamp on disk (EF stamps the current time, so it will, but verify the file name).

- [ ] **Step 2: Write the failing Postgres tests**

Read `backend/MicroLIMS.Tests/Fixtures/PostgresTestConfiguration.cs` (top 40 lines) to learn what `MICROLIMS_TEST_POSTGRES` must contain, and export it for the shell you run tests in. Without it every `[PostgresFact]` is SKIPPED and proves nothing.

Create `backend/MicroLIMS.Tests/IntegrationTests/WorkflowProfileMigrationPostgresTests.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Migrations;
using MicroLIMS.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Own throw-away database: migrate to the migration BEFORE AddWorkflowProfileVersions, seed
// legacy rows, migrate on, then down. The ids are looked up, so the test survives EF's timestamp.
public class WorkflowProfileMigrationPostgresTests
{
    [PostgresFact]
    public async Task Migration_ImportsLegacyWeightsAsDrafts_InstallsTheTrigger_AndRevertsOnDown()
    {
        var admin = PostgresTestConfiguration.AdminConnectionString!;
        var name = PostgresTestConfiguration.NewTestDatabaseName();
        var cs = PostgresTestConfiguration.BuildTestConnectionString(admin, name);
        await Exec(admin, $"CREATE DATABASE \"{name}\"");
        try
        {
            await using var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>().UseNpgsql(cs).Options);
            var ids = db.GetService<IMigrationsAssembly>().Migrations.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            var target = ids.Single(k => k.EndsWith("_AddWorkflowProfileVersions", StringComparison.Ordinal));
            var before = ids[ids.IndexOf(target) - 1];
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(before);

            await using var c = new NpgsqlConnection(cs);
            await c.OpenAsync();
            // Non-superuser CI role: drop FKs while seeding, re-add NOT VALID afterwards.
            var fks = new List<(string Table, string Name, string Def)>();
            await using (var q = new NpgsqlCommand(
                "SELECT conrelid::regclass::text, conname, pg_get_constraintdef(oid) FROM pg_constraint " +
                "WHERE contype = 'f' AND connamespace = 'public'::regnamespace", c))
            await using (var r = await q.ExecuteReaderAsync())
                while (await r.ReadAsync()) fks.Add((r.GetString(0), r.GetString(1), r.GetString(2)));
            foreach (var fk in fks) await Exec(c, $"ALTER TABLE {fk.Table} DROP CONSTRAINT \"{fk.Name}\"");

            await Insert(c, "Roles", ("Name", "WP Admin"), ("Type", 0));
            await Insert(c, "Roles", ("Name", "WP Section Head"), ("Type", 1));
            await Insert(c, "Roles", ("Name", "WP Analyst"), ("Type", 3));

            // WorkflowType ints: 0 CountTest, 2 HplcAssay (retired), 4 Measurement.
            await Insert(c, "TestDefinitions", ("Code", "TAMC"), ("DisplayName", "TAMC"), ("WorkflowType", 0));
            await Insert(c, "TestDefinitions", ("Code", "HPLCOLD"), ("DisplayName", "Old HPLC"), ("WorkflowType", 2));
            await Insert(c, "TestDefinitions", ("Code", "PH"), ("DisplayName", "pH"), ("WorkflowType", 4));
            await Insert(c, "WorkloadWeights", ("TestCode", "TAMC"), ("Weight", 1.5m));
            await Insert(c, "WorkloadWeights", ("TestCode", "HPLCOLD"), ("Weight", 2.0m));
            await Insert(c, "WorkloadWeights", ("TestCode", "PH"), ("Weight", 0.5m));
            await Insert(c, "WorkloadWeights", ("TestCode", "INVESTIGATION"), ("Weight", 1.0m)); // no test definition

            foreach (var fk in fks) await Exec(c, $"ALTER TABLE {fk.Table} ADD CONSTRAINT \"{fk.Name}\" {fk.Def} NOT VALID");

            await migrator.MigrateAsync(target);

            // Legacy import: two Drafts, with no effect until signed.
            Assert.Equal(2, await Scalar(c, "SELECT count(*) FROM \"WorkflowProfileVersions\" WHERE \"Status\" = 0 AND \"CreatedByUserId\" = 0 AND \"ElectronicSignatureId\" IS NULL"));
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM \"WorkflowProfileVersions\" WHERE \"TestCode\" = 'TAMC' AND \"WorkflowType\" = 0 AND \"WorkUnits\" = 1.5 AND \"TargetAnalystDays\" IS NULL AND \"EffectiveFrom\" = CURRENT_DATE AND \"ReasonForChange\" = 'Imported from legacy workload weight'"));
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM \"WorkflowProfileVersions\" WHERE \"TestCode\" = 'PH' AND \"WorkflowType\" = 4 AND \"WorkUnits\" = 0.5"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"WorkflowProfileVersions\" WHERE \"TestCode\" IN ('HPLCOLD', 'INVESTIGATION')"));
            Assert.Equal(2, await Scalar(c, $"SELECT count(*) FROM \"AuditLogs\" WHERE \"ActorType\" = 2 AND \"ActionCode\" = 'WorkflowProfileDraftImported' AND \"SystemProcessName\" = 'Migration {target}'"));

            // Permission: row enforced, granted to role types 0 and 1 only.
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM \"Permissions\" WHERE \"Code\" = 'Kpi.ConfigureProfiles' AND \"IsEnforced\""));
            Assert.Equal(2, await Scalar(c, "SELECT count(*) FROM \"RolePermissions\" rp JOIN \"Permissions\" p ON p.\"Id\" = rp.\"PermissionId\" WHERE p.\"Code\" = 'Kpi.ConfigureProfiles'"));

            // The permission SQL is idempotent: replaying it changes nothing.
            foreach (var op in new AddWorkflowProfileVersions().UpOperations.OfType<SqlOperation>().Where(o => o.Sql.Contains("Kpi.ConfigureProfiles")))
                await Exec(c, op.Sql);
            Assert.Equal(1, await Scalar(c, "SELECT count(*) FROM \"Permissions\" WHERE \"Code\" = 'Kpi.ConfigureProfiles'"));
            Assert.Equal(2, await Scalar(c, "SELECT count(*) FROM \"RolePermissions\" rp JOIN \"Permissions\" p ON p.\"Id\" = rp.\"PermissionId\" WHERE p.\"Code\" = 'Kpi.ConfigureProfiles'"));

            // Trigger: a Draft may be signed in place once, an Effective row never changes, nothing is deleted.
            await Exec(c, "UPDATE \"WorkflowProfileVersions\" SET \"Status\" = 1 WHERE \"TestCode\" = 'TAMC'");
            var updateEx = await Assert.ThrowsAsync<PostgresException>(
                () => Exec(c, "UPDATE \"WorkflowProfileVersions\" SET \"WorkUnits\" = 9 WHERE \"TestCode\" = 'TAMC'"));
            Assert.Contains("append-only", updateEx.MessageText);
            var deleteEffective = await Assert.ThrowsAsync<PostgresException>(
                () => Exec(c, "DELETE FROM \"WorkflowProfileVersions\" WHERE \"TestCode\" = 'TAMC'"));
            Assert.Contains("append-only", deleteEffective.MessageText);
            var deleteDraft = await Assert.ThrowsAsync<PostgresException>(
                () => Exec(c, "DELETE FROM \"WorkflowProfileVersions\" WHERE \"TestCode\" = 'PH'"));
            Assert.Contains("append-only", deleteDraft.MessageText);

            // The check constraints back up the service validation.
            await Assert.ThrowsAsync<PostgresException>(() => Exec(c,
                "INSERT INTO \"WorkflowProfileVersions\" (\"WorkflowType\", \"EffectiveFrom\", \"Status\", \"ReasonForChange\", \"CreatedByUserId\", \"CreatedAt\") " +
                "VALUES (0, CURRENT_DATE, 0, 'no values', 1, now())"));

            await migrator.MigrateAsync(before);

            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM information_schema.tables WHERE table_name = 'WorkflowProfileVersions'"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM pg_proc WHERE proname = 'prevent_workflowprofileversion_modification'"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"Permissions\" WHERE \"Code\" = 'Kpi.ConfigureProfiles'"));
            Assert.Equal(0, await Scalar(c, "SELECT count(*) FROM \"RolePermissions\" rp JOIN \"Permissions\" p ON p.\"Id\" = rp.\"PermissionId\" WHERE p.\"Code\" = 'Kpi.ConfigureProfiles'"));
            Assert.Equal(4, await Scalar(c, "SELECT count(*) FROM \"WorkloadWeights\"")); // legacy tables untouched
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            PostgresTestConfiguration.EnsureSafeToDrop(name, admin);
            await Exec(admin, $"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
        }
    }

    // Inserts one row; every NOT NULL column without a default gets a type-based filler unless overridden.
    private static async Task<int> Insert(NpgsqlConnection c, string table, params (string Col, object Val)[] over)
    {
        var values = over.ToDictionary(o => o.Col, o => o.Val);
        await using (var q = new NpgsqlCommand(
            "SELECT column_name, data_type FROM information_schema.columns WHERE table_name = @t AND is_nullable = 'NO' " +
            "AND column_default IS NULL AND is_identity = 'NO'", c))
        {
            q.Parameters.AddWithValue("t", table);
            await using var r = await q.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var col = r.GetString(0);
                if (values.ContainsKey(col)) continue;
                values[col] = r.GetString(1) switch
                {
                    "integer" or "bigint" or "smallint" => 1,
                    "numeric" => 1m,
                    "boolean" => false,
                    "timestamp with time zone" => DateTime.UtcNow,
                    "timestamp without time zone" => DateTime.UtcNow,
                    "date" => DateOnly.FromDateTime(DateTime.UtcNow),
                    "jsonb" or "json" => "{}",
                    _ => "x"
                };
            }
        }
        var cols = string.Join(",", values.Keys.Select(k => $"\"{k}\""));
        var ps = string.Join(",", values.Keys.Select((_, i) => $"@p{i}"));
        await using var cmd = new NpgsqlCommand($"INSERT INTO \"{table}\" ({cols}) VALUES ({ps}) RETURNING \"Id\"", c);
        var n = 0;
        foreach (var v in values.Values) cmd.Parameters.AddWithValue($"p{n++}", v);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    private static async Task<long> Scalar(NpgsqlConnection c, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private static async Task Exec(string cs, string sql)
    {
        await using var c = new NpgsqlConnection(cs);
        await c.OpenAsync();
        await Exec(c, sql);
    }

    private static async Task Exec(NpgsqlConnection c, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }
}

// Real transactions and real xmin: the half of the sign-and-mutate rules InMemory cannot show.
[Collection("PostgresDatabaseCollection")]
public class WorkflowProfileServicePostgresTests
{
    private const string Password = "Correct-Password-1!";
    private readonly PostgresTestFixture _fixture;

    public WorkflowProfileServicePostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static async Task<User> SeedUserAsync(MicroLimsDbContext db)
    {
        var tag = Guid.NewGuid().ToString("N");
        var role = new Role { Type = RoleType.SectionHead, Name = "WP SH " + tag };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User { FullName = "WP Signer", Username = "wp" + tag, RoleId = role.Id, PasswordHash = TestPasswords.Hash(Password) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static DateOnly Tomorrow => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

    [PostgresFact]
    public async Task AddVersion_WrongPassword_LeavesNoRowSignatureOrAuditEvent()
    {
        await using var db = _fixture.CreateDbContext();
        var user = await SeedUserAsync(db);
        var reason = "marker-" + Guid.NewGuid();
        var failedBefore = await db.AuditLogs.CountAsync(a => a.Action == "SignatureFailed");

        await Assert.ThrowsAsync<SignatureVerificationException>(() => TestServiceFactory.WorkflowProfile(db).AddVersionAsync(
            new CreateWorkflowProfileVersionRequest(WorkflowType.WeightVariation, null, 1m, 5m, Tomorrow, reason, "WrongPassword!"),
            user.Id, "127.0.0.1"));

        await using var verify = _fixture.CreateDbContext();
        Assert.False(await verify.WorkflowProfileVersions.AnyAsync(v => v.ReasonForChange == reason));
        Assert.False(await verify.ElectronicSignatures.AnyAsync(s => s.UserId == user.Id && s.EntityType == ReviewEntityTypes.WorkflowProfile));
        Assert.False(await verify.AuditLogs.AnyAsync(a => a.ActionCode == WorkflowProfileService.VersionAddedActionCode && a.Reason == reason));
        // The refused attempt itself must stay on record (UnitOfWork re-writes it after the rollback).
        Assert.True(await verify.AuditLogs.CountAsync(a => a.Action == "SignatureFailed") > failedBefore);
    }

    [PostgresFact]
    public async Task AddVersion_CorrectPassword_PersistsEffectiveRowWithSignature_UnderTheTrigger()
    {
        await using var db = _fixture.CreateDbContext();
        var user = await SeedUserAsync(db);
        var reason = "marker-" + Guid.NewGuid();
        var service = TestServiceFactory.WorkflowProfile(db);

        var dto = await service.AddVersionAsync(
            new CreateWorkflowProfileVersionRequest(WorkflowType.WeightVariation, null, 1m, 5m, Tomorrow, reason, Password),
            user.Id, "127.0.0.1");

        await using var verify = _fixture.CreateDbContext();
        var row = await verify.WorkflowProfileVersions.SingleAsync(v => v.Id == dto.Id);
        Assert.Equal(WorkflowProfileStatus.Effective, row.Status);
        Assert.NotNull(row.ElectronicSignatureId);
        Assert.True(await verify.AuditLogs.AnyAsync(a => a.ActionCode == WorkflowProfileService.VersionAddedActionCode && a.Reason == reason));

        // A second version for the same scope must also succeed (the first one is now immutable).
        var second = await service.AddVersionAsync(
            new CreateWorkflowProfileVersionRequest(WorkflowType.WeightVariation, null, 2m, null, Tomorrow.AddDays(1), reason + "-2", Password),
            user.Id, "127.0.0.1");
        Assert.Equal(WorkflowProfileStatus.Effective, second.Status);
    }

    [PostgresFact]
    public async Task SignDraft_SecondSign_Rejected_AndStaleVersion_Conflicts()
    {
        int draftId;
        uint staleVersion;
        User user;
        await using (var seed = _fixture.CreateDbContext())
        {
            user = await SeedUserAsync(seed);
            var draft = new WorkflowProfileVersion
            {
                WorkflowType = WorkflowType.Disintegration, WorkUnits = 1m, EffectiveFrom = Tomorrow,
                Status = WorkflowProfileStatus.Draft, ReasonForChange = "Imported from legacy workload weight", CreatedByUserId = 0
            };
            seed.WorkflowProfileVersions.Add(draft);
            await seed.SaveChangesAsync();
            draftId = draft.Id;
            staleVersion = draft.Version;
        }

        // A colleague saves the draft first, so the form's version is stale.
        await using (var colleague = _fixture.CreateDbContext())
        {
            var row = await colleague.WorkflowProfileVersions.SingleAsync(v => v.Id == draftId);
            row.ReasonForChange = "edited by a colleague";
            await colleague.SaveChangesAsync();
        }

        var request = new SignWorkflowProfileDraftRequest(2m, 9m, Tomorrow, "Reviewed", Password);

        await using (var stale = _fixture.CreateDbContext())
        {
            stale.ExpectedVersion = staleVersion;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                () => TestServiceFactory.WorkflowProfile(stale).SignDraftAsync(draftId, request, user.Id, null));
        }

        uint currentVersion;
        await using (var probe = _fixture.CreateDbContext())
            currentVersion = (await probe.WorkflowProfileVersions.AsNoTracking().SingleAsync(v => v.Id == draftId)).Version;

        await using (var current = _fixture.CreateDbContext())
        {
            current.ExpectedVersion = currentVersion;
            var signed = await TestServiceFactory.WorkflowProfile(current).SignDraftAsync(draftId, request, user.Id, null);
            Assert.Equal(WorkflowProfileStatus.Effective, signed.Status);
        }

        await using (var again = _fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => TestServiceFactory.WorkflowProfile(again).SignDraftAsync(draftId, request, user.Id, null));
            Assert.Contains("already Effective", ex.Message);
        }

        await using var verify = _fixture.CreateDbContext();
        Assert.Equal(1, await verify.ElectronicSignatures.CountAsync(
            s => s.EntityType == ReviewEntityTypes.WorkflowProfile && s.EntityId == draftId));
    }
}
```

- [ ] **Step 3: Run the tests, expect failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~WorkflowProfileMigrationPostgresTests|FullyQualifiedName~WorkflowProfileServicePostgresTests"
```
Expected (with `MICROLIMS_TEST_POSTGRES` set): the build fails with `The type or namespace name 'AddWorkflowProfileVersions' could not be found` (the migration class does not exist yet).

- [ ] **Step 4: Generate the migration**

```
dotnet ef migrations add AddWorkflowProfileVersions --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release
```
Review the generated `Up()` against this checklist and STOP if any line fails it:
1. It contains only `migrationBuilder.CreateTable(name: "WorkflowProfileVersions", ...)`, `CreateIndex` for `IX_WorkflowProfileVersions_WorkflowType_TestCode_EffectiveFrom` and for the `ElectronicSignatureId` FK, and the three `CK_WorkflowProfileVersions_*` check constraints.
2. Columns: `WorkUnits` `numeric(8,2)`, `TargetAnalystDays` `numeric(6,2)`, `EffectiveFrom` `date`, `TestCode` `character varying(50)`, `ReasonForChange` `character varying(500)`, `Id` identity, `xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)`, FK to `ElectronicSignatures` with `ReferentialAction.Restrict`.
3. NO Media/Cryovial operations, no `AlterColumn`, no `DropColumn`, nothing on a foreign table. If you see any, the snapshot carried the other feature's pending model changes: stop and tell the human instead of editing around it.
4. The file name's timestamp sorts after `20261011103646`.

- [ ] **Step 5: Hand-edit the migration**

Add `using System.Linq;` is not needed. At the END of `Up(MigrationBuilder migrationBuilder)` (after the generated `CreateIndex` calls) append:
```csharp
            var migrationId = GetType().GetCustomAttributes(typeof(MigrationAttribute), false)
                .Cast<MigrationAttribute>().Single().Id;

            // Legacy import (spec 2.4): every WorkloadWeights row with a live test definition becomes a
            // Draft override. Old weights were relative (1.0 = a count test), not hours, so they have
            // no effect until a Section Head reviews and signs each one. Rows whose definition has a
            // retired workflow type (2, 3, 10, 11) are skipped: the service rejects those types, so
            // the Draft could never be signed. The legacy INVESTIGATION row has no definition.
            // WorkflowType ints: 0 CountTest, 1 Observation, 4 Measurement, 5 Gravimetric, 6 Qualitative,
            // 7 Dissolution, 8 Disintegration, 9 WeightVariation, 12 HplcMethodAssay, 13 Titration,
            // 14 IcpMethodAssay. Status 0 = Draft. ActorType 2 = system, ActionCategory 6 = Configuration.
            migrationBuilder.Sql($$"""
                WITH imported AS (
                    INSERT INTO "WorkflowProfileVersions"
                        ("WorkflowType", "TestCode", "WorkUnits", "TargetAnalystDays", "EffectiveFrom", "Status",
                         "ReasonForChange", "ElectronicSignatureId", "CreatedByUserId", "CreatedAt")
                    SELECT td."WorkflowType", w."TestCode", w."Weight", NULL, CURRENT_DATE, 0,
                           'Imported from legacy workload weight', NULL, 0, now()
                    FROM "WorkloadWeights" w
                    JOIN "TestDefinitions" td ON td."Code" = w."TestCode"
                    WHERE td."WorkflowType" IN (0, 1, 4, 5, 6, 7, 8, 9, 12, 13, 14)
                      AND w."Weight" > 0
                      AND NOT EXISTS (SELECT 1 FROM "WorkflowProfileVersions" v
                                      WHERE v."TestCode" = w."TestCode"
                                        AND v."ReasonForChange" = 'Imported from legacy workload weight')
                    RETURNING "Id", "TestCode", "WorkUnits"
                )
                INSERT INTO "AuditLogs" (
                    "EntityName", "EntityId", "Action", "PreviousValue", "NewValue", "UserId", "Timestamp",
                    "ActorType", "SystemProcessName", "ActionCode", "ActionCategory", "Reason")
                SELECT 'WorkflowProfileVersion', i."Id"::text, 'Create', '{}',
                       jsonb_build_object('TestCode', i."TestCode", 'WorkUnits', i."WorkUnits", 'Status', 'Draft')::text,
                       0, now(), 2, 'Migration {{migrationId}}', 'WorkflowProfileDraftImported', 6,
                       'Legacy workload weight imported as a Draft workflow profile (KPI remodel S1)'
                FROM imported i;
                """);

            // Append-only guard (same idea as trg_electronicsignatures_immutable): nothing is ever
            // deleted, and only a Draft may be updated - which is how signing turns it Effective.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION prevent_workflowprofileversion_modification() RETURNS trigger AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'WorkflowProfileVersions is append-only: DELETE operations are prohibited';
                    END IF;
                    IF OLD."Status" = 1 THEN
                        RAISE EXCEPTION 'WorkflowProfileVersions is append-only: an Effective version cannot be modified';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_workflowprofileversions_immutable
                BEFORE UPDATE OR DELETE ON "WorkflowProfileVersions"
                FOR EACH ROW EXECUTE FUNCTION prevent_workflowprofileversion_modification();
                """);

            // DbSeeder seeds Kpi.ConfigureProfiles on a fresh database; these idempotent statements
            // cover existing databases. RoleType: 0 = SystemAdministrator, 1 = SectionHead.
            migrationBuilder.Sql("""
                INSERT INTO "Permissions" ("Code", "Description", "IsEnforced")
                SELECT 'Kpi.ConfigureProfiles', 'Create and sign workflow profile versions (work units and analyst-stage targets) used by the KPI page.', TRUE
                WHERE NOT EXISTS (SELECT 1 FROM "Permissions" WHERE "Code" = 'Kpi.ConfigureProfiles');
                """);
            migrationBuilder.Sql("""
                UPDATE "Permissions" SET "IsEnforced" = TRUE WHERE "Code" = 'Kpi.ConfigureProfiles';
                """);
            migrationBuilder.Sql("""
                INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
                SELECT r."Id", p."Id"
                FROM "Roles" r CROSS JOIN "Permissions" p
                WHERE r."Type" IN (0, 1)
                  AND p."Code" = 'Kpi.ConfigureProfiles'
                  AND NOT EXISTS (SELECT 1 FROM "RolePermissions" rp
                                  WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id");
                """);
```
At the START of `Down(MigrationBuilder migrationBuilder)` (before the generated `DropTable`) insert:
```csharp
            migrationBuilder.Sql("""
                DELETE FROM "RolePermissions" WHERE "PermissionId" IN
                    (SELECT "Id" FROM "Permissions" WHERE "Code" = 'Kpi.ConfigureProfiles');
                """);
            migrationBuilder.Sql("""
                DELETE FROM "Permissions" WHERE "Code" = 'Kpi.ConfigureProfiles';
                """);
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_workflowprofileversions_immutable ON "WorkflowProfileVersions";
                """);
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS prevent_workflowprofileversion_modification();
                """);
```
The legacy tables are not touched in either direction. The imported Draft rows and their `AuditLogs` rows are removed with the table (the audit rows stay: `AuditLogs` is append-only history, exactly like the data-only migrations in this repo).

- [ ] **Step 6: Run the Postgres tests, expect pass**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~WorkflowProfile|FullyQualifiedName~PermissionEnforcementMigrationPostgresTests|FullyQualifiedName~FreshDatabaseSeed|FullyQualifiedName~UserDeletion"
```
Expected: PASS, and NONE of the `[PostgresFact]` tests reported as skipped (if they are skipped the env var is not set in this shell).

- [ ] **Step 7: Commit**

```
git status --short backend/MicroLIMS.Persistence/Migrations
git diff -- backend/MicroLIMS.Persistence/Migrations/MicroLimsDbContextModelSnapshot.cs
git add backend/MicroLIMS.Persistence/Migrations/<timestamp>_AddWorkflowProfileVersions.cs backend/MicroLIMS.Persistence/Migrations/<timestamp>_AddWorkflowProfileVersions.Designer.cs backend/MicroLIMS.Tests/IntegrationTests/WorkflowProfileMigrationPostgresTests.cs
git add -p backend/MicroLIMS.Persistence/Migrations/MicroLimsDbContextModelSnapshot.cs
git commit -m "feat(kpi): WorkflowProfileVersions migration with append-only trigger and legacy import

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
(`<timestamp>` is the real file name EF generated; copy it from `ls`. In the snapshot answer `y` only to hunks that mention `WorkflowProfileVersion`. After staging, `git diff --cached -- backend/MicroLIMS.Persistence/Migrations/MicroLimsDbContextModelSnapshot.cs` must contain no Media or Cryovial text.)

---

### Task 7: Close the legacy workload-weight write path

The old `PUT /api/kpi/workload-weights/{testCode}` sits under `Kpi.View` only, records no e-signature, and (through `ClaimTypes.Name`) always stamps the literal "Section Head". Profiles replace it. `GET /api/kpi/workload-weights` and `GetWorkloadWeightsAsync` stay for the old page until S4.

**Files:**
- Modify: `backend/MicroLIMS.API/Controllers/KpiController.cs:40-50` (remove the PUT action)
- Modify: `backend/MicroLIMS.Application/Services/KpiService.cs:33-35` (record) and `:146-209` (method)
- Modify: `backend/MicroLIMS.Tests/WorkflowTests/WorkloadWeightsTests.cs:20-64` (first test only)
- Modify: `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:490` (description wording)
- Modify: `backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt:275`

> **Run order: execute this task AFTER Task 10** (run order 1-6, 8, 9, 10, 7; the numbering is kept so cross-references stay valid). Until Task 10 swaps the old `WorkloadWeightsDialog` for the profiles dialog, the legacy "Weights" button would get a 404 on save, so the backend write path must not close first. This keeps the old page working at every commit (cross-slice rule: the app stays shippable until S4).

**Interfaces:**
- Consumes: nothing new.
- Produces: no `UpdateWorkloadWeight` action, no `KpiService.UpdateWorkloadWeightAsync`, no `UpdateWorkloadWeightRequest`.

- [ ] **Step 1: Write the failing test (the reviewed matrix loses the endpoint)**

In `backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt` delete the line
```
KpiController.UpdateWorkloadWeight(String, UpdateWorkloadWeightRequest): SystemAdministrator,SectionHead
```
(line 275; leave `KpiController.GetWorkloadWeights(): ...` in place).

- [ ] **Step 2: Run it, expect failure**

```
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~AuthorizationMatrixTests"
```
Expected: FAIL - the matrix test reports `KpiController.UpdateWorkloadWeight(String, UpdateWorkloadWeightRequest)` as present in the code but missing from the reviewed file.

- [ ] **Step 3: Remove the write path**

- `KpiController.cs`: delete the whole `[HttpPut("workload-weights/{testCode}")] ... UpdateWorkloadWeight(...)` action (lines 40-50). Leave `GetWorkloadWeights` and the rest.
- `KpiService.cs`: delete `public record UpdateWorkloadWeightRequest(...)` (lines 33-35) and the whole `UpdateWorkloadWeightAsync` method (lines 146-209). Leave `GetWorkloadWeightsAsync`, `WorkloadWeightDto` and everything else.
- `WorkloadWeightsTests.cs`: delete only `UpdateWorkloadWeightAsync_PersistsNewWeightAndCreatesAuditHistory` (lines 20-64). Keep `GetAnalystKpisAsync_CalculatesWorkloadUnitsFromConfiguredWeights`.
- `DbSeeder.cs` line 490: change the description string `"View laboratory KPIs and manage workload weights."` to `"View laboratory KPIs."` (existing databases keep their stored text; that is harmless).

- [ ] **Step 4: Build and run the affected tests, expect pass**

```
dotnet build backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --artifacts-path "$TEMP/kpi-art"
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art" --filter "FullyQualifiedName~Kpi|FullyQualifiedName~WorkloadWeight|FullyQualifiedName~WorkflowProfile|FullyQualifiedName~AuthorizationMatrix|FullyQualifiedName~RolePermission|FullyQualifiedName~Permission"
```
Expected: build succeeds with no reference to the removed members; all PASS. (If the build reports another caller of `UpdateWorkloadWeightAsync`, stop: recon found none.)

- [ ] **Step 5: Commit**

```
git diff -- backend/MicroLIMS.API/Controllers/KpiController.cs backend/MicroLIMS.Application/Services/KpiService.cs backend/MicroLIMS.Persistence/Seed/DbSeeder.cs
git add backend/MicroLIMS.API/Controllers/KpiController.cs backend/MicroLIMS.Application/Services/KpiService.cs backend/MicroLIMS.Tests/WorkflowTests/WorkloadWeightsTests.cs backend/MicroLIMS.Tests/ArchitectureTests/authorization-matrix.txt
git add -p backend/MicroLIMS.Persistence/Seed/DbSeeder.cs
git commit -m "refactor(kpi): close the legacy workload weight write path

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

### Task 8: Frontend types, service and hook

All frontend commands run from `E:\MicroLIMS\MicroLIMS\frontend`.

**Files:**
- Create: `frontend/src/modules/kpi/types/workflowProfileTypes.ts`
- Create: `frontend/src/modules/kpi/services/workflowProfileService.ts`
- Create: `frontend/src/modules/kpi/hooks/useWorkflowProfiles.ts`
- Test: `frontend/src/modules/kpi/services/workflowProfileService.test.ts`
- Test: `frontend/src/modules/kpi/hooks/useWorkflowProfiles.test.tsx`

**Interfaces:**
- Consumes: `apiClient`, `ifMatch(version)` from `frontend/src/services/apiClient.ts` (`ifMatch` returns `{ headers: { "If-Match": "\"<n>\"" } }`); backend routes of Task 5.
- Produces:
  - Types exactly as contract 2.6 (`LiveWorkflowType`, `WorkflowProfileStatus`, `ProfileCoverage`, `WorkflowProfileVersion`, `CreateWorkflowProfileVersionRequest`, `SignWorkflowProfileDraftRequest`, `WorkflowProfileTestRow`, `WorkflowProfileTypeRow`, `WorkflowProfileOverview`, `WorkflowProfileCoverage`) plus helpers `workflowTypeLabel(type: LiveWorkflowType): string` and `profileErrorMessage(e: unknown, fallback: string): string`.
  - `workflowProfileService` with `list(params)`, `overview()`, `coverage()`, `create(req)`, `signDraft(id, version, req)`.
  - `useWorkflowProfiles(): { overview: WorkflowProfileOverview | null; coverage: WorkflowProfileCoverage | null; loading: boolean; error: string | null; reload(): Promise<void> }`.

- [ ] **Step 1: Write the failing tests**

Create `frontend/src/modules/kpi/services/workflowProfileService.test.ts`:
```ts
import { afterEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../../../services/apiClient";
import { workflowProfileService } from "./workflowProfileService";

vi.mock("../../../services/apiClient", () => ({
  apiClient: { get: vi.fn(), post: vi.fn() },
  ifMatch: (version: number) => ({ headers: { "If-Match": `"${version}"` } })
}));

afterEach(() => vi.clearAllMocks());

describe("workflowProfileService", () => {
  it("lists with the filters as query params and unwraps the envelope", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: { success: true, data: [{ id: 1 }] } } as never);

    const rows = await workflowProfileService.list({ workflowType: "CountTest", defaultOnly: true });

    expect(apiClient.get).toHaveBeenCalledWith("/kpi/workflow-profiles", {
      params: { workflowType: "CountTest", defaultOnly: true }
    });
    expect(rows).toEqual([{ id: 1 }]);
  });

  it("reads the overview and coverage", async () => {
    vi.mocked(apiClient.get).mockResolvedValue({ data: { success: true, data: { types: [] } } } as never);

    await workflowProfileService.overview();
    await workflowProfileService.coverage();

    expect(apiClient.get).toHaveBeenNthCalledWith(1, "/kpi/workflow-profiles/overview");
    expect(apiClient.get).toHaveBeenNthCalledWith(2, "/kpi/workflow-profiles/coverage");
  });

  it("posts a new version with exactly the wire field names", async () => {
    vi.mocked(apiClient.post).mockResolvedValue({ data: { success: true, data: { id: 9 } } } as never);
    const request = {
      workflowType: "CountTest" as const,
      testCode: null,
      workUnits: 1.5,
      targetAnalystDays: 7,
      effectiveFrom: "2026-10-11",
      reasonForChange: "SOP time study",
      password: "pw"
    };

    const created = await workflowProfileService.create(request);

    expect(apiClient.post).toHaveBeenCalledWith("/kpi/workflow-profiles", request);
    expect(created).toEqual({ id: 9 });
  });

  it("signs a draft with the version it was loaded at as If-Match", async () => {
    vi.mocked(apiClient.post).mockResolvedValue({ data: { success: true, data: { id: 7 } } } as never);
    const request = {
      workUnits: 2,
      targetAnalystDays: null,
      effectiveFrom: "2026-10-12",
      reasonForChange: "Reviewed",
      password: "pw"
    };

    await workflowProfileService.signDraft(7, 3, request);

    expect(apiClient.post).toHaveBeenCalledWith("/kpi/workflow-profiles/7/sign", request, {
      headers: { "If-Match": "\"3\"" }
    });
  });
});
```

Create `frontend/src/modules/kpi/hooks/useWorkflowProfiles.test.tsx`:
```tsx
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import { useWorkflowProfiles } from "./useWorkflowProfiles";
import { workflowProfileService } from "../services/workflowProfileService";

vi.mock("../services/workflowProfileService", () => ({
  workflowProfileService: { overview: vi.fn(), coverage: vi.fn() }
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("useWorkflowProfiles", () => {
  it("loads the overview and coverage together", async () => {
    vi.mocked(workflowProfileService.overview).mockResolvedValue({ types: [] });
    vi.mocked(workflowProfileService.coverage).mockResolvedValue({
      typesWithoutUnits: ["Gravimetric"],
      typesWithoutTarget: [],
      testsNeedingConfiguration: []
    });

    const { result } = renderHook(() => useWorkflowProfiles());
    expect(result.current.loading).toBe(true);

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.error).toBeNull();
    expect(result.current.overview).toEqual({ types: [] });
    expect(result.current.coverage?.typesWithoutUnits).toEqual(["Gravimetric"]);
  });

  it("surfaces the server message instead of swallowing the failure", async () => {
    vi.mocked(workflowProfileService.overview).mockRejectedValue({ response: { data: { message: "Boom" } } });
    vi.mocked(workflowProfileService.coverage).mockResolvedValue({
      typesWithoutUnits: [],
      typesWithoutTarget: [],
      testsNeedingConfiguration: []
    });

    const { result } = renderHook(() => useWorkflowProfiles());

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.error).toBe("Boom");
    expect(result.current.overview).toBeNull();
  });

  it("reload fetches again", async () => {
    vi.mocked(workflowProfileService.overview).mockResolvedValue({ types: [] });
    vi.mocked(workflowProfileService.coverage).mockResolvedValue({
      typesWithoutUnits: [],
      typesWithoutTarget: [],
      testsNeedingConfiguration: []
    });
    const { result } = renderHook(() => useWorkflowProfiles());
    await waitFor(() => expect(result.current.loading).toBe(false));

    await act(async () => {
      await result.current.reload();
    });

    expect(workflowProfileService.overview).toHaveBeenCalledTimes(2);
  });
});
```

- [ ] **Step 2: Run the tests, expect failure**

```
npx vitest run src/modules/kpi
```
Expected: FAIL - `Failed to resolve import "./workflowProfileService"` / `"./useWorkflowProfiles"`.

- [ ] **Step 3: Implement**

Create `frontend/src/modules/kpi/types/workflowProfileTypes.ts`:
```ts
// Wire types for the workflow profile API (KPI remodel S1). Enums travel as strings,
// dates as "yyyy-MM-dd". Mirrors WorkflowProfileDtos.cs.

export type LiveWorkflowType =
  | "CountTest"
  | "Observation"
  | "Measurement"
  | "Gravimetric"
  | "Qualitative"
  | "Dissolution"
  | "Disintegration"
  | "WeightVariation"
  | "HplcMethodAssay"
  | "Titration"
  | "IcpMethodAssay";

export type WorkflowProfileStatus = "Draft" | "Effective";
export type ProfileCoverage = "Configured" | "Partial" | "NeedsConfiguration";

export interface WorkflowProfileVersion {
  id: number;
  workflowType: LiveWorkflowType;
  testCode: string | null;
  testName: string | null;
  workUnits: number | null;
  targetAnalystDays: number | null;
  effectiveFrom: string;
  status: WorkflowProfileStatus;
  reasonForChange: string;
  electronicSignatureId: number | null;
  createdByUserId: number;
  createdByName: string;
  createdAt: string;
  signedAt: string | null;
  version: number;
}

export interface CreateWorkflowProfileVersionRequest {
  workflowType: LiveWorkflowType;
  testCode: string | null;
  workUnits: number | null;
  targetAnalystDays: number | null;
  effectiveFrom: string;
  reasonForChange: string;
  password: string;
}

export interface SignWorkflowProfileDraftRequest {
  workUnits: number | null;
  targetAnalystDays: number | null;
  effectiveFrom: string;
  reasonForChange: string;
  password: string;
}

export interface WorkflowProfileTestRow {
  testCode: string;
  testName: string;
  effectiveWorkUnits: number | null;
  effectiveTargetAnalystDays: number | null;
  hasOverride: boolean;
  coverage: ProfileCoverage;
}

export interface WorkflowProfileTypeRow {
  workflowType: LiveWorkflowType;
  effectiveWorkUnits: number | null;
  effectiveTargetAnalystDays: number | null;
  lastEffectiveFrom: string | null;
  lastChangeAt: string | null;
  lastChangeByName: string | null;
  coverage: ProfileCoverage;
  draftCount: number;
  tests: WorkflowProfileTestRow[];
}

export interface WorkflowProfileOverview {
  types: WorkflowProfileTypeRow[];
}

export interface WorkflowProfileCoverage {
  typesWithoutUnits: LiveWorkflowType[];
  typesWithoutTarget: LiveWorkflowType[];
  testsNeedingConfiguration: WorkflowProfileTestRow[];
}

// "HplcMethodAssay" -> "Hplc Method Assay"
export function workflowTypeLabel(type: LiveWorkflowType): string {
  return type.replace(/([a-z])([A-Z])/g, "$1 $2");
}

// Errors read response.data.message; anything else falls back to a fixed sentence.
export function profileErrorMessage(e: unknown, fallback: string): string {
  const message = (e as { response?: { data?: { message?: string } } } | null)?.response?.data?.message;
  return message ?? fallback;
}
```

Create `frontend/src/modules/kpi/services/workflowProfileService.ts`:
```ts
import { apiClient, ifMatch } from "../../../services/apiClient";
import type {
  CreateWorkflowProfileVersionRequest,
  LiveWorkflowType,
  SignWorkflowProfileDraftRequest,
  WorkflowProfileCoverage,
  WorkflowProfileOverview,
  WorkflowProfileStatus,
  WorkflowProfileVersion
} from "../types/workflowProfileTypes";

interface Envelope<T> {
  success: boolean;
  data: T;
}

const BASE = "/kpi/workflow-profiles";

export interface WorkflowProfileListParams {
  workflowType?: LiveWorkflowType;
  testCode?: string;
  defaultOnly?: boolean;
  status?: WorkflowProfileStatus;
}

export const workflowProfileService = {
  async list(params: WorkflowProfileListParams = {}): Promise<WorkflowProfileVersion[]> {
    const res = await apiClient.get<Envelope<WorkflowProfileVersion[]>>(BASE, { params });
    return res.data.data;
  },

  async overview(): Promise<WorkflowProfileOverview> {
    const res = await apiClient.get<Envelope<WorkflowProfileOverview>>(`${BASE}/overview`);
    return res.data.data;
  },

  async coverage(): Promise<WorkflowProfileCoverage> {
    const res = await apiClient.get<Envelope<WorkflowProfileCoverage>>(`${BASE}/coverage`);
    return res.data.data;
  },

  async create(req: CreateWorkflowProfileVersionRequest): Promise<WorkflowProfileVersion> {
    const res = await apiClient.post<Envelope<WorkflowProfileVersion>>(BASE, req);
    return res.data.data;
  },

  // `version` is the Draft's version as loaded; the server refuses a stale form with 409.
  async signDraft(id: number, version: number, req: SignWorkflowProfileDraftRequest): Promise<WorkflowProfileVersion> {
    const res = await apiClient.post<Envelope<WorkflowProfileVersion>>(`${BASE}/${id}/sign`, req, ifMatch(version));
    return res.data.data;
  }
};
```

Create `frontend/src/modules/kpi/hooks/useWorkflowProfiles.ts`:
```ts
import { useCallback, useEffect, useState } from "react";
import { workflowProfileService } from "../services/workflowProfileService";
import {
  profileErrorMessage,
  type WorkflowProfileCoverage,
  type WorkflowProfileOverview
} from "../types/workflowProfileTypes";

export function useWorkflowProfiles() {
  const [overview, setOverview] = useState<WorkflowProfileOverview | null>(null);
  const [coverage, setCoverage] = useState<WorkflowProfileCoverage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [o, c] = await Promise.all([workflowProfileService.overview(), workflowProfileService.coverage()]);
      setOverview(o);
      setCoverage(c);
    } catch (e) {
      setError(profileErrorMessage(e, "Could not load workflow profiles."));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void reload();
  }, [reload]);

  return { overview, coverage, loading, error, reload };
}
```

- [ ] **Step 4: Run the tests and the type-check, expect pass**

```
npx vitest run src/modules/kpi
npx tsc --noEmit -p tsconfig.json
```
Expected: 7 tests PASS; `tsc` prints no errors.

- [ ] **Step 5: Commit**

```
git add frontend/src/modules/kpi/types/workflowProfileTypes.ts frontend/src/modules/kpi/services/workflowProfileService.ts frontend/src/modules/kpi/services/workflowProfileService.test.ts frontend/src/modules/kpi/hooks/useWorkflowProfiles.ts frontend/src/modules/kpi/hooks/useWorkflowProfiles.test.tsx
git commit -m "feat(kpi): workflow profile frontend types, service and hook

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 9: AddProfileVersionDialog and ProfileHistoryDrawer

**Files:**
- Create: `frontend/src/modules/kpi/components/AddProfileVersionDialog.tsx`
- Create: `frontend/src/modules/kpi/components/ProfileHistoryDrawer.tsx`
- Test: `frontend/src/modules/kpi/components/AddProfileVersionDialog.test.tsx`
- Test: `frontend/src/modules/kpi/components/ProfileHistoryDrawer.test.tsx`

**Interfaces:**
- Consumes: `FloatingDialog` (`frontend/src/components/FloatingDialog.tsx`: `open, title, onClose, children, actions, maxWidth`), `SignatureDialog` (`frontend/src/components/SignatureDialog.tsx`: `open, meaningStatement, onCancel, onConfirm(password): Promise<void>`; it keeps itself open and shows the server message when `onConfirm` throws), `useHasPermission` (`frontend/src/contexts/AuthContext.tsx`), `PERMISSIONS.KPI_CONFIGURE_PROFILES` (Task 5), `workflowProfileService` and types (Task 8).
- Produces:
  - `AddProfileVersionDialog({ open, onClose(): void, onDone(): void, workflowType: LiveWorkflowType, testCode: string | null, draft?: WorkflowProfileVersion })`
  - `ProfileHistoryDrawer({ open, onClose(): void, workflowType: LiveWorkflowType, testCode: string | null, onChanged?(): void })` (`onChanged` is an additive optional prop so the panel can refresh after a Draft is signed from the drawer).

- [ ] **Step 1: Write the failing tests**

Create `frontend/src/modules/kpi/components/AddProfileVersionDialog.test.tsx`:
```tsx
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AddProfileVersionDialog } from "./AddProfileVersionDialog";
import { workflowProfileService } from "../services/workflowProfileService";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import type { WorkflowProfileVersion } from "../types/workflowProfileTypes";

vi.mock("../services/workflowProfileService", () => ({
  workflowProfileService: { create: vi.fn(), signDraft: vi.fn() }
}));

// The real dialog is covered by its own tests; here only the confirm hand-off matters.
vi.mock("../../../components/SignatureDialog", async () => {
  const React = await import("react");
  return {
    SignatureDialog: (props: { open: boolean; meaningStatement: string; onConfirm: (password: string) => Promise<void> }) =>
      props.open
        ? React.createElement(
            "div",
            null,
            React.createElement("p", null, props.meaningStatement),
            React.createElement("button", { onClick: () => void props.onConfirm("pw-123") }, "confirm-sign")
          )
        : null
  };
});

const draft = (overrides: Partial<WorkflowProfileVersion> = {}): WorkflowProfileVersion => ({
  id: 7,
  workflowType: "CountTest",
  testCode: "TAMC",
  testName: "Total Aerobic Microbial Count",
  workUnits: 1.5,
  targetAnalystDays: null,
  effectiveFrom: "2026-10-20",
  status: "Draft",
  reasonForChange: "Imported from legacy workload weight",
  electronicSignatureId: null,
  createdByUserId: 0,
  createdByName: "System (legacy import)",
  createdAt: "2026-10-01T00:00:00Z",
  signedAt: null,
  version: 4,
  ...overrides
});

function renderDialog(props: Partial<React.ComponentProps<typeof AddProfileVersionDialog>> = {}) {
  const onClose = vi.fn();
  const onDone = vi.fn();
  render(
    <PinnedLightTheme>
      <AddProfileVersionDialog open onClose={onClose} onDone={onDone} workflowType="CountTest" testCode={null} {...props} />
    </PinnedLightTheme>
  );
  return { onClose, onDone };
}

const continueButton = () => screen.getByRole("button", { name: "Continue to sign" }) as HTMLButtonElement;

beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date("2026-10-11T10:00:00Z"));
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.clearAllMocks();
});

describe("AddProfileVersionDialog", () => {
  it("keeps Continue to sign disabled until a value and a reason are entered", async () => {
    renderDialog();
    expect(continueButton().disabled).toBe(true);

    await userEvent.type(screen.getByLabelText(/Work units/i), "2");
    expect(continueButton().disabled).toBe(true);

    await userEvent.type(screen.getByLabelText(/Reason for change/i), "SOP time study");
    expect(continueButton().disabled).toBe(false);
  });

  it("defaults a new version to today", () => {
    renderDialog();
    expect((screen.getByLabelText(/Effective from/i) as HTMLInputElement).value).toBe("2026-10-11");
  });

  it("prefills today when the draft date is in the past", () => {
    renderDialog({ draft: draft({ effectiveFrom: "2026-10-01" }), testCode: "TAMC" });
    expect((screen.getByLabelText(/Effective from/i) as HTMLInputElement).value).toBe("2026-10-11");
  });

  it("keeps a draft date that is still in the future", () => {
    renderDialog({ draft: draft({ effectiveFrom: "2026-10-20" }), testCode: "TAMC" });
    expect((screen.getByLabelText(/Effective from/i) as HTMLInputElement).value).toBe("2026-10-20");
  });

  it("creates a version after the signature step and reports done", async () => {
    vi.mocked(workflowProfileService.create).mockResolvedValue(draft());
    const { onClose, onDone } = renderDialog();

    await userEvent.type(screen.getByLabelText(/Work units/i), "2.5");
    await userEvent.type(screen.getByLabelText(/Target analyst days/i), "10");
    await userEvent.type(screen.getByLabelText(/Reason for change/i), "SOP time study");
    await userEvent.click(continueButton());
    expect(screen.getByText(/Count Test type default/)).toBeTruthy();
    await userEvent.click(screen.getByText("confirm-sign"));

    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(workflowProfileService.create).toHaveBeenCalledWith({
      workflowType: "CountTest",
      testCode: null,
      workUnits: 2.5,
      targetAnalystDays: 10,
      effectiveFrom: "2026-10-11",
      reasonForChange: "SOP time study",
      password: "pw-123"
    });
    expect(onClose).toHaveBeenCalled();
  });

  it("signs a draft with the version it was loaded at", async () => {
    vi.mocked(workflowProfileService.signDraft).mockResolvedValue(draft({ status: "Effective" }));
    const { onDone } = renderDialog({ draft: draft(), testCode: "TAMC" });

    await userEvent.type(screen.getByLabelText(/Reason for change/i), "Reviewed against the SOP");
    await userEvent.click(continueButton());
    await userEvent.click(screen.getByText("confirm-sign"));

    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(workflowProfileService.signDraft).toHaveBeenCalledWith(7, 4, {
      workUnits: 1.5,
      targetAnalystDays: null,
      effectiveFrom: "2026-10-20",
      reasonForChange: "Reviewed against the SOP",
      password: "pw-123"
    });
    expect(workflowProfileService.create).not.toHaveBeenCalled();
  });
});
```

Create `frontend/src/modules/kpi/components/ProfileHistoryDrawer.test.tsx`:
```tsx
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { ProfileHistoryDrawer } from "./ProfileHistoryDrawer";
import { workflowProfileService } from "../services/workflowProfileService";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import type { WorkflowProfileVersion } from "../types/workflowProfileTypes";

const auth = vi.hoisted(() => ({ canConfigure: false }));

vi.mock("../../../contexts/AuthContext", () => ({
  useAuth: () => ({ fullName: "Sam Signer", username: "sam", role: "SectionHead", permissions: [] }),
  useHasPermission: () => auth.canConfigure
}));
vi.mock("../services/workflowProfileService", () => ({
  workflowProfileService: { list: vi.fn(), create: vi.fn(), signDraft: vi.fn() }
}));

const row = (overrides: Partial<WorkflowProfileVersion>): WorkflowProfileVersion => ({
  id: 1,
  workflowType: "CountTest",
  testCode: null,
  testName: null,
  workUnits: 1,
  targetAnalystDays: 7,
  effectiveFrom: "2026-01-01",
  status: "Effective",
  reasonForChange: "Baseline",
  electronicSignatureId: 5,
  createdByUserId: 2,
  createdByName: "Jane Head",
  createdAt: "2026-01-01T00:00:00Z",
  signedAt: "2026-01-01T00:00:00Z",
  version: 1,
  ...overrides
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  auth.canConfigure = false;
});

const renderDrawer = (testCode: string | null) =>
  render(
    <PinnedLightTheme>
      <ProfileHistoryDrawer open onClose={() => {}} workflowType="CountTest" testCode={testCode} />
    </PinnedLightTheme>
  );

describe("ProfileHistoryDrawer", () => {
  it("lists the type defaults with who, when and why", async () => {
    vi.mocked(workflowProfileService.list).mockResolvedValue([
      row({ id: 2, status: "Draft", reasonForChange: "Imported from legacy workload weight", createdByName: "System (legacy import)", signedAt: null }),
      row({ id: 1 })
    ]);

    renderDrawer(null);

    await waitFor(() => expect(screen.getByText("Baseline")).toBeTruthy());
    expect(workflowProfileService.list).toHaveBeenCalledWith({ workflowType: "CountTest", defaultOnly: true });
    expect(screen.getByText("Imported from legacy workload weight")).toBeTruthy();
    expect(screen.getByText("Jane Head")).toBeTruthy();
    expect(screen.getByText("Draft")).toBeTruthy();
  });

  it("asks for one test's rows by test code only", async () => {
    vi.mocked(workflowProfileService.list).mockResolvedValue([]);

    renderDrawer("TAMC");

    await waitFor(() => expect(workflowProfileService.list).toHaveBeenCalledWith({ testCode: "TAMC" }));
    expect(screen.getByText(/No versions yet/i)).toBeTruthy();
  });

  it("offers Review and sign for a Draft only to someone who may configure", async () => {
    vi.mocked(workflowProfileService.list).mockResolvedValue([row({ id: 2, status: "Draft", signedAt: null })]);

    renderDrawer(null);
    await waitFor(() => expect(screen.getByText("Draft")).toBeTruthy());
    expect(screen.queryByRole("button", { name: /Review and sign/i })).toBeNull();

    cleanup();
    auth.canConfigure = true;
    renderDrawer(null);
    await waitFor(() => expect(screen.getByRole("button", { name: /Review and sign/i })).toBeTruthy());
  });

  it("shows the server message when loading fails", async () => {
    vi.mocked(workflowProfileService.list).mockRejectedValue({ response: { data: { message: "Not allowed" } } });

    renderDrawer(null);

    await waitFor(() => expect(screen.getByText("Not allowed")).toBeTruthy());
  });
});
```

- [ ] **Step 2: Run the tests, expect failure**

```
npx vitest run src/modules/kpi/components
```
Expected: FAIL - `Failed to resolve import "./AddProfileVersionDialog"` / `"./ProfileHistoryDrawer"`.

- [ ] **Step 3: Implement**

Create `frontend/src/modules/kpi/components/AddProfileVersionDialog.tsx`:
```tsx
import { useEffect, useState } from "react";
import { Button, Stack, TextField, Typography } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { workflowProfileService } from "../services/workflowProfileService";
import { workflowTypeLabel, type LiveWorkflowType, type WorkflowProfileVersion } from "../types/workflowProfileTypes";

interface AddProfileVersionDialogProps {
  open: boolean;
  onClose: () => void;
  onDone: () => void;
  workflowType: LiveWorkflowType;
  testCode: string | null;
  // Present when a Draft is being reviewed and signed instead of a new version being added.
  draft?: WorkflowProfileVersion;
}

// The server compares against today in UTC, so the form does too.
const todayUtc = () => new Date().toISOString().slice(0, 10);
const parse = (v: string): number | null => (v.trim() === "" ? null : Number(v));

// Step 1: the values and a reason. Step 2: SignatureDialog. Same two-step flow as
// ChangeMediaProductCodeDialog. All rules (dates, ranges, retired types) are enforced by the server.
export function AddProfileVersionDialog({ open, onClose, onDone, workflowType, testCode, draft }: AddProfileVersionDialogProps) {
  const [units, setUnits] = useState("");
  const [target, setTarget] = useState("");
  const [effectiveFrom, setEffectiveFrom] = useState(todayUtc());
  const [reason, setReason] = useState("");
  const [signatureOpen, setSignatureOpen] = useState(false);

  useEffect(() => {
    if (!open) return;
    setUnits(draft?.workUnits?.toString() ?? "");
    setTarget(draft?.targetAnalystDays?.toString() ?? "");
    // An imported Draft carries the migration day; a past date would be refused, so offer today.
    const today = todayUtc();
    setEffectiveFrom(draft && draft.effectiveFrom >= today ? draft.effectiveFrom : today);
    setReason("");
    setSignatureOpen(false);
  }, [open, draft]);

  const u = parse(units);
  const t = parse(target);
  const valuesOk =
    (u !== null || t !== null) &&
    (u === null || (Number.isFinite(u) && u > 0)) &&
    (t === null || (Number.isFinite(t) && t > 0));
  const canContinue = valuesOk && reason.trim() !== "" && /^\d{4}-\d{2}-\d{2}$/.test(effectiveFrom);

  const scope = testCode ? `test ${testCode}` : `${workflowTypeLabel(workflowType)} type default`;
  const meaningStatement =
    `I am ${draft ? "signing" : "adding"} a workflow profile version for ${scope}: ` +
    `${u ?? "inherited"} work units (1 unit = 1 standard analyst-hour), ${t ?? "inherited"} target analyst days, ` +
    `effective ${effectiveFrom}. Reason: ${reason.trim()}`;

  const handleConfirm = async (password: string) => {
    if (draft) {
      await workflowProfileService.signDraft(draft.id, draft.version, {
        workUnits: u,
        targetAnalystDays: t,
        effectiveFrom,
        reasonForChange: reason.trim(),
        password
      });
    } else {
      await workflowProfileService.create({
        workflowType,
        testCode,
        workUnits: u,
        targetAnalystDays: t,
        effectiveFrom,
        reasonForChange: reason.trim(),
        password
      });
    }
    setSignatureOpen(false);
    onDone();
    onClose();
  };

  return (
    <>
      <FloatingDialog
        open={open && !signatureOpen}
        title={`${draft ? "Review and sign" : "Add version"}: ${testCode ?? workflowTypeLabel(workflowType)}`}
        onClose={onClose}
        maxWidth="xs"
        actions={
          <>
            <Button onClick={onClose}>Cancel</Button>
            <Button variant="contained" disabled={!canContinue} onClick={() => setSignatureOpen(true)}>
              Continue to sign
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2" color="text.secondary">
            {testCode
              ? "Leave a field empty to inherit it from the workflow type default."
              : "Applies to every test of this workflow type that has no override."}
          </Typography>
          <TextField
            label="Work units"
            type="number"
            size="small"
            value={units}
            onChange={(e) => setUnits(e.target.value)}
            helperText="1 unit = 1 standard analyst-hour of hands-on work"
            slotProps={{ htmlInput: { min: 0, step: 0.01 } }}
            autoFocus
          />
          <TextField
            label="Target analyst days"
            type="number"
            size="small"
            value={target}
            onChange={(e) => setTarget(e.target.value)}
            helperText="Analyst-stage deadline, from assignment to submission for review"
            slotProps={{ htmlInput: { min: 0, step: 0.01 } }}
          />
          <TextField
            label="Effective from"
            type="date"
            size="small"
            value={effectiveFrom}
            onChange={(e) => setEffectiveFrom(e.target.value)}
            helperText="Cannot be in the past, except for the first version of this profile"
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <TextField
            label="Reason for change"
            required
            multiline
            rows={3}
            size="small"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="e.g. SOP time study 2026"
          />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={open && signatureOpen}
        meaningStatement={meaningStatement}
        onCancel={() => setSignatureOpen(false)}
        onConfirm={handleConfirm}
      />
    </>
  );
}
```

Create `frontend/src/modules/kpi/components/ProfileHistoryDrawer.tsx`:
```tsx
import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Drawer,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { useHasPermission } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { workflowProfileService } from "../services/workflowProfileService";
import {
  profileErrorMessage,
  workflowTypeLabel,
  type LiveWorkflowType,
  type WorkflowProfileVersion
} from "../types/workflowProfileTypes";
import { AddProfileVersionDialog } from "./AddProfileVersionDialog";

interface ProfileHistoryDrawerProps {
  open: boolean;
  onClose: () => void;
  workflowType: LiveWorkflowType;
  testCode: string | null;
  onChanged?: () => void;
}

const fmt = (n: number | null) => (n === null ? "-" : String(n));

// Every version of one scope, newest first. A test shows all of its own rows whatever
// type they were written under (the test may have been re-typed since).
export function ProfileHistoryDrawer({ open, onClose, workflowType, testCode, onChanged }: ProfileHistoryDrawerProps) {
  const canConfigure = useHasPermission(PERMISSIONS.KPI_CONFIGURE_PROFILES);
  const [rows, setRows] = useState<WorkflowProfileVersion[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [signing, setSigning] = useState<WorkflowProfileVersion | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setRows(await workflowProfileService.list(testCode ? { testCode } : { workflowType, defaultOnly: true }));
    } catch (e) {
      setError(profileErrorMessage(e, "Could not load the version history."));
    } finally {
      setLoading(false);
    }
  }, [workflowType, testCode]);

  useEffect(() => {
    if (open) void load();
  }, [open, load]);

  return (
    <>
      <Drawer anchor="right" open={open} onClose={onClose}>
        <Box sx={{ width: { xs: "100vw", sm: 720 }, p: 2 }}>
          <Stack direction="row" sx={{ alignItems: "center", justifyContent: "space-between", mb: 2 }}>
            <Typography variant="h6">History: {testCode ?? `${workflowTypeLabel(workflowType)} default`}</Typography>
            <IconButton aria-label="Close history" onClick={onClose}>
              <CloseIcon />
            </IconButton>
          </Stack>

          {error && <Alert severity="error">{error}</Alert>}
          {loading && <CircularProgress size={24} />}
          {!loading && !error && rows.length === 0 && (
            <Typography color="text.secondary">No versions yet.</Typography>
          )}

          {rows.length > 0 && (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Effective from</TableCell>
                  <TableCell align="right">Work units</TableCell>
                  <TableCell align="right">Target (days)</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Reason</TableCell>
                  <TableCell>By</TableCell>
                  <TableCell />
                </TableRow>
              </TableHead>
              <TableBody>
                {rows.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell>{r.effectiveFrom}</TableCell>
                    <TableCell align="right">{fmt(r.workUnits)}</TableCell>
                    <TableCell align="right">{fmt(r.targetAnalystDays)}</TableCell>
                    <TableCell>
                      <Chip size="small" label={r.status} color={r.status === "Effective" ? "success" : "default"} />
                    </TableCell>
                    <TableCell>{r.reasonForChange}</TableCell>
                    <TableCell>
                      {r.createdByName}
                      {r.signedAt && (
                        <Typography variant="caption" sx={{ display: "block" }} color="text.secondary">
                          signed {r.signedAt.slice(0, 10)}
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell>
                      {canConfigure && r.status === "Draft" && (
                        <Button size="small" onClick={() => setSigning(r)}>
                          Review and sign
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </Box>
      </Drawer>

      {signing && (
        <AddProfileVersionDialog
          open
          draft={signing}
          workflowType={signing.workflowType}
          testCode={signing.testCode}
          onClose={() => setSigning(null)}
          onDone={() => {
            void load();
            onChanged?.();
          }}
        />
      )}
    </>
  );
}
```

- [ ] **Step 4: Run the tests and the type-check, expect pass**

```
npx vitest run src/modules/kpi
npx tsc --noEmit -p tsconfig.json
```
Expected: all PASS, no type errors. If `slotProps={{ htmlInput: ... }}` is rejected by the installed MUI version, use `inputProps={{ min: 0, step: 0.01 }}` for those two fields (and `InputLabelProps={{ shrink: true }}` for the date field).

- [ ] **Step 5: Commit**

```
git add frontend/src/modules/kpi/components/AddProfileVersionDialog.tsx frontend/src/modules/kpi/components/AddProfileVersionDialog.test.tsx frontend/src/modules/kpi/components/ProfileHistoryDrawer.tsx frontend/src/modules/kpi/components/ProfileHistoryDrawer.test.tsx
git commit -m "feat(kpi): add-version dialog and profile history drawer

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 10: WorkflowProfilesPanel, dialog, and the swap in the old KPI tab

**Files:**
- Create: `frontend/src/modules/kpi/components/WorkflowProfilesPanel.tsx`
- Create: `frontend/src/modules/kpi/components/WorkflowProfilesDialog.tsx`
- Test: `frontend/src/modules/kpi/components/WorkflowProfilesPanel.test.tsx`
- Modify: `frontend/src/modules/reports/components/AnalystKpiTab.tsx:25` (import), `:89` (state), `:350-357` (button), `:994-999` (dialog)
- Modify: `frontend/src/modules/reports/services/AnalystKpiService.ts:340-352` (remove `updateWorkloadWeight`)
- Delete: `frontend/src/modules/reports/components/WorkloadWeightsDialog.tsx`

**Interfaces:**
- Consumes: Task 8 hook/types, Task 9 dialog and drawer, `useHasPermission`, `PERMISSIONS.KPI_CONFIGURE_PROFILES`, `FloatingDialog`.
- Produces: `WorkflowProfilesPanel()` (no props), `WorkflowProfilesDialog({ open, onClose(): void })`.

- [ ] **Step 1: Write the failing tests**

Create `frontend/src/modules/kpi/components/WorkflowProfilesPanel.test.tsx`:
```tsx
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { WorkflowProfilesPanel } from "./WorkflowProfilesPanel";
import { workflowProfileService } from "../services/workflowProfileService";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import type { WorkflowProfileCoverage, WorkflowProfileOverview } from "../types/workflowProfileTypes";

const auth = vi.hoisted(() => ({ canConfigure: false }));

vi.mock("../../../contexts/AuthContext", () => ({
  useAuth: () => ({ fullName: "Sam Signer", username: "sam", role: "SectionHead", permissions: [] }),
  useHasPermission: () => auth.canConfigure
}));
vi.mock("../services/workflowProfileService", () => ({
  workflowProfileService: { overview: vi.fn(), coverage: vi.fn(), list: vi.fn(), create: vi.fn(), signDraft: vi.fn() }
}));

const overview: WorkflowProfileOverview = {
  types: [
    {
      workflowType: "CountTest",
      effectiveWorkUnits: 1,
      effectiveTargetAnalystDays: 7,
      lastEffectiveFrom: "2026-01-01",
      lastChangeAt: "2026-01-01T09:00:00Z",
      lastChangeByName: "Jane Head",
      coverage: "Configured",
      draftCount: 1,
      tests: [
        {
          testCode: "TAMC",
          testName: "Total Aerobic Microbial Count",
          effectiveWorkUnits: 4,
          effectiveTargetAnalystDays: 7,
          hasOverride: true,
          coverage: "Configured"
        }
      ]
    },
    {
      workflowType: "Measurement",
      effectiveWorkUnits: null,
      effectiveTargetAnalystDays: null,
      lastEffectiveFrom: null,
      lastChangeAt: null,
      lastChangeByName: null,
      coverage: "NeedsConfiguration",
      draftCount: 0,
      tests: [
        {
          testCode: "PH",
          testName: "pH",
          effectiveWorkUnits: null,
          effectiveTargetAnalystDays: null,
          hasOverride: false,
          coverage: "NeedsConfiguration"
        }
      ]
    }
  ]
};

const coverage: WorkflowProfileCoverage = {
  typesWithoutUnits: ["Measurement"],
  typesWithoutTarget: ["Measurement"],
  testsNeedingConfiguration: [overview.types[1].tests[0]]
};

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  auth.canConfigure = false;
});

function renderPanel() {
  vi.mocked(workflowProfileService.overview).mockResolvedValue(overview);
  vi.mocked(workflowProfileService.coverage).mockResolvedValue(coverage);
  render(
    <PinnedLightTheme>
      <WorkflowProfilesPanel />
    </PinnedLightTheme>
  );
}

describe("WorkflowProfilesPanel", () => {
  it("shows the effective values per workflow type and marks what is not set", async () => {
    renderPanel();

    await waitFor(() => expect(screen.getByText("Count Test")).toBeTruthy());
    expect(screen.getByText("Measurement")).toBeTruthy();
    expect(screen.getAllByText("Not set").length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText("Jane Head")).toBeTruthy();
    expect(screen.getByText("1 draft")).toBeTruthy();
    expect(screen.getByText(/1 workflow type without work units/i)).toBeTruthy();
  });

  it("hides Add version from someone without Kpi.ConfigureProfiles", async () => {
    renderPanel();
    await waitFor(() => expect(screen.getByText("Count Test")).toBeTruthy());

    expect(screen.queryAllByRole("button", { name: /Add version/i })).toHaveLength(0);
    expect(screen.getAllByRole("button", { name: /History/i }).length).toBeGreaterThan(0);
  });

  it("offers Add version to someone with Kpi.ConfigureProfiles", async () => {
    auth.canConfigure = true;
    renderPanel();
    await waitFor(() => expect(screen.getByText("Count Test")).toBeTruthy());

    expect(screen.getAllByRole("button", { name: /Add version/i }).length).toBeGreaterThan(0);
  });

  it("needs-configuration filter hides fully configured types", async () => {
    renderPanel();
    await waitFor(() => expect(screen.getByText("Count Test")).toBeTruthy());

    await userEvent.click(screen.getByLabelText(/Needs configuration only/i));

    expect(screen.queryByText("Count Test")).toBeNull();
    expect(screen.getByText("Measurement")).toBeTruthy();
  });

  it("expands a type to show its tests and flags overrides", async () => {
    renderPanel();
    await waitFor(() => expect(screen.getByText("Count Test")).toBeTruthy());

    await userEvent.click(screen.getByRole("button", { name: /Show tests for Count Test/i }));

    expect(screen.getByText("TAMC")).toBeTruthy();
    expect(screen.getByText("Override")).toBeTruthy();
  });

  it("shows the load error instead of an empty table", async () => {
    vi.mocked(workflowProfileService.overview).mockRejectedValue({ response: { data: { message: "No access" } } });
    vi.mocked(workflowProfileService.coverage).mockResolvedValue(coverage);
    render(
      <PinnedLightTheme>
        <WorkflowProfilesPanel />
      </PinnedLightTheme>
    );

    await waitFor(() => expect(screen.getByText("No access")).toBeTruthy());
    expect(screen.queryByText("Count Test")).toBeNull();
  });
});
```

- [ ] **Step 2: Run the test, expect failure**

```
npx vitest run src/modules/kpi/components/WorkflowProfilesPanel.test.tsx
```
Expected: FAIL - `Failed to resolve import "./WorkflowProfilesPanel"`.

- [ ] **Step 3: Implement the panel and dialog**

Create `frontend/src/modules/kpi/components/WorkflowProfilesPanel.tsx`:
```tsx
import { Fragment, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Collapse,
  FormControlLabel,
  IconButton,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography
} from "@mui/material";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowRightIcon from "@mui/icons-material/KeyboardArrowRight";
import { useHasPermission } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { useWorkflowProfiles } from "../hooks/useWorkflowProfiles";
import {
  workflowTypeLabel,
  type LiveWorkflowType,
  type ProfileCoverage,
  type WorkflowProfileTestRow,
  type WorkflowProfileTypeRow
} from "../types/workflowProfileTypes";
import { AddProfileVersionDialog } from "./AddProfileVersionDialog";
import { ProfileHistoryDrawer } from "./ProfileHistoryDrawer";

interface Scope {
  workflowType: LiveWorkflowType;
  testCode: string | null;
}

const COVERAGE_LABEL: Record<ProfileCoverage, string> = {
  Configured: "Configured",
  Partial: "Partial",
  NeedsConfiguration: "Needs configuration"
};
const COVERAGE_COLOR: Record<ProfileCoverage, "success" | "warning" | "default"> = {
  Configured: "success",
  Partial: "warning",
  NeedsConfiguration: "default"
};

const value = (n: number | null) => (n === null ? "Not set" : String(n));
const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;

// Configuration only: effective work units and targets per workflow type, with per-test
// overrides underneath. It never shows analyst figures (no ranking, spec D6).
export function WorkflowProfilesPanel() {
  const { overview, coverage, loading, error, reload } = useWorkflowProfiles();
  const canConfigure = useHasPermission(PERMISSIONS.KPI_CONFIGURE_PROFILES);
  const [onlyGaps, setOnlyGaps] = useState(false);
  const [expanded, setExpanded] = useState<Set<LiveWorkflowType>>(new Set());
  const [adding, setAdding] = useState<Scope | null>(null);
  const [history, setHistory] = useState<Scope | null>(null);

  const toggle = (type: LiveWorkflowType) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(type)) next.delete(type);
      else next.add(type);
      return next;
    });

  const needsWork = (c: ProfileCoverage) => c !== "Configured";
  const visibleTests = (row: WorkflowProfileTypeRow): WorkflowProfileTestRow[] =>
    onlyGaps ? row.tests.filter((t) => needsWork(t.coverage)) : row.tests;
  const rows = (overview?.types ?? []).filter(
    (t) => !onlyGaps || needsWork(t.coverage) || t.tests.some((x) => needsWork(x.coverage))
  );

  const gaps = coverage
    ? [
        `${plural(coverage.typesWithoutUnits.length, "workflow type", "workflow types")} without work units`,
        `${coverage.typesWithoutTarget.length} without a target`,
        `${plural(coverage.testsNeedingConfiguration.length, "test needs", "tests need")} configuration`
      ]
    : [];
  const hasGaps =
    !!coverage &&
    (coverage.typesWithoutUnits.length > 0 ||
      coverage.typesWithoutTarget.length > 0 ||
      coverage.testsNeedingConfiguration.length > 0);

  return (
    <Box>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Work units: 1 unit = 1 standard analyst-hour of hands-on work. Target: analyst-stage days. A version takes effect on
        its effective date and is never edited; correct a mistake by adding a new version. Tests without a profile stay
        unweighted - they are never counted as 1.0.
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}
      {loading && !overview && <CircularProgress size={24} />}

      {hasGaps && (
        <Alert
          severity="warning"
          sx={{ mb: 2 }}
          action={
            <FormControlLabel
              control={<Switch size="small" checked={onlyGaps} onChange={(e) => setOnlyGaps(e.target.checked)} />}
              label="Needs configuration only"
            />
          }
        >
          {gaps.join(", ")}
        </Alert>
      )}

      {overview && (
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell />
              <TableCell>Workflow type</TableCell>
              <TableCell align="right">Work units</TableCell>
              <TableCell align="right">Target (days)</TableCell>
              <TableCell>Last change</TableCell>
              <TableCell>Status</TableCell>
              <TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((row) => {
              const label = workflowTypeLabel(row.workflowType);
              const open = expanded.has(row.workflowType);
              return (
                <Fragment key={row.workflowType}>
                  <TableRow hover>
                    <TableCell padding="checkbox">
                      <IconButton size="small" aria-label={`Show tests for ${label}`} onClick={() => toggle(row.workflowType)}>
                        {open ? <KeyboardArrowDownIcon /> : <KeyboardArrowRightIcon />}
                      </IconButton>
                    </TableCell>
                    <TableCell>{label}</TableCell>
                    <TableCell align="right">{value(row.effectiveWorkUnits)}</TableCell>
                    <TableCell align="right">{value(row.effectiveTargetAnalystDays)}</TableCell>
                    <TableCell>
                      {row.lastEffectiveFrom ? (
                        <>
                          <Typography variant="body2">{row.lastEffectiveFrom}</Typography>
                          <Typography variant="caption" color="text.secondary">
                            {row.lastChangeByName}
                          </Typography>
                        </>
                      ) : (
                        "-"
                      )}
                    </TableCell>
                    <TableCell>
                      <Stack direction="row" spacing={0.5}>
                        <Chip size="small" label={COVERAGE_LABEL[row.coverage]} color={COVERAGE_COLOR[row.coverage]} />
                        {row.draftCount > 0 && (
                          <Chip size="small" variant="outlined" label={plural(row.draftCount, "draft", "drafts")} />
                        )}
                      </Stack>
                    </TableCell>
                    <TableCell align="right">
                      <Button size="small" onClick={() => setHistory({ workflowType: row.workflowType, testCode: null })}>
                        History
                      </Button>
                      {canConfigure && (
                        <Button size="small" onClick={() => setAdding({ workflowType: row.workflowType, testCode: null })}>
                          Add version
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                  <TableRow>
                    <TableCell colSpan={7} sx={{ py: 0, borderBottom: open ? undefined : "none" }}>
                      <Collapse in={open} unmountOnExit>
                        <Table size="small" sx={{ my: 1 }}>
                          <TableHead>
                            <TableRow>
                              <TableCell>Test</TableCell>
                              <TableCell>Name</TableCell>
                              <TableCell align="right">Work units</TableCell>
                              <TableCell align="right">Target (days)</TableCell>
                              <TableCell>Status</TableCell>
                              <TableCell />
                            </TableRow>
                          </TableHead>
                          <TableBody>
                            {visibleTests(row).map((t) => (
                              <TableRow key={t.testCode}>
                                <TableCell>{t.testCode}</TableCell>
                                <TableCell>{t.testName}</TableCell>
                                <TableCell align="right">{value(t.effectiveWorkUnits)}</TableCell>
                                <TableCell align="right">{value(t.effectiveTargetAnalystDays)}</TableCell>
                                <TableCell>
                                  <Stack direction="row" spacing={0.5}>
                                    <Chip size="small" label={COVERAGE_LABEL[t.coverage]} color={COVERAGE_COLOR[t.coverage]} />
                                    {t.hasOverride && <Chip size="small" variant="outlined" label="Override" />}
                                  </Stack>
                                </TableCell>
                                <TableCell align="right">
                                  <Button
                                    size="small"
                                    onClick={() => setHistory({ workflowType: row.workflowType, testCode: t.testCode })}
                                  >
                                    History
                                  </Button>
                                  {canConfigure && (
                                    <Button
                                      size="small"
                                      onClick={() => setAdding({ workflowType: row.workflowType, testCode: t.testCode })}
                                    >
                                      {t.hasOverride ? "Add version" : "Add override"}
                                    </Button>
                                  )}
                                </TableCell>
                              </TableRow>
                            ))}
                            {visibleTests(row).length === 0 && (
                              <TableRow>
                                <TableCell colSpan={6}>
                                  <Typography variant="body2" color="text.secondary">
                                    No tests to show for this workflow type.
                                  </Typography>
                                </TableCell>
                              </TableRow>
                            )}
                          </TableBody>
                        </Table>
                      </Collapse>
                    </TableCell>
                  </TableRow>
                </Fragment>
              );
            })}
          </TableBody>
        </Table>
      )}

      {adding && (
        <AddProfileVersionDialog
          open
          workflowType={adding.workflowType}
          testCode={adding.testCode}
          onClose={() => setAdding(null)}
          onDone={() => void reload()}
        />
      )}
      {history && (
        <ProfileHistoryDrawer
          open
          workflowType={history.workflowType}
          testCode={history.testCode}
          onClose={() => setHistory(null)}
          onChanged={() => void reload()}
        />
      )}
    </Box>
  );
}
```

Create `frontend/src/modules/kpi/components/WorkflowProfilesDialog.tsx`:
```tsx
import { Button } from "@mui/material";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { WorkflowProfilesPanel } from "./WorkflowProfilesPanel";

interface WorkflowProfilesDialogProps {
  open: boolean;
  onClose: () => void;
}

// Hosts the panel on the existing KPI tab. The panel loads only while the dialog is open.
export function WorkflowProfilesDialog({ open, onClose }: WorkflowProfilesDialogProps) {
  return (
    <FloatingDialog
      open={open}
      title="Workflow Profiles"
      onClose={onClose}
      maxWidth="lg"
      actions={<Button onClick={onClose}>Close</Button>}
    >
      <WorkflowProfilesPanel />
    </FloatingDialog>
  );
}
```

- [ ] **Step 4: Run the panel tests, expect pass**

```
npx vitest run src/modules/kpi
```
Expected: all PASS. If the "1 workflow type without work units" assertion fails because of the wording, fix the component text, not the test: the rule is `<n> workflow type(s) without work units, <m> without a target, <k> test(s) need(s) configuration`.

- [ ] **Step 5: Swap the old tab over and delete the old dialog**

`frontend/src/modules/reports/components/AnalystKpiTab.tsx`:
- Line 25: replace `import { WorkloadWeightsDialog } from "./WorkloadWeightsDialog";` with `import { WorkflowProfilesDialog } from "../../kpi/components/WorkflowProfilesDialog";`
- Line 89: replace `const [weightsDialogOpen, setWeightsDialogOpen] = useState(false);` with `const [profilesDialogOpen, setProfilesDialogOpen] = useState(false);`
- Lines 350-357: replace the button with
```tsx
              <Button
                size="small"
                variant="outlined"
                startIcon={<TuneIcon />}
                onClick={() => setProfilesDialogOpen(true)}
              >
                Workflow Profiles
              </Button>
```
- Lines 994-999: replace the `{/* Workload Weights Dialog */}` block with
```tsx
      {/* Workflow Profiles Dialog */}
      <WorkflowProfilesDialog
        open={profilesDialogOpen}
        onClose={() => setProfilesDialogOpen(false)}
      />
```
(`onUpdated={loadData}` is dropped on purpose: profiles do not feed the legacy maths in S1, so there is nothing to reload.)

`frontend/src/modules/reports/services/AnalystKpiService.ts` - remove the dead write call. Replace
```ts
    }));
  },

  async updateWorkloadWeight(
    testCode: string,
    newWeight: number,
    reason: string
  ): Promise<void> {
    await apiClient.put(`/kpi/workload-weights/${testCode}`, {
      weight: newWeight,
      reasonForChange: reason
    });
  }
};
```
with
```ts
    }));
  }
};
```
Keep `getWorkloadWeights` and all other methods (S4 removes them).

Delete the old dialog:
```
git rm frontend/src/modules/reports/components/WorkloadWeightsDialog.tsx
git grep -n "WorkloadWeightsDialog\|updateWorkloadWeight" -- frontend/src
```
Expected: the `git grep` prints nothing.

- [ ] **Step 6: Verify the frontend**

```
npx tsc --noEmit -p tsconfig.json
npx vitest run src/modules/kpi
npm test
npm run lint
```
Expected: `tsc` clean; the kpi tests PASS; the full suite PASSES (no other test imported the deleted dialog); lint reports no NEW errors in the files this slice touched (existing warnings are fine).

- [ ] **Step 7: Commit**

```
git diff -- frontend/src/modules/reports/components/AnalystKpiTab.tsx frontend/src/modules/reports/services/AnalystKpiService.ts
git add frontend/src/modules/kpi/components/WorkflowProfilesPanel.tsx frontend/src/modules/kpi/components/WorkflowProfilesPanel.test.tsx frontend/src/modules/kpi/components/WorkflowProfilesDialog.tsx
git add -p frontend/src/modules/reports/components/AnalystKpiTab.tsx frontend/src/modules/reports/services/AnalystKpiService.ts
git commit -m "feat(kpi): workflow profiles panel replaces the workload weights dialog

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```
(`AnalystKpiTab.tsx` and `AnalystKpiService.ts` already carry the uncommitted 2026-10-11 KPI page patch (deltas removed, nullable percents, reviewReturns). Stage only the Workflow Profiles hunks with `git add -p`; leave the patch hunks unstaged, or have the human commit that patch first.) (`git rm` already staged the deletion of `WorkloadWeightsDialog.tsx`; it is included in this commit. Check `git status --short` shows no other staged path.)

---

## Slice verification (run before calling S1 done)

- [ ] **Backend:** from `E:\MicroLIMS\MicroLIMS`, with `MICROLIMS_TEST_POSTGRES` set so no Postgres test is skipped:
```
dotnet build backend/MicroLIMS.Tests/MicroLIMS.Tests.csproj --artifacts-path "$TEMP/kpi-art"
dotnet test backend/MicroLIMS.Tests --artifacts-path "$TEMP/kpi-art"
```
Expected: build clean; the full suite passes (any failure in a file this slice did not touch: check `git stash`-free by re-running on the failing test alone and compare with a clean checkout before blaming this work).

- [ ] **Frontend:** `npm test`, `npx tsc --noEmit -p tsconfig.json` from `frontend`.

- [ ] **Manual check (optional, no commit):** start the backend (`https://localhost:65435`) and the frontend with `VITE_API_BASE_URL=https://localhost:65435`; open Reports > KPI / Performance > "Workflow Profiles"; as a SectionHead add a baseline version for one type (past date accepted), then try a second past-dated version for the same type (refused with the "cannot be in the past" message); sign the imported Drafts via History > "Review and sign"; confirm the old KPI cards below still load unchanged.

- [ ] **Hygiene:** `git status --short` must list no file of this slice, only unrelated work (if any) and nothing of this slice as uncommitted; `git log --stat -10` shows only the paths named in the tasks above.

---

## Notes for the human reviewer (deviations and limits)

1. **Type defaults honour `EffectiveFrom <= t` too.** Contract rule 2 omits the date condition for the type default; spec 2.2 and D4 (no retroactive change) include it, so the resolver applies it to both steps (Task 2).
2. **Legacy import skips retired-type definitions.** Contract 2.5 imports every `WorkloadWeights` row with a matching `TestDefinition`; this plan also requires a live `WorkflowType` (ints 0,1,4,5,6,7,8,9,12,13,14), because the service rejects retired types and such a Draft could never be signed (Task 6). Consequence: a test whose definition still has a retired workflow type can never be weighted until the definition is re-typed.
3. **Additive members beyond the contract:** `WorkflowProfileResolver.ResolveDefault` and `HasOverride` (overview needs them), `ProfileHistoryDrawer.onChanged?` (panel refresh), `WorkflowProfileModelTests.cs`, `workflowProfileService.test.ts`, `useWorkflowProfiles.test.tsx`, `ProfileHistoryDrawer.test.tsx` (extra test files), `profileErrorMessage` / `workflowTypeLabel` helpers.
4. **Permission plumbing and the controller share Task 5** because `EveryEnforcedCode_IsCheckedSomewhere` fails for an enforced code nobody references.
5. **`KpiView` catalog description** changes from "...and manage workload weights" to "View laboratory KPIs." (fresh databases only; existing rows keep their text).
6. **InMemory cannot show rollback.** The "wrong password leaves no row" and xmin-conflict rules are pinned in `WorkflowProfileServicePostgresTests` (needs `MICROLIMS_TEST_POSTGRES`); the unit tests pin the signature/audit half.
7. **Concurrent baselines** are not locked (see the `ponytail:` comment in `EnsureEffectiveFromAsync`); fine for one Section Head, add a partial unique index if it ever matters.
8. **Overview "last change"** reports the signer for a signed Draft and the creator otherwise; `CreatedByName` in version DTOs stays the creator ("System (legacy import)" for imports).
9a. **Spec 2.4 Draft type-default seed is intentionally not implemented.** `CK_WorkflowProfileVersions_AnyValue` forbids empty rows. Gaps are shown by `GetCoverageAsync` and the "Needs configuration" filter instead (contract deviation 10).
9b. **AddVersion inserts as Draft, then flips to Effective** in the same unit of work, because the append-only trigger rejects any UPDATE of an Effective row (attaching the signature is an update).
9c. **Shared dirty files:** `AnalystKpiTab.tsx` and `AnalystKpiService.ts` carry the uncommitted KPI page patch; stage only Workflow Profiles hunks (Task 10 Step 7).
9. **Down migration** drops the table (and its imported Draft rows) but leaves the `AuditLogs` rows that describe the import, like the other data migrations in this repo; the legacy tables are never touched.
