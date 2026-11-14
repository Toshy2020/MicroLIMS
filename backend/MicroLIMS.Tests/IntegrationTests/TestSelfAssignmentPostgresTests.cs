using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MyWork;
using MicroLIMS.Application.Services.MyWork.Sources;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Migrations;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Take and release of unassigned tests (My Work, Task 4) against a real database. Arrange, act and assert each
// use their own DbContext; assertions read with AsNoTracking. The race tests drive two services on two contexts.
[Collection("PostgresDatabaseCollection")]
public class TestSelfAssignmentPostgresTests
{
    // 12:00 UTC on 14 Nov 2026.
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
    private const string CannotRelease = "This test can no longer be released. Ask a Section Head to reassign it.";

    private readonly PostgresTestFixture _fixture;

    public TestSelfAssignmentPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class Settings : ISystemSettingsReader
    {
        private readonly bool _selfAssign;
        public Settings(bool selfAssign) => _selfAssign = selfAssign;
        public Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(key == SystemSettingKeys.MyWorkLookAheadDays ? 7 : 0);
        public Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(key != SystemSettingKeys.SelfAssignmentEnabled || _selfAssign);
        public Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
    }

    private sealed class NoopErrors : IErrorCaptureService
    {
        public Task CaptureAsync(ErrorCaptureRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    // Forwards every call to the real scope service; runs a hook once, just before the member sections are read.
    // The service reads them after loading the test and before saving, so the hook lands exactly in the race window.
    public class HookedScope : DispatchProxy
    {
        public IUserSectionScopeService Inner { get; set; } = null!;
        public Func<Task>? BeforeMembers { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IUserSectionScopeService.GetMemberSectionIdsAsync) && BeforeMembers is { } hook)
            {
                BeforeMembers = null;
                return RunAfter(hook, targetMethod, args);
            }

            try { return targetMethod.Invoke(Inner, args); }
            catch (TargetInvocationException ex) { ExceptionDispatchInfo.Capture(ex.InnerException!).Throw(); throw; }
        }

        private async Task<IReadOnlyList<int>> RunAfter(Func<Task> hook, MethodInfo method, object?[]? args)
        {
            await hook();
            return await (Task<IReadOnlyList<int>>)method.Invoke(Inner, args)!;
        }
    }

    private static TestSelfAssignmentService BuildService(MicroLimsDbContext db, bool selfAssign = true, IUserSectionScopeService? scope = null) =>
        new(db, scope ?? new UserSectionScopeService(db), new Settings(selfAssign), new FixedTimeProvider(new DateTimeOffset(Now)));

    private static MyWorkService BuildMyWork(MicroLimsDbContext db, bool selfAssign = true) =>
        new(db, new UserSectionScopeService(db),
            new LabClock(new FixedTimeProvider(new DateTimeOffset(Now)), LabClock.ResolveTimeZone("Africa/Cairo")),
            new Settings(selfAssign), new NoopErrors(),
            new IMyWorkSource[] { new AssignedTestsSource(db), new AvailableTestsSource(db, new Settings(selfAssign)), new UnassignedTestsSource(db) });

    private async Task<MyWorkResponse> MyWorkAsync(int userId, bool selfAssign = true)
    {
        await using var actDb = _fixture.CreateDbContext();
        var response = await BuildMyWork(actDb, selfAssign).GetAsync(userId, null);
        Assert.Empty(response.UnavailableSources);
        return response;
    }

    private static MyWorkGroup? Group(MyWorkResponse r, string key) => r.Groups.SingleOrDefault(g => g.Key == key);

    // The database is shared with other tests, so a group is judged only on the tests this test created.
    private static List<MyWorkItem> Mine(MyWorkResponse r, string group, params TestOrder[] created) =>
        (Group(r, group)?.Items ?? new List<MyWorkItem>()).Where(i => created.Any(c => c.Id == i.Target.TestOrderId)).ToList();

    // ---- arrange helpers ----

    private sealed record World(DocumentSection Micro, DocumentSection Fp, int CauseId);

    private static async Task<World> WorldAsync(MicroLimsDbContext db)
    {
        var micro = await db.DocumentSections.AsNoTracking().SingleAsync(s => s.Code == "MICRO");
        var fp = await db.DocumentSections.AsNoTracking().SingleAsync(s => s.Code == "FP");
        var cause = await db.CausesOfTesting.FirstOrDefaultAsync();
        if (cause is null)
        {
            cause = new CauseOfTesting { Name = "Routine Testing", IsActive = true };
            db.CausesOfTesting.Add(cause);
            await db.SaveChangesAsync();
        }

        return new World(micro, fp, cause.Id);
    }

