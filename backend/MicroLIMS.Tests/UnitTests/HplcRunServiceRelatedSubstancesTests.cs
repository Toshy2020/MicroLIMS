using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public partial class HplcRunServiceTests
{
    [Fact]
    public async Task StartRun_RelatedSubstancesMethod_SnapshotCarriesImpurities()
    {
        await using var db = NewDb();
        var (_, clock) = NewClock(SepFirst);
        var s = await SeedScenarioAsync(db, clock);
        // The run snapshot only reads the loaded method, so mark it RS and give it impurities directly.
        s.Method.ResultMode = HplcResultMode.RelatedSubstances;
        s.Method.ReferenceSolutionPercent = 0.1m;
        s.Method.DisregardLimitPercent = 0.05m;
        s.Method.Impurities.Add(new HplcMethodImpurity { DisplayOrder = 1, Name = "Impurity D", RelativeRetention = 0.15m, CorrectionFactor = 0.7m });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear(); // so the run must load the impurities itself
        var service = TestServiceFactory.HplcRun(db, clock: clock);

        var run = await service.StartRunAsync(StartRequest(s), s.UserId);

        var stored = await db.HplcRuns.FirstAsync(r => r.Id == run.Id);
        var snapshot = JsonSerializer.Deserialize<HplcMethodResponse>(stored.MethodSnapshotJson, SnapshotJson.Options)!;
        var impurity = Assert.Single(snapshot.Impurities);
        Assert.Equal("Impurity D", impurity.Name);
        Assert.Equal(0.7m, impurity.CorrectionFactor);
        Assert.Equal(0.1m, snapshot.ReferenceSolutionPercent);
    }
}
