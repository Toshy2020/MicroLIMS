using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// A laboratory dashboard (lab=MICRO / lab=FP) shows only that laboratory's
// activity: a Microbiology dashboard never counts Physicochemical work and
// vice versa, including for users who belong to both laboratories.
public class DashboardLabScopeTests
{
    private sealed record Fixture(
        MicroLimsDbContext Db, DocumentSection Micro, DocumentSection Fp,
        User MicroAnalyst, User DualAnalyst, User Admin, CauseOfTesting Cause);

    private static async Task<Fixture> SeedAsync()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var dept = new DocumentDepartment { Name = "Quality Control", Code = "QC", IsActive = true };
        db.DocumentDepartments.Add(dept);
        await db.SaveChangesAsync();
        var micro = new DocumentSection { Name = "Microbiology", Code = "MICRO", DepartmentId = dept.Id, IsActive = true };
        var fp = new DocumentSection { Name = "Physicochemical", Code = "FP", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.AddRange(micro, fp);

        var analystRole = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        var adminRole = new Role { Name = "System Administrator", Type = RoleType.SystemAdministrator, IsActive = true };
        db.Roles.AddRange(analystRole, adminRole);
        await db.SaveChangesAsync();

        var microAnalyst = new User { Username = "micro", FullName = "Micro Analyst", RoleId = analystRole.Id, Role = analystRole, IsActive = true };
        var dualAnalyst = new User { Username = "dual", FullName = "Dual Analyst", RoleId = analystRole.Id, Role = analystRole, IsActive = true };
        var admin = new User { Username = "admin", FullName = "Administrator", RoleId = adminRole.Id, Role = adminRole, IsActive = true };
        db.Users.AddRange(microAnalyst, dualAnalyst, admin);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.AddRange(
            new UserOrgMembership { UserId = microAnalyst.Id, DepartmentId = dept.Id, SectionId = micro.Id },
            new UserOrgMembership { UserId = dualAnalyst.Id, DepartmentId = dept.Id, SectionId = micro.Id },
            new UserOrgMembership { UserId = dualAnalyst.Id, DepartmentId = dept.Id, SectionId = fp.Id });
        var cause = new CauseOfTesting { Name = "Routine Release" };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        return new Fixture(db, micro, fp, microAnalyst, dualAnalyst, admin, cause);
    }

    private static DashboardLabScopeService Resolver(MicroLimsDbContext db) => new(db, new UserSectionScopeService(db));

