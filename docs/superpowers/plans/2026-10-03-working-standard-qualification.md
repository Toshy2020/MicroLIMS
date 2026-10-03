# Working Standard Qualification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the lab qualify an in-house working standard by HPLC assay against a primary reference standard, sign it off in three steps, and use the approved `WS-nn/MM/yyyy` lot wherever a primary RS lot is accepted.

**Architecture:** A WS lot is a `Material` row (`MaterialType.WorkingStandard`) linked to the primary standard's master entry, so the existing lot matching (`LotUsability`) accepts it everywhere with no new gate. A new `WorkingStandardQualification` record carries source, moisture, results and signatures. The candidate is assigned to a passed HPLC run as an `HplcRunSample` whose `TestOrderId` is null and `WorkingStandardQualificationId` is set; it has its own entry/submit endpoints in the workspace, and review/approval happen on the Working Standards page.

**Tech Stack:** ASP.NET Core 8, EF Core + PostgreSQL (Npgsql), xUnit (in-memory unit tests + `[PostgresFact]` integration tests), React + TypeScript + MUI.

**Spec:** `docs/superpowers/specs/2026-10-03-working-standard-qualification-design.md` (read it with this plan).

## Global Constraints

- Site rule: exactly **6 replicates**, **RSD ≤ 2.0 %** (compared after rounding to 2 decimals, midpoint away from zero), potency = mean.
- Potency on the **dried basis**: `potency = mean as-is assay × 100 / (100 − MC)`, stored on the lot as `Purity` (rounded to 3 decimals), MC stored as `MoisturePercent`.
- A potency above **100.000 %** fails the qualification ("Assigned potency is above 100 %.") — see Open decision OD1.
- Validity **12 months** from the approval date (lab-local date); due-soon window **30 days**.
- Lot code `WS-nn/MM/yyyy`; qualification code `WSQ-nn/MM/yyyy`; both continuous per calendar year (`SolutionPreparationCode.NextAsync`).
- Sign-off: submit (analyst, `ResultRecorded`) → review (`Samples.Review`, `Reviewed`) → approve (`Samples.Approve`, `Approved`); reviewer ≠ submitter; approver ≠ submitter and ≠ reviewer.
- A WS is never qualified against a WS: every SST standard lot of the run must be `MaterialType.ReferenceStandard`.
- No deduction from the source (a received RM sample is data, not stock).
- Requalification updates the **same** lot; no new code.
- Sign-first pattern: call `IElectronicSignatureService.SignAsync` before any mutation (it saves its own audit row on a failed password).
- No client-side potency/RSD math. Frontend files stay small (one component per file, < ~250 lines).
- All builds/artifacts/temp on E: — use `--artifacts-path E:/MicroLIMS/rls-tmp/<name>`; never C:.
- Local only. No push. Migration applied to LIMSV2 only after a backup to `E:/MicroLIMS/backups`.

## Spec amendments made by this plan

