using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class MaterialMasterServiceTests
{
    private const string Password = "Correct-Password-1!";

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

        var user = new User { Username = "u_" + Guid.NewGuid().ToString("N")[..6], FullName = "Test User", RoleId = role.Id, IsActive = true, PasswordHash = TestPasswords.Hash(Password) };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        TestServiceFactory.AssignUserToMicroSection(db, user.Id);
        return (section, user.Id);
    }

    // A second lab with its own analyst, to prove entries are app-wide but editable only by the owner lab.
    private static async Task<(DocumentSection section, int userId)> SeedOtherLabAsync(MicroLimsDbContext db)
    {
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var section = new DocumentSection { Name = "Other Lab", Code = "OTHER", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(section);
        var role = new Role { Name = "Analyst 2", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { Username = "o_" + Guid.NewGuid().ToString("N")[..6], FullName = "Other User", RoleId = role.Id, IsActive = true, PasswordHash = TestPasswords.Hash(Password) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = user.Id, DepartmentId = section.DepartmentId, SectionId = section.Id });
        await db.SaveChangesAsync();
        return (section, user.Id);
    }

    private static async Task<Material> AddLotAsync(MicroLimsDbContext db, int sectionId, int entryId, string name, string code)
    {
        var lot = new Material
        {
            SectionId = sectionId,
            MaterialType = MaterialType.Chemical,
            MaterialMasterEntryId = entryId,
            MaterialName = name,
            Code = code,
            ManufacturerName = "Merck",
            BatchNumber = "B1",
            ReceivingDate = DateTime.UtcNow,
            Location = "Shelf",
            QuantityReceived = 10,
            QuantityRemaining = 10,
            Unit = MaterialUnit.Gram
        };
        db.Materials.Add(lot);
        await db.SaveChangesAsync();
        return lot;
    }

    // code = null uses the category's enforced prefix.
    private static SaveMaterialMasterEntryRequest Req(
        string? code = null,
        string name = "Sodium Hydroxide",
        MaterialMasterCategory category = MaterialMasterCategory.Reagent,
        string? workingConcentration = null,
        string? solvent = null,
        decimal? transitionRangeFrom = null,
        decimal? transitionRangeTo = null,
        string? colourChange = null,
        string? indicatorUse = null,
        int? sectionId = null) =>
        new(code ?? MaterialMasterService.PrefixFor(category) + "NAOH-01", name, category, "AR Grade", "Merck", MaterialUnit.Gram, sectionId,
            workingConcentration, solvent, transitionRangeFrom, transitionRangeTo, colourChange, indicatorUse);

    [Fact]
    public async Task Create_TrimsAndUppercasesCode_AndIsActive()
    {
        await using var db = NewDb();
        var (_, userId) = await SeedAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);

        var result = await service.CreateAsync(Req(code: " rg-naoh-01 ", name: "  Sodium Hydroxide  "), userId);

        Assert.Equal("RG-NAOH-01", result.Code);
        Assert.Equal("Sodium Hydroxide", result.Name);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Create_DuplicateCodeInSameSection_Throws()
    {
        await using var db = NewDb();
        var (_, userId) = await SeedAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);

        await service.CreateAsync(Req(code: "RG-NAOH-01"), userId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(Req(code: "rg-naoh-01", name: "Another"), userId));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task Create_DuplicateCodeInAnotherSection_Throws()
    {
        await using var db = NewDb();
        var (_, userId) = await SeedAsync(db);
        var (_, otherUserId) = await SeedOtherLabAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);

        await service.CreateAsync(Req(code: "RG-NAOH-01"), userId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(Req(code: "RG-NAOH-01", name: "Another"), otherUserId));
        Assert.Contains("Material master entry code \"RG-NAOH-01\" already exists.", ex.Message);
    }

    [Theory]
    [InlineData(MaterialMasterCategory.Reagent, "IND-X1", "Reagent codes must start with \"RG-\".")]
    [InlineData(MaterialMasterCategory.Indicator, "RG-X1", "Indicator codes must start with \"IND-\".")]
    [InlineData(MaterialMasterCategory.ReferenceStandard, "NAOH-01", "Reference standard codes must start with \"RS-\".")]
    [InlineData(MaterialMasterCategory.PrimaryStandard, "RS-X1", "Primary standard codes must start with \"PS-\".")]
    public async Task Create_WrongOrMissingPrefix_Throws(MaterialMasterCategory category, string code, string expected)
    {
        await using var db = NewDb();
        var (_, userId) = await SeedAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(Req(code: code, category: category), userId));
        Assert.Equal(expected, ex.Message);
    }

    [Fact]
    public async Task Update_ChangedCodeNeedsPrefix_ButUnchangedLegacyCodeIsGrandfathered()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);

        // Seeded directly: an older, un-prefixed code from before the rule.
        var legacy = new MaterialMasterEntry { SectionId = section.Id, Code = "NAOH-01", Name = "Sodium Hydroxide", Category = MaterialMasterCategory.Reagent, BaseUnit = MaterialUnit.Gram, IsActive = true };
        db.MaterialMasterEntries.Add(legacy);
        await db.SaveChangesAsync();

        var renamed = await service.UpdateAsync(legacy.Id, Req(code: "NAOH-01", name: "Sodium Hydroxide Pellets"), userId);
        Assert.Equal("NAOH-01", renamed.Code);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(legacy.Id, Req(code: "NAOH-02"), userId));
        var recoded = await service.UpdateAsync(legacy.Id, Req(code: "rg-naoh-02"), userId);
        Assert.Equal("RG-NAOH-02", recoded.Code);
    }

    [Fact]
    public async Task GetAll_ReturnsEntriesOfAllLabs_WithOwnerLabAndEditability()
    {
        await using var db = NewDb();
        var (microSection, userId) = await SeedAsync(db);
        var (otherSection, otherUserId) = await SeedOtherLabAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);

        var mine = await service.CreateAsync(Req(code: "RG-A"), userId);
        var theirs = await service.CreateAsync(Req(code: "RG-B"), otherUserId);
        await AddLotAsync(db, otherSection.Id, theirs.Id, theirs.Name, theirs.Code);

        var seenByMicro = await service.GetAllAsync(userId);

        Assert.Equal(2, seenByMicro.Count);
        var seenMine = seenByMicro.Single(e => e.Id == mine.Id);
        var seenTheirs = seenByMicro.Single(e => e.Id == theirs.Id);
        Assert.True(seenMine.CanEdit);
        Assert.False(seenMine.HasStock);
        Assert.Equal(microSection.Id, seenMine.SectionId);
        Assert.False(seenTheirs.CanEdit);
        Assert.True(seenTheirs.HasStock);
        Assert.Equal(otherSection.Id, seenTheirs.SectionId);
        Assert.Equal("OTHER", seenTheirs.SectionCode);
    }

    [Fact]
    public async Task Update_AndSetActive_ByNonOwnerLab_Throw()
    {
        await using var db = NewDb();
        var (_, userId) = await SeedAsync(db);
        var (_, otherUserId) = await SeedOtherLabAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);
        var entry = await service.CreateAsync(Req(), userId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(entry.Id, Req(name: "Hijacked"), otherUserId));
        Assert.Contains("owner lab", ex.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetActiveAsync(entry.Id, false, otherUserId));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeCodeAsync(entry.Id, "RG-NEW", "Reason", Password, otherUserId, null));

        Assert.Equal("Sodium Hydroxide", (await db.MaterialMasterEntries.SingleAsync()).Name);
    }

    [Fact]
    public async Task Update_CodeChangeIsRejected_OnceTheEntryHasStock()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);
        var entry = await service.CreateAsync(Req(), userId);
        await AddLotAsync(db, section.Id, entry.Id, entry.Name, entry.Code);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(entry.Id, Req(code: "RG-OTHER"), userId));
        Assert.Equal("This entry has stock; change its code with a reason and an electronic signature.", ex.Message);
        Assert.Equal("RG-NAOH-01", (await db.MaterialMasterEntries.SingleAsync()).Code);
    }

    [Fact]
    public async Task ChangeCode_WithValidSignature_ChangesEntryButNotLotSnapshots()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);
        var entry = await service.CreateAsync(Req(), userId);
        var lot = await AddLotAsync(db, section.Id, entry.Id, entry.Name, entry.Code);

        var updated = await service.ChangeCodeAsync(entry.Id, " rg-naoh-02 ", "Aligning with the new numbering", Password, userId, "10.0.0.1");

        Assert.Equal("RG-NAOH-02", updated.Code);
        Assert.True(updated.HasStock);
        Assert.Equal("RG-NAOH-02", (await db.MaterialMasterEntries.SingleAsync()).Code);
        Assert.Equal("RG-NAOH-01", (await db.Materials.SingleAsync(m => m.Id == lot.Id)).Code);

        var signature = Assert.Single(await db.ElectronicSignatures.ToListAsync());
        Assert.Equal(SignatureMeaning.MasterDataChanged, signature.MeaningOfSignature);
        Assert.Equal(ReviewEntityTypes.MaterialMasterEntry, signature.EntityType);
        Assert.Equal(entry.Id, signature.EntityId);
        Assert.Contains("RG-NAOH-01 -> RG-NAOH-02", signature.Comment);

        var audit = Assert.Single(await db.AuditLogs.Include(a => a.Changes)
            .Where(a => a.ActionCode == MaterialMasterService.CodeChangedActionCode && a.EntityId == entry.Id.ToString()).ToListAsync());
        Assert.Equal("Aligning with the new numbering", audit.Reason);
    }

    [Fact]
    public async Task ChangeCode_WrongPassword_LeavesCodeUnchanged()
    {
        await using var db = NewDb();
        var (_, userId) = await SeedAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);
        var entry = await service.CreateAsync(Req(), userId);

        await Assert.ThrowsAsync<SignatureVerificationException>(
            () => service.ChangeCodeAsync(entry.Id, "RG-NAOH-02", "Reason", "WrongPassword!", userId, null));

        Assert.Equal("RG-NAOH-01", (await db.MaterialMasterEntries.SingleAsync()).Code);
        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task ChangeCode_EnforcesReasonPrefixAndGlobalUniqueness()
    {
        await using var db = NewDb();
        var (_, userId) = await SeedAsync(db);
        var (_, otherUserId) = await SeedOtherLabAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);
        var entry = await service.CreateAsync(Req(), userId);
        await service.CreateAsync(Req(code: "RG-TAKEN", name: "Taken"), otherUserId);

        var noReason = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeCodeAsync(entry.Id, "RG-NEW", " ", Password, userId, null));
        Assert.Contains("reason", noReason.Message);
        var badPrefix = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeCodeAsync(entry.Id, "IND-NEW", "Reason", Password, userId, null));
        Assert.Contains("must start with \"RG-\"", badPrefix.Message);
        var taken = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeCodeAsync(entry.Id, "rg-taken", "Reason", Password, userId, null));
        Assert.Contains("already exists", taken.Message);
        var same = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangeCodeAsync(entry.Id, "RG-NAOH-01", "Reason", Password, userId, null));
        Assert.Contains("must be different", same.Message);
    }

    [Fact]
    public async Task Create_IndicatorFieldsOnNonIndicator_Throws()
    {
        await using var db = NewDb();
        var (_, userId) = await SeedAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(Req(category: MaterialMasterCategory.Reagent, solvent: "Ethanol"), userId));
        Assert.Contains("Indicator fields", ex.Message);
    }

    [Fact]
    public async Task Create_TransitionRangeFromAboveTo_Throws()
    {
        await using var db = NewDb();
        var (_, userId) = await SeedAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(Req(category: MaterialMasterCategory.Indicator, transitionRangeFrom: 10, transitionRangeTo: 8), userId));
        Assert.Contains("Transition range", ex.Message);
    }

    [Fact]
    public async Task Update_ChangesFields_AndKeepsCategoryRuleOnExistingLots()
    {
        await using var db = NewDb();
        var (section, userId) = await SeedAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);

        var entry = await service.CreateAsync(Req(category: MaterialMasterCategory.Reagent), userId);

        db.Materials.Add(new Material
        {
            SectionId = section.Id,
            MaterialType = MaterialType.Chemical,
            MaterialMasterEntryId = entry.Id,
            MaterialName = entry.Name,
            ManufacturerName = "Merck",
            BatchNumber = "B1",
            ReceivingDate = DateTime.UtcNow,
            Location = "Shelf",
            QuantityReceived = 10,
            QuantityRemaining = 10,
            Unit = MaterialUnit.Gram
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(entry.Id, Req(code: "RG-NAOH-01", category: MaterialMasterCategory.Indicator, solvent: "Water"), userId));
        Assert.Contains("linked stock lots", ex.Message);

        var updated = await service.UpdateAsync(entry.Id, Req(name: "Sodium Hydroxide Pellets"), userId);
        Assert.Equal("Sodium Hydroxide Pellets", updated.Name);
    }

    [Fact]
    public async Task SetActive_False_HidesFromActiveOnlyList()
    {
        await using var db = NewDb();
        var (_, userId) = await SeedAsync(db);
        var service = TestServiceFactory.MaterialMaster(db);

        var entry = await service.CreateAsync(Req(), userId);
        await service.SetActiveAsync(entry.Id, false, userId);

        var activeOnly = await service.GetAllAsync(userId, activeOnly: true);
        Assert.DoesNotContain(activeOnly, e => e.Id == entry.Id);

        var all = await service.GetAllAsync(userId);
        Assert.Contains(all, e => e.Id == entry.Id);
    }
}
