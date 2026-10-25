using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// A raw material is received against the grade stated on the supplier's CoA.
// The grade decides which of the item's assigned tests the sample gets, and
// can be corrected (signed) while every test is still pending.
public class GradedReceiptTests
{
    private const string Password = "Correct-Horse-9";

    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Specification Spec(int itemId, string testCode, string? grade) => new()
    {
        ItemId = itemId, TestCode = testCode, ParameterName = testCode,
        LimitType = LimitType.Qualitative, ExpectedResultText = "Conforms", Grade = grade,
    };

    // ASSAY: BP + USP rows; CHLORIDE: USP only; TAMC: ungraded.
    private static async Task<Item> SeedItemAsync(MicroLimsDbContext db, bool graded = true)
    {
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var item = new Item { Name = "Glycerin", Code = "RM-GLY", Category = SampleCategory.RawMaterial, IsActive = true };
        foreach (var code in new[] { "ASSAY", "CHLORIDE", "TAMC" })
        {
            item.AssignedTests.Add(new SampleTest { TestCode = code, DisplayName = code });
            db.TestDefinitions.Add(new TestDefinition { Code = code, DisplayName = code, SectionId = micro.Id });
        }
        db.Items.Add(item);
        await db.SaveChangesAsync();

        if (graded)
        {
            db.Specifications.AddRange(
                Spec(item.Id, "ASSAY", "BP"), Spec(item.Id, "ASSAY", "USP"),
                Spec(item.Id, "CHLORIDE", "USP"), Spec(item.Id, "TAMC", null));
        }
        else
        {
            db.Specifications.AddRange(Spec(item.Id, "ASSAY", null), Spec(item.Id, "TAMC", null));
        }
        await db.SaveChangesAsync();
        return item;
    }

    private static ItemBasedReceiveRequest Request(int itemId, string? grade) =>
        new(itemId, 1, "10 g", "Analyst", "LOT-1", "CTRL-1", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null, 1, null, grade);

    private static ProductWorkflowEngine Engine(MicroLimsDbContext db) => new(db, new ReferenceNumberGenerator(db));

    [Fact]
    public async Task Receive_Bp_CreatesOnlyTestsThatApplyToBp()
    {
        await using var db = NewDb();
        var item = await SeedItemAsync(db);

        var sample = await Engine(db).ReceiveAsync(Request(item.Id, "BP"));

        Assert.Equal("BP", sample.Grade);
        Assert.Equal(new[] { "ASSAY", "TAMC" }, sample.TestOrders.Select(t => t.TestCode).OrderBy(c => c));
    }

    [Fact]
    public async Task Receive_LowerCaseUsp_StoresUpperCaseAndCreatesEveryApplicableTest()
    {
        await using var db = NewDb();
        var item = await SeedItemAsync(db);

        var sample = await Engine(db).ReceiveAsync(Request(item.Id, "usp"));

        Assert.Equal("USP", sample.Grade);
        Assert.Equal(new[] { "ASSAY", "CHLORIDE", "TAMC" }, sample.TestOrders.Select(t => t.TestCode).OrderBy(c => c));
    }

    [Fact]
    public async Task Receive_GradedItemWithoutGrade_IsRefused()
    {
        await using var db = NewDb();
        var item = await SeedItemAsync(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Engine(db).ReceiveAsync(Request(item.Id, null)));
        Assert.Equal("Choose the grade stated on the supplier's CoA.", ex.Message);
    }

    [Fact]
    public async Task Receive_GradeTheItemHasNoSpecificationFor_IsRefused()
    {
        await using var db = NewDb();
        var item = await SeedItemAsync(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Engine(db).ReceiveAsync(Request(item.Id, "EP")));
        Assert.Equal("Grade EP has no specification for this item.", ex.Message);
    }

    [Fact]
    public async Task Receive_UngradedItem_BehavesAsBefore()
    {
        await using var db = NewDb();
        var item = await SeedItemAsync(db, graded: false);

        var sample = await Engine(db).ReceiveAsync(Request(item.Id, null));

        Assert.Null(sample.Grade);
        Assert.Equal(3, sample.TestOrders.Count);
    }

    [Fact]
    public void TestAppliesToGrade_NoRows_Ungraded_OrMatchingGrade()
    {
        Assert.True(ProductWorkflowEngine.TestAppliesToGrade(Array.Empty<Specification>(), "BP"));
        Assert.True(ProductWorkflowEngine.TestAppliesToGrade(new[] { Spec(1, "T", null), Spec(1, "T", "USP") }, "BP"));
        Assert.True(ProductWorkflowEngine.TestAppliesToGrade(new[] { Spec(1, "T", "usp") }, "USP"));
        Assert.False(ProductWorkflowEngine.TestAppliesToGrade(new[] { Spec(1, "T", "USP") }, "BP"));
    }

