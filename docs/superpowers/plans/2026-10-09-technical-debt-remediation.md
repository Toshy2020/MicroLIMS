# Technical Debt Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the technical debt found in the 2026-10-09 refactoring assessment (oversized read-model services, whole-table scans, duplicated mappers, giant React components, controller exception boilerplate) without changing any laboratory behavior.

**Architecture:** Every backend refactor follows one recipe: write a test for a pure builder first, extract the matching block of a god method into that builder (loaded data in, DTO out, no database access), run the existing suite, commit. Frontend refactors are pure file moves guarded by `tsc` and a browser smoke check. Nothing here changes a public API, a migration, an audit trail or an electronic signature.

**Tech Stack:** ASP.NET Core / EF Core 10 / xUnit (InMemory for unit tests, Postgres for integration), React + TypeScript + MUI, Vitest.

**Spec:** No separate spec. The findings baseline is the "Findings" section below, produced from the assessment in this conversation (static metrics plus targeted reads; no runtime profiling).

## Findings baseline (what this plan fixes, and what it deliberately does not)

| # | Finding | Evidence | Handled in |
|---|---|---|---|
| F1 | God methods that assemble read models | `PathogenSessionService.GetSessionAsync` 247-813 (566 lines), `SampleSummaryService.GetSummaryAsync` 92-656, `DashboardService.GetSectionHeadDashboardAsync` 536-890, `GroupedTestActionService.GetActionableGroupsAsync` 54-507 and `ExecuteBatchSelectMediaAsync` 544-873, `DocumentApprovalService.ExecuteApprovalDecisionCoreAsync` 428-677 | Phases 2, 3 |
| F2 | Whole-table `Users` load to map names | `PathogenSessionService.cs:284`; ~10 more `ToDictionaryAsync(u => u.Id ...)` sites to audit | Task 2, Task 6 |
| F3 | Duplicate DTO mappers | `MapRevisionDto` (`DocumentApprovalService.cs:844`) vs `MapRevisionToDto` (`DocumentMasterService.cs:791`) | Task 3 |
| F4 | 37 request records declared at the top of a controller | `TestWorkflowController.cs:20-90` | Task 1 |
| F5 | Frontend giant components | `TestMasterPage.tsx` 2801 lines, `SampleSummaryDialog.tsx` 2108, 331 uses of `any` | Phase 5 |
| F6 | Migration chain size | 154 migrations, 306 files, 40 MB, snapshot 502 KB | Task 16 (measure only) |
| F7 | `IMicroLimsDbContext` mirrors 163 `DbSet`s by hand (164 vs 163, so they have already drifted) | interface vs `MicroLimsDbContext` | Task 15 |
| F8 | 58 controller catches remain (20 `ArgumentException`, Conflict/422/integrity, `WorkflowStepException`, others) | Module A result | Decision D1 |

**Withdrawn from the original assessment (verified wrong or already handled; do not "fix"):**
- *"Application is coupled to EF Core."* This is a deliberate design: `IMicroLimsDbContext` is the port, and `LayerDependencyTests.Application_DoesNotReferenceTheDatabaseProvider` enforces it. No work.
- *"Recorders save several times per request, risking half-written state."* `IncubationStepRecorder` and `PathogenConfirmationRecorder` already wrap every multi-save command in `UnitOfWork.RunAsync` (a transaction). No work.
- *Controller exception boilerplate* is done (commits `621fb240`, `9dfb8dda`).

## Global Constraints

