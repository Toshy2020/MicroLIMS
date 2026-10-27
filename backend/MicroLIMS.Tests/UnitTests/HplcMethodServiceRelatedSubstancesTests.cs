using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using Xunit;
using Base = MicroLIMS.Tests.UnitTests.HplcMethodServiceTests;

namespace MicroLIMS.Tests.UnitTests;

// Related-substances method mode (Plan 3 task 1): impurities, reference solution %, disregard limit.
public class HplcMethodServiceRelatedSubstancesTests
{
    private static SaveHplcMethodRequest RsReq(
        int diluentId, int mobilePhaseId, int standardId, string abbreviation = "RS-01", string? reason = null,
        List<HplcAnalyteInput>? analytes = null, decimal? reference = 0.1m, decimal? disregard = 0.05m,
        List<HplcImpurityInput>? impurities = null) =>
        Base.Req(diluentId, new() { new("A", mobilePhaseId, null) },
            analytes ?? new() { new(null, "Principal", 230m, standardId, 0m, 0m, 5) },
            name: "Related Substances", abbreviation: abbreviation, reason: reason) with
        {
            ResultMode = HplcResultMode.RelatedSubstances,
            ReferenceSolutionPercent = reference,
            DisregardLimitPercent = disregard,
            Impurities = impurities ?? new() { new(null, "Impurity D", 0.15m, 0.7m), new(null, "Impurity A", 1.25m, null) },
        };

    [Fact]
    public async Task Create_RelatedSubstances_SavesImpuritiesAndMethodValues()
    {
        await using var db = Base.NewDb();
        var (section, userId) = await Base.SeedAsync(db);
        var (diluent, mp, standard) = await Base.SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var result = await service.CreateAsync(RsReq(diluent.Id, mp.Id, standard.Id), userId);

        Assert.Equal(HplcResultMode.RelatedSubstances, result.ResultMode);
        Assert.Equal(0.1m, result.ReferenceSolutionPercent);
        Assert.Equal(0.05m, result.DisregardLimitPercent);
        Assert.Equal(2, result.Impurities.Count);
        Assert.Equal("Impurity D", result.Impurities[0].Name);
        Assert.Equal(1, result.Impurities[0].DisplayOrder);
        Assert.Equal(0.15m, result.Impurities[0].RelativeRetention);
        Assert.Equal(0.7m, result.Impurities[0].CorrectionFactor);
        Assert.Equal(1m, result.Impurities[1].CorrectionFactor);
        Assert.Equal(2, await db.HplcMethodImpurities.CountAsync(i => i.HplcMethodId == result.Id));
    }