These resolve details the spec left open; Task 1 also writes them into the spec.
1. The run link lives only on `HplcRunSample.WorkingStandardQualificationId` (no `HplcRunSampleId` on the qualification — avoids a two-way FK). The "current" run sample = the `Assigned` one on a run that is not `Abandoned`.
2. WS entry uses its own workspace endpoints/page (`/hplc-workspace/:instrumentId/run/:runId/qualification/:runSampleId`) reusing `ReplicateEntryTable`; responses are entered only for the qualified analyte (the method analyte whose `StandardEntryId` = the qualification's master entry), so multi-analyte methods work.
3. The page lives at `/working-standards` in the labs menu next to Solution Preparation (the `/laboratory-configuration/*` area is gated by `MasterData.Manage`, which analysts and reviewers lack).
4. `Material.MaterialName` of the WS lot = master entry name (same as every master-linked lot); the source name stays on the qualification.
5. Attachments are `WorkingStandardDocument` rows (kinds `SourceReport`, `MoistureReport`); a new upload of the same kind supersedes the previous one.
6. Return (reviewer sends an `Assayed` qualification back to `Draft`) requires a reason, clears the computed results, and makes the replicates editable again even if the run is `Completed`.

## Open decision

- **OD1** — Dried-basis potency above 100 %: this plan fails the qualification. If the user prefers capping at 100.000 % or allowing it, change only `WorkingStandardCalculator.Evaluate` and its test in Task 2.

## Execution routing (user, 2026-10-03: "agy for easy, sonnet for advanced")

| Task | Executor | Why |
|---|---|---|
| 1 Domain/persistence/migration | Sonnet | migration review, nullable FK ripple |
| 2 Calculator | agy | small pure helper, tests given verbatim |
| 3 Materials Stock rules | agy | three guard lines + tests given |
| 4 Create/edit/documents | Sonnet | many rules, scoping |
| 5 Workspace qualification samples | Sonnet | touches the live product assay path |
| 6 Sign-off + lot create/extend | Sonnet | signatures, segregation of duties |
| 7 Controllers | agy | mechanical endpoint table |
| 8 Postgres tests | Sonnet | concurrency interceptor |
| 9, 10 Frontend | agy (with the ui-ux-pro-max skill) | screens |
| 11 Migration apply, suite, browser E2E | Opus (orchestrator) | backup + verification |

Opus reviews every diff, reruns build/tests, and commits. Tasks 2, 3 can run in parallel with Task 1's migration review only if files are disjoint (they are: Task 2/3 do not touch Task 1 files except `materialTypes.ts`, which Task 1 does not edit).

## Review Focus

1. A product sample and a WS candidate on the same run: completing the run must count both kinds of pending rows; product entry must refuse a WS run sample (no null-reference on `TestOrderId`) — tests in Task 5.
2. A reviewer who also submitted the qualification tries to review it — refused (Task 6).
3. Two approvals at the same moment take the same `WS-nn` code — the second gets a clear "sign again" error, nothing half-written (Task 8 Postgres).
4. A qualification run whose SST used a WS lot as the standard — assignment refused (Task 5).
5. A requalification approved after the lot already expired — the same lot becomes usable again with the new expiry; no second lot (Task 6).

---

### Task 1: Domain, persistence, permission, migration

**Files:**
- Modify: `backend/MicroLIMS.Domain/Enums/MaterialType.cs` (append `WorkingStandard` = 12)
- Create: `backend/MicroLIMS.Domain/Enums/WorkingStandardQualificationStatus.cs`
- Create: `backend/MicroLIMS.Domain/Enums/WorkingStandardQualificationKind.cs`
- Create: `backend/MicroLIMS.Domain/Enums/WorkingStandardDocumentKind.cs`
- Create: `backend/MicroLIMS.Domain/Entities/WorkingStandardQualification.cs`
- Modify: `backend/MicroLIMS.Domain/Entities/HplcRun.cs` (`HplcRunSample`)
- Create: `backend/MicroLIMS.Persistence/Configurations/WorkingStandardQualificationConfiguration.cs`
- Modify: `backend/MicroLIMS.Persistence/Configurations/HplcRunSampleConfiguration.cs`
- Modify: `backend/MicroLIMS.Persistence/Configurations/MaterialConfiguration.cs`
- Modify: `backend/MicroLIMS.Application/Abstractions/Persistence/IMicroLimsDbContext.cs`, `backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs`
- Modify: `backend/MicroLIMS.Application/Abstractions/Persistence/UniqueIndexNames.cs`
- Modify: `backend/MicroLIMS.Shared/Constants/PermissionConstants.cs`, `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs`
- Modify: `frontend/src/routes/routes.ts` (permission key)
- Create: migration `AddWorkingStandards` (generated) + hand-added permission SQL
- Modify: `backend/MicroLIMS.Tests/IntegrationTests/PermissionEnforcementMigrationPostgresTests.cs`
- Modify (compile fixes for nullable `TestOrderId`): `backend/MicroLIMS.Application/Services/HplcRunService.Samples.cs`, `HplcRunService.cs`, `HplcSampleEntryContext.cs`
- Modify: the spec (amendments above)

**Interfaces:**
- Produces: `MaterialType.WorkingStandard`; `WorkingStandardQualification`, `WorkingStandardDocument`; enums below; `HplcRunSample.TestOrderId` is `int?`, `HplcRunSample.WorkingStandardQualificationId` `int?`; `IMicroLimsDbContext.WorkingStandardQualifications`, `.WorkingStandardDocuments`; `UniqueIndexNames.WorkingStandardCode = "IX_Materials_WorkingStandardCode"`, `UniqueIndexNames.WorkingStandardQualificationCode = "IX_WorkingStandardQualifications_Code"`; `PermissionConstants.WorkingStandardsQualify = "WorkingStandards.Qualify"`.

- [ ] **Step 1: Enums**

```csharp
// WorkingStandardQualificationStatus.cs
namespace MicroLIMS.Domain.Enums;

// Working standard qualification (spec 2026-10-03, section 5).
public enum WorkingStandardQualificationStatus
{
    Draft,
    Assayed,
    Reviewed,
    Approved,
    Rejected
}
```

```csharp
// WorkingStandardQualificationKind.cs
namespace MicroLIMS.Domain.Enums;

public enum WorkingStandardQualificationKind
{
    Initial,
    Requalification
}
```

```csharp
// WorkingStandardDocumentKind.cs
namespace MicroLIMS.Domain.Enums;

public enum WorkingStandardDocumentKind
{
    SourceReport,   // first test report of a manually entered raw material
    MoistureReport
}
```

Append to `MaterialType` (after `ReferenceStandard`, keep the order — values are stored as integers):

```csharp
    ReferenceStandard,
    // In-house secondary standard, created only by approving a working
    // standard qualification (never received through Materials Stock).
    WorkingStandard
```

- [ ] **Step 2: Entities**

```csharp
// WorkingStandardQualification.cs
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.Domain.Entities;

// One qualification or requalification of an in-house working standard
// (spec 2026-10-03). The assay runs on an HPLC run through an HplcRunSample
// whose WorkingStandardQualificationId points here. Approval creates the
// WorkingStandard Material lot (Initial) or updates it (Requalification).
// Audited by MicroLimsDbContext.SaveChanges like every entity.
public class WorkingStandardQualification : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public int SectionId { get; set; }
    public DocumentSection? Section { get; set; }
    public string Code { get; set; } = string.Empty;
    public WorkingStandardQualificationKind Kind { get; set; }
    public WorkingStandardQualificationStatus Status { get; set; }

    // Initial: null until approval. Requalification: the lot being requalified.
    public int? WorkingStandardMaterialId { get; set; }
    public Material? WorkingStandardMaterial { get; set; }

    public int MaterialMasterEntryId { get; set; } // ReferenceStandard category
    public MaterialMasterEntry? MaterialMasterEntry { get; set; }

    public int? SourceSampleId { get; set; } // received RawMaterial sample with an approved assay
    public Sample? SourceSample { get; set; }
    public string SourceMaterialName { get; set; } = string.Empty;
    public string SourceBatchNumber { get; set; } = string.Empty;

    public decimal? QuantityGrams { get; set; } // Initial only
    public string? Location { get; set; }       // Initial only
    public decimal? MoisturePercent { get; set; }

    // Set at assignment: the method analyte whose standard entry matches.
    public int? HplcMethodAnalyteId { get; set; }

    public decimal? MeanAssayPercent { get; set; }
    public decimal? RsdPercent { get; set; }
    public decimal? PotencyPercent { get; set; }
    public bool Passed { get; set; }
    public string? FailureReasons { get; set; }
    public string? ReplicateAssaysJson { get; set; } // per-replicate as-is assay %, for the record

    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }

    public int? PreparedByUserId { get; set; }
    public DateTime? PreparedAt { get; set; }
    public int? PreparedSignatureId { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedSignatureId { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int? ApprovedSignatureId { get; set; }
    public int? RejectedByUserId { get; set; }
    public DateTime? RejectedAt { get; set; }
    public int? RejectedSignatureId { get; set; }
    public string? RejectReason { get; set; }
    public string? ReturnReason { get; set; } // last return to Draft

    public List<WorkingStandardDocument> Documents { get; set; } = new();

    public bool IsOpen => Status is WorkingStandardQualificationStatus.Draft
        or WorkingStandardQualificationStatus.Assayed or WorkingStandardQualificationStatus.Reviewed;
}

// Attachment on a qualification. Never replaced in place: a new upload of the
// same kind sets SupersededByDocumentId on the previous current one.
public class WorkingStandardDocument
{
    public int Id { get; set; }
    public int WorkingStandardQualificationId { get; set; }
    public WorkingStandardQualification? WorkingStandardQualification { get; set; }
    public WorkingStandardDocumentKind Kind { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }
    public int? SupersededByDocumentId { get; set; }
}
```

In `HplcRun.cs`, `HplcRunSample`:

```csharp
// A sample assigned to a run after its SST passed: either a test order or a
// working standard qualification (exactly one - CK_HplcRunSamples_OneSubject).
// Removed = Status Removed + reason, never deleted (Global Constraints).
public class HplcRunSample
{
    public int Id { get; set; }
    public int HplcRunId { get; set; }
    public HplcRun? HplcRun { get; set; }
    public int? TestOrderId { get; set; }
    public TestOrder? TestOrder { get; set; }
    public int? WorkingStandardQualificationId { get; set; }
    public WorkingStandardQualification? WorkingStandardQualification { get; set; }
    // ... rest unchanged
```

- [ ] **Step 3: EF configuration**

```csharp
// WorkingStandardQualificationConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Persistence.Configurations;

public class WorkingStandardQualificationConfiguration : IEntityTypeConfiguration<WorkingStandardQualification>
{
    public void Configure(EntityTypeBuilder<WorkingStandardQualification> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
        builder.Property(e => e.SourceMaterialName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.SourceBatchNumber).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Location).HasMaxLength(200);
        builder.Property(e => e.QuantityGrams).HasPrecision(12, 4);
        builder.Property(e => e.MoisturePercent).HasPrecision(6, 3);
        builder.Property(e => e.MeanAssayPercent).HasPrecision(9, 4);
        builder.Property(e => e.RsdPercent).HasPrecision(9, 4);
        builder.Property(e => e.PotencyPercent).HasPrecision(6, 3);
        builder.Property(e => e.FailureReasons).HasMaxLength(1000);
        builder.Property(e => e.ReplicateAssaysJson).HasColumnType("jsonb");
        builder.Property(e => e.RejectReason).HasMaxLength(500);
        builder.Property(e => e.ReturnReason).HasMaxLength(500);
        builder.Ignore(e => e.IsOpen);

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("IX_WorkingStandardQualifications_Code");
        builder.HasIndex(e => new { e.WorkingStandardMaterialId, e.Status });

        builder.HasOne(e => e.Section).WithMany().HasForeignKey(e => e.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.WorkingStandardMaterial).WithMany().HasForeignKey(e => e.WorkingStandardMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.MaterialMasterEntry).WithMany().HasForeignKey(e => e.MaterialMasterEntryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.SourceSample).WithMany().HasForeignKey(e => e.SourceSampleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<HplcMethodAnalyte>().WithMany().HasForeignKey(e => e.HplcMethodAnalyteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ElectronicSignature>().WithMany().HasForeignKey(e => e.PreparedSignatureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ElectronicSignature>().WithMany().HasForeignKey(e => e.ReviewedSignatureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ElectronicSignature>().WithMany().HasForeignKey(e => e.ApprovedSignatureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ElectronicSignature>().WithMany().HasForeignKey(e => e.RejectedSignatureId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Documents).WithOne(d => d.WorkingStandardQualification)
            .HasForeignKey(d => d.WorkingStandardQualificationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkingStandardDocumentConfiguration : IEntityTypeConfiguration<WorkingStandardDocument>
{
    public void Configure(EntityTypeBuilder<WorkingStandardDocument> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FileName).IsRequired().HasMaxLength(255);
        builder.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
    }
}
```

`HplcRunSampleConfiguration.Configure` — add after the TestOrder relationship:

```csharp
        builder.HasIndex(e => e.WorkingStandardQualificationId);
        builder.HasOne(e => e.WorkingStandardQualification).WithMany()
            .HasForeignKey(e => e.WorkingStandardQualificationId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("CK_HplcRunSamples_OneSubject",
            "(\"TestOrderId\" IS NULL) <> (\"WorkingStandardQualificationId\" IS NULL)"));
```

`MaterialConfiguration.Configure` — add (12 = `MaterialType.WorkingStandard`):

```csharp
        // WS-nn/MM/yyyy codes are generated (WorkingStandardService.ApproveAsync);
        // other types reuse master-entry codes, so the uniqueness is WS-only.
        builder.HasIndex(m => m.Code, "IX_Materials_WorkingStandardCode").IsUnique().HasFilter("\"MaterialType\" = 12");
```

`UniqueIndexNames`:

```csharp
    // Like SolutionPreparationCode: a clash is reported, not retried
    // (WorkingStandardService asks the user to sign again).
    public const string WorkingStandardCode = "IX_Materials_WorkingStandardCode";
    public const string WorkingStandardQualificationCode = "IX_WorkingStandardQualifications_Code";
```

DbSets — `IMicroLimsDbContext` and `MicroLimsDbContext` next to `HplcRunSamples`:

```csharp
    DbSet<WorkingStandardQualification> WorkingStandardQualifications { get; }
    DbSet<WorkingStandardDocument> WorkingStandardDocuments { get; }
```
```csharp
    public DbSet<WorkingStandardQualification> WorkingStandardQualifications => Set<WorkingStandardQualification>();
    public DbSet<WorkingStandardDocument> WorkingStandardDocuments => Set<WorkingStandardDocument>();
```

- [ ] **Step 4: Permission**

`PermissionConstants`: add `public const string WorkingStandardsQualify = "WorkingStandards.Qualify";` next to `SolutionsPrepare`, and add it to the `All` and `Enforced` collections exactly the way `SolutionsPrepare` is listed there.
`DbSeeder`: add the description row `(PermissionConstants.WorkingStandardsQualify, "Create and prepare working standard qualifications, attach documents, enter and submit qualification replicates.")` after the `HplcOperate` row, and add `PermissionConstants.WorkingStandardsQualify` to every role list that contains `PermissionConstants.SolutionsPrepare` (SectionHead and Analyst; SystemAdministrator gets `All`).
`frontend/src/routes/routes.ts`: add `WORKING_STANDARDS_QUALIFY: "WorkingStandards.Qualify",` after `SOLUTIONS_PREPARE`.
`PermissionEnforcementMigrationPostgresTests.AddedByLaterMigrations`: add `PermissionConstants.WorkingStandardsQualify`.

- [ ] **Step 5: Compile fixes for nullable `TestOrderId`**

`HplcRunService.cs` record: `public record HplcRunSampleSummaryDto(int Id, int? TestOrderId, HplcRunSampleStatus Status, string SampleNumber, string? BatchNumber, string? ProductName, string TestCode, bool Submitted, int? WorkingStandardQualificationId = null);`

`HplcRunService.Samples.cs` `EligibleOrdersQuery` busy set:

```csharp
        var busy = _db.HplcRunSamples
            .Where(s => s.TestOrderId != null && s.Status == HplcRunSampleStatus.Assigned && s.HplcRun!.Status != HplcRunStatus.Abandoned)
            .Select(s => s.TestOrderId!.Value);
```

`HplcSampleEntryContext.LoadAsync`, right after loading `runSample`:

```csharp
        if (runSample.TestOrderId is not int testOrderId)
            throw new InvalidOperationException("This run sample is a working standard qualification - open it from its qualification entry.");
```
and change the order lookup to `.FirstAsync(o => o.Id == testOrderId, ct)`.

`CompleteRunAsync`, `RemoveSampleAsync` and `BuildSampleSummariesAsync` are rewritten in Task 5; for now make them compile:
- `CompleteRunAsync`: `var orderIds = assigned.Where(s => s.TestOrderId != null).Select(s => s.TestOrderId!.Value).ToList();` and `submittedOrderIds.Contains(s.TestOrderId ?? 0)`.
- `BuildSampleSummariesAsync`: `run.Samples.Where(s => s.TestOrderId != null).Select(s => s.TestOrderId!.Value)` for `orderIds`; `orders.TryGetValue(s.TestOrderId ?? 0, out var o)`; `submitted.Contains(s.TestOrderId ?? 0)`.
- `RemoveSampleAsync`: `a.TestOrderId == sample.TestOrderId` compiles as is.
Then run `dotnet build backend/MicroLIMS.API --artifacts-path E:/MicroLIMS/rls-tmp/ws-build` and fix any remaining `int?`→`int` errors the same way (`?? 0` in lookups, `!.Value` after a `!= null` filter).

- [ ] **Step 6: Migration**

Run (from `backend/`):
```
dotnet ef migrations add AddWorkingStandards --project MicroLIMS.Persistence --startup-project MicroLIMS.API --configuration Release
```
Open the generated migration and check it: `TestOrderId` altered to nullable; `WorkingStandardQualificationId` column + index + FK; check constraint `CK_HplcRunSamples_OneSubject`; tables `WorkingStandardQualifications`, `WorkingStandardDocuments`; index `IX_Materials_WorkingStandardCode` with the filter. Then append at the end of `Up` (pattern from `20260929173324_AddSolutionPreparation.cs`):

```csharp
            // DbSeeder seeds WorkingStandards.Qualify on a fresh database; these
            // idempotent INSERTs cover existing databases. RoleType: 0 =
            // SystemAdministrator, 1 = SectionHead, 3 = Analyst - the roles that
            // hold Solutions.Prepare.
            migrationBuilder.Sql(@"
                INSERT INTO ""Permissions"" (""Code"", ""Description"", ""IsEnforced"")
                SELECT 'WorkingStandards.Qualify', 'Create and prepare working standard qualifications, attach documents, enter and submit qualification replicates.', TRUE
                WHERE NOT EXISTS (SELECT 1 FROM ""Permissions"" WHERE ""Code"" = 'WorkingStandards.Qualify');");
            migrationBuilder.Sql(@"
                INSERT INTO ""RolePermissions"" (""RoleId"", ""PermissionId"")
                SELECT r.""Id"", p.""Id""
                FROM ""Roles"" r CROSS JOIN ""Permissions"" p
                WHERE r.""Type"" IN (0, 1, 3)
                  AND p.""Code"" = 'WorkingStandards.Qualify'
                  AND NOT EXISTS (SELECT 1 FROM ""RolePermissions"" rp
                                  WHERE rp.""RoleId"" = r.""Id"" AND rp.""PermissionId"" = p.""Id"");");
```
and at the start of `Down`:
```csharp
            migrationBuilder.Sql(@"DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN
                (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" = 'WorkingStandards.Qualify');");
            migrationBuilder.Sql(@"DELETE FROM ""Permissions"" WHERE ""Code"" = 'WorkingStandards.Qualify';");
```
Note: `Down` re-making `TestOrderId` NOT NULL fails if WS rows exist; add before the `AlterColumn` in `Down`:
```csharp
            migrationBuilder.Sql(@"DELETE FROM ""HplcRunSamples"" WHERE ""TestOrderId"" IS NULL;");
```

- [ ] **Step 7: Build and run the whole suite**

```
dotnet build backend/MicroLIMS.API --artifacts-path E:/MicroLIMS/rls-tmp/ws-build
dotnet build backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/ws-build
bash .claude/scripts/run-postgres-tests.sh E:/MicroLIMS/rls-tmp/ws-build
```
Expected: build clean; all tests pass (baseline count unchanged + 0 failures). `PermissionEnforcementMigrationPostgresTests` must pass with the new entry.

- [ ] **Step 8: Write the spec amendments** (section "Spec amendments made by this plan" above) into the spec under a new heading `## 10. Amendments (plan 2026-10-03)`.

- [ ] **Step 9: Commit**

```bash
git add backend frontend/src/routes/routes.ts docs/superpowers/specs/2026-10-03-working-standard-qualification-design.md
git commit -m "feat(ws): working standard entities, run sample subject, permission, migration"
```

---

### Task 2: Qualification calculator

**Files:**
- Create: `backend/MicroLIMS.Application/Helpers/WorkingStandardCalculator.cs`
- Test: `backend/MicroLIMS.Tests/UnitTests/WorkingStandardCalculatorTests.cs`

**Interfaces:**
- Consumes: `SystemSuitabilityService.CalculateStandardRsd(IReadOnlyList<decimal>?)` (sample SD, n−1).
- Produces:
```csharp
public static class WorkingStandardRules { Replicates = 6; MaxRsdPercent = 2.0m; ValidityMonths = 12; DueSoonDays = 30; }
public record WorkingStandardResult(IReadOnlyList<decimal> ReplicateAssayPercents, decimal MeanAssayPercent, decimal? RsdPercent, decimal PotencyPercent, bool Passed, string? FailureReasons);
public static class WorkingStandardCalculator {
    public static bool PassesRsd(decimal rsdPercent);
    public static decimal DriedBasisPotency(decimal meanAssayPercent, decimal moisturePercent);
    public static WorkingStandardResult Evaluate(IReadOnlyList<decimal> replicateAssayPercents, decimal moisturePercent);
}
```

- [ ] **Step 1: Write the failing tests**

```csharp
using MicroLIMS.Application.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class WorkingStandardCalculatorTests
{
    // Hand-checked: mean 99.0; deviations ±0.5/0 -> sum sq 1.0, var 0.2, SD 0.44721,
    // RSD 0.4517 %; potency = 99.0 x 100 / 99.5 = 99.4975 -> 99.497.
    [Fact]
    public void Evaluate_SixGoodReplicates_PassesWithDriedBasisPotency()
    {
        var r = WorkingStandardCalculator.Evaluate(new[] { 98.5m, 99.0m, 99.5m, 98.5m, 99.0m, 99.5m }, 0.5m);

        Assert.True(r.Passed);
        Assert.Null(r.FailureReasons);
        Assert.Equal(99.0m, r.MeanAssayPercent);
        Assert.Equal(0.45m, Math.Round(r.RsdPercent!.Value, 2));
        Assert.Equal(99.497m, r.PotencyPercent);
    }

    // Hand-checked: mean 100, deviations ±3 -> var 54/5 = 10.8, SD 3.2863, RSD 3.29 %.
    [Fact]
    public void Evaluate_HighRsd_Fails()
    {
        var r = WorkingStandardCalculator.Evaluate(new[] { 97m, 103m, 97m, 103m, 97m, 103m }, 0m);

        Assert.False(r.Passed);
        Assert.Contains("RSD 3.29 % is above 2.0 %", r.FailureReasons);
    }

    [Fact]
    public void Evaluate_FiveReplicates_Fails()
    {
        var r = WorkingStandardCalculator.Evaluate(new[] { 99m, 99m, 99m, 99m, 99m }, 0m);

        Assert.False(r.Passed);
        Assert.Contains("Exactly 6 replicates are required (5 entered).", r.FailureReasons);
    }

    // OD1: dried-basis potency above 100 % fails. 99.8 x 100 / 99.5 = 100.3015.
    [Fact]
    public void Evaluate_PotencyAbove100_Fails()
    {
        var r = WorkingStandardCalculator.Evaluate(new[] { 99.8m, 99.8m, 99.8m, 99.8m, 99.8m, 99.8m }, 0.5m);

        Assert.False(r.Passed);
        Assert.Equal(100.302m, r.PotencyPercent);
        Assert.Contains("Assigned potency is above 100 %.", r.FailureReasons);
    }

    [Theory]
    [InlineData("2.0", true)]
    [InlineData("2.004", true)]
    [InlineData("2.005", false)]
    [InlineData("2.1", false)]
    public void PassesRsd_RoundsToTwoDecimalsAwayFromZero(string rsd, bool expected) =>
        Assert.Equal(expected, WorkingStandardCalculator.PassesRsd(decimal.Parse(rsd, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("-0.1")]
    [InlineData("100")]
    public void DriedBasisPotency_MoistureOutOfRange_Throws(string mc) =>
        Assert.Throws<InvalidOperationException>(() =>
            WorkingStandardCalculator.DriedBasisPotency(99m, decimal.Parse(mc, System.Globalization.CultureInfo.InvariantCulture)));
}
```

- [ ] **Step 2: Run to verify they fail**

`dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/ws-build --filter WorkingStandardCalculatorTests` → FAIL (type not found).

- [ ] **Step 3: Implement**

```csharp
using MicroLIMS.Application.Services;

namespace MicroLIMS.Application.Helpers;

// Fixed site rule for working standard qualification (spec 2026-10-03, D2/D6).
public static class WorkingStandardRules
{
    public const int Replicates = 6;
    public const decimal MaxRsdPercent = 2.0m;
    public const int ValidityMonths = 12;
    public const int DueSoonDays = 30;
}

public record WorkingStandardResult(
    IReadOnlyList<decimal> ReplicateAssayPercents, decimal MeanAssayPercent, decimal? RsdPercent,
    decimal PotencyPercent, bool Passed, string? FailureReasons);

// Pure: per-replicate as-is assay % (from HplcAssayCalculator) -> mean, RSD,
// dried-basis potency (D5) and pass/fail.
public static class WorkingStandardCalculator
{
    public static bool PassesRsd(decimal rsdPercent) =>
        Math.Round(rsdPercent, 2, MidpointRounding.AwayFromZero) <= WorkingStandardRules.MaxRsdPercent;

    public static decimal DriedBasisPotency(decimal meanAssayPercent, decimal moisturePercent)
    {
        if (moisturePercent < 0m || moisturePercent >= 100m)
            throw new InvalidOperationException("Moisture content must be at least 0 and below 100.");
        return Math.Round(meanAssayPercent * 100m / (100m - moisturePercent), 3, MidpointRounding.AwayFromZero);
    }

    public static WorkingStandardResult Evaluate(IReadOnlyList<decimal> replicateAssayPercents, decimal moisturePercent)
    {
        ArgumentNullException.ThrowIfNull(replicateAssayPercents);
        if (replicateAssayPercents.Count == 0)
            throw new InvalidOperationException("At least one replicate is required.");

        var mean = replicateAssayPercents.Average();
        var rsd = SystemSuitabilityService.CalculateStandardRsd(replicateAssayPercents);
        var potency = DriedBasisPotency(mean, moisturePercent);

        var reasons = new List<string>();
        if (replicateAssayPercents.Count != WorkingStandardRules.Replicates)
            reasons.Add($"Exactly {WorkingStandardRules.Replicates} replicates are required ({replicateAssayPercents.Count} entered).");
        if (rsd.HasValue && !PassesRsd(rsd.Value))
            reasons.Add($"RSD {Math.Round(rsd.Value, 2, MidpointRounding.AwayFromZero):0.00} % is above {WorkingStandardRules.MaxRsdPercent:0.0} %.");
        if (potency > 100m)
            reasons.Add("Assigned potency is above 100 %.");

        return new WorkingStandardResult(replicateAssayPercents.ToList(), mean, rsd, potency,
            reasons.Count == 0, reasons.Count == 0 ? null : string.Join(" ", reasons));
    }
}
```

- [ ] **Step 4: Run to verify they pass** — same command → PASS.

- [ ] **Step 5: Commit** — `git add` both files; `git commit -m "feat(ws): qualification calculator (6 reps, RSD <= 2.0 %, dried-basis potency)"`

---

### Task 3: Materials Stock rules for WorkingStandard

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/MaterialService.cs`
- Modify: `frontend/src/modules/inventory/materials/types/materialTypes.ts`
- Test: `backend/MicroLIMS.Tests/UnitTests/MaterialServiceWorkingStandardTests.cs`

**Interfaces:**
- Produces: `MaterialService.GetUsableReferenceStandardsAsync` returns `ReferenceStandard` **and** `WorkingStandard` lots; Create/Update refuse WorkingStandard.

- [ ] **Step 1: Failing tests**

```csharp
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class MaterialServiceWorkingStandardTests
{
    private static MicroLimsDbContext NewDb()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CurrentUserId = 1;
        return db;
    }

    private static Material Lot(int sectionId, MaterialType type, string batch) => new()
    {
        SectionId = sectionId, MaterialType = type, MaterialName = "Paracetamol", ManufacturerName = "x",
        BatchNumber = batch, ReceivingDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddMonths(6),
        Location = "S1", QuantityReceived = 5m, QuantityRemaining = 5m, Unit = MaterialUnit.Gram,
        Purity = 99.5m, MoisturePercent = 0.2m, CreatedByUserId = 1, LastModifiedByUserId = 1,
    };

    [Fact]
    public async Task Create_WorkingStandard_Refused()
    {
        await using var db = NewDb();
        var section = TestServiceFactory.EnsureMicroSection(db);
        var service = TestServiceFactory.Material(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new SaveMaterialRequest(
            MaterialType.WorkingStandard, "P", "M", "B1", DateTime.UtcNow, null, null, "S1", 1m, MaterialUnit.Gram,
            null, null, null, SectionId: section.Id), 1));
        Assert.Equal("Working standards are created by approving a qualification, not received here.", ex.Message);
    }

    [Fact]
    public async Task Update_WorkingStandardLot_Refused()
    {
        await using var db = NewDb();
        var section = TestServiceFactory.EnsureMicroSection(db);
        var lot = Lot(section.Id, MaterialType.WorkingStandard, "WS1");
        db.Materials.Add(lot);
        await db.SaveChangesAsync();
        var service = TestServiceFactory.Material(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(lot.Id, new SaveMaterialRequest(
            MaterialType.WorkingStandard, "P", "M", "WS1", DateTime.UtcNow, null, null, "S1", 5m, MaterialUnit.Gram,
            null, null, null), 1));
        Assert.Equal("Working standards change only through their qualifications.", ex.Message);
    }

    [Fact]
    public async Task UsableReferenceStandards_IncludesWorkingStandards()
    {
        await using var db = NewDb();
        var section = TestServiceFactory.EnsureMicroSection(db);
        db.Materials.AddRange(Lot(section.Id, MaterialType.ReferenceStandard, "RS1"), Lot(section.Id, MaterialType.WorkingStandard, "WS1"),
            Lot(section.Id, MaterialType.Chemical, "CH1"));
        await db.SaveChangesAsync();

        var lots = await TestServiceFactory.Material(db).GetUsableReferenceStandardsAsync(1);

        Assert.Equal(new[] { "RS1", "WS1" }, lots.Select(l => l.BatchNumber).OrderBy(b => b));
    }
}
```
If `TestServiceFactory.Material` does not exist, add to `TestServiceFactory.cs`:
```csharp
    public static MaterialService Material(MicroLimsDbContext db, IUserSectionScopeService? scope = null) =>
        new(db, scope ?? new UserSectionScopeService(db));