- All builds, test artifacts, temp files and scratch go under `E:/MicroLIMS/rls-tmp`; never `C:` (it is full). Use `--artifacts-path E:/MicroLIMS/rls-tmp/art-<name>` and export `TEMP=/e/MicroLIMS/rls-tmp TMP=/e/MicroLIMS/rls-tmp`.
- A task is not done until the **full** backend suite shows `Failed: 0 ... Skipped: 0`. `Skipped` above 0 means Postgres did not run (see Task 0).
- Commit each tested slice locally. **Never push, open a PR, or merge without being asked.**
- Refactors change structure only. Any intentional behavior change must be listed in the task that makes it.
- Laboratory rules, audit trail writes and electronic-signature calls are moved verbatim, never reworded or reordered.
- Heavy reading of services over ~500 lines goes through agy-bridge (`analyze_files`, at most 3 files per call, `git grep` only). Edits are done directly.
- Use Bash heredocs for commit messages (PowerShell 5.1 adds a BOM). End every commit message with `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- Frontend must not gain laboratory or GMP logic (CLAUDE.md principle 2).

Standard backend test command (referred to below as **FULL-SUITE**):

```bash
cd /e/MicroLIMS/MicroLIMS/backend
export MICROLIMS_TEST_POSTGRES="$(python -c "import json;print(json.load(open(r'E:\MicroLIMS\MicroLIMS\.claude\settings.local.json'))['permissions']['env']['MICROLIMS_TEST_POSTGRES'])")"
export TEMP=/e/MicroLIMS/rls-tmp TMP=/e/MicroLIMS/rls-tmp
dotnet test MicroLIMS.Tests --artifacts-path /e/MicroLIMS/rls-tmp/art-debt --nologo -v q 2>&1 | grep -E '^\s*(Failed|Passed)!|\[FAIL\]| error '
```

Expected: `Passed!  - Failed:     0, Passed:  2766, Skipped:     0` (the passed count grows as tasks add tests).

## Review Focus

Failure modes the refactors imply but no existing test exercises, most likely first. Each is pinned by a test in the task named.

1. A session with **no locations** (single product sample) must still return one default `Product` location with every test mapped to location `0`. (Task 5)
2. Two different water points or rooms with the **same display name** must stay two separate session locations, not merge. (Task 5)
3. After narrowing the `Users` query, a user id that no longer resolves must still display as `"Unknown"`, not throw. (Task 6)
4. A test whose TSB incubation window is **not configured** must report `WindowNotConfigured = true` and no ready time. (Task 7)
5. Moving request records must not change JSON binding (property names, optional defaults like `Comment = null`). (Task 1, guarded by `ApiContractTests`)

---

## Phase 0 - Land what already exists (user-owned gates)

### Task 0: Verify and land the controller exception cleanup

**Files:**
- Modify: `.claude/settings.local.json` (move `MICROLIMS_TEST_POSTGRES` from `permissions.env` to a top-level `env` key)

**Interfaces:**
- Produces: a working `MICROLIMS_TEST_POSTGRES` for every later task, and a merge-ready branch `refactor/controller-exception-cleanup`.

- [ ] **Step 1: Fix the test environment variable.** In `.claude/settings.local.json` the key currently sits at `permissions.env.MICROLIMS_TEST_POSTGRES`, which Claude Code does not read as environment, so Postgres tests silently skip. Move the whole `"env": { ... }` object out of `"permissions"` to the top level of the file.
- [ ] **Step 2: Restart Claude Code and confirm** by running FULL-SUITE without the `export MICROLIMS_TEST_POSTGRES=...` line. Expected: `Skipped: 0`.
- [ ] **Step 3: Browser smoke test the 500 contract (user).** Real bugs now return a generic 500 instead of a raw 400. In Document Control open a revision, a review task and an approval task; in Testing open a pathogen session. Confirm normal flows work and that a forced rule violation (e.g. approve without a signature) still shows its readable message.
- [ ] **Step 4: Open the PR (user decides).** The branch is based on `feat/instrument-report-ocr`, so the PR base must include those commits or be rebased first.

---

## Phase 1 - Quick wins

### Task 1: Move the `TestWorkflowController` request records to their own file

**Files:**
- Create: `backend/MicroLIMS.API/Controllers/TestWorkflowRequests.cs`
- Modify: `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs:20-90` (delete the records)
- Test: `backend/MicroLIMS.Tests/ArchitectureTests/ApiContractTests.cs` (existing, run only)

**Interfaces:**
- Produces: the same public types in the same namespace `MicroLIMS.API.Controllers`, so no `using` changes anywhere.

- [ ] **Step 1: Capture the baseline.** Run `dotnet test MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/art-debt --filter "FullyQualifiedName~ApiContractTests"`. Expected: PASS.
- [ ] **Step 2: Create the file** with this header, then paste lines 20-90 of `TestWorkflowController.cs` below it **exactly as they are**, including the comment block and the `[Obsolete]` record:

```csharp
using MicroLIMS.Domain.Enums;

namespace MicroLIMS.API.Controllers;

