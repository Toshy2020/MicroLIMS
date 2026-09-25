# Laboratory Separation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Run the Microbiology and Physicochemical laboratories as separate labs on one shared sample: lab-targeted receipt, a Receiving area with a tracking board, per-lab outcomes (a rejection no longer closes the other lab; the other lab may close its testing), per-lab inventory and specification ownership, and the UI rename.

**Architecture:** Built on the existing section model — `DocumentSection` rows MICRO (id 1) and FP (id 2) are the two labs; `TestOrder.SectionId`, `SampleSectionSignoff` and `SampleSectionRollup` already carry per-lab state. `Sample.Status` stays the internal roll-up of *open* work (Rejected/Approved only when no lab is still open); a separate computed **overall status** drives the board, summary header and CoA. Server-side scoping (`IUserSectionScopeService`) remains the enforcement point; the menu only reflects it.

**Tech Stack:** ASP.NET Core (net10.0), EF Core + PostgreSQL, xUnit (EF InMemory + Postgres integration), React + TypeScript + MUI.

**Spec:** `docs/superpowers/specs/2026-09-24-lab-separation-design.md`

## Global Constraints

- Local only: branch `feat/fp-hplc-foundation`; never push; no production/Neon changes.
- Persisted enums are ints — **append** new members, never insert.
- Section codes are fixed: `MICRO` (Microbiology), `FP` (Physicochemical). `PreparationRules.NoPreparationSectionCode = "FP"` must keep working — the rename changes `DocumentSection.Name` only, never `Code`.
- Internal code names (`Fp…`, `WorkflowType` values) are not renamed. F.P / S.F production stages and the sample category "Finished Product" keep their names.
- Every migration is applied to LIMSV2 only after `pg_dump` to `E:/MicroLIMS/db-backups/LIMSV2_before_<name>_20260924.dump`. psql / pg_dump live in `/c/Program Files/PostgreSQL/18/bin/` (not on PATH); password from `backend/MicroLIMS.API/appsettings.Development.json`, never printed.
- The API may be running and locking `bin/`: build/test with `--artifacts-path <scratch dir>`; migrations with `dotnet ef migrations add <Name> --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release`.
- Each stage ends with the full Postgres suite: `bash .claude/scripts/run-postgres-tests.sh <artifacts>` (last baseline 1839/0/0) and, for frontend stages, `npm run build` in `frontend/` (tsc clean). Stop for the user after each stage.
- Every signed action goes through `ReviewGateService.SignAndLogAsync` (signature + `ReviewWorkflowEvent` in one save).
- Commit messages end with the two attribution lines from the session (Co-Authored-By / Claude-Session). Use a Bash heredoc (`git commit -F -`), not PowerShell.
- New privileges: `Samples.Receive`, `Samples.TrackAll` — seeded for System Administrator and Section Head only.

## Review Focus

1. **A lab keeps working after the other lab rejected** — reviewer expects the continuing lab can still auto-submit, be reviewed, approved, and that the sample only finalizes (archive, ResultRecord refresh, OOS propagation) when the last lab closes. Pinned in Task 2 (`Reject_OtherLabContinues_ReviewsAndApproves_SampleFinalizesRejected`).
2. **A section with no sign-off row after the other lab rejects** — `StatusOf` falls back to `Sample.Status`; a lab that never reached review must not read as Rejected. Pinned in Task 2 (`Reject_OtherLabNeverReachedReview_StaysInTesting`).
3. **Lab user smuggling another lab's id** into receipt or add-laboratory — expected 403. Pinned in Task 4 (`Receive_LabUserTargetsOtherLab_IsForbidden`) and Task 5.
4. **Adding Microbiology to a physicochemical-only sample that is already Ready** — micro tests need preparation; expected the sample goes back to NeedsPreparation while its physicochemical tests stay open. Pinned in Task 5 (`AddLab_MicroToReadyFpSample_NeedsPreparationAgain`).
5. **Closing testing when no lab rejected, or closing twice** — expected refusal with a clear message. Pinned in Task 3.

---

# STAGE 1 — Backend rules

### Task 1: Receive / track privileges

**Files:**
- Modify: `backend/MicroLIMS.Shared/Constants/PermissionConstants.cs`
- Modify: `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:465-500` (catalog) and `:520-540` (SectionHead grants)
- Create: migration `LabSeparationPrivileges` (via `dotnet ef migrations add`)
- Test: `backend/MicroLIMS.Tests/UnitTests/RolePermissionSeedDataTests.cs`

**Interfaces:**
- Produces: `PermissionConstants.SamplesReceive = "Samples.Receive"`, `PermissionConstants.SamplesTrackAll = "Samples.TrackAll"`; both in `PermissionConstants.All`.

- [ ] **Step 1: Write the failing test** — append to `RolePermissionSeedDataTests`:

```csharp
[Fact]
public void ReceiveAndTrackAll_AreSeededForAdminAndSectionHeadOnly()
{
    using var db = NewDb();
    SeedRoles(db);                     // use the file's existing role-seeding helper
    DbSeeder.SeedPermissionsAndGrants(db);

    string[] CodesFor(RoleType t) => db.RolePermissions
        .Where(rp => rp.Role!.Type == t).Select(rp => rp.Permission!.Code).ToArray();

    foreach (var code in new[] { PermissionConstants.SamplesReceive, PermissionConstants.SamplesTrackAll })
    {
        Assert.Contains(code, CodesFor(RoleType.SystemAdministrator));
        Assert.Contains(code, CodesFor(RoleType.SectionHead));
        Assert.DoesNotContain(code, CodesFor(RoleType.Reviewer));
        Assert.DoesNotContain(code, CodesFor(RoleType.Analyst));
    }
}
```
(Match the helper and navigation names the file already uses; if `RolePermission` has no navigations, join through `db.Permissions`/`db.Roles` the way the existing tests at lines ~90-165 do.)

- [ ] **Step 2: Run it — expect FAIL** (`SamplesReceive` not defined).
Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path <art> --filter FullyQualifiedName~RolePermissionSeedDataTests`

- [ ] **Step 3: Implement**

`PermissionConstants.cs` — add next to `SamplesApprove`:
```csharp
public const string SamplesReceive = "Samples.Receive";
public const string SamplesTrackAll = "Samples.TrackAll";
```
and add `SamplesReceive, SamplesTrackAll` to `All` after `SamplesApprove`.

`DbSeeder.cs` catalog:
```csharp
(PermissionConstants.SamplesReceive, "Receive samples for any laboratory on the main Receiving page and add a laboratory to a sample."),
(PermissionConstants.SamplesTrackAll, "View the sample tracking board across every laboratory."),
```
SectionHead grant array: add `PermissionConstants.SamplesReceive, PermissionConstants.SamplesTrackAll` after `SamplesApprove`. (Admin gets `All` automatically.)

- [ ] **Step 4: Migration for existing databases** — `dotnet ef migrations add LabSeparationPrivileges ...`; the generated model diff should be empty; write `Up`/`Down` by hand, following `20260909204150_AddSecurityAuditEvents.cs:91-117`:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    foreach (var (code, description) in new[]
    {
        ("Samples.Receive", "Receive samples for any laboratory on the main Receiving page and add a laboratory to a sample."),
        ("Samples.TrackAll", "View the sample tracking board across every laboratory.")
    })
    {
        migrationBuilder.Sql($@"
            INSERT INTO ""Permissions"" (""Code"", ""Description"", ""IsEnforced"")
            SELECT '{code}', '{description}', FALSE
            WHERE NOT EXISTS (SELECT 1 FROM ""Permissions"" WHERE ""Code"" = '{code}');");
        migrationBuilder.Sql($@"
            INSERT INTO ""RolePermissions"" (""RoleId"", ""PermissionId"")
            SELECT r.""Id"", p.""Id""
            FROM ""Roles"" r CROSS JOIN ""Permissions"" p
            WHERE r.""Type"" IN (0, 1) -- SystemAdministrator, SectionHead
              AND p.""Code"" = '{code}'
              AND NOT EXISTS (SELECT 1 FROM ""RolePermissions"" rp
                              WHERE rp.""RoleId"" = r.""Id"" AND rp.""PermissionId"" = p.""Id"");");
    }
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(@"DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN
        (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" IN ('Samples.Receive','Samples.TrackAll'));");
    migrationBuilder.Sql(@"DELETE FROM ""Permissions"" WHERE ""Code"" IN ('Samples.Receive','Samples.TrackAll');");
}
```

- [ ] **Step 5: Run tests — expect PASS** (same filter, plus `~PermissionAuthorizationTests`).
- [ ] **Step 6: Commit** — `feat(auth): Samples.Receive and Samples.TrackAll privileges`.

---

