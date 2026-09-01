using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

public class SamplePreparationTests
{
    private const string Password = "Correct-Horse-1!";

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    // Role is required: ElectronicSignatureService loads the signer with
    // Include(u => u.Role), which EF resolves as an inner join - a role-less
    // user simply isn't found, and surfaces as a password failure.
    private static User NewUser(MicroLimsDbContext db, int id, string name)
    {
        var role = db.Roles.FirstOrDefault(r => r.Type == RoleType.Analyst);
        if (role is null)
        {
            role = new Role { Type = RoleType.Analyst, Name = "Analyst" };
            db.Roles.Add(role);
            db.SaveChanges();
        }

        return new User
        {
            Id = id,
            Username = $"user{id}",
            FullName = name,
            RoleId = role.Id,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
            IsActive = true
        };
    }

    // Product/RM/PM preparation always hangs off an Item - that's what the
    // configuration is keyed on.
    private static async Task<(Item item, DiluentType diluent, Neutralizer neutralizer)> SeedMasterDataAsync(MicroLimsDbContext db)
    {
        var item = new Item { Name = "Example Tablet", Code = "FP-0001", Category = SampleCategory.FinishedProduct };
        var diluent = new DiluentType { Name = "Buffer", RequiresBatchTracking = false };
        var neutralizer = new Neutralizer { Name = "Tween" };
        db.Items.Add(item);
        db.DiluentTypes.Add(diluent);
        db.Neutralizers.Add(neutralizer);
        await db.SaveChangesAsync();
        return (item, diluent, neutralizer);
    }

