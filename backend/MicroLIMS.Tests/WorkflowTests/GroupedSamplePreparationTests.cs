using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Grouped Test Preparation - the confirm-only path applied to several
// samples at once. Grouping is by Item preparation configuration, because
// that configuration is what the analyst's signature attests to.
public class GroupedSamplePreparationTests
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
    // Include(u => u.Role), which EF resolves as an inner join.
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

    private static Item NewItem(MicroLimsDbContext db, string name, string code)
    {
        var item = new Item { Name = name, Code = code, Category = SampleCategory.FinishedProduct };
        db.Items.Add(item);
        db.SaveChanges();
        return item;
    }

    private static ItemPreparationConfiguration NewConfig(MicroLimsDbContext db, int itemId, decimal amount)
    {
        var config = new ItemPreparationConfiguration
        {
            ItemId = itemId,
            Amount = amount,
            Technique = "PourPlate",
            Diluent = "Buffer",
            Neutralizer = "Tween",
            ApprovalStatus = ApprovalGateStatus.Approved,
            CreatedByUserId = 1
        };
        db.ItemPreparationConfigurations.Add(config);
        db.SaveChanges();
        return config;
    }

    private static Sample NewSample(MicroLimsDbContext db, Item? item, string control, SampleCategory category = SampleCategory.FinishedProduct)
    {
        var sample = new Sample
        {
            Category = category,
            ItemId = item?.Id,
            ControlNumber = control,
            ReferenceNumber = control,
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.NeedsPreparation
        };
        db.Samples.Add(sample);
        db.SaveChanges();
        return sample;
    }

    [Fact]
    public async Task GetGroupedPreparationAsync_BucketsByItemConfiguration()
    {
        await using var db = NewDb();
        db.Users.Add(NewUser(db, 1, "Analyst One"));
        await db.SaveChangesAsync();

        var itemA = NewItem(db, "Tablet A", "FP-A");
        var itemB = NewItem(db, "Tablet B", "FP-B");
        NewConfig(db, itemA.Id, 10m);
        NewConfig(db, itemB.Id, 25m);

        var a1 = NewSample(db, itemA, "A-1");
        var a2 = NewSample(db, itemA, "A-2");
        var b1 = NewSample(db, itemB, "B-1");

        var service = TestServiceFactory.SamplePreparation(db);
        var result = await service.GetGroupedPreparationAsync(new List<int> { a1.Id, a2.Id, b1.Id }, userId: 1);

        Assert.Equal(2, result.Groups.Count);
        Assert.Empty(result.Excluded);

        var groupA = result.Groups.Single(g => g.ItemName == "Tablet A");
        Assert.Equal(2, groupA.SampleCount);
        Assert.Equal(10m, groupA.Amount);
        Assert.Equal(new[] { a1.Id, a2.Id }, groupA.Samples.Select(s => s.SampleId).OrderBy(id => id).ToArray());

        var groupB = result.Groups.Single(g => g.ItemName == "Tablet B");
        Assert.Equal(1, groupB.SampleCount);
        Assert.Equal(25m, groupB.Amount);
    }

    [Fact]
    public async Task GetGroupedPreparationAsync_ExcludesWhatCannotBeGrouped_WithAReason()
    {
        await using var db = NewDb();
        db.Users.AddRange(NewUser(db, 1, "Analyst One"), NewUser(db, 2, "Analyst Two"));
        await db.SaveChangesAsync();

        var itemA = NewItem(db, "Tablet A", "FP-A");
        var itemNoConfig = NewItem(db, "Tablet Unconfigured", "FP-U");
        NewConfig(db, itemA.Id, 10m);

        var groupable = NewSample(db, itemA, "A-1");
        var noConfig = NewSample(db, itemNoConfig, "U-1");
        var water = NewSample(db, null, "W-1", SampleCategory.Water);

        var alreadyPrepared = NewSample(db, itemA, "A-2");
        alreadyPrepared.PreparationStatus = SamplePreparationStatus.Ready;

        var otherAnalysts = NewSample(db, itemA, "A-3");
        otherAnalysts.TestOrders.Add(new TestOrder
        {
            TestCode = "TAMC",
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting,
            AssignedAnalystId = 2
        });
        await db.SaveChangesAsync();

        var service = TestServiceFactory.SamplePreparation(db);
        var result = await service.GetGroupedPreparationAsync(
            new List<int> { groupable.Id, noConfig.Id, water.Id, alreadyPrepared.Id, otherAnalysts.Id }, userId: 1);

        var single = Assert.Single(result.Groups);
        Assert.Equal(groupable.Id, Assert.Single(single.Samples).SampleId);

        Assert.Equal(4, result.ExcludedCount);
        Assert.Contains("no preparation configuration", ReasonFor(result, noConfig.Id));
        Assert.Contains("prepared individually", ReasonFor(result, water.Id));
        Assert.Contains("Already prepared", ReasonFor(result, alreadyPrepared.Id));
        Assert.Contains("Analyst Two", ReasonFor(result, otherAnalysts.Id));
    }

    private static string ReasonFor(GroupedPreparationResponse result, int sampleId) =>
        result.Excluded.Single(e => e.SampleId == sampleId).Reason;

    [Fact]
    public async Task ConfirmBatchFromConfigurationAsync_SignsEverySampleSeparately_FromOnePassword()
    {
        await using var db = NewDb();
        db.Users.Add(NewUser(db, 7, "Analyst Seven"));
        await db.SaveChangesAsync();

        var item = NewItem(db, "Tablet A", "FP-A");
        var config = NewConfig(db, item.Id, 10m);

        var s1 = NewSample(db, item, "A-1");
        var s2 = NewSample(db, item, "A-2");
        var s3 = NewSample(db, item, "A-3");

        var service = TestServiceFactory.SamplePreparation(db);
        var result = await service.ConfirmBatchFromConfigurationAsync(
            new BatchConfirmPreparationRequest(new List<int> { s1.Id, s2.Id, s3.Id }, config.Id, Password),
            userId: 7);

        Assert.Equal(3, result.SucceededCount);
        Assert.Equal(0, result.SkippedCount);

        var preparations = await db.SamplePreparations.AsNoTracking().ToListAsync();
        Assert.Equal(3, preparations.Count);
        Assert.All(preparations, p =>
        {
            Assert.Equal(10m, p.Amount);
            Assert.True(p.WasConfirmedFromConfig);
            Assert.Equal(config.Id, p.SourceConfigurationId);
            Assert.Equal(7, p.PreparedByUserId);
        });

        // One signature per sample, each pointing at its own sample - the
        // whole reason the batch does not collapse into a single record.
        var signatures = await db.ElectronicSignatures.AsNoTracking()
            .Where(s => s.MeaningOfSignature == SignatureMeaning.PreparationConfirmed)
            .ToListAsync();
        Assert.Equal(3, signatures.Count);
        Assert.Equal(
            new[] { s1.Id, s2.Id, s3.Id },
            signatures.Select(s => s.EntityId).OrderBy(id => id).ToArray());

        var samples = await db.Samples.AsNoTracking().ToListAsync();
        Assert.All(samples, s => Assert.Equal(SamplePreparationStatus.Ready, s.PreparationStatus));
    }

    [Fact]
    public async Task ConfirmBatchFromConfigurationAsync_WithWrongPassword_WritesNothing()
    {
        await using var db = NewDb();
        db.Users.Add(NewUser(db, 7, "Analyst Seven"));
        await db.SaveChangesAsync();

        var item = NewItem(db, "Tablet A", "FP-A");
        var config = NewConfig(db, item.Id, 10m);
        var s1 = NewSample(db, item, "A-1");
        var s2 = NewSample(db, item, "A-2");

        var service = TestServiceFactory.SamplePreparation(db);
        await Assert.ThrowsAsync<SignatureVerificationException>(() =>
            service.ConfirmBatchFromConfigurationAsync(
                new BatchConfirmPreparationRequest(new List<int> { s1.Id, s2.Id }, config.Id, "wrong-password"),
                userId: 7));

        Assert.Empty(await db.SamplePreparations.ToListAsync());
        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
        Assert.All(await db.Samples.AsNoTracking().ToListAsync(),
            s => Assert.Equal(SamplePreparationStatus.NeedsPreparation, s.PreparationStatus));

        // Exactly one failed-attempt audit row, not one per selected sample.
        var failures = await db.AuditLogs.AsNoTracking().Where(a => a.Action == "SignatureFailed").ToListAsync();
        Assert.Single(failures);
    }

    [Fact]
    public async Task ConfirmBatchFromConfigurationAsync_SkipsSampleOwnedByAnotherAnalyst_AndPreparesTheRest()
    {
        await using var db = NewDb();
        db.Users.AddRange(NewUser(db, 7, "Analyst Seven"), NewUser(db, 8, "Analyst Eight"));
        await db.SaveChangesAsync();

        var item = NewItem(db, "Tablet A", "FP-A");
        var config = NewConfig(db, item.Id, 10m);

        var mine = NewSample(db, item, "A-1");
        var theirs = NewSample(db, item, "A-2");
        theirs.TestOrders.Add(new TestOrder
        {
            TestCode = "TAMC",
            Status = ApprovalStatus.Pending,
            CurrentStep = WorkflowStep.Waiting,
            AssignedAnalystId = 8
        });
        await db.SaveChangesAsync();

        var service = TestServiceFactory.SamplePreparation(db);
        var result = await service.ConfirmBatchFromConfigurationAsync(
            new BatchConfirmPreparationRequest(new List<int> { mine.Id, theirs.Id }, config.Id, Password),
            userId: 7);

        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(mine.Id, Assert.Single(result.Succeeded).SampleId);

        var skipped = Assert.Single(result.Skipped);
        Assert.Equal(theirs.Id, skipped.SampleId);
        Assert.Contains("Analyst Eight", skipped.Reason);

        Assert.Equal(SamplePreparationStatus.Ready,
            (await db.Samples.AsNoTracking().FirstAsync(s => s.Id == mine.Id)).PreparationStatus);
        Assert.Equal(SamplePreparationStatus.NeedsPreparation,
            (await db.Samples.AsNoTracking().FirstAsync(s => s.Id == theirs.Id)).PreparationStatus);
    }

    [Fact]
    public async Task ConfirmBatchFromConfigurationAsync_WhenConfigurationChangedSinceTheGroupLoaded_SkipsTheSample()
    {
        await using var db = NewDb();
        db.Users.Add(NewUser(db, 7, "Analyst Seven"));
        await db.SaveChangesAsync();

        var item = NewItem(db, "Tablet A", "FP-A");
        var config = NewConfig(db, item.Id, 10m);
        var sample = NewSample(db, item, "A-1");

        // A different configuration id than the one the item actually
        // carries - what the analyst would be holding if the Section Head
        // replaced the protocol between the panel loading and the signature.
        var staleConfig = NewConfig(db, NewItem(db, "Tablet B", "FP-B").Id, 25m);

        var service = TestServiceFactory.SamplePreparation(db);
        var result = await service.ConfirmBatchFromConfigurationAsync(
            new BatchConfirmPreparationRequest(new List<int> { sample.Id }, staleConfig.Id, Password),
            userId: 7);

        Assert.Equal(0, result.SucceededCount);
        Assert.Contains("changed after the group was loaded", Assert.Single(result.Skipped).Reason);
        Assert.Empty(await db.SamplePreparations.ToListAsync());
        Assert.NotNull(config);
    }
}