### Task 2: A rejection no longer closes the other lab; overall status

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/SampleSectionRollup.cs` (`Compute`, remove `CloseOtherSectionsOnReject`, add `FreezeOpenSections`, `Overall`)
- Modify: `backend/MicroLIMS.Application/Services/SampleApprovalService.cs:334-352` (Reject), `:555-575` (OOS origin), `:501-510` (finalization)
- Test: `backend/MicroLIMS.Tests/WorkflowTests/MultiSectionReviewApprovalTests.cs`

**Interfaces:**
- Produces:
  - `SampleSectionRollup.Overall(Sample sample) : OverallSampleStatus` — new enum in `backend/MicroLIMS.Domain/Enums/OverallSampleStatus.cs`: `InProgress, Approved, Rejected, RetestRequested, Voided, Cancelled` (not persisted — order free, but keep this order).
  - `SampleSectionRollup.FreezeOpenSections(Sample sample)` — creates a sign-off row for every section that has none, capturing its current shared state.
  - `SampleApprovalService.FinalizeIfClosedAsync(Sample sample, string archiveLabel, int userId)` (private → `internal`, used again by Task 3).

- [ ] **Step 1: Rewrite the existing rejection test and add the new ones.** Replace `Reject_ByOneSection_RejectsTheSampleAndClosesTheOtherSection` (line ~243) with:

```csharp
[Fact]
public async Task Reject_ByOneLab_OverallRejected_OtherLabStaysOpen()
{
    await using var db = NewDb();
    var w = await SeedUnderApprovalAsync(db);
    var approval = TestServiceFactory.SampleApproval(db);

    await approval.DecideAsync(w.Sample.Id, w.HeadMicro, Password, ApprovalDecision.Reject, "Out of limits", null);

    var sample = await db.Samples.Include(s => s.TestOrders).Include(s => s.SectionSignoffs).FirstAsync(s => s.Id == w.Sample.Id);
    Assert.Equal(OverallSampleStatus.Rejected, SampleSectionRollup.Overall(sample));
    Assert.Equal(SampleStatus.UnderApproval, sample.Status);                   // FP still waiting for approval
    Assert.Equal(SectionSignoffStatus.UnderApproval, (await SignoffAsync(db, w.Sample.Id, w.Fp))!.Status);
    Assert.Equal(ApprovalStatus.Reviewed, sample.TestOrders.First(t => t.Id == w.FpAssay.Id).Status);
}

[Fact]
public async Task Reject_OtherLabContinues_ReviewsAndApproves_SampleFinalizesRejected()
{
    await using var db = NewDb();
    var w = await SeedAsync(db, fpReady: false);
    var review = TestServiceFactory.SampleReview(db);
    var approval = TestServiceFactory.SampleApproval(db);
    await review.AutoSubmitForReviewIfReadyAsync(w.Sample.Id, 1);
    await db.SaveChangesAsync();
    await review.CompleteReviewAsync(w.Sample.Id, w.ReviewerMicro, Password, null, null);
    await approval.DecideAsync(w.Sample.Id, w.HeadMicro, Password, ApprovalDecision.Reject, "Out of limits", null);

    // FP finishes after the rejection and still goes through its own review and approval.
    var assay = await db.TestOrders.FirstAsync(t => t.Id == w.FpAssay.Id);
    assay.Status = ApprovalStatus.ResultEntered;
    assay.CurrentStep = WorkflowStep.Ready;
    await db.SaveChangesAsync();
    await review.AutoSubmitForReviewIfReadyAsync(w.Sample.Id, 2);
    await db.SaveChangesAsync();
    Assert.Equal(SectionSignoffStatus.UnderReview, (await SignoffAsync(db, w.Sample.Id, w.Fp))!.Status);

    await review.CompleteReviewAsync(w.Sample.Id, w.ReviewerFp, Password, null, null);
    await approval.DecideAsync(w.Sample.Id, w.HeadFp, Password, ApprovalDecision.Approve, null, null);

    Assert.Equal(SampleStatus.Rejected, await StatusAsync(db, w.Sample.Id));
    Assert.Equal(SectionSignoffStatus.Approved, (await SignoffAsync(db, w.Sample.Id, w.Fp))!.Status);
    Assert.True(await db.RecordArchives.AnyAsync(a => a.EntityId == w.Sample.Id));   // use the archive table name the Archive service writes
}

[Fact]
public async Task Reject_OtherLabNeverReachedReview_StaysInTesting()
{
    await using var db = NewDb();
    var w = await SeedAsync(db, fpReady: false);
    var review = TestServiceFactory.SampleReview(db);
    await review.AutoSubmitForReviewIfReadyAsync(w.Sample.Id, 1);
    await db.SaveChangesAsync();
    await review.CompleteReviewAsync(w.Sample.Id, w.ReviewerMicro, Password, null, null);

    await TestServiceFactory.SampleApproval(db).DecideAsync(w.Sample.Id, w.HeadMicro, Password, ApprovalDecision.Reject, null, null);

    Assert.Equal(SectionSignoffStatus.InTesting, (await SignoffAsync(db, w.Sample.Id, w.Fp))!.Status);
    Assert.Equal(SampleStatus.InTesting, await StatusAsync(db, w.Sample.Id));
}
```
Keep `Reject_KeepsAnotherSectionsEarlierApproval` but change its sample-status assertion: FP approved first, then micro rejects → no lab open → `SampleStatus.Rejected` (unchanged expectation) — verify it still holds.

Also add to the same class, pure roll-up cases:
```csharp
[Theory]
[InlineData(new[] { SectionSignoffStatus.Rejected, SectionSignoffStatus.UnderReview }, SampleStatus.UnderReview)]
[InlineData(new[] { SectionSignoffStatus.Rejected, SectionSignoffStatus.Approved }, SampleStatus.Rejected)]
[InlineData(new[] { SectionSignoffStatus.Rejected, SectionSignoffStatus.Cancelled }, SampleStatus.Rejected)]
[InlineData(new[] { SectionSignoffStatus.Approved, SectionSignoffStatus.Approved }, SampleStatus.Approved)]
[InlineData(new[] { SectionSignoffStatus.Rejected, SectionSignoffStatus.RetestRequested }, SampleStatus.Rejected)]
[InlineData(new[] { SectionSignoffStatus.RetestRequested, SectionSignoffStatus.Approved }, SampleStatus.RetestRequested)]
public void Compute_OpenLabsWinOverRejection(SectionSignoffStatus[] statuses, SampleStatus expected) =>
    Assert.Equal(expected, SampleSectionRollup.Compute(statuses));
```

- [ ] **Step 2: Run — expect FAIL** (compile: `OverallSampleStatus`, `Overall` missing).
Run: `dotnet test ... --filter FullyQualifiedName~MultiSectionReviewApprovalTests`

- [ ] **Step 3: Implement the roll-up** in `SampleSectionRollup.cs`:

```csharp
// Sample.Status is the roll-up of OPEN work: while any lab is still
// testing, in review or in approval, the sample sits at the least advanced
// of them - even after another lab rejected - because the rest of the
// system treats Rejected/Approved as "closed". What users see as the
// sample's outcome is Overall(), where a rejection wins at once.
public static SampleStatus? Compute(IReadOnlyCollection<SectionSignoffStatus> statuses)
{
    if (statuses.Count == 0) return null;
    if (statuses.All(s => s == SectionSignoffStatus.Voided)) return SampleStatus.Voided;
    if (statuses.Contains(SectionSignoffStatus.InTesting)) return SampleStatus.InTesting;
    if (statuses.Contains(SectionSignoffStatus.UnderReview)) return SampleStatus.UnderReview;
    if (statuses.Contains(SectionSignoffStatus.UnderApproval)) return SampleStatus.UnderApproval;
    if (statuses.Contains(SectionSignoffStatus.Rejected)) return SampleStatus.Rejected;
    if (statuses.Contains(SectionSignoffStatus.RetestRequested)) return SampleStatus.RetestRequested;
    return SampleStatus.Approved;
}

public static OverallSampleStatus Overall(Sample sample)
{
    if (sample.Status == SampleStatus.Voided) return OverallSampleStatus.Voided;
    if (sample.Status == SampleStatus.Cancelled) return OverallSampleStatus.Cancelled;
    var statuses = SectionIds(sample).Select(id => StatusOf(sample, id)).ToList();
    if (statuses.Contains(SectionSignoffStatus.Rejected)) return OverallSampleStatus.Rejected;
    if (statuses.Any(IsOpen)) return OverallSampleStatus.InProgress;
    if (statuses.Contains(SectionSignoffStatus.RetestRequested)) return OverallSampleStatus.RetestRequested;
    return OverallSampleStatus.Approved;
}

// Called before a decision changes Sample.Status: every section without a
// sign-off row shares the sample's state, so give each one its own row
// first - otherwise a lab that never reached review would read as Rejected.
public static void FreezeOpenSections(Sample sample)
{
    foreach (var id in SectionIds(sample))
        GetOrAdd(sample, id);
}
```
Delete `CloseOtherSectionsOnReject` entirely.

`OverallSampleStatus.cs`:
```csharp
namespace MicroLIMS.Domain.Enums;