```
(The in-memory test user 1 may have no section membership; if `GetAccessibleSectionIdsAsync(1)` filters everything out, pass a scope stub returning `null` — copy the stub pattern used in existing `MaterialService` tests: `git grep -l "MaterialService(" backend/MicroLIMS.Tests`.)

- [ ] **Step 2: Run** `--filter MaterialServiceWorkingStandardTests` → FAIL.

- [ ] **Step 3: Implement in `MaterialService`**

At the top of `CreateAsync`:
```csharp
        if (r.MaterialType == MaterialType.WorkingStandard)
            throw new InvalidOperationException("Working standards are created by approving a qualification, not received here.");
```
In `UpdateAsync`, right after `RecordVersion.EnsureCurrent(_db, entity);`:
```csharp
        if (entity.MaterialType == MaterialType.WorkingStandard || r.MaterialType == MaterialType.WorkingStandard)
            throw new InvalidOperationException("Working standards change only through their qualifications.");
```
`DefaultUnitFor`: add `MaterialType.WorkingStandard => MaterialUnit.Gram,`.
`GetUsableReferenceStandardsAsync`: replace the type filter with
```csharp
        query = query.Where(m => m.MaterialType == MaterialType.ReferenceStandard || m.MaterialType == MaterialType.WorkingStandard);
