using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

// Master Testing Materials phase 2: categories of Other materials, receipt against
// a master entry, COA / quantity / release rules that now come from the category.
public class OtherMaterialsTests
{
    private const string Password = "Correct-Password-1!";

    private static MicroLimsDbContext NewDb()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.CurrentUserId = 1;
        return db;
    }

    // Two Microbiology users (a receiver and a second person) and one Physicochemical user.
    private record World(MicroLimsDbContext Db, DocumentSection Micro, DocumentSection Fp, int ReceiverId, int ReleaserId, int FpUserId);

    private static async Task<World> SeedAsync()
    {
        var db = NewDb();
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var fp = new DocumentSection { Name = "Physicochemical Laboratory", Code = "FP", DepartmentId = micro.DepartmentId, IsActive = true };
        db.DocumentSections.Add(fp);
        var role = new Role { Name = "Analyst", Type = RoleType.SectionHead, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        User NewUser(string name) => new() { Username = name, FullName = name, RoleId = role.Id, IsActive = true, PasswordHash = TestPasswords.Hash(Password) };
        var receiver = NewUser("receiver");
        var releaser = NewUser("releaser");
        var fpUser = NewUser("fpuser");
        db.Users.AddRange(receiver, releaser, fpUser);
        await db.SaveChangesAsync();

        TestServiceFactory.AssignUserToMicroSection(db, receiver.Id);
        TestServiceFactory.AssignUserToMicroSection(db, releaser.Id);
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = fpUser.Id, DepartmentId = fp.DepartmentId, SectionId = fp.Id });
        await db.SaveChangesAsync();
        return new World(db, micro, fp, receiver.Id, releaser.Id, fpUser.Id);
    }

    private static SaveMaterialRequest Receive(int entryId, DateTime? expiry, MaterialUnit? unit = null, string? storage = null,
        MaterialType type = MaterialType.Other, string? customType = null) =>
        new(type, "ignored", "Merck", "LOT-" + Guid.NewGuid().ToString("N")[..6],
            DateTime.UtcNow.AddDays(-1), expiry, null, "Shelf 1", 10m, unit, null, null, null,
            MaterialMasterEntryId: entryId, StorageCondition: storage, CustomType: customType);

    private static DateTime NextYear => DateTime.UtcNow.AddYears(1);

    private static async Task AddCoaAsync(MicroLimsDbContext db, int materialId) =>
        await AddCoaAsync(db, materialId, MaterialDocumentStatus.Current);

    private static async Task AddCoaAsync(MicroLimsDbContext db, int materialId, MaterialDocumentStatus status)
    {
        db.MaterialDocuments.Add(new MaterialDocument
        {
            MaterialId = materialId, DocumentType = MaterialDocumentType.COA, OriginalFileName = "COA.pdf", StorageKey = "t/" + Guid.NewGuid(),
            FileExtension = ".pdf", ContentType = "application/pdf", FileSizeBytes = 100, ContentSha256 = "HASH",
            UploadedByUserId = 1, Status = status
        });
        await db.SaveChangesAsync();
    }

    private static SaveMaterialCategoryRequest CategoryRequest(
        string name = "Filter Membrane", string prefix = "FLT", bool coa = false, bool expiry = true, bool tracked = true,
        bool approval = false, bool label = false, string? storage = null, int[]? sections = null,
        string reason = "New material type", string password = Password) =>
        new(name, prefix, MaterialUnit.Piece, coa, expiry, tracked, storage, approval, label, sections, reason, password);

    // ---- Category maintenance -------------------------------------------------------------

    [Fact]
    public async Task CreateCategory_NeedsReasonAndPassword_AndSignsMasterDataChanged()
    {
        var w = await SeedAsync();
        var service = TestServiceFactory.MaterialCategory(w.Db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(CategoryRequest(reason: " "), w.ReleaserId, null));
        await Assert.ThrowsAsync<SignatureVerificationException>(() => service.CreateAsync(CategoryRequest(password: "wrong"), w.ReleaserId, null));
        Assert.Empty(await w.Db.ElectronicSignatures.ToListAsync());

        var created = await service.CreateAsync(CategoryRequest(storage: " 2-8 C ", sections: new[] { w.Micro.Id }), w.ReleaserId, "10.0.0.1");

        Assert.Equal("FLT", created.CodePrefix);
        Assert.Equal("2-8 C", created.DefaultStorageCondition);
        Assert.Equal(new[] { w.Micro.Id }, created.SectionIds);
        var signature = await w.Db.ElectronicSignatures.SingleAsync();
        Assert.Equal(SignatureMeaning.MasterDataChanged, signature.MeaningOfSignature);
        Assert.Equal(ReviewEntityTypes.MaterialCategory, signature.EntityType);
        Assert.Equal(created.Id, signature.EntityId);
        Assert.Contains("New material type", signature.Comment);
        Assert.True(await w.Db.AuditLogs.AnyAsync(a => a.ActionCode == MaterialCategoryService.CreatedActionCode && a.Reason == "New material type"));
    }

    [Fact]
    public async Task CreateCategory_ValidatesPrefixAndNames()
    {
        var w = await SeedAsync();
        var service = TestServiceFactory.MaterialCategory(w.Db);
        await service.CreateAsync(CategoryRequest(), w.ReleaserId, null);

        foreach (var bad in new[] { "F", "FLTRAA", "F-T", "F1T", "RG", "ind" })
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(CategoryRequest(name: "Other " + bad, prefix: bad), w.ReleaserId, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(CategoryRequest(name: "filter membrane", prefix: "FMB"), w.ReleaserId, null)); // name
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(CategoryRequest(name: "Another", prefix: "flt"), w.ReleaserId, null)); // prefix
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(CategoryRequest(name: "Bad section", prefix: "BSE", sections: new[] { 9999 }), w.ReleaserId, null));
    }

    [Fact]
    public async Task UpdateCategory_SignsAndChangesRules_ButPrefixIsFrozenOnceAnEntryUsesIt()
    {
        var w = await SeedAsync();
        var service = TestServiceFactory.MaterialCategory(w.Db);
        var created = await service.CreateAsync(CategoryRequest(), w.ReleaserId, null);

        await Assert.ThrowsAsync<SignatureVerificationException>(() => service.UpdateAsync(created.Id, CategoryRequest(coa: true, password: "wrong"), w.ReleaserId, null));
        Assert.False((await w.Db.MaterialCategories.AsNoTracking().SingleAsync()).CoaRequired);

        var updated = await service.UpdateAsync(created.Id, CategoryRequest(prefix: "FIL", coa: true, approval: true, sections: new[] { w.Micro.Id }, reason: "COA now needed"), w.ReleaserId, null);
        Assert.True(updated.CoaRequired);
        Assert.True(updated.ApprovalBeforeUse);
        Assert.Equal("FIL", updated.CodePrefix); // no entry yet, so the prefix may still change
        Assert.Equal(2, await w.Db.ElectronicSignatures.CountAsync());

        var category = await w.Db.MaterialCategories.SingleAsync();
        await OtherMaterialTestData.CreateEntryAsync(w.Db, category, "Nylon 0.45", "FIL-001", w.Micro.Id);
        var frozen = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(created.Id, CategoryRequest(prefix: "FLT", coa: true, approval: true, sections: new[] { w.Micro.Id }), w.ReleaserId, null));
        Assert.Contains("prefix", frozen.Message);
        // The rules (not the prefix) can still change, and the sections can be cleared (= every section).
        var cleared = await service.UpdateAsync(created.Id, CategoryRequest(prefix: "FIL", coa: false), w.ReleaserId, null);
        Assert.Empty(cleared.SectionIds);
        Assert.True(cleared.HasEntries);
    }

    [Fact]
    public async Task GetCategories_FiltersByActiveAndSection()
    {
        var w = await SeedAsync();
        var service = TestServiceFactory.MaterialCategory(w.Db);
        await service.CreateAsync(CategoryRequest(name: "Everywhere", prefix: "EVR"), w.ReleaserId, null);
        await service.CreateAsync(CategoryRequest(name: "Micro only", prefix: "MIC", sections: new[] { w.Micro.Id }), w.ReleaserId, null);
        var off = await service.CreateAsync(CategoryRequest(name: "Retired", prefix: "RET"), w.ReleaserId, null);
        await service.SetActiveAsync(off.Id, false, w.ReleaserId);

        Assert.Equal(3, (await service.GetAllAsync()).Count);
        Assert.Equal(2, (await service.GetAllAsync(activeOnly: true)).Count);
        Assert.Equal(new[] { "Everywhere", "Micro only" }, (await service.GetAllAsync(true, w.Micro.Id)).Select(c => c.Name));
        Assert.Equal(new[] { "Everywhere" }, (await service.GetAllAsync(true, w.Fp.Id)).Select(c => c.Name));
    }

    // ---- Entries of the Other group ---------------------------------------------------------

    [Fact]
    public async Task Entry_CodeTakesTheCategoryPrefix_AndTheCategoryRulesApply()
    {
        var w = await SeedAsync();
        var category = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Supplement", "SUP", null, w.Micro.Id);
        var inactive = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Retired", "RET", c => c.IsActive = false);
        var fpOnly = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Physico only", "PHY", null, w.Fp.Id);
        var service = TestServiceFactory.MaterialMaster(w.Db);

        SaveMaterialMasterEntryRequest Req(string code, MaterialMasterCategory kind, int? categoryId) =>
            new(code, "Egg Yolk Tellurite", kind, null, null, MaterialUnit.Milliliter, null, MaterialCategoryId: categoryId);

        var ok = await service.CreateAsync(Req("sup-001", MaterialMasterCategory.Other, category.Id), w.ReceiverId);
        Assert.Equal("SUP-001", ok.Code);
        Assert.Equal("Supplement", ok.MaterialCategoryName);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Req("RG-001", MaterialMasterCategory.Other, category.Id), w.ReceiverId)); // wrong prefix
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Req("SUP-002", MaterialMasterCategory.Other, null), w.ReceiverId)); // category required
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Req("RG-002", MaterialMasterCategory.Reagent, category.Id), w.ReceiverId)); // only for Other
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Req("RET-001", MaterialMasterCategory.Other, inactive.Id), w.ReceiverId)); // inactive
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Req("PHY-001", MaterialMasterCategory.Other, fpOnly.Id), w.ReceiverId)); // owner lab not offered
    }

    [Fact]
    public async Task Entry_CategoryCannotChangeOnceStocked()
    {
        var w = await SeedAsync();
        var a = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Supplement", "SUP");
        var b = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Kit", "KIT");
        var entry = await OtherMaterialTestData.CreateEntryAsync(w.Db, a, "Egg Yolk", "SUP-001", w.Micro.Id);
        var master = TestServiceFactory.MaterialMaster(w.Db);
        var lots = TestServiceFactory.Material(w.Db);

        // Before stock the category may move, with a code that fits the new prefix.
        var moved = await master.UpdateAsync(entry.Id, new("KIT-001", "Egg Yolk", MaterialMasterCategory.Other, null, null, MaterialUnit.Milliliter, null, MaterialCategoryId: b.Id), w.ReceiverId);
        Assert.Equal(b.Id, moved.MaterialCategoryId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => master.UpdateAsync(entry.Id, new("KIT-001", "Egg Yolk", MaterialMasterCategory.Other, null, null, MaterialUnit.Milliliter, null, MaterialCategoryId: a.Id), w.ReceiverId)); // old code, new prefix needed

        await lots.CreateAsync(Receive(entry.Id, NextYear), w.ReceiverId);
        var stocked = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            master.UpdateAsync(entry.Id, new("SUP-001", "Egg Yolk", MaterialMasterCategory.Other, null, null, MaterialUnit.Milliliter, null, MaterialCategoryId: a.Id), w.ReceiverId));
        Assert.Contains("can't change", stocked.Message);
    }

    // ---- Receipt ---------------------------------------------------------------------------------

    [Fact]
    public async Task Receive_NewLotOfALegacyNamedType_IsRefused()
    {
        var w = await SeedAsync();
        var category = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Supplement", "SUP");
        var entry = await OtherMaterialTestData.CreateEntryAsync(w.Db, category, "Egg Yolk", "SUP-001", w.Micro.Id);
        var lots = TestServiceFactory.Material(w.Db);

        foreach (var type in MaterialTypeRules.LegacyNamedTypes)
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => lots.CreateAsync(Receive(entry.Id, NextYear, type: type), w.ReceiverId));
            Assert.Equal("Receive this as an Other material: choose its master entry.", ex.Message);
        }
        Assert.Empty(await w.Db.Materials.ToListAsync());
    }

    [Fact]
    public async Task Receive_Other_TakesNameCodeUnitAndStorageFromTheEntryAndCategory()
    {
        var w = await SeedAsync();
        var category = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Antibiotic Disc", "ABD",
            c => { c.DefaultUnit = MaterialUnit.Disc; c.DefaultStorageCondition = "2-8 C"; c.LabelRequired = true; });
        var entry = await OtherMaterialTestData.CreateEntryAsync(w.Db, category, "Ampicillin 10 ug", "ABD-001", w.Micro.Id);
        var lots = TestServiceFactory.Material(w.Db);

        var lot = await lots.CreateAsync(Receive(entry.Id, NextYear), w.ReceiverId);
        Assert.Equal(MaterialType.Other, lot.MaterialType);
        Assert.Equal("Ampicillin 10 ug", lot.MaterialName);
        Assert.Equal("ABD-001", lot.Code);
        Assert.Equal(MaterialUnit.Disc, lot.Unit);
        Assert.Equal("2-8 C", lot.StorageCondition);
        Assert.Equal("Antibiotic Disc", lot.CategoryName);
        Assert.True(lot.LabelRequired);
        Assert.Null(lot.ApprovalStatus);

        var overridden = await lots.CreateAsync(Receive(entry.Id, NextYear, MaterialUnit.Pack, " -20 C "), w.ReceiverId);
        Assert.Equal(MaterialUnit.Pack, overridden.Unit);
        Assert.Equal("-20 C", overridden.StorageCondition);
        Assert.Null(overridden.CustomType);
    }

    [Fact]
    public async Task Receive_Other_NeedsAnEntryFromAnActiveCategoryAvailableInTheLabsSection()
    {
        var w = await SeedAsync();
        var microOnly = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Micro only", "MIC", null, w.Micro.Id);
        var retired = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Retired", "RET");
        var good = await OtherMaterialTestData.CreateEntryAsync(w.Db, microOnly, "Thing", "MIC-001", w.Micro.Id);
        var inactiveEntry = await OtherMaterialTestData.CreateEntryAsync(w.Db, microOnly, "Inactive thing", "MIC-002", w.Micro.Id);
        inactiveEntry.IsActive = false;
        var inRetired = await OtherMaterialTestData.CreateEntryAsync(w.Db, retired, "Retired thing", "RET-001", w.Micro.Id);
        retired.IsActive = false;
        var reagent = new MaterialMasterEntry { SectionId = w.Micro.Id, Code = "RG-001", Name = "NaOH", Category = MaterialMasterCategory.Reagent, BaseUnit = MaterialUnit.Gram, IsActive = true };
        w.Db.MaterialMasterEntries.Add(reagent);
        await w.Db.SaveChangesAsync();
        var lots = TestServiceFactory.Material(w.Db);

        var noEntry = new SaveMaterialRequest(MaterialType.Other, "Free text", "Merck", "B1", DateTime.UtcNow, NextYear, null, "L", 1m, null, null, null, null);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => lots.CreateAsync(noEntry, w.ReceiverId));
        Assert.Contains("master entry", ex.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => lots.CreateAsync(Receive(inactiveEntry.Id, NextYear), w.ReceiverId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => lots.CreateAsync(Receive(inRetired.Id, NextYear), w.ReceiverId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => lots.CreateAsync(Receive(reagent.Id, NextYear), w.ReceiverId)); // not an Other entry
        await Assert.ThrowsAsync<InvalidOperationException>(() => lots.CreateAsync(Receive(good.Id, NextYear, customType: "Whatever"), w.ReceiverId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => lots.CreateAsync(Receive(good.Id, NextYear), w.FpUserId)); // category not offered to FP

        Assert.NotNull(await lots.CreateAsync(Receive(good.Id, NextYear), w.ReceiverId));
    }

    [Fact]
    public async Task Receive_ExpiryIsRequiredOnlyWhenTheCategorySaysSo()
    {
        var w = await SeedAsync();
        var needsExpiry = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Needs expiry", "NEX");
        var noExpiry = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Disposable Tool", "DSP", c => c.ExpiryRequired = false);
        var e1 = await OtherMaterialTestData.CreateEntryAsync(w.Db, needsExpiry, "A", "NEX-001", w.Micro.Id);
        var e2 = await OtherMaterialTestData.CreateEntryAsync(w.Db, noExpiry, "Gloves", "DSP-001", w.Micro.Id);
        var lots = TestServiceFactory.Material(w.Db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => lots.CreateAsync(Receive(e1.Id, null), w.ReceiverId));
        Assert.Contains("expiry", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null((await lots.CreateAsync(Receive(e2.Id, null), w.ReceiverId)).ExpiryDate);
    }

    [Fact]
    public async Task Update_ExistingLegacyLotKeepsItsStoredType_AndItsSnapshot()
    {
        var w = await SeedAsync();
        var category = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Supplement", "SUP");
        var entry = await OtherMaterialTestData.CreateEntryAsync(w.Db, category, "Egg Yolk Tellurite", "SUP-001", w.Micro.Id);
        var legacy = new Material
        {
            SectionId = w.Micro.Id, MaterialType = MaterialType.Supplement, MaterialName = "egg yolk  tellurite ", Code = null, ManufacturerName = "X",
            BatchNumber = "B1", ReceivingDate = DateTime.UtcNow, ExpiryDate = NextYear, Location = "L", QuantityReceived = 5, QuantityRemaining = 5,
            Unit = MaterialUnit.Milliliter, MaterialMasterEntryId = entry.Id
        };
        w.Db.Materials.Add(legacy);
        await w.Db.SaveChangesAsync();
        var lots = TestServiceFactory.Material(w.Db);

        await lots.UpdateAsync(legacy.Id, Receive(entry.Id, NextYear, MaterialUnit.Milliliter, type: MaterialType.Supplement) with { Location = "Fridge 2", QuantityReceived = 5m }, w.ReceiverId);

        var reloaded = await w.Db.Materials.AsNoTracking().SingleAsync();
        Assert.Equal(MaterialType.Supplement, reloaded.MaterialType);
        Assert.Equal("egg yolk  tellurite ", reloaded.MaterialName); // snapshot untouched
        Assert.Null(reloaded.Code);
        Assert.Equal("Fridge 2", reloaded.Location);
        Assert.Equal(entry.Id, reloaded.MaterialMasterEntryId);
    }

    // ---- COA, quantity, release ---------------------------------------------------------------

    [Fact]
    public async Task Consume_OtherLotInACoaCategory_IsBlockedWithoutACoa_AndAllowedWithOne()
    {
        var w = await SeedAsync();
        var withCoa = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Needs COA", "COA", c => c.CoaRequired = true);
        var without = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "No COA", "NOC");
        var e1 = await OtherMaterialTestData.CreateEntryAsync(w.Db, withCoa, "Reagent A", "COA-001", w.Micro.Id);
        var e2 = await OtherMaterialTestData.CreateEntryAsync(w.Db, without, "Reagent B", "NOC-001", w.Micro.Id);
        var lots = TestServiceFactory.Material(w.Db);
        var lotA = await lots.CreateAsync(Receive(e1.Id, NextYear), w.ReceiverId);
        var lotB = await lots.CreateAsync(Receive(e2.Id, NextYear), w.ReceiverId);

        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() => lots.ConsumeAsync(lotA.Id, MaterialType.Other, 1m, w.ReceiverId));
        Assert.Contains("Certificate of Analysis", blocked.Message);
        await lots.ConsumeAsync(lotB.Id, MaterialType.Other, 1m, w.ReceiverId); // a category without the rule is not blocked

        await AddCoaAsync(w.Db, lotA.Id, MaterialDocumentStatus.Superseded);
        await Assert.ThrowsAsync<InvalidOperationException>(() => lots.ConsumeAsync(lotA.Id, MaterialType.Other, 1m, w.ReceiverId));
        await AddCoaAsync(w.Db, lotA.Id);
        await lots.ConsumeAsync(lotA.Id, MaterialType.Other, 1m, w.ReceiverId);
        Assert.Equal(9m, (await w.Db.Materials.FindAsync(lotA.Id))!.QuantityRemaining);
    }

    [Fact]
    public async Task CoaEligibility_FollowsTheCategory()
    {
        var w = await SeedAsync();
        var withCoa = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Needs COA", "COA", c => c.CoaRequired = true);
        var entry = await OtherMaterialTestData.CreateEntryAsync(w.Db, withCoa, "Reagent A", "COA-001", w.Micro.Id);
        var lot = await TestServiceFactory.Material(w.Db).CreateAsync(Receive(entry.Id, NextYear), w.ReceiverId);
        var documents = new MaterialDocumentService(w.Db, new InMemoryFileStorageService(), new MaterialDocumentFileValidator(10_000_000),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MaterialDocumentService>.Instance, new UserSectionScopeService(w.Db));

        var before = await documents.GetCOAEligibilityAsync(lot.Id);
        Assert.True(before.CoaRequired);
        Assert.False(before.IsEligible);
        await AddCoaAsync(w.Db, lot.Id);
        Assert.True((await documents.GetCOAEligibilityAsync(lot.Id)).IsEligible);
    }

    [Fact]
    public async Task Consume_LegacySupplementWithoutACategory_StillNeedsACoa()
    {
        var w = await SeedAsync();
        var legacy = new Material
        {
            SectionId = w.Micro.Id, MaterialType = MaterialType.Supplement, MaterialName = "Old supplement", ManufacturerName = "X",
            BatchNumber = "B1", ReceivingDate = DateTime.UtcNow, ExpiryDate = NextYear, Location = "L", QuantityReceived = 5, QuantityRemaining = 5, Unit = MaterialUnit.Milliliter
        };
        w.Db.Materials.Add(legacy);
        await w.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => TestServiceFactory.Material(w.Db).ConsumeAsync(legacy.Id, MaterialType.Supplement, 1m, w.ReceiverId));
    }

    [Fact]
    public async Task Consume_QuantityNotTracked_NeitherChecksNorDecrementsTheBalance()
    {
        var w = await SeedAsync();
        var tools = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Disposable Tool", "DSP", c => { c.QuantityTracked = false; c.ExpiryRequired = false; });
        var entry = await OtherMaterialTestData.CreateEntryAsync(w.Db, tools, "Gloves", "DSP-001", w.Micro.Id);
        var lots = TestServiceFactory.Material(w.Db);
        var lot = await lots.CreateAsync(Receive(entry.Id, null), w.ReceiverId);

        await lots.ConsumeAsync(lot.Id, MaterialType.Other, 1000m, w.ReceiverId); // far more than the 10 received
        Assert.Equal(10m, (await w.Db.Materials.FindAsync(lot.Id))!.QuantityRemaining);

        // Expiry is still enforced.
        var expired = await w.Db.Materials.FindAsync(lot.Id);
        expired!.ExpiryDate = DateTime.UtcNow.AddDays(-1);
        await w.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => lots.ConsumeAsync(lot.Id, MaterialType.Other, 1m, w.ReceiverId));
    }

    [Fact]
    public async Task ApprovalGate_HoldsTheLotUntilAnotherPersonReleasesIt()
    {
        var w = await SeedAsync();
        var gated = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Gated", "GAT", c => c.ApprovalBeforeUse = true);
        var entry = await OtherMaterialTestData.CreateEntryAsync(w.Db, gated, "Gated thing", "GAT-001", w.Micro.Id);
        var lots = TestServiceFactory.Material(w.Db);
        var release = TestServiceFactory.MaterialRelease(w.Db);
        var lot = await lots.CreateAsync(Receive(entry.Id, NextYear), w.ReceiverId);

        Assert.Equal(ApprovalGateStatus.PendingReview, lot.ApprovalStatus);
        Assert.False(lot.IsUsable);
        var held = await Assert.ThrowsAsync<InvalidOperationException>(() => lots.ConsumeAsync(lot.Id, MaterialType.Other, 1m, w.ReceiverId));
        Assert.Contains("awaiting release", held.Message);

        // The receiver cannot release their own lot; nothing is signed or changed.
        var sod = await Assert.ThrowsAsync<InvalidOperationException>(() => release.DecideAsync(lot.Id, w.ReceiverId, Password, true, null, null));
        Assert.Contains("received", sod.Message);
        Assert.Empty(await w.Db.ElectronicSignatures.ToListAsync());

        await Assert.ThrowsAsync<SignatureVerificationException>(() => release.DecideAsync(lot.Id, w.ReleaserId, "wrong", true, null, null));
        Assert.Equal(ApprovalGateStatus.PendingReview, (await w.Db.Materials.FindAsync(lot.Id))!.ApprovalStatus);

        await release.DecideAsync(lot.Id, w.ReleaserId, Password, true, "Checked against the CoA", "10.0.0.2");
        var released = await w.Db.Materials.FindAsync(lot.Id);
        Assert.Equal(ApprovalGateStatus.Approved, released!.ApprovalStatus);
        Assert.Equal(w.ReleaserId, released.ApprovedByUserId);
        Assert.NotNull(released.ApprovedAt);
        Assert.True(released.IsUsable);
        var signature = await w.Db.ElectronicSignatures.SingleAsync();
        Assert.Equal(SignatureMeaning.Approved, signature.MeaningOfSignature);
        Assert.Equal(ReviewEntityTypes.Material, signature.EntityType);
        Assert.Single(await w.Db.ReviewWorkflowEvents.Where(e => e.EntityType == ReviewEntityTypes.Material && e.EntityId == lot.Id).ToListAsync());

        await lots.ConsumeAsync(lot.Id, MaterialType.Other, 1m, w.ReceiverId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => release.DecideAsync(lot.Id, w.ReleaserId, Password, true, null, null)); // already decided
    }

    [Fact]
    public async Task ApprovalGate_RejectedLotIsNeverUsable_AndRejectionNeedsAComment()
    {
        var w = await SeedAsync();
        var gated = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Gated", "GAT", c => c.ApprovalBeforeUse = true);
        var entry = await OtherMaterialTestData.CreateEntryAsync(w.Db, gated, "Gated thing", "GAT-001", w.Micro.Id);
        var lots = TestServiceFactory.Material(w.Db);
        var release = TestServiceFactory.MaterialRelease(w.Db);
        var lot = await lots.CreateAsync(Receive(entry.Id, NextYear), w.ReceiverId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => release.DecideAsync(lot.Id, w.ReleaserId, Password, false, " ", null));
        await release.DecideAsync(lot.Id, w.ReleaserId, Password, false, "Damaged packaging", null);

        var rejected = await w.Db.Materials.FindAsync(lot.Id);
        Assert.Equal(ApprovalGateStatus.Rejected, rejected!.ApprovalStatus);
        Assert.False(rejected.IsUsable);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => lots.ConsumeAsync(lot.Id, MaterialType.Other, 1m, w.ReceiverId));
        Assert.Contains("was rejected", ex.Message);
    }

    [Fact]
    public async Task Release_NeedsWriteAccessToTheLotsSection_AndAGatedLot()
    {
        var w = await SeedAsync();
        var gated = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Gated", "GAT", c => c.ApprovalBeforeUse = true);
        var plain = await OtherMaterialTestData.CreateCategoryAsync(w.Db, "Plain", "PLN");
        var gatedEntry = await OtherMaterialTestData.CreateEntryAsync(w.Db, gated, "Gated thing", "GAT-001", w.Micro.Id);
        var plainEntry = await OtherMaterialTestData.CreateEntryAsync(w.Db, plain, "Plain thing", "PLN-001", w.Micro.Id);
        var lots = TestServiceFactory.Material(w.Db);
        var release = TestServiceFactory.MaterialRelease(w.Db);
        var gatedLot = await lots.CreateAsync(Receive(gatedEntry.Id, NextYear), w.ReceiverId);
        var plainLot = await lots.CreateAsync(Receive(plainEntry.Id, NextYear), w.ReceiverId);

        await Assert.ThrowsAnyAsync<Exception>(() => release.DecideAsync(gatedLot.Id, w.FpUserId, Password, true, null, null)); // other lab
        Assert.Equal(ApprovalGateStatus.PendingReview, (await w.Db.Materials.FindAsync(gatedLot.Id))!.ApprovalStatus);
        var noGate = await Assert.ThrowsAsync<InvalidOperationException>(() => release.DecideAsync(plainLot.Id, w.ReleaserId, Password, true, null, null));
        Assert.Contains("does not need a release", noGate.Message);
    }

    [Fact]
    public async Task UsableReferenceStandards_ExcludeLotsHeldForRelease()
    {
        var w = await SeedAsync();
        w.Db.Materials.AddRange(
            new Material { SectionId = w.Micro.Id, MaterialType = MaterialType.ReferenceStandard, MaterialName = "Open", ManufacturerName = "X", BatchNumber = "A", ReceivingDate = DateTime.UtcNow, Location = "L", QuantityReceived = 1, QuantityRemaining = 1, Unit = MaterialUnit.Gram },
            new Material { SectionId = w.Micro.Id, MaterialType = MaterialType.ReferenceStandard, MaterialName = "Held", ManufacturerName = "X", BatchNumber = "B", ReceivingDate = DateTime.UtcNow, Location = "L", QuantityReceived = 1, QuantityRemaining = 1, Unit = MaterialUnit.Gram, ApprovalStatus = ApprovalGateStatus.PendingReview },
            new Material { SectionId = w.Micro.Id, MaterialType = MaterialType.ReferenceStandard, MaterialName = "Rejected", ManufacturerName = "X", BatchNumber = "C", ReceivingDate = DateTime.UtcNow, Location = "L", QuantityReceived = 1, QuantityRemaining = 1, Unit = MaterialUnit.Gram, ApprovalStatus = ApprovalGateStatus.Rejected });
        await w.Db.SaveChangesAsync();

        var usable = await TestServiceFactory.Material(w.Db).GetUsableReferenceStandardsAsync(w.ReceiverId);
        Assert.Equal(new[] { "Open" }, usable.Select(m => m.MaterialName));
    }
}