// What a sample's outcome reads as across every laboratory - computed,
// never stored. A rejection by any lab wins at once, while Sample.Status
// keeps following the labs still at work.
public enum OverallSampleStatus { InProgress, Approved, Rejected, RetestRequested, Voided, Cancelled }
```

- [ ] **Step 4: Rewire `SampleApprovalService`.**
  - At the top of `DecideAsync`, after `signoff` is resolved (line ~142): `SampleSectionRollup.FreezeOpenSections(sample);`
  - `case ApprovalDecision.Reject:` — remove the `CloseOtherSectionsOnReject(...)` call; keep the rest.
  - OOS origin path (line ~570): remove the `if (outcome != Approve) CloseOtherSectionsOnReject(...)`; call `SampleSectionRollup.FreezeOpenSections(origin);` before `originSignoff.Status = ...`.
  - Extract lines ~501-510 into:
```csharp
// A sample is final once no lab is still open. The decision that closes it
// may be another lab's approval after an earlier rejection, so the OOS
// outcome follows the sample's own final state, not this one decision.
internal async Task FinalizeIfClosedAsync(Sample sample, string archiveLabel, int userId)
{
    if (sample.Status is not (SampleStatus.Approved or SampleStatus.Rejected)) return;

    await _resultProjection.RefreshApprovalFieldsAsync(sample.Id);
    var document = await _summary.BuildReportDocumentAsync(sample.Id);
    if (document is not null)
        await _archive.ArchiveAsync(ReviewEntityTypes.Sample, sample.Id, document, archiveLabel, userId);

    var outcome = sample.Status == SampleStatus.Rejected ? ApprovalDecision.Reject : ApprovalDecision.Approve;
    await PropagateOosOutcomeAsync(sample, outcome, userId);
}
```
    and call `await FinalizeIfClosedAsync(sample, $"Sample {decision}", sectionHeadUserId);` where the block was.
  - In `case ApprovalDecision.Approve`, `sample.ApprovalDecision = Approve` is only set when `sample.Status == Approved` — leave; and in `case Reject` keep `sample.ApprovalDecision = ApprovalDecision.Reject` (the board reads it).

- [ ] **Step 5: Run the class — expect PASS**, then `--filter FullyQualifiedName~Approval|FullyQualifiedName~Review|FullyQualifiedName~Oos` to catch callers relying on the old close.
- [ ] **Step 6: Commit** — `feat(review): one lab's rejection no longer closes the other lab`.

---

### Task 3: Close testing (the other lab stops after a rejection)

**Files:**
- Modify: `backend/MicroLIMS.Domain/Enums/ApprovalStatus.cs` (append `Cancelled`)
- Modify: `backend/MicroLIMS.Domain/Enums/SignatureMeaning.cs` (append `TestingClosed`)
- Modify: `backend/MicroLIMS.Domain/Enums/ReviewWorkflowEventType.cs` (append `SectionTestingClosed`)
- Modify: `backend/MicroLIMS.Domain/Entities/TestOrder.cs` (add `CancelledAtStep`, `CancelledAtStage`)
- Modify: `backend/MicroLIMS.Domain/Entities/SampleSectionSignoff.cs` (add `ClosedByUserId`, `ClosedAt`, `CloseReason`, `CloseSignatureId`/`CloseSignature`)
- Create: `backend/MicroLIMS.Application/Services/SectionClosureService.cs`
- Modify: `backend/MicroLIMS.API/Controllers/SampleApprovalController.cs` (new action)
- Modify: `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs` (register service)
- Modify: `backend/MicroLIMS.Tests/TestServiceFactory.cs` (factory)
- Create: migration `LabSeparationCloseTesting`
- Test: `backend/MicroLIMS.Tests/WorkflowTests/SectionClosureTests.cs`

**Interfaces:**
- Consumes: `SampleSectionRollup.FreezeOpenSections`, `Overall`, `SampleApprovalService.FinalizeIfClosedAsync` (Task 2).
- Produces: `SectionClosureService.CloseTestingAsync(int sampleId, int sectionId, int sectionHeadUserId, string password, string reason, string? ipAddress) : Task`; endpoint `POST api/samples/{id}/approval/sections/{sectionId}/close` with body `CloseSectionTestingRequest(string Password, string Reason)`.

- [ ] **Step 1: Write failing tests** — `SectionClosureTests.cs`. Copy the `NewDb`, `SeedUserAsync`, `World`, `SeedAsync` helpers from `MultiSectionReviewApprovalTests` (they are private there; duplicate rather than refactor). Tests:

```csharp
[Fact]
public async Task Close_AfterOtherLabRejected_CancelsOpenTests_KeepsStage_SampleFinalizes()
{
    await using var db = NewDb();
    var w = await SeedAsync(db, fpReady: false);             // FP assay Incubating
    db.Incubations.Add(new Incubation { TestOrderId = w.FpAssay.Id, StageNumber = 2, StartedAt = DateTime.UtcNow, StartedByUserId = 2 });
    await db.SaveChangesAsync();
    await RejectMicroAsync(db, w);                            // helper: auto-submit, review, reject by HeadMicro

    await TestServiceFactory.SectionClosure(db).CloseTestingAsync(w.Sample.Id, w.Fp, w.HeadFp, Password, "Sample rejected by Microbiology Laboratory", null);

    var order = await db.TestOrders.AsNoTracking().FirstAsync(t => t.Id == w.FpAssay.Id);
    Assert.Equal(ApprovalStatus.Cancelled, order.Status);
    Assert.Equal(WorkflowStep.Incubating, order.CancelledAtStep);
    Assert.Equal(2, order.CancelledAtStage);
    var signoff = (await db.SampleSectionSignoffs.AsNoTracking().FirstAsync(s => s.SampleId == w.Sample.Id && s.SectionId == w.Fp));
    Assert.Equal(SectionSignoffStatus.Cancelled, signoff.Status);
    Assert.Equal(w.HeadFp, signoff.ClosedByUserId);
    Assert.Equal("Sample rejected by Microbiology Laboratory", signoff.CloseReason);
    Assert.NotNull(signoff.CloseSignatureId);
    Assert.Equal(SampleStatus.Rejected, (await db.Samples.AsNoTracking().FirstAsync(s => s.Id == w.Sample.Id)).Status);
    Assert.True(await db.ReviewWorkflowEvents.AnyAsync(e => e.EntityId == w.Sample.Id && e.EventType == ReviewWorkflowEventType.SectionTestingClosed && e.SectionId == w.Fp));
}

[Fact]
public async Task Close_WhenNoLabRejected_IsRefused()
{
    await using var db = NewDb();
    var w = await SeedAsync(db, fpReady: false);
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        TestServiceFactory.SectionClosure(db).CloseTestingAsync(w.Sample.Id, w.Fp, w.HeadFp, Password, "x", null));
    Assert.Contains("only after another laboratory rejected", ex.Message);
}

[Fact]
public async Task Close_Twice_IsRefused() { /* close once as above, second call throws "already closed" */ }

[Fact]
public async Task Close_ByOtherLabsHead_IsForbidden()
{
    // HeadMicro tries to close the FP section -> UnauthorizedAccessException
}

[Fact]
public async Task Close_KeepsResultsTheLabAlreadyApproved() { /* FP approved before micro rejects -> FP not open -> refused: "has no open tests" */ }

[Fact]
public async Task Close_RequiresReason() { /* blank reason -> InvalidOperationException "A reason is required" */ }
```
Write the four sketched tests out fully in the same style before running.

- [ ] **Step 2: Run — expect FAIL** (compile).

- [ ] **Step 3: Domain changes.**
  - `ApprovalStatus`: append after `Voided`:
```csharp
    // Appended: persisted as int. This lab stopped testing because another
    // lab rejected the sample - no judgement on this test's material.
    Cancelled
```
  - `SignatureMeaning`: append `TestingClosed`. `ReviewWorkflowEventType`: append `SectionTestingClosed`.
  - `TestOrder`:
```csharp
    // Set when the lab closes its testing after another lab rejected the
    // sample: the step (and incubation stage, when there was one) the test
    // had reached, shown in the sample summary.
    public WorkflowStep? CancelledAtStep { get; set; }
    public int? CancelledAtStage { get; set; }
```
  - `SampleSectionSignoff`:
```csharp
    // Close testing (another lab rejected the sample).
    public int? ClosedByUserId { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? CloseReason { get; set; }
    public int? CloseSignatureId { get; set; }
    public ElectronicSignature? CloseSignature { get; set; }
```
  - Configure `CloseSignature` FK in the signoff's EF configuration exactly like `ApprovalSignature` (find it with `git grep -n "ApprovalSignature" backend/MicroLIMS.Persistence`), `OnDelete(DeleteBehavior.Restrict)`.

