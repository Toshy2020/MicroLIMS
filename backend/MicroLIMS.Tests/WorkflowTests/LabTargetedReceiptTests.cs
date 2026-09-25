using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Task 4 (lab separation): a receipt only creates test orders for the
// laboratories chosen at receiving time - the main Receiving page may
// target any lab (Samples.Receive), a lab's own workspace may only target
// labs the receiving user belongs to (ReceiptLabGuard).
public class LabTargetedReceiptTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static DocumentSection EnsureFpSection(MicroLimsDbContext db)
    {
        var dept = db.DocumentDepartments.FirstOrDefault(d => d.Code == "QC")
            ?? db.DocumentDepartments.Add(new DocumentDepartment { Name = "Quality Control", Code = "QC", IsActive = true }).Entity;
        db.SaveChanges();

        var section = db.DocumentSections.FirstOrDefault(s => s.Code == "FP");
        if (section == null)
        {
            section = new DocumentSection { Name = "Physicochemical Laboratory", Code = "FP", DepartmentId = dept.Id, IsActive = true };
            db.DocumentSections.Add(section);
            db.SaveChanges();
        }
        return section;
    }

    // Seeds sections MICRO/FP, TestDefinition rows TAMC (MICRO) and ASSAY
    // (FP), and an FP item with both tests assigned.
    private static async Task<(MicroLimsDbContext db, Item item, DocumentSection micro, DocumentSection fp)> SeedAsync()
    {
        var db = NewDb();
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = EnsureFpSection(db);
        TestServiceFactory.EnsureProductionStage(db);

        db.TestDefinitions.Add(new TestDefinition { Code = "TAMC", DisplayName = "Total Aerobic Microbial Count", SectionId = micro.Id });
        db.TestDefinitions.Add(new TestDefinition { Code = "ASSAY", DisplayName = "Assay", SectionId = fp.Id });

        var item = new Item { Name = "Tablet", Code = "TAB-01", Category = SampleCategory.FinishedProduct, IsActive = true };
        item.AssignedTests.Add(new SampleTest { TestCode = "TAMC" });
        item.AssignedTests.Add(new SampleTest { TestCode = "ASSAY" });
        db.Items.Add(item);
        await db.SaveChangesAsync();

        return (db, item, micro, fp);
    }

    private static ItemBasedReceiveRequest Request(int itemId, int[]? targets) =>
        new(itemId, 1, "10 units", "Analyst", "LOT-1", "CTRL-1", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), "F.P", 1, targets);

    // ---- ProductWorkflowEngine.ReceiveAsync respects TargetSectionIds ----

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
    public async Task Receive_BothLabs_CreatesAllOrders()
    {
        var (db, item, micro, fp) = await SeedAsync();
        var engine = new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db));

        var sample = await engine.ReceiveAsync(Request(item.Id, new[] { micro.Id, fp.Id }));

        Assert.Equal(new[] { "TAMC", "ASSAY" }, sample.TestOrders.Select(t => t.TestCode));
        Assert.Equal(SamplePreparationStatus.NeedsPreparation, sample.PreparationStatus);
    }

    [Fact]
    public async Task Receive_TargetLabWithNoTestsForItem_IsRefused()
    {
        var db = NewDb();
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = EnsureFpSection(db);
        TestServiceFactory.EnsureProductionStage(db);
        db.TestDefinitions.Add(new TestDefinition { Code = "ASSAY", DisplayName = "Assay", SectionId = fp.Id });

        var item = new Item { Name = "Tablet", Code = "TAB-02", Category = SampleCategory.FinishedProduct, IsActive = true };
        item.AssignedTests.Add(new SampleTest { TestCode = "ASSAY" });
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var engine = new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.ReceiveAsync(Request(item.Id, new[] { micro.Id })));
        Assert.Contains("has no tests for Microbiology Laboratory", ex.Message);
    }

    [Fact]
    public async Task Receive_NullTargets_KeepsEveryAssignedTest()
    {
        var (db, item, micro, fp) = await SeedAsync();
        var engine = new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db));

        var sample = await engine.ReceiveAsync(Request(item.Id, null));

        Assert.Equal(new[] { "TAMC", "ASSAY" }, sample.TestOrders.Select(t => t.TestCode));
        Assert.Equal(SamplePreparationStatus.NeedsPreparation, sample.PreparationStatus);
    }

    // ---- ReceiptLabGuard ----

    [Fact]
    public async Task Guard_EmptyTargets_IsRefused()
    {
        var (db, _, _, _) = await SeedAsync();
        var scope = new UserSectionScopeService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ReceiptLabGuard.ResolveTargetsAsync(db, scope, 1, canReceiveForAnyLab: false, requested: Array.Empty<int>()));
        Assert.Equal("Choose at least one laboratory to receive the sample for.", ex.Message);
    }

    [Fact]
    public async Task Receive_LabUserTargetsOtherLab_IsForbidden()
    {
        var (db, _, micro, fp) = await SeedAsync();
        var analystRole = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(analystRole);
        await db.SaveChangesAsync();

        var user = new User { Username = "micro_analyst", FullName = "Micro Analyst", RoleId = analystRole.Id, Role = analystRole, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = micro.DepartmentId, SectionId = micro.Id });
        await db.SaveChangesAsync();

        var scope = new UserSectionScopeService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => ReceiptLabGuard.ResolveTargetsAsync(db, scope, user.Id, canReceiveForAnyLab: false, requested: new[] { fp.Id }));
    }

    [Fact]
    public async Task Guard_ReceiverWithPrivilege_MayTargetAnyLab()
    {
        var (db, _, micro, fp) = await SeedAsync();
        var analystRole = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(analystRole);
        await db.SaveChangesAsync();

        var user = new User { Username = "receiver", FullName = "Receiver", RoleId = analystRole.Id, Role = analystRole, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        // Deliberately no UserOrgMembership row - the Samples.Receive
        // privilege bypasses membership scoping entirely.

        var scope = new UserSectionScopeService(db);

        var result = await ReceiptLabGuard.ResolveTargetsAsync(db, scope, user.Id, canReceiveForAnyLab: true, requested: new[] { micro.Id, fp.Id });

        Assert.Equal(new HashSet<int> { micro.Id, fp.Id }, result.ToHashSet());
    }
}