    [Theory]
    [InlineData("two-analytes", "A related-substances method has exactly one analyte: the principal peak.")]
    [InlineData("thwt", "Theoretical weights are not used in a related-substances method.")]
    [InlineData("ref-null", "Enter the reference solution concentration as % of the test solution (above 0, up to 100).")]
    [InlineData("ref-zero", "Enter the reference solution concentration as % of the test solution (above 0, up to 100).")]
    [InlineData("ref-over", "Enter the reference solution concentration as % of the test solution (above 0, up to 100).")]
    [InlineData("disregard-neg", "The disregard limit must be between 0 and 100 %.")]
    [InlineData("disregard-over", "The disregard limit must be between 0 and 100 %.")]
    [InlineData("name-blank", "Each impurity needs a unique name.")]
    [InlineData("name-dup", "Each impurity needs a unique name.")]
    [InlineData("cf-zero", "The correction factor must be greater than zero and at most 100.")]
    [InlineData("cf-negative", "The correction factor must be greater than zero and at most 100.")]
    [InlineData("cf-over", "The correction factor must be greater than zero and at most 100.")]
    [InlineData("rrt-zero", "The relative retention must be greater than zero and at most 100.")]
    [InlineData("rrt-negative", "The relative retention must be greater than zero and at most 100.")]
    [InlineData("rrt-over", "The relative retention must be greater than zero and at most 100.")]
    [InlineData("name-long", "Impurity names are at most 100 characters.")]
    [InlineData("no-analytes", "A related-substances method has exactly one analyte: the principal peak.")]
    [InlineData("null-analytes", "A related-substances method has exactly one analyte: the principal peak.")]
    [InlineData("gc", "Related substances is available for HPLC methods only.")]
    public async Task Create_RelatedSubstances_Refusals(string field, string message)
    {
        await using var db = Base.NewDb();
        var (section, userId) = await Base.SeedAsync(db);
        var (diluent, mp, standard) = await Base.SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        var r = RsReq(diluent.Id, mp.Id, standard.Id);

        var req = field switch
        {
            "two-analytes" => RsReq(diluent.Id, mp.Id, standard.Id, analytes: new()
            {
                new(null, "Principal", 230m, standard.Id, 0m, 0m, 5), new(null, "Second", 230m, standard.Id, 0m, 0m, 5),
            }),
            "thwt" => RsReq(diluent.Id, mp.Id, standard.Id, analytes: new() { new(null, "Principal", 230m, standard.Id, 50m, 50m, 5) }),
            "ref-null" => r with { ReferenceSolutionPercent = null },
            "ref-zero" => r with { ReferenceSolutionPercent = 0m },
            "ref-over" => r with { ReferenceSolutionPercent = 100.5m },
            "disregard-neg" => r with { DisregardLimitPercent = -0.01m },
            "name-blank" => r with { Impurities = new() { new(null, "  ", null, null) } },
            "name-dup" => r with { Impurities = new() { new(null, "Imp A", null, null), new(null, "imp a", null, null) } },
            "disregard-over" => r with { DisregardLimitPercent = 100.5m },
            "cf-zero" => r with { Impurities = new() { new(null, "Imp A", null, 0m) } },
            "cf-negative" => r with { Impurities = new() { new(null, "Imp A", null, -1m) } },
            "cf-over" => r with { Impurities = new() { new(null, "Imp A", null, 100.5m) } },
            "rrt-zero" => r with { Impurities = new() { new(null, "Imp A", 0m, null) } },
            "rrt-negative" => r with { Impurities = new() { new(null, "Imp A", -0.5m, null) } },
            "rrt-over" => r with { Impurities = new() { new(null, "Imp A", 100.5m, null) } },
            "name-long" => r with { Impurities = new() { new(null, new string((char)120, 101), null, null) } },
            "no-analytes" => r with { Analytes = new() },
            "null-analytes" => r with { Analytes = null! },
            "gc" => r with { Technique = HplcTechnique.Gc },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Equal(message, ex.Message);
    }

    [Theory]
    [InlineData("impurities")]
    [InlineData("reference")]
    [InlineData("disregard")]
    public async Task Create_AssayOrResidual_RefusesImpuritiesAndRsValues(string field)
    {
        await using var db = Base.NewDb();
        var (section, userId) = await Base.SeedAsync(db);
        var (diluent, mp, standard) = await Base.SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        var assay = Base.Req(diluent.Id, new() { new("A", mp.Id, null) }, new() { Base.AnalyteInput(standard.Id) });

        var req = field switch
        {
            "impurities" => assay with { Impurities = new() { new(null, "Imp A", null, null) } },
            "reference" => assay with { ReferenceSolutionPercent = 0.1m },
            _ => assay with { DisregardLimitPercent = 0.05m },
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Equal("Impurities and reference-solution values belong to related-substances methods only.", ex.Message);
    }

    [Fact]
    public async Task Create_Assay_HasEmptyImpurityList()
    {
        await using var db = Base.NewDb();
        var (section, userId) = await Base.SeedAsync(db);
        var (diluent, mp, standard) = await Base.SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var created = await service.CreateAsync(
            Base.Req(diluent.Id, new() { new("A", mp.Id, null) }, new() { Base.AnalyteInput(standard.Id) }), userId);

        Assert.NotNull(created.Impurities);
        Assert.Empty(created.Impurities);
        Assert.Null(created.ReferenceSolutionPercent);
    }

    [Fact]
    public async Task Update_KeepsImpurityIdsWhenUnchanged_AndRefusesModeChange()
    {
        await using var db = Base.NewDb();
        var (section, userId) = await Base.SeedAsync(db);
        var (diluent, mp, standard) = await Base.SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        var created = await service.CreateAsync(RsReq(diluent.Id, mp.Id, standard.Id), userId);
        var d = created.Impurities[0];
        var analyteId = created.Analytes[0].Id;
        var principal = new List<HplcAnalyteInput> { new(analyteId, "Principal", 230m, standard.Id, 0m, 0m, 5) };

        var updated = await service.UpdateAsync(created.Id, RsReq(diluent.Id, mp.Id, standard.Id, reason: "edit", analytes: principal,
            reference: 0.2m, impurities: new()
            {
                new(d.Id, "Impurity D", 0.16m, 0.7m),   // kept, RRT edited
                new(null, "Impurity B", 0.9m, 1.5m),    // added; Impurity A dropped
            }), userId);

        Assert.Equal(0.2m, updated.ReferenceSolutionPercent);
        Assert.Equal(2, updated.Impurities.Count);
        Assert.Equal(d.Id, updated.Impurities[0].Id);
        Assert.Equal(0.16m, updated.Impurities[0].RelativeRetention);
        Assert.Equal("Impurity B", updated.Impurities[1].Name);
        Assert.Equal(2, await db.HplcMethodImpurities.CountAsync(i => i.HplcMethodId == created.Id));

        var audit = await db.AuditLogs.Include(a => a.Changes)
            .FirstAsync(a => a.ActionCode == "HplcMethod.Updated" && a.EntityId == created.Id.ToString());
        Assert.Contains("Impurity B", audit.Changes.First().NewValue);
        Assert.Contains("Impurity A", audit.Changes.First().PreviousValue);

        var assay = Base.Req(diluent.Id, new() { new("A", mp.Id, null) },
            new() { new(analyteId, "Principal", 254m, standard.Id, 50m, 50m, 5) }, reason: "switch");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, assay, userId));
        Assert.Equal("The technique and result mode can't be changed after the method is created.", ex.Message);
    }

