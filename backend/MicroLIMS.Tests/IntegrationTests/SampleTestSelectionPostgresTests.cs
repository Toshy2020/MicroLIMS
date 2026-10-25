using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Migrations;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Postgres-backed: the register and tracking queries, the migration's cause
// row and the sample's new columns only exist in the real schema.
[Collection("PostgresDatabaseCollection")]
public class SampleTestSelectionPostgresTests
{
    private const string Password = "Correct-Horse-9";
    private readonly PostgresTestFixture _fixture;

    public SampleTestSelectionPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed record World(
        MicroLimsDbContext Db, Sample Sample, int MicroId, int FpId, string Lod, string Assay, string UspOnly, string FpTest,
        User Head, User Supervisor, User Analyst, User OutsideHead);

    private static Specification Spec(int itemId, string testCode, string? grade) => new()
    {
        ItemId = itemId, TestCode = testCode, ParameterName = "Result", Grade = grade,
        LimitType = LimitType.Qualitative, ExpectedResultText = "Conforms",
    };

    private async Task<User> UserAsync(MicroLimsDbContext db, RoleType type, string uid, int deptId, int sectionId)
    {
        var role = new Role { Type = type, Name = $"{type}-{uid}", IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User { FullName = $"{type} {uid}", Username = $"{type}{uid}".ToLowerInvariant(), RoleId = role.Id, PasswordHash = TestPasswords.Hash(Password), IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = deptId, SectionId = sectionId });
        await db.SaveChangesAsync();
        return user;
    }

    // A raw material with LOD (ungraded), ASSAY (BP + USP), a USP-only test and an FP test,
    // received as a BP periodic retest for Microbiology.
    private async Task<World> ReceiveAsync(string grade = "BP")
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var db = _fixture.CreateDbContext();
        var micro = await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");
        var fp = await db.DocumentSections.FirstAsync(s => s.Code == "FP");
        string lod = $"LD{uid}", assay = $"AS{uid}", uspOnly = $"US{uid}", fpTest = $"FP{uid}";
        foreach (var (code, sectionId) in new[] { (lod, micro.Id), (assay, micro.Id), (uspOnly, micro.Id), (fpTest, fp.Id) })
            db.TestDefinitions.Add(new TestDefinition { Code = code, DisplayName = $"{code} name", SectionId = sectionId, IsActive = true });
        var item = new Item { Code = $"RM-{uid}", Name = $"Glycerin {uid}", Category = SampleCategory.RawMaterial, IsActive = true };
        foreach (var code in new[] { lod, assay, uspOnly, fpTest })
            item.AssignedTests.Add(new SampleTest { TestCode = code, DisplayName = $"{code} name" });
        db.Items.Add(item);
        await db.SaveChangesAsync();
        db.Specifications.AddRange(Spec(item.Id, assay, "BP"), Spec(item.Id, assay, "USP"), Spec(item.Id, uspOnly, "USP"),
            Spec(item.Id, lod, null), Spec(item.Id, fpTest, null));
        await db.SaveChangesAsync();

        var cause = await db.CausesOfTesting.SingleAsync(c => c.Name == "Periodic retest");
        var head = await UserAsync(db, RoleType.SectionHead, "H" + uid, micro.DepartmentId, micro.Id);
        var supervisor = await UserAsync(db, RoleType.Reviewer, "S" + uid, micro.DepartmentId, micro.Id);
        var analyst = await UserAsync(db, RoleType.Analyst, "A" + uid, micro.DepartmentId, micro.Id);
        var outside = await UserAsync(db, RoleType.SectionHead, "O" + uid, fp.DepartmentId, fp.Id);