```
and update its comment: `// SST picker: usable primary and working standards (in stock, not expired) in the caller's sections.`
Also find any other `ConsumeAsync`/type allow-list that rejects unknown types for solution preparation: `git grep -n "MaterialType\." backend/MicroLIMS.Application/Services/SolutionPreparationService.cs` — expected none (it matches by master entry); if one exists, add `WorkingStandard` beside `ReferenceStandard`.

Frontend `materialTypes.ts`: add `| "WorkingStandard"` after `"ReferenceStandard"`; if there is a label map in that file or `git grep -n "ReferenceStandard:" frontend/src`, add `WorkingStandard: "Working Standard"` beside it. Do **not** add it to any receive-form type list.

- [ ] **Step 4: Run tests** → PASS; `cd frontend && npx tsc --noEmit` → clean.

- [ ] **Step 5: Commit** — `git commit -m "feat(ws): materials stock accepts working standards in standard pickers only"`

---

### Task 4: WorkingStandardService — create, edit, documents, lists

**Files:**
- Create: `backend/MicroLIMS.Application/DTOs/Responses/WorkingStandardDtos.cs`
- Create: `backend/MicroLIMS.Application/Services/WorkingStandardService.cs`
- Modify: DI registration (`git grep -n "AddScoped<HplcRunService>" backend` — register `WorkingStandardService` next to it)
- Modify: `backend/MicroLIMS.Tests/TestServiceFactory.cs`
- Test: `backend/MicroLIMS.Tests/UnitTests/WorkingStandardServiceTests.cs`

**Interfaces:**
- Consumes: Task 1 entities; `IUserSectionScopeService`; `IFileStorageService`; `ILabClock`; `IElectronicSignatureService`; `SolutionPreparationCode.NextAsync(IQueryable<string>, string head, DateTime labLocal, CancellationToken)`.
- Produces (records in `WorkingStandardDtos.cs`, namespace `MicroLIMS.Application.DTOs.Responses`):
```csharp
public record CreateQualificationRequest(
    WorkingStandardQualificationKind Kind, int? WorkingStandardMaterialId, int? MaterialMasterEntryId,
    int? SourceSampleId, string? SourceMaterialName, string? SourceBatchNumber,
    decimal? QuantityGrams, string? Location, decimal? MoisturePercent);
public record UpdateQualificationRequest(decimal? QuantityGrams, string? Location, decimal? MoisturePercent);
public record WorkingStandardSignRequest(string Password, string? Comment);
public record WorkingStandardReasonRequest(string Password, string Reason);
public record WorkingStandardReturnRequest(string Reason);
public record EligibleSourceSampleDto(int SampleId, string ReferenceNumber, string MaterialName, string? BatchNumber, string TestCode);
public record WorkingStandardDocumentDto(int Id, WorkingStandardDocumentKind Kind, string FileName, string ContentType, DateTime UploadedAt, string? UploadedByUserName, bool IsCurrent);
public record WorkingStandardRunLinkDto(int HplcRunId, int EquipmentId, string RunCode, int RunSampleId, HplcRunSampleStatus Status);
public record WorkingStandardQualificationDto(
    int Id, uint Version, string Code, WorkingStandardQualificationKind Kind, WorkingStandardQualificationStatus Status,
    int SectionId, int? WorkingStandardMaterialId, string? WorkingStandardCode,
    int MaterialMasterEntryId, string MaterialMasterCode, string MaterialMasterName,
    int? SourceSampleId, string? SourceSampleReference, string SourceMaterialName, string SourceBatchNumber,
    decimal? QuantityGrams, string? Location, decimal? MoisturePercent,
    List<decimal>? ReplicateAssayPercents, decimal? MeanAssayPercent, decimal? RsdPercent, decimal? PotencyPercent,
    bool Passed, string? FailureReasons,
    string? CreatedByUserName, DateTime CreatedAt,
    string? PreparedByUserName, DateTime? PreparedAt, string? ReviewedByUserName, DateTime? ReviewedAt,
    string? ApprovedByUserName, DateTime? ApprovedAt, string? RejectedByUserName, DateTime? RejectedAt,
    string? RejectReason, string? ReturnReason,
    WorkingStandardRunLinkDto? Run, List<WorkingStandardDocumentDto> Documents, List<int> RunEvidenceIds);
public record WorkingStandardLotDto(
    int MaterialId, string Code, string MaterialName, string BatchNumber, string MasterEntryCode,
    decimal? PotencyPercent, decimal? MoisturePercent, decimal QuantityRemaining, DateTime? ExpiryDate,
    string Status, int? OpenQualificationId);   // Status: "Valid" | "DueSoon" | "Expired" | "Depleted"
```
- Service methods (all take `int userId, CancellationToken ct = default` last):
```csharp
Task<List<WorkingStandardLotDto>> GetLotsAsync(int userId, ...);
Task<List<WorkingStandardQualificationDto>> GetQualificationsAsync(int userId, ...);
Task<WorkingStandardQualificationDto> GetAsync(int id, int userId, ...);
Task<List<EligibleSourceSampleDto>> GetEligibleSourceSamplesAsync(string? search, int userId, ...);
Task<WorkingStandardQualificationDto> CreateAsync(CreateQualificationRequest r, int userId, ...);
Task<WorkingStandardQualificationDto> UpdateDraftAsync(int id, UpdateQualificationRequest r, int userId, ...);
Task<WorkingStandardDocumentDto> UploadDocumentAsync(int id, WorkingStandardDocumentKind kind, string fileName, string contentType, byte[] content, int userId, ...);
Task<(byte[] Content, string ContentType, string FileName)> DownloadDocumentAsync(int documentId, int userId, ...);
internal static string? AssignProblem(WorkingStandardQualification q); // used by HplcRunService (Task 5)
```