    [Theory]
    [InlineData("foreign", "Impurity 999999 does not belong to this method.")]
    [InlineData("duplicate", "Each impurity can appear only once.")]
    public async Task Update_RefusesForeignAndDuplicateImpurityIds(string field, string message)
    {
        await using var db = Base.NewDb();
        var (section, userId) = await Base.SeedAsync(db);
        var (diluent, mp, standard) = await Base.SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        var created = await service.CreateAsync(RsReq(diluent.Id, mp.Id, standard.Id), userId);
        var principal = new List<HplcAnalyteInput> { new(created.Analytes[0].Id, "Principal", 230m, standard.Id, 0m, 0m, 5) };
        var d = created.Impurities[0];

        var impurities = field == "foreign"
            ? new List<HplcImpurityInput> { new(d.Id, "Impurity D", 0.15m, 0.7m), new(999999, "Impurity X", 0.5m, null) }
            : new List<HplcImpurityInput> { new(d.Id, "Impurity D", 0.15m, 0.7m), new(d.Id, "Impurity E", 0.5m, null) };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(created.Id, RsReq(diluent.Id, mp.Id, standard.Id, reason: "edit", analytes: principal, impurities: impurities), userId));
        Assert.Equal(message, ex.Message);
        Assert.Equal(2, await db.HplcMethodImpurities.CountAsync(i => i.HplcMethodId == created.Id));
    }

    [Fact]
    public async Task Update_RemovingImpurityWithRecordedPeaks_Refused()
    {
        await using var db = Base.NewDb();
        var (section, userId) = await Base.SeedAsync(db);
        var (diluent, mp, standard) = await Base.SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);
        var created = await service.CreateAsync(RsReq(diluent.Id, mp.Id, standard.Id), userId);
        var d = created.Impurities[0];
        db.HplcReplicatePeaks.Add(new HplcReplicatePeak { HplcSampleReplicateId = 1, HplcMethodImpurityId = d.Id, Area = 100m });
        await db.SaveChangesAsync();
        var principal = new List<HplcAnalyteInput> { new(created.Analytes[0].Id, "Principal", 230m, standard.Id, 0m, 0m, 5) };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id,
            RsReq(diluent.Id, mp.Id, standard.Id, reason: "drop", analytes: principal, impurities: new() { new(null, "Impurity Q", 0.5m, null) }), userId));

        Assert.Equal("Impurity Impurity D has recorded peaks and cannot be removed.", ex.Message);
        Assert.True(await db.HplcMethodImpurities.AnyAsync(i => i.Id == d.Id));
    }
}
