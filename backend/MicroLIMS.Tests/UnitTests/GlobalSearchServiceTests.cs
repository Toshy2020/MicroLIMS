using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs.Search;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class GlobalSearchServiceTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static (DocumentSection secA, DocumentSection secB) SeedSections(MicroLimsDbContext db)
    {
        var dept = new DocumentDepartment { Name = "Quality Control", Code = "QC", IsActive = true };
        db.DocumentDepartments.Add(dept);
        db.SaveChanges();

        var secA = new DocumentSection { Name = "Microbiology", Code = "MICRO", DepartmentId = dept.Id, IsActive = true };
        var secB = new DocumentSection { Name = "Physicochemical", Code = "FP", DepartmentId = dept.Id, IsActive = true };
        db.DocumentSections.AddRange(secA, secB);
        db.SaveChanges();

        return (secA, secB);
    }

    private static (User userA, User userB, User admin) SeedUsers(MicroLimsDbContext db, DocumentSection secA, DocumentSection secB)
    {
        var analystRole = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        var adminRole = new Role { Name = "System Administrator", Type = RoleType.SystemAdministrator, IsActive = true };
        db.Roles.AddRange(analystRole, adminRole);
        db.SaveChanges();

        var userA = new User { Username = "analystA", FullName = "Analyst Micro", RoleId = analystRole.Id, Role = analystRole, IsActive = true };
        var userB = new User { Username = "analystB", FullName = "Analyst FP", RoleId = analystRole.Id, Role = analystRole, IsActive = true };
        var admin = new User { Username = "admin", FullName = "System Administrator", RoleId = adminRole.Id, Role = adminRole, IsActive = true };
        db.Users.AddRange(userA, userB, admin);
        db.SaveChanges();
        AdministratorScope.GrantViewAll(db, adminRole.Id); // reads every section (Sections.ViewAll), belongs to none

        db.UserOrgMemberships.AddRange(
            new UserOrgMembership { UserId = userA.Id, DepartmentId = secA.DepartmentId, SectionId = secA.Id },
            new UserOrgMembership { UserId = userB.Id, DepartmentId = secB.DepartmentId, SectionId = secB.Id }
        );
        db.SaveChanges();

        return (userA, userB, admin);
    }

    [Fact]
    public async Task SearchAsync_QueryTooShort_ReturnsEmpty()
    {
        await using var db = NewDb();
        var (secA, secB) = SeedSections(db);
        var (userA, _, _) = SeedUsers(db, secA, secB);
        var scope = new UserSectionScopeService(db);
        var searchService = new GlobalSearchService(db, scope);

        var result = await searchService.SearchAsync(new GlobalSearchRequest("a"), userA.Id);

        Assert.Equal(0, result.TotalMatches);
        Assert.Empty(result.Categories);
    }

    [Fact]
    public async Task SearchAsync_ExactSampleReference_ReturnsSampleWithCorrectNavigation()
    {
        await using var db = NewDb();
        var (secA, secB) = SeedSections(db);
        var (userA, _, _) = SeedUsers(db, secA, secB);
        var scope = new UserSectionScopeService(db);
        var searchService = new GlobalSearchService(db, scope);

        var item = new Item { Name = "Amoxicillin 500mg", Code = "ITM-AMOX", Category = SampleCategory.FinishedProduct };
        db.Items.Add(item);

        var sample = new Sample
        {
            ReferenceNumber = "FP0107026",
            ControlNumber = "CTRL-001",
            BatchNumber = "BAT-9912",
            Category = SampleCategory.FinishedProduct,
            ItemId = item.Id,
            Item = item,
            Status = SampleStatus.InTesting
        };
        sample.TestOrders.Add(new TestOrder { TestCode = "TAMC", SectionId = secA.Id, IsSuperseded = false });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var result = await searchService.SearchAsync(new GlobalSearchRequest("FP0107026"), userA.Id);

        Assert.True(result.TotalMatches >= 1);
        var sampleCat = result.Categories.FirstOrDefault(c => c.CategoryName == "Samples");
        Assert.NotNull(sampleCat);
        var match = sampleCat.Items.FirstOrDefault(i => i.Title == "FP0107026");
        Assert.NotNull(match);
        Assert.Equal($"/receiving-testing?sampleId={sample.Id}", match.NavigationTarget);
        Assert.Contains("Amoxicillin", match.Subtitle);
    }

    [Fact]
    public async Task SearchAsync_BatchNumber_MatchesAcrossSamplesAndMaterials()
    {
        await using var db = NewDb();
        var (secA, secB) = SeedSections(db);
        var (userA, _, _) = SeedUsers(db, secA, secB);
        var scope = new UserSectionScopeService(db);
        var searchService = new GlobalSearchService(db, scope);

        var sharedBatch = "BATCH-XYZ-2026";

        var sample = new Sample
        {
            ReferenceNumber = "SMP-BATCH-1",
            ControlNumber = "CTRL-B1",
            BatchNumber = sharedBatch,
            Category = SampleCategory.FinishedProduct,
            Status = SampleStatus.Received
        };
        sample.TestOrders.Add(new TestOrder { TestCode = "TAMC", SectionId = secA.Id, IsSuperseded = false });
        db.Samples.Add(sample);

        var material = new Material
        {
            MaterialName = "Sodium Chloride",
            BatchNumber = sharedBatch,
            SectionId = secA.Id,
            MaterialType = MaterialType.Chemical,
            Location = "Shelf A2"
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        var result = await searchService.SearchAsync(new GlobalSearchRequest("BATCH-XYZ"), userA.Id);

        Assert.True(result.TotalMatches >= 2);
        Assert.Contains(result.Categories, c => c.CategoryName == "Samples");
        Assert.Contains(result.Categories, c => c.CategoryName == "Materials");
    }

    [Fact]
    public async Task SearchAsync_RespectsLaboratorySectionIsolation()
    {
        await using var db = NewDb();
        var (secA, secB) = SeedSections(db);
        var (userA, userB, admin) = SeedUsers(db, secA, secB);
        var scope = new UserSectionScopeService(db);
        var searchService = new GlobalSearchService(db, scope);

        // Sample in Section A (Microbiology)
        var sampleA = new Sample { ReferenceNumber = "SMP-MICRO-01", ControlNumber = "C-A", BatchNumber = "B-COMMON", Status = SampleStatus.InTesting };
        sampleA.TestOrders.Add(new TestOrder { TestCode = "TAMC", SectionId = secA.Id, IsSuperseded = false });

        // Sample in Section B (Physicochemical)
        var sampleB = new Sample { ReferenceNumber = "SMP-FP-02", ControlNumber = "C-B", BatchNumber = "B-COMMON", Status = SampleStatus.InTesting };
        sampleB.TestOrders.Add(new TestOrder { TestCode = "ASSAY", SectionId = secB.Id, IsSuperseded = false });

        db.Samples.AddRange(sampleA, sampleB);
        await db.SaveChangesAsync();

        // User A (Microbiology only) should see only SMP-MICRO-01
        var resultUserA = await searchService.SearchAsync(new GlobalSearchRequest("B-COMMON"), userA.Id);
        var samplesUserA = resultUserA.Categories.FirstOrDefault(c => c.CategoryName == "Samples");
        Assert.NotNull(samplesUserA);
        Assert.Single(samplesUserA.Items);
        Assert.Equal("SMP-MICRO-01", samplesUserA.Items[0].Title);

        // User B (FP only) should see only SMP-FP-02
        var resultUserB = await searchService.SearchAsync(new GlobalSearchRequest("B-COMMON"), userB.Id);
        var samplesUserB = resultUserB.Categories.FirstOrDefault(c => c.CategoryName == "Samples");
        Assert.NotNull(samplesUserB);
        Assert.Single(samplesUserB.Items);
        Assert.Equal("SMP-FP-02", samplesUserB.Items[0].Title);

        // A System Administrator holding Sections.ViewAll should see BOTH (read-only)
        var resultAdmin = await searchService.SearchAsync(new GlobalSearchRequest("B-COMMON"), admin.Id);
        var samplesAdmin = resultAdmin.Categories.FirstOrDefault(c => c.CategoryName == "Samples");
        Assert.NotNull(samplesAdmin);
        Assert.Equal(2, samplesAdmin.Items.Count);
    }

    [Fact]
    public async Task SearchAsync_EquipmentAndMedia_ReturnsScopedResults()
    {
        await using var db = NewDb();
        var (secA, secB) = SeedSections(db);
        var (userA, _, _) = SeedUsers(db, secA, secB);
        var scope = new UserSectionScopeService(db);
        var searchService = new GlobalSearchService(db, scope);

        var equip = new EquipmentInventory
        {
            Code = "INC-TEST-001",
            InstrumentType = "Incubator 150L",
            ManufacturerName = "Memmert",
            Location = "Micro Room 1",
            SectionId = secA.Id
        };
        db.EquipmentInventories.Add(equip);

        var mediaMat = new Material
        {
            MaterialName = "TSA Dehydrated",
            BatchNumber = "TSA-RAW-1",
            SectionId = secA.Id,
            MaterialType = MaterialType.DehydratedMedia
        };
        db.Materials.Add(mediaMat);

        var media = new Media
        {
            LotNumber = "MED-LOT-9988",
            Material = mediaMat,
            MaterialId = mediaMat.Id,
            ExpiryDate = DateTime.UtcNow.AddMonths(3),
            Status = MediaStatus.Active,
            IsReleasedForUse = true
        };
        db.Media.Add(media);
        await db.SaveChangesAsync();

        var equipResult = await searchService.SearchAsync(new GlobalSearchRequest("INC-TEST"), userA.Id);
        Assert.Contains(equipResult.Categories, c => c.CategoryName == "Equipment");

        var mediaResult = await searchService.SearchAsync(new GlobalSearchRequest("MED-LOT-9988"), userA.Id);
        Assert.Contains(mediaResult.Categories, c => c.CategoryName == "Media");
    }

    [Fact]
    public async Task SearchAsync_CategoryFilter_RestrictsCategoriesReturned()
    {
        await using var db = NewDb();
        var (secA, secB) = SeedSections(db);
        var (userA, _, _) = SeedUsers(db, secA, secB);
        var scope = new UserSectionScopeService(db);
        var searchService = new GlobalSearchService(db, scope);

        var sample = new Sample { ReferenceNumber = "SAMPLE-FILTER-1", ControlNumber = "C1", Status = SampleStatus.Received };
        sample.TestOrders.Add(new TestOrder { TestCode = "TAMC", SectionId = secA.Id, IsSuperseded = false });
        db.Samples.Add(sample);

        var equip = new EquipmentInventory
        {
            Code = "SAMPLE-EQUIP-1",
            InstrumentType = "Filter Unit",
            SectionId = secA.Id
        };
        db.EquipmentInventories.Add(equip);
        await db.SaveChangesAsync();

        // Target only "Equipment"
        var result = await searchService.SearchAsync(new GlobalSearchRequest("SAMPLE", Category: "Equipment"), userA.Id);

        Assert.Single(result.Categories);
        Assert.Equal("Equipment", result.Categories[0].CategoryName);
    }

    [Fact]
    public void DbModel_SampleAndMaterial_ContainSearchIndexes()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new MicroLimsDbContext(options);

        var sampleType = db.Model.FindEntityType(typeof(Sample));
        Assert.NotNull(sampleType);
        var sampleIndexes = sampleType.GetIndexes().Select(i => string.Join(",", i.Properties.Select(p => p.Name))).ToList();
        Assert.Contains("BatchNumber", sampleIndexes);
        Assert.Contains("ControlNumber", sampleIndexes);

        var materialType = db.Model.FindEntityType(typeof(Material));
        Assert.NotNull(materialType);
        var materialIndexes = materialType.GetIndexes().Select(i => string.Join(",", i.Properties.Select(p => p.Name))).ToList();
        Assert.Contains("BatchNumber", materialIndexes);
        Assert.Contains("MaterialName", materialIndexes);
    }

    [Fact]
    public void Controller_ReadsTheSearchTextFromQ_TheParameterTheFrontendSends()
    {
        var query = typeof(GlobalSearchController).GetMethod(nameof(GlobalSearchController.Search))!.GetParameters()[0];
        Assert.Equal("q", query.GetCustomAttribute<FromQueryAttribute>()!.Name);
    }
}