Rules (each one gets a test below):
- Create Initial: `MaterialMasterEntryId` required, entry exists, `Category == ReferenceStandard`, active, entry section accessible to the user; exactly one source: `SourceSampleId` **or** (`SourceMaterialName` + `SourceBatchNumber`, both non-blank) — both or neither → "Choose a received raw material sample or enter the material name and batch, not both." / "...either...". Sample source must be in `GetEligibleSourceSamplesAsync` (else "That sample has no approved assay result."), and name/batch are copied (`Sample.Item.Name`, `Sample.BatchNumber`). `QuantityGrams > 0` ("Enter the quantity in grams."), `Location` non-blank ("Enter the storage location."). Moisture optional at create; if given, `0 ≤ x < 100`.
- Create Requalification: `WorkingStandardMaterialId` required, lot exists, `MaterialType == WorkingStandard`, accessible; no open qualification for that lot ("Lot WS-01/10/2026 already has an open qualification (WSQ-..)."); entry = lot's entry; source name/batch = lot's `MaterialName`/`BatchNumber`; quantity/location stay null.
- Code: `WSQ-nn/MM/yyyy` via `SolutionPreparationCode.NextAsync(_db.WorkingStandardQualifications.Select(q => q.Code), "WSQ-", _clock.ToLabLocal(nowUtc), ct)`; save with `TrySaveChangesAsync(UniqueIndexNames.WorkingStandardQualificationCode)`; on false regenerate once and retry, then throw "Could not allocate a qualification code - try again.".
- UpdateDraft: only `Draft` ("Only a draft qualification can be edited."); not while assigned to a non-abandoned run with replicates already saved? — keep simple: allowed in Draft. Quantity/location only for Initial (ignored with an error for Requalification: "Quantity and location belong to the lot for a requalification.").
- Documents: allowed in `Draft` only; content types pdf/png/jpeg, ≤ 25 MB (copy `HplcRunService.ValidateEvidenceFile` logic into a private static in this service); storage key `working-standards/{q.Id}/{doc.Id}{ext}`; a new upload supersedes the current document of the same kind.
- `AssignProblem(q)` returns the first of: not Draft → "Only a draft qualification can be assigned."; `MoisturePercent == null` → "Enter the moisture content first."; manual source with no current `SourceReport` document → "Attach the raw material's first test report first."; else null. (Requires `Documents` loaded.)
- Eligible source samples: `TestOrders` where `CurrentStep == WorkflowStep.Approved`, `!IsSuperseded`, `Sample.Category == SampleCategory.RawMaterial`, sample not Voided/Cancelled, section accessible, and the order's `TestDefinition.EquationType` in `{ HplcAssay, HplcMultiAnalyte, StandardComparison, HplcMethodAssay }` (join `TestDefinitions` on `Code == TestCode`); one row per sample (first matching test code), search over reference number, item name, batch.
- Lots: `Materials` with `MaterialType == WorkingStandard` in accessible sections; `Status`: `QuantityRemaining <= 0` → Depleted, `ExpiryDate < LabToday` → Expired, `ExpiryDate <= LabToday + 30 days` → DueSoon, else Valid; `OpenQualificationId` = open qualification of that lot.
- `GetAsync` includes `Run` = current run sample (`HplcRunSamples` where `WorkingStandardQualificationId == id`, `Status == Assigned`, run not `Abandoned`), and `RunEvidenceIds` = current (`SupersededByEvidenceId == null`) `HplcEvidences` of that run sample plus the run's current SST `StandardReport` (downloaded through the existing `GET api/hplc-workspace/evidence/{id}/file`).

- [ ] **Step 1: Failing tests** — `WorkingStandardServiceTests.cs`. Seeding helper (in-memory, same shape as `HplcRunServiceTests.SeedAsync`/`AddEntryAsync`; copy those two helpers into this class as private statics). Tests:

```csharp
[Fact] public async Task CreateInitial_ManualSource_GetsWsqCodeAndDraft()
// entry STD-01 (ReferenceStandard); Create(Initial, entry, name "Paracetamol API", batch "RM-77", 10g, "Fridge 2", MC 0.4)
// Assert: Code == $"WSQ-01/{MM}/{yyyy}" for the fixed clock (SepFirst 2026-09-01 -> "WSQ-01/09/2026"), Status Draft,
// SourceMaterialName "Paracetamol API", SourceBatchNumber "RM-77".

[Fact] public async Task CreateInitial_BothSources_Throws()
[Fact] public async Task CreateInitial_EntryNotReferenceStandard_Throws()   // Reagent entry -> "Master entry \"X\" is not a reference standard."
[Fact] public async Task CreateInitial_SampleWithoutApprovedAssay_Throws()   // RM sample with an order at WorkflowStep.Waiting
[Fact] public async Task CreateInitial_SampleWithApprovedAssay_CopiesNameAndBatch()
// Item "Paracetamol" RawMaterial; Sample Category RawMaterial batch "RM-9"; TestDefinition EquationType StandardComparison;
// TestOrder CurrentStep Approved -> q.SourceMaterialName "Paracetamol", q.SourceBatchNumber "RM-9", q.SourceSampleId == sample.Id
[Fact] public async Task CreateRequalification_LotWithOpenQualification_Throws()
[Fact] public async Task CreateRequalification_UsesLotEntryNameAndBatch()
[Fact] public async Task SecondQualificationSameMonth_Gets02()
[Fact] public async Task UploadDocument_SameKindTwice_SupersedesFirst()
// two SourceReport uploads -> first IsCurrent false, second true
[Fact] public async Task AssignProblem_ManualWithoutReport_ReportsMissingReport()
[Fact] public async Task GetLots_StatusDueSoonAndExpired()
// lots expiring LabToday+10 -> DueSoon, LabToday-1 -> Expired, LabToday+200 -> Valid, qty 0 -> Depleted
```
Write each test body fully: arrange with the helpers, act on `TestServiceFactory.WorkingStandard(db, clock: clock)`, assert the exact message strings listed in the rules above.

`TestServiceFactory`:
```csharp
    public static WorkingStandardService WorkingStandard(
        MicroLimsDbContext db,
        IUserSectionScopeService? scope = null,
        IElectronicSignatureService? signatures = null,
        IFileStorageService? storage = null,
        ILabClock? clock = null) =>
        new(db,
            scope ?? new UserSectionScopeService(db),
            signatures ?? new ElectronicSignatureService(db),
            storage ?? new InMemoryFileStorageService(),
            clock);
```

- [ ] **Step 2: Run** `--filter WorkingStandardServiceTests` → FAIL.

- [ ] **Step 3: Implement** `WorkingStandardService` (constructor `(IMicroLimsDbContext db, IUserSectionScopeService scope, IElectronicSignatureService signatures, IFileStorageService storage, ILabClock? clock = null)`, `_clock = clock ?? LabClock.Default`) as a `partial class` so Task 6 adds `WorkingStandardService.SignOff.cs`. Implement every rule above. Access check helper:

```csharp
    private async Task EnsureSectionAsync(int sectionId, int userId, CancellationToken ct)
    {
        var scope = await _scope.GetAccessibleSectionIdsAsync(userId, ct);
        if (scope != null && !scope.Contains(sectionId))
            throw new NotFoundException("Working standard qualification not found.");
    }

    private async Task<WorkingStandardQualification> LoadAsync(int id, int userId, CancellationToken ct)
    {
        var q = await _db.WorkingStandardQualifications
            .Include(x => x.Documents)
            .Include(x => x.MaterialMasterEntry)
            .Include(x => x.WorkingStandardMaterial)
            .Include(x => x.SourceSample)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"Working standard qualification {id} not found.");
        await EnsureSectionAsync(q.SectionId, userId, ct);
        return q;
    }
```
Set `_db.CurrentUserId = userId;` before each `SaveChangesAsync` (audit attribution, as `SolutionPreparationService` does).

- [ ] **Step 4: Run** → PASS. Build the API project.

- [ ] **Step 5: Commit** — `git commit -m "feat(ws): qualification create/edit/documents and lists"`

---

### Task 5: HPLC workspace — assign, entry, submit, remove, complete

**Files:**
- Create: `backend/MicroLIMS.Application/Services/HplcRunService.WorkingStandards.cs`
- Modify: `backend/MicroLIMS.Application/Services/HplcRunService.Samples.cs` (`RemoveSampleAsync`, `CompleteRunAsync`, `BuildSampleSummariesAsync`, extract replicate replace)
- Test: `backend/MicroLIMS.Tests/UnitTests/HplcRunServiceWorkingStandardTests.cs` (partial of `HplcRunServiceTests`, reuses `SeedScenarioAsync`, `AddStandardLotAsync`, `StartPassedRunAsync`, `PassSstAsync`, `PdfBytes`, `SeedAssayAsync`, `AddAssayOrderAsync`)

