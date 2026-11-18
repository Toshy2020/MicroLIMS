using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Tests.ArchitectureTests;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class SampleBulkAssignPostgresTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly PostgresTestFixture _fixture;

    public SampleBulkAssignPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

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

    private static SampleBulkAssignmentService BuildBulkService(MicroLimsDbContext db, IUserSectionScopeService? scope = null)
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(Now));
        var effectiveScope = scope ?? new UserSectionScopeService(db);
        var workspace = new TestingWorkspaceService(db, effectiveScope, clock);
        var singleAssign = new SampleAssignmentService(db, workspace, effectiveScope, clock);
        return new SampleBulkAssignmentService(db, singleAssign, effectiveScope);
    }

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

    private static async Task<User> NewUserAsync(MicroLimsDbContext db, string label, RoleType roleType, params DocumentSection[] memberOf)
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var role = await db.Roles.AsNoTracking().Where(r => r.Type == roleType).OrderBy(r => r.Id).FirstAsync();
        var user = new User
        {
            Username = $"{label}_{uid}",
            FullName = $"{label} {uid}",
            PasswordHash = "x",
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        foreach (var section in memberOf)
        {
            db.UserOrgMemberships.Add(new UserOrgMembership
            {
                UserId = user.Id,
                DepartmentId = section.DepartmentId,
                SectionId = section.Id
            });
        }

        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<(Sample Sample, TestOrder Order)> NewSampleWithTestAsync(
        MicroLimsDbContext db,
        World w,
        DocumentSection section,
        int? assignedTo = null,
        ApprovalStatus status = ApprovalStatus.Pending,
        Sample? existingSample = null)
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var sample = existingSample;
        if (sample is null)
        {
            var receiver = await db.Users.AsNoTracking().OrderBy(u => u.Id).FirstAsync();
            sample = new Sample
            {
                ReferenceNumber = $"BA-{uid}",
                Category = SampleCategory.FinishedProduct,
                CauseOfTestingId = w.CauseId,
                ReceivedAt = Now.AddHours(-2),
                ReceivedByUserId = receiver.Id,
                Status = status == ApprovalStatus.Approved ? SampleStatus.Approved : SampleStatus.InTesting
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
            AssignedAnalystId = assignedTo
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();
        return (sample, order);
    }

    private async Task<TestOrder> PersistedAsync(int orderId)
    {
        await using var assertDb = _fixture.CreateDbContext();
        return await assertDb.TestOrders.AsNoTracking().SingleAsync(t => t.Id == orderId);
    }

    private static readonly string[] AssignmentAuditActions =
        { "AssignedAnalyst", "ReassignedAnalyst", "UnassignedAnalyst" };

    private async Task<List<AuditLog>> AuditAsync(int orderId)
    {
        await using var assertDb = _fixture.CreateDbContext();
        return await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == "TestOrder"
                && a.EntityId == orderId.ToString()
                && AssignmentAuditActions.Contains(a.Action))
            .OrderBy(a => a.Id)
            .ToListAsync();
    }

    [PostgresFact]
    public async Task AllValid_AssignsEveryTest_OneAuditRowPerChangedTest()
    {
        User head, analyst;
        Sample sample1, sample2;
        TestOrder order1A, order1B, order2A;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrangeDb);
            head = await NewUserAsync(arrangeDb, "head", RoleType.SectionHead, w.Micro);
            analyst = await NewUserAsync(arrangeDb, "analyst", RoleType.Analyst, w.Micro);
            (sample1, order1A) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro);
            (_, order1B) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro, existingSample: sample1);
            (sample2, order2A) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro);
        }

        BulkAssignResult result;
        await using (var actDb = _fixture.CreateDbContext())
        {
            result = await BuildBulkService(actDb).AssignAsync(
                new BulkAssignRequest(new[] { sample1.Id, sample2.Id }, analyst.Id, "Urgent batch"),
                head.Id);
        }

        Assert.Equal(2, result.AssignedCount);
        Assert.Equal(0, result.RefusedCount);
        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, r =>
        {
            Assert.True(r.Assigned);
            Assert.Null(r.RefusedReason);
        });

        foreach (var (sample, order) in new[] { (sample1, order1A), (sample1, order1B), (sample2, order2A) })
        {
            var persisted = await PersistedAsync(order.Id);
            Assert.Equal(analyst.Id, persisted.AssignedAnalystId);

            var audit = Assert.Single(await AuditAsync(order.Id));
            Assert.Equal("AssignedAnalyst", audit.Action);
            Assert.Equal(head.Id, audit.UserId);
            Assert.Equal($"{order.TestCode}: Unassigned", audit.PreviousValue);
            Assert.Equal($"{order.TestCode}: {analyst.FullName} | Reason: Urgent batch", audit.NewValue);
            Assert.Equal(Now, audit.Timestamp);
            Assert.Equal(sample.Id, audit.SampleId);
            Assert.Equal(order.Id, audit.TestOrderId);
            Assert.Equal(sample.ReferenceNumber, audit.SampleReferenceNumber);
        }
    }

    [PostgresFact]
    public async Task Mixed_AssignsValid_ReturnsRefusedWithReason_NoAuditForRefused()
    {
        User head, analyst;
        Sample validSample, closedSample;
        TestOrder validOrder, closedOrder;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrangeDb);
            head = await NewUserAsync(arrangeDb, "head", RoleType.SectionHead, w.Micro);
            analyst = await NewUserAsync(arrangeDb, "analyst", RoleType.Analyst, w.Micro);
            (validSample, validOrder) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro);
            (closedSample, closedOrder) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro, status: ApprovalStatus.Approved);
        }

        BulkAssignResult result;
        await using (var actDb = _fixture.CreateDbContext())
        {
            result = await BuildBulkService(actDb).AssignAsync(
                new BulkAssignRequest(new[] { validSample.Id, closedSample.Id }, analyst.Id, null),
                head.Id);
        }

        Assert.Equal(1, result.AssignedCount);
        Assert.Equal(1, result.RefusedCount);
        Assert.Equal(2, result.Rows.Count);

        var okRow = Assert.Single(result.Rows, r => r.SampleId == validSample.Id);
        Assert.True(okRow.Assigned);
        Assert.Null(okRow.RefusedReason);
        Assert.Equal(validSample.ReferenceNumber, okRow.ReferenceNumber);

        var refusedRow = Assert.Single(result.Rows, r => r.SampleId == closedSample.Id);
        Assert.False(refusedRow.Assigned);
        Assert.Equal("This sample has no open tests you can assign.", refusedRow.RefusedReason);
        Assert.Equal(closedSample.ReferenceNumber, refusedRow.ReferenceNumber);

        Assert.Equal(analyst.Id, (await PersistedAsync(validOrder.Id)).AssignedAnalystId);
        Assert.Single(await AuditAsync(validOrder.Id));

        Assert.Null((await PersistedAsync(closedOrder.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(closedOrder.Id));
    }

    // The bulk path judges each row with the single path's own check, so what one accepts or refuses the other
    // does too: a voided sample and a sample under review (result entered) are both refused with the same sentence.
    [PostgresFact]
    public async Task BulkAndSingleAssign_AgreeOnVoidedAndUnderReviewSamples()
    {
        User head, analyst;
        Sample voided, voidedForSingle, underReview, underReviewForSingle;
        TestOrder voidedOrder, underReviewOrder, underReviewSingleOrder;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrangeDb);
            head = await NewUserAsync(arrangeDb, "head", RoleType.SectionHead, w.Micro);
            analyst = await NewUserAsync(arrangeDb, "analyst", RoleType.Analyst, w.Micro);
            (voided, voidedOrder) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro, status: ApprovalStatus.Voided);
            (voidedForSingle, _) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro, status: ApprovalStatus.Voided);
            (underReview, underReviewOrder) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro, status: ApprovalStatus.ResultEntered);
            (underReviewForSingle, underReviewSingleOrder) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro, status: ApprovalStatus.ResultEntered);
        }

        BulkAssignResult result;
        await using (var actDb = _fixture.CreateDbContext())
        {
            result = await BuildBulkService(actDb).AssignAsync(
                new BulkAssignRequest(new[] { voided.Id, underReview.Id }, analyst.Id, null), head.Id);
        }

        string singleVoidedRefusal, singleUnderReviewRefusal;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var scope = new UserSectionScopeService(actDb);
            var clock = new FixedTimeProvider(new DateTimeOffset(Now));
            var single = new SampleAssignmentService(actDb, new TestingWorkspaceService(actDb, scope, clock), scope, clock);
            var voidedEx = await Assert.ThrowsAsync<BusinessRuleException>(() => single.AssignAnalystAsync(voidedForSingle.Id, analyst.Id, head.Id));
            singleVoidedRefusal = voidedEx.Message;
            var reviewEx = await Assert.ThrowsAsync<BusinessRuleException>(() => single.AssignAnalystAsync(underReviewForSingle.Id, analyst.Id, head.Id));
            singleUnderReviewRefusal = reviewEx.Message;
            Assert.Equal(MessageCodes.SampleCannotReassignWithResult, reviewEx.Code);
        }

        Assert.Equal(0, result.AssignedCount);
        Assert.Equal(2, result.RefusedCount);

        var voidedRow = Assert.Single(result.Rows, r => r.SampleId == voided.Id);
        Assert.False(voidedRow.Assigned);
        Assert.Equal(singleVoidedRefusal, voidedRow.RefusedReason);

        var underReviewRow = Assert.Single(result.Rows, r => r.SampleId == underReview.Id);
        Assert.False(underReviewRow.Assigned);
        Assert.Equal(singleUnderReviewRefusal, underReviewRow.RefusedReason);
        Assert.Equal("Tests with a result cannot be reassigned.", underReviewRow.RefusedReason);

        Assert.Null((await PersistedAsync(voidedOrder.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(voidedOrder.Id));
        Assert.Null((await PersistedAsync(underReviewOrder.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(underReviewOrder.Id));
        Assert.Null((await PersistedAsync(underReviewSingleOrder.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(underReviewSingleOrder.Id));
    }

    [PostgresFact]
    public async Task SampleInSectionICannotWrite_IsRefused_NotAssigned()
    {
        User headInMicroOnly, analystInBoth;
        Sample microSample, fpSample;
        TestOrder microOrder, fpOrder;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrangeDb);
            headInMicroOnly = await NewUserAsync(arrangeDb, "headMicro", RoleType.SectionHead, w.Micro);
            analystInBoth = await NewUserAsync(arrangeDb, "analystBoth", RoleType.Analyst, w.Micro, w.Fp);
            (microSample, microOrder) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro);
            (fpSample, fpOrder) = await NewSampleWithTestAsync(arrangeDb, w, w.Fp);
        }

        BulkAssignResult result;
        await using (var actDb = _fixture.CreateDbContext())
        {
            result = await BuildBulkService(actDb).AssignAsync(
                new BulkAssignRequest(new[] { microSample.Id, fpSample.Id }, analystInBoth.Id, null),
                headInMicroOnly.Id);
        }

        Assert.Equal(1, result.AssignedCount);
        Assert.Equal(1, result.RefusedCount);

        var fpRow = Assert.Single(result.Rows, r => r.SampleId == fpSample.Id);
        Assert.False(fpRow.Assigned);
        Assert.Equal("This sample has no open tests you can assign.", fpRow.RefusedReason);

        Assert.Null((await PersistedAsync(fpOrder.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(fpOrder.Id));

        Assert.Equal(analystInBoth.Id, (await PersistedAsync(microOrder.Id)).AssignedAnalystId);
        Assert.Single(await AuditAsync(microOrder.Id));
    }

    [PostgresFact]
    public async Task AnalystNotMemberOfSampleSection_RowRefused()
    {
        User headInBoth, fpOnlyAnalyst;
        Sample microSample, fpSample;
        TestOrder microOrder, fpOrder;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrangeDb);
            headInBoth = await NewUserAsync(arrangeDb, "headBoth", RoleType.SectionHead, w.Micro, w.Fp);
            fpOnlyAnalyst = await NewUserAsync(arrangeDb, "analystFp", RoleType.Analyst, w.Fp);
            (microSample, microOrder) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro);
            (fpSample, fpOrder) = await NewSampleWithTestAsync(arrangeDb, w, w.Fp);
        }

        BulkAssignResult result;
        await using (var actDb = _fixture.CreateDbContext())
        {
            result = await BuildBulkService(actDb).AssignAsync(
                new BulkAssignRequest(new[] { microSample.Id, fpSample.Id }, fpOnlyAnalyst.Id, null),
                headInBoth.Id);
        }

        Assert.Equal(1, result.AssignedCount);
        Assert.Equal(1, result.RefusedCount);

        var microRow = Assert.Single(result.Rows, r => r.SampleId == microSample.Id);
        Assert.False(microRow.Assigned);
        Assert.NotNull(microRow.RefusedReason);
        Assert.Contains("is not a member of the", microRow.RefusedReason);

        Assert.Null((await PersistedAsync(microOrder.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(microOrder.Id));

        Assert.Equal(fpOnlyAnalyst.Id, (await PersistedAsync(fpOrder.Id)).AssignedAnalystId);
        Assert.Single(await AuditAsync(fpOrder.Id));
    }

    [PostgresFact]
    public async Task SampleApprovedBetweenSelectionAndSubmit_RowRefused_OthersAssigned()
    {
        User head, analyst;
        Sample s1, s2, s3;
        TestOrder o1, o2, o3;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrangeDb);
            head = await NewUserAsync(arrangeDb, "head", RoleType.SectionHead, w.Micro);
            analyst = await NewUserAsync(arrangeDb, "analyst", RoleType.Analyst, w.Micro);
            (s1, o1) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro);
            (s2, o2) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro);
            (s3, o3) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro);
        }

        // Between UI selection and form submit, sample 2 is approved in another request.
        await using (var approveDb = _fixture.CreateDbContext())
        {
            var orderToApprove = await approveDb.TestOrders.SingleAsync(t => t.Id == o2.Id);
            var sampleToApprove = await approveDb.Samples.SingleAsync(s => s.Id == s2.Id);
            orderToApprove.Status = ApprovalStatus.Approved;
            sampleToApprove.Status = SampleStatus.Approved;
            await approveDb.SaveChangesAsync();
        }

        BulkAssignResult result;
        await using (var actDb = _fixture.CreateDbContext())
        {
            result = await BuildBulkService(actDb).AssignAsync(
                new BulkAssignRequest(new[] { s1.Id, s2.Id, s3.Id }, analyst.Id, "Shift handover"),
                head.Id);
        }

        Assert.Equal(2, result.AssignedCount);
        Assert.Equal(1, result.RefusedCount);

        var r1 = Assert.Single(result.Rows, r => r.SampleId == s1.Id);
        var r2 = Assert.Single(result.Rows, r => r.SampleId == s2.Id);
        var r3 = Assert.Single(result.Rows, r => r.SampleId == s3.Id);
        Assert.True(r1.Assigned);
        Assert.False(r2.Assigned);
        Assert.Equal("This sample has no open tests you can assign.", r2.RefusedReason);
        Assert.True(r3.Assigned);

        Assert.Equal(analyst.Id, (await PersistedAsync(o1.Id)).AssignedAnalystId);
        Assert.Single(await AuditAsync(o1.Id));

        Assert.Null((await PersistedAsync(o2.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(o2.Id));

        Assert.Equal(analyst.Id, (await PersistedAsync(o3.Id)).AssignedAnalystId);
        Assert.Single(await AuditAsync(o3.Id));
    }

    [PostgresFact]
    public async Task Request201Ids_400_NothingChanged()
    {
        User head, analyst;
        Sample s1;
        TestOrder o1;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrangeDb);
            head = await NewUserAsync(arrangeDb, "head", RoleType.SectionHead, w.Micro);
            analyst = await NewUserAsync(arrangeDb, "analyst", RoleType.Analyst, w.Micro);
            (s1, o1) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro);
        }

        var ids201 = new[] { s1.Id }.Concat(Enumerable.Range(1_000_000, 200)).ToList();

        await using (var actDb = _fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<BusinessRuleException>(
                () => BuildBulkService(actDb).AssignAsync(new BulkAssignRequest(ids201, analyst.Id, null), head.Id));
            await Assert.ThrowsAsync<BusinessRuleException>(
                () => BuildBulkService(actDb).AssignAsync(new BulkAssignRequest(Array.Empty<int>(), analyst.Id, null), head.Id));
        }

        Assert.Null((await PersistedAsync(o1.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(o1.Id));
    }

    [PostgresFact]
    public async Task DuplicateIds_400()
    {
        User head, analyst;
        Sample s1;
        TestOrder o1;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrangeDb);
            head = await NewUserAsync(arrangeDb, "head", RoleType.SectionHead, w.Micro);
            analyst = await NewUserAsync(arrangeDb, "analyst", RoleType.Analyst, w.Micro);
            (s1, o1) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro);
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<BusinessRuleException>(
                () => BuildBulkService(actDb).AssignAsync(new BulkAssignRequest(new[] { s1.Id, s1.Id }, analyst.Id, null), head.Id));
        }

        Assert.Null((await PersistedAsync(o1.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(o1.Id));
    }

    [PostgresFact]
    public async Task UnknownAnalyst_400()
    {
        User head;
        Sample s1;
        TestOrder o1;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrangeDb);
            head = await NewUserAsync(arrangeDb, "head", RoleType.SectionHead, w.Micro);
            (s1, o1) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro);
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            await Assert.ThrowsAsync<BusinessRuleException>(
                () => BuildBulkService(actDb).AssignAsync(new BulkAssignRequest(new[] { s1.Id }, int.MaxValue, null), head.Id));
        }

        Assert.Null((await PersistedAsync(o1.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(o1.Id));
    }

    [PostgresFact]
    public async Task WithoutPermission_403()
    {
        var method = typeof(SampleController).GetMethod("BulkAssign");
        Assert.NotNull(method);

        var authAttr = method!.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>()
            .FirstOrDefault(a => a.Policy == PermissionConstants.SamplesAssignAnalyst);
        Assert.NotNull(authAttr);
        Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());

        var postAttr = method.GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(postAttr);
        Assert.Equal("bulk-assign", postAttr!.Template);

        var allowed = AuthorizationMatrixTests.AllowedRoles<SampleController>("BulkAssign");
        Assert.NotNull(allowed);
        Assert.Contains(RoleType.SectionHead, allowed);
        Assert.DoesNotContain(RoleType.Analyst, allowed);
        Assert.DoesNotContain(RoleType.Reviewer, allowed);
        Assert.DoesNotContain(RoleType.Qa, allowed);
        Assert.DoesNotContain(RoleType.SystemAdministrator, allowed);

        await using var db = _fixture.CreateDbContext();
        var perm = await db.Permissions.AsNoTracking().SingleAsync(p => p.Code == PermissionConstants.SamplesAssignAnalyst);
        Assert.True(perm.IsEnforced);
    }

    [PostgresFact]
    public async Task AlreadyAssignedToSameAnalyst_CountsAsAssigned_NoNewAuditRow()
    {
        User head, analyst;
        Sample s1;
        TestOrder o1;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrangeDb);
            head = await NewUserAsync(arrangeDb, "head", RoleType.SectionHead, w.Micro);
            analyst = await NewUserAsync(arrangeDb, "analyst", RoleType.Analyst, w.Micro);
            (s1, o1) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro, assignedTo: analyst.Id);
        }

        BulkAssignResult result;
        await using (var actDb = _fixture.CreateDbContext())
        {
            result = await BuildBulkService(actDb).AssignAsync(
                new BulkAssignRequest(new[] { s1.Id }, analyst.Id, "Re-run bulk"),
                head.Id);
        }

        Assert.Equal(1, result.AssignedCount);
        Assert.Equal(0, result.RefusedCount);
        var row = Assert.Single(result.Rows);
        Assert.True(row.Assigned);
        Assert.Null(row.RefusedReason);

        Assert.Equal(analyst.Id, (await PersistedAsync(o1.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(o1.Id));
    }

    [PostgresFact]
    public async Task FailureMidApply_RollsBackEarlierRowsInTransaction()
    {
        User head, analyst, otherAnalyst;
        Sample s1, s2;
        TestOrder o1, o2;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrangeDb);
            head = await NewUserAsync(arrangeDb, "head", RoleType.SectionHead, w.Micro);
            analyst = await NewUserAsync(arrangeDb, "analyst", RoleType.Analyst, w.Micro);
            otherAnalyst = await NewUserAsync(arrangeDb, "other", RoleType.Analyst, w.Micro);
            (s1, o1) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro);
            (s2, o2) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro);
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var scope = DispatchProxy.Create<IUserSectionScopeService, HookedScope>();
            var hooked = (HookedScope)(object)scope;
            hooked.Inner = new UserSectionScopeService(actDb);
            hooked.BeforeMembers = async () =>
            {
                await using var concurrentDb = _fixture.CreateDbContext();
                var concurrentOrder2 = await concurrentDb.TestOrders.SingleAsync(t => t.Id == o2.Id);
                concurrentOrder2.AssignedAnalystId = otherAnalyst.Id;
                await concurrentDb.SaveChangesAsync();
            };

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                () => BuildBulkService(actDb, scope).AssignAsync(
                    new BulkAssignRequest(new[] { s1.Id, s2.Id }, analyst.Id, "Atomic batch"),
                    head.Id));
        }

        Assert.Null((await PersistedAsync(o1.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(o1.Id));
    }

    private static SampleAssignmentService BuildSingleService(MicroLimsDbContext db)
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(Now));
        var scope = new UserSectionScopeService(db);
        return new SampleAssignmentService(db, new TestingWorkspaceService(db, scope, clock), scope, clock);
    }

    [PostgresFact]
    public async Task SingleWholeSampleAssign_WithResult_Refused_AndSkipsTestsWithResultWhenOthersOpen()
    {
        User head, originalAnalyst, otherAnalyst;
        Sample readySample, reviewedSample, mixedSample;
        TestOrder readyOrder, reviewedOrder, mixedOpenOrder, mixedDoneOrder;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrangeDb);
            head = await NewUserAsync(arrangeDb, "head", RoleType.SectionHead, w.Micro);
            originalAnalyst = await NewUserAsync(arrangeDb, "orig", RoleType.Analyst, w.Micro);
            otherAnalyst = await NewUserAsync(arrangeDb, "other", RoleType.Analyst, w.Micro);

            (readySample, readyOrder) = await NewSampleWithTestAsync(
                arrangeDb, w, w.Micro, assignedTo: originalAnalyst.Id, status: ApprovalStatus.ResultEntered);
            readyOrder.CurrentStep = WorkflowStep.Ready;

            (reviewedSample, reviewedOrder) = await NewSampleWithTestAsync(
                arrangeDb, w, w.Micro, assignedTo: originalAnalyst.Id, status: ApprovalStatus.Reviewed);
            reviewedOrder.CurrentStep = WorkflowStep.Reviewed;

            (mixedSample, mixedOpenOrder) = await NewSampleWithTestAsync(
                arrangeDb, w, w.Micro, status: ApprovalStatus.Pending);
            (_, mixedDoneOrder) = await NewSampleWithTestAsync(
                arrangeDb, w, w.Micro, assignedTo: originalAnalyst.Id, status: ApprovalStatus.ResultEntered, existingSample: mixedSample);
            mixedDoneOrder.CurrentStep = WorkflowStep.Ready;
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var svc = BuildSingleService(actDb);

            var readyEx = await Assert.ThrowsAsync<BusinessRuleException>(
                () => svc.AssignAnalystAsync(readySample.Id, otherAnalyst.Id, head.Id, "Late swap"));
            Assert.Equal("Tests with a result cannot be reassigned.", readyEx.Message);
            Assert.Equal(MessageCodes.SampleCannotReassignWithResult, readyEx.Code);

            var unassignEx = await Assert.ThrowsAsync<BusinessRuleException>(
                () => svc.AssignAnalystAsync(readySample.Id, null, head.Id, "Late unassign"));
            Assert.Equal("Tests with a result cannot be reassigned.", unassignEx.Message);
            Assert.Equal(MessageCodes.SampleCannotReassignWithResult, unassignEx.Code);

            var reviewedEx = await Assert.ThrowsAsync<BusinessRuleException>(
                () => svc.AssignAnalystAsync(reviewedSample.Id, otherAnalyst.Id, head.Id, "Already reviewed"));
            Assert.Equal("Tests with a result cannot be reassigned.", reviewedEx.Message);
            Assert.Equal(MessageCodes.SampleCannotReassignWithResult, reviewedEx.Code);

            await svc.AssignAnalystAsync(mixedSample.Id, otherAnalyst.Id, head.Id, "Assign remaining");
        }

        Assert.Equal(originalAnalyst.Id, (await PersistedAsync(readyOrder.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(readyOrder.Id));

        Assert.Equal(originalAnalyst.Id, (await PersistedAsync(reviewedOrder.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(reviewedOrder.Id));

        Assert.Equal(otherAnalyst.Id, (await PersistedAsync(mixedOpenOrder.Id)).AssignedAnalystId);
        var mixedAudit = Assert.Single(await AuditAsync(mixedOpenOrder.Id));
        Assert.Equal("AssignedAnalyst", mixedAudit.Action);
        Assert.Equal(head.Id, mixedAudit.UserId);

        Assert.Equal(originalAnalyst.Id, (await PersistedAsync(mixedDoneOrder.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(mixedDoneOrder.Id));
    }

    [PostgresFact]
    public async Task PerTestAssign_WithResult_Refused_NothingPersistedNoAudit()
    {
        User head, originalAnalyst, otherAnalyst;
        Sample sample;
        TestOrder openOrder, resultOrder;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrangeDb);
            head = await NewUserAsync(arrangeDb, "head", RoleType.SectionHead, w.Micro);
            originalAnalyst = await NewUserAsync(arrangeDb, "orig", RoleType.Analyst, w.Micro);
            otherAnalyst = await NewUserAsync(arrangeDb, "other", RoleType.Analyst, w.Micro);

            (sample, openOrder) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro, status: ApprovalStatus.Pending);
            (_, resultOrder) = await NewSampleWithTestAsync(
                arrangeDb, w, w.Micro, assignedTo: originalAnalyst.Id, status: ApprovalStatus.ResultEntered, existingSample: sample);
            resultOrder.CurrentStep = WorkflowStep.Ready;
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var svc = BuildSingleService(actDb);

            // Reassigning the test with a result is refused.
            var reassignEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.AssignTestsAsync(sample.Id, new[]
                {
                    new TestAnalystAssignment(openOrder.Id, otherAnalyst.Id),
                    new TestAnalystAssignment(resultOrder.Id, otherAnalyst.Id)
                }, head.Id, "Late swap"));
            Assert.Equal("Tests with a result cannot be reassigned.", reassignEx.Message);
            Assert.Equal(MessageCodes.SampleCannotReassignWithResult, reassignEx.Code);

            // Unassigning the test with a result is also refused.
            var unassignEx = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                svc.AssignTestsAsync(sample.Id, new[]
                {
                    new TestAnalystAssignment(resultOrder.Id, null)
                }, head.Id, "Late clear"));
            Assert.Equal("Tests with a result cannot be reassigned.", unassignEx.Message);
            Assert.Equal(MessageCodes.SampleCannotReassignWithResult, unassignEx.Code);
        }

        Assert.Null((await PersistedAsync(openOrder.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(openOrder.Id));
        Assert.Equal(originalAnalyst.Id, (await PersistedAsync(resultOrder.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(resultOrder.Id));
    }

    [PostgresFact]
    public async Task BulkAssign_SubmittedTests_Refused_NothingPersistedNoAudit()
    {
        User head, originalAnalyst, otherAnalyst;
        Sample validSample, submittedSample;
        TestOrder validOrder, submittedOrder;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrangeDb);
            head = await NewUserAsync(arrangeDb, "head", RoleType.SectionHead, w.Micro);
            originalAnalyst = await NewUserAsync(arrangeDb, "orig", RoleType.Analyst, w.Micro);
            otherAnalyst = await NewUserAsync(arrangeDb, "other", RoleType.Analyst, w.Micro);

            (validSample, validOrder) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro, status: ApprovalStatus.InProgress);
            (submittedSample, submittedOrder) = await NewSampleWithTestAsync(
                arrangeDb, w, w.Micro, assignedTo: originalAnalyst.Id, status: ApprovalStatus.ResultEntered);
            submittedOrder.CurrentStep = WorkflowStep.Ready;
            await arrangeDb.SaveChangesAsync();
        }

        BulkAssignResult result;
        await using (var actDb = _fixture.CreateDbContext())
        {
            result = await BuildBulkService(actDb).AssignAsync(
                new BulkAssignRequest(new[] { validSample.Id, submittedSample.Id }, otherAnalyst.Id, "Bulk shift"),
                head.Id);
        }

        Assert.Equal(1, result.AssignedCount);
        Assert.Equal(1, result.RefusedCount);

        var okRow = Assert.Single(result.Rows, r => r.SampleId == validSample.Id);
        Assert.True(okRow.Assigned);
        Assert.Null(okRow.RefusedReason);

        var refusedRow = Assert.Single(result.Rows, r => r.SampleId == submittedSample.Id);
        Assert.False(refusedRow.Assigned);
        Assert.Equal("Tests with a result cannot be reassigned.", refusedRow.RefusedReason);

        Assert.Equal(otherAnalyst.Id, (await PersistedAsync(validOrder.Id)).AssignedAnalystId);
        Assert.Single(await AuditAsync(validOrder.Id));

        Assert.Equal(originalAnalyst.Id, (await PersistedAsync(submittedOrder.Id)).AssignedAnalystId);
        Assert.Empty(await AuditAsync(submittedOrder.Id));
    }

    [PostgresFact]
    public async Task IntermediateResultRow_WhileInProgress_CanStillBeAssignedBySinglePerTestAndBulk()
    {
        User head, analyst1, analyst2;
        Sample singleSample, perTestSample, bulkSample;
        TestOrder singleOrder, perTestOrder, bulkOrder;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrangeDb);
            head = await NewUserAsync(arrangeDb, "head", RoleType.SectionHead, w.Micro);
            analyst1 = await NewUserAsync(arrangeDb, "a1", RoleType.Analyst, w.Micro);
            analyst2 = await NewUserAsync(arrangeDb, "a2", RoleType.Analyst, w.Micro);

            (singleSample, singleOrder) = await NewSampleWithTestAsync(
                arrangeDb, w, w.Micro, assignedTo: analyst1.Id, status: ApprovalStatus.InProgress);
            singleOrder.CurrentStep = WorkflowStep.Incubating;

            (perTestSample, perTestOrder) = await NewSampleWithTestAsync(
                arrangeDb, w, w.Micro, assignedTo: analyst1.Id, status: ApprovalStatus.InProgress);
            perTestOrder.CurrentStep = WorkflowStep.Running;

            (bulkSample, bulkOrder) = await NewSampleWithTestAsync(
                arrangeDb, w, w.Micro, assignedTo: analyst1.Id, status: ApprovalStatus.InProgress);
            bulkOrder.CurrentStep = WorkflowStep.Incubating;

            foreach (var order in new[] { singleOrder, perTestOrder, bulkOrder })
            {
                arrangeDb.Results.Add(new Result
                {
                    TestOrderId = order.Id,
                    RawValue = "step-1",
                    InterpretedValue = "intermediate reading",
                    Type = ResultType.Numeric,
                    EnteredByUserId = analyst1.Id,
                    EnteredAt = Now.AddMinutes(-10)
                });
            }
            await arrangeDb.SaveChangesAsync();
        }

        BulkAssignResult bulkResult;
        await using (var actDb = _fixture.CreateDbContext())
        {
            var single = BuildSingleService(actDb);
            await single.AssignAnalystAsync(singleSample.Id, analyst2.Id, head.Id, "Handover mid-test");
            await single.AssignTestsAsync(perTestSample.Id, new[] { new TestAnalystAssignment(perTestOrder.Id, analyst2.Id) }, head.Id, "Per-test handover");
            bulkResult = await BuildBulkService(actDb).AssignAsync(
                new BulkAssignRequest(new[] { bulkSample.Id }, analyst2.Id, "Bulk handover"),
                head.Id);
        }

        Assert.Equal(1, bulkResult.AssignedCount);
        Assert.Equal(0, bulkResult.RefusedCount);

        foreach (var (order, expectedReason) in new[]
        {
            (singleOrder, "Handover mid-test"),
            (perTestOrder, "Per-test handover"),
            (bulkOrder, "Bulk handover")
        })
        {
            Assert.Equal(analyst2.Id, (await PersistedAsync(order.Id)).AssignedAnalystId);
            var audit = Assert.Single(await AuditAsync(order.Id));
            Assert.Equal("ReassignedAnalyst", audit.Action);
            Assert.Equal(head.Id, audit.UserId);
            Assert.Equal($"{order.TestCode}: {analyst1.FullName}", audit.PreviousValue);
            Assert.Equal($"{order.TestCode}: {analyst2.FullName} | Reason: {expectedReason}", audit.NewValue);
        }
    }

    [PostgresFact]
    public async Task AssignBeforeResult_SinglePerTestAndBulk_SucceedsWithAuditRow()
    {
        User head, analyst1, analyst2;
        Sample singleSample, perTestSample, bulkSample;
        TestOrder singleOrder, perTestOrder, bulkOrder;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrangeDb);
            head = await NewUserAsync(arrangeDb, "head", RoleType.SectionHead, w.Micro);
            analyst1 = await NewUserAsync(arrangeDb, "a1", RoleType.Analyst, w.Micro);
            analyst2 = await NewUserAsync(arrangeDb, "a2", RoleType.Analyst, w.Micro);

            (singleSample, singleOrder) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro, status: ApprovalStatus.Pending);
            (perTestSample, perTestOrder) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro, assignedTo: analyst1.Id, status: ApprovalStatus.InProgress);
            perTestOrder.CurrentStep = WorkflowStep.Incubating;
            (bulkSample, bulkOrder) = await NewSampleWithTestAsync(arrangeDb, w, w.Micro, status: ApprovalStatus.InProgress);
            bulkOrder.CurrentStep = WorkflowStep.Running;
            await arrangeDb.SaveChangesAsync();
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var single = BuildSingleService(actDb);
            await single.AssignAnalystAsync(singleSample.Id, analyst1.Id, head.Id, "Initial");
            await single.AssignTestsAsync(perTestSample.Id, new[] { new TestAnalystAssignment(perTestOrder.Id, analyst2.Id) }, head.Id, "Before result");
            var bulkResult = await BuildBulkService(actDb).AssignAsync(
                new BulkAssignRequest(new[] { bulkSample.Id }, analyst2.Id, "Bulk before result"),
                head.Id);
            Assert.Equal(1, bulkResult.AssignedCount);
            Assert.Equal(0, bulkResult.RefusedCount);
        }

        Assert.Equal(analyst1.Id, (await PersistedAsync(singleOrder.Id)).AssignedAnalystId);
        var singleAudit = Assert.Single(await AuditAsync(singleOrder.Id));
        Assert.Equal("AssignedAnalyst", singleAudit.Action);
        Assert.Equal($"{singleOrder.TestCode}: {analyst1.FullName} | Reason: Initial", singleAudit.NewValue);

        Assert.Equal(analyst2.Id, (await PersistedAsync(perTestOrder.Id)).AssignedAnalystId);
        var perTestAudit = Assert.Single(await AuditAsync(perTestOrder.Id));
        Assert.Equal("ReassignedAnalyst", perTestAudit.Action);
        Assert.Equal($"{perTestOrder.TestCode}: {analyst1.FullName}", perTestAudit.PreviousValue);
        Assert.Equal($"{perTestOrder.TestCode}: {analyst2.FullName} | Reason: Before result", perTestAudit.NewValue);

        Assert.Equal(analyst2.Id, (await PersistedAsync(bulkOrder.Id)).AssignedAnalystId);
        var bulkAudit = Assert.Single(await AuditAsync(bulkOrder.Id));
        Assert.Equal("AssignedAnalyst", bulkAudit.Action);
        Assert.Equal($"{bulkOrder.TestCode}: {analyst2.FullName} | Reason: Bulk before result", bulkAudit.NewValue);
    }
}