// Request bodies for TestWorkflowController. Kept in their own file so the
// controller shows only the endpoints. Namespace is unchanged on purpose.
```

- [ ] **Step 3: Delete those lines from the controller** (keep `[ApiController]`, `[Route("api/test-workflow")]` and everything after).
- [ ] **Step 4: Build.** `dotnet build MicroLIMS.API --artifacts-path E:/MicroLIMS/rls-tmp/art-debt -v q --nologo`. Expected: 0 errors. If a type is missing a `using` (for example `GrowthObservation`, `AnalystDecision`), add it to the new file.
- [ ] **Step 5: Run FULL-SUITE.** Expected: all pass, `Skipped: 0`.
- [ ] **Step 6: Commit** `refactor(api): move TestWorkflowController request records to their own file`.

### Task 2: Audit whole-table `Users` loads - DONE (one site, fixed in Task 6)

**Files:**
- Modify: each site this audit finds to be unfiltered (Task 6 handles `PathogenSessionService`)

- [ ] **Step 1: List candidate sites.** Run:

```bash
cd /e/MicroLIMS/MicroLIMS/backend
git grep -n -B3 -E 'ToDictionaryAsync\(u => u\.Id' -- MicroLIMS.Application
```

- [ ] **Step 2: Classify each site.** A site is a defect only if the preceding lines have no `.Where(u => <ids>.Contains(u.Id))` and the table is not tiny by nature. Write the verdict per site (file:line, filtered or not) at the bottom of this task in this file.
- [ ] **Step 3: Fix each unfiltered site** with the pattern below, keeping the existing fallback for a missing id:

```csharp
var userIds = rows.Select(r => r.UserId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
var users = await _db.Users
    .Where(u => userIds.Contains(u.Id))
    .ToDictionaryAsync(u => u.Id, u => u.FullName);
```

- [ ] **Step 4: Run FULL-SUITE**, then commit `perf(application): load only the users a result actually references`.

**Verdict (done 2026-10-09):** all ~45 other `ToDictionaryAsync(u => u.Id ...)` sites already filter with `.Where(u => <ids>.Contains(u.Id))`. The only whole-table load is `PathogenSessionService.cs:284`, fixed in Task 6. Steps 3-4 are not needed.

### Task 3: One shared revision mapper for Document Control - DONE (3 of 4 copies)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/DocumentControl/DocumentDtoMapper.cs`
- Modify: `DocumentApprovalService.cs:844`, `DocumentMasterService.cs:791`
- Test: `backend/MicroLIMS.Tests/UnitTests/DocumentDtoMapperTests.cs`

**Interfaces:**
- Produces: `internal static class DocumentDtoMapper { public static DocumentRevisionDto MapRevision(DocumentRevision r); }`

- [ ] **Step 1: Prove the two mappers are identical.** Read `DocumentApprovalService.MapRevisionDto` (844-869) and `DocumentMasterService.MapRevisionToDto` (791-). If they differ in any field or default, **stop and report the difference**; do not merge them.
- [ ] **Step 2: Write the failing test** that builds one `DocumentRevision` with every field populated and asserts every property of the resulting `DocumentRevisionDto` equals the entity's value. Run it. Expected: FAIL (`DocumentDtoMapper` does not exist).
- [ ] **Step 3: Create `DocumentDtoMapper`** by moving the body of `MapRevisionDto` verbatim into `MapRevision`.
- [ ] **Step 4: Replace both private methods** with calls to `DocumentDtoMapper.MapRevision`. Leave `MapFileDto`, `MapMasterDto` and `MapReviewTaskDto` where they are unless Step 1 showed duplicates of them elsewhere (`git grep -n "static .*Dto Map"` to check).
- [ ] **Step 5: Run FULL-SUITE**, then commit `refactor(document-control): share one revision DTO mapper`.

**Outcome (2026-10-09):** there were four copies, not two. Approval, Master and Revision services were identical and now share `DocumentDtoMapper.MapRevision/MapFile` (public, since the test project has no `InternalsVisibleTo`). `PeriodicReviewService` (~line 323) was deliberately left alone: it omits the canceller name, does not order files and defaults the last file fields, so merging would change its output.

### Task 4: Untracked scratch files in the repo root

- [ ] **Step 1 (user decision):** these are untracked and not part of the product: `assay_met.txt`, `page_1.txt` ... `page_7.txt`, `temp_ibu_pdf.txt`, `temp_met_pdf.txt`, `temp_pdf_out.txt`, `docs/superpowers/plans/2026-09-30-hplc-chain-test-data.md`. Decide per file: delete, move to `E:/MicroLIMS/rls-tmp`, or add to `.gitignore`.
- [ ] **Step 2:** apply the decision. Do not delete anything without the user's say-so.

---

## Phase 2 - Split `PathogenSessionService.GetSessionAsync`

`GetSessionAsync` (247-813) is one method with six blocks. Each becomes a pure builder (data in, DTO out). The public signature `Task<PathogenTestingSessionDto?> GetSessionAsync(int sampleId)` never changes. The 16 existing tests that call it are the regression net; each new builder also gets its own unit tests. New files go in `backend/MicroLIMS.Application/Services/PathogenSession/` and use namespace `MicroLIMS.Application.Services`.

Block map (lines in the current file):

| Block | Lines | Becomes |
|---|---|---|
| A. Load sample graph | 249-267 | stays in the service |
| B. Load test definitions + users | 270-285 | `PathogenSessionData.LoadAsync` (Task 6) |
| C. Group locations | 287-346 | `SessionLocationGrouper.Group` (Task 5) |
| D. Load incubations, step results, observations, readings | 348-379 | `PathogenSessionData.LoadAsync` (Task 6) |
| E. Shared TSB state | 381-492 | `SharedTsbStateBuilder.Build` (Task 7) |
| F. Assigned test DTOs | 494-~800 | `AssignedTestBuilder` (Task 8) |

### Task 5: Extract `SessionLocationGrouper` (block C)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/PathogenSession/SessionLocationGrouper.cs`
- Modify: `PathogenSessionService.cs:287-346`
- Test: `backend/MicroLIMS.Tests/UnitTests/SessionLocationGrouperTests.cs`

**Interfaces:**
- Produces: `internal static class SessionLocationGrouper { public static List<SessionLocationDto> Group(Sample sample); }`
- Consumes: `Sample` with `Locations` (including `RoomTestConfiguration.Room`, `MachinePartConfiguration.MachinePart`, `WaterSamplingPoint`), `TestOrders`, `Item`, `ReferenceNumber`.

- [ ] **Step 1: Write the failing tests** (these pin Review Focus 1 and 2):

```csharp
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SessionLocationGrouperTests
{
    [Fact]
    public void SampleWithNoLocations_GetsOneDefaultProductLocation_MappingEveryTestToZero()
    {
        var sample = new Sample
        {
            ReferenceNumber = "S-1",
            Item = new Item { Name = "Cream" },
            TestOrders = { new TestOrder { Id = 1, TestCode = "TAMC" }, new TestOrder { Id = 2, TestCode = "TYMC" } }
        };

        var result = SessionLocationGrouper.Group(sample);

        var only = Assert.Single(result);
        Assert.Equal("Cream", only.Name);
        Assert.Equal("Product", only.Type);
        Assert.All(only.TestLocationMap.Values, v => Assert.Equal(0, v));
        Assert.Equal(2, only.TestLocationMap.Count);
    }

    [Fact]
    public void TwoWaterPointsWithTheSameDisplayName_StayTwoLocations()
    {
        var sample = new Sample
        {
            ReferenceNumber = "S-2",
            TestOrders = { new TestOrder { Id = 1, TestCode = "TAMC" } },
            Locations =
            {
                new SampleLocation { Id = 10, TestOrderId = 1, WaterSamplingPointId = 100, WaterSamplingPoint = new WaterSamplingPoint { Id = 100, Location = "WTU" } },
                new SampleLocation { Id = 11, TestOrderId = 1, WaterSamplingPointId = 101, WaterSamplingPoint = new WaterSamplingPoint { Id = 101, Location = "WTU" } }
            }
        };

        var result = SessionLocationGrouper.Group(sample);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 1, 2 }, result.Select(r => r.Order));
    }
}
```

If `SessionLocationDto` names its first property differently (the code constructs it positionally as order, locationId, name, type, grade, map), adjust the property names in the assertions to match the record in `MicroLIMS.Application/DTOs`.

- [ ] **Step 2: Run them.** `dotnet test MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/art-debt --filter "FullyQualifiedName~SessionLocationGrouperTests"`. Expected: FAIL (type does not exist).
- [ ] **Step 3: Create the class** by moving lines 287-346 verbatim (the comment about display names stays) into:

```csharp
namespace MicroLIMS.Application.Services;

internal static class SessionLocationGrouper
{
    public static List<SessionLocationDto> Group(Sample sample)
    {
        // <paste lines 288-346 here unchanged: groupedLocations, locationMap,
        //  the foreach over sample.Locations, and the default-location branch>
        return groupedLocations;
    }
}
```

Add whichever `using` lines the pasted block needs (copy them from the top of `PathogenSessionService.cs`).
- [ ] **Step 4: Replace lines 287-346 in the service** with `var groupedLocations = SessionLocationGrouper.Group(sample);`.
- [ ] **Step 5: Run the new tests, then FULL-SUITE.** Expected: all pass.
- [ ] **Step 6: Commit** `refactor(pathogen-session): extract SessionLocationGrouper`.

### Task 6: Extract `PathogenSessionData` and narrow the Users query (blocks B and D, fixes F2)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/PathogenSession/PathogenSessionData.cs`
- Modify: `PathogenSessionService.cs:270-285, 348-379`
- Test: `backend/MicroLIMS.Tests/UnitTests/PathogenSessionDataTests.cs`

**Interfaces:**
- Produces:

```csharp
internal sealed record PathogenSessionData(
    Dictionary<string, TestDefinition> TestDefs,
    Func<int?, string> UserName,
    List<Incubation> Incubations,
    List<WorkflowStepResult> StepResults,
    List<LocationPathogenObservation> PrimaryObservations,
    List<CountTestReading> CountTestReadings)
{
    public static Task<PathogenSessionData> LoadAsync(IMicroLimsDbContext db, Sample sample);
}
```

- [ ] **Step 1: Write the failing test** (Review Focus 3): seed an InMemory context with two users (ids 5 and 6) and one incubation whose `StartedByUserId` is 5; call `LoadAsync`; assert `UserName(5)` returns the full name, `UserName(999)` returns `"Unknown"`, `UserName(null)` returns `"Unknown"`. Also assert user 6 was not needed by checking `UserName(6)` still returns `"Unknown"` (it is not referenced, so it is deliberately not loaded).
- [ ] **Step 2: Run it.** Expected: FAIL.
- [ ] **Step 3: Implement `LoadAsync`** by moving the queries from lines 273-281 and 349-372 verbatim. **One intentional change:** replace `_db.Users.ToDictionaryAsync(u => u.Id, u => u.FullName)` (line 284) with a query for only the referenced ids. The ids come from the loaded incubations' `StartedByUserId` plus any other `...UserId` this method later passes to `UserName(...)` (search the method body for `UserName(` to get the full list, then include each source), so load the users **after** the other rows:

```csharp
var userIds = incubations.Select(i => i.StartedByUserId)
    .Concat(/* every other source passed to UserName() in the method body */)
    .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
var users = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
string UserName(int? uid) => uid.HasValue && users.TryGetValue(uid.Value, out var n) ? n : "Unknown";
```

- [ ] **Step 4: Call it from the service** and delete the moved lines. Run the new test and FULL-SUITE.
- [ ] **Step 5: Commit** `refactor(pathogen-session): extract PathogenSessionData; load only referenced users`.

### Task 7: Extract `SharedTsbStateBuilder` (block E)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/PathogenSession/SharedTsbStateBuilder.cs`
- Modify: `PathogenSessionService.cs:381-492` (and move `DescribeTsbRanges`, 813)
- Test: `backend/MicroLIMS.Tests/UnitTests/SharedTsbStateBuilderTests.cs`

**Interfaces:**
- Consumes: `Sample`, `PathogenSessionData`, `int locationCount`, `DateTime utcNow`.
- Produces: `internal static SharedTsbResult Build(Sample sample, PathogenSessionData data, int locationCount, DateTime utcNow)` returning `(SharedTsbStateDto Dto, Dictionary<int, TestWorkflowStep> TsbStepByOrder, List<string> ApplicableCodes, Dictionary<int, (Incubation Incubation, IncubationWindow? Window, bool IsComplete)> OwnTsbByOrder)` as a small named record `SharedTsbResult`, because block F reads the last three.

- [ ] **Step 1: Write three failing tests** against the builder using hand-built entities and a fixed `utcNow`: (a) no shared TSB incubation: `IsStarted == false`, `ApplicableTestCodes` equals the codes with a TSB step; (b) shared incubation whose test has no resolvable window: `WindowNotConfigured == true`, `MinReadyAt == null` (Review Focus 4); (c) two tests joined to one shared start, one complete and one not: `IsCompleted == false`, `IsIncubating == true`.
- [ ] **Step 2: Run them.** Expected: FAIL.
- [ ] **Step 3: Move lines 381-492 verbatim** into `Build`, returning the record. `_time.GetUtcNow().UtcDateTime` becomes the `utcNow` parameter (the service passes `_time.GetUtcNow().UtcDateTime`; this is the only edit inside the block).
- [ ] **Step 4: Run new tests and FULL-SUITE**, then commit `refactor(pathogen-session): extract SharedTsbStateBuilder`.

### Task 8: Extract the assigned-test builder (block F)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/PathogenSession/AssignedTestBuilder.cs`
- Modify: `PathogenSessionService.cs:494-~800`

- [ ] **Step 1: Map block F.** Read lines 494-813 (use `mcp__agy-bridge__analyze_files` on the file with a 1-question prompt: "READ-ONLY: list the sub-blocks between lines 494 and 813 of GetSessionAsync, with start/end lines and the variables each reads from outside the block; under 300 words"). Append the result as a table under this task before editing code.
- [ ] **Step 2: For each sub-block the map identifies** (step DTO building at 511-537, workflow state at 539-546, count result at 548-, then whatever follows), repeat the Task 5 cycle: failing test with hand-built entities, extract verbatim into `AssignedTestBuilder`, replace in the service, FULL-SUITE, one commit each (`refactor(pathogen-session): extract <name>`).
- [ ] **Step 3: Success check.** `GetSessionAsync` is now under 120 lines and reads as: load, group locations, build TSB state, build each assigned test, assemble `PathogenTestingSessionDto`.

### Task 9: Split the other long methods in the same file

Methods over 100 lines: `StartSharedTsbCoreAsync` (827-1033, 207 lines), `SaveBatchConfirmatoryPlateReadingsAsync` (1326-1452, 127), `StartSharedConfirmatorySetupAsync` (1208-1325, 118).

- [ ] **Step 1:** for each, apply the same cycle: separate validation (pure, testable), data load, and mutation. Move validation into a private static method first (smallest diff), test it, commit. Only split the mutation block if the method is still over 100 lines.
- [ ] **Step 2:** these methods write audit rows and signatures; **do not reorder statements inside the mutation block**, only move whole contiguous blocks.

### Task 10: Tighten the file-size ratchet

**Files:**
- Modify: `backend/MicroLIMS.Tests/ArchitectureTests/SourceFileSizeTests.cs:11` (`MaxLines = 2000`)

- [ ] **Step 1:** after Task 9, run `git ls-files 'backend/MicroLIMS.Application/*.cs' | grep -v Migrations | xargs wc -l | sort -rn | head -3`. Set `MaxLines` to the next round number above the largest file (expected 1100 once `PathogenSessionService` is under it, since `DashboardService` is 1099).
- [ ] **Step 2:** FULL-SUITE, commit `test(architecture): lower the source file size limit`. Lower it again at the end of Phase 3.

---

## Phase 3 - The remaining god methods

Same recipe as Phase 2 for each service. For every one, the first step is a block map produced by agy (`analyze_files`, one service per call, "READ-ONLY, list sub-blocks with line ranges and external variables, under 300 words"), appended to this plan under the task. Then one extraction per block: failing test, verbatim move, FULL-SUITE, commit.

| Task | Service and method | Lines | Notes |
|---|---|---|---|
| 11 | `SampleSummaryService.GetSummaryAsync` | 92-656 | Also feeds PDF/Word export; export output must be byte-identical for a fixed sample, so add one test comparing `BuildReportLines` before and after on the same `SampleSummaryDto`. |
| 12 | `DashboardService.GetSectionHeadDashboardAsync` | 536-890 | Dashboard decisions are recorded in the dashboard-orientation memory (samples not tests, worst-of OOS, retest = in-progress spin-offs). Do not change counting rules; tests must assert the same numbers on a fixed dataset. |
| 13 | `GroupedTestActionService.GetActionableGroupsAsync` and `ExecuteBatchSelectMediaAsync` | 54-507, 544-873 | `ClusterByCompatibility` (507) is already separate; extend that pattern. |
| 14 | `DocumentApprovalService.ExecuteApprovalDecisionCoreAsync` and `GetApprovalDossierAsync` | 428-677, 239-397 | Decision path writes signatures and audit rows: move whole blocks only (see Global Constraints). `ValidateSegregationOfDuties` (677) and `ValidateDecisionSegregationOfDuties` (698) are good first extractions. |

- [ ] After Task 14: FULL-SUITE, then repeat Task 10 Step 1-2 to lower `MaxLines` again.

---

## Phase 4 - Guards and measurements (no behavior change)

### Task 15: Stop `IMicroLimsDbContext` drifting from `MicroLimsDbContext`

**Files:**
- Create: `backend/MicroLIMS.Tests/ArchitectureTests/DbContextPortTests.cs`

- [ ] **Step 1: Write the test** and run it; it is expected to fail now (164 vs 163):

```csharp
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Abstractions.Persistence;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.ArchitectureTests;

// IMicroLimsDbContext is the Application layer's view of the database.
// Every DbSet on the real context must be on the interface, and vice versa.
public class DbContextPortTests
{
    private static HashSet<string> DbSetNames(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
         .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
         .Select(p => p.Name)
         .ToHashSet();

    [Fact]
    public void EveryDbSetIsOnTheInterface_AndTheInterfaceHasNoExtras()
    {
        var real = DbSetNames(typeof(MicroLimsDbContext));
        var port = DbSetNames(typeof(IMicroLimsDbContext));

        Assert.Empty(real.Except(port));   // on the context but missing from the port
        Assert.Empty(port.Except(real));   // on the port but not implemented
    }
}
```

- [ ] **Step 2: The failure names the one extra or missing DbSet.** Fix the drift in whichever file is wrong (add the property to the interface, or remove the stray one; if removing, confirm with `git grep` that nothing uses it).
- [ ] **Step 3:** FULL-SUITE, commit `test(architecture): keep IMicroLimsDbContext in step with the real context`.

### Task 16: Measure the migration chain (investigation only, no changes)

**Files:**
- Create: `docs/superpowers/plans/2026-10-09-migration-chain-findings.md`

- [ ] **Step 1: Measure.** Record, in the findings file: file count and size of `Migrations/` (`du -sh`, `ls | wc -l`), the model snapshot size (502 KB), cold-start time of the API against an empty Postgres database applying all 154 migrations (stopwatch from process start to "Application started"; use a throwaway database name starting `microlims_scratch_`), and the same against a database already at head (time spent only checking the history table).
- [ ] **Step 2: Decide with the user.** The decision is data-driven: if applying from empty is only a development convenience cost and head-to-head startup is negligible, the answer is **do nothing**. Squashing is only worth discussing if it is measurably slow on the free-tier prod instance (see the Neon notes in memory). Do not squash, delete or edit any migration in this plan.
- [ ] **Step 3:** commit the findings document only.

---

## Phase 5 - Frontend

The frontend has only `testMasterArea.test.ts` covering these files, so safety comes from `tsc` (`npm run build`), `npm run lint`, `npm test`, and a browser check. All of these tasks are pure moves: no logic, prop or style changes.

Run from `frontend/`:

```bash
export TEMP=/e/MicroLIMS/rls-tmp TMP=/e/MicroLIMS/rls-tmp npm_config_cache=/e/MicroLIMS/rls-tmp/npm-cache
npm run build && npm run lint && npm test
```

### Task 17: Split `TestMasterPage.tsx` (2801 lines)

**Files:**
- Create: `frontend/src/modules/laboratoryConfiguration/masterDataSimple/testMaster/TestStageReplicatesSection.tsx` (from line 284-564)
- Create: `.../testMaster/WorkflowStepsSection.tsx` (from 565-1388, 824 lines)
- Create: `.../testMaster/testMasterConstants.ts` (constants 74-175 and helpers 176-283: `validateStepForm`, `stage1Ranges`, `stepNeedsConfiguration`, `ALL_STAGE_ROLES`)
- Modify: `TestMasterPage.tsx` (keeps only `TestMasterPage`, from 1389)

- [ ] **Step 1:** confirm which helpers each section uses (`grep -n` the names in each line range) and put a helper in `testMasterConstants.ts` only if two files use it.
- [ ] **Step 2:** move one block, add `export`, import it back in `TestMasterPage.tsx`, run `npm run build`. Repeat per block. One commit per block.
- [ ] **Step 3: Browser smoke (user or Claude in Chrome):** open Test Master, edit a micro test's workflow steps, open a physchem test's stage replicates. Compare with `main`.

### Task 18: Split `SampleSummaryDialog.tsx` (2108 lines)

**Files:**
- Create under `frontend/src/modules/testingWorkspace/sampleSummary/`: `ApprovalSignaturesCard.tsx` (1033-1542, 510 lines), `FinalResultBlock.tsx` (689-841), `TestResultsSection.tsx` (842-978), `AnalysisBlocks.tsx` (`AnalysisResultBlock`, `AnalysisReadingsTable`, 497-688), and `SummaryCards.tsx` (`SummaryField`, `SampleIdentityCard`, `SamplePreparationCard`, `IncubationStageBlock`, `LocationResultsTable`, 161-496)
- Modify: `SampleSummaryDialog.tsx` (keeps constants 132-160, `WorkflowHistoryStrip`, and `SampleSummaryDialog` from 1543)

- [ ] **Step 1-3:** same cycle as Task 17 (one block, build, commit). `ApprovalSignaturesCard` takes many props (`comment`, `decision`, `certificateRemarks`, `selectedTestOrderIds`, ...); export its props type from the new file rather than redefining it.
- [ ] **Step 4: Browser smoke:** open the summary of a sample under review, check the signature card, and open a certificate preview.

### Task 19: Reduce `any` in the five worst files

Files and counts: `MediaEvaluationWorkflowDialog.tsx` (18), `AnalystKpiService.ts` (14), `MediaEvaluationPage.tsx` (13), `ApprovedMediaListPage.tsx` (13), `TestWorkflowDialog.tsx` (11).

- [ ] **Step 1:** for each file, replace `any` with the DTO type the API client already exports (check `frontend/src/**/services/*.ts` first; do not invent types). If no type exists, define it next to the service from the backend DTO.
- [ ] **Step 2:** `npm run build` after each file; commit per file `refactor(frontend): type <file>`. Stop at these five; the remaining ~270 are not worth a campaign.

---

## Phase 6 - Close out

### Task 20: Record the outcome

- [ ] **Step 1:** update `docs/superpowers/plans/2026-10-09-technical-debt-remediation.md` by ticking the boxes that were done and noting anything skipped and why.
- [ ] **Step 2:** save a project memory summarizing: branch names, what was withdrawn from the assessment (EF port is deliberate, recorders already transactional), the lowered `MaxLines`, and the `settings.local.json` fix.
- [ ] **Step 3:** final FULL-SUITE and `npm run build && npm run lint && npm test`; report numbers. Do not push or open PRs unless asked.

---

## Decisions needed from you

- **D1: the remaining 58 controller catches.** Default: leave them. They are intentional status mappings. The only open question is the 20 `ArgumentException -> 400` arms; mapping that exception to 400 in `ExceptionMiddleware` would remove them but would also turn framework argument bugs into 400s. Default: do not.
- **D2: Task 4 scratch files**: delete, move, or ignore.
- **D3: execution order.** Recommended: Phase 0, 1, 2, then stop for review before Phase 3 and 5.

## Self-review

- **Coverage:** F1 -> Phases 2, 3; F2 -> Tasks 2, 6; F3 -> Task 3; F4 -> Task 1; F5 -> Phase 5; F6 -> Task 16; F7 -> Task 15; F8 -> D1. Withdrawn items are listed with reasons.
- **Placeholders:** Phases 3 and Task 8 start with a block-mapping step because those line ranges have not been read yet; the deliverable of that step is explicit and is appended to this file. Tasks 5-7 contain real code because their source lines were read.
- **Type consistency:** `PathogenSessionData`, `SharedTsbResult`, `SessionLocationGrouper.Group`, `SharedTsbStateBuilder.Build` and `AssignedTestBuilder` are named identically wherever they appear.
- **Review Focus:** items 1-2 -> Task 5, 3 -> Task 6, 4 -> Task 7, 5 -> Task 1.

---

## Execution log (2026-10-09, branch `refactor/debt-phase-1-2`, local only, nothing pushed)

Full suite after every commit: `Failed: 0, Skipped: 0`; final count 2792 passed (was 2766).

| Task | Status | What actually happened |
|---|---|---|
| 0 | Step 1 done; Steps 2-4 are yours | `MICROLIMS_TEST_POSTGRES` moved to a top-level `env` key in `.claude/settings.local.json`. A Claude Code restart is needed for it to take effect in new sessions. Browser smoke test and PR are still to do. |
| 1 | Done `92899966` | Records were lines 17-73 (not 20-90): 57 lines moved byte-for-byte to `TestWorkflowRequests.cs`. |
| 2 | Done (no change needed) | Only `PathogenSessionService.cs:284` was unfiltered. |
| 3 | Done `ae935726` | Three of four copies merged; `PeriodicReviewService` differs and was left alone. |
| 4 | Not started | Needs your decision on the scratch files. |
| 5 | Done `23e0881d` | `SessionLocationGrouper`, 4 tests. |
| 6 | Done `a1775e26` | `PathogenSessionData`; whole-table Users load now only referenced users, 2 tests. |
| 7 | Done `899017f6` | `SharedTsbStateBuilder`, 3 tests. |
| 8 | Done, split differently | Block F became four pieces instead of one `AssignedTestBuilder`: `ResultMatrixBuilder` (5 tests), `SessionStatusResolver` (5 tests), `AssignedTestStepBuilder` (4 tests) and a private `BuildCountResultAsync` (needs the database). `GetSessionAsync` went 566 -> 163 lines (target was ~120; the rest is the sample-graph query and DTO assembly). |
| 9 | Done | `StartSharedTsbCoreAsync` 207 -> 103, `StartSharedConfirmatorySetupAsync` 118 -> under 100, `SaveBatchConfirmatoryPlateReadingsAsync` 127 -> under 100. Only whole contiguous blocks were moved, no statement reordered. The mutation blocks that write audit rows were not touched. |
| 10 | Done | `SourceFileSizeTests.MaxLines` 2000 -> 1500. `PathogenSessionService` is still 1,404 lines (the file holds many other methods), so it cannot go lower until that file is split by responsibility. |

Mistake caught and fixed during execution: after extracting `DescribeTsbRanges` its comment was left above `StartSharedTsbAsync`; moved back in commit `ee405d25`.

Phases 3 and 5 are intentionally not started (stop for review).

## Execution log, Phase 3 (2026-10-09, same branch, local only)

Full suite after every commit: `Failed: 0, Skipped: 0`; final count 2816 passed.

| Task | Status | What actually happened |
|---|---|---|
| 11 | Done `60eb4391` (golden test) + `ae7ce46d` | Golden-output test first (3 golden JSON files: retest chain, every location branch, incubation stage pairs, analysis, scoped vs unscoped sections). `GetSummaryAsync` 565 -> 255 lines (the rest is data loading); new `Services/SampleSummary/` with `SampleSummaryData`, `SampleSummaryTestOrderBuilder`, `SampleSummarySectionsBuilder`, `SampleSummaryUserIds`. Anonymous analysis projection became named rows. Golden output unchanged byte-for-byte. |
| 12 | Done `bf270a71` | `Services/Dashboard/SectionHeadQueueBuilder.cs`: review queue, approval queue, 4 attention-item groups; `SampleDisplayName.For` replaces 5 copies. `GetSectionHeadDashboardAsync` ~355 -> ~215 lines (remaining: queries, analyst workloads). Counting rules untouched; existing dashboard tests unchanged and green. |
| 13 | Done `a596f78b` | `GetActionableGroupsAsync` 451 -> ~155 lines: `EvaluateOrderAsync` (per-order decision), `ActionableGroupBuilder` (grouping, clustering, permitted media). `ExecuteBatchSelectMediaAsync`: lot/incubator/result-entry preconditions in `BatchSelectMediaRules`; engine calls, history rows and saves not touched. |
| 14 | Done `42e44475` | `ExecuteApprovalDecisionCoreAsync` keeps guards + dispatch; the three branches are verbatim methods. Change-item/impact mappers moved to `DocumentDtoMapper` (also used by `DocumentRevisionService`); duplicated signature query is one helper. |

`MaxLines` not lowered again: the largest non-migration backend file is still `PathogenSessionService.cs` (1,404 lines), so 1,500 is already tight. Phases 4 and 5 not started.