- [ ] **Step 4: Service** — `SectionClosureService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// After one laboratory rejects a sample, another laboratory may stop its
// own work instead of finishing it. Its open tests are cancelled - not
// rejected, nothing was judged - each keeping the step it had reached.
public class SectionClosureService
{
    private readonly MicroLimsDbContext _db;
    private readonly ReviewGateService _reviewGate;
    private readonly IUserSectionScopeService _scope;
    private readonly SampleApprovalService _approval;

    public SectionClosureService(MicroLimsDbContext db, ReviewGateService reviewGate, IUserSectionScopeService scope, SampleApprovalService approval)
    {
        _db = db;
        _reviewGate = reviewGate;
        _scope = scope;
        _approval = approval;
    }

    public async Task CloseTestingAsync(int sampleId, int sectionId, int sectionHeadUserId, string password, string reason, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required to close testing.");

        var sample = await _db.Samples.Include(s => s.TestOrders).Include(s => s.SectionSignoffs)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        var userScope = await _scope.GetAccessibleSectionIdsAsync(sectionHeadUserId);
        if (userScope is not null && !userScope.Contains(sectionId))
            throw new UnauthorizedAccessException("This test belongs to a laboratory section you are not assigned to.");
        if (!SampleSectionRollup.SectionIds(sample).Contains(sectionId))
            throw new InvalidOperationException("This sample has no tests in the selected laboratory section.");

        var otherRejected = SampleSectionRollup.SectionIds(sample)
            .Any(id => id != sectionId && SampleSectionRollup.StatusOf(sample, id) == SectionSignoffStatus.Rejected);
        if (!otherRejected)
            throw new InvalidOperationException("Testing can be closed only after another laboratory rejected the sample.");

        var status = SampleSectionRollup.StatusOf(sample, sectionId);
        if (status == SectionSignoffStatus.Cancelled)
            throw new InvalidOperationException("This laboratory's testing is already closed.");
        if (!SampleSectionRollup.IsOpen(status))
            throw new InvalidOperationException("This laboratory has no open tests on the sample.");

        SampleSectionRollup.FreezeOpenSections(sample);

        var signature = await _reviewGate.SignAndLogAsync(
            ReviewEntityTypes.Sample, sampleId, sectionHeadUserId, password,
            SignatureMeaning.TestingClosed, ReviewWorkflowEventType.SectionTestingClosed,
            reason.Trim(), ipAddress, null, sectionId);

        var orderIds = SampleSectionRollup.CurrentOrders(sample, sectionId).Select(o => o.Id).ToList();
        var stageByOrder = await _db.Incubations
            .Where(i => orderIds.Contains(i.TestOrderId))
            .GroupBy(i => i.TestOrderId)
            .Select(g => new { g.Key, Stage = g.Max(i => i.StageNumber) })
            .ToDictionaryAsync(x => x.Key, x => x.Stage);

        foreach (var order in SampleSectionRollup.CurrentOrders(sample, sectionId))
        {
            if (order.Status is ApprovalStatus.Approved or ApprovalStatus.Voided or ApprovalStatus.Cancelled) continue;
            order.CancelledAtStep = order.CurrentStep;
            order.CancelledAtStage = stageByOrder.TryGetValue(order.Id, out var stage) ? stage : null;
            order.Status = ApprovalStatus.Cancelled;
            _db.WorkflowHistories.Add(new WorkflowHistory
            {
                TestOrderId = order.Id,
                FromStep = order.CurrentStep,
                ToStep = order.CurrentStep,
                Note = $"Testing closed by {signature.UserFullNameSnapshot}: {reason.Trim()}",
                PerformedByUserId = sectionHeadUserId
            });
        }

        var signoff = SampleSectionRollup.GetOrAdd(sample, sectionId);
        signoff.Status = SectionSignoffStatus.Cancelled;
        signoff.ClosedByUserId = sectionHeadUserId;
        signoff.ClosedAt = DateTime.UtcNow;
        signoff.CloseReason = reason.Trim();
        signoff.CloseSignature = signature;
        SampleSectionRollup.Apply(sample);

        await _db.SaveChangesAsync();
        await _approval.FinalizeIfClosedAsync(sample, "Laboratory testing closed", sectionHeadUserId);
    }
}
```
(Check `SignAndLogAsync`'s parameter order at `ReviewGateService.cs:32` — `decision` before `sectionId`. Check that `Incubation.TestOrderId` is the FK name; adjust if it is nullable.)

- [ ] **Step 5: Wire up.** Register `services.AddScoped<SectionClosureService>();` next to `SampleApprovalService` in `ServiceCollectionExtensions.cs`. Factory in `TestServiceFactory`:
```csharp
public static SectionClosureService SectionClosure(MicroLimsDbContext db) =>
    new(db, ReviewGate(db), new UserSectionScopeService(db), SampleApproval(db));
```
Controller (`SampleApprovalController`, class-level `[Authorize(Roles = SectionHead, SystemAdministrator)]` already applies):
```csharp
public record CloseSectionTestingRequest(string Password, string Reason);

[HttpPost("sections/{sectionId:int}/close")]
public async Task<IActionResult> CloseTesting(int id, int sectionId, CloseSectionTestingRequest request)
{
    await _closureService.CloseTestingAsync(id, sectionId, CurrentUserId, request.Password, request.Reason,
        HttpContext.Connection.RemoteIpAddress?.ToString());
    return Ok(ApiResponse<object>.Ok(new { }));
}
```
(inject `SectionClosureService _closureService` via the constructor; `InvalidOperationException`/`UnauthorizedAccessException` are already mapped globally — confirm by checking how `Decide` surfaces them.)

- [ ] **Step 6: Migration** — `dotnet ef migrations add LabSeparationCloseTesting ...`. Inspect: 2 nullable columns on `TestOrders`, 4 nullable columns + FK + index on `SampleSectionSignoffs`, nothing else. Down drops them.
- [ ] **Step 7: Run tests — expect PASS** (`~SectionClosureTests`, `~MultiSectionReviewApprovalTests`).
- [ ] **Step 8: Check frontend enum consumers compile** — `git grep -n "\"Voided\"" frontend/src` for exhaustive `ApprovalStatus` maps; add a `Cancelled: "Cancelled"` label where a `Record<...>` requires every member. `npm run build` in `frontend/`.
- [ ] **Step 9: Commit** — `feat(review): a laboratory can close its testing after another lab rejected`.

---

### Task 4: Lab-targeted receipt

**Files:**
- Modify: `backend/MicroLIMS.Application/Workflows/ProductWorkflowEngine.cs` (`ItemBasedReceiveRequest`, `ReceiveAsync`)
- Create: `backend/MicroLIMS.Application/Services/ReceiptLabGuard.cs`
- Modify: `backend/MicroLIMS.API/Controllers/SampleController.cs:13-25` (request), `:67-83` (Receive), new `GET receipt-labs`
- Test: `backend/MicroLIMS.Tests/WorkflowTests/LabTargetedReceiptTests.cs`

**Interfaces:**
- Produces:
  - `ItemBasedReceiveRequest(... existing ..., IReadOnlyCollection<int>? TargetSectionIds = null)` — `null` keeps today's "every assigned test" behaviour for internal callers; the HTTP endpoint always passes a set.
  - `ReceiptLabGuard.ResolveTargetsAsync(MicroLimsDbContext db, IUserSectionScopeService scope, int userId, bool canReceiveForAnyLab, IReadOnlyCollection<int>? requested) : Task<IReadOnlyCollection<int>>` — throws `InvalidOperationException` on empty, `UnauthorizedAccessException` on a lab the user may not target.
  - `GET api/samples/receipt-labs?itemId=` → `List<ReceiptLabOptionDto>` where `record ReceiptLabOptionDto(int SectionId, string SectionCode, string SectionName, int TestCount)`.
  - HTTP body `ReceiveItemBasedSampleRequest` gains `List<int> TargetSectionIds`.

- [ ] **Step 1: Failing tests** (`LabTargetedReceiptTests.cs`, EF InMemory; seed sections MICRO/FP, `TestDefinition` rows `TAMC` (MICRO) and `ASSAY` (FP), an FP item with both assigned):

```csharp
[Fact]
public async Task Receive_OnlyPhysicochemical_CreatesOnlyItsOrders_AndSampleIsReady()
{
    var (db, item, micro, fp) = await SeedAsync();
    var engine = new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db));
    var sample = await engine.ReceiveAsync(Request(item.Id, new[] { fp.Id }));
    Assert.Equal(new[] { "ASSAY" }, sample.TestOrders.Select(t => t.TestCode));
    Assert.Equal(SamplePreparationStatus.Ready, sample.PreparationStatus);
}

[Fact]
public async Task Receive_BothLabs_CreatesAllOrders() { /* {micro.Id, fp.Id} -> TAMC + ASSAY, NeedsPreparation */ }

[Fact]
public async Task Receive_TargetLabWithNoTestsForItem_IsRefused()
{
    // item with only ASSAY assigned, target {micro.Id} ->
    // InvalidOperationException "has no tests for Microbiology Laboratory"
}

[Fact]
public async Task Receive_NullTargets_KeepsEveryAssignedTest() { /* backward compatibility */ }

[Fact]
public async Task Guard_EmptyTargets_IsRefused() { /* ResolveTargetsAsync(..., requested: []) -> "Choose at least one laboratory" */ }

[Fact]
public async Task Receive_LabUserTargetsOtherLab_IsForbidden()
{
    // user member of MICRO only, canReceiveForAnyLab false, requested {fp.Id}
    // -> UnauthorizedAccessException
}

[Fact]
public async Task Guard_ReceiverWithPrivilege_MayTargetAnyLab() { /* canReceiveForAnyLab true, no membership -> returns {micro, fp} */ }
```
`Request(...)` helper: `new ItemBasedReceiveRequest(itemId, 1, "10 units", "Analyst", "LOT-1", "CTRL-1", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, 1, targets)`.

- [ ] **Step 2: Run — expect FAIL.**

- [ ] **Step 3: Engine.** Add the optional record parameter, then in `ReceiveAsync` replace the order-building loop:

```csharp
var testSections = await TestSectionLookup.ResolveAsync(_db, item.AssignedTests.Select(t => t.TestCode));

var assigned = item.AssignedTests.ToList();
if (request.TargetSectionIds is { } targets)
{
    foreach (var sectionId in targets)
    {
        if (!assigned.Any(t => testSections[t.TestCode] == sectionId))
        {
            var name = await _db.DocumentSections.Where(s => s.Id == sectionId).Select(s => s.Name).FirstOrDefaultAsync() ?? $"section {sectionId}";
            throw new InvalidOperationException($"Item '{item.Name}' has no tests for {name}.");
        }
    }
    assigned = assigned.Where(t => targets.Contains(testSections[t.TestCode])).ToList();
}

foreach (var test in assigned)
{
    sample.TestOrders.Add(new TestOrder
    {
        TestCode = test.TestCode,
        SectionId = testSections[test.TestCode],
        Status = ApprovalStatus.Pending,
        CurrentStep = WorkflowStep.Waiting
    });
}

sample.PreparationStatus = await PreparationRules.InitialStatusAsync(_db, sample.TestOrders.Select(o => o.SectionId));
```

- [ ] **Step 4: Guard** — `ReceiptLabGuard.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// Which laboratories a receipt may create tests for. The main Receiving
// page (Samples.Receive) may target any active laboratory; receiving from
// inside a lab's own workspace may target only the labs the user is a
// member of, so a lab can never create another lab's work.
public static class ReceiptLabGuard
{
    public static async Task<IReadOnlyCollection<int>> ResolveTargetsAsync(
        MicroLimsDbContext db, IUserSectionScopeService scope, int userId,
        bool canReceiveForAnyLab, IReadOnlyCollection<int>? requested)
    {
        var ids = requested?.Distinct().ToList() ?? new List<int>();
        if (ids.Count == 0)
            throw new InvalidOperationException("Choose at least one laboratory to receive the sample for.");

        var known = await db.DocumentSections.Where(s => ids.Contains(s.Id) && s.IsActive).Select(s => s.Id).ToListAsync();
        if (known.Count != ids.Count)
            throw new InvalidOperationException("One or more selected laboratories do not exist.");

        if (!canReceiveForAnyLab)
        {
            var userScope = await scope.GetAccessibleSectionIdsAsync(userId);
            if (userScope is not null && ids.Any(id => !userScope.Contains(id)))
                throw new UnauthorizedAccessException("You can receive samples only for your own laboratory.");
        }
        return ids;
    }
}
```

- [ ] **Step 5: Controller.** Add `List<int>? TargetSectionIds` as the last member of `ReceiveItemBasedSampleRequest`. In `Receive`:
```csharp
var targets = await ReceiptLabGuard.ResolveTargetsAsync(_db, _scopeService, CurrentUserId,
    User.HasClaim("permission", PermissionConstants.SamplesReceive), request.TargetSectionIds);
var sample = await _receivingService.ReceiveSampleAsync(new ItemBasedReceiveRequest(
    request.ItemId, request.CauseOfTestingId, request.SampleQuantity, request.SampledBy,
    request.BatchNumber, request.ControlNumber, request.MfgDate, request.ExpDate,
    request.ProductionStage, CurrentUserId, targets));
```
(inject `MicroLimsDbContext _db` if the controller has none; keep the existing try/catch; add `catch (UnauthorizedAccessException) { return Forbid(); }` if not globally mapped.) The item-based endpoint only serves FP/RM/PM items (Item.Category), so the category limit of the main Receiving page holds server-side already — Water/EM/After-cleaning use `/water|/em|/aftercleaning/receive`, which stay Microbiology-only in the UI.

New endpoint:
```csharp
[HttpGet("receipt-labs")]
public async Task<IActionResult> ReceiptLabs([FromQuery] int itemId)
{
    var codes = await _db.Items.Where(i => i.Id == itemId).SelectMany(i => i.AssignedTests.Select(t => t.TestCode)).ToListAsync();
    var rows = await _db.TestDefinitions.Where(t => codes.Contains(t.Code))
        .GroupBy(t => new { t.SectionId, t.Section!.Code, t.Section.Name })
        .Select(g => new ReceiptLabOptionDto(g.Key.SectionId, g.Key.Code, g.Key.Name, g.Count()))
        .ToListAsync();
    return Ok(ApiResponse<object>.Ok(rows));
}
```
(`ReceiptLabOptionDto` in `backend/MicroLIMS.Application/DTOs/ReceiptLabOptionDto.cs`.)

- [ ] **Step 6: Run tests — expect PASS**, plus `~ProductionStageRoleTests` (unchanged callers pass `null`).
- [ ] **Step 7: Commit** — `feat(receiving): receive a sample for chosen laboratories only`.

---

### Task 5: Add laboratory to an existing sample

**Files:**
- Create: `backend/MicroLIMS.Application/Services/AddLaboratoryService.cs`
- Modify: `backend/MicroLIMS.Domain/Enums/SignatureMeaning.cs` (append `LaboratoryAdded`), `ReviewWorkflowEventType.cs` (append `LaboratoryAdded`)
- Modify: `backend/MicroLIMS.API/Controllers/SampleController.cs` (new action), `ServiceCollectionExtensions.cs`, `TestServiceFactory.cs`
- Test: `backend/MicroLIMS.Tests/WorkflowTests/AddLaboratoryTests.cs`

**Interfaces:**
- Consumes: `SampleSectionRollup.Overall`, `Apply` (Task 2), `TestSectionLookup`, `PreparationRules`.
- Produces: `AddLaboratoryService.AddAsync(int sampleId, int sectionId, int userId, string password, string reason, string? ipAddress) : Task`; endpoint `POST api/samples/{id}/laboratories` `[Authorize(Policy = PermissionConstants.SamplesReceive)]`, body `AddLaboratoryRequest(int SectionId, string Password, string Reason)`.

- [ ] **Step 1: Failing tests:**
```csharp
[Fact] AddLab_CreatesTheLabsOrdersOnTheSameSample()          // FP-only sample + add MICRO -> TAMC order, same ReferenceNumber
[Fact] AddLab_MicroToReadyFpSample_NeedsPreparationAgain()   // PreparationStatus Ready -> NeedsPreparation; FP order untouched
[Fact] AddLab_ToApprovedSample_ReturnsItToInProgress()      // FP section Approved, sample Approved -> add MICRO -> Sample.Status InTesting, Overall InProgress
[Fact] AddLab_ToRejectedSample_IsRefused()                  // any section Rejected -> "cannot be added to a rejected, voided or cancelled sample"
[Fact] AddLab_LabAlreadyOnSample_IsRefused()
[Fact] AddLab_LabWithNoTestsForItem_IsRefused()
[Fact] AddLab_RecordsSignatureAndEvent()                    // ReviewWorkflowEvents LaboratoryAdded with SectionId
```
Write each in full (Arrange with the same seed as Task 4 plus `db.Users` with a password hash; Act via `TestServiceFactory.AddLaboratory(db).AddAsync(...)`; Assert as named).

- [ ] **Step 2: Run — expect FAIL.**
- [ ] **Step 3: Implement:**

```csharp
// Adds a second laboratory's tests to a sample already received - same
// sample, same reference - so one combined CoA still covers the batch.
public class AddLaboratoryService
{
    private readonly MicroLimsDbContext _db;
    private readonly ReviewGateService _reviewGate;

    public AddLaboratoryService(MicroLimsDbContext db, ReviewGateService reviewGate)
    {
        _db = db;
        _reviewGate = reviewGate;
    }

    public async Task AddAsync(int sampleId, int sectionId, int userId, string password, string reason, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason is required to add a laboratory.");

        var sample = await _db.Samples.Include(s => s.TestOrders).Include(s => s.SectionSignoffs)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

        if (SampleSectionRollup.Overall(sample) is OverallSampleStatus.Rejected or OverallSampleStatus.Voided or OverallSampleStatus.Cancelled)
            throw new InvalidOperationException("A laboratory cannot be added to a rejected, voided or cancelled sample.");
        if (sample.TestOrders.Any(t => t.SectionId == sectionId))
            throw new InvalidOperationException("This laboratory is already testing the sample.");
        if (sample.ItemId is not int itemId)
            throw new InvalidOperationException("A laboratory can be added only to a product, raw material or packaging material sample.");

        var item = await _db.Items.Include(i => i.AssignedTests).FirstAsync(i => i.Id == itemId);
        var sections = await TestSectionLookup.ResolveAsync(_db, item.AssignedTests.Select(t => t.TestCode));
        var tests = item.AssignedTests.Where(t => sections[t.TestCode] == sectionId).ToList();
        if (tests.Count == 0)
        {
            var name = await _db.DocumentSections.Where(s => s.Id == sectionId).Select(s => s.Name).FirstOrDefaultAsync() ?? $"section {sectionId}";
            throw new InvalidOperationException($"Item '{item.Name}' has no tests for {name}.");
        }

        SampleSectionRollup.FreezeOpenSections(sample);
        await _reviewGate.SignAndLogAsync(ReviewEntityTypes.Sample, sampleId, userId, password,
            SignatureMeaning.LaboratoryAdded, ReviewWorkflowEventType.LaboratoryAdded, reason.Trim(), ipAddress, null, sectionId);

        foreach (var test in tests)
            sample.TestOrders.Add(new TestOrder { TestCode = test.TestCode, SectionId = sectionId, Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting });

        // New work re-opens a sample whose other labs had already finished.
        SampleSectionRollup.GetOrAdd(sample, sectionId).Status = SectionSignoffStatus.InTesting;
        SampleSectionRollup.Apply(sample);
        if (sample.Status == SampleStatus.InTesting && sample.ApprovalDecision == ApprovalDecision.Approve)
        {
            sample.ApprovalDecision = null;
            sample.ApprovedAt = null;
            sample.ApprovedByUserId = null;
        }

        // Microbiology tests need the sample prepared; a sample made Ready by
        // physicochemical-only testing goes back to needing preparation.
        var needs = await PreparationRules.InitialStatusAsync(_db, new[] { sectionId });
        if (needs == SamplePreparationStatus.NeedsPreparation && !await _db.SamplePreparations.AnyAsync(p => p.SampleId == sampleId))
            sample.PreparationStatus = SamplePreparationStatus.NeedsPreparation;

        await _db.SaveChangesAsync();
    }
}
```
Note on `GetOrAdd(...).Status = InTesting` when the section already had no row: `GetOrAdd` builds the row from the sample's status (possibly Approved) — the explicit assignment is required.

- [ ] **Step 4: Wire** — register scoped; factory `AddLaboratory(db) => new(db, ReviewGate(db))`; controller:
```csharp
public record AddLaboratoryRequest(int SectionId, string Password, string Reason);

[HttpPost("{id}/laboratories")]
[Authorize(Policy = PermissionConstants.SamplesReceive)]
public async Task<IActionResult> AddLaboratory(int id, AddLaboratoryRequest request)
{
    try
    {
        await _addLaboratory.AddAsync(id, request.SectionId, CurrentUserId, request.Password, request.Reason,
            HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(ApiResponse<object>.Ok(new { }));
    }
    catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.Fail(ex.Message)); }
}
```
- [ ] **Step 5: Migration check** — only enum values were appended; run `dotnet ef migrations add LabSeparationAddLaboratory ...` and delete it if empty (`dotnet ef migrations remove`).
- [ ] **Step 6: Run tests — expect PASS.**
- [ ] **Step 7: Commit** — `feat(receiving): add a laboratory to a received sample`.

---

### Task 6: Overall status, cancelled-at-stage and CoA availability in the summary; tracking board

**Files:**
- Modify: `backend/MicroLIMS.Application/DTOs/SampleSummaryDto.cs`
- Modify: `backend/MicroLIMS.Application/Services/SampleSummaryService.cs:~425` (test order DTO), `:~641-665` (sections)
- Create: `backend/MicroLIMS.Application/Services/SampleTrackingService.cs`, `backend/MicroLIMS.Application/DTOs/SampleTrackingDtos.cs`
- Create: `backend/MicroLIMS.API/Controllers/SampleTrackingController.cs`
- Test: `backend/MicroLIMS.Tests/WorkflowTests/SampleTrackingTests.cs`

**Interfaces:**
- Consumes: `SampleSectionRollup.Overall`, `StatusOf`, `SectionIds` (Task 2); `CancelledAtStep/Stage`, `CloseReason` (Task 3).
- Produces:
  - `SampleSummaryDto.OverallStatus : string`, `CombinedCoaAvailable : bool`; `SampleSectionSummaryDto.CoaAvailable : bool`, `ClosedByName`, `ClosedAt`, `CloseReason`; `TestOrderSummaryDetailDto.CancelledAtStep : string?`, `CancelledAtStage : int?`.
  - Rules: `CombinedCoaAvailable = Sample.Status is Approved or Rejected` (no lab open); `CoaAvailable = section Approved`. A Closed (Cancelled) section reads `Status = "Closed"` in the DTO.
  - `GET api/samples/tracking?labSectionId=&overall=&from=&to=&itemId=&page=&pageSize=` `[Authorize(Policy = Samples.TrackAll)]` → `PagedResult<SampleTrackingRowDto>`:
```csharp
public record SampleTrackingLabDto(int SectionId, string SectionName, string Stage);   // Testing | UnderReview | UnderApproval | Approved | Rejected | RetestRequested | Closed | Voided
public record SampleTrackingRowDto(int SampleId, string ReferenceNumber, string Product, string? BatchNumber,
    DateTime ReceivedAt, string OverallStatus, List<SampleTrackingLabDto> Labs);
```
  A lab not on the sample is absent from `Labs` (UI renders "not requested").

- [ ] **Step 1: Failing tests** — tracking: two samples (one micro-rejected with FP under review; one FP-only approved); assert rows, `OverallStatus` "Rejected"/"Approved", per-lab stages, filter `labSectionId` returns only samples with that lab's tests, `overall=Rejected` filter. Summary: after Task 3 closure, the FP test DTO has `CancelledAtStep == "Incubating"`, `CancelledAtStage == 2`, section `Status == "Closed"`, `CloseReason` set, `CombinedCoaAvailable == true`; before the closure `CombinedCoaAvailable == false`.
- [ ] **Step 2: Run — expect FAIL.**
- [ ] **Step 3: Summary changes.** In the section projection add:
```csharp
Status = SampleSectionRollup.StatusOf(sample, id) == SectionSignoffStatus.Cancelled ? "Closed" : SampleSectionRollup.StatusOf(sample, id).ToString(),
CoaAvailable = SampleSectionRollup.StatusOf(sample, id) == SectionSignoffStatus.Approved,
ClosedByName = canView && signoff?.ClosedByUserId is int closer ? NameOf(closer) : null,
ClosedAt = canView ? signoff?.ClosedAt : null,
CloseReason = canView ? signoff?.CloseReason : null,
```
and after it:
```csharp
dto.OverallStatus = SampleSectionRollup.Overall(sample).ToString();
dto.CombinedCoaAvailable = sample.Status is SampleStatus.Approved or SampleStatus.Rejected;
```
Test order DTO: `CancelledAtStep = order.CancelledAtStep?.ToString(), CancelledAtStage = order.CancelledAtStage`.
- [ ] **Step 4: Tracking service** — query `Samples` with `TestOrders` and `SectionSignoffs`, `Include(Item)`; filter `labSectionId` via `s.TestOrders.Any(t => t.SectionId == labSectionId && !t.IsSuperseded)`; date range on `ReceivedAt`; `itemId`; page (default 50, max 200, same as `TestingWorkspaceService`); order by `ReceivedAt desc`. Overall filter is applied after materialising the page's candidates: load the filtered query ordered, compute `Overall` in memory, then page — acceptable for the local data size; note it in a comment. Stage text: `Cancelled → "Closed"`, `InTesting → "Testing"`, others `ToString()`. Section names from `db.DocumentSections`.
- [ ] **Step 5: Controller** `[Route("api/samples/tracking")]`, `[Authorize(Policy = PermissionConstants.SamplesTrackAll)]`, one `GET`.
- [ ] **Step 6: Run tests — expect PASS.**
- [ ] **Step 7: Commit** — `feat(receiving): tracking board and overall status per laboratory`.

---

### Task 7: Lab narrowing for the workspace

**Files:**
- Modify: `backend/MicroLIMS.Application/DTOs/TestingWorkspaceFilterDto.cs` (add `int? LabSectionId`)
- Modify: `backend/MicroLIMS.Application/Services/TestingWorkspaceService.cs:50-60` and `:281-290`
- Modify: `backend/MicroLIMS.API/Controllers/TestingWorkspaceController.cs:42-47` (`counts?labSectionId=`)
- Create: `backend/MicroLIMS.Application/Services/LabScope.cs`
- Test: `backend/MicroLIMS.Tests/WorkflowTests/LabScopeTests.cs`

**Interfaces:**
- Produces: `LabScope.Narrow(IReadOnlyList<int>? scope, int? labSectionId) : IReadOnlyList<int>?` — no lab → `scope` unchanged; lab given and `scope` null (admin) → `[lab]`; lab in scope → `[lab]`; lab not in scope → throws `UnauthorizedAccessException`. `GetWorkloadCountsAsync(int? currentUserId = null, int? labSectionId = null)`.

- [ ] **Step 1: Failing tests** — the four `Narrow` cases; plus a workspace test: a mixed sample and a user in both labs — `GetActiveSamplesAsync(new() { LabSectionId = fp }, userId)` returns the sample and its `AssignedTests` contain only FP orders (if `ToDto` does not already filter tests by scope, extend it to take the narrowed scope — check `TestingWorkspaceService.ToDto` first).
- [ ] **Step 2: Run — expect FAIL.**
- [ ] **Step 3: Implement** `LabScope.Narrow` and apply it right after each `var scope = ... GetAccessibleSectionIdsAsync(...)` in `GetActiveSamplesAsync(filter, …)` and `GetWorkloadCountsAsync`: `scope = LabScope.Narrow(scope, filter.LabSectionId);`.
- [ ] **Step 4: Run — expect PASS.**
- [ ] **Step 5: Stage 1 gate** — full Postgres suite; expect all green (baseline 1839 + new tests). Apply migration `LabSeparationPrivileges` and `LabSeparationCloseTesting` to LIMSV2 after a `pg_dump` backup (`LIMSV2_before_lab_separation_s1_20260924.dump`); `dotnet ef database update --configuration Release ...`; verify with psql: `select "Code" from "Permissions" where "Code" like 'Samples.%';` returns 4 rows.
- [ ] **Step 6: Commit** — `feat(workspace): narrow the workspace to one laboratory`. **STOP — report Stage 1 to the user.**

---

# STAGE 2 — Backend masters

### Task 8: Every equipment inventory asset belongs to one lab

**Files:**
- Modify: `backend/MicroLIMS.Domain/Entities/EquipmentInventory.cs` (add `int? SectionId` + `DocumentSection? Section`)
- Modify: `backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs` (`SaveEquipmentInventoryRequest` + `int SectionId`; list/print/active/where-is-it filtered by scope; create/update guarded)
- Modify: `backend/MicroLIMS.API/Controllers/EquipmentInventoryController.cs` (pass `CurrentUserId`; 403 by-id guard)
- Modify: `backend/MicroLIMS.Application/Interfaces/IUserSectionScopeService.cs` + implementation (`EnsureEquipmentInventoryAccessAsync(int userId, int inventoryId)`)
- Create: migration `EquipmentInventoryLab`
- Test: `backend/MicroLIMS.Tests/UnitTests/EquipmentInventoryLabTests.cs`

**Interfaces:**
- Produces: `EquipmentInventory.SectionId` (nullable in the database until every row is assigned; required on create/update in the service). `EquipmentInventoryService.GetAllAsync(IReadOnlyList<int>? scope)` and the other list methods take `scope` (null = unrestricted).

- [ ] **Step 1: Failing tests** — create without `SectionId` → "Choose the laboratory this asset belongs to"; micro user lists only micro assets; micro user `GetById` of an FP asset → `UnauthorizedAccessException`; micro user creating an FP asset → `UnauthorizedAccessException`; admin sees all.
- [ ] **Step 2: Run — expect FAIL.**
- [ ] **Step 3: Implement** — entity property with comment (`// Owning laboratory - every asset belongs to exactly one. Nullable only until legacy rows are assigned.`); service filters `Where(e => scope == null || (e.SectionId != null && scope.Contains(e.SectionId.Value)))`; create/update validate `r.SectionId` exists and is in scope (reuse `_scope.ResolveSectionForCreateAsync(userId, r.SectionId)`).
- [ ] **Step 4: Migration** — add nullable `SectionId` + FK + index; then back-fill by code match:
```csharp
migrationBuilder.Sql(@"
    UPDATE ""EquipmentInventories"" i
    SET ""SectionId"" = e.""SectionId""
    FROM ""Equipment"" e
    WHERE e.""Code"" = i.""Code"" AND i.""SectionId"" IS NULL;");
```
After applying to LIMSV2 (backup first), list the unassigned rows for the user:
`select "Id","Code","InstrumentType","Location" from "EquipmentInventories" where "SectionId" is null;` (2 of 14 expected on 2026-09-24). The column stays nullable; the UI (Task 13) shows "Unassigned" and requires a lab on the next edit. Making it NOT NULL is a separate follow-up once the user has assigned them.
- [ ] **Step 5: Run — expect PASS.**
- [ ] **Step 6: Commit** — `feat(inventory): every equipment asset belongs to one laboratory`.

---

### Task 9: Specification rows are owned by their test's lab

**Files:**
- Create: `backend/MicroLIMS.Application/Services/SpecificationOwnership.cs`
- Modify: `backend/MicroLIMS.API/Controllers/MasterDataController.cs:672` (Create), `:731` (Update), `:796` (Delete), `:667` (Get — add `CanEdit` per row)
- Test: `backend/MicroLIMS.Tests/UnitTests/SpecificationOwnershipTests.cs`

**Interfaces:**
- Produces: `SpecificationOwnership.EnsureCanEditAsync(MicroLimsDbContext db, IUserSectionScopeService scope, int userId, string testCode) : Task` — resolves the lab via `TestDefinitions.Code == testCode`; throws `UnauthorizedAccessException("This specification belongs to <lab name> - only its Section Head can change it.")` when out of scope. GET returns `SpecificationRowDto` = the spec plus `bool CanEdit`, `string SectionName`.

- [ ] **Step 1: Failing tests** — micro Section Head edits a TAMC row → ok; edits an ASSAY row → forbidden; update that *changes* `TestCode` from TAMC to ASSAY → forbidden (check both old and new code); delete ASSAY row as micro → forbidden; admin → ok.
- [ ] **Step 2: Run — expect FAIL.**
- [ ] **Step 3: Implement** the helper; call it in Create (request.TestCode), Update (existing `spec.TestCode` **and** `request.TestCode`), Delete (`spec.TestCode`). GET: compute `CanEdit` from scope per row; keep returning every row (read-only for other labs).
- [ ] **Step 4: Run — expect PASS.**
- [ ] **Step 5: Commit** — `feat(specs): each laboratory edits only its own specification rows`.

---

### Task 10: Rename the lab to Physicochemical Laboratory

**Files:**
- Create: migration `RenamePhysicochemicalLaboratory`
- Modify: any seeder that creates the FP section (`git grep -n "Finished Product Laboratory" backend`)
- Test: existing suite; grep test fixtures for the old name.

- [ ] **Step 1:** Migration `Up`:
```csharp
migrationBuilder.Sql(@"UPDATE ""DocumentSections"" SET ""Name"" = 'Physicochemical Laboratory' WHERE ""Code"" = 'FP';");
```
`Down` restores `'Finished Product Laboratory'`.
- [ ] **Step 2:** Update seeders/fixtures that create the section by name (code stays `FP`).
- [ ] **Step 3: Stage 2 gate** — full Postgres suite; backup + apply `EquipmentInventoryLab`, `RenamePhysicochemicalLaboratory` to LIMSV2; run the unassigned-assets query and include the list in the stage report.
- [ ] **Step 4: Commit** — `feat(org): Finished Product Laboratory is now Physicochemical Laboratory`. **STOP — report Stage 2 (with the unassigned assets list) to the user.**

---

# STAGE 3 — Frontend

### Task 11: Menu areas by privilege and lab membership

**Files:**
- Modify: `frontend/src/routes/menuConfig.ts`
- Modify: `frontend/src/components/Sidebar.tsx:55`
- Create: `frontend/src/hooks/useMyLabs.ts`

**Interfaces:**
- Produces: `getGroupedMenu(ctx: { role: Role | null; permissions: string[]; labCodes: string[] }): MenuGroup[]`; `useMyLabs(): { labs: LaboratorySection[]; codes: string[]; loading: boolean }` (wraps `getMySections()`; System Administrator → both codes).

- [ ] **Step 1:** Define the new items (keep existing items and routes; only regroup):
```ts
const receivingAreaItems: MenuItem[] = [
  { label: "Receive Sample", path: "/receiving", icon: ScienceOutlinedIcon, group: "RECEIVING" },
  { label: "Tracking Board", path: "/receiving/tracking", icon: FactCheckOutlinedIcon, group: "RECEIVING" }
];
const microArea: MenuItem = {
  label: "Microbiology Laboratory", icon: BiotechOutlinedIcon, group: "LABORATORIES",
  children: [
    { label: "Workspace", path: "/microbiology/workspace" },
    { label: "Media Preparation & Evaluation", path: "/laboratory-configuration/media" },
    { label: "Reference Cryovials", path: "/laboratory-configuration/cryovials" },
    { label: "Materials Stock", path: "/inventory/materials?lab=MICRO" },
    { label: "Equipment Inventory", path: "/inventory/equipment?lab=MICRO" },
    { label: "Approved Media List", path: "/inventory/approved-media" },
    { label: "Approved Cryovial List", path: "/inventory/approved-cryovials" }
  ]
};
const microConfigArea: MenuItem = { ...microConfigurationItem, group: "LABORATORIES" };   // Section Head / Admin only
const physchemArea: MenuItem = {
  label: "Physicochemical Laboratory", icon: MedicationOutlinedIcon, group: "LABORATORIES",
  children: [
    { label: "Workspace", path: "/physicochemical/workspace" },
    { label: "System Suitability (HPLC)", path: "/laboratory/system-suitability" },
    { label: "Calibration Runs (ICP-OES / AAS)", path: "/laboratory/calibration-runs" },
    { label: "Materials Stock", path: "/inventory/materials?lab=FP" },
    { label: "Equipment Inventory", path: "/inventory/equipment?lab=FP" }
  ]
};
const physchemConfigArea: MenuItem = {
  label: "Physicochemical Configuration", icon: MedicationOutlinedIcon, group: "LABORATORIES",
  children: [
    { label: "Physicochemical Test Master", path: "/laboratory-configuration/fp-test-master" },
    { label: "Equation Types", path: "/laboratory-configuration/equation-types" },
    { label: "Physicochemical Instruments", path: "/laboratory-configuration/fp-instruments" },
    { label: "Chromatography Columns", path: "/laboratory-configuration/columns" }
  ]
};
const generalConfigArea: MenuItem[] = [
  { ...itemsConfigItem, group: "GENERAL LABORATORY CONFIGURATION" },
  { ...receivingConfigItem, group: "GENERAL LABORATORY CONFIGURATION" }
];
```
Remove `receivingTestingItem` from every role (the route `/receiving-testing` stays and redirects to the user's first lab workspace — Task 13).
- [ ] **Step 2:** `getGroupedMenu`:
```ts
export function getGroupedMenu({ role, permissions, labCodes }: MenuContext): MenuGroup[] {
  const isHead = role === "SectionHead" || role === "SystemAdministrator";
  const items: MenuItem[] = [dashboardItem];
  if (permissions.includes("Samples.Receive")) items.push(receivingAreaItems[0]);
  if (permissions.includes("Samples.TrackAll")) items.push(receivingAreaItems[1]);
  if (labCodes.includes("MICRO")) items.push(microArea, ...(isHead ? [microConfigArea] : []));
  if (labCodes.includes("FP")) items.push(physchemArea, ...(isHead ? [physchemConfigArea] : []));
  if (isHead) items.push(...generalConfigArea);
  items.push(...sharedItemsFor(role));   // document control, reports, audit, OOS, users/roles, error monitoring - exactly today's per-role lists minus the moved items
  return groupItems(items);             // the existing grouping body of getGroupedMenuForRole
}
```
Keep `getGroupedMenuForRole` as a thin wrapper only if other callers exist (`git grep -n getGroupedMenuForRole frontend/src`); otherwise delete it.
- [ ] **Step 3:** `Sidebar.tsx` — read `permissions` from the auth context (same source as `role`), `const { codes } = useMyLabs();`, call `getGroupedMenu({ role, permissions, labCodes: codes })`.
- [ ] **Step 4:** `npm run build` — tsc clean.
- [ ] **Step 5: Commit** — `feat(ui): menu areas for receiving and each laboratory`.

### Task 12: Receiving page and tracking board

**Files:**
- Create: `frontend/src/modules/receiving/ReceivingPage.tsx`, `frontend/src/modules/receiving/TrackingBoardPage.tsx`, `frontend/src/modules/receiving/services/TrackingService.ts`
- Modify: `frontend/src/modules/receiving/dialogs/NewSampleDialog.tsx` (props `allowedCategories`, `labMode`), `MultiSampleEntryGrid.tsx` (lab column), `SampleTypeSelector.tsx` (filter by `allowedCategories`), `services/ReceiveService.ts` (`targetSectionIds`, `getReceiptLabs`)
- Modify: the router file that declares `/receiving-testing` (`git grep -n "receiving-testing" frontend/src/routes`)

**Interfaces:**
- Consumes: `GET /samples/receipt-labs?itemId=` (Task 4), `POST /samples` with `targetSectionIds`, `GET /samples/tracking` (Task 6).
- Produces: `NewSampleDialog` props:
```ts
interface Props {
  open: boolean;
  onClose: () => void;
  onSuccess: (count: number) => void;
  allowedCategories: SampleCategoryKey[];              // main Receiving: ["product","rm","pm"]
  labMode: { kind: "choose" } | { kind: "fixed"; sectionId: number };
}
```
- [ ] **Step 1:** `ReceiveService`: `receiptLabs(itemId) => apiClient.get("/samples/receipt-labs", { params: { itemId } })`; item-based receive body gains `targetSectionIds: number[]`.
- [ ] **Step 2:** `MultiSampleEntryGrid` — when `labMode.kind === "choose"`, add a "Laboratories" column: on item change fetch `receiptLabs(itemId)`, render one checkbox per lab labelled `Microbiology (3)`, disabled when `testCount === 0`, default all enabled ones checked; row validation error "Choose at least one laboratory". In `fixed` mode send `[sectionId]` and show no column.
- [ ] **Step 3:** `ReceivingPage` — header "Receive Sample", the existing register table limited to FP/RM/PM received today (reuse `SampleRegisterTable` with a category filter), "Receive" button opening `NewSampleDialog` with `allowedCategories={["product","rm","pm"]}` and `labMode={{ kind: "choose" }}`. Row action "Add laboratory" (dialog: lab select limited to labs not on the sample, reason, password → `POST /samples/{id}/laboratories`).
- [ ] **Step 4:** `TrackingBoardPage` — filters (lab, overall, from/to, product), table columns Reference · Product · Batch · Received · Overall (chip: Rejected red, Approved green, In progress blue) · Microbiology · Physicochemical (stage chip, "Not requested" grey); row click opens the existing sample summary dialog.
- [ ] **Step 5:** Routes `/receiving` (guard `Samples.Receive`) and `/receiving/tracking` (guard `Samples.TrackAll`) using the existing permission route guard pattern (`git grep -n "System.ViewErrorLog" frontend/src/routes`).
- [ ] **Step 6:** `npm run build`; commit — `feat(ui): receiving page and tracking board`.

### Task 13: Lab workspaces, close testing, inventory lab, spec ownership

**Files:**
- Modify: `frontend/src/modules/receivingTesting/ReceivingTestingWorkspacePage.tsx` (prop `lab: { sectionId: number; code: "MICRO" | "FP"; name: string }`; pass `labSectionId` to `/testorders/page` and `/testorders/counts`; its "New sample" button opens `NewSampleDialog` with `labMode={{ kind: "fixed", sectionId }}` and `allowedCategories` = MICRO: all six, FP: `["product","rm","pm"]`)
- Create: `frontend/src/modules/receivingTesting/LabWorkspaceRoute.tsx` (resolves `/microbiology/workspace` → MICRO, `/physicochemical/workspace` → FP from `useMyLabs`; 403 view if not a member); `/receiving-testing` redirects to the first lab the user belongs to.
- Create: `frontend/src/modules/approval/CloseTestingDialog.tsx`; show a "Close testing" action in the sample summary / approval screen for a Section Head when `summary.overallStatus === "Rejected"` and the head's lab section status is open → `POST /samples/{id}/approval/sections/{sectionId}/close` with reason (default `Sample rejected by <rejecting lab name>`) and password.
- Modify: equipment inventory form/list (`git grep -ln "inventory/equipment\|EquipmentInventory" frontend/src/modules/inventory`) — required "Laboratory" select (user's labs), list filter from `?lab=`, "Unassigned" chip for legacy rows.
- Modify: `frontend/src/modules/laboratoryConfiguration/items/components/ItemSpecificationsSection.tsx` — rows with `canEdit === false` render read-only with a lab chip; add/edit dialogs offer only the user's own lab's tests.
- [ ] Steps: implement each file above; `npm run build`; commit — `feat(ui): laboratory workspaces, close testing, lab-owned inventory and specs`.

### Task 14: Summary, CoA and rename strings

**Files:**
- Modify: `frontend/src/modules/testingWorkspace/SampleSummaryDialog.tsx` — header shows `overallStatus`; a lab chip row (`Microbiology: Rejected · Physicochemical: Closed`); cancelled tests show `Cancelled at <step>` + `, stage <n>` when present; closed section shows who/when/reason.
- Modify: `frontend/src/modules/testingWorkspace/SampleCoaPage.tsx:331-372` — replace status checks with `summary.combinedCoaAvailable` (combined) and `section.coaAvailable` (single-lab); conclusion text `Rejected — <lab>` when `overallStatus === "Rejected"`; closed lab's tests print "Not completed — testing closed (<step>, stage n)".
- Modify: rename sweep — `git grep -n -i "finished product lab\|F\.P\. Configuration\|FP Test Master\|FP Instruments" frontend/src`: page titles and labels → "Physicochemical …". Do **not** touch the sample category label "Finished Product" or F.P/S.F stages.
- Modify: types in `frontend/src/modules/testingWorkspace/types` for the new DTO fields (`overallStatus`, `combinedCoaAvailable`, `coaAvailable`, `closedByName`, `closedAt`, `closeReason`, `cancelledAtStep`, `cancelledAtStage`).
- [ ] Steps: implement; `npm run build`; **Stage 3 gate**: full Postgres suite + build; commit — `feat(ui): overall status, closed labs and CoA rules; Physicochemical naming`. **STOP — report Stage 3.**

---

# STAGE 4 — Browser end-to-end

### Task 15: Browser verification

Restart the API from the branch (`dotnet run` in `backend/MicroLIMS.API`) and the Vite dev server; use Claude in Chrome. Label all new data "SMOKE TEST".

- [ ] **15.1 Sample 95 (pending since 2026-09-24):** in the Physicochemical workspace re-enter pH (signed) → FP section goes to review while micro tests stay Pending; review (FP reviewer), approve (FP head); open the single-lab CoA; tracking board shows `Physicochemical: Approved · Microbiology: Testing`, overall In progress.
- [ ] **15.2 Main Receiving:** receive an FP item for Physicochemical only → only FP tests, sample Ready; "Add laboratory" Microbiology → micro tests appear, sample needs preparation, same reference.
- [ ] **15.3 Rejection path:** reject micro on a mixed sample → board overall Rejected, FP still open in its queue; FP head "Close testing" with default reason → FP tests Cancelled with stage shown in summary; combined CoA available with conclusion "Rejected — Microbiology Laboratory".
- [ ] **15.4 Isolation:** a micro-only user sees no Physicochemical area, cannot open `/physicochemical/workspace`, and a crafted `POST /samples` with the FP section id returns 403.
- [ ] **15.5 Masters:** micro Section Head sees FP spec rows read-only; equipment inventory lists only own-lab assets.
- [ ] Record findings; fix blockers in follow-up commits; update memory `lab-separation-design.md` with the outcome.
