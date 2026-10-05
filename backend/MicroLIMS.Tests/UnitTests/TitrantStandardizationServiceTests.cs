using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class TitrantStandardizationServiceTests
{
    private const string Password = "ValidPassword123!";

    public class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public void SetUtcNow(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        db.CurrentUserId = 1;
        return db;
    }

    private static readonly DateTimeOffset SepFirst = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private static (FakeTimeProvider TimeProvider, ILabClock Clock) NewClock(DateTimeOffset utcNow)
    {
        var tz = LabClock.ResolveTimeZone("Africa/Cairo");
        var fake = new FakeTimeProvider(utcNow);
        return (fake, new LabClock(fake, tz));
    }

    private static async Task<(DocumentSection section, int userId)> SeedAsync(MicroLimsDbContext db)
    {
        var section = TestServiceFactory.EnsureMicroSection(db);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User
        {
            Username = "u_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Test Analyst",
            RoleId = role.Id,
            IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        TestServiceFactory.AssignUserToMicroSection(db, user.Id);
        return (section, user.Id);
    }

    private static async Task<MaterialMasterEntry> AddEntryAsync(
        MicroLimsDbContext db, int sectionId, string code,
        MaterialMasterCategory category = MaterialMasterCategory.Reagent)
    {
        var entry = new MaterialMasterEntry
        {
            SectionId = sectionId,
            Code = code,
            Name = code,
            Category = category,
            BaseUnit = MaterialUnit.Milliliter,
            IsActive = true,
            CreatedByUserId = 1,
            CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = 1,
            LastModifiedAt = DateTime.UtcNow,
        };
        db.MaterialMasterEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    private static async Task<Material> AddSolventLotAsync(
        MicroLimsDbContext db, int sectionId, MaterialMasterEntry entry, decimal quantityRemaining = 100000m, string batch = "SOLV-LOT-01")
    {
        var lot = new Material
        {
            SectionId = sectionId,
            MaterialType = MaterialType.Chemical,
            MaterialMasterEntryId = entry.Id,
            MaterialName = entry.Name,
            ManufacturerName = "Acme",
            BatchNumber = batch,
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Location = "Shelf 1",
            QuantityReceived = quantityRemaining,
            QuantityRemaining = quantityRemaining,
            Unit = MaterialUnit.Milliliter,
            CreatedByUserId = 1,
            LastModifiedByUserId = 1,
        };
        db.Materials.Add(lot);
        await db.SaveChangesAsync();
        return lot;
    }

    private static async Task<Material> AddStandardLotAsync(
        MicroLimsDbContext db, int sectionId, MaterialMasterEntry entry, decimal? purity = null,
        decimal quantityRemaining = 100m, DateTime? expiryDate = null, string batch = "STD-LOT-01")
    {
        var lot = new Material
        {
            SectionId = sectionId,
            MaterialType = MaterialType.PrimaryStandard,
            MaterialMasterEntryId = entry.Id,
            MaterialName = entry.Name,
            ManufacturerName = "Acme",
            BatchNumber = batch,
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = expiryDate ?? DateTime.UtcNow.AddYears(1),
            Location = "Shelf 2",
            QuantityReceived = quantityRemaining,
            QuantityRemaining = quantityRemaining,
            Unit = MaterialUnit.Gram,
            Purity = purity,
            CreatedByUserId = 1,
            LastModifiedByUserId = 1,
        };
        db.Materials.Add(lot);
        await db.SaveChangesAsync();
        return lot;
    }

    private static async Task<SolutionMaster> AddPrimaryStandardTitrantAsync(
        MicroLimsDbContext db, int sectionId, string name, MaterialMasterEntry solventEntry, MaterialMasterEntry standardEntry,
        int replicateCount = 1, decimal factorMin = 0.5m, decimal factorMax = 1.5m, decimal maxRsd = 5.0m,
        int validityDays = 30, bool blankRequired = false, decimal nominalStrength = 0.1m)
    {
        var service = TestServiceFactory.SolutionMaster(db);
        var created = await service.CreateAsync(new SaveSolutionMasterRequest(
            name, SolutionType.Titrant, 7, ShelfLifeUnit.Days, "Room temperature", 1000m, "Standardize per SOP.",
            new List<SolutionComponentInput> { new(solventEntry.Id, 500m, SolutionComponentUnit.Milliliter) },
            SectionId: sectionId,
            NominalStrength: nominalStrength, StrengthUnit: TitrantStrengthUnit.Normal, StandardizationMode: StandardizationMode.PrimaryStandard,
            StandardEntryId: standardEntry.Id, EquivalenceMgPerMl: 20.42m,
            BlankRequired: blankRequired, ReplicateCount: replicateCount, FactorMin: factorMin, FactorMax: factorMax,
            MaxRsdPercent: maxRsd, ValidityDays: validityDays), 1);

        return await db.SolutionMasters.FirstAsync(s => s.Id == created.Id);
    }

    private static async Task<SolutionMaster> AddAgainstVsTitrantAsync(
        MicroLimsDbContext db, int sectionId, string name, MaterialMasterEntry solventEntry, int referenceSolutionId,
        int replicateCount = 1, decimal factorMin = 0.5m, decimal factorMax = 1.5m, decimal maxRsd = 5.0m,
        int validityDays = 30, bool blankRequired = false, decimal nominalStrength = 0.1m)
    {
        var service = TestServiceFactory.SolutionMaster(db);
        var created = await service.CreateAsync(new SaveSolutionMasterRequest(
            name, SolutionType.Titrant, 7, ShelfLifeUnit.Days, "Room temperature", 1000m, "Standardize per SOP.",
            new List<SolutionComponentInput> { new(solventEntry.Id, 500m, SolutionComponentUnit.Milliliter) },
            SectionId: sectionId,
            NominalStrength: nominalStrength, StrengthUnit: TitrantStrengthUnit.Normal, StandardizationMode: StandardizationMode.AgainstVolumetricSolution,
            ReferenceSolutionId: referenceSolutionId,
            BlankRequired: blankRequired, ReplicateCount: replicateCount, FactorMin: factorMin, FactorMax: factorMax,
            MaxRsdPercent: maxRsd, ValidityDays: validityDays), 1);

        return await db.SolutionMasters.FirstAsync(s => s.Id == created.Id);
    }

    private static async Task<MicroLIMS.Application.DTOs.Responses.SolutionPreparationResponse> PrepareTitrantAsync(
        MicroLimsDbContext db, DocumentSection section, int userId, SolutionMaster titrant, MaterialMasterEntry solventEntry, ILabClock? clock = null)
    {
        var prepService = TestServiceFactory.SolutionPreparation(db, clock: clock);
        var started = await prepService.StartAsync(new StartPreparationRequest(titrant.Id, null), userId);
        var lot = await db.Materials.AsNoTracking().FirstAsync(m => m.MaterialMasterEntryId == solventEntry.Id);
        await prepService.SaveAsync(started.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(started.Components[0].Id, lot.Id, 5m) }, 1000m, null),
            userId);
        return await prepService.CompleteAsync(started.Id, new CompletePreparationRequest(Password, null), userId, "127.0.0.1");
    }

    private sealed class Scenario
    {
        public required DocumentSection Section;
        public required int UserId;
        public required MaterialMasterEntry SolventEntry;
        public required MaterialMasterEntry StandardEntry;
    }

    private static async Task<Scenario> SeedScenarioAsync(MicroLimsDbContext db)
    {
        var (section, userId) = await SeedAsync(db);
        var solventEntry = await AddEntryAsync(db, section.Id, "SOLV-01");
        var standardEntry = await AddEntryAsync(db, section.Id, "STD-KHP", MaterialMasterCategory.PrimaryStandard);
        await AddSolventLotAsync(db, section.Id, solventEntry);
        return new Scenario { Section = section, UserId = userId, SolventEntry = solventEntry, StandardEntry = standardEntry };
    }

    // --- Happy paths ---

    [Fact]
    public async Task Standardize_PrimaryStandard_ThreeReplicates_MeanAndRsdStored()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry, purity: 100m, quantityRemaining: 10);
        var titrant = await AddPrimaryStandardTitrantAsync(db, s.Section.Id, "0.1N NaOH", s.SolventEntry, s.StandardEntry,
            replicateCount: 3, factorMin: 0.95m, factorMax: 1.05m, maxRsd: 0.5m);
        var prep = await PrepareTitrantAsync(db, s.Section, s.UserId, titrant, s.SolventEntry);
        var lot = await db.Materials.AsNoTracking().FirstAsync(m => m.MaterialMasterEntryId == s.StandardEntry.Id);
        var service = TestServiceFactory.TitrantStandardization(db);

        // Weight/volume chosen so factor = weight / (volume x 20.42) gives 0.998, 1.000, 1.002.
        var replicates = new List<StandardizationReplicateInput>
        {
            new(lot.Id, 203.7916m, null, null, 10m, null),
            new(lot.Id, 204.2000m, null, null, 10m, null),
            new(lot.Id, 204.6084m, null, null, 10m, null),
        };

        var result = await service.StandardizeAsync(prep.Id, new StandardizeRequest(replicates, Password, "S5 test"), s.UserId, "127.0.0.1");

        Assert.True(result.Passed);
        Assert.Equal(1.0000m, decimal.Round(result.MeanFactor, 4));
        Assert.NotNull(result.RsdPercent);
        Assert.Equal(0.20m, decimal.Round(result.RsdPercent!.Value, 2));
        Assert.Equal(3, result.Replicates.Count);
        Assert.Equal(StandardizationMode.PrimaryStandard, result.Mode);
    }

    [Fact]
    public async Task Standardize_AgainstVolumetricSolution_ComputesFactorFromReferenceCurrent()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry, purity: 100m, quantityRemaining: 10);
        var refTitrant = await AddPrimaryStandardTitrantAsync(db, s.Section.Id, "0.1N NaOH (ref)", s.SolventEntry, s.StandardEntry,
            replicateCount: 1, factorMin: 0.5m, factorMax: 1.5m, nominalStrength: 0.1m);
        var refPrep = await PrepareTitrantAsync(db, s.Section, s.UserId, refTitrant, s.SolventEntry);
        var refLot = await db.Materials.AsNoTracking().FirstAsync(m => m.MaterialMasterEntryId == s.StandardEntry.Id);
        var service = TestServiceFactory.TitrantStandardization(db);

        // weight / (10 x 20.42) = 1.002
        await service.StandardizeAsync(refPrep.Id,
            new StandardizeRequest(new List<StandardizationReplicateInput> { new(refLot.Id, 204.6084m, null, null, 10m, null) }, Password, null),
            s.UserId, null);

        var titrant = await AddAgainstVsTitrantAsync(db, s.Section.Id, "0.1N HCl", s.SolventEntry, refTitrant.Id,
            replicateCount: 1, factorMin: 0.5m, factorMax: 1.5m, nominalStrength: 0.1m);
        var prep = await PrepareTitrantAsync(db, s.Section, s.UserId, titrant, s.SolventEntry);

        var result = await service.StandardizeAsync(prep.Id,
            new StandardizeRequest(new List<StandardizationReplicateInput> { new(null, null, refPrep.Id, 25.00m, 24.90m, null) }, Password, null),
            s.UserId, null);

        Assert.True(result.Passed);
        Assert.Equal(1.0060241m, decimal.Round(result.MeanFactor, 7));
        Assert.Equal(1.002m, result.Replicates[0].ReferenceFactor);
        Assert.Equal(StandardizationMode.AgainstVolumetricSolution, result.Mode);
    }

    // --- Review Focus ---

    [Fact]
    public async Task Standardize_TitrantVolumeAtOrBelowBlank_Refused()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry, purity: 100m, quantityRemaining: 10);
        var titrant = await AddPrimaryStandardTitrantAsync(db, s.Section.Id, "0.1N NaOH", s.SolventEntry, s.StandardEntry,
            replicateCount: 1, blankRequired: true);
        var prep = await PrepareTitrantAsync(db, s.Section, s.UserId, titrant, s.SolventEntry);
        var lot = await db.Materials.AsNoTracking().FirstAsync(m => m.MaterialMasterEntryId == s.StandardEntry.Id);
        var service = TestServiceFactory.TitrantStandardization(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StandardizeAsync(prep.Id,
            new StandardizeRequest(new List<StandardizationReplicateInput> { new(lot.Id, 200m, null, null, 0.05m, 0.05m) }, Password, null),
            s.UserId, null));

        Assert.Contains("Replicate 1", ex.Message);
        Assert.Contains("greater than the blank", ex.Message);
    }

    [Fact]
    public async Task Standardize_AgainstReferenceWithNoStandardization_Refused()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var refTitrant = await AddPrimaryStandardTitrantAsync(db, s.Section.Id, "0.1N NaOH (ref)", s.SolventEntry, s.StandardEntry);
        var refPrep = await PrepareTitrantAsync(db, s.Section, s.UserId, refTitrant, s.SolventEntry);

        var titrant = await AddAgainstVsTitrantAsync(db, s.Section.Id, "0.1N HCl", s.SolventEntry, refTitrant.Id);
        var prep = await PrepareTitrantAsync(db, s.Section, s.UserId, titrant, s.SolventEntry);
        var service = TestServiceFactory.TitrantStandardization(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StandardizeAsync(prep.Id,
            new StandardizeRequest(new List<StandardizationReplicateInput> { new(null, null, refPrep.Id, 25.00m, 24.90m, null) }, Password, null),
            s.UserId, null));

        Assert.Contains("Replicate 1", ex.Message);
        Assert.Contains("expired, failed or missing", ex.Message);
    }

    [Fact]
    public async Task Standardize_ReplicateCountMismatch_Refused()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry, purity: 100m, quantityRemaining: 10);
        var titrant = await AddPrimaryStandardTitrantAsync(db, s.Section.Id, "0.1N NaOH", s.SolventEntry, s.StandardEntry, replicateCount: 2);
        var prep = await PrepareTitrantAsync(db, s.Section, s.UserId, titrant, s.SolventEntry);
        var lot = await db.Materials.AsNoTracking().FirstAsync(m => m.MaterialMasterEntryId == s.StandardEntry.Id);
        var service = TestServiceFactory.TitrantStandardization(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StandardizeAsync(prep.Id,
            new StandardizeRequest(new List<StandardizationReplicateInput> { new(lot.Id, 204.2m, null, null, 10m, null) }, Password, null),
            s.UserId, null));

        Assert.Contains("Exactly 2 replicate", ex.Message);
    }

    [Fact]
    public async Task Standardize_FailedRun_DoesNotReplaceThePreviousPassedCurrentFactor()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry, purity: 100m, quantityRemaining: 10);
        var titrant = await AddPrimaryStandardTitrantAsync(db, s.Section.Id, "0.1N NaOH", s.SolventEntry, s.StandardEntry,
            replicateCount: 1, factorMin: 0.95m, factorMax: 1.05m, validityDays: 30);
        var prep = await PrepareTitrantAsync(db, s.Section, s.UserId, titrant, s.SolventEntry);
        var lot = await db.Materials.AsNoTracking().FirstAsync(m => m.MaterialMasterEntryId == s.StandardEntry.Id);
        var service = TestServiceFactory.TitrantStandardization(db);

        var passed = await service.StandardizeAsync(prep.Id,
            new StandardizeRequest(new List<StandardizationReplicateInput> { new(lot.Id, 204.2m, null, null, 10m, null) }, Password, null),
            s.UserId, null);
        Assert.True(passed.Passed);

        // Way outside [0.95, 1.05].
        var failed = await service.StandardizeAsync(prep.Id,
            new StandardizeRequest(new List<StandardizationReplicateInput> { new(lot.Id, 50m, null, null, 10m, null) }, Password, null),
            s.UserId, null);
        Assert.False(failed.Passed);

        var current = await service.GetCurrentFactorAsync(prep.Id);
        Assert.Equal("Valid", current.State);
        Assert.Equal(passed.MeanFactor, current.Factor);
    }

    [Fact]
    public async Task Standardize_ExpiredPreparation_Refused()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry, purity: 100m, quantityRemaining: 10);
        var titrant = await AddPrimaryStandardTitrantAsync(db, s.Section.Id, "0.1N NaOH", s.SolventEntry, s.StandardEntry, replicateCount: 1);
        var (fake, clock) = NewClock(SepFirst);
        var prep = await PrepareTitrantAsync(db, s.Section, s.UserId, titrant, s.SolventEntry, clock);
        var lot = await db.Materials.AsNoTracking().FirstAsync(m => m.MaterialMasterEntryId == s.StandardEntry.Id);
        var service = TestServiceFactory.TitrantStandardization(db, clock: clock);

        // Titrant's shelf life is 7 days - jump well past it.
        fake.SetUtcNow(SepFirst.AddDays(10));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StandardizeAsync(prep.Id,
            new StandardizeRequest(new List<StandardizationReplicateInput> { new(lot.Id, 204.2m, null, null, 10m, null) }, Password, null),
            s.UserId, null));

        Assert.Contains("expired or discarded", ex.Message);
    }

    // --- Current factor states ---

    [Fact]
    public async Task GetCurrentFactor_BeforeAnyStandardization_IsNotStandardized()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var titrant = await AddPrimaryStandardTitrantAsync(db, s.Section.Id, "0.1N NaOH", s.SolventEntry, s.StandardEntry);
        var prep = await PrepareTitrantAsync(db, s.Section, s.UserId, titrant, s.SolventEntry);
        var service = TestServiceFactory.TitrantStandardization(db);

        var current = await service.GetCurrentFactorAsync(prep.Id);

        Assert.Equal("NotStandardized", current.State);
        Assert.Null(current.Factor);
    }

    [Fact]
    public async Task GetCurrentFactor_ValidThenDue_AsTimePasses()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry, purity: 100m, quantityRemaining: 10);
        var titrant = await AddPrimaryStandardTitrantAsync(db, s.Section.Id, "0.1N NaOH", s.SolventEntry, s.StandardEntry,
            replicateCount: 1, validityDays: 1);
        var (fake, clock) = NewClock(SepFirst);
        var prep = await PrepareTitrantAsync(db, s.Section, s.UserId, titrant, s.SolventEntry, clock);
        var lot = await db.Materials.AsNoTracking().FirstAsync(m => m.MaterialMasterEntryId == s.StandardEntry.Id);
        var service = TestServiceFactory.TitrantStandardization(db, clock: clock);

        await service.StandardizeAsync(prep.Id,
            new StandardizeRequest(new List<StandardizationReplicateInput> { new(lot.Id, 204.2m, null, null, 10m, null) }, Password, null),
            s.UserId, null);

        var valid = await service.GetCurrentFactorAsync(prep.Id);
        Assert.Equal("Valid", valid.State);

        fake.SetUtcNow(SepFirst.AddDays(2));
        var due = await service.GetCurrentFactorAsync(prep.Id);
        Assert.Equal("Due", due.State);
    }

    [Fact]
    public async Task GetCurrentFactor_ZeroValidityDays_IsBeforeEachUse()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry, purity: 100m, quantityRemaining: 10);
        var titrant = await AddPrimaryStandardTitrantAsync(db, s.Section.Id, "0.1N NaOH", s.SolventEntry, s.StandardEntry,
            replicateCount: 1, validityDays: 0);
        var prep = await PrepareTitrantAsync(db, s.Section, s.UserId, titrant, s.SolventEntry);
        var lot = await db.Materials.AsNoTracking().FirstAsync(m => m.MaterialMasterEntryId == s.StandardEntry.Id);
        var service = TestServiceFactory.TitrantStandardization(db);

        await service.StandardizeAsync(prep.Id,
            new StandardizeRequest(new List<StandardizationReplicateInput> { new(lot.Id, 204.2m, null, null, 10m, null) }, Password, null),
            s.UserId, null);

        var current = await service.GetCurrentFactorAsync(prep.Id);

        Assert.Equal("BeforeEachUse", current.State);
        Assert.Null(current.ValidUntil);
    }

    // --- Primary standard lot rules (physchem areas, B1.3) ---

    private static async Task<(TitrantStandardizationService Service, int PrepId, int LotId, int UserId)> ArrangePrimaryLotAsync(
        MicroLimsDbContext db, Action<Material> tweak)
    {
        var s = await SeedScenarioAsync(db);
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry, purity: 99.95m, quantityRemaining: 10, batch: "B1");
        tweak(lot);
        await db.SaveChangesAsync();
        var titrant = await AddPrimaryStandardTitrantAsync(db, s.Section.Id, "0.1N NaOH", s.SolventEntry, s.StandardEntry, replicateCount: 1);
        var prep = await PrepareTitrantAsync(db, s.Section, s.UserId, titrant, s.SolventEntry);
        return (TestServiceFactory.TitrantStandardization(db), prep.Id, lot.Id, s.UserId);
    }

    [Fact]
    public async Task Standardize_PrimaryStandardLot_WithoutPurity_IsRefused()
    {
        await using var db = NewDb();
        var (service, prepId, lotId, userId) = await ArrangePrimaryLotAsync(db, l => l.Purity = null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StandardizeAsync(prepId,
            new StandardizeRequest(new List<StandardizationReplicateInput> { new(lotId, 510.6m, null, null, 25.00m, null) }, Password, null), userId, null));
        Assert.Equal("Replicate 1: standard lot (batch B1) has no purity - enter it in Materials Stock before standardizing.", ex.Message);
    }

    [Fact]
    public async Task Standardize_ChemicalLot_IsRefused()
    {
        await using var db = NewDb();
        var (service, prepId, lotId, userId) = await ArrangePrimaryLotAsync(db, l => l.MaterialType = MaterialType.Chemical);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StandardizeAsync(prepId,
            new StandardizeRequest(new List<StandardizationReplicateInput> { new(lotId, 510.6m, null, null, 25.00m, null) }, Password, null), userId, null));
        Assert.Equal("Replicate 1: standard lot (batch B1) is not a primary standard.", ex.Message);
    }

    [Fact]
    public async Task Standardize_UsesLotPurity()
    {
        await using var db = NewDb();
        var (service, prepId, lotId, userId) = await ArrangePrimaryLotAsync(db, _ => { });

        var result = await service.StandardizeAsync(prepId,
            new StandardizeRequest(new List<StandardizationReplicateInput> { new(lotId, 510.6m, null, null, 25.00m, null) }, Password, null), userId, null);
        // 510.6 x 0.9995 / (25.00 x 20.42)
        Assert.Equal(decimal.Round(510.6m * 0.9995m / (25.00m * 20.42m), 6), decimal.Round(result.MeanFactor, 6));
    }

    [Fact]
    public async Task StandardLotOptions_ListOnlyPrimaryStandardLots()
    {
        await using var db = NewDb();
        var (service, prepId, _, userId) = await ArrangePrimaryLotAsync(db, _ => { });
        var entry = await db.MaterialMasterEntries.FirstAsync(e => e.Code == "STD-KHP");
        await AddStandardLotAsync(db, entry.SectionId, entry, purity: null, batch: "CHEM-LOT");
        var chem = await db.Materials.FirstAsync(m => m.BatchNumber == "CHEM-LOT");
        chem.MaterialType = MaterialType.Chemical;
        await db.SaveChangesAsync();

        var options = await service.GetStandardLotOptionsAsync(prepId, userId);
        var only = Assert.Single(options);
        Assert.Equal("B1", only.BatchNumber);
    }

    // --- Wiring into SolutionPreparationResponse ---

    [Fact]
    public async Task SolutionPreparationResponse_CarriesCurrentFactorForTitrants()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry, purity: 100m, quantityRemaining: 10);
        var titrant = await AddPrimaryStandardTitrantAsync(db, s.Section.Id, "0.1N NaOH", s.SolventEntry, s.StandardEntry, replicateCount: 1);
        var prep = await PrepareTitrantAsync(db, s.Section, s.UserId, titrant, s.SolventEntry);
        var lot = await db.Materials.AsNoTracking().FirstAsync(m => m.MaterialMasterEntryId == s.StandardEntry.Id);
        var standardizationService = TestServiceFactory.TitrantStandardization(db);

        await standardizationService.StandardizeAsync(prep.Id,
            new StandardizeRequest(new List<StandardizationReplicateInput> { new(lot.Id, 204.2m, null, null, 10m, null) }, Password, null),
            s.UserId, null);

        var prepService = TestServiceFactory.SolutionPreparation(db);
        var reloaded = await prepService.GetByIdAsync(prep.Id, s.UserId);

        Assert.NotNull(reloaded.CurrentFactor);
        Assert.Equal("Valid", reloaded.CurrentFactor!.State);
        Assert.Equal(1.0000m, decimal.Round(reloaded.CurrentFactor.Factor!.Value, 4));
    }

    // --- Stock deduction ---

    private static async Task<(TitrantStandardizationService Service, int PrepId, int LotId, int UserId)> ArrangeStockAsync(
        MicroLimsDbContext db, decimal lotQuantity)
    {
        var s = await SeedScenarioAsync(db);
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry, purity: 100m, quantityRemaining: lotQuantity);
        var titrant = await AddPrimaryStandardTitrantAsync(db, s.Section.Id, "0.1N NaOH", s.SolventEntry, s.StandardEntry,
            replicateCount: 3, factorMin: 0.5m, factorMax: 1.5m, maxRsd: 5m);
        var prep = await PrepareTitrantAsync(db, s.Section, s.UserId, titrant, s.SolventEntry);
        return (TestServiceFactory.TitrantStandardization(db), prep.Id, lot.Id, s.UserId);
    }

    private static StandardizeRequest ThreeReplicates(int lotId) => new(
        Enumerable.Range(0, 3).Select(_ => new StandardizationReplicateInput(lotId, 200m, null, null, 10m, null)).ToList(),
        Password, null);

    [Fact]
    public async Task Standardize_DeductsSummedWeightFromLot()
    {
        await using var db = NewDb();
        var (svc, prepId, lotId, uid) = await ArrangeStockAsync(db, 50m);
        await svc.StandardizeAsync(prepId, ThreeReplicates(lotId), uid, null);
        Assert.Equal(49.4m, (await db.Materials.AsNoTracking().FirstAsync(m => m.Id == lotId)).QuantityRemaining);
    }

    [Fact]
    public async Task Standardize_InsufficientStock_RefusedWithNoSignatureAndStockUnchanged()
    {
        await using var db = NewDb();
        var (svc, prepId, lotId, uid) = await ArrangeStockAsync(db, 0.5m);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.StandardizeAsync(prepId, ThreeReplicates(lotId), uid, null));
        Assert.Equal(0.5m, (await db.Materials.AsNoTracking().FirstAsync(m => m.Id == lotId)).QuantityRemaining);
        Assert.Equal(0, await db.ElectronicSignatures.CountAsync(x => x.MeaningOfSignature == SignatureMeaning.TitrantStandardized));
    }

    [Fact]
    public async Task Standardize_AgainstReferencePreparation_DeductsNothing()
    {
        await using var db = NewDb();
        var s = await SeedScenarioAsync(db);
        var lot = await AddStandardLotAsync(db, s.Section.Id, s.StandardEntry, purity: 100m, quantityRemaining: 10);
        var refTitrant = await AddPrimaryStandardTitrantAsync(db, s.Section.Id, "0.1N NaOH (ref)", s.SolventEntry, s.StandardEntry,
            replicateCount: 1, factorMin: 0.5m, factorMax: 1.5m, nominalStrength: 0.1m);
        var refPrep = await PrepareTitrantAsync(db, s.Section, s.UserId, refTitrant, s.SolventEntry);
        var service = TestServiceFactory.TitrantStandardization(db);
        await service.StandardizeAsync(refPrep.Id,
            new StandardizeRequest(new List<StandardizationReplicateInput> { new(lot.Id, 204.6084m, null, null, 10m, null) }, Password, null),
            s.UserId, null);
        var afterRef = (await db.Materials.AsNoTracking().FirstAsync(m => m.Id == lot.Id)).QuantityRemaining;

        var titrant = await AddAgainstVsTitrantAsync(db, s.Section.Id, "0.1N HCl", s.SolventEntry, refTitrant.Id,
            replicateCount: 1, factorMin: 0.5m, factorMax: 1.5m, nominalStrength: 0.1m);
        var prep = await PrepareTitrantAsync(db, s.Section, s.UserId, titrant, s.SolventEntry);
        await service.StandardizeAsync(prep.Id,
            new StandardizeRequest(new List<StandardizationReplicateInput> { new(null, null, refPrep.Id, 25.00m, 24.90m, null) }, Password, null),
            s.UserId, null);

        Assert.Equal(afterRef, (await db.Materials.AsNoTracking().FirstAsync(m => m.Id == lot.Id)).QuantityRemaining);
    }
}
