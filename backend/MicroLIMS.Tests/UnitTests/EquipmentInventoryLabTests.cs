using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.InventoryTests;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

// Task 8 - every equipment inventory asset belongs to one laboratory.
// Mirrors the pattern in InventoryTests/MaterialSectionScopeTests.cs.
public class EquipmentInventoryLabTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static (DocumentSection microSec, DocumentSection fpSec, User microUser, User adminUser) SeedBase(MicroLimsDbContext db)
    {
        var microSec = TestServiceFactory.EnsureMicroSection(db);

        var fpSec = db.DocumentSections.FirstOrDefault(s => s.Code == "FP");
        if (fpSec == null)
        {
            fpSec = new DocumentSection
            {
                Name = "Finished Product Laboratory",
                Code = "FP",
                DepartmentId = microSec.DepartmentId,
                IsActive = true
            };
            db.DocumentSections.Add(fpSec);
            db.SaveChanges();
        }

        var analystRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.Analyst);
        if (analystRole == null)
        {
            analystRole = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
            db.Roles.Add(analystRole);
            db.SaveChanges();
        }

        var adminRole = db.Roles.FirstOrDefault(r => r.Type == RoleType.SystemAdministrator);
        if (adminRole == null)
        {
            adminRole = new Role { Name = "System Administrator", Type = RoleType.SystemAdministrator, IsActive = true };
            db.Roles.Add(adminRole);
            db.SaveChanges();
        }

        var microUser = new User
        {
            Username = "microUser_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Micro Analyst",
            RoleId = analystRole.Id,
            Role = analystRole,
            IsActive = true
        };
        var adminUser = new User
        {
            Username = "adminUser_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Sys Admin",
            RoleId = adminRole.Id,
            Role = adminRole,
            IsActive = true
        };
        db.Users.AddRange(microUser, adminUser);
        db.SaveChanges();

        TestServiceFactory.AssignUserToMicroSection(db, microUser.Id);

        return (microSec, fpSec, microUser, adminUser);
    }

    private static EquipmentInventory SeedAsset(MicroLimsDbContext db, int? sectionId, string code)
    {
        var asset = new EquipmentInventory
        {
            InstrumentType = "Incubator",
            ManufacturerName = "Memmert",
            Code = code,
            Location = "Lab",
            Status = EquipmentOperationalStatus.InService,
            SectionId = sectionId,
            CreatedByUserId = 1,
            LastModifiedByUserId = 1
        };
        db.EquipmentInventories.Add(asset);
        db.SaveChanges();
        return asset;
    }

    private static SaveEquipmentInventoryRequest BuildRequest(string code, int? sectionId) =>
        new(
            InstrumentType: "Incubator",
            ManufacturerName: "Memmert",
            SerialNumber: null,
            FirmwareVersion: null,
            Code: code,
            Location: "Lab",
            CalibrationDueDate: null,
            Status: EquipmentOperationalStatus.InService,
            StatusChangeComment: null,
            SectionId: sectionId);

    [Fact]
    public async Task Create_WithoutSectionId_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var (_, _, microUser, _) = SeedBase(db);
        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        var request = BuildRequest("INC-NEW-01", sectionId: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(request, microUser.Id));
        Assert.Equal("Choose the laboratory this asset belongs to.", ex.Message);
    }

    [Fact]
    public async Task List_ShowsOnlyInScopeAssets_AdminSeesAll()
    {
        await using var db = NewDb();
        var (microSec, fpSec, microUser, adminUser) = SeedBase(db);

        var microAsset = SeedAsset(db, microSec.Id, "INC-MICRO-01");
        var fpAsset = SeedAsset(db, fpSec.Id, "INC-FP-01");

        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));
        var scopeService = new UserSectionScopeService(db);

        var microScope = await scopeService.GetAccessibleSectionIdsAsync(microUser.Id);
        var microList = await service.GetAllAsync(microScope);
        Assert.Single(microList);
        Assert.Equal(microAsset.Id, microList[0].Id);

        var adminScope = await scopeService.GetAccessibleSectionIdsAsync(adminUser.Id);
        var adminList = await service.GetAllAsync(adminScope);
        Assert.Equal(2, adminList.Count);
        Assert.Contains(adminList, e => e.Id == microAsset.Id);
        Assert.Contains(adminList, e => e.Id == fpAsset.Id);
    }

    [Fact]
    public async Task GetById_OutOfScopeAsset_ThrowsUnauthorizedAccessException()
    {
        await using var db = NewDb();
        var (_, fpSec, microUser, _) = SeedBase(db);
        var fpAsset = SeedAsset(db, fpSec.Id, "INC-FP-02");

        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetByIdAsync(fpAsset.Id, microUser.Id));
        Assert.Equal("This equipment belongs to a laboratory section you are not assigned to.", ex.Message);
    }

    [Fact]
    public async Task Create_OutOfScopeSection_ThrowsUnauthorizedAccessException()
    {
        await using var db = NewDb();
        var (_, fpSec, microUser, _) = SeedBase(db);
        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        var request = BuildRequest("INC-FP-NEW-01", sectionId: fpSec.Id);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(request, microUser.Id));
        Assert.Equal("This equipment belongs to a laboratory section you are not assigned to.", ex.Message);
    }

    [Fact]
    public async Task GetById_AdminSeesAnyLabsAsset()
    {
        await using var db = NewDb();
        var (_, fpSec, _, adminUser) = SeedBase(db);
        var fpAsset = SeedAsset(db, fpSec.Id, "INC-FP-03");

        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        var found = await service.GetByIdAsync(fpAsset.Id, adminUser.Id);
        Assert.NotNull(found);
        Assert.Equal(fpAsset.Id, found!.Id);
    }

    [Fact]
    public async Task Create_InScopeSection_Succeeds()
    {
        await using var db = NewDb();
        var (microSec, _, microUser, _) = SeedBase(db);
        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        var request = BuildRequest("INC-MICRO-02", sectionId: microSec.Id);
        var created = await service.CreateAsync(request, microUser.Id);

        Assert.Equal(microSec.Id, created.SectionId);
    }

    [Fact]
    public async Task Update_WithoutSectionId_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var (microSec, _, microUser, _) = SeedBase(db);
        var asset = SeedAsset(db, microSec.Id, "INC-MICRO-03");
        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        var request = BuildRequest(asset.Code, sectionId: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(asset.Id, request, microUser.Id));
        Assert.Equal("Choose the laboratory this asset belongs to.", ex.Message);
    }

    [Fact]
    public async Task Update_OutOfScopeAsset_ThrowsUnauthorizedAccessException()
    {
        await using var db = NewDb();
        var (_, fpSec, microUser, _) = SeedBase(db);
        var fpAsset = SeedAsset(db, fpSec.Id, "INC-FP-04");
        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        var request = BuildRequest(fpAsset.Code, sectionId: fpSec.Id);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateAsync(fpAsset.Id, request, microUser.Id));
        Assert.Equal("This equipment belongs to a laboratory section you are not assigned to.", ex.Message);
    }

    [Fact]
    public async Task LegacyAssetWithNullSectionId_HiddenFromAnalyst_VisibleToAdmin()
    {
        await using var db = NewDb();
        var (_, _, microUser, adminUser) = SeedBase(db);
        var legacyAsset = SeedAsset(db, sectionId: null, code: "INC-LEGACY-01");

        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetByIdAsync(legacyAsset.Id, microUser.Id));
        Assert.Equal("This equipment belongs to a laboratory section you are not assigned to.", ex.Message);

        var found = await service.GetByIdAsync(legacyAsset.Id, adminUser.Id);
        Assert.NotNull(found);

        var scopeService = new UserSectionScopeService(db);
        var microScope = await scopeService.GetAccessibleSectionIdsAsync(microUser.Id);
        var microList = await service.GetAllAsync(microScope);
        Assert.DoesNotContain(microList, e => e.Id == legacyAsset.Id);

        var adminList = await service.GetAllAsync(null);
        Assert.Contains(adminList, e => e.Id == legacyAsset.Id);
    }

    // Fix round 1 - WhereIsItAsync must not leak another lab's sample/media
    // activity even in masked form.

    private static User SeedFpUser(MicroLimsDbContext db, DocumentSection fpSec)
    {
        var analystRole = db.Roles.First(r => r.Type == RoleType.Analyst);
        var fpUser = new User
        {
            Username = "fpUser_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "FP Analyst",
            RoleId = analystRole.Id,
            Role = analystRole,
            IsActive = true
        };
        db.Users.Add(fpUser);
        db.SaveChanges();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = fpUser.Id,
            DepartmentId = fpSec.DepartmentId,
            SectionId = fpSec.Id,
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();

        return fpUser;
    }

    [Fact]
    public async Task WhereIsIt_SampleReferenceInAnotherLab_HiddenFromMicroUser_VisibleToFpUserAndAdmin()
    {
        await using var db = NewDb();
        var (_, fpSec, microUser, _) = SeedBase(db);
        var fpUser = SeedFpUser(db, fpSec);

        var sample = new Sample { Id = 900, ReferenceNumber = "FP-REF-900", ControlNumber = "CTRL-900" };
        db.Samples.Add(sample);
        var testOrder = new TestOrder { Id = 901, SampleId = 900, TestCode = "ASSAY", SectionId = fpSec.Id };
        db.TestOrders.Add(testOrder);
        db.Incubations.Add(new Incubation
        {
            Id = 902,
            TestOrderId = 901,
            StepName = "Incubation Step",
            StartedAt = DateTime.UtcNow.AddHours(-1),
            IncubationStartUtc = DateTime.UtcNow.AddHours(-1),
            IncubationEndUtc = DateTime.UtcNow.AddHours(23),
            StartedByUserId = 1
        });
        await db.SaveChangesAsync();

        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));
        var scopeService = new UserSectionScopeService(db);

        var microScope = await scopeService.GetAccessibleSectionIdsAsync(microUser.Id);
        var microResult = await service.WhereIsItAsync("FP-REF-900", microScope);
        Assert.Null(microResult.CurrentActivity);
        Assert.Empty(microResult.History);

        var fpScope = await scopeService.GetAccessibleSectionIdsAsync(fpUser.Id);
        var fpResult = await service.WhereIsItAsync("FP-REF-900", fpScope);
        Assert.Single(fpResult.History);

        var adminResult = await service.WhereIsItAsync("FP-REF-900", null);
        Assert.Single(adminResult.History);
    }

    [Fact]
    public async Task WhereIsIt_MediaLotInAnotherLab_HiddenFromMicroUser_VisibleToFpUserAndAdmin()
    {
        await using var db = NewDb();
        var (_, fpSec, microUser, _) = SeedBase(db);
        var fpUser = SeedFpUser(db, fpSec);

        var material = new Material { Id = 950, SectionId = fpSec.Id, MaterialName = "FP Culture Media", Code = "FP-MED-950" };
        db.Materials.Add(material);
        db.Media.Add(new Media
        {
            Id = 951,
            MaterialId = 950,
            LotNumber = "FP-LOT-951",
            ManufacturerLot = "MFG-951",
            ManufacturerName = "Merck",
            ExpiryDate = DateTime.UtcNow.AddMonths(1),
            PreparedAt = DateTime.UtcNow.AddDays(-1),
            PreparedByUserId = 1,
            Status = MediaStatus.Active
        });
        // Media-only activity - no TestOrder, so scoping falls back to
        // Media.Material.SectionId (the SectionMediaRule rule).
        db.Incubations.Add(new Incubation
        {
            Id = 952,
            MediaId = 951,
            StepName = "Media Incubation",
            StartedAt = DateTime.UtcNow.AddHours(-1),
            IncubationStartUtc = DateTime.UtcNow.AddHours(-1),
            IncubationEndUtc = DateTime.UtcNow.AddHours(23),
            StartedByUserId = 1
        });
        await db.SaveChangesAsync();

        var service = new EquipmentInventoryService(db, new UserSectionScopeService(db));
        var scopeService = new UserSectionScopeService(db);

        var microScope = await scopeService.GetAccessibleSectionIdsAsync(microUser.Id);
        var microResult = await service.WhereIsItAsync("FP-LOT-951", microScope);
        Assert.Null(microResult.CurrentActivity);
        Assert.Empty(microResult.History);

        var fpScope = await scopeService.GetAccessibleSectionIdsAsync(fpUser.Id);
        var fpResult = await service.WhereIsItAsync("FP-LOT-951", fpScope);
        Assert.Single(fpResult.History);

        var adminResult = await service.WhereIsItAsync("FP-LOT-951", null);
        Assert.Single(adminResult.History);
    }
}
