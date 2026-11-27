using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class WaterAndEMEngineTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    // EM/After Cleaning batch preparation (multiple rooms/parts -> one
    // TestOrder per TestCode + SampleLocation rows) is covered by
    // EMBatchLocationTests.cs, replacing the old per-location model this
    // file used to exercise here.

    [Fact]
    public async Task Water_ReceiveAsync_StartsAsNeedsPreparation()
    {
        await using var db = NewDb();
        var department = new WaterDepartment { Name = "WTU" };
        db.WaterDepartments.Add(department);
        await db.SaveChangesAsync();

        var engine = new WaterWorkflowEngine(db, new ReferenceNumberGenerator(db));
        var sample = await engine.ReceiveAsync(new WaterReceiveRequest(department.Id, 0, "500ml", "Analyst", "CTRL-2", 1));

        Assert.Equal(SamplePreparationStatus.NeedsPreparation, sample.PreparationStatus);
        Assert.Empty(sample.TestOrders);
    }

    [Fact]
    public async Task SamplePreparationService_PrepareAsync_FlipsSampleToReady()
    {
        await using var db = NewDb();
        const string password = "Correct-Horse-1!";
        // Role is required - the signature service loads the signer with
        // Include(u => u.Role), which resolves as an inner join.
        var role = new Role { Type = RoleType.Analyst, Name = "Analyst" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        db.Users.Add(new User { Id = 5, Username = "user5", FullName = "Analyst Five", RoleId = role.Id, PasswordHash = TestPasswords.Hash(password), IsActive = true });
        var item = new Item { Name = "Example Tablet", Code = "FP-0001", Category = SampleCategory.FinishedProduct };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ItemId = item.Id, ControlNumber = "CTRL-3", Status = SampleStatus.Received, PreparationStatus = SamplePreparationStatus.NeedsPreparation };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.SamplePreparation(db);
        await service.PrepareAsync(await PreparationTestData.ManualRequestAsync(db, sample.Id, 10m, 5, password));

        var reloaded = await db.Samples.FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(SamplePreparationStatus.Ready, reloaded.PreparationStatus);
    }
}