    // ---- Signed correction ----

    private sealed record Received(MicroLimsDbContext Db, Sample Sample, User Signer, SampleCorrectionService Service);

    private static async Task<Received> ReceiveBpAsync()
    {
        var db = NewDb();
        var item = await SeedItemAsync(db);
        var role = new Role { Type = RoleType.SectionHead, Name = "Section Head" };
        db.Roles.Add(role);
        db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine" });
        await db.SaveChangesAsync();
        var signer = new User { FullName = "Sam Head", Username = "samhead", RoleId = role.Id, PasswordHash = TestPasswords.Hash(Password) };
        db.Users.Add(signer);
        await db.SaveChangesAsync();

        var request = Request(item.Id, "BP") with { CauseOfTestingId = db.CausesOfTesting.First().Id, ReceivedByUserId = signer.Id };
        var sample = await Engine(db).ReceiveAsync(request);
        return new Received(db, sample, signer, TestServiceFactory.SampleCorrection(db));
    }

    private static SampleCorrectionRequest AsStored(Sample s, string? grade) => new(
        s.ControlNumber, s.SampledBy, s.CauseOfTestingId, s.BatchNumber, s.MfgDate, s.ExpDate, s.SampleQuantity,
        null, null, null, null, null, s.ItemId, null, null, null, grade);

    [Fact]
    public async Task Correct_GradeWhilePending_RederivesTestsAndIsAudited()
    {
        var (db, sample, signer, service) = await ReceiveBpAsync();
        await using var _ = db;

        await service.CorrectAsync(sample.Id, AsStored(sample, "usp"), "CoA states USP", Password, signer.Id, null);

        var saved = await db.Samples.Include(s => s.TestOrders).AsNoTracking().FirstAsync(s => s.Id == sample.Id);
        Assert.Equal("USP", saved.Grade);
        Assert.Equal(new[] { "ASSAY", "CHLORIDE", "TAMC" }, saved.TestOrders.Select(t => t.TestCode).OrderBy(c => c));

        var audit = await db.AuditLogs.Include(a => a.Changes)
            .FirstAsync(a => a.ActionCode == SampleCorrectionService.CorrectedActionCode && a.EntityId == sample.Id.ToString());
        Assert.Contains(audit.Changes, c => c.FieldName == "Grade" && c.PreviousValue == "BP" && c.NewValue == "USP");
        Assert.Contains(audit.Changes, c => c.FieldName == "Tests");
    }

    [Fact]
    public async Task Correct_GradeBackToBp_RemovesTestsThatNoLongerApply()
    {
        var (db, sample, signer, service) = await ReceiveBpAsync();
        await using var _ = db;
        await service.CorrectAsync(sample.Id, AsStored(sample, "USP"), "CoA states USP", Password, signer.Id, null);
        var current = await db.Samples.AsNoTracking().FirstAsync(s => s.Id == sample.Id);

        await service.CorrectAsync(sample.Id, AsStored(current, "BP"), "Entered wrongly", Password, signer.Id, null);

        var codes = await db.TestOrders.Where(o => o.SampleId == sample.Id).Select(o => o.TestCode).OrderBy(c => c).ToListAsync();
        Assert.Equal(new[] { "ASSAY", "TAMC" }, codes);
    }

    [Fact]
    public async Task Correct_GradeOnceATestHasStarted_IsRefused()
    {
        var (db, sample, signer, service) = await ReceiveBpAsync();
        await using var _ = db;
        var order = sample.TestOrders.First();
        order.Status = ApprovalStatus.InProgress;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CorrectAsync(sample.Id, AsStored(sample, "USP"), "Too late", Password, signer.Id, null));
        Assert.Contains("still pending", ex.Message);
        Assert.Equal("BP", (await db.Samples.AsNoTracking().FirstAsync(s => s.Id == sample.Id)).Grade);
    }

    [Fact]
    public async Task Correct_OtherFieldsOnGradedSample_KeepsGradeWhenResent()
    {
        var (db, sample, signer, service) = await ReceiveBpAsync();
        await using var _ = db;

        await service.CorrectAsync(sample.Id, AsStored(sample, "BP") with { SampledBy = "Someone Else" }, "Typo", Password, signer.Id, null);

        var saved = await db.Samples.Include(s => s.TestOrders).AsNoTracking().FirstAsync(s => s.Id == sample.Id);
        Assert.Equal("BP", saved.Grade);
        Assert.Equal(2, saved.TestOrders.Count);
    }
}