    // roleType = the fixture's seeded role of that type (Analyst holds Tests.SelfAssign, SectionHead holds Samples.AssignAnalyst,
    // Reviewer holds neither); extraCodes builds a custom role of that type with exactly those codes instead.
    private static async Task<User> NewUserAsync(MicroLimsDbContext db, string label, RoleType roleType, string[]? extraCodes, params DocumentSection[] memberOf)
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        Role role;
        if (extraCodes is null)
        {
            role = await db.Roles.AsNoTracking().Where(r => r.Type == roleType).OrderBy(r => r.Id).FirstAsync();
        }
        else
        {
            role = new Role { Type = roleType, Name = $"Custom {label} {uid}", IsActive = true };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            foreach (var code in extraCodes)
            {
                var permission = await db.Permissions.SingleAsync(p => p.Code == code);
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            }
        }

        var user = new User { Username = $"{label}_{uid}", FullName = $"{label} {uid}", PasswordHash = "x", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        foreach (var section in memberOf)
            db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = section.DepartmentId, SectionId = section.Id });
        await db.SaveChangesAsync();
        return user;
    }

    private static Task<User> NewAnalystAsync(MicroLimsDbContext db, string label, params DocumentSection[] memberOf) =>
        NewUserAsync(db, label, RoleType.Analyst, null, memberOf);

    private static async Task<TestOrder> NewTestAsync(MicroLimsDbContext db, World w, DocumentSection section, int? assignedTo = null,
        bool superseded = false, ApprovalStatus status = ApprovalStatus.Pending, string? outsourcedTo = null, Sample? onSample = null)
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var sample = onSample;
        if (sample is null)
        {
            var receiver = await db.Users.AsNoTracking().OrderBy(u => u.Id).FirstAsync();
            sample = new Sample
            {
                ReferenceNumber = $"SA-{uid}",
                Category = SampleCategory.FinishedProduct,
                CauseOfTestingId = w.CauseId,
                ReceivedAt = Now.AddHours(-2),
                ReceivedByUserId = receiver.Id,
                Status = SampleStatus.InTesting
            };
            db.Samples.Add(sample);
            await db.SaveChangesAsync();
        }

        var order = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = $"T{uid}",
            SectionId = section.Id,
            Status = status,
            CurrentStep = WorkflowStep.Waiting,
            AssignedAnalystId = assignedTo,
            IsSuperseded = superseded,
            OutsourcedToLab = outsourcedTo
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private static async Task AddRowsAsync(MicroLimsDbContext db, params object[] rows)
    {
        db.AddRange(rows);
        await db.SaveChangesAsync();
    }

    // ---- assert helpers (each opens its own context) ----

    private async Task<TestOrder> PersistedAsync(int orderId)
    {
        await using var assertDb = _fixture.CreateDbContext();
        return await assertDb.TestOrders.AsNoTracking().SingleAsync(t => t.Id == orderId);
    }

    private async Task<List<AuditLog>> AuditAsync(int orderId, params string[] actions)
    {
        await using var assertDb = _fixture.CreateDbContext();
        return await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == "TestOrder" && a.EntityId == orderId.ToString() && actions.Contains(a.Action))
            .OrderBy(a => a.Id).ToListAsync();
    }

    private async Task TakeAsync(int orderId, int userId, bool selfAssign = true)
    {
        await using var actDb = _fixture.CreateDbContext();
        await BuildService(actDb, selfAssign).TakeAsync(orderId, userId);
    }

    // ---- take ----

    [PostgresFact]
    public async Task Take_AssignsToCaller_WritesAuditRow_FromUnassignedToCallerName()
    {
        User analyst;
        TestOrder order;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            analyst = await NewAnalystAsync(arrange, "taker", w.Micro);
            order = await NewTestAsync(arrange, w, w.Micro);
        }

        await TakeAsync(order.Id, analyst.Id);

        var persisted = await PersistedAsync(order.Id);
        Assert.Equal(analyst.Id, persisted.AssignedAnalystId);
        var audit = Assert.Single(await AuditAsync(order.Id, "TEST_TAKEN"));
        Assert.Equal(analyst.Id, audit.UserId);
        Assert.Equal($"{order.TestCode}: Unassigned", audit.PreviousValue);
        Assert.Equal($"{order.TestCode}: {analyst.FullName}", audit.NewValue);
        Assert.Equal(order.Id, audit.TestOrderId);
        Assert.Equal(order.SampleId, audit.SampleId);
        Assert.Equal(Now, audit.Timestamp);
        Assert.NotNull(audit.SampleReferenceNumber);
    }

    [PostgresFact]
    public async Task Take_SettingOff_Throws()
    {
        User analyst;
        TestOrder order;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            analyst = await NewAnalystAsync(arrange, "taker", w.Micro);
            order = await NewTestAsync(arrange, w, w.Micro);
        }

        await Assert.ThrowsAsync<BusinessRuleException>(() => TakeAsync(order.Id, analyst.Id, selfAssign: false));

        Assert.Null((await PersistedAsync(order.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(order.Id, "TEST_TAKEN"));
    }

    [PostgresFact]
    public async Task Take_WithoutPermission_Returns403()
    {
        User reviewer;
        TestOrder order;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            // A built-in Reviewer is assignable by a Section Head but does not hold Tests.SelfAssign.
            reviewer = await NewUserAsync(arrange, "noperm", RoleType.Reviewer, null, w.Micro);
            order = await NewTestAsync(arrange, w, w.Micro);
        }

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => TakeAsync(order.Id, reviewer.Id));

        Assert.Null((await PersistedAsync(order.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(order.Id, "TEST_TAKEN"));
    }

    [PostgresFact]
    public async Task Endpoints_AreGatedByTestsSelfAssignPolicy()
    {
        foreach (var name in new[] { nameof(TestSelfAssignmentController.Take), nameof(TestSelfAssignmentController.Release) })
        {
            var action = typeof(TestSelfAssignmentController).GetMethod(name);
            Assert.NotNull(action);
            Assert.Contains(action!.GetCustomAttributes<AuthorizeAttribute>(), a => a.Policy == PermissionConstants.TestsSelfAssign);
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.NotNull(action.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpPostAttribute>());
        }

        Assert.Contains(PermissionConstants.TestsSelfAssign, PermissionConstants.All);
        Assert.Contains(PermissionConstants.TestsSelfAssign, PermissionConstants.Enforced);
        await Task.CompletedTask;
    }

    [PostgresFact]
    public async Task Take_NotMemberOfSection_Throws()
    {
        User viewAllNonMember, outsider;
        TestOrder order;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            // Can read every section (Sections.ViewAll) but belongs to Physicochemical only: reads the test, may not take it.
            viewAllNonMember = await NewUserAsync(arrange, "viewer", RoleType.Analyst, new[] { PermissionConstants.TestsSelfAssign, PermissionConstants.SectionsViewAll }, w.Fp);
            // Cannot even read the section.
            outsider = await NewAnalystAsync(arrange, "outsider", w.Fp);
            order = await NewTestAsync(arrange, w, w.Micro);
        }

        await Assert.ThrowsAsync<BusinessRuleException>(() => TakeAsync(order.Id, viewAllNonMember.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => TakeAsync(order.Id, outsider.Id));

        Assert.Null((await PersistedAsync(order.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(order.Id, "TEST_TAKEN"));
    }

    [PostgresFact]
    public async Task Take_UnknownTest_ThrowsNotFound()
    {
        User analyst;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            analyst = await NewAnalystAsync(arrange, "taker", w.Micro);
        }

        await Assert.ThrowsAsync<NotFoundException>(() => TakeAsync(int.MaxValue, analyst.Id));
    }

    [PostgresFact]
    public async Task Take_AlreadyAssigned_ThrowsWithHolderName()
    {
        User holder, taker;
        TestOrder order;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            holder = await NewAnalystAsync(arrange, "holder", w.Micro);
            taker = await NewAnalystAsync(arrange, "taker", w.Micro);
            order = await NewTestAsync(arrange, w, w.Micro, assignedTo: holder.Id);
        }

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => TakeAsync(order.Id, taker.Id));

        Assert.Equal($"Already taken by {holder.FullName}.", ex.Message);
        Assert.Equal(holder.Id, (await PersistedAsync(order.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(order.Id, "TEST_TAKEN"));
    }

    [PostgresFact]
    public async Task Take_SupersededClosedOrOutsourcedTest_Throws()
    {
        User analyst;
        TestOrder superseded, approved, outsourced;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            analyst = await NewAnalystAsync(arrange, "taker", w.Micro);
            superseded = await NewTestAsync(arrange, w, w.Micro, superseded: true);
            approved = await NewTestAsync(arrange, w, w.Micro, status: ApprovalStatus.Approved);
            outsourced = await NewTestAsync(arrange, w, w.Micro, outsourcedTo: "Outside Lab");
        }

        foreach (var order in new[] { superseded, approved, outsourced })
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() => TakeAsync(order.Id, analyst.Id));
            Assert.Null((await PersistedAsync(order.Id)).AssignedAnalystId);
            Assert.Empty(await AuditAsync(order.Id, "TEST_TAKEN"));
        }
    }

    [PostgresFact]
    public async Task Take_TwoContextsConcurrently_ExactlyOneSucceeds_OneAuditRow()
    {
        User winner, loser;
        TestOrder order;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            winner = await NewAnalystAsync(arrange, "winner", w.Micro);
            loser = await NewAnalystAsync(arrange, "loser", w.Micro);
            order = await NewTestAsync(arrange, w, w.Micro);
        }

        // The loser has already read the unassigned test (and its xmin) when the winner's take commits.
        Exception? caught = null;
        await using (var loserDb = _fixture.CreateDbContext())
        {
            var scope = DispatchProxy.Create<IUserSectionScopeService, HookedScope>();
            var hooked = (HookedScope)(object)scope;
            hooked.Inner = new UserSectionScopeService(loserDb);
            hooked.BeforeMembers = () => TakeAsync(order.Id, winner.Id);
            try { await BuildService(loserDb, scope: scope).TakeAsync(order.Id, loser.Id); }
            catch (Exception ex) { caught = ex; }
        }

        var refusal = Assert.IsType<BusinessRuleException>(caught);
        Assert.Equal($"Already taken by {winner.FullName}.", refusal.Message);
        Assert.Equal(winner.Id, (await PersistedAsync(order.Id)).AssignedAnalystId);
        var audit = Assert.Single(await AuditAsync(order.Id, "TEST_TAKEN"));
        Assert.Equal(winner.Id, audit.UserId);
        // Audit and state commit together: the loser's rolled-back save left no trace.
        await using var assertDb = _fixture.CreateDbContext();
        Assert.Empty(await assertDb.AuditLogs.AsNoTracking().Where(a => a.TestOrderId == order.Id && a.UserId == loser.Id).ToListAsync());
    }

    [PostgresFact]
    public async Task Take_ParallelRace_ExactlyOneWins_EveryRound()
    {
        User a, b;
        var orders = new List<TestOrder>();
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            a = await NewAnalystAsync(arrange, "racerA", w.Micro);
            b = await NewAnalystAsync(arrange, "racerB", w.Micro);
            for (var i = 0; i < 8; i++) orders.Add(await NewTestAsync(arrange, w, w.Micro));
        }

        foreach (var order in orders)
        {
            var gate = new TaskCompletionSource();
            async Task<Exception?> Run(int userId)
            {
                await gate.Task;
                try { await TakeAsync(order.Id, userId); return null; }
                catch (Exception ex) { return ex; }
            }

            var ta = Run(a.Id);
            var tb = Run(b.Id);
            gate.SetResult();
            var results = await Task.WhenAll(ta, tb);

            Assert.Single(results, r => r is null);
            var winnerId = (await PersistedAsync(order.Id)).AssignedAnalystId!.Value;
            Assert.Equal(Array.FindIndex(results, r => r is null) == 0 ? a.Id : b.Id, winnerId);
            var audit = Assert.Single(await AuditAsync(order.Id, "TEST_TAKEN"));
            Assert.Equal(winnerId, audit.UserId);
            var loser = Assert.IsType<BusinessRuleException>(results.Single(r => r is not null));
            Assert.StartsWith("Already taken by ", loser.Message);
        }
    }

    // ---- release ----

    [PostgresFact]
    public async Task Release_BeforeAnyRecord_Unassigns_Audited()
    {
        User analyst;
        TestOrder order;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            analyst = await NewAnalystAsync(arrange, "taker", w.Micro);
            order = await NewTestAsync(arrange, w, w.Micro);
        }

        await TakeAsync(order.Id, analyst.Id);
        await using (var actDb = _fixture.CreateDbContext())
            await BuildService(actDb).ReleaseAsync(order.Id, analyst.Id);

        Assert.Null((await PersistedAsync(order.Id)).AssignedAnalystId);
        var audit = Assert.Single(await AuditAsync(order.Id, "TEST_RELEASED"));
        Assert.Equal(analyst.Id, audit.UserId);
        Assert.Equal($"{order.TestCode}: {analyst.FullName}", audit.PreviousValue);
        Assert.Equal($"{order.TestCode}: Unassigned", audit.NewValue);
        Assert.Equal(Now, audit.Timestamp);
    }

    [PostgresFact]
    public async Task Release_AfterIncubationStarted_Throws()
    {
        User analyst;
        TestOrder order;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            analyst = await NewAnalystAsync(arrange, "taker", w.Micro);
            order = await NewTestAsync(arrange, w, w.Micro);
        }

        await TakeAsync(order.Id, analyst.Id);
        await using (var arrange = _fixture.CreateDbContext())
            await AddRowsAsync(arrange, new Incubation { TestOrderId = order.Id, StepNumber = 1, StepName = "Plate", StartedAt = Now, StartedByUserId = analyst.Id });

        await using (var actDb = _fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => BuildService(actDb).ReleaseAsync(order.Id, analyst.Id));
            Assert.Equal(CannotRelease, ex.Message);
        }

        Assert.Equal(analyst.Id, (await PersistedAsync(order.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(order.Id, "TEST_RELEASED"));
    }

    [PostgresFact]
    public async Task Release_AfterAnyRecordByCallerOnTheSample_Throws()
    {
        User analyst;
        TestOrder preparedTest, siblingTest, siblingOther;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            analyst = await NewAnalystAsync(arrange, "taker", w.Micro);
            preparedTest = await NewTestAsync(arrange, w, w.Micro);
            // A second sample with two tests: the caller records on one and takes the other.
            siblingTest = await NewTestAsync(arrange, w, w.Micro);
            var sample = await arrange.Samples.AsNoTracking().SingleAsync(s => s.Id == siblingTest.SampleId);
            siblingOther = await NewTestAsync(arrange, w, w.Micro, onSample: sample);
        }

        await TakeAsync(preparedTest.Id, analyst.Id);
        await TakeAsync(siblingTest.Id, analyst.Id);
        await using (var arrange = _fixture.CreateDbContext())
        {
            await AddRowsAsync(arrange,
                new SamplePreparation
                {
                    SampleId = preparedTest.SampleId, Amount = 10, Technique = "PourPlate", Diluent = "Diluent", Neutralizer = "None",
                    PreparedByUserId = analyst.Id, PreparedAt = Now
                },
                new Result { TestOrderId = siblingOther.Id, RawValue = "2", EnteredByUserId = analyst.Id, EnteredAt = Now });
        }

        foreach (var order in new[] { preparedTest, siblingTest })
        {
            await using var actDb = _fixture.CreateDbContext();
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => BuildService(actDb).ReleaseAsync(order.Id, analyst.Id));
            Assert.Equal(CannotRelease, ex.Message);
            Assert.Equal(analyst.Id, (await PersistedAsync(order.Id)).AssignedAnalystId);
            Assert.Empty(await AuditAsync(order.Id, "TEST_RELEASED"));
        }
    }

    [PostgresFact]
    public async Task Release_ByAnotherUser_Throws()
    {
        User taker, other;
        TestOrder order;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            taker = await NewAnalystAsync(arrange, "taker", w.Micro);
            other = await NewAnalystAsync(arrange, "other", w.Micro);
            order = await NewTestAsync(arrange, w, w.Micro);
        }

        await TakeAsync(order.Id, taker.Id);
        await using (var actDb = _fixture.CreateDbContext())
            await Assert.ThrowsAsync<BusinessRuleException>(() => BuildService(actDb).ReleaseAsync(order.Id, other.Id));

        Assert.Equal(taker.Id, (await PersistedAsync(order.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(order.Id, "TEST_RELEASED"));
    }

    [PostgresFact]
    public async Task Release_TestAssignedByASectionHead_Throws()
    {
        User analyst, head;
        TestOrder assignedByHead, takenThenReassigned;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            analyst = await NewAnalystAsync(arrange, "analyst", w.Micro);
            head = await NewUserAsync(arrange, "head", RoleType.SectionHead, null, w.Micro);
            assignedByHead = await NewTestAsync(arrange, w, w.Micro, assignedTo: analyst.Id);
            takenThenReassigned = await NewTestAsync(arrange, w, w.Micro);
            // Section Head assignments leave an "AssignedAnalyst" audit row (SampleAssignmentService); none was a take.
            await AddRowsAsync(arrange, HeadAudit(assignedByHead, head.Id, "AssignedAnalyst"));
        }

        await TakeAsync(takenThenReassigned.Id, analyst.Id);
        await using (var arrange = _fixture.CreateDbContext())
        {
            // The head reassigns the same analyst after the take: the last assignment is no longer a take by the caller.
            await AddRowsAsync(arrange, HeadAudit(takenThenReassigned, head.Id, "ReassignedAnalyst"));
        }

        foreach (var order in new[] { assignedByHead, takenThenReassigned })
        {
            await using var actDb = _fixture.CreateDbContext();
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => BuildService(actDb).ReleaseAsync(order.Id, analyst.Id));
            Assert.Equal(CannotRelease, ex.Message);
            Assert.Equal(analyst.Id, (await PersistedAsync(order.Id)).AssignedAnalystId);
            Assert.Empty(await AuditAsync(order.Id, "TEST_RELEASED"));
        }
    }

    private static AuditLog HeadAudit(TestOrder order, int headId, string action) => new()
    {
        EntityName = "TestOrder",
        EntityId = order.Id.ToString(),
        Action = action,
        PreviousValue = $"{order.TestCode}: Unassigned",
        NewValue = $"{order.TestCode}: analyst",
        UserId = headId,
        Timestamp = Now.AddMinutes(1),
        SampleId = order.SampleId,
        TestOrderId = order.Id
    };

    // ---- My Work groups ----

    [PostgresFact]
    public async Task AvailableGroup_ShowsUnassignedInMySections_WithTakeAction_HiddenWhenSettingOff()
    {
        User analyst;
        TestOrder mine;
        var created = new List<TestOrder>();
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            analyst = await NewAnalystAsync(arrange, "member", w.Micro);
            var other = await NewAnalystAsync(arrange, "someone", w.Micro);
            mine = await NewTestAsync(arrange, w, w.Micro);
            created.Add(mine);
            created.Add(await NewTestAsync(arrange, w, w.Fp));                                       // another section
            created.Add(await NewTestAsync(arrange, w, w.Micro, assignedTo: other.Id));              // assigned
            created.Add(await NewTestAsync(arrange, w, w.Micro, superseded: true));                  // superseded
            created.Add(await NewTestAsync(arrange, w, w.Micro, outsourcedTo: "Outside Lab"));       // outsourced
            created.Add(await NewTestAsync(arrange, w, w.Micro, status: ApprovalStatus.Approved));   // closed
        }

        var on = await MyWorkAsync(analyst.Id);
        var item = Assert.Single(Mine(on, "available", created.ToArray()));
        Assert.Equal($"available:{mine.Id}", item.Key);
        Assert.Equal(new[] { "take" }, item.Actions);
        Assert.Equal(mine.Id, item.Target.TestOrderId);
        Assert.Equal(mine.SampleId, item.Target.SampleId);
        Assert.Equal("MICRO", item.Lab);
        Assert.Empty(Mine(on, "unassigned", created.ToArray()));

        var off = await MyWorkAsync(analyst.Id, selfAssign: false);
        Assert.Empty(Mine(off, "available", created.ToArray()));
        Assert.Null(Group(off, "available"));

        // The badge counts exactly what the list shows: switching the setting off removes the available group's count.
        await using var summaryDb = _fixture.CreateDbContext();
        var summaryOn = await BuildMyWork(summaryDb).GetSummaryAsync(analyst.Id);
        await using var summaryOffDb = _fixture.CreateDbContext();
        var summaryOff = await BuildMyWork(summaryOffDb, selfAssign: false).GetSummaryAsync(analyst.Id);
        Assert.Equal(Group(on, "available")!.Count, summaryOn.Total - summaryOff.Total);
    }

    [PostgresFact]
    public async Task AvailableGroup_NotShownToReadOnlyViewerOutsideTheSection_OrWithoutPermission()
    {
        User viewer, head;
        TestOrder inMicro;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            viewer = await NewUserAsync(arrange, "viewer", RoleType.Analyst, new[] { PermissionConstants.TestsSelfAssign, PermissionConstants.SectionsViewAll }, w.Fp);
            head = await NewUserAsync(arrange, "head", RoleType.SectionHead, null, w.Micro);
            inMicro = await NewTestAsync(arrange, w, w.Micro);
        }

        // Reading a section is not membership: nothing to take there.
        Assert.Empty(Mine(await MyWorkAsync(viewer.Id), "available", inMicro));
        // A Section Head without Tests.SelfAssign gets the assign list, never the take list.
        Assert.Null(Group(await MyWorkAsync(head.Id), "available"));
    }

    [PostgresFact]
    public async Task UserWithBothPermissions_SeesBothGroups()
    {
        User both;
        TestOrder order;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            both = await NewUserAsync(arrange, "both", RoleType.Analyst, new[] { PermissionConstants.TestsSelfAssign, PermissionConstants.SamplesAssignAnalyst }, w.Micro);
            order = await NewTestAsync(arrange, w, w.Micro);
        }

        var response = await MyWorkAsync(both.Id);

        var available = Assert.Single(Mine(response, "available", order));
        Assert.Equal(new[] { "take" }, available.Actions);
        var unassigned = Assert.Single(Mine(response, "unassigned", order));
        Assert.Equal($"unassigned:{order.Id}", unassigned.Key);
        Assert.Equal("sample-assign", unassigned.Target.Type);
        Assert.Equal(order.SampleId, unassigned.Target.SampleId);
        Assert.Equal(new[] { "assign" }, unassigned.Actions);
    }

    [PostgresFact]
    public async Task UserWithAssignPermissionOnly_SeesOnlyUnassignedGroup()
    {
        User head;
        TestOrder order;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            head = await NewUserAsync(arrange, "head", RoleType.SectionHead, null, w.Micro);
            order = await NewTestAsync(arrange, w, w.Micro);
        }

        var response = await MyWorkAsync(head.Id);

        Assert.Equal(new[] { "assign" }, Assert.Single(Mine(response, "unassigned", order)).Actions);
        Assert.Null(Group(response, "available"));
    }

    [PostgresFact]
    public async Task UnassignedGroup_IsSectionScoped_AndNotAffectedByTheSelfAssignSetting()
    {
        User head, farHead;
        TestOrder inMicro;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            head = await NewUserAsync(arrange, "head", RoleType.SectionHead, null, w.Micro);
            farHead = await NewUserAsync(arrange, "farhead", RoleType.SectionHead, null, w.Fp);
            inMicro = await NewTestAsync(arrange, w, w.Micro);
        }

        Assert.Single(Mine(await MyWorkAsync(head.Id, selfAssign: false), "unassigned", inMicro));
        Assert.Empty(Mine(await MyWorkAsync(farHead.Id), "unassigned", inMicro));
    }

    [PostgresFact]
    public async Task TakenTest_MovesFromAvailableToToPerform_WithReleaseAction()
    {
        User analyst;
        TestOrder order;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            analyst = await NewAnalystAsync(arrange, "taker", w.Micro);
            order = await NewTestAsync(arrange, w, w.Micro, status: ApprovalStatus.Pending);
        }

        Assert.Single(Mine(await MyWorkAsync(analyst.Id), "available", order));

        await TakeAsync(order.Id, analyst.Id);

        var after = await MyWorkAsync(analyst.Id);
        Assert.Empty(Mine(after, "available", order));
        var toPerform = Assert.Single(Assert.IsType<MyWorkGroup>(Group(after, "toPerform")).Items, i => i.Target.TestOrderId == order.Id);
        Assert.Equal(new[] { "release" }, toPerform.Actions);

        // Once something is recorded the release action goes away.
        await using (var arrange = _fixture.CreateDbContext())
            await AddRowsAsync(arrange, new Result { TestOrderId = order.Id, RawValue = "1", EnteredByUserId = analyst.Id, EnteredAt = Now });
        var recorded = await MyWorkAsync(analyst.Id);
        var still = Assert.Single(Assert.IsType<MyWorkGroup>(Group(recorded, "toPerform")).Items, i => i.Target.TestOrderId == order.Id);
        Assert.Empty(still.Actions);
    }

    // ---- migration ----

    [PostgresFact]
    public async Task Migration_GrantsTestsSelfAssignToAnalystRole_AndWritesGrantAudit()
    {
        const string code = PermissionConstants.TestsSelfAssign;
        await using var db = _fixture.CreateDbContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        // Back to the state before the migration: no permission row, no grants.
        await db.Database.ExecuteSqlRawAsync(
            @"DELETE FROM ""RolePermissions"" WHERE ""PermissionId"" IN (SELECT ""Id"" FROM ""Permissions"" WHERE ""Code"" = {0});
              DELETE FROM ""Permissions"" WHERE ""Code"" = {0};", new object[] { code });
        // Production seeds the Analyst role as a system role; the fixture's copy is not flagged, so make the shape explicit.
        var analystRole = new Role { Type = RoleType.Analyst, Name = $"Analyst (system) {Guid.NewGuid():N}", IsSystemRole = true, IsActive = true };
        var customAnalystRole = new Role { Type = RoleType.Analyst, Name = $"Analyst (custom) {Guid.NewGuid():N}", IsSystemRole = false, IsActive = true };
        var sectionHeadRole = new Role { Type = RoleType.SectionHead, Name = $"Head (system) {Guid.NewGuid():N}", IsSystemRole = true, IsActive = true };
        db.Roles.AddRange(analystRole, customAnalystRole, sectionHeadRole);
        await db.SaveChangesAsync();
        var analystRoleId = analystRole.Id;
        var versionBefore = await db.Roles.Where(r => r.Id == analystRoleId).Select(r => r.PermissionVersion).SingleAsync();
        var auditsBefore = await db.AuditLogs.CountAsync(a => a.EntityName == "Role" && a.Action == "ROLE_PERMISSIONS_UPDATED");

        async Task RunAsync(IEnumerable<MigrationOperation> operations)
        {
            foreach (var op in operations.Cast<SqlOperation>())
                await db.Database.ExecuteSqlRawAsync(op.Sql);
        }

        var up = new AddTestsSelfAssignPermission().UpOperations;
        await RunAsync(up);

        var permission = await db.Permissions.AsNoTracking().SingleAsync(p => p.Code == code);
        Assert.True(permission.IsEnforced);
        Assert.False(string.IsNullOrWhiteSpace(permission.Description));

        var holders = await db.RolePermissions.AsNoTracking().Where(rp => rp.Permission!.Code == code)
            .Select(rp => new { rp.RoleId, rp.Role!.Type, rp.Role.IsSystemRole }).ToListAsync();
        var holder = Assert.Single(holders);
        Assert.Equal(analystRoleId, holder.RoleId);
        Assert.Equal(RoleType.Analyst, holder.Type);
        Assert.True(holder.IsSystemRole);
        Assert.Equal(versionBefore + 1, await db.Roles.AsNoTracking().Where(r => r.Id == analystRoleId).Select(r => r.PermissionVersion).SingleAsync());

        var audits = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == "Role" && a.Action == "ROLE_PERMISSIONS_UPDATED").OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(auditsBefore + 1, audits.Count);
        var grant = audits[^1];
        Assert.Equal(analystRoleId.ToString(), grant.EntityId);
        Assert.Contains(code, grant.NewValue);
        Assert.DoesNotContain(code, grant.PreviousValue);
        Assert.Equal("Migration 20261114075222_AddTestsSelfAssignPermission", grant.SystemProcessName);

        // Idempotent.
        var grants = await db.RolePermissions.CountAsync();
        await RunAsync(up);
        Assert.Equal(grants, await db.RolePermissions.CountAsync());

        // Only the built-in Analyst role: not custom roles, not Section Head or Reviewer.
        Assert.DoesNotContain(customAnalystRole.Id, holders.Select(h => h.RoleId));
        Assert.DoesNotContain(sectionHeadRole.Id, holders.Select(h => h.RoleId));

        // Never granted to a role that manages users or roles (segregation of duties), or to Section Head / Reviewer.
        Assert.False(await db.RolePermissions.AnyAsync(rp => rp.Permission!.Code == code
            && db.RolePermissions.Any(x => x.RoleId == rp.RoleId && (x.Permission!.Code == "Users.Manage" || x.Permission!.Code == "Roles.Manage"))));

        await RunAsync(new AddTestsSelfAssignPermission().DownOperations);
        Assert.False(await db.Permissions.AnyAsync(p => p.Code == code));
        Assert.False(await db.RolePermissions.AnyAsync(rp => rp.Permission!.Code == code));

        await tx.RollbackAsync();
    }
}
