using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

// The unified Master Testing Materials list: grouping, filters, per-lab edit rights and the read-only working standards.
public class TestingMaterialCatalogTests
{
    private record World(MicroLimsDbContext Db, DocumentSection Micro, DocumentSection Other, int OwnerId, int OutsiderId,
        MaterialCategory Supplements, MaterialCategory Ppe);

    private static async Task<World> SeedAsync()
    {
        var db = new MicroLimsDbContext(new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { CurrentUserId = 1 };
        var micro = TestServiceFactory.EnsureMicroSection(db);
        var other = new DocumentSection { Name = "Other Lab", Code = "OTHER", DepartmentId = micro.DepartmentId, IsActive = true };
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.DocumentSections.Add(other);
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var owner = new User { Username = "owner", FullName = "Owner", RoleId = role.Id, IsActive = true };
        var outsider = new User { Username = "outsider", FullName = "Outsider", RoleId = role.Id, IsActive = true };
        db.Users.AddRange(owner, outsider);
        await db.SaveChangesAsync();
        TestServiceFactory.AssignUserToMicroSection(db, owner.Id);
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = outsider.Id, DepartmentId = other.DepartmentId, SectionId = other.Id });

        // Supplements are offered to Micro only; PPE to every section.
        var supplements = new MaterialCategory { Name = "Supplement", CodePrefix = "SUP-", IsActive = true };
        supplements.Sections.Add(new MaterialCategorySection { SectionId = micro.Id });
        var ppe = new MaterialCategory { Name = "PPE", CodePrefix = "PPE-", IsActive = true };
        db.MaterialCategories.AddRange(supplements, ppe);
        await db.SaveChangesAsync();
        return new World(db, micro, other, owner.Id, outsider.Id, supplements, ppe);
    }

    private static MaterialMasterEntry Entry(World w, string code, string name, MaterialMasterCategory category, bool active = true,
        MaterialCategory? materialCategory = null) => new()
    {
        SectionId = w.Micro.Id, Code = code, Name = name, Category = category, BaseUnit = MaterialUnit.Gram, IsActive = active,
        MaterialCategoryId = materialCategory?.Id,
        CreatedByUserId = 1, CreatedAt = DateTime.UtcNow, LastModifiedByUserId = 1, LastModifiedAt = DateTime.UtcNow,
    };

    private static async Task<World> SeedAllGroupsAsync()
    {
        var w = await SeedAsync();
        var ecoli = new Organism { ScientificName = "Escherichia coli" };
        w.Db.Organisms.Add(ecoli);
        w.Db.MaterialMasterEntries.AddRange(
            Entry(w, "RG-001", "Sodium hydroxide", MaterialMasterCategory.Reagent),
            Entry(w, "IND-001", "Methyl red", MaterialMasterCategory.Indicator),
            Entry(w, "RS-001", "Paracetamol RS", MaterialMasterCategory.ReferenceStandard),
            Entry(w, "PS-001", "Potassium hydrogen phthalate", MaterialMasterCategory.PrimaryStandard, active: false),
            Entry(w, "SUP-001", "Egg yolk tellurite", MaterialMasterCategory.Other, materialCategory: w.Supplements),
            Entry(w, "PPE-001", "Nitrile gloves", MaterialMasterCategory.Other, materialCategory: w.Ppe));
        w.Db.MediaProducts.AddRange(
            new MediaProduct { Name = "Tryptic Soy Agar", Code = "TSA", IsActive = true },
            new MediaProduct { Name = "Sabouraud Dextrose Agar", Code = "SDA", IsActive = false });
        await w.Db.SaveChangesAsync();
        w.Db.ReferenceOrganisms.Add(new ReferenceOrganism
        {
            Code = "ORG-001", OrganismId = ecoli.Id, CultureCollection = CultureCollection.ATCC, StrainNumber = "8739",
            OwnerSectionId = w.Micro.Id, IsActive = true, CreatedByUserId = 1, LastModifiedByUserId = 1,
        });
        // A working standard lot (created by a qualification) and a depleted one.
        w.Db.Materials.AddRange(
            WorkingStandard(w, "WS-01/09/2026", 5m),
            WorkingStandard(w, "WS-01/03/2026", 0m));
        await w.Db.SaveChangesAsync();
        return w;
    }

    private static Material WorkingStandard(World w, string code, decimal remaining) => new()
    {
        SectionId = w.Micro.Id, MaterialType = MaterialType.WorkingStandard, MaterialName = "Paracetamol WS", BatchNumber = "RM-1",
        Code = code, QuantityReceived = 5m, QuantityRemaining = remaining, ExpiryDate = DateTime.UtcNow.AddYears(1),
        CreatedByUserId = 1, LastModifiedByUserId = 1,
    };

    private static TestingMaterialCatalogService Service(World w) => new(w.Db, new UserSectionScopeService(w.Db));

    [Fact]
    public async Task ListsAllFiveGroups_InTabOrder_WithTheSourceShape()
    {
        var w = await SeedAllGroupsAsync();

        var rows = await Service(w).GetAsync(w.OwnerId);

        Assert.Equal(new[]
        {
            TestingMaterialGroups.ReagentsIndicators, TestingMaterialGroups.ReagentsIndicators,
            TestingMaterialGroups.Standards, TestingMaterialGroups.Standards, TestingMaterialGroups.Standards, TestingMaterialGroups.Standards,
            TestingMaterialGroups.CultureMedia, TestingMaterialGroups.CultureMedia,
            TestingMaterialGroups.ReferenceOrganisms,
            TestingMaterialGroups.OtherMaterials, TestingMaterialGroups.OtherMaterials,
        }, rows.Select(r => r.Group));
        Assert.Equal(new[] { "IND-001", "RG-001" }, rows.Where(r => r.Group == TestingMaterialGroups.ReagentsIndicators).Select(r => r.Code));
        Assert.All(rows.Where(r => r.Group is TestingMaterialGroups.ReagentsIndicators or TestingMaterialGroups.OtherMaterials),
            r => Assert.Equal(TestingMaterialSourceTypes.MaterialMasterEntry, r.SourceType));
        Assert.Equal(TestingMaterialSourceTypes.MediaProduct, rows.Single(r => r.Code == "TSA").SourceType);
        var strain = rows.Single(r => r.Code == "ORG-001");
        Assert.Equal(TestingMaterialSourceTypes.ReferenceOrganism, strain.SourceType);
        Assert.Equal("Escherichia coli ATCC 8739", strain.Name);
        Assert.Equal("Escherichia coli", strain.SubCategory);
        Assert.Equal("Reference Standard", rows.Single(r => r.Code == "RS-001").SubCategory);
        Assert.Equal("Indicator", rows.Single(r => r.Code == "IND-001").SubCategory);
        Assert.Equal("Supplement", rows.Single(r => r.Code == "SUP-001").SubCategory);
        Assert.Equal("MICRO", rows.Single(r => r.Code == "RG-001").OwnerSectionCode);
    }

    [Fact]
    public async Task WorkingStandards_AreListedReadOnly_UnderStandards()
    {
        var w = await SeedAllGroupsAsync();

        var rows = await Service(w).GetAsync(w.OwnerId, group: "standards");

        var ws = rows.Where(r => r.SourceType == TestingMaterialSourceTypes.WorkingStandard).ToList();
        Assert.Equal(new[] { "WS-01/03/2026", "WS-01/09/2026" }, ws.Select(r => r.Code));
        Assert.All(ws, r =>
        {
            Assert.Equal(TestingMaterialGroups.Standards, r.Group);
            Assert.True(r.ReadOnly);
            Assert.False(r.CanEdit);
            Assert.Equal("Working Standard", r.SubCategory);
        });
        var current = ws.Single(r => r.Code == "WS-01/09/2026");
        Assert.True(current.IsActive);
        Assert.True(current.HasStock);
        var depleted = ws.Single(r => r.Code == "WS-01/03/2026");
        Assert.False(depleted.IsActive);
        Assert.False(depleted.HasStock);
        Assert.All(rows.Where(r => r.SourceType != TestingMaterialSourceTypes.WorkingStandard), r => Assert.False(r.ReadOnly));
        // activeOnly drops the depleted lot and the inactive primary standard.
        var active = await Service(w).GetAsync(w.OwnerId, group: TestingMaterialGroups.Standards, activeOnly: true);
        Assert.Equal(new[] { "RS-001", "WS-01/09/2026" }, active.Select(r => r.Code));
    }

    [Fact]
    public async Task Filters_ByGroup_Search_AndActiveOnly()
    {
        var w = await SeedAllGroupsAsync();
        var service = Service(w);

        Assert.Equal(2, (await service.GetAsync(w.OwnerId, group: TestingMaterialGroups.CultureMedia)).Count);
        Assert.Equal("TSA", Assert.Single(await service.GetAsync(w.OwnerId, group: "CultureMedia", activeOnly: true)).Code);
        Assert.Equal(new[] { "RG-001" }, (await service.GetAsync(w.OwnerId, search: "  SODIUM ")).Select(r => r.Code));
        Assert.Equal(new[] { "PS-001" }, (await service.GetAsync(w.OwnerId, search: "ps-0")).Select(r => r.Code));
        Assert.Equal(new[] { "ORG-001" }, (await service.GetAsync(w.OwnerId, group: TestingMaterialGroups.ReferenceOrganisms, search: "8739")).Select(r => r.Code));
        Assert.Empty(await service.GetAsync(w.OwnerId, search: "nothing like this"));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAsync(w.OwnerId, group: "Nonsense"));
        Assert.Contains("Unknown group", ex.Message);
    }

    [Fact]
    public async Task SectionFilter_NarrowsOnlyOtherMaterials_ToCategoriesOfferedThere()
    {
        var w = await SeedAllGroupsAsync();
        var service = Service(w);

        var forOther = await service.GetAsync(w.OwnerId, sectionId: w.Other.Id);
        var forMicro = await service.GetAsync(w.OwnerId, sectionId: w.Micro.Id);

        Assert.Equal(new[] { "PPE-001" }, forOther.Where(r => r.Group == TestingMaterialGroups.OtherMaterials).Select(r => r.Code));
        Assert.Equal(new[] { "PPE-001", "SUP-001" }, forMicro.Where(r => r.Group == TestingMaterialGroups.OtherMaterials).Select(r => r.Code));
        Assert.Equal(forMicro.Count(r => r.Group != TestingMaterialGroups.OtherMaterials), forOther.Count(r => r.Group != TestingMaterialGroups.OtherMaterials));
    }

    [Fact]
    public async Task EveryLabSeesEveryRow_ButOnlyTheOwnerLabCanEdit_AndHasStockIsReported()
    {
        var w = await SeedAllGroupsAsync();
        var reagent = await w.Db.MaterialMasterEntries.SingleAsync(e => e.Code == "RG-001");
        var tsa = await w.Db.MediaProducts.SingleAsync(p => p.Code == "TSA");
        w.Db.Materials.AddRange(
            new Material { SectionId = w.Micro.Id, MaterialType = MaterialType.Chemical, MaterialName = "NaOH", BatchNumber = "B1", MaterialMasterEntryId = reagent.Id, QuantityRemaining = 1, CreatedByUserId = 1, LastModifiedByUserId = 1 },
            new Material { SectionId = w.Micro.Id, MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA", BatchNumber = "B2", MediaProductId = tsa.Id, QuantityRemaining = 1, CreatedByUserId = 1, LastModifiedByUserId = 1 });
        await w.Db.SaveChangesAsync();
        var service = Service(w);

        var asOwner = await service.GetAsync(w.OwnerId);
        var asOutsider = await service.GetAsync(w.OutsiderId);

        Assert.Equal(asOwner.Count, asOutsider.Count);
        Assert.True(asOwner.Single(r => r.Code == "RG-001").CanEdit);
        Assert.False(asOutsider.Single(r => r.Code == "RG-001").CanEdit);
        Assert.False(asOutsider.Single(r => r.Code == "ORG-001").CanEdit);
        Assert.True(asOwner.Single(r => r.Code == "RG-001").HasStock);
        Assert.False(asOwner.Single(r => r.Code == "IND-001").HasStock);
        Assert.True(asOwner.Single(r => r.Code == "TSA").HasStock);
        Assert.False(asOwner.Single(r => r.Code == "SDA").HasStock);
    }
}