        var sample = await new ProductWorkflowEngine(db, new ReferenceNumberGenerator(db)).ReceiveAsync(new ItemBasedReceiveRequest(
            item.Id, cause.Id, "10 g", "Analyst", $"LOT-{uid}", $"CTRL-{uid}", DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null,
            _fixture.SeededUserId, new[] { micro.Id }, grade));
        return new World(db, sample, micro.Id, fp.Id, lod, assay, uspOnly, fpTest, head, supervisor, analyst, outside);
    }

    private static SelectTestsRequest Choose(string password, params string[] codes) => new(codes, password, null);

    [PostgresFact]
    public async Task Migration_AddsPeriodicRetestCause_Once_AndRerunningItsInsertChangesNothing()
    {
        await using var db = _fixture.CreateDbContext();
        var cause = await db.CausesOfTesting.SingleAsync(c => c.Name == "Periodic retest");
        Assert.True(cause.RequiresTestSelection);

        await db.Database.ExecuteSqlRawAsync(AddPeriodicRetestSelection.InsertPeriodicRetestCauseSql);

        Assert.Equal(1, await db.CausesOfTesting.CountAsync(c => c.Name == "Periodic retest"));
    }

    [PostgresFact]
    public async Task Receive_PeriodicRetest_CreatesNoOrders_AndStaysInTheRegisterAsReceived()
    {
        var w = await ReceiveAsync();
        await using var db = w.Db;

        Assert.Empty(w.Sample.TestOrders);
        Assert.True(w.Sample.AwaitingTestSelection);
        Assert.Equal(w.MicroId.ToString(), w.Sample.PendingTargetSectionIds);
        Assert.Equal(SampleStatus.Received, w.Sample.Status);

        await using var read = _fixture.CreateDbContext();
        var workspace = new TestingWorkspaceService(read, new UserSectionScopeService(read));
        var filter = new TestingWorkspaceFilterDto { Category = "RawMaterial", Search = w.Sample.ReferenceNumber, PageSize = 200 };

        // The Microbiology head (scoped) and an unscoped administrator both see it.
        foreach (var userId in new[] { w.Head.Id, _fixture.SeededUserId })
        {
            var row = Assert.Single((await workspace.GetActiveSamplesAsync(filter, userId)).Items);
            Assert.Equal("Received", row.Status);
            Assert.True(row.AwaitingTestSelection);
            Assert.Empty(row.AssignedTests);
        }
        // A head of another laboratory does not, and neither does a lab workspace.
        Assert.Empty((await workspace.GetActiveSamplesAsync(filter, w.OutsideHead.Id)).Items);
        filter.LabSectionId = w.MicroId;
        Assert.Empty((await workspace.GetActiveSamplesAsync(filter, w.Head.Id)).Items);

        var single = await workspace.GetSampleAsync(w.Sample.Id, w.Head.Id);
        Assert.True(single!.AwaitingTestSelection);

        var tracking = await TestServiceFactory.SampleTracking(read).GetTrackingAsync(new SampleTrackingFilterDto { PageSize = 200 });
        var trackingRow = Assert.Single(tracking.Items, r => r.ReferenceNumber == w.Sample.ReferenceNumber);
        Assert.Equal("InProgress", trackingRow.OverallStatus);
        Assert.Equal("Awaiting test selection", Assert.Single(trackingRow.Labs).Stage);

        var summary = await TestServiceFactory.SampleSummary(read).GetSummaryAsync(w.Sample.Id);
        Assert.Equal("InProgress", summary!.OverallStatus);
        var scope = new UserSectionScopeService(read);
        await scope.EnsureSampleAccessAsync(w.Head.Id, w.Sample.Id);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scope.EnsureSampleAccessAsync(w.OutsideHead.Id, w.Sample.Id));
    }

    [PostgresFact]
    public async Task GetEligible_ListsAssignedTestsThatApplyToTheGradeInTheSamplesLabs()
    {
        var w = await ReceiveAsync();
        await using var db = w.Db;

        var eligible = await TestServiceFactory.SampleTestSelection(db).GetEligibleAsync(w.Sample.Id);

        Assert.Equal(new[] { w.Assay, w.Lod }.OrderBy(c => c, StringComparer.Ordinal), eligible.Select(e => e.TestCode));
        Assert.All(eligible, e => Assert.Equal(w.MicroId, e.SectionId));
        Assert.Equal($"{w.Lod} name", eligible.Single(e => e.TestCode == w.Lod).DisplayName);
    }

    [PostgresFact]
    public async Task SectionHead_SelectsTests_CreatesOrdersSignatureAndAudit()
    {
        var w = await ReceiveAsync();
        await using var db = w.Db;

        await TestServiceFactory.SampleTestSelection(db).SelectAsync(
            w.Sample.Id, new SelectTestsRequest(new[] { w.Lod, w.Assay }, Password, "Annual retest"), w.Head.Id, "10.0.0.1");

        await using var read = _fixture.CreateDbContext();
        var saved = await read.Samples.Include(s => s.TestOrders).AsNoTracking().SingleAsync(s => s.Id == w.Sample.Id);
        Assert.Equal(new[] { w.Assay, w.Lod }.OrderBy(c => c, StringComparer.Ordinal), saved.TestOrders.Select(t => t.TestCode).OrderBy(c => c, StringComparer.Ordinal));
        Assert.All(saved.TestOrders, t =>
        {
            Assert.Equal(ApprovalStatus.Pending, t.Status);
            Assert.Equal(WorkflowStep.Waiting, t.CurrentStep);
            Assert.Equal(w.MicroId, t.SectionId);
        });
        Assert.False(saved.AwaitingTestSelection);
        Assert.Null(saved.PendingTargetSectionIds);
        Assert.Equal(SamplePreparationStatus.NeedsPreparation, saved.PreparationStatus);

        var signature = await read.ElectronicSignatures.AsNoTracking().SingleAsync(s => s.Id == saved.TestSelectionSignatureId);
        Assert.Equal(SignatureMeaning.TestsSelected, signature.MeaningOfSignature);
        Assert.Equal("Sample", signature.EntityType);
        Assert.Equal(w.Sample.Id, signature.EntityId);
        Assert.Equal(w.Head.Id, signature.UserId);

        var audit = await read.AuditLogs.Include(a => a.Changes).AsNoTracking()
            .SingleAsync(a => a.ActionCode == "SAMPLE_TESTS_SELECTED" && a.SampleId == w.Sample.Id);
        Assert.Equal(w.Head.Id, audit.UserId);
        Assert.Equal("Annual retest", audit.Reason);
        Assert.Contains(w.Lod, audit.Changes.Single(c => c.FieldName == "Tests").NewValue);
        Assert.Contains(w.Assay, audit.Changes.Single(c => c.FieldName == "Tests").NewValue);
    }

    [PostgresFact]
    public async Task Supervisor_MayChooseTheTests()
    {
        var w = await ReceiveAsync();
        await using var db = w.Db;

        await TestServiceFactory.SampleTestSelection(db).SelectAsync(w.Sample.Id, Choose(Password, w.Lod), w.Supervisor.Id, null);

        Assert.Single(await db.TestOrders.Where(t => t.SampleId == w.Sample.Id).ToListAsync());
    }

    [PostgresFact]
    public async Task Analyst_IsRefused_AndNothingIsCreated()
    {
        var w = await ReceiveAsync();
        await using var db = w.Db;

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            TestServiceFactory.SampleTestSelection(db).SelectAsync(w.Sample.Id, Choose(Password, w.Lod), w.Analyst.Id, null));

        Assert.Equal("Only a Section Head or Supervisor can choose the tests for a periodic retest.", ex.Message);
        Assert.Empty(await db.TestOrders.Where(t => t.SampleId == w.Sample.Id).ToListAsync());
    }

    [PostgresFact]
    public async Task HeadOfAnotherLaboratory_IsRefused()
    {
        var w = await ReceiveAsync();
        await using var db = w.Db;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            TestServiceFactory.SampleTestSelection(db).SelectAsync(w.Sample.Id, Choose(Password, w.Lod), w.OutsideHead.Id, null));

        Assert.Empty(await db.TestOrders.Where(t => t.SampleId == w.Sample.Id).ToListAsync());
    }

    [PostgresFact]
    public async Task WrongPassword_FailsVerification_AndCreatesNothing()
    {
        var w = await ReceiveAsync();
        await using var db = w.Db;

        await Assert.ThrowsAsync<SignatureVerificationException>(() =>
            TestServiceFactory.SampleTestSelection(db).SelectAsync(w.Sample.Id, Choose("wrong-password", w.Lod), w.Head.Id, null));

        await using var read = _fixture.CreateDbContext();
        var saved = await read.Samples.Include(s => s.TestOrders).AsNoTracking().SingleAsync(s => s.Id == w.Sample.Id);
        Assert.Empty(saved.TestOrders);
        Assert.True(saved.AwaitingTestSelection);
        Assert.Null(saved.TestSelectionSignatureId);
    }

    [PostgresFact]
    public async Task EmptyChoice_CodeNotEligible_AndSecondSelection_AreRefusedWithTheirMessages()
    {
        var w = await ReceiveAsync();
        await using var db = w.Db;
        var service = TestServiceFactory.SampleTestSelection(db);

        var empty = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SelectAsync(w.Sample.Id, Choose(Password), w.Head.Id, null));
        Assert.Equal("Choose at least one test.", empty.Message);

        // USP-only test on a BP sample; an FP test for a Microbiology-only receipt.
        var gradeMismatch = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SelectAsync(w.Sample.Id, Choose(Password, w.Lod, w.UspOnly), w.Head.Id, null));
        Assert.Equal($"Test {w.UspOnly} is not available for this sample.", gradeMismatch.Message);
        var otherLab = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SelectAsync(w.Sample.Id, Choose(Password, w.FpTest), w.Head.Id, null));
        Assert.Equal($"Test {w.FpTest} is not available for this sample.", otherLab.Message);
        Assert.Empty(await db.TestOrders.Where(t => t.SampleId == w.Sample.Id).ToListAsync());

        await service.SelectAsync(w.Sample.Id, Choose(Password, w.Lod), w.Head.Id, null);
        var again = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SelectAsync(w.Sample.Id, Choose(Password, w.Assay), w.Head.Id, null));
        Assert.Equal("Tests for this sample have already been selected.", again.Message);
        Assert.Single(await db.TestOrders.Where(t => t.SampleId == w.Sample.Id).ToListAsync());
    }

    [PostgresFact]
    public async Task GradeCorrection_OnAnAwaitingSample_OnlyChangesTheGrade_AndSelectionUsesTheNewOne()
    {
        var w = await ReceiveAsync("BP");
        await using var db = w.Db;
        var s = w.Sample;

        await TestServiceFactory.SampleCorrection(db).CorrectAsync(s.Id,
            new SampleCorrectionRequest(s.ControlNumber, s.SampledBy, s.CauseOfTestingId, s.BatchNumber, s.MfgDate, s.ExpDate, s.SampleQuantity, Grade: "USP"),
            "CoA says USP", Password, w.Head.Id, null);

        await using var read = _fixture.CreateDbContext();
        var saved = await read.Samples.Include(x => x.TestOrders).AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.Equal("USP", saved.Grade);
        Assert.Empty(saved.TestOrders);
        Assert.True(saved.AwaitingTestSelection);
        Assert.Equal(w.MicroId.ToString(), saved.PendingTargetSectionIds);

        await TestServiceFactory.SampleTestSelection(db).SelectAsync(s.Id, Choose(Password, w.UspOnly), w.Head.Id, null);
        Assert.Equal(new[] { w.UspOnly }, await db.TestOrders.Where(t => t.SampleId == s.Id).Select(t => t.TestCode).ToListAsync());
    }
}
