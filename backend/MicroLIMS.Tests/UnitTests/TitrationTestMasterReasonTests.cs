using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Persistence.Helpers;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Reason for change on titration settings (spec 2026-10-05, S2).
public class TitrationTestMasterReasonTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static (MicroLimsDbContext Db, TitrationScenario S, TestDefinitionMasterDataService Svc) Setup()
    {
        var db = NewDb();
        var s = TitrationScenario.Seed(db);
        var audit = new AuditEventService(db, new DatabaseSequenceHelper(db));
        return (db, s, new TestDefinitionMasterDataService(db, new UserSectionScopeService(db), audit));
    }

    private static UpdateTestDefinitionRequest Update(TitrationScenario s, string? displayName = null, decimal? factor = null, string? reason = null) =>
        new(s.Test.Code, displayName ?? s.Test.DisplayName, TitrationEquivalencyFactor: factor) { ChangeReason = reason };

    [Fact]
    public async Task ChangingEquivalencyFactor_WithoutReason_Refused()
    {
        var (db, s, svc) = Setup();
        using var _ = db;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.UpdateTestDefinitionAsync(s.UserId, s.Test.Id, Update(s, factor: 88.07m)));
        Assert.Equal("A reason for change is required when titration settings change.", ex.Message);
    }

    [Fact]
    public async Task ChangingEquivalencyFactor_WithReason_SavesAndAudits()
    {
        var (db, s, svc) = Setup();
        using var _ = db;
        var res = await svc.UpdateTestDefinitionAsync(s.UserId, s.Test.Id, Update(s, factor: 88.07m, reason: "USP 2026 revision"));
        Assert.Equal(88.07m, res.TitrationEquivalencyFactor);

        var log = await db.AuditLogs.Include(a => a.Changes).SingleAsync(a => a.ActionCode == "TestDefinition.TitrationChanged");
        Assert.Equal("USP 2026 revision", log.Reason);
        Assert.Equal(s.Test.Id.ToString(), log.EntityId);
        var change = Assert.Single(log.Changes);
        Assert.Contains("88.06", change.PreviousValue);
        Assert.Contains("88.07", change.NewValue);
    }

    [Fact]
    public async Task ChangingPhyschemArea_WithoutReason_Refused()
    {
        var (db, s, svc) = Setup();
        using var _ = db;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpdateTestDefinitionAsync(s.UserId, s.Test.Id, Update(s) with { PhyschemArea = PhyschemArea.FinishedProduct }));
        Assert.Equal("A reason for change is required when titration settings change.", ex.Message);
    }

    [Fact]
    public async Task ChangingPhyschemArea_WithReason_SavesAndAudits()
    {
        var (db, s, svc) = Setup();
        using var _ = db;
        var res = await svc.UpdateTestDefinitionAsync(s.UserId, s.Test.Id,
            (Update(s, reason: "Also used for raw materials") with { PhyschemArea = PhyschemArea.FinishedProduct }));
        Assert.Equal(PhyschemArea.FinishedProduct, res.PhyschemArea);
        var log = await db.AuditLogs.SingleAsync(a => a.ActionCode == "TestDefinition.TitrationChanged");
        Assert.Equal("Also used for raw materials", log.Reason);
    }

    [Fact]
    public async Task DisplayNameOnly_NeedsNoReason()
    {
        var (db, s, svc) = Setup();
        using var _ = db;
        var res = await svc.UpdateTestDefinitionAsync(s.UserId, s.Test.Id, Update(s, displayName: "Renamed titration"));
        Assert.Equal("Renamed titration", res.DisplayName);
        Assert.Empty(db.AuditLogs.Where(a => a.ActionCode == "TestDefinition.TitrationChanged"));
    }

    [Fact]
    public async Task NonTitrationTest_NeverNeedsReason()
    {
        var (db, s, svc) = Setup();
        using var _ = db;
        var other = new TestDefinition
        {
            Code = "GRAV_" + Guid.NewGuid().ToString("N")[..6], DisplayName = "Gravimetric", SectionId = s.Section.Id, PhyschemArea = PhyschemArea.Both,
            WorkflowType = WorkflowType.Gravimetric, EquationType = EquationType.GravimetricLoss, ReplicateCount = 2, IsActive = true,
        };
        db.TestDefinitions.Add(other);
        db.SaveChanges();
        var res = await svc.UpdateTestDefinitionAsync(s.UserId, other.Id, new UpdateTestDefinitionRequest(other.Code, "Gravimetric renamed"));
        Assert.Equal("Gravimetric renamed", res.DisplayName);
    }
}
