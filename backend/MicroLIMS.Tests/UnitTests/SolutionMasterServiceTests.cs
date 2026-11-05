using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class SolutionMasterServiceTests
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
        MicroLimsDbContext db, int sectionId, string code,
        MaterialMasterCategory category = MaterialMasterCategory.Reagent, bool isActive = true)
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

    private static SaveSolutionMasterRequest Req(
        string name = "Mobile Phase A",
        SolutionType type = SolutionType.MobilePhase,
        int shelfLifeValue = 7,
        ShelfLifeUnit shelfLifeUnit = ShelfLifeUnit.Days,
        string storageCondition = "Room temperature",
        decimal finalVolumeMl = 1000,
        string instructions = "Mix and filter.",
        List<SolutionComponentInput>? components = null,
        decimal? phTarget = null,
        decimal? phTolerance = null,
        int? phAdjustingEntryId = null,
        int? sectionId = null,
        decimal? nominalStrength = null,
        TitrantStrengthUnit? strengthUnit = null,
        StandardizationMode? standardizationMode = null,
        int? standardEntryId = null,
        decimal? equivalenceMgPerMl = null,
        int? referenceSolutionId = null,
        bool blankRequired = false,
        int? replicateCount = null,
        decimal? factorMin = null,
        decimal? factorMax = null,
        decimal? maxRsdPercent = null,
        int? validityDays = null,
        string? reason = null) =>
        new(name, type, shelfLifeValue, shelfLifeUnit, storageCondition, finalVolumeMl, instructions,
            components ?? new List<SolutionComponentInput>(),
            phTarget, phTolerance, phAdjustingEntryId, sectionId,
            nominalStrength, strengthUnit, standardizationMode, standardEntryId, equivalenceMgPerMl,
            referenceSolutionId, blankRequired, replicateCount, factorMin, factorMax, maxRsdPercent,
            validityDays, reason);

    private static List<SolutionComponentInput> OneComponent(int entryId, decimal quantity = 500) =>
        new() { new SolutionComponentInput(entryId, quantity, SolutionComponentUnit.Milliliter) };

    private static SaveSolutionMasterRequest TitrantReq(
        int standardEntryId, string name = "Sodium Hydroxide 0.1N",
        decimal nominalStrength = 0.1m,
        int? sectionId = null,
        List<SolutionComponentInput>? components = null,
        int? referenceSolutionId = null,
        StandardizationMode mode = StandardizationMode.PrimaryStandard) =>
        Req(
            name: name,
            type: SolutionType.Titrant,
            sectionId: sectionId,
            components: components,
            nominalStrength: nominalStrength,
            strengthUnit: TitrantStrengthUnit.Normal,
            standardizationMode: mode,
            standardEntryId: mode == StandardizationMode.PrimaryStandard ? standardEntryId : null,
            equivalenceMgPerMl: mode == StandardizationMode.PrimaryStandard ? 6.005m : null,
            referenceSolutionId: mode == StandardizationMode.AgainstVolumetricSolution ? referenceSolutionId : null,
            replicateCount: 3,
            factorMin: 0.95m,
            factorMax: 1.05m,
            maxRsdPercent: 1.0m,
            validityDays: 30);

    [Fact]
    public async Task Create_MobilePhase_WithComponents_OrdersThem()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var buffer = await AddEntryAsync(db, section.Id, "BUF-01");
        var acn = await AddEntryAsync(db, section.Id, "ACN-01");
        var service = TestServiceFactory.SolutionMaster(db);

        var components = new List<SolutionComponentInput>
        {
            new(buffer.Id, 700, SolutionComponentUnit.Milliliter),
            new(acn.Id, 300, SolutionComponentUnit.Milliliter)
        };

        var result = await service.CreateAsync(Req(components: components), userId);

        Assert.Equal(2, result.Components.Count);
        Assert.Equal(1, result.Components[0].Order);
        Assert.Equal(buffer.Id, result.Components[0].MaterialMasterEntryId);
        Assert.Equal(2, result.Components[1].Order);
        Assert.Equal(acn.Id, result.Components[1].MaterialMasterEntryId);
    }

    [Fact]
    public async Task Create_NoComponents_Throws()
    {
        await using var db = NewDb();
        var (_, userId) = await SeedAsync(db);
        var service = TestServiceFactory.SolutionMaster(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(Req(), userId));
        Assert.Contains("at least one component", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_DuplicateComponentEntry_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "BUF-01");
        var service = TestServiceFactory.SolutionMaster(db);

        var components = new List<SolutionComponentInput>
        {
            new(entry.Id, 500, SolutionComponentUnit.Milliliter),
            new(entry.Id, 200, SolutionComponentUnit.Milliliter)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(Req(components: components), userId));
        Assert.Contains("listed twice", ex.Message);
    }

    [Fact]
    public async Task Create_ComponentFromOtherLabEntry_Succeeds()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);

        var otherDept = new MicroLIMS.Domain.Entities.DocumentDepartment { Name = "Other Dept", Code = "OTH", IsActive = true };
        db.DocumentDepartments.Add(otherDept);
        await db.SaveChangesAsync();
        var otherSection = new DocumentSection { Name = "Physicochemical Laboratory", Code = "FP", DepartmentId = otherDept.Id, IsActive = true };
        db.DocumentSections.Add(otherSection);
        await db.SaveChangesAsync();

        var entry = await AddEntryAsync(db, otherSection.Id, "BUF-01");
        var service = TestServiceFactory.SolutionMaster(db);

        // Reagent master entries are app-wide: any lab's solution can use another lab's entry.
        var created = await service.CreateAsync(Req(components: OneComponent(entry.Id)), userId);
        Assert.Equal(section.Id, created.SectionId);
        Assert.Equal(entry.Id, Assert.Single(created.Components).MaterialMasterEntryId);
    }

    [Fact]
    public async Task Create_InactiveComponentEntry_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "BUF-01", isActive: false);
        var service = TestServiceFactory.SolutionMaster(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(Req(components: OneComponent(entry.Id)), userId));
        Assert.Contains("inactive", ex.Message);
    }

    [Fact]
    public async Task Create_ShelfLifeZero_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "BUF-01");
        var service = TestServiceFactory.SolutionMaster(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(Req(shelfLifeValue: 0, components: OneComponent(entry.Id)), userId));
        Assert.Contains("greater than zero", ex.Message);
    }

    [Fact]
    public async Task Create_FinalVolumeZero_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "BUF-01");
        var service = TestServiceFactory.SolutionMaster(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(Req(finalVolumeMl: 0, components: OneComponent(entry.Id)), userId));
        Assert.Contains("greater than zero", ex.Message);
    }

    [Fact]
    public async Task Create_PhToleranceWithoutTarget_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "BUF-01");
        var service = TestServiceFactory.SolutionMaster(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(Req(components: OneComponent(entry.Id), phTolerance: 0.2m), userId));
        Assert.Contains("pH target", ex.Message);
    }

    [Fact]
    public async Task Create_TitrantFieldsOnDiluent_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "BUF-01");
        var service = TestServiceFactory.SolutionMaster(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(Req(type: SolutionType.Diluent, components: OneComponent(entry.Id), nominalStrength: 0.1m), userId));
        Assert.Contains("only allowed for titrants", ex.Message);
    }

    [Fact]
    public async Task Create_Titrant_PrimaryStandard_RequiresStandardAndEquivalence()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var titrantComponent = await AddEntryAsync(db, section.Id, "NAOH-01");
        var service = TestServiceFactory.SolutionMaster(db);

        var req = Req(
            name: "NaOH 0.1N", type: SolutionType.Titrant,
            components: OneComponent(titrantComponent.Id),
            nominalStrength: 0.1m, strengthUnit: TitrantStrengthUnit.Normal,
            standardizationMode: StandardizationMode.PrimaryStandard,
            // StandardEntryId and EquivalenceMgPerMl deliberately omitted
            replicateCount: 3, factorMin: 0.95m, factorMax: 1.05m, maxRsdPercent: 1.0m, validityDays: 30);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("standard entry", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_Titrant_PrimaryStandard_StandardMustBePrimaryStandardEntry()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var titrantComponent = await AddEntryAsync(db, section.Id, "NAOH-01");
        var indicator = await AddEntryAsync(db, section.Id, "PHTH-01", category: MaterialMasterCategory.Indicator);
        var service = TestServiceFactory.SolutionMaster(db);

        var req = TitrantReq(indicator.Id, components: OneComponent(titrantComponent.Id));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("must be a Primary Standard entry", ex.Message);
    }

    [Fact]
    public async Task Create_Titrant_PrimaryStandard_WithReagentEntry_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var titrantComponent = await AddEntryAsync(db, section.Id, "NAOH-01");
        var reagent = await AddEntryAsync(db, section.Id, "KHP-R", category: MaterialMasterCategory.Reagent);
        var service = TestServiceFactory.SolutionMaster(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(TitrantReq(reagent.Id, components: OneComponent(titrantComponent.Id)), userId));
        Assert.Equal("The primary standard must be a Primary Standard entry in Reagents & Standards.", ex.Message);
    }

    [Fact]
    public async Task Create_Titrant_PrimaryStandard_WithPrimaryStandardEntry_Succeeds()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var titrantComponent = await AddEntryAsync(db, section.Id, "NAOH-01");
        var khp = await AddEntryAsync(db, section.Id, "KHP-P", category: MaterialMasterCategory.PrimaryStandard);
        var service = TestServiceFactory.SolutionMaster(db);

        var created = await service.CreateAsync(TitrantReq(khp.Id, components: OneComponent(titrantComponent.Id)), userId);
        Assert.True(created.Id > 0);
    }

    [Fact]
    public async Task Update_Titrant_PrimaryStandard_ToReagentEntry_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var titrantComponent = await AddEntryAsync(db, section.Id, "NAOH-01");
        var khp = await AddEntryAsync(db, section.Id, "KHP-P", category: MaterialMasterCategory.PrimaryStandard);
        var reagent = await AddEntryAsync(db, section.Id, "KHP-R", category: MaterialMasterCategory.Reagent);
        var service = TestServiceFactory.SolutionMaster(db);
        var created = await service.CreateAsync(TitrantReq(khp.Id, components: OneComponent(titrantComponent.Id)), userId);

        var req = TitrantReq(reagent.Id, components: OneComponent(titrantComponent.Id)) with { Reason = "Switch standard" };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, req, userId));
        Assert.Contains("must be a Primary Standard entry", ex.Message);
    }

    [Fact]
    public async Task Create_Titrant_AgainstVs_ReferenceMustBeActiveTitrant()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var titrantComponent = await AddEntryAsync(db, section.Id, "NAOH-01");
        var diluentEntry = await AddEntryAsync(db, section.Id, "WATER-01");
        var service = TestServiceFactory.SolutionMaster(db);

        var diluent = await service.CreateAsync(Req(name: "Diluent Water", type: SolutionType.Diluent, components: OneComponent(diluentEntry.Id)), userId);

        var req = TitrantReq(0, components: OneComponent(titrantComponent.Id), referenceSolutionId: diluent.Id, mode: StandardizationMode.AgainstVolumetricSolution);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("active titrant", ex.Message);
    }

    [Fact]
    public async Task Create_Titrant_FactorMinAboveMax_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var titrantComponent = await AddEntryAsync(db, section.Id, "NAOH-01");
        var standard = await AddEntryAsync(db, section.Id, "KHP-01", category: MaterialMasterCategory.PrimaryStandard);
        var service = TestServiceFactory.SolutionMaster(db);

        var req = TitrantReq(standard.Id, components: OneComponent(titrantComponent.Id)) with { FactorMin = 1.10m, FactorMax = 0.90m };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("Factor min and max", ex.Message);
    }

    [Fact]
    public async Task Create_Titrant_ReplicateCountBelowOne_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var titrantComponent = await AddEntryAsync(db, section.Id, "NAOH-01");
        var standard = await AddEntryAsync(db, section.Id, "KHP-01", category: MaterialMasterCategory.PrimaryStandard);
        var service = TestServiceFactory.SolutionMaster(db);

        var req = TitrantReq(standard.Id, components: OneComponent(titrantComponent.Id)) with { ReplicateCount = 0 };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(req, userId));
        Assert.Contains("Replicate count", ex.Message);
    }

    [Fact]
    public async Task Update_WithoutReason_Throws_AndNothingSaved()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "BUF-01");
        var service = TestServiceFactory.SolutionMaster(db);

        var created = await service.CreateAsync(Req(components: OneComponent(entry.Id)), userId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(created.Id, Req(name: "Renamed", components: OneComponent(entry.Id)), userId));
        Assert.Contains("reason", ex.Message, StringComparison.OrdinalIgnoreCase);

        var reloaded = await service.GetByIdAsync(created.Id, userId);
        Assert.Equal("Mobile Phase A", reloaded.Name);
    }

    [Fact]
    public async Task Update_ReplacesComponents_AndRecordsReasonEvent()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var buffer = await AddEntryAsync(db, section.Id, "BUF-01");
        var acn = await AddEntryAsync(db, section.Id, "ACN-01");
        var service = TestServiceFactory.SolutionMaster(db);

        var created = await service.CreateAsync(Req(components: OneComponent(buffer.Id)), userId);

        var newComponents = new List<SolutionComponentInput> { new(acn.Id, 250, SolutionComponentUnit.Milliliter) };
        var updated = await service.UpdateAsync(created.Id, Req(name: "Mobile Phase A Revised", components: newComponents, reason: "Recipe correction"), userId);

        Assert.Single(updated.Components);
        Assert.Equal(acn.Id, updated.Components[0].MaterialMasterEntryId);
        Assert.Equal("Mobile Phase A Revised", updated.Name);

        var auditLog = await db.AuditLogs.FirstOrDefaultAsync(a => a.ActionCode == "SolutionMaster.Updated" && a.EntityId == created.Id.ToString());
        Assert.NotNull(auditLog);
        Assert.Equal("Recipe correction", auditLog!.Reason);
    }

    [Fact]
    public async Task Update_KeepsComponentWhoseEntryWasDeactivated()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var buffer = await AddEntryAsync(db, section.Id, "BUF-01");
        var service = TestServiceFactory.SolutionMaster(db);

        var created = await service.CreateAsync(Req(components: OneComponent(buffer.Id)), userId);

        buffer.IsActive = false;
        await db.SaveChangesAsync();

        var updated = await service.UpdateAsync(
            created.Id,
            Req(name: "Mobile Phase A", components: OneComponent(buffer.Id, 600), reason: "Volume tweak"),
            userId);

        Assert.Single(updated.Components);
        Assert.Equal(buffer.Id, updated.Components[0].MaterialMasterEntryId);
        Assert.Equal(600, updated.Components[0].Quantity);
    }

    [Fact]
    public async Task Update_Titrant_ReferenceToItself_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var titrantComponent = await AddEntryAsync(db, section.Id, "NAOH-01");
        var standard = await AddEntryAsync(db, section.Id, "KHP-01", category: MaterialMasterCategory.PrimaryStandard);
        var service = TestServiceFactory.SolutionMaster(db);

        var created = await service.CreateAsync(TitrantReq(standard.Id, components: OneComponent(titrantComponent.Id)), userId);

        var req = TitrantReq(0, components: OneComponent(titrantComponent.Id), referenceSolutionId: created.Id, mode: StandardizationMode.AgainstVolumetricSolution)
            with { Reason = "Self reference attempt" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, req, userId));
        Assert.Contains("cannot reference itself", ex.Message);
    }

    [Fact]
    public async Task SetActive_RequiresReason()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "BUF-01");
        var service = TestServiceFactory.SolutionMaster(db);

        var created = await service.CreateAsync(Req(components: OneComponent(entry.Id)), userId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SetActiveAsync(created.Id, false, "", userId));
        Assert.Contains("reason", ex.Message, StringComparison.OrdinalIgnoreCase);

        var deactivated = await service.SetActiveAsync(created.Id, false, "No longer used", userId);
        Assert.False(deactivated.IsActive);
    }

    // --- Microbiology general types: flags, prepared-solution and text components ---

    private static SaveSolutionMasterRequest GeneralReq(
        string name, SolutionType type, List<SolutionComponentInput> components, int? sectionId = null,
        bool sterilization = false, bool release = false) =>
        Req(name: name, type: type, components: components, sectionId: sectionId) with
        {
            SterilizationRequired = sterilization,
            ReleaseRequired = release
        };

    [Theory]
    [InlineData(SolutionType.Diluent)]
    [InlineData(SolutionType.Reagent)]
    [InlineData(SolutionType.Neutralizer)]
    public async Task Create_GeneralType_SavesFlags_AndUpdateAuditsFlagChange(SolutionType type)
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "NACL");
        var service = TestServiceFactory.SolutionMaster(db);

        var created = await service.CreateAsync(GeneralReq("Saline", type, OneComponent(entry.Id), sterilization: true, release: true), userId);

        Assert.Equal(type, created.Type);
        Assert.True(created.SterilizationRequired);
        Assert.True(created.ReleaseRequired);

        var updated = await service.UpdateAsync(created.Id,
            GeneralReq("Saline", type, OneComponent(entry.Id), sterilization: true, release: false) with { Reason = "No release needed" }, userId);
        Assert.True(updated.SterilizationRequired);
        Assert.False(updated.ReleaseRequired);
    }

    [Fact]
    public async Task Create_FlagsDefaultOff_ForExistingStyleRecipe()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "NACL");
        var service = TestServiceFactory.SolutionMaster(db);

        var created = await service.CreateAsync(Req(type: SolutionType.Diluent, components: OneComponent(entry.Id)), userId);

        Assert.False(created.SterilizationRequired);
        Assert.False(created.ReleaseRequired);
        Assert.Equal(SolutionComponentKind.Material, created.Components[0].Kind);
    }

    [Theory]
    [InlineData(SolutionType.MobilePhase, true, false)]
    [InlineData(SolutionType.MobilePhase, false, true)]
    [InlineData(SolutionType.Titrant, true, false)]
    [InlineData(SolutionType.Titrant, false, true)]
    public async Task Create_HplcTypeWithMicrobiologyFlags_Throws(SolutionType type, bool sterilization, bool release)
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "BUF-01");
        var std = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.PrimaryStandard);
        var service = TestServiceFactory.SolutionMaster(db);

        var request = type == SolutionType.Titrant
            ? TitrantReq(std.Id, components: OneComponent(entry.Id)) with { SterilizationRequired = sterilization, ReleaseRequired = release }
            : GeneralReq("Mobile Phase A", type, OneComponent(entry.Id), sterilization: sterilization, release: release);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request, userId));
        Assert.Contains("only allowed for diluents, reagents and neutralizers", ex.Message);
    }

    [Fact]
    public async Task Create_ReagentWithTitrantFields_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "NACL");
        var service = TestServiceFactory.SolutionMaster(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            GeneralReq("Reagent X", SolutionType.Reagent, OneComponent(entry.Id)) with { NominalStrength = 0.1m }, userId));
        Assert.Contains("only allowed for titrants", ex.Message);
    }

    private static async Task<SolutionMasterResponse> CreateGeneralAsync(
        SolutionMasterService service, int userId, string name, int entryId, SolutionType type = SolutionType.Reagent) =>
        await service.CreateAsync(GeneralReq(name, type, OneComponent(entryId)), userId);

    [Fact]
    public async Task Create_PreparedSolutionComponent_ResolvesSourceAndKeepsText()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "PO4");
        var service = TestServiceFactory.SolutionMaster(db);
        var stock = await CreateGeneralAsync(service, userId, "Phosphate buffer stock", entry.Id);

        var created = await service.CreateAsync(GeneralReq("Phosphate buffer solution", SolutionType.Diluent, new List<SolutionComponentInput>
        {
            new(null, 1.25m, SolutionComponentUnit.Milliliter, SolutionComponentKind.PreparedSolution, SourceSolutionMasterId: stock.Id),
            new(null, null, null, SolutionComponentKind.Text, Text: "  Purified water to 1000 mL  ")
        }, sterilization: true, release: true), userId);

        Assert.Equal(SolutionComponentKind.PreparedSolution, created.Components[0].Kind);
        Assert.Equal(stock.Id, created.Components[0].SourceSolutionMasterId);
        Assert.Equal("Phosphate buffer stock", created.Components[0].SourceSolutionName);
        Assert.Null(created.Components[0].MaterialMasterEntryId);
        Assert.Equal(SolutionComponentKind.Text, created.Components[1].Kind);
        Assert.Equal("Purified water to 1000 mL", created.Components[1].Text);
        Assert.Null(created.Components[1].Quantity);
        Assert.Null(created.Components[1].Unit);
    }

    [Fact]
    public async Task Create_PreparedSolutionComponent_OtherSection_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "PO4");
        var service = TestServiceFactory.SolutionMaster(db);
        var otherSection = new DocumentSection { Name = "Other Lab", Code = "OTH", DepartmentId = section.DepartmentId, IsActive = true };
        db.DocumentSections.Add(otherSection);
        await db.SaveChangesAsync();
        db.SolutionMasters.Add(new SolutionMaster
        {
            SectionId = otherSection.Id, Name = "Foreign stock", Type = SolutionType.Reagent, ShelfLifeValue = 7,
            ShelfLifeUnit = ShelfLifeUnit.Days, StorageCondition = "2-8 C", FinalVolumeMl = 100, Instructions = "x", IsActive = true,
            CreatedByUserId = 1, LastModifiedByUserId = 1
        });
        await db.SaveChangesAsync();
        var foreignId = (await db.SolutionMasters.SingleAsync(s => s.Name == "Foreign stock")).Id;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            GeneralReq("Buffer", SolutionType.Diluent, new List<SolutionComponentInput>
            {
                new(entry.Id, 1m, SolutionComponentUnit.Gram),
                new(null, 1m, SolutionComponentUnit.Milliliter, SolutionComponentKind.PreparedSolution, SourceSolutionMasterId: foreignId)
            }), userId));
        Assert.Contains("another laboratory", ex.Message);
    }

    [Fact]
    public async Task Create_PreparedSolutionComponent_InactiveSource_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "PO4");
        var service = TestServiceFactory.SolutionMaster(db);
        var stock = await CreateGeneralAsync(service, userId, "Old stock", entry.Id);
        await service.SetActiveAsync(stock.Id, false, "Retired", userId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            GeneralReq("Buffer", SolutionType.Diluent, new List<SolutionComponentInput>
            {
                new(null, 1m, SolutionComponentUnit.Milliliter, SolutionComponentKind.PreparedSolution, SourceSolutionMasterId: stock.Id)
            }), userId));
        Assert.Contains("inactive", ex.Message);
    }

    [Fact]
    public async Task Update_PreparedSolutionComponent_SelfReference_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "PO4");
        var service = TestServiceFactory.SolutionMaster(db);
        var a = await CreateGeneralAsync(service, userId, "A", entry.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(a.Id,
            GeneralReq("A", SolutionType.Reagent, new List<SolutionComponentInput>
            {
                new(null, 1m, SolutionComponentUnit.Milliliter, SolutionComponentKind.PreparedSolution, SourceSolutionMasterId: a.Id)
            }) with { Reason = "Loop" }, userId));
        Assert.Contains("itself", ex.Message);
    }

    [Fact]
    public async Task Update_PreparedSolutionComponent_IndirectCycle_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "PO4");
        var service = TestServiceFactory.SolutionMaster(db);
        var a = await CreateGeneralAsync(service, userId, "A", entry.Id);
        var b = await service.CreateAsync(GeneralReq("B", SolutionType.Reagent, new List<SolutionComponentInput>
        {
            new(null, 1m, SolutionComponentUnit.Milliliter, SolutionComponentKind.PreparedSolution, SourceSolutionMasterId: a.Id)
        }), userId);
        var c = await service.CreateAsync(GeneralReq("C", SolutionType.Reagent, new List<SolutionComponentInput>
        {
            new(null, 1m, SolutionComponentUnit.Milliliter, SolutionComponentKind.PreparedSolution, SourceSolutionMasterId: b.Id)
        }), userId);

        // A -> C would close the loop A -> C -> B -> A.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(a.Id,
            GeneralReq("A", SolutionType.Reagent, new List<SolutionComponentInput>
            {
                new(null, 1m, SolutionComponentUnit.Milliliter, SolutionComponentKind.PreparedSolution, SourceSolutionMasterId: c.Id)
            }) with { Reason = "Loop" }, userId));
        Assert.Contains("circular", ex.Message);
    }

    [Fact]
    public async Task Create_PreparedSolutionComponent_Duplicate_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "PO4");
        var service = TestServiceFactory.SolutionMaster(db);
        var stock = await CreateGeneralAsync(service, userId, "Stock", entry.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            GeneralReq("Buffer", SolutionType.Diluent, new List<SolutionComponentInput>
            {
                new(null, 1m, SolutionComponentUnit.Milliliter, SolutionComponentKind.PreparedSolution, SourceSolutionMasterId: stock.Id),
                new(null, 2m, SolutionComponentUnit.Milliliter, SolutionComponentKind.PreparedSolution, SourceSolutionMasterId: stock.Id)
            }), userId));
        Assert.Contains("twice", ex.Message);
    }

    [Fact]
    public async Task Create_PreparedSolutionComponent_OnMobilePhase_Throws()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "PO4");
        var service = TestServiceFactory.SolutionMaster(db);
        var stock = await CreateGeneralAsync(service, userId, "Stock", entry.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            GeneralReq("MP", SolutionType.MobilePhase, new List<SolutionComponentInput>
            {
                new(null, 1m, SolutionComponentUnit.Milliliter, SolutionComponentKind.PreparedSolution, SourceSolutionMasterId: stock.Id)
            }), userId));
        Assert.Contains("only allowed for diluents, reagents and neutralizers", ex.Message);
    }

    [Fact]
    public async Task Create_TextComponent_Validation()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var entry = await AddEntryAsync(db, section.Id, "NACL");
        var service = TestServiceFactory.SolutionMaster(db);
        var material = new SolutionComponentInput(entry.Id, 9m, SolutionComponentUnit.Gram);

        // Text is required.
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(GeneralReq("S1", SolutionType.Diluent,
            new() { material, new(null, null, null, SolutionComponentKind.Text, Text: "  ") }), userId));
        // No quantity or unit on a text line.
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(GeneralReq("S2", SolutionType.Diluent,
            new() { material, new(null, 1m, null, SolutionComponentKind.Text, Text: "Water") }), userId));
        // No entry on a text line.
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(GeneralReq("S3", SolutionType.Diluent,
            new() { material, new(entry.Id, null, null, SolutionComponentKind.Text, Text: "Water") }), userId));
        // At most 200 characters.
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(GeneralReq("S4", SolutionType.Diluent,
            new() { material, new(null, null, null, SolutionComponentKind.Text, Text: new string('x', 201)) }), userId));
        // A recipe needs at least one non-text component.
        var onlyText = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(GeneralReq("S5", SolutionType.Diluent,
            new() { new(null, null, null, SolutionComponentKind.Text, Text: "Purified water") }), userId));
        Assert.Contains("At least one component", onlyText.Message);
        // A material line still needs a positive quantity.
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(GeneralReq("S6", SolutionType.Diluent,
            new() { new(entry.Id, null, null) }), userId));
    }
}