**Interfaces:**
- Consumes: `WorkingStandardService.AssignProblem(q)` (make it `public static`), `WorkingStandardCalculator.Evaluate`, `HplcAssayCalculator.Calculate(int analyteId, string name, decimal thWtStdMg, decimal thWtTestMg, AssayStandard std, IReadOnlyList<AssayReplicateInput> reps)`, `IElectronicSignatureService.SignAsync(userId, password, SignatureMeaning.ResultRecorded, "WorkingStandardQualification", q.Id, comment, ip)`.
- Produces:
```csharp
public record EligibleQualificationDto(int QualificationId, string Code, WorkingStandardQualificationKind Kind, string MaterialName, string BatchNumber, string AnalyteName);
public record AssignQualificationsRequest(List<int> QualificationIds);
public record WorkingStandardPreviewDto(List<decimal> ReplicateAssayPercents, decimal MeanAssayPercent, decimal? RsdPercent, decimal PotencyPercent, bool Passed, string? FailureReasons);
public record HplcQualificationEntryDto(
    int RunSampleId, int HplcRunId, string RunCode, string SstCode, HplcSstStatus SstStatus,
    int QualificationId, string QualificationCode, WorkingStandardQualificationStatus QualificationStatus,
    string MaterialName, string BatchNumber, decimal? MoisturePercent,
    List<HplcMethodWeightDto> MethodWeights, int RequiredReplicates, List<HplcReplicateDto> Replicates,
    WorkingStandardPreviewDto? Preview, List<HplcEvidenceDto> Evidence,
    bool Editable, string? EditableReason, bool Submitted, bool CanSubmit, string? CanSubmitReason);
// HplcRunService:
Task<List<EligibleQualificationDto>> GetEligibleQualificationsAsync(int runId, int userId, CancellationToken ct = default);
Task<HplcRunDto> AssignQualificationsAsync(int runId, List<int> qualificationIds, int userId, CancellationToken ct = default);
Task<HplcQualificationEntryDto> GetQualificationEntryAsync(int runSampleId, int userId, CancellationToken ct = default);
Task<HplcQualificationEntryDto> SaveQualificationReplicatesAsync(int runSampleId, SaveReplicatesRequest r, int userId, CancellationToken ct = default);
Task<HplcQualificationEntryDto> SubmitQualificationAsync(int runSampleId, SubmitHplcSampleRequest r, int userId, string? ip, CancellationToken ct = default);
```
`HplcMethodWeightDto` in the WS entry holds only the qualified analyte, so the frontend `ReplicateEntryTable` shows one response column.

Rules:
- Eligible: `Draft` qualifications in `run.SectionId` whose `MaterialMasterEntryId` is the `StandardEntryId` of one of the run snapshot's analytes, `AssignProblem(q) == null`, and not on another `Assigned` run sample of a non-abandoned run.
- Assign: run `Open`; SST `Passed`; every `run.Sst.Analytes[*].StandardMaterial.MaterialType == ReferenceStandard` else "A working standard can only be qualified against a primary reference standard - this run's system suitability used a working standard."; each id eligible (else "Qualification {code} is not eligible for this run." or "...is already assigned to run {code}."). Sets `q.HplcMethodAnalyteId` = matching analyte id and adds `HplcRunSample { WorkingStandardQualificationId = q.Id, Status = Assigned, ... }`.
- Entry editable: run sample `Assigned`; `q.Status == Draft`; run `Open`, or run `Completed` and `q.ReturnReason != null`; messages: "This sample was removed from the run.", "The qualification has already been submitted.", "The run is closed.".
- Save replicates: editable; each replicate weight > 0; exactly one response, for the qualified analyte, > 0 ("Replicate {n}: exactly one response for {analyte} is required." / "...must be greater than zero."); replace-all inside `UnitOfWork.RunAsync` like `SaveReplicatesAsync`.
- Entry problem (preview shown when null): editable; SST passed; exactly 6 replicates ("Exactly 6 replicates are required ({n} entered)."); SST row of the analyte has mean response, weight, purity, moisture; moisture set on q.
- Submit problem: entry problem, then current `SampleReport` evidence for this run sample ("Upload the sample report before submitting.").
- Submit: check problem → sign first → compute (`HplcAssayCalculator.Calculate` with the SST row's `AssayStandard(MeanResponse, StandardWeightMg, StandardPurityPercent, StandardMoisturePercent)` and the snapshot analyte's Th.Wt values; then `WorkingStandardCalculator.Evaluate(reps.Select(r => r.AssayPercent).ToList(), q.MoisturePercent!.Value)`) → set q results, `ReplicateAssaysJson` (`JsonSerializer.Serialize(list)`), `Status = Assayed`, `PreparedByUserId/At/SignatureId = sig.Id`, `ReturnReason = null` → save.
- `RemoveSampleAsync`: when `sample.WorkingStandardQualificationId != null`, refuse unless the qualification is `Draft` ("The qualification has already been submitted."); otherwise keep the existing test-analysis check (only when `TestOrderId != null`).
- `CompleteRunAsync`: pending = assigned order samples not submitted **plus** assigned WS samples whose qualification is `Draft`; message unchanged.
- `BuildSampleSummariesAsync`: WS rows → `SampleNumber = q.Code`, `BatchNumber = q.SourceBatchNumber`, `ProductName = q.SourceMaterialName`, `TestCode = "WS qualification"`, `Submitted = q.Status != Draft`, `WorkingStandardQualificationId = q.Id`, `TestOrderId = null`.

- [ ] **Step 1: Failing tests** (`HplcRunServiceWorkingStandardTests.cs`, `public partial class HplcRunServiceTests`). Seed a qualification directly:

```csharp
    private static async Task<WorkingStandardQualification> AddQualificationAsync(
        MicroLimsDbContext db, Scenario s, decimal? moisture = 0.5m, string code = "WSQ-01/09/2026")
    {
        var q = new WorkingStandardQualification
        {
            SectionId = s.Section.Id, Code = code, Kind = WorkingStandardQualificationKind.Initial,
            Status = WorkingStandardQualificationStatus.Draft, MaterialMasterEntryId = s.StandardEntry.Id,
            SourceSampleId = null, SourceMaterialName = "Vitamin C API", SourceBatchNumber = "RM-1",
            QuantityGrams = 10m, Location = "Fridge", MoisturePercent = moisture,
            CreatedByUserId = s.UserId, CreatedAt = DateTime.UtcNow,
        };
        q.Documents.Add(new WorkingStandardDocument { Kind = WorkingStandardDocumentKind.SourceReport, FileName = "r.pdf",
            ContentType = "application/pdf", FilePath = "x", UploadedByUserId = s.UserId, UploadedAt = DateTime.UtcNow });
        db.WorkingStandardQualifications.Add(q);
        await db.SaveChangesAsync();
        return q;
    }

    private static SaveReplicatesRequest SixReplicates(int analyteId, decimal response = 1000m) =>
        new(Enumerable.Range(1, 6).Select(_ => new HplcReplicateInput(50m,
            new List<HplcReplicateResponseInput> { new(analyteId, response) })).ToList());
```

Tests (write each fully):
```csharp
[Fact] public async Task WsEligible_ListsDraftForRunStandardEntry()
[Fact] public async Task WsAssign_SstUsedWorkingStandard_Throws()
// PassSstAsync with a WorkingStandard lot: add lot via AddStandardLotAsync then set lot.MaterialType = WorkingStandard before SaveSst
[Fact] public async Task WsAssign_SetsAnalyteAndShowsInRunSummary()
// summary row: TestOrderId null, TestCode "WS qualification", SampleNumber == q.Code, Submitted false
[Fact] public async Task WsEntry_FiveReplicates_NoPreviewAndCannotSubmit()
[Fact] public async Task WsSubmit_WithoutSampleReport_Throws()
[Fact] public async Task WsSubmit_ComputesAndMovesToAssayed()
// SST responses 1000 x3, std weight 50, purity 99.5, MC 0.5 (AddStandardLotAsync defaults); sample weight 50, response 1000.
// Expected per-replicate assay = StandardComparisonCalculator.CalculatePreparationAssay(1000,1000,50,thStd,thTest,50,0.5,99.5)
// -> compute `expected` in the test by calling that same function once, then assert q.MeanAssayPercent == expected,
//    q.RsdPercent == 0, q.PotencyPercent == Math.Round(expected*100/(100-q.MoisturePercent), 3, AwayFromZero),
//    q.Status == Assayed, q.PreparedSignatureId != null.
[Fact] public async Task WsRemove_AfterSubmit_Throws()
[Fact] public async Task CompleteRun_DraftQualificationPending_Throws_ProductAndWsCounted()
// one product order assigned (not submitted) + one WS (Draft) -> message starts "2 assigned sample(s)"
[Fact] public async Task ProductEntry_OnWsRunSample_ThrowsClearError()
// service.GetSampleEntryAsync(wsRunSampleId) -> InvalidOperationException "This run sample is a working standard qualification - open it from its qualification entry."
```

- [ ] **Step 2: Run** `--filter "FullyQualifiedName~HplcRunServiceTests"` → new tests FAIL, existing pass.

- [ ] **Step 3: Implement** `HplcRunService.WorkingStandards.cs` with the methods/rules above, and the three edits in `HplcRunService.Samples.cs`. Load the qualification run sample with:

```csharp
    private async Task<(HplcRun Run, HplcRunSample RunSample, WorkingStandardQualification Q, HplcMethodResponse Snapshot, HplcMethodAnalyteResponse Analyte)>
        LoadQualificationSampleAsync(int runSampleId, CancellationToken ct)
    {
        var runSample = await _db.HplcRunSamples.Include(s => s.Replicates).ThenInclude(r => r.Responses)
            .FirstOrDefaultAsync(s => s.Id == runSampleId, ct)
            ?? throw new NotFoundException($"Run sample {runSampleId} not found.");
        if (runSample.WorkingStandardQualificationId is not int qId)
            throw new InvalidOperationException("This run sample is a test order - open it from the sample entry.");
        var run = await LoadRunAsync(runSample.HplcRunId, ct);
        var q = await _db.WorkingStandardQualifications.Include(x => x.Documents).FirstAsync(x => x.Id == qId, ct);
        var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>(run.MethodSnapshotJson, JsonOptions)
            ?? throw new InvalidOperationException("The run's method snapshot could not be read.");
        var analyte = snapshot.Analytes.First(a => a.Id == q.HplcMethodAnalyteId);
        return (run, runSample, q, snapshot, analyte);
    }
```

