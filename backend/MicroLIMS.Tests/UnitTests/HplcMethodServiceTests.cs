using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class HplcMethodServiceTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        db.CurrentUserId = 1;
        return db;
    }

    private static async Task<(DocumentSection section, int userId)> SeedAsync(MicroLimsDbContext db)
    {
        var section = TestServiceFactory.EnsureMicroSection(db);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { Username = "u_" + Guid.NewGuid().ToString("N")[..6], FullName = "Test User", RoleId = role.Id, IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        TestServiceFactory.AssignUserToMicroSection(db, user.Id);
        return (section, user.Id);
    }

    private static async Task<MaterialMasterEntry> AddEntryAsync(
        MicroLimsDbContext db, int sectionId, string code, bool isActive = true,
        MaterialMasterCategory category = MaterialMasterCategory.ReferenceStandard)
    {
        var entry = new MaterialMasterEntry
        {
            SectionId = sectionId,
            Code = code,
            Name = code,
            Category = category,
            BaseUnit = MaterialUnit.Gram,
            IsActive = isActive,
            CreatedByUserId = 1,
            CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = 1,
            LastModifiedAt = DateTime.UtcNow
        };
        db.MaterialMasterEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    private static async Task<SolutionMaster> AddSolutionAsync(
        MicroLimsDbContext db, int sectionId, string name, SolutionType type, bool isActive = true)
    {
        var componentEntry = await AddEntryAsync(db, sectionId, "COMP-" + Guid.NewGuid().ToString("N")[..6], category: MaterialMasterCategory.Reagent);
        var service = TestServiceFactory.SolutionMaster(db);
        var created = await service.CreateAsync(new SaveSolutionMasterRequest(
            name, type, 7, ShelfLifeUnit.Days, "Room temperature", 1000m, "Mix and filter.",
            new List<SolutionComponentInput> { new(componentEntry.Id, 500m, SolutionComponentUnit.Milliliter) },
            SectionId: sectionId), 1);

        if (!isActive)
        {
            var solution = await db.SolutionMasters.FirstAsync(s => s.Id == created.Id);
            solution.IsActive = false;
            await db.SaveChangesAsync();
        }

        return await db.SolutionMasters.FirstAsync(s => s.Id == created.Id);
    }

    private static async Task<(SolutionMaster diluent, SolutionMaster mobilePhase, MaterialMasterEntry standard)> SeedBasicsAsync(MicroLimsDbContext db, int sectionId)
    {
        var diluent = await AddSolutionAsync(db, sectionId, "Diluent Water", SolutionType.Diluent);
        var mobilePhase = await AddSolutionAsync(db, sectionId, "Mobile Phase A", SolutionType.MobilePhase);
        var standard = await AddEntryAsync(db, sectionId, "STD-01");
        return (diluent, mobilePhase, standard);
    }

    private static HplcAnalyteInput AnalyteInput(int standardEntryId, string name = "Analyte 1", int? id = null) =>
        new(id, name, 254m, standardEntryId, 50m, 50m, 5);

    private static SaveHplcMethodRequest Req(
        int diluentSolutionId, List<HplcMobilePhaseInput> mobilePhases, List<HplcAnalyteInput> analytes,
        string name = "Assay Method", string abbreviation = "AM-01", ElutionMode elutionMode = ElutionMode.Isocratic,
        List<HplcGradientStepInput>? gradientSteps = null, int? sectionId = null, string? reason = null) =>
        new(
            name, abbreviation, DateTime.UtcNow,
            "L1", 150m, 4.6m, 5m,
            30m, elutionMode, 1m,
            HplcDetectorType.UV, 20m, 15m, diluentSolutionId,
            mobilePhases, gradientSteps ?? new List<HplcGradientStepInput>(), analytes,
            SectionId: sectionId, Reason: reason);

    [Fact]
    public async Task Create_Isocratic_SavesChildren_AndUppercasesAbbreviation()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var req = Req(diluent.Id,
            new List<HplcMobilePhaseInput> { new("a", mp.Id, null) },
            new List<HplcAnalyteInput> { AnalyteInput(standard.Id) },
            abbreviation: "am-01");

        var result = await service.CreateAsync(req, userId);

        Assert.Equal("AM-01", result.Abbreviation);
        Assert.Single(result.MobilePhases);
        Assert.Equal("A", result.MobilePhases[0].Channel);
        Assert.Single(result.Analytes);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Create_DuplicateAbbreviationInSection_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) }, new List<HplcAnalyteInput> { AnalyteInput(standard.Id) });
        await service.CreateAsync(req, userId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task Create_NoAnalytes_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, _) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) }, new List<HplcAnalyteInput>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("analyte", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_NoMobilePhases_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, _, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var req = Req(diluent.Id, new List<HplcMobilePhaseInput>(), new List<HplcAnalyteInput> { AnalyteInput(standard.Id) });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("mobile phase", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_DuplicateAnalyteName_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var standard2 = await AddEntryAsync(db, section.Id, "STD-02");
        var service = TestServiceFactory.HplcMethod(db);

        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) },
            new List<HplcAnalyteInput> { AnalyteInput(standard.Id, "Same"), AnalyteInput(standard2.Id, "same") });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("more than once", ex.Message);
    }

    [Fact]
    public async Task Create_AnalyteStandardNotReferenceStandard_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, _) = await SeedBasicsAsync(db, section.Id);
        var reagent = await AddEntryAsync(db, section.Id, "REAG-01", category: MaterialMasterCategory.Reagent);
        var service = TestServiceFactory.HplcMethod(db);

        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) }, new List<HplcAnalyteInput> { AnalyteInput(reagent.Id) });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("reference standard", ex.Message);
    }

    [Fact]
    public async Task Create_ThWtZero_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var analyte = new HplcAnalyteInput(null, "A1", 254m, standard.Id, 0m, 50m, 5);
        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) }, new List<HplcAnalyteInput> { analyte });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("Theoretical weight", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_StandardInjectionsZero_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var analyte = new HplcAnalyteInput(null, "A1", 254m, standard.Id, 50m, 50m, 0);
        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) }, new List<HplcAnalyteInput> { analyte });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("Standard injections", ex.Message);
    }

    [Fact]
    public async Task Create_ChannelNotAtoD_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("E", mp.Id, null) }, new List<HplcAnalyteInput> { AnalyteInput(standard.Id) });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("A, B, C or D", ex.Message);
    }

    [Fact]
    public async Task Create_DuplicateChannel_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var mp2 = await AddSolutionAsync(db, section.Id, "Mobile Phase B", SolutionType.MobilePhase);
        var service = TestServiceFactory.HplcMethod(db);

        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null), new("A", mp2.Id, null) }, new List<HplcAnalyteInput> { AnalyteInput(standard.Id) });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("used more than once", ex.Message);
    }

    [Fact]
    public async Task Create_MobilePhaseChannelUsesDiluentSolution_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, _, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", diluent.Id, null) }, new List<HplcAnalyteInput> { AnalyteInput(standard.Id) });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("MobilePhase", ex.Message);
    }

    [Fact]
    public async Task Create_DiluentIsMobilePhaseSolution_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (_, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var req = Req(mp.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) }, new List<HplcAnalyteInput> { AnalyteInput(standard.Id) });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("type Diluent", ex.Message);
    }

    [Fact]
    public async Task Create_GradientWithoutSteps_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) }, new List<HplcAnalyteInput> { AnalyteInput(standard.Id) },
            elutionMode: ElutionMode.Gradient);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("at least 2 steps", ex.Message);
    }

    [Fact]
    public async Task Create_IsocraticWithSteps_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var steps = new List<HplcGradientStepInput> { new(0m, 100m, 0m, 0m, 0m) };
        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) }, new List<HplcAnalyteInput> { AnalyteInput(standard.Id) },
            elutionMode: ElutionMode.Isocratic, gradientSteps: steps);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("must not have gradient steps", ex.Message);
    }

    [Fact]
    public async Task Create_GradientStepPercentsNot100_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var mp2 = await AddSolutionAsync(db, section.Id, "Mobile Phase B", SolutionType.MobilePhase);
        var service = TestServiceFactory.HplcMethod(db);

        var steps = new List<HplcGradientStepInput>
        {
            new(0m, 90m, 0m, 0m, 0m),
            new(5m, 50m, 40m, 0m, 0m)
        };
        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null), new("B", mp2.Id, null) },
            new List<HplcAnalyteInput> { AnalyteInput(standard.Id) }, elutionMode: ElutionMode.Gradient, gradientSteps: steps);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("must equal 100", ex.Message);
    }

    [Fact]
    public async Task Create_IsocraticRatiosNot100_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var mp2 = await AddSolutionAsync(db, section.Id, "Mobile Phase B", SolutionType.MobilePhase);
        var service = TestServiceFactory.HplcMethod(db);

        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, 60m), new("B", mp2.Id, 30m) },
            new List<HplcAnalyteInput> { AnalyteInput(standard.Id) });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("sum to 100", ex.Message);
    }

    [Fact]
    public async Task Update_WithoutReason_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) }, new List<HplcAnalyteInput> { AnalyteInput(standard.Id) });
        var created = await service.CreateAsync(req, userId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, req, userId));
        Assert.Contains("reason", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_KeepsAnalyteIds_ForUnchangedAnalytes()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var standard2 = await AddEntryAsync(db, section.Id, "STD-02");
        var service = TestServiceFactory.HplcMethod(db);

        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) },
            new List<HplcAnalyteInput> { AnalyteInput(standard.Id, "Analyte 1") });
        var created = await service.CreateAsync(req, userId);
        var existingAnalyteId = created.Analytes[0].Id;

        var updateReq = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) },
            new List<HplcAnalyteInput>
            {
                new(existingAnalyteId, "Analyte 1", 254m, standard.Id, 50m, 50m, 5),
                AnalyteInput(standard2.Id, "Analyte 2")
            }, reason: "Add second analyte");

        var updated = await service.UpdateAsync(created.Id, updateReq, userId);

        Assert.Equal(2, updated.Analytes.Count);
        Assert.Contains(updated.Analytes, a => a.Id == existingAnalyteId && a.Name == "Analyte 1");
    }

    [Fact]
    public async Task Update_RemovingAnalyteUsedBySpecification_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) },
            new List<HplcAnalyteInput> { AnalyteInput(standard.Id, "Analyte 1") });
        var created = await service.CreateAsync(req, userId);
        var analyteId = created.Analytes[0].Id;

        var item = new Item { Code = "ITEM-" + Guid.NewGuid().ToString("N")[..6], Name = "Item 1" };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        db.Specifications.Add(new Specification
        {
            ItemId = item.Id,
            TestCode = "T1",
            ParameterName = "Assay",
            HplcMethodAnalyteId = analyteId,
            LimitType = LimitType.Range,
            LowerLimit = 90m,
            UpperLimit = 110m
        });
        await db.SaveChangesAsync();

        var standard2 = await AddEntryAsync(db, section.Id, "STD-02");
        var updateReq = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) },
            new List<HplcAnalyteInput> { AnalyteInput(standard2.Id, "Analyte 2") }, reason: "Swap analyte");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, updateReq, userId));
        Assert.Contains("used by specifications", ex.Message);
    }

    [Fact]
    public async Task Update_RecordsHistoryWithBeforeAndAfterJson()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) }, new List<HplcAnalyteInput> { AnalyteInput(standard.Id) });
        var created = await service.CreateAsync(req, userId);

        var updateReq = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) },
            new List<HplcAnalyteInput> { AnalyteInput(standard.Id) }, name: "Renamed Method", reason: "Rename");
        await service.UpdateAsync(created.Id, updateReq, userId);

        var history = await service.GetHistoryAsync(created.Id, userId);

        var updateEntry = Assert.Single(history, h => h.Action == "HplcMethod.Updated");
        Assert.Equal("Rename", updateEntry.Reason);
        Assert.NotNull(updateEntry.BeforeJson);
        Assert.NotNull(updateEntry.AfterJson);
        Assert.Contains("Assay Method", updateEntry.BeforeJson);
        Assert.Contains("Renamed Method", updateEntry.AfterJson);
    }

    [Fact]
    public async Task SetActive_False_RequiresReason_AndAppearsInHistory()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var (diluent, mp, standard) = await SeedBasicsAsync(db, section.Id);
        var service = TestServiceFactory.HplcMethod(db);

        var req = Req(diluent.Id, new List<HplcMobilePhaseInput> { new("A", mp.Id, null) }, new List<HplcAnalyteInput> { AnalyteInput(standard.Id) });
        var created = await service.CreateAsync(req, userId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetActiveAsync(created.Id, false, "", userId));
        Assert.Contains("reason", ex.Message, StringComparison.OrdinalIgnoreCase);

        var deactivated = await service.SetActiveAsync(created.Id, false, "No longer used", userId);
        Assert.False(deactivated.IsActive);

        var history = await service.GetHistoryAsync(created.Id, userId);
        Assert.Contains(history, h => h.Action == "HplcMethod.Deactivated" && h.Reason == "No longer used");
    }
}
