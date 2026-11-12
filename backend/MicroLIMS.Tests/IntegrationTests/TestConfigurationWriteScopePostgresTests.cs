using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Seven test-configuration writes (workflow type; add / change / move / delete
// workflow step; freeze; unfreeze) must refuse a user who may not configure
// the test's laboratory, and the five that replace an existing record must
// refuse a form that was loaded before a colleague saved. Each test arranges,
// acts and asserts on separate contexts, reads with AsNoTracking, and checks
// the persisted state, the audit trail and the refusal (CLAUDE.md Rules 4.1,
// 4.4). Laboratory A owns the test; Section Head A belongs to A only,
// Section Head B to B only; the seeded administrator holds
// Sections.ConfigureAll and is not a member of either laboratory.
[Collection("PostgresDatabaseCollection")]
public class TestConfigurationWriteScopePostgresTests
{
    private const string OutOfScopeMessage = "This test belongs to a laboratory section you are not assigned to.";

    private readonly PostgresTestFixture _fixture;

    public TestConfigurationWriteScopePostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed record World(int SectionA, int HeadA, int HeadB, int HeadWithoutLab, int TestId, int[] StepIds);

    private sealed record StepState(int Id, int Order, string Name, bool IsFinal, StepType Type, uint Version);

    private sealed record State(WorkflowType Workflow, bool IsActive, string DisplayName, uint TestVersion, IReadOnlyList<StepState> Steps);

    private async Task<World> ArrangeAsync(bool active = true)
    {
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var db = _fixture.CreateDbContext();
        db.CurrentUserId = _fixture.SeededUserId;

        // A separate department so the seeded administrator (a member of QC) is a member of neither laboratory.
        var dept = new DocumentDepartment { Name = "Dept " + tag, Code = "D" + tag, IsActive = true };
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();
        var labA = new DocumentSection { Name = "Lab A " + tag, Code = "A" + tag, DepartmentId = dept.Id, IsActive = true };
        var labB = new DocumentSection { Name = "Lab B " + tag, Code = "B" + tag, DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.AddRange(labA, labB);
        await db.SaveChangesAsync();

        var headRole = await db.Roles.AsNoTracking().FirstAsync(r => r.Type == RoleType.SectionHead);
        User NewHead(string name) => new() { Username = $"{name}_{tag}", FullName = $"{name} {tag}", RoleId = headRole.Id, IsActive = true, PasswordHash = "x" };
        var headA = NewHead("headA");
        var headB = NewHead("headB");
        var headNone = NewHead("headNone");
        db.Users.AddRange(headA, headB, headNone);
        await db.SaveChangesAsync();
        db.UserOrgMemberships.AddRange(
            new UserOrgMembership { UserId = headA.Id, DepartmentId = dept.Id, SectionId = labA.Id },
            new UserOrgMembership { UserId = headB.Id, DepartmentId = dept.Id, SectionId = labB.Id });
        await db.SaveChangesAsync();

        var test = new TestDefinition
        {
            Code = "WS-" + tag, DisplayName = "Scope test " + tag, SectionId = labA.Id,
            WorkflowType = WorkflowType.Observation, EquationType = EquationType.None, IsActive = active
        };
        db.TestDefinitions.Add(test);
        await db.SaveChangesAsync();
        for (var i = 1; i <= 3; i++)
            db.TestWorkflowSteps.Add(new TestWorkflowStep
            {
                TestDefinitionId = test.Id, StepOrder = i, StepName = "Step " + i, StepType = StepType.BiochemicalTest,
                PhenotypicTestType = PhenotypicTestType.Gram, IsFinalStep = i == 3
            });
        await db.SaveChangesAsync();
        var stepIds = await db.TestWorkflowSteps.AsNoTracking().Where(s => s.TestDefinitionId == test.Id)
            .OrderBy(s => s.StepOrder).Select(s => s.Id).ToArrayAsync();
        return new World(labA.Id, headA.Id, headB.Id, headNone.Id, test.Id, stepIds);
    }

    private async Task<State> StateAsync(World w)
    {
        await using var db = _fixture.CreateDbContext();
        var test = await db.TestDefinitions.AsNoTracking().SingleAsync(t => t.Id == w.TestId);
        var steps = await db.TestWorkflowSteps.AsNoTracking().Where(s => s.TestDefinitionId == w.TestId)
            .OrderBy(s => s.StepOrder)
            .Select(s => new StepState(s.Id, s.StepOrder, s.StepName, s.IsFinalStep, s.StepType, s.Version)).ToListAsync();
        return new State(test.WorkflowType, test.IsActive, test.DisplayName, test.Version, steps);
    }

    // Audit rows written for the test and for any step of it, including deleted ones.
    private async Task<int> AuditCountAsync(World w)
    {
        await using var db = _fixture.CreateDbContext();
        var stepIds = w.StepIds.Select(i => i.ToString()).ToList();
        var testId = w.TestId.ToString();
        var all = await db.AuditLogs.AsNoTracking()
            .Where(a => (a.EntityName == nameof(TestDefinition) && a.EntityId == testId)
                     || (a.EntityName == nameof(TestWorkflowStep) && stepIds.Contains(a.EntityId)))
            .CountAsync();
        var created = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(TestWorkflowStep) && a.Action == "Create" && a.NewValue!.Contains("\"TestDefinitionId\":" + w.TestId))
            .CountAsync();
        return all + created;
    }

    private async Task ActAsync(int userId, Func<MicroLimsDbContext, Task> act, uint? expectedVersion = null)
    {
        await using var db = _fixture.CreateDbContext();
        db.CurrentUserId = userId;
        db.ExpectedVersion = expectedVersion;
        await act(db);
    }

    private static TestWorkflowStepMasterDataService Steps(MicroLimsDbContext db) => new(db, new UserSectionScopeService(db));
    private static TestDefinitionMasterDataService Defs(MicroLimsDbContext db) => TestServiceFactory.TestDefinitionMaster(db);

    private static CreateTestWorkflowStepRequest NewStep(string name) => new(name, 0, 0, 0, 0, false, StepType.BiochemicalTest,
        null, new List<StepMediaRequest>(), false, null, null, PhenotypicTestType.Gram);

    private static UpdateTestWorkflowStepRequest ChangedStep(string name) => new(name, 0, 0, 0, 0, false, StepType.BiochemicalTest,
        null, new List<StepMediaRequest>(), false, null, null, PhenotypicTestType.Gram);

    // A colleague saves the test (or a step) after the form was loaded.
    private Task ColleagueRenamesTestAsync(World w) => ActAsync(w.HeadA, async db =>
    {
        var t = await db.TestDefinitions.SingleAsync(x => x.Id == w.TestId);
        t.DisplayName += " (colleague)";
        await db.SaveChangesAsync();
    });

    private Task ColleagueRenamesStepAsync(World w, int stepId) => ActAsync(w.HeadA, async db =>
    {
        var s = await db.TestWorkflowSteps.SingleAsync(x => x.Id == stepId);
        s.StepName += " (colleague)";
        await db.SaveChangesAsync();
    });

    private async Task AssertRefusedAndUntouchedAsync(World w, Func<MicroLimsDbContext, Task> act, uint? expectedVersion = null, int? userId = null)
    {
        var before = await StateAsync(w);
        var auditBefore = await AuditCountAsync(w);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ActAsync(userId ?? w.HeadB, act, expectedVersion));
        Assert.Equal(OutOfScopeMessage, ex.Message);

        Assert.Equivalent(before, await StateAsync(w), strict: true);
        Assert.Equal(auditBefore, await AuditCountAsync(w));
    }

    private async Task<AuditLog> SingleAuditAsync(string entityName, int id, string action, int userId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.AuditLogs.AsNoTracking().SingleAsync(a =>
            a.EntityName == entityName && a.EntityId == id.ToString() && a.Action == action && a.UserId == userId);
    }

    // A move saves twice (the moved step is parked at -1 first), so it has two audit rows; the last one is the result.
    private async Task<AuditLog> LastAuditAsync(string entityName, int id, string action, int userId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == entityName && a.EntityId == id.ToString() && a.Action == action && a.UserId == userId)
            .OrderByDescending(a => a.Id).FirstAsync();
    }

    // ---------------------------------------------------------------- workflow type

    [PostgresFact]
    public async Task WorkflowType_change_by_other_laboratory_refused()
    {
        var w = await ArrangeAsync();
        await AssertRefusedAndUntouchedAsync(w, db => Steps(db).UpdateWorkflowTypeAsync(w.HeadB, w.TestId, new UpdateWorkflowTypeRequest(WorkflowType.CountTest)));
    }

    [PostgresFact]
    public async Task WorkflowType_change_by_own_laboratory_persists_and_audits()
    {
        var w = await ArrangeAsync();
        await ActAsync(w.HeadA, db => Steps(db).UpdateWorkflowTypeAsync(w.HeadA, w.TestId, new UpdateWorkflowTypeRequest(WorkflowType.CountTest)));

        Assert.Equal(WorkflowType.CountTest, (await StateAsync(w)).Workflow);
        var audit = await SingleAuditAsync(nameof(TestDefinition), w.TestId, "Update", w.HeadA);
        Assert.Contains("WorkflowType", audit.PreviousValue);
        Assert.Contains("WorkflowType", audit.NewValue);
        Assert.NotEqual(audit.PreviousValue, audit.NewValue);
    }

    [PostgresFact]
    public async Task WorkflowType_change_by_configure_all_succeeds()
    {
        var w = await ArrangeAsync();
        var admin = _fixture.SeededUserId;
        await ActAsync(admin, db => Steps(db).UpdateWorkflowTypeAsync(admin, w.TestId, new UpdateWorkflowTypeRequest(WorkflowType.CountTest)));

        Assert.Equal(WorkflowType.CountTest, (await StateAsync(w)).Workflow);
        await SingleAuditAsync(nameof(TestDefinition), w.TestId, "Update", admin);
    }

    [PostgresFact]
    public async Task Workflow_type_with_stale_version_refused()
    {
        var w = await ArrangeAsync();
        var loaded = (await StateAsync(w)).TestVersion;
        await ColleagueRenamesTestAsync(w);
        var before = await StateAsync(w);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ActAsync(w.HeadA,
            db => Steps(db).UpdateWorkflowTypeAsync(w.HeadA, w.TestId, new UpdateWorkflowTypeRequest(WorkflowType.CountTest)), loaded));

        var after = await StateAsync(w);
        Assert.Equal(WorkflowType.Observation, after.Workflow);
        Assert.Equal(before.DisplayName, after.DisplayName);
        Assert.Equal(before.TestVersion, after.TestVersion);
    }

    // ---------------------------------------------------------------- add step

    [PostgresFact]
    public async Task Step_create_by_other_laboratory_refused()
    {
        var w = await ArrangeAsync();
        await AssertRefusedAndUntouchedAsync(w, db => Steps(db).CreateTestWorkflowStepAsync(w.HeadB, w.TestId, NewStep("Added")));
    }

    [PostgresFact]
    public async Task Step_create_by_own_laboratory_persists_and_audits()
    {
        var w = await ArrangeAsync();
        await ActAsync(w.HeadA, db => Steps(db).CreateTestWorkflowStepAsync(w.HeadA, w.TestId, NewStep("Added")));

        var steps = (await StateAsync(w)).Steps;
        var added = Assert.Single(steps, s => s.Name == "Added");
        Assert.Equal(4, added.Order);
        var audit = await SingleAuditAsync(nameof(TestWorkflowStep), added.Id, "Create", w.HeadA);
        Assert.Contains("\"StepName\":\"Added\"", audit.NewValue);
    }

    [PostgresFact]
    public async Task Step_create_by_configure_all_succeeds()
    {
        var w = await ArrangeAsync();
        var admin = _fixture.SeededUserId;
        await ActAsync(admin, db => Steps(db).CreateTestWorkflowStepAsync(admin, w.TestId, NewStep("Added")));

        var added = Assert.Single((await StateAsync(w)).Steps, s => s.Name == "Added");
        await SingleAuditAsync(nameof(TestWorkflowStep), added.Id, "Create", admin);
    }

    // ---------------------------------------------------------------- change step

    [PostgresFact]
    public async Task Step_update_by_other_laboratory_refused()
    {
        var w = await ArrangeAsync();
        await AssertRefusedAndUntouchedAsync(w, db => Steps(db).UpdateTestWorkflowStepAsync(w.HeadB, w.StepIds[0], ChangedStep("Renamed")));
    }

    [PostgresFact]
    public async Task Step_update_by_own_laboratory_persists_and_audits()
    {
        var w = await ArrangeAsync();
        await ActAsync(w.HeadA, db => Steps(db).UpdateTestWorkflowStepAsync(w.HeadA, w.StepIds[0], ChangedStep("Renamed")));

        Assert.Equal("Renamed", (await StateAsync(w)).Steps.Single(s => s.Id == w.StepIds[0]).Name);
        var audit = await SingleAuditAsync(nameof(TestWorkflowStep), w.StepIds[0], "Update", w.HeadA);
        Assert.Contains("\"StepName\":\"Step 1\"", audit.PreviousValue);
        Assert.Contains("\"StepName\":\"Renamed\"", audit.NewValue);
    }

    [PostgresFact]
    public async Task Step_update_by_configure_all_succeeds()
    {
        var w = await ArrangeAsync();
        var admin = _fixture.SeededUserId;
        await ActAsync(admin, db => Steps(db).UpdateTestWorkflowStepAsync(admin, w.StepIds[0], ChangedStep("Renamed")));

        Assert.Equal("Renamed", (await StateAsync(w)).Steps.Single(s => s.Id == w.StepIds[0]).Name);
        await SingleAuditAsync(nameof(TestWorkflowStep), w.StepIds[0], "Update", admin);
    }

    [PostgresFact]
    public async Task Step_update_with_stale_version_refused()
    {
        var w = await ArrangeAsync();
        var loaded = (await StateAsync(w)).Steps[0].Version;
        await ColleagueRenamesStepAsync(w, w.StepIds[0]);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ActAsync(w.HeadA,
            db => Steps(db).UpdateTestWorkflowStepAsync(w.HeadA, w.StepIds[0], ChangedStep("Stale rename")), loaded));

        Assert.Equal("Step 1 (colleague)", (await StateAsync(w)).Steps[0].Name);
    }

    // ---------------------------------------------------------------- move step

    [PostgresFact]
    public async Task Step_move_by_other_laboratory_refused()
    {
        var w = await ArrangeAsync();
        await AssertRefusedAndUntouchedAsync(w, db => Steps(db).MoveTestWorkflowStepAsync(w.HeadB, w.StepIds[1], new MoveTestWorkflowStepRequest("up")));
    }

    [PostgresFact]
    public async Task Step_move_by_own_laboratory_persists_and_audits()
    {
        var w = await ArrangeAsync();
        var before = await StateAsync(w);
        await ActAsync(w.HeadA, db => Steps(db).MoveTestWorkflowStepAsync(w.HeadA, w.StepIds[1], new MoveTestWorkflowStepRequest("up")));

        var after = await StateAsync(w);
        Assert.Equal(new[] { w.StepIds[1], w.StepIds[0], w.StepIds[2] }, after.Steps.Select(s => s.Id));
        Assert.Equal(new[] { 1, 2, 3 }, after.Steps.Select(s => s.Order));
        // Only the order of the two swapped steps changed.
        foreach (var s in after.Steps)
        {
            var was = before.Steps.Single(b => b.Id == s.Id);
            Assert.Equal((was.Name, was.IsFinal, was.Type), (s.Name, s.IsFinal, s.Type));
        }
        var moved = await LastAuditAsync(nameof(TestWorkflowStep), w.StepIds[1], "Update", w.HeadA);
        Assert.Contains("\"StepOrder\":1", moved.NewValue);
        var neighbour = await LastAuditAsync(nameof(TestWorkflowStep), w.StepIds[0], "Update", w.HeadA);
        Assert.Contains("\"StepOrder\":2", neighbour.NewValue);
    }

    [PostgresFact]
    public async Task Step_move_by_configure_all_succeeds()
    {
        var w = await ArrangeAsync();
        var admin = _fixture.SeededUserId;
        await ActAsync(admin, db => Steps(db).MoveTestWorkflowStepAsync(admin, w.StepIds[1], new MoveTestWorkflowStepRequest("up")));

        Assert.Equal(new[] { w.StepIds[1], w.StepIds[0], w.StepIds[2] }, (await StateAsync(w)).Steps.Select(s => s.Id));
        await LastAuditAsync(nameof(TestWorkflowStep), w.StepIds[1], "Update", admin);
    }

    [PostgresFact]
    public async Task Move_with_stale_version_of_moved_step_refused()
    {
        var w = await ArrangeAsync();
        var loaded = (await StateAsync(w)).Steps[1].Version;
        await ColleagueRenamesStepAsync(w, w.StepIds[1]);
        var before = await StateAsync(w);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ActAsync(w.HeadA,
            db => Steps(db).MoveTestWorkflowStepAsync(w.HeadA, w.StepIds[1], new MoveTestWorkflowStepRequest("up")), loaded));

        // Neither the moved step nor the step it would have swapped with changed, and the colleague's rename survived.
        Assert.Equivalent(before, await StateAsync(w), strict: true);
        Assert.Equal("Step 2 (colleague)", (await StateAsync(w)).Steps[1].Name);
    }

    // ---------------------------------------------------------------- delete step

    [PostgresFact]
    public async Task Step_delete_by_other_laboratory_refused()
    {
        var w = await ArrangeAsync();
        await AssertRefusedAndUntouchedAsync(w, db => Steps(db).DeleteTestWorkflowStepAsync(w.HeadB, w.StepIds[0]));
    }

    [PostgresFact]
    public async Task Step_delete_by_own_laboratory_persists_and_audits()
    {
        var w = await ArrangeAsync();
        await ActAsync(w.HeadA, db => Steps(db).DeleteTestWorkflowStepAsync(w.HeadA, w.StepIds[0]));

        var after = (await StateAsync(w)).Steps;
        Assert.Equal(new[] { w.StepIds[1], w.StepIds[2] }, after.Select(s => s.Id));
        Assert.Equal(new[] { 1, 2 }, after.Select(s => s.Order));
        var audit = await SingleAuditAsync(nameof(TestWorkflowStep), w.StepIds[0], "Delete", w.HeadA);
        Assert.Contains("\"StepName\":\"Step 1\"", audit.PreviousValue);
    }

    [PostgresFact]
    public async Task Step_delete_by_configure_all_succeeds()
    {
        var w = await ArrangeAsync();
        var admin = _fixture.SeededUserId;
        await ActAsync(admin, db => Steps(db).DeleteTestWorkflowStepAsync(admin, w.StepIds[0]));

        Assert.DoesNotContain((await StateAsync(w)).Steps, s => s.Id == w.StepIds[0]);
        await SingleAuditAsync(nameof(TestWorkflowStep), w.StepIds[0], "Delete", admin);
    }

    [PostgresFact]
    public async Task Delete_with_stale_version_refused()
    {
        var w = await ArrangeAsync();
        var loaded = (await StateAsync(w)).Steps[0].Version;
        await ColleagueRenamesStepAsync(w, w.StepIds[0]);
        var before = await StateAsync(w);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ActAsync(w.HeadA,
            db => Steps(db).DeleteTestWorkflowStepAsync(w.HeadA, w.StepIds[0]), loaded));

        Assert.Equivalent(before, await StateAsync(w), strict: true);
    }

    // ---------------------------------------------------------------- null / empty scope

    // TestDefinition.SectionId is a non-nullable column, so a test "without a
    // laboratory" cannot exist; the test update's only edge rule is a user whose
    // configurable scope is empty (no membership). A step write must give the
    // same outcome as the test update there: the same exception and message.
    [PostgresFact]
    public async Task Step_write_on_test_without_section_follows_test_update_rule()
    {
        var w = await ArrangeAsync();
        var before = await StateAsync(w);

        var viaTestUpdate = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ActAsync(w.HeadWithoutLab,
            db => Defs(db).UpdateTestDefinitionAsync(w.HeadWithoutLab, w.TestId,
                new UpdateTestDefinitionRequest("WS-X", "Renamed by nobody"))));
        var viaStepWrite = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ActAsync(w.HeadWithoutLab,
            db => Steps(db).CreateTestWorkflowStepAsync(w.HeadWithoutLab, w.TestId, NewStep("Added"))));
        var viaStepDelete = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ActAsync(w.HeadWithoutLab,
            db => Steps(db).DeleteTestWorkflowStepAsync(w.HeadWithoutLab, w.StepIds[0])));

        Assert.Equal(viaTestUpdate.Message, viaStepWrite.Message);
        Assert.Equal(viaTestUpdate.Message, viaStepDelete.Message);
        Assert.Equal(OutOfScopeMessage, viaStepWrite.Message);
        Assert.Equivalent(before, await StateAsync(w), strict: true);
    }

    // ---------------------------------------------------------------- freeze / unfreeze

    [PostgresFact]
    public async Task Freeze_by_other_laboratory_refused()
    {
        var w = await ArrangeAsync();
        await AssertRefusedAndUntouchedAsync(w, db => Defs(db).FreezeTestDefinitionAsync(w.HeadB, w.TestId));
    }

    [PostgresFact]
    public async Task Unfreeze_by_other_laboratory_refused()
    {
        var w = await ArrangeAsync(active: false);
        await AssertRefusedAndUntouchedAsync(w, db => Defs(db).UnfreezeTestDefinitionAsync(w.HeadB, w.TestId));
        Assert.False((await StateAsync(w)).IsActive);
    }

    [PostgresFact]
    public async Task Freeze_by_own_laboratory_persists_and_audits()
    {
        var w = await ArrangeAsync();
        await ActAsync(w.HeadA, db => Defs(db).FreezeTestDefinitionAsync(w.HeadA, w.TestId));

        Assert.False((await StateAsync(w)).IsActive);
        var audit = await SingleAuditAsync(nameof(TestDefinition), w.TestId, "Update", w.HeadA);
        Assert.Contains("\"IsActive\":true", audit.PreviousValue);
        Assert.Contains("\"IsActive\":false", audit.NewValue);
    }

    [PostgresFact]
    public async Task Unfreeze_by_own_laboratory_persists_and_audits()
    {
        var w = await ArrangeAsync(active: false);
        await ActAsync(w.HeadA, db => Defs(db).UnfreezeTestDefinitionAsync(w.HeadA, w.TestId));

        Assert.True((await StateAsync(w)).IsActive);
        var audit = await SingleAuditAsync(nameof(TestDefinition), w.TestId, "Update", w.HeadA);
        Assert.Contains("\"IsActive\":false", audit.PreviousValue);
        Assert.Contains("\"IsActive\":true", audit.NewValue);
    }

    [PostgresFact]
    public async Task Freeze_and_unfreeze_by_configure_all_succeed()
    {
        var w = await ArrangeAsync();
        var admin = _fixture.SeededUserId;
        await ActAsync(admin, db => Defs(db).FreezeTestDefinitionAsync(admin, w.TestId));
        Assert.False((await StateAsync(w)).IsActive);
        await ActAsync(admin, db => Defs(db).UnfreezeTestDefinitionAsync(admin, w.TestId));
        Assert.True((await StateAsync(w)).IsActive);
    }

    [PostgresFact]
    public async Task Freeze_with_stale_version_refused()
    {
        var w = await ArrangeAsync();
        var loaded = (await StateAsync(w)).TestVersion;
        await ColleagueRenamesTestAsync(w);
        var before = await StateAsync(w);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ActAsync(w.HeadA,
            db => Defs(db).FreezeTestDefinitionAsync(w.HeadA, w.TestId), loaded));

        Assert.Equivalent(before, await StateAsync(w), strict: true);
        Assert.True((await StateAsync(w)).IsActive);
    }

    [PostgresFact]
    public async Task Unfreeze_with_stale_version_refused()
    {
        var w = await ArrangeAsync(active: false);
        var loaded = (await StateAsync(w)).TestVersion;
        await ColleagueRenamesTestAsync(w);
        var before = await StateAsync(w);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ActAsync(w.HeadA,
            db => Defs(db).UnfreezeTestDefinitionAsync(w.HeadA, w.TestId), loaded));

        Assert.Equivalent(before, await StateAsync(w), strict: true);
        Assert.False((await StateAsync(w)).IsActive);
    }
}