    [Fact]
    public async Task PrepareAsync_AssignsPreparerAsAnalyst_ToEveryWaitingTestOrderOnTheSample()
    {
        await using var db = NewDb();
        db.Users.Add(NewUser(db, 5, "Analyst Five"));
        var (item, diluent, neutralizer) = await SeedMasterDataAsync(db);

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ItemId = item.Id, ControlNumber = "CTRL-1", Status = SampleStatus.Received };
        var waitingOrder1 = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        var waitingOrder2 = new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting };
        sample.TestOrders.Add(waitingOrder1);
        sample.TestOrders.Add(waitingOrder2);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.SamplePreparation(db);
        await service.PrepareAsync(new PrepareSampleRequest(
            sample.Id, 10m, "ml", "PourPlate", null, null, diluent.Id, null, neutralizer.Id, UserId: 5, Password));

        var reloadedWaiting1 = await db.TestOrders.FirstAsync(t => t.Id == waitingOrder1.Id);
        var reloadedWaiting2 = await db.TestOrders.FirstAsync(t => t.Id == waitingOrder2.Id);

        Assert.Equal(5, reloadedWaiting1.AssignedAnalystId);
        Assert.Equal(5, reloadedWaiting2.AssignedAnalystId);
    }

    [Fact]
    public async Task PrepareAsync_WhenSampleAssignedToDifferentAnalyst_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        db.Users.AddRange(NewUser(db, 10, "Analyst X"), NewUser(db, 20, "Analyst Y"));
        var (item, diluent, neutralizer) = await SeedMasterDataAsync(db);

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ItemId = item.Id, ControlNumber = "CTRL-2", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 10 };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.SamplePreparation(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareAsync(new PrepareSampleRequest(
            sample.Id, 10m, "ml", "PourPlate", null, null, diluent.Id, null, neutralizer.Id, UserId: 20, Password)));

        Assert.Contains("Analyst X", ex.Message);
        Assert.Contains("Only the assigned analyst may perform sample preparation", ex.Message);
    }

    [Fact]
    public async Task PrepareAsync_WhenSampleAssignedToSameAnalyst_SucceedsAndSetsPreparedBy()
    {
        await using var db = NewDb();
        db.Users.Add(NewUser(db, 10, "Analyst X"));
        var (item, diluent, neutralizer) = await SeedMasterDataAsync(db);

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ItemId = item.Id, ControlNumber = "CTRL-3", Status = SampleStatus.Received };
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 10 };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.SamplePreparation(db);
        var prep = await service.PrepareAsync(new PrepareSampleRequest(
            sample.Id, 10m, "ml", "PourPlate", null, null, diluent.Id, null, neutralizer.Id, UserId: 10, Password));

        Assert.NotNull(prep);
        Assert.Equal(10, prep.PreparedByUserId);
        Assert.Equal(10, order.AssignedAnalystId);
    }

    // The fallback path is what seeds an item's standing configuration -
    // usable immediately, reviewed by the Section Head after the fact.
    [Fact]
    public async Task PrepareAsync_WhenItemHasNoConfiguration_SeedsOneAsPendingReview()
    {
        await using var db = NewDb();
        db.Users.Add(NewUser(db, 7, "Analyst Seven"));
        var (item, diluent, neutralizer) = await SeedMasterDataAsync(db);

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ItemId = item.Id, ControlNumber = "CTRL-4", Status = SampleStatus.Received };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.SamplePreparation(db);
        var prep = await service.PrepareAsync(new PrepareSampleRequest(
            sample.Id, 25m, "gm", "PourPlate", null, null, diluent.Id, null, neutralizer.Id, UserId: 7, Password));

        var config = await db.ItemPreparationConfigurations.SingleAsync(c => c.ItemId == item.Id);
        Assert.Equal(ApprovalGateStatus.PendingReview, config.ApprovalStatus);
        Assert.Equal(7, config.CreatedByUserId);
        Assert.Equal(25m, config.Amount);
        Assert.Equal("gm", config.Unit);

        // The sample's own record points back at the config it seeded, but is
        // flagged as manual entry rather than a confirmation.
        Assert.Equal(config.Id, prep.SourceConfigurationId);
        Assert.False(prep.WasConfirmedFromConfig);
    }

    [Fact]
    public async Task ConfirmFromConfigurationAsync_CopiesConfiguredValuesOntoTheSampleRecord()
    {
        await using var db = NewDb();
        db.Users.Add(NewUser(db, 8, "Analyst Eight"));
        var (item, diluent, neutralizer) = await SeedMasterDataAsync(db);

        db.ItemPreparationConfigurations.Add(new ItemPreparationConfiguration
        {
            ItemId = item.Id,
            Amount = 40m,
            Unit = "ml",
            Technique = "Filtration",
            FiltrationVolume = 100m,
            WashingVolume = 300m,
            DiluentTypeId = diluent.Id,
            NeutralizerId = neutralizer.Id,
            ApprovalStatus = ApprovalGateStatus.Approved,
            CreatedByUserId = 8
        });

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ItemId = item.Id, ControlNumber = "CTRL-5", Status = SampleStatus.Received };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.SamplePreparation(db);
        var prep = await service.ConfirmFromConfigurationAsync(new ConfirmPreparationRequest(sample.Id, UserId: 8, Password));

        Assert.True(prep.WasConfirmedFromConfig);
        Assert.Equal(40m, prep.Amount);
        Assert.Equal("Filtration", prep.Technique);
        Assert.Equal(100m, prep.FiltrationVolume);
        Assert.Equal(300m, prep.WashingVolume);
        Assert.Equal(SamplePreparationStatus.Ready, (await db.Samples.FirstAsync(s => s.Id == sample.Id)).PreparationStatus);
    }

    // Editing the config afterwards must not rewrite what was already signed.
    [Fact]
    public async Task EditingConfigurationAfterConfirmation_LeavesTheSampleSnapshotUnchanged()
    {
        await using var db = NewDb();
        db.Users.Add(NewUser(db, 9, "Analyst Nine"));
        var (item, diluent, neutralizer) = await SeedMasterDataAsync(db);

        db.ItemPreparationConfigurations.Add(new ItemPreparationConfiguration
        {
            ItemId = item.Id,
            Amount = 10m,
            Unit = "ml",
            Technique = "PourPlate",
            DiluentTypeId = diluent.Id,
            NeutralizerId = neutralizer.Id,
            ApprovalStatus = ApprovalGateStatus.Approved,
            CreatedByUserId = 9
        });

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ItemId = item.Id, ControlNumber = "CTRL-6", Status = SampleStatus.Received };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var prepService = TestServiceFactory.SamplePreparation(db);
        var prep = await prepService.ConfirmFromConfigurationAsync(new ConfirmPreparationRequest(sample.Id, UserId: 9, Password));
        Assert.Equal(10m, prep.Amount);

        var configService = TestServiceFactory.ItemPreparationConfiguration(db);
        await configService.UpsertAsync(item.Id, new PreparationParameters(
            999m, "gm", "PourPlate", null, null, diluent.Id, null, neutralizer.Id), userId: 9);

        var reloadedPrep = await db.SamplePreparations.AsNoTracking().FirstAsync(p => p.Id == prep.Id);
        Assert.Equal(10m, reloadedPrep.Amount);
        Assert.Equal("ml", reloadedPrep.Unit);
    }

    [Fact]
    public async Task UpsertAsync_EditingAnApprovedConfiguration_ReopensItForApproval()
    {
        await using var db = NewDb();
        var (item, diluent, neutralizer) = await SeedMasterDataAsync(db);

        db.ItemPreparationConfigurations.Add(new ItemPreparationConfiguration
        {
            ItemId = item.Id,
            Amount = 10m,
            Unit = "ml",
            Technique = "PourPlate",
            DiluentTypeId = diluent.Id,
            NeutralizerId = neutralizer.Id,
            ApprovalStatus = ApprovalGateStatus.Approved,
            CreatedByUserId = 1,
            ApprovedByUserId = 3,
            ApprovedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var configService = TestServiceFactory.ItemPreparationConfiguration(db);
        var dto = await configService.UpsertAsync(item.Id, new PreparationParameters(
            20m, "ml", "PourPlate", null, null, diluent.Id, null, neutralizer.Id), userId: 3);

        Assert.Equal(ApprovalGateStatus.PendingReview, dto.ApprovalStatus);
        Assert.Null(dto.ApprovedByUserId);
        Assert.Null(dto.ApprovedAt);
    }

    [Fact]
    public async Task PrepareAsync_WithWrongPassword_WritesNothing()
    {
        await using var db = NewDb();
        db.Users.Add(NewUser(db, 11, "Analyst Eleven"));
        var (item, diluent, neutralizer) = await SeedMasterDataAsync(db);

        var sample = new Sample { Category = SampleCategory.FinishedProduct, ItemId = item.Id, ControlNumber = "CTRL-7", Status = SampleStatus.Received, PreparationStatus = SamplePreparationStatus.NeedsPreparation };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.SamplePreparation(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareAsync(new PrepareSampleRequest(
            sample.Id, 10m, "ml", "PourPlate", null, null, diluent.Id, null, neutralizer.Id, UserId: 11, "wrong-password")));

        Assert.Empty(await db.SamplePreparations.ToListAsync());
        Assert.Empty(await db.ItemPreparationConfigurations.ToListAsync());
        Assert.Equal(SamplePreparationStatus.NeedsPreparation, (await db.Samples.AsNoTracking().FirstAsync(s => s.Id == sample.Id)).PreparationStatus);
    }
}
