using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.Kpi;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class WorkflowProfileServiceTests
{
    private const string Password = "Correct-Password-1!";
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 11);

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        db.CurrentUserId = 1;
        return db;
    }

    private static WorkflowProfileService NewService(MicroLimsDbContext db) =>
        TestServiceFactory.WorkflowProfile(db, new FixedTimeProvider(Now));

    private static async Task<User> SeedUserAsync(MicroLimsDbContext db, string fullName = "Test SectionHead")
    {
        var role = new Role { Type = RoleType.SectionHead, Name = "Section Head" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User
        {
            FullName = fullName, Username = "sectionhead", RoleId = role.Id,
            PasswordHash = TestPasswords.Hash(Password)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task SeedTestAsync(MicroLimsDbContext db, string code, WorkflowType type, bool active = true)
    {
        db.TestDefinitions.Add(new TestDefinition
        {
            Code = code, DisplayName = code + " name", SectionId = 1, WorkflowType = type, IsActive = active
        });
        await db.SaveChangesAsync();
    }

    private static async Task<WorkflowProfileVersion> SeedRowAsync(MicroLimsDbContext db, WorkflowType type, string? code,
        decimal? units, decimal? target, DateOnly from, WorkflowProfileStatus status, int createdBy = 0)
    {
        var row = new WorkflowProfileVersion
        {
            WorkflowType = type, TestCode = code, WorkUnits = units, TargetAnalystDays = target,
            EffectiveFrom = from, Status = status, ReasonForChange = "Imported from legacy workload weight",
            CreatedByUserId = createdBy, CreatedAt = Now.UtcDateTime
        };
        db.WorkflowProfileVersions.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    private static CreateWorkflowProfileVersionRequest Create(
        WorkflowType type = WorkflowType.CountTest, string? testCode = null, decimal? units = 1.5m,
        decimal? target = 7m, DateOnly? from = null, string reason = "SOP time study 2026", string password = Password) =>
        new(type, testCode, units, target, from ?? Today, reason, password);

    private static SignWorkflowProfileDraftRequest Sign(
        decimal? units = 2m, decimal? target = 10m, DateOnly? from = null, string reason = "Reviewed against the SOP", string password = Password) =>
        new(units, target, from ?? Today, reason, password);

    // ---- AddVersionAsync -------------------------------------------------

    [Fact]
    public async Task AddVersion_CorrectPassword_CreatesEffectiveRowSignatureAndAuditEvent()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = NewService(db);

        var dto = await service.AddVersionAsync(Create(), user.Id, "10.0.0.1");

        Assert.Equal(WorkflowProfileStatus.Effective, dto.Status);
        Assert.Equal(WorkflowType.CountTest, dto.WorkflowType);
        Assert.Null(dto.TestCode);
        Assert.Equal(1.5m, dto.WorkUnits);
        Assert.Equal(7m, dto.TargetAnalystDays);
        Assert.Equal(Today, dto.EffectiveFrom);
        Assert.Equal("SOP time study 2026", dto.ReasonForChange);
        Assert.Equal(user.Id, dto.CreatedByUserId);
        Assert.Equal("Test SectionHead", dto.CreatedByName);
        Assert.NotNull(dto.ElectronicSignatureId);
        Assert.NotNull(dto.SignedAt);

        var signature = Assert.Single(await db.ElectronicSignatures.ToListAsync());
        Assert.Equal(SignatureMeaning.MasterDataChanged, signature.MeaningOfSignature);
        Assert.Equal(ReviewEntityTypes.WorkflowProfile, signature.EntityType);
        Assert.Equal(dto.Id, signature.EntityId);
        Assert.Equal("10.0.0.1", signature.IpAddress);
        Assert.Contains("CountTest type default", signature.Comment);
        Assert.Contains("SOP time study 2026", signature.Comment);

        var audit = Assert.Single(await db.AuditLogs.Include(a => a.Changes)
            .Where(a => a.ActionCode == WorkflowProfileService.VersionAddedActionCode).ToListAsync());
        Assert.Equal(ReviewEntityTypes.WorkflowProfile, audit.EntityName);
        Assert.Equal(AuditActionCategory.Configuration, audit.ActionCategory);
        Assert.Equal(dto.Id.ToString(), audit.EntityId);
        Assert.Equal("SOP time study 2026", audit.Reason);
        Assert.Contains(audit.Changes, c => c.FieldName == "WorkUnits" && c.PreviousValue == null && c.NewValue == "1.5");
        Assert.Contains(audit.Changes, c => c.FieldName == "TargetAnalystDays" && c.NewValue == "7");
        Assert.Contains(audit.Changes, c => c.FieldName == "EffectiveFrom" && c.NewValue == "2026-10-11");
    }

    [Fact]
    public async Task AddVersion_WrongPassword_ThrowsAndWritesNoSignatureOrAuditEvent()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = NewService(db);

        await Assert.ThrowsAsync<SignatureVerificationException>(
            () => service.AddVersionAsync(Create(password: "WrongPassword!"), user.Id, "127.0.0.1"));

        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
        Assert.Empty(await db.AuditLogs.Where(a => a.ActionCode == WorkflowProfileService.VersionAddedActionCode).ToListAsync());
        // The row itself is removed by the PostgreSQL transaction (UnitOfWork is a no-op on
        // InMemory); WorkflowProfileMigrationPostgresTests pins that half.
    }

    [Fact]
    public async Task AddVersion_Today_IsAllowed_AndYesterdayIsRejectedOnceTheScopeHasAnEffectiveVersion()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = NewService(db);
        await service.AddVersionAsync(Create(from: Today), user.Id, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddVersionAsync(Create(units: 2m, from: Today.AddDays(-1)), user.Id, null));

        Assert.Contains("cannot be in the past", ex.Message);
        Assert.Contains("2026-10-11", ex.Message);
    }

    [Fact]
    public async Task AddVersion_BaselineMayBeBackDated_ExactlyOnce()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = NewService(db);

        var first = await service.AddVersionAsync(Create(from: Today.AddDays(-400)), user.Id, null);
        Assert.Equal(Today.AddDays(-400), first.EffectiveFrom);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddVersionAsync(Create(units: 2m, from: Today.AddDays(-400)), user.Id, null));
    }

    [Fact]
    public async Task AddVersion_DraftDoesNotCountAsTheBaseline()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        await SeedRowAsync(db, WorkflowType.CountTest, null, 2m, null, Today.AddDays(-30), WorkflowProfileStatus.Draft);
        var service = NewService(db);

        var dto = await service.AddVersionAsync(Create(from: Today.AddDays(-60)), user.Id, null);

        Assert.Equal(Today.AddDays(-60), dto.EffectiveFrom);
    }

    [Fact]
    public async Task AddVersion_BaselineIsPerScope_TypeDefaultAndTestOverrideAreIndependent()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        await SeedTestAsync(db, "TAMC", WorkflowType.CountTest);
        var service = NewService(db);
        await service.AddVersionAsync(Create(from: Today), user.Id, null);

        var overrideRow = await service.AddVersionAsync(
            Create(testCode: "TAMC", units: 4m, target: null, from: Today.AddDays(-90)), user.Id, null);

        Assert.Equal("TAMC", overrideRow.TestCode);
        Assert.Equal(Today.AddDays(-90), overrideRow.EffectiveFrom);
    }

    [Theory]
    [InlineData(0.0, 7.0)]
    [InlineData(-1.0, 7.0)]
    [InlineData(1.0, 0.0)]
    [InlineData(1.0, -3.0)]
    [InlineData(12.345, 7.0)]        // would be silently rounded to 12.35 by numeric(8,2)
    [InlineData(1.0, 7.001)]         // numeric(6,2)
    [InlineData(1000000.0, 7.0)]     // numeric(8,2) overflow would be a 500
    [InlineData(1.0, 10000.0)]       // numeric(6,2) overflow
    public async Task AddVersion_InvalidNumbers_Throw(double units, double target)
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var service = NewService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddVersionAsync(Create(units: (decimal)units, target: (decimal)target), user.Id, null));

        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task AddVersion_NeitherValueSet_Throws()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(db).AddVersionAsync(Create(units: null, target: null), user.Id, null));
        Assert.Contains("work units, a target, or both", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddVersion_BlankReason_Throws(string reason)
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(db).AddVersionAsync(Create(reason: reason), user.Id, null));
        Assert.Contains("reason", ex.Message);
    }

    [Fact]
    public async Task AddVersion_ReasonOver500Characters_Throws()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(db).AddVersionAsync(Create(reason: new string('x', 501)), user.Id, null));
    }

    [Theory]
    [InlineData(WorkflowType.HplcAssay)]
    [InlineData(WorkflowType.ElementalAssay)]
    [InlineData(WorkflowType.HplcMultiAnalyte)]
    [InlineData(WorkflowType.StandardComparison)]
    public async Task AddVersion_RetiredWorkflowType_Throws(WorkflowType retired)
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(db).AddVersionAsync(Create(type: retired), user.Id, null));
        Assert.Contains("retired", ex.Message);
    }

    [Fact]
    public async Task AddVersion_UnknownTestCode_Throws()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(db).AddVersionAsync(Create(testCode: "GHOST"), user.Id, null));
        Assert.Contains("GHOST", ex.Message);
    }

    [Fact]
    public async Task AddVersion_TestCode_OverwritesTheRequestedTypeWithTheDefinitionsCurrentType()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        await SeedTestAsync(db, "TITR1", WorkflowType.Titration);

        var dto = await NewService(db).AddVersionAsync(
            Create(type: WorkflowType.CountTest, testCode: "TITR1"), user.Id, null);

        Assert.Equal(WorkflowType.Titration, dto.WorkflowType);
        Assert.Equal("TITR1 name", dto.TestName);
    }

    [Fact]
    public async Task AddVersion_OverrideOfATestWhoseTypeIsRetired_Throws()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        await SeedTestAsync(db, "OLDHPLC", WorkflowType.HplcAssay);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(db).AddVersionAsync(Create(testCode: "OLDHPLC"), user.Id, null));
    }

    // ---- SignDraftAsync --------------------------------------------------

    [Fact]
    public async Task SignDraft_CorrectPassword_AppliesFinalValuesSignsAndAudits()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var draft = await SeedRowAsync(db, WorkflowType.CountTest, null, 1m, null, Today, WorkflowProfileStatus.Draft);
        var service = NewService(db);

        var dto = await service.SignDraftAsync(draft.Id, Sign(units: 2.5m, target: 10m), user.Id, "10.0.0.2");

        Assert.Equal(WorkflowProfileStatus.Effective, dto.Status);
        Assert.Equal(2.5m, dto.WorkUnits);
        Assert.Equal(10m, dto.TargetAnalystDays);
        Assert.Equal("Reviewed against the SOP", dto.ReasonForChange);
        Assert.NotNull(dto.SignedAt);
        Assert.Equal(0, dto.CreatedByUserId); // the importer stays the creator
        Assert.Equal("System (legacy import)", dto.CreatedByName);

        var signature = Assert.Single(await db.ElectronicSignatures.ToListAsync());
        Assert.Equal(draft.Id, signature.EntityId);
        Assert.Equal(ReviewEntityTypes.WorkflowProfile, signature.EntityType);

        var audit = Assert.Single(await db.AuditLogs.Include(a => a.Changes)
            .Where(a => a.ActionCode == WorkflowProfileService.DraftSignedActionCode).ToListAsync());
        Assert.Contains(audit.Changes, c => c.FieldName == "WorkUnits" && c.PreviousValue == "1" && c.NewValue == "2.5");
    }

    [Fact]
    public async Task SignDraft_WrongPassword_LeavesTheDraftUntouched()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var draft = await SeedRowAsync(db, WorkflowType.CountTest, null, 1m, null, Today, WorkflowProfileStatus.Draft);

        await Assert.ThrowsAsync<SignatureVerificationException>(
            () => NewService(db).SignDraftAsync(draft.Id, Sign(password: "nope"), user.Id, null));

        var reloaded = await db.WorkflowProfileVersions.AsNoTracking().SingleAsync(v => v.Id == draft.Id);
        Assert.Equal(WorkflowProfileStatus.Draft, reloaded.Status);
        Assert.Equal(1m, reloaded.WorkUnits);
        Assert.Null(reloaded.ElectronicSignatureId);
        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task SignDraft_AlreadyEffective_Throws()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var draft = await SeedRowAsync(db, WorkflowType.CountTest, null, 1m, null, Today, WorkflowProfileStatus.Draft);
        var service = NewService(db);
        await service.SignDraftAsync(draft.Id, Sign(), user.Id, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SignDraftAsync(draft.Id, Sign(units: 3m), user.Id, null));

        Assert.Contains("already Effective", ex.Message);
        Assert.Single(await db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task SignDraft_StaleIfMatch_ThrowsConcurrency()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var draft = await SeedRowAsync(db, WorkflowType.CountTest, null, 1m, null, Today, WorkflowProfileStatus.Draft);
        db.ExpectedVersion = draft.Version + 5; // the form was loaded before someone else saved

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => NewService(db).SignDraftAsync(draft.Id, Sign(), user.Id, null));

        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task SignDraft_UnknownId_ThrowsNotFound()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        await Assert.ThrowsAsync<MicroLIMS.Shared.Exceptions.NotFoundException>(
            () => NewService(db).SignDraftAsync(999, Sign(), user.Id, null));
    }

    [Fact]
    public async Task SignDraft_StaleImportDate_RejectedWhenScopeHasEffective()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        await SeedRowAsync(db, WorkflowType.CountTest, null, 1m, 7m, Today.AddDays(-100), WorkflowProfileStatus.Effective, user.Id);
        var draft = await SeedRowAsync(db, WorkflowType.CountTest, null, 2m, null, Today.AddDays(-5), WorkflowProfileStatus.Draft);
        var service = NewService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SignDraftAsync(draft.Id, Sign(from: Today.AddDays(-5)), user.Id, null));
        Assert.Contains("cannot be in the past", ex.Message);

        var ok = await service.SignDraftAsync(draft.Id, Sign(from: Today), user.Id, null);
        Assert.Equal(WorkflowProfileStatus.Effective, ok.Status);
        Assert.Equal(Today, ok.EffectiveFrom);
    }

    [Fact]
    public async Task SignDraft_StaleImportDate_AllowedAsBaseline()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var draft = await SeedRowAsync(db, WorkflowType.CountTest, null, 2m, null, Today.AddDays(-5), WorkflowProfileStatus.Draft);

        var dto = await NewService(db).SignDraftAsync(draft.Id, Sign(from: Today.AddDays(-5)), user.Id, null);

        Assert.Equal(Today.AddDays(-5), dto.EffectiveFrom);
    }

    [Fact]
    public async Task SignDraft_FollowsTheTestsCurrentWorkflowType()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        await SeedTestAsync(db, "TAMC", WorkflowType.Measurement); // re-typed since the import
        var draft = await SeedRowAsync(db, WorkflowType.CountTest, "TAMC", 2m, null, Today, WorkflowProfileStatus.Draft);

        var dto = await NewService(db).SignDraftAsync(draft.Id, Sign(), user.Id, null);

        Assert.Equal(WorkflowType.Measurement, dto.WorkflowType);
    }

    [Fact]
    public async Task SignDraft_InvalidNumbers_Throw()
    {
        await using var db = NewDb();
        var user = await SeedUserAsync(db);
        var draft = await SeedRowAsync(db, WorkflowType.CountTest, null, 2m, null, Today, WorkflowProfileStatus.Draft);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewService(db).SignDraftAsync(draft.Id, Sign(units: 12.345m), user.Id, null));
    }
}