- [ ] **Step 4: Run** → all `HplcRunServiceTests` PASS (old + new).

- [ ] **Step 5: Commit** — `git commit -m "feat(ws): qualification samples on HPLC runs (assign, entry, submit)"`

---

### Task 6: Sign-off — review, return, reject, approve (lot create/extend)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/WorkingStandardService.SignOff.cs`
- Test: `backend/MicroLIMS.Tests/UnitTests/WorkingStandardSignOffTests.cs`

**Interfaces:**
- Produces:
```csharp
Task<WorkingStandardQualificationDto> ReviewAsync(int id, WorkingStandardSignRequest r, int userId, string? ip, CancellationToken ct = default);
Task<WorkingStandardQualificationDto> ReturnAsync(int id, WorkingStandardReturnRequest r, int userId, CancellationToken ct = default);
Task<WorkingStandardQualificationDto> RejectAtReviewAsync(int id, WorkingStandardReasonRequest r, int userId, string? ip, CancellationToken ct = default);
Task<WorkingStandardQualificationDto> ApproveAsync(int id, WorkingStandardSignRequest r, int userId, string? ip, CancellationToken ct = default);
Task<WorkingStandardQualificationDto> RejectAtApprovalAsync(int id, WorkingStandardReasonRequest r, int userId, string? ip, CancellationToken ct = default);
```

Rules:
- Review: status `Assayed` ("Only an assayed qualification can be reviewed."); `Passed` ("A failed qualification can only be rejected."); `userId != PreparedByUserId` ("The analyst who submitted the qualification cannot review it."); sign `Reviewed` → `Status = Reviewed`, Reviewed*.
- Return: status `Assayed`; reason required (trim, ≤ 500, "Enter a reason."); `Status = Draft`, `ReturnReason = reason`, clear `MeanAssayPercent, RsdPercent, PotencyPercent, ReplicateAssaysJson, FailureReasons, Passed=false, Prepared*`. No signature (reason + audit trail).
- RejectAtReview: status `Assayed`; reason; sign `Rejected` → `Status = Rejected`, Rejected*, `RejectReason`.
- Approve: status `Reviewed` ("Only a reviewed qualification can be approved."); `Passed`; `userId` ≠ preparer ("The analyst who submitted the qualification cannot approve it.") and ≠ reviewer ("The reviewer cannot also approve the qualification."); sign `Approved`; then
  - `expiry = _clock.LabToday.AddMonths(WorkingStandardRules.ValidityMonths).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)`;
  - Initial: `code = await SolutionPreparationCode.NextAsync(_db.Materials.Where(m => m.MaterialType == MaterialType.WorkingStandard && m.Code != null).Select(m => m.Code!), "WS-", _clock.ToLabLocal(nowUtc), ct)`; add `Material { SectionId = q.SectionId, MaterialType = WorkingStandard, MaterialMasterEntryId = q.MaterialMasterEntryId, MaterialName = entry.Name, ManufacturerName = "In-house", BatchNumber = q.SourceBatchNumber, ReceivingDate = nowUtc, ExpiryDate = expiry, Code = code, Location = q.Location!, QuantityReceived = q.QuantityGrams!.Value, QuantityRemaining = q.QuantityGrams.Value, Unit = MaterialUnit.Gram, Purity = q.PotencyPercent, MoisturePercent = q.MoisturePercent, CreatedByUserId = userId, CreatedAt = nowUtc, LastModifiedByUserId = userId, LastModifiedAt = nowUtc }`; `q.WorkingStandardMaterial = lot`;
  - Requalification: lot = `q.WorkingStandardMaterial`; set `Purity`, `MoisturePercent`, `ExpiryDate`, `LastModifiedByUserId/At`;
  - `Status = Approved`, Approved*; `_db.CurrentUserId = userId`; `if (!await _db.TrySaveChangesAsync(UniqueIndexNames.WorkingStandardCode)) throw new InvalidOperationException("Another approval took that working standard code at the same moment - sign again.");`
- RejectAtApproval: status `Reviewed`; reason; `userId != PreparedByUserId`; sign `Rejected`.
- Every signing method: validate state first (no signature on a refused action), then `SignAsync`, then mutate (sign-first relative to mutation).

- [ ] **Step 1: Failing tests** — seed users: analyst (A), reviewer (R), approver (P) with the in-memory helpers; seed an `Assayed` qualification directly (`Passed = true`, `PotencyPercent = 99.497m`, `MoisturePercent = 0.5m`, `PreparedByUserId = A`). Tests:

```csharp
[Fact] public async Task Review_BySubmitter_Throws()
[Fact] public async Task Review_Failed_Throws()
[Fact] public async Task Approve_ByReviewer_Throws()
[Fact] public async Task Approve_Initial_CreatesWorkingStandardLot()
// clock SepFirst 2026-09-01 -> lot.Code "WS-01/09/2026", MaterialType WorkingStandard, Purity 99.497, MoisturePercent 0.5,
// ExpiryDate 2027-09-01, QuantityRemaining == q.QuantityGrams, MaterialMasterEntryId == entry, q.WorkingStandardMaterialId == lot.Id
[Fact] public async Task Approve_Requalification_ExtendsSameLot()
// expired lot (ExpiryDate 2026-08-01, Purity 98.9); requal approved -> same Id, Purity 99.497, ExpiryDate 2027-09-01,
// db.Materials.Count(WorkingStandard) == 1, LotUsability.Check(lot, entry.Id, null, new DateOnly(2026,9,1)).Usable == true
[Fact] public async Task Return_ClearsResultsAndGoesToDraft()
[Fact] public async Task RejectAtReview_RecordsReasonAndSignature()
[Fact] public async Task WrongPassword_NoStateChange()
// ReviewAsync with "bad" -> SignatureVerificationException; q.Status still Assayed
```

- [ ] **Step 2: Run** `--filter WorkingStandardSignOffTests` → FAIL.
- [ ] **Step 3: Implement** `WorkingStandardService.SignOff.cs` per the rules.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** — `git commit -m "feat(ws): review, return, reject and approve with lot creation/extension"`

---

### Task 7: API controllers

**Files:**
- Create: `backend/MicroLIMS.API/Controllers/WorkingStandardsController.cs`
- Modify: `backend/MicroLIMS.API/Controllers/HplcWorkspaceController.cs`
- Test: extend `backend/MicroLIMS.Tests/UnitTests/HplcRunServiceUiContractTests.cs` only if it asserts the controller route list; otherwise none (build + Task 11 E2E cover it).

- [ ] **Step 1: `WorkingStandardsController`** (`[Route("api/working-standards")]`, `[Authorize]`, same `CurrentUserId`/`ClientIpAddress`/`ReadAsync` helpers as `HplcWorkspaceController`, 30 MB `RequestSizeLimit` on uploads):

| Verb | Route | Policy | Service |
|---|---|---|---|
| GET | `lots` | — | `GetLotsAsync` |
| GET | `qualifications` | — | `GetQualificationsAsync` |
| GET | `qualifications/{id}` | — | `GetAsync` |
| GET | `source-samples?search=` | — | `GetEligibleSourceSamplesAsync` |
| POST | `qualifications` | `WorkingStandardsQualify` | `CreateAsync` |
| PUT | `qualifications/{id}` | `WorkingStandardsQualify` | `UpdateDraftAsync` |
| POST | `qualifications/{id}/documents` (form: file, kind) | `WorkingStandardsQualify` | `UploadDocumentAsync` |
| GET | `documents/{id}/file` | — | `DownloadDocumentAsync` → `File(...)` |
| POST | `qualifications/{id}/review` | `SamplesReview` | `ReviewAsync` |
| POST | `qualifications/{id}/return` | `SamplesReview` | `ReturnAsync` |
| POST | `qualifications/{id}/reject-review` | `SamplesReview` | `RejectAtReviewAsync` |
| POST | `qualifications/{id}/approve` | `SamplesApprove` | `ApproveAsync` |
| POST | `qualifications/{id}/reject-approval` | `SamplesApprove` | `RejectAtApprovalAsync` |

All return `Ok(ApiResponse<object>.Ok(...))`.

- [ ] **Step 2: `HplcWorkspaceController`** additions (all writes `HplcOperate`):

```csharp
    [HttpGet("runs/{id:int}/eligible-qualifications")]
    public async Task<IActionResult> GetEligibleQualifications(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.GetEligibleQualificationsAsync(id, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.HplcOperate)]
    [HttpPost("runs/{id:int}/qualifications")]
    public async Task<IActionResult> AssignQualifications(int id, [FromBody] AssignQualificationsRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.AssignQualificationsAsync(id, r.QualificationIds, CurrentUserId)));

    [HttpGet("qualification-samples/{runSampleId:int}")]
    public async Task<IActionResult> GetQualificationEntry(int runSampleId) =>
        Ok(ApiResponse<object>.Ok(await _service.GetQualificationEntryAsync(runSampleId, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.HplcOperate)]
    [HttpPut("qualification-samples/{runSampleId:int}/replicates")]
    public async Task<IActionResult> SaveQualificationReplicates(int runSampleId, [FromBody] SaveReplicatesRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.SaveQualificationReplicatesAsync(runSampleId, r, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.HplcOperate)]
    [HttpPost("qualification-samples/{runSampleId:int}/submit")]
    public async Task<IActionResult> SubmitQualification(int runSampleId, [FromBody] SubmitHplcSampleRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.SubmitQualificationAsync(runSampleId, r, CurrentUserId, ClientIpAddress)));
```

- [ ] **Step 3: Build** API → clean. Run full suite (`run-postgres-tests.sh` after `dotnet build`) → all pass.
- [ ] **Step 4: Commit** — `git commit -m "feat(ws): working standards API and workspace qualification endpoints"`

