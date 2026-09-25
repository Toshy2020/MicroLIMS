using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SectionAccessGuardTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static (DocumentSection microSec, DocumentSection otherSec, User microUser, User adminUser) SeedBase(MicroLimsDbContext db)
    {
        var microSec = TestServiceFactory.EnsureMicroSection(db);

        var otherSec = db.DocumentSections.FirstOrDefault(s => s.Code == "CHEM");
        if (otherSec == null)
        {
            otherSec = new DocumentSection
            {
                Name = "Chemistry Laboratory",
                Code = "CHEM",
                DepartmentId = microSec.DepartmentId,
                IsActive = true
            };
            db.DocumentSections.Add(otherSec);
            db.SaveChanges();
        }

        var analystRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.Analyst);
        if (analystRole == null)
        {
            analystRole = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
            db.Roles.Add(analystRole);
            db.SaveChanges();
        }

        var adminRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.SystemAdministrator);
        if (adminRole == null)
        {
            adminRole = new Role { Name = "System Administrator", Type = RoleType.SystemAdministrator, IsActive = true };
            db.Roles.Add(adminRole);
            db.SaveChanges();
        }

        var microUser = new User
        {
            Username = "microUser_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Micro Analyst",
            RoleId = analystRole.Id,
            Role = analystRole,
            IsActive = true
        };
        var adminUser = new User
        {
            Username = "adminUser_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Sys Admin",
            RoleId = adminRole.Id,
            Role = adminRole,
            IsActive = true
        };
        db.Users.AddRange(microUser, adminUser);
        db.SaveChanges();

        TestServiceFactory.AssignUserToMicroSection(db, microUser.Id);

        return (microSec, otherSec, microUser, adminUser);
    }

    [Fact]
    public async Task EnsureTestOrderAccessAsync_InScope_Allows()
    {
        await using var db = NewDb();
        var (microSec, _, microUser, _) = SeedBase(db);

        var order = new TestOrder { TestCode = "TAMC", SectionId = microSec.Id };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        await guard.EnsureTestOrderAccessAsync(microUser.Id, order.Id);
    }

    [Fact]
    public async Task EnsureTestOrderAccessAsync_OutOfScope_Throws()
    {
        await using var db = NewDb();
        var (_, otherSec, microUser, _) = SeedBase(db);

        var order = new TestOrder { TestCode = "HPLC", SectionId = otherSec.Id };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            guard.EnsureTestOrderAccessAsync(microUser.Id, order.Id));
        Assert.Equal("This test belongs to a laboratory section you are not assigned to.", ex.Message);
    }

    [Fact]
    public async Task EnsureTestOrderAccessAsync_Admin_ReturnsSilently()
    {
        await using var db = NewDb();
        var (_, otherSec, _, adminUser) = SeedBase(db);

        var order = new TestOrder { TestCode = "HPLC", SectionId = otherSec.Id };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        await guard.EnsureTestOrderAccessAsync(adminUser.Id, order.Id);
    }

    [Fact]
    public async Task EnsureTestOrderAccessAsync_NonExistentId_ReturnsSilently()
    {
        await using var db = NewDb();
        var (_, _, microUser, _) = SeedBase(db);

        var guard = new UserSectionScopeService(db);
        await guard.EnsureTestOrderAccessAsync(microUser.Id, 99999);
    }

    [Fact]
    public async Task EnsureTestOrdersAccessAsync_AllInScope_Allows()
    {
        await using var db = NewDb();
        var (microSec, _, microUser, _) = SeedBase(db);

        var o1 = new TestOrder { TestCode = "TAMC", SectionId = microSec.Id };
        var o2 = new TestOrder { TestCode = "TYMC", SectionId = microSec.Id };
        db.TestOrders.AddRange(o1, o2);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        await guard.EnsureTestOrdersAccessAsync(microUser.Id, new[] { o1.Id, o2.Id });
    }

    [Fact]
    public async Task EnsureTestOrdersAccessAsync_OneOutOfScope_Throws()
    {
        await using var db = NewDb();
        var (microSec, otherSec, microUser, _) = SeedBase(db);

        var o1 = new TestOrder { TestCode = "TAMC", SectionId = microSec.Id };
        var o2 = new TestOrder { TestCode = "HPLC", SectionId = otherSec.Id };
        db.TestOrders.AddRange(o1, o2);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            guard.EnsureTestOrdersAccessAsync(microUser.Id, new[] { o1.Id, o2.Id }));
        Assert.Equal("This test belongs to a laboratory section you are not assigned to.", ex.Message);
    }

    [Fact]
    public async Task EnsureTestOrdersAccessAsync_Admin_ReturnsSilently()
    {
        await using var db = NewDb();
        var (_, otherSec, _, adminUser) = SeedBase(db);

        var o1 = new TestOrder { TestCode = "HPLC", SectionId = otherSec.Id };
        db.TestOrders.Add(o1);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        await guard.EnsureTestOrdersAccessAsync(adminUser.Id, new[] { o1.Id });
    }

    [Fact]
    public async Task EnsureTestOrdersAccessAsync_NonExistentId_ReturnsSilently()
    {
        await using var db = NewDb();
        var (_, _, microUser, _) = SeedBase(db);

        var guard = new UserSectionScopeService(db);
        await guard.EnsureTestOrdersAccessAsync(microUser.Id, new[] { 99991, 99992 });
    }

    [Fact]
    public async Task EnsureSampleAccessAsync_InScope_Allows()
    {
        await using var db = NewDb();
        var (microSec, _, microUser, _) = SeedBase(db);

        var sample = new Sample { ReferenceNumber = "SMP-001", ControlNumber = "C-001", Status = SampleStatus.InTesting };
        sample.TestOrders.Add(new TestOrder { TestCode = "TAMC", SectionId = microSec.Id, IsSuperseded = false });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        await guard.EnsureSampleAccessAsync(microUser.Id, sample.Id);
    }

    [Fact]
    public async Task EnsureSampleAccessAsync_OutOfScope_Throws()
    {
        await using var db = NewDb();
        var (_, otherSec, microUser, _) = SeedBase(db);

        var sample = new Sample { ReferenceNumber = "SMP-002", ControlNumber = "C-002", Status = SampleStatus.InTesting };
        sample.TestOrders.Add(new TestOrder { TestCode = "HPLC", SectionId = otherSec.Id, IsSuperseded = false });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            guard.EnsureSampleAccessAsync(microUser.Id, sample.Id));
        Assert.Equal("This test belongs to a laboratory section you are not assigned to.", ex.Message);
    }

    [Fact]
    public async Task EnsureSampleAccessAsync_OnlySupersededInScope_Throws()
    {
        await using var db = NewDb();
        var (microSec, otherSec, microUser, _) = SeedBase(db);

        var sample = new Sample { ReferenceNumber = "SMP-003", ControlNumber = "C-003", Status = SampleStatus.InTesting };
        sample.TestOrders.Add(new TestOrder { TestCode = "TAMC", SectionId = microSec.Id, IsSuperseded = true });
        sample.TestOrders.Add(new TestOrder { TestCode = "HPLC", SectionId = otherSec.Id, IsSuperseded = false });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            guard.EnsureSampleAccessAsync(microUser.Id, sample.Id));
        Assert.Equal("This test belongs to a laboratory section you are not assigned to.", ex.Message);
    }

    [Fact]
    public async Task EnsureSampleAccessAsync_Admin_ReturnsSilently()
    {
        await using var db = NewDb();
        var (_, otherSec, _, adminUser) = SeedBase(db);

        var sample = new Sample { ReferenceNumber = "SMP-004", ControlNumber = "C-004", Status = SampleStatus.InTesting };
        sample.TestOrders.Add(new TestOrder { TestCode = "HPLC", SectionId = otherSec.Id, IsSuperseded = false });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        await guard.EnsureSampleAccessAsync(adminUser.Id, sample.Id);
    }

    [Fact]
    public async Task EnsureSampleAccessAsync_NonExistentId_ReturnsSilently()
    {
        await using var db = NewDb();
        var (_, _, microUser, _) = SeedBase(db);

        var guard = new UserSectionScopeService(db);
        await guard.EnsureSampleAccessAsync(microUser.Id, 99999);
    }

    [Fact]
    public async Task EnsureIncubationAccessAsync_InScope_Allows()
    {
        await using var db = NewDb();
        var (microSec, _, microUser, _) = SeedBase(db);

        var order = new TestOrder { TestCode = "TAMC", SectionId = microSec.Id };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        var inc = new Incubation { TestOrderId = order.Id, StepName = "CountIncubation" };
        db.Incubations.Add(inc);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        await guard.EnsureIncubationAccessAsync(microUser.Id, inc.Id);
    }

    [Fact]
    public async Task EnsureIncubationAccessAsync_OutOfScope_Throws()
    {
        await using var db = NewDb();
        var (_, otherSec, microUser, _) = SeedBase(db);

        var order = new TestOrder { TestCode = "HPLC", SectionId = otherSec.Id };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        var inc = new Incubation { TestOrderId = order.Id, StepName = "PrepIncubation" };
        db.Incubations.Add(inc);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            guard.EnsureIncubationAccessAsync(microUser.Id, inc.Id));
        Assert.Equal("This test belongs to a laboratory section you are not assigned to.", ex.Message);
    }

    [Fact]
    public async Task EnsureIncubationAccessAsync_WithoutTestOrderId_ReturnsSilently()
    {
        await using var db = NewDb();
        var (_, _, microUser, _) = SeedBase(db);

        var inc = new Incubation { TestOrderId = null, StepName = "MediaIncubation" };
        db.Incubations.Add(inc);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        await guard.EnsureIncubationAccessAsync(microUser.Id, inc.Id);
    }

    [Fact]
    public async Task EnsureIncubationAccessAsync_Admin_ReturnsSilently()
    {
        await using var db = NewDb();
        var (_, otherSec, _, adminUser) = SeedBase(db);

        var order = new TestOrder { TestCode = "HPLC", SectionId = otherSec.Id };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        var inc = new Incubation { TestOrderId = order.Id, StepName = "PrepIncubation" };
        db.Incubations.Add(inc);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        await guard.EnsureIncubationAccessAsync(adminUser.Id, inc.Id);
    }

    [Fact]
    public async Task EnsureIncubationAccessAsync_NonExistentId_ReturnsSilently()
    {
        await using var db = NewDb();
        var (_, _, microUser, _) = SeedBase(db);

        var guard = new UserSectionScopeService(db);
        await guard.EnsureIncubationAccessAsync(microUser.Id, 99999);
    }

    [Fact]
    public async Task EnsurePathogenSampleAccessAsync_InScope_Allows()
    {
        await using var db = NewDb();
        var (microSec, _, microUser, _) = SeedBase(db);

        db.TestDefinitions.Add(new TestDefinition { Code = "SALM", WorkflowType = WorkflowType.Observation });
        await db.SaveChangesAsync();

        var sample = new Sample { ReferenceNumber = "SMP-P1", ControlNumber = "C-P1", Status = SampleStatus.InTesting };
        sample.TestOrders.Add(new TestOrder { TestCode = "SALM", SectionId = microSec.Id, IsSuperseded = false });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        await guard.EnsurePathogenSampleAccessAsync(microUser.Id, sample.Id);
    }

    [Fact]
    public async Task EnsurePathogenSampleAccessAsync_OneOfTwoPathogenOrdersOutOfScope_Throws()
    {
        await using var db = NewDb();
        var (microSec, otherSec, microUser, _) = SeedBase(db);

        db.TestDefinitions.AddRange(
            new TestDefinition { Code = "SALM", WorkflowType = WorkflowType.Observation },
            new TestDefinition { Code = "LIST", WorkflowType = WorkflowType.Observation }
        );
        await db.SaveChangesAsync();

        var sample = new Sample { ReferenceNumber = "SMP-P2", ControlNumber = "C-P2", Status = SampleStatus.InTesting };
        sample.TestOrders.Add(new TestOrder { TestCode = "SALM", SectionId = microSec.Id, IsSuperseded = false });
        sample.TestOrders.Add(new TestOrder { TestCode = "LIST", SectionId = otherSec.Id, IsSuperseded = false });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            guard.EnsurePathogenSampleAccessAsync(microUser.Id, sample.Id));
        Assert.Equal("This test belongs to a laboratory section you are not assigned to.", ex.Message);
    }

    [Fact]
    public async Task EnsurePathogenSampleAccessAsync_CountTestOutOfScope_ObservationInScope_Allows()
    {
        await using var db = NewDb();
        var (microSec, otherSec, microUser, _) = SeedBase(db);

        db.TestDefinitions.AddRange(
            new TestDefinition { Code = "SALM", WorkflowType = WorkflowType.Observation },
            new TestDefinition { Code = "TAMC", WorkflowType = WorkflowType.CountTest }
        );
        await db.SaveChangesAsync();

        var sample = new Sample { ReferenceNumber = "SMP-P3", ControlNumber = "C-P3", Status = SampleStatus.InTesting };
        sample.TestOrders.Add(new TestOrder { TestCode = "SALM", SectionId = microSec.Id, IsSuperseded = false });
        sample.TestOrders.Add(new TestOrder { TestCode = "TAMC", SectionId = otherSec.Id, IsSuperseded = false });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        await guard.EnsurePathogenSampleAccessAsync(microUser.Id, sample.Id);
    }

    [Fact]
    public async Task EnsurePathogenSampleAccessAsync_Admin_ReturnsSilently()
    {
        await using var db = NewDb();
        var (_, otherSec, _, adminUser) = SeedBase(db);

        db.TestDefinitions.Add(new TestDefinition { Code = "LIST", WorkflowType = WorkflowType.Observation });
        await db.SaveChangesAsync();

        var sample = new Sample { ReferenceNumber = "SMP-P4", ControlNumber = "C-P4", Status = SampleStatus.InTesting };
        sample.TestOrders.Add(new TestOrder { TestCode = "LIST", SectionId = otherSec.Id, IsSuperseded = false });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var guard = new UserSectionScopeService(db);
        await guard.EnsurePathogenSampleAccessAsync(adminUser.Id, sample.Id);
    }

    [Fact]
    public async Task EnsurePathogenSampleAccessAsync_NonExistentId_ReturnsSilently()
    {
        await using var db = NewDb();
        var (_, _, microUser, _) = SeedBase(db);

        var guard = new UserSectionScopeService(db);
        await guard.EnsurePathogenSampleAccessAsync(microUser.Id, 99999);
    }
}