    private static Sample NewSample(Fixture f, string reference) => new()
    {
        ReferenceNumber = reference,
        ControlNumber = reference,
        Category = SampleCategory.FinishedProduct,
        Status = SampleStatus.InTesting,
        CauseOfTestingId = f.Cause.Id,
        CauseOfTesting = f.Cause,
        ReceivedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Resolve_NarrowsToTheRequestedLaboratory_AndKeepsTheOldScopeWithoutOne()
    {
        var f = await SeedAsync();
        var resolver = Resolver(f.Db);

        var legacy = await resolver.ResolveAsync(f.DualAnalyst.Id, null);
        Assert.Null(legacy.LabCode);
        Assert.Equal(new[] { f.Micro.Id, f.Fp.Id }.OrderBy(x => x), legacy.SectionIds!.OrderBy(x => x));

        var micro = await resolver.ResolveAsync(f.DualAnalyst.Id, "MICRO");
        Assert.Equal(new[] { f.Micro.Id }, micro.SectionIds);
        Assert.True(micro.IsMicrobiology);

        var fp = await resolver.ResolveAsync(f.DualAnalyst.Id, "fp");
        Assert.Equal(new[] { f.Fp.Id }, fp.SectionIds);
        Assert.False(fp.IsMicrobiology);

        // Administrators are unrestricted, but a laboratory view still narrows.
        var adminFp = await resolver.ResolveAsync(f.Admin.Id, "FP");
        Assert.Equal(new[] { f.Fp.Id }, adminFp.SectionIds);
    }

    [Fact]
    public async Task Resolve_RefusesALaboratoryTheUserDoesNotBelongTo_AndUnknownCodes()
    {
        var f = await SeedAsync();
        var resolver = Resolver(f.Db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => resolver.ResolveAsync(f.MicroAnalyst.Id, "FP"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.ResolveAsync(f.DualAnalyst.Id, "CHEM"));
    }

    [Fact]
    public async Task MyTasks_ForOneLaboratory_ExcludeTheOtherLaboratorysTests()
    {
        var f = await SeedAsync();
        var now = DateTime.UtcNow;
        var sample = NewSample(f, "S-1");
        var microOrder = new TestOrder { TestCode = "TAMC", SectionId = f.Micro.Id, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running, AssignedAnalystId = f.DualAnalyst.Id };
        microOrder.Incubations.Add(new Incubation { StepName = "TAMC Incubation", ExpectedReadingAt = now.AddHours(2) });
        var fpOrder = new TestOrder { TestCode = "ASSAY", SectionId = f.Fp.Id, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Running, AssignedAnalystId = f.DualAnalyst.Id };
        fpOrder.Incubations.Add(new Incubation { StepName = "Hold", ExpectedReadingAt = now.AddHours(3) });
        sample.TestOrders.Add(microOrder);
        sample.TestOrders.Add(fpOrder);
        f.Db.Samples.Add(sample);
        await f.Db.SaveChangesAsync();

        var service = new MyTasksService(f.Db, new UserSectionScopeService(f.Db));
        var resolver = Resolver(f.Db);

        var all = await service.GetMyTasksAsync(f.DualAnalyst.Id);
        Assert.Equal(2, all.Count);

        var micro = await service.GetMyTasksAsync(f.DualAnalyst.Id, await resolver.ResolveAsync(f.DualAnalyst.Id, "MICRO"));
        Assert.Equal(microOrder.Id, Assert.Single(micro).TestOrderId);

        var fp = await service.GetMyTasksAsync(f.DualAnalyst.Id, await resolver.ResolveAsync(f.DualAnalyst.Id, "FP"));
        Assert.Equal(fpOrder.Id, Assert.Single(fp).TestOrderId);
    }

    [Fact]
    public async Task AnalystMetrics_ForOneLaboratory_CountOnlyThatLaboratorysTestsAndMedia()
    {
        var f = await SeedAsync();
        var sample = NewSample(f, "S-2");
        var microOrder = new TestOrder { TestCode = "TAMC", SectionId = f.Micro.Id, Status = ApprovalStatus.InProgress, AssignedAnalystId = f.DualAnalyst.Id };
        var fpOrder = new TestOrder { TestCode = "ASSAY", SectionId = f.Fp.Id, Status = ApprovalStatus.InProgress, AssignedAnalystId = f.DualAnalyst.Id };
        var fpOrder2 = new TestOrder { TestCode = "PH", SectionId = f.Fp.Id, Status = ApprovalStatus.Pending, AssignedAnalystId = f.DualAnalyst.Id };
        sample.TestOrders.Add(microOrder);
        sample.TestOrders.Add(fpOrder);
        sample.TestOrders.Add(fpOrder2);
        f.Db.Samples.Add(sample);
        await f.Db.SaveChangesAsync();

        f.Db.Results.AddRange(
            new Result { TestOrderId = microOrder.Id, RawValue = "10", EnteredByUserId = f.DualAnalyst.Id, EnteredAt = DateTime.UtcNow },
            new Result { TestOrderId = fpOrder.Id, RawValue = "99.1", EnteredByUserId = f.DualAnalyst.Id, EnteredAt = DateTime.UtcNow });
        var microMaterial = new Material { MaterialName = "TSA", SectionId = f.Micro.Id, MaterialType = MaterialType.DehydratedMedia };
        f.Db.Materials.Add(microMaterial);
        await f.Db.SaveChangesAsync();
        f.Db.Media.Add(new Media { MaterialId = microMaterial.Id, LotNumber = "TSA/1/26", PreparedByUserId = f.DualAnalyst.Id, PreparedAt = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddDays(30) });
        await f.Db.SaveChangesAsync();

        var service = TestServiceFactory.Dashboard(f.Db);
        var resolver = Resolver(f.Db);

        var micro = await service.GetAnalystMetricsAsync(f.DualAnalyst.Id, await resolver.ResolveAsync(f.DualAnalyst.Id, "MICRO"));
        Assert.Equal(1, micro.TestsCompletedToday);
        Assert.Equal(1, micro.MediaLotsPreparedToday);
        Assert.Equal(1, micro.ActiveAssignedOrders);

        var fp = await service.GetAnalystMetricsAsync(f.DualAnalyst.Id, await resolver.ResolveAsync(f.DualAnalyst.Id, "FP"));
        Assert.Equal(1, fp.TestsCompletedToday);
        Assert.Equal(0, fp.MediaLotsPreparedToday);
        Assert.Equal(2, fp.ActiveAssignedOrders);

        // No laboratory: unchanged, spans both.
        var all = await service.GetAnalystMetricsAsync(f.DualAnalyst.Id);
        Assert.Equal(2, all.TestsCompletedToday);
        Assert.Equal(3, all.ActiveAssignedOrders);
    }

    [Fact]
    public async Task Notifications_ForOneLaboratory_KeepOnlyThatLaboratorysAndLabNeutralOnes()
    {
        var f = await SeedAsync();
        var microSample = NewSample(f, "S-M");
        var microOrder = new TestOrder { TestCode = "TAMC", SectionId = f.Micro.Id, Status = ApprovalStatus.InProgress };
        microSample.TestOrders.Add(microOrder);
        var fpSample = NewSample(f, "S-F");
        var fpOrder = new TestOrder { TestCode = "ASSAY", SectionId = f.Fp.Id, Status = ApprovalStatus.InProgress };
        fpSample.TestOrders.Add(fpOrder);
        f.Db.Samples.AddRange(microSample, fpSample);
        await f.Db.SaveChangesAsync();

        var userId = f.DualAnalyst.Id;
        f.Db.NotificationLogs.AddRange(
            new NotificationLog { UserId = userId, Type = "TestReturnedForRevision", Message = "micro test returned", SampleId = microSample.Id, TestOrderId = microOrder.Id },
            new NotificationLog { UserId = userId, Type = "TestReturnedForRevision", Message = "fp test returned", SampleId = fpSample.Id, TestOrderId = fpOrder.Id },
            new NotificationLog { UserId = userId, Type = "MediaExpiry", Message = "TSA lot expiring" },
            new NotificationLog { UserId = userId, Type = "SampleNote", Message = "fp sample note", SampleId = fpSample.Id },
            new NotificationLog { UserId = userId, Type = "DirectMessage", Message = "new message" });
        await f.Db.SaveChangesAsync();

        // Already computed: return the persisted log as seeded.
        var throttle = new NotificationRecomputeThrottle(TimeSpan.FromHours(1));
        throttle.MarkComputed(userId, DateTime.UtcNow);
        var service = new DashboardNotificationService(f.Db, new NoOpNotificationService(), new NoOpEmailSender(), new UserSectionScopeService(f.Db), throttle);
        var resolver = Resolver(f.Db);

        var micro = (await service.GetNotificationsAsync(RoleType.Analyst, userId, await resolver.ResolveAsync(userId, "MICRO")))
            .Select(n => n.Message).OrderBy(m => m, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "TSA lot expiring", "micro test returned", "new message" }, micro);

        var fp = (await service.GetNotificationsAsync(RoleType.Analyst, userId, await resolver.ResolveAsync(userId, "FP")))
            .Select(n => n.Message).OrderBy(m => m, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "fp sample note", "fp test returned", "new message" }, fp);

        // The header bell (no laboratory) still sees everything.
        Assert.Equal(5, (await service.GetNotificationsAsync(RoleType.Analyst, userId)).Count);
    }
}