---

### Task 8: Postgres integration tests

**Files:**
- Create: `backend/MicroLIMS.Tests/IntegrationTests/WorkingStandardPostgresIntegrationTests.cs` (`[Collection("PostgresDatabaseCollection")]`, `[PostgresFact]`, seeding like `SolutionPreparationPostgresIntegrationTests.SeedAsync` with a random suffix)

Tests:
- [ ] **`RunSample_BothOrNeitherSubject_RejectedByCheckConstraint`** — insert an `HplcRunSample` with both ids null via raw SQL (`ExecuteSqlRawAsync`) → `PostgresException` with `ConstraintName == "CK_HplcRunSamples_OneSubject"`.
- [ ] **`Approve_TwoAtSameMoment_SecondGetsSignAgain`** — two `Reviewed` initial qualifications; an interceptor (copy `CompleteOtherFirst` from `SolutionPreparationPostgresIntegrationTests`, renamed `ApproveOtherFirst`, calling `ApproveAsync` for the other qualification on a separate context just before this save) → `InvalidOperationException` "Another approval took that working standard code at the same moment - sign again."; exactly one `WS-01/MM/yyyy` lot exists; the losing qualification is still `Reviewed`.
- [ ] **`ApprovedLot_UsableInSolutionPreparationAndSstPicker`** — after approval, `TestServiceFactory.Material(db).GetUsableReferenceStandardsAsync(user)` contains the WS lot; `LotUsability.Check(lot, entryId, 1m, today).Usable` is true.
- [ ] **`Migration_GrantsWorkingStandardsQualify`** — covered by `PermissionEnforcementMigrationPostgresTests` (Task 1); no new test.

- [ ] Run: `dotnet build backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/ws-build && bash .claude/scripts/run-postgres-tests.sh E:/MicroLIMS/rls-tmp/ws-build --filter WorkingStandardPostgresIntegrationTests` → PASS; then the full suite → all pass.
- [ ] Commit — `git commit -m "test(ws): Postgres checks for run sample subject, code clash, lot usability"`

---

### Task 9: Frontend — Working Standards page

**Files (all new under `frontend/src/modules/workingStandards/`):**
- `types.ts` — TS mirrors of the Task 4/6 DTOs (camelCase, enums as string unions: `"Draft" | "Assayed" | "Reviewed" | "Approved" | "Rejected"`, `"Initial" | "Requalification"`, `"SourceReport" | "MoistureReport"`).
- `services/WorkingStandardService.ts` — one function per endpoint in Task 7's table, same `ApiResponse<T>` unwrap as `HplcWorkspaceService.ts`; uploads via `FormData` (`file`, `kind`).
- `WorkingStandardsPage.tsx` — tabs "Lots" and "Qualifications"; "New qualification" button (needs `WORKING_STANDARDS_QUALIFY`).
- `lots/WorkingStandardLotsTable.tsx` — columns Code, Material, Batch, Potency %, MC %, Remaining (g), Expiry, Status badge (Valid green, DueSoon amber, Expired/Depleted red); row action "Requalify" (disabled when `openQualificationId`).
- `qualifications/QualificationsTable.tsx` — Code, Kind, Material, Batch, Status, Run, Created; row click opens the detail drawer.
- `qualifications/NewQualificationDialog.tsx` — Kind Initial: source toggle (Received sample → searchable list from `source-samples`; Manual → name + batch), master entry select (ReferenceStandard entries from `MaterialMasterService`), quantity (g), location, moisture %. Kind Requalification: opened from a lot row with the lot fixed; moisture only.
- `qualifications/QualificationDetailDrawer.tsx` — header (code, status), source, moisture, documents list + upload (Draft only), run link (navigates to `/hplc-workspace/{equipmentId}/run/{runId}/qualification/{runSampleId}`), results card (replicate assay %, mean, RSD, potency, pass/fail reasons — values from the API only), run evidence links (`/api/hplc-workspace/evidence/{id}/file`), sign-off actions by status and permission: Assayed → Review / Return / Reject (`SAMPLES_REVIEW`); Reviewed → Approve / Reject (`SAMPLES_APPROVE`).
- `qualifications/SignOffDialog.tsx` — password (+ comment, or reason when rejecting); reuse the project's existing signature dialog if one exists (`git grep -ln "type=\"password\"" frontend/src/components`), else a small MUI dialog.
- Modify: `frontend/src/routes/AppRoutes.tsx` — `<Route path="/working-standards" element={<WorkingStandardsPage />} />` next to the `/hplc-workspace` routes (authenticated, no permission wrapper — reads are open, actions are permission-gated in the UI and API).
- Modify: `frontend/src/routes/menuConfig.ts` — `{ label: "Working Standards", path: "/working-standards" },` after "Solution Preparation".

Steps:
- [ ] Write the files (keep each < ~250 lines; split if larger).
- [ ] `cd frontend && npx tsc --noEmit && npx eslint src/modules/workingStandards src/routes` → clean; `npm run build` → OK.
- [ ] Add one Vitest/RTL test `frontend/src/modules/workingStandards/__tests__/NewQualificationDialog.test.tsx`: Initial + Manual with empty batch → Save disabled; filling name, batch, entry, quantity, location → Save enabled. Run `npx vitest run src/modules/workingStandards` → PASS.
- [ ] Commit — `git commit -m "feat(ws): Working Standards page (lots, qualifications, sign-off)"`

---

### Task 10: Frontend — HPLC workspace qualification samples

**Files:**
- Modify: `frontend/src/modules/hplcWorkspace/types.ts` — `HplcRunSampleSummaryDto.testOrderId: number | null`, add `workingStandardQualificationId?: number | null`; add `EligibleQualificationDto`, `HplcQualificationEntryDto`, `WorkingStandardPreviewDto`.
- Modify: `frontend/src/modules/hplcWorkspace/services/HplcWorkspaceService.ts` — `getEligibleQualifications(runId)`, `assignQualifications(runId, ids)`, `getQualificationEntry(runSampleId)`, `saveQualificationReplicates(runSampleId, req)`, `submitQualification(runSampleId, req)`.
- Create: `frontend/src/modules/hplcWorkspace/samples/EligibleQualificationTable.tsx` — dialog like `EligibleSampleTable` listing eligible qualifications (Code, Material, Batch, Analyte), multi-select, Assign.
- Modify: `frontend/src/modules/hplcWorkspace/samples/SampleAssignmentPanel.tsx` — second button "Assign Working Standard..." opening the new dialog; sample rows with `workingStandardQualificationId` show a "WS qualification" chip and navigate to `/hplc-workspace/${run.equipmentId}/run/${run.id}/qualification/${s.id}`.
- Create: `frontend/src/modules/hplcWorkspace/entry/QualificationEntryPage.tsx` — loads `getQualificationEntry`; renders `ReplicateEntryTable` with `methodWeights` (one analyte) and `requiredReplicates = 6`; Save replicates; `ReportUploadPanel` for the sample report (same props as `HplcSampleEntryPage` uses, `runSampleId` = this run sample); preview card (mean, RSD, potency, pass/fail from the API); Submit via `SendForReviewDialog` (password + comment) calling `submitQualification`; after submit shows "Submitted - review on the Working Standards page" with a link to `/working-standards`.
- Modify: `frontend/src/routes/AppRoutes.tsx` — `<Route path="/hplc-workspace/:instrumentId/run/:runId/qualification/:runSampleId" element={<QualificationEntryPage />} />`.

Steps:
- [ ] Implement; `npx tsc --noEmit`, eslint on `src/modules/hplcWorkspace`, `npm run build` → clean.
- [ ] Vitest: `SampleAssignmentPanel` renders a WS row with the "WS qualification" chip (render with a run fixture containing one WS summary row). Run → PASS.
- [ ] Commit — `git commit -m "feat(ws): assign and enter working standard qualifications in the HPLC workspace"`

---

### Task 11: Apply migration, full verification, browser end-to-end

- [ ] Stop nothing the user runs; back up LIMSV2: `pg_dump -Fc -d LIMSV2 -f E:/MicroLIMS/backups/LIMSV2_before_working_standards_20261003.dump` (use the same connection flags as previous backups; password from `appsettings.Development.json`, never printed).
- [ ] Apply: `dotnet ef database update --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release`.
- [ ] Verify Down on a scratch copy is not required (Down SQL written in Task 1); verify in LIMSV2: `SELECT "Code" FROM "Permissions" WHERE "Code"='WorkingStandards.Qualify';` → 1 row; `\d "HplcRunSamples"` shows `CK_HplcRunSamples_OneSubject`.
- [ ] Full suite: `dotnet build backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/ws-build && bash .claude/scripts/run-postgres-tests.sh E:/MicroLIMS/rls-tmp/ws-build` → 0 failed; frontend `npm run build`, `npx vitest run` → pass.
- [ ] Browser E2E (restart API + Vite on the new code; test users `e2e.analyst` / `e2e.reviewer` / `e2e.head`, FP section, passwords in `frontend/.env.e2e.local`):
  1. Analyst: Working Standards → New qualification → Manual source "Ascorbic Acid API", batch "RM-WS-1", entry = Vitamin C RS entry, 5 g, "Fridge 1", MC 0.3; upload a PDF as the source report.
  2. Analyst: HPLC workspace → start a run of the Vitamin C method, SST with the **primary** RS lot → passes.
  3. Assign Working Standard → the qualification appears → assign; open it; enter 6 replicates; upload sample report; preview shows mean/RSD/potency; submit with password.
  4. Reviewer: Working Standards → qualification → Review (sign). Head: Approve (sign) → Lots tab shows `WS-01/10/2026`, potency, expiry 2027-10-03, Valid.
  5. Analyst: Solution Preparation of a standard solution using the Vitamin C RS entry → the WS lot is offered; new run SST → WS lot offered in the standard picker.
  6. Negative: start another run whose SST uses the WS lot → "Assign Working Standard" refuses with the primary-standard message.
- [ ] Record numbers and screenshots in the final summary; update memory `working-standard-area-decisions.md` with commits and test counts.
- [ ] Commit any fixes found during E2E (each with its own message).
