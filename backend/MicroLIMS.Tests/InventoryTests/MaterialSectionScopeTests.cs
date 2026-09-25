using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

public class MaterialSectionScopeTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static (DocumentSection microSec, DocumentSection otherSec, User microUser, User adminUser) SeedBase(MicroLimsDbContext db)
    {
        var microSec = TestServiceFactory.EnsureMicroSection(db);

        var otherSec = db.DocumentSections.FirstOrDefault(s => s.Code == "CHEM");
        if (otherSec == null)
        {
            otherSec = new DocumentSection
            {
                Name = "Chemistry Laboratory",
                Code = "CHEM",
                DepartmentId = microSec.DepartmentId,
                IsActive = true
            };
            db.DocumentSections.Add(otherSec);
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

        return (microSec, otherSec, microUser, adminUser);
    }

    private static Material SeedMaterial(MicroLimsDbContext db, int sectionId, string name = "Reagent A", decimal qty = 100m)
    {
        var material = new Material
        {
            SectionId = sectionId,
            MaterialType = MaterialType.Chemical,
            MaterialName = name,
            ManufacturerName = "Merck",
            BatchNumber = "LOT-" + Guid.NewGuid().ToString("N")[..8],
            ReceivingDate = DateTime.UtcNow.AddDays(-5),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Location = "Shelf 1",
            QuantityReceived = qty,
            QuantityRemaining = qty,
            Unit = MaterialUnit.Gram,
            CreatedByUserId = 1,
            LastModifiedByUserId = 1
        };
        db.Materials.Add(material);
        db.SaveChanges();
        return material;
    }

    private static MaterialDocumentService BuildDocService(MicroLimsDbContext db)
    {
        var validator = new MaterialDocumentFileValidator(maxFileSizeBytes: 26_214_400L);
        return new MaterialDocumentService(
            db,
            new InMemoryFileStorageService(),
            validator,
            NullLogger<MaterialDocumentService>.Instance,
            new UserSectionScopeService(db));
    }

    [Fact]
    public async Task List_ShowsOnlyInScopeMaterials_AdminSeesAll()
    {
        await using var db = NewDb();
        var (microSec, otherSec, microUser, adminUser) = SeedBase(db);

        var microMat = SeedMaterial(db, microSec.Id, "Micro Peptone");
        var otherMat = SeedMaterial(db, otherSec.Id, "Chem Acetone");

        var service = new MaterialService(db, new UserSectionScopeService(db));

        var microList = await service.GetAllAsync(microUser.Id);
        Assert.Single(microList);
        Assert.Equal(microMat.Id, microList[0].Id);

        var adminList = await service.GetAllAsync(adminUser.Id);
        Assert.Equal(2, adminList.Count);
        Assert.Contains(adminList, m => m.Id == microMat.Id);
        Assert.Contains(adminList, m => m.Id == otherMat.Id);

        var microPrint = await service.GetForPrintAsync(microUser.Id);
        Assert.Single(microPrint);
        Assert.Equal(microMat.Id, microPrint[0].Id);

        var adminPrint = await service.GetForPrintAsync(adminUser.Id);
        Assert.Equal(2, adminPrint.Count);
    }

    [Fact]
    public async Task Consume_OutOfScopeMaterial_ThrowsUnauthorizedAccessException_AndLeavesQuantityUnchanged()
    {
        await using var db = NewDb();
        var (microSec, otherSec, microUser, _) = SeedBase(db);

        var otherMat = SeedMaterial(db, otherSec.Id, "Chem Acetone", qty: 100m);
        var service = new MaterialService(db, new UserSectionScopeService(db));

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ConsumeAsync(otherMat.Id, MaterialType.Chemical, 10m, microUser.Id));
        Assert.Equal("This material belongs to a laboratory section you are not assigned to.", ex.Message);

        var reloaded = await db.Materials.FindAsync(otherMat.Id);
        Assert.Equal(100m, reloaded!.QuantityRemaining);
    }

    [Fact]
    public async Task Update_OutOfScopeMaterial_ThrowsUnauthorizedAccessException()
    {
        await using var db = NewDb();
        var (_, otherSec, microUser, _) = SeedBase(db);

        var otherMat = SeedMaterial(db, otherSec.Id, "Chem Acetone", qty: 100m);
        var service = new MaterialService(db, new UserSectionScopeService(db));

        var updateReq = new SaveMaterialRequest(
            MaterialType: MaterialType.Chemical,
            MaterialName: "Updated Name",
            ManufacturerName: "Merck",
            BatchNumber: otherMat.BatchNumber,
            ReceivingDate: otherMat.ReceivingDate,
            ExpiryDate: otherMat.ExpiryDate,
            Code: null,
            Location: "Shelf 2",
            QuantityReceived: 100m,
            Unit: MaterialUnit.Gram,
            MinimumStockLevel: null,
            AtccNumber: null,
            OrganismId: null);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateAsync(otherMat.Id, updateReq, microUser.Id));
        Assert.Equal("This material belongs to a laboratory section you are not assigned to.", ex.Message);
    }

    [Fact]
    public async Task Update_NeverChangesSectionId()
    {
        await using var db = NewDb();
        var (microSec, otherSec, microUser, _) = SeedBase(db);

        var microMat = SeedMaterial(db, microSec.Id, "Micro Buffer", qty: 50m);
        var service = new MaterialService(db, new UserSectionScopeService(db));

        var updateReq = new SaveMaterialRequest(
            MaterialType: MaterialType.Chemical,
            MaterialName: "Renamed Buffer",
            ManufacturerName: "Merck",
            BatchNumber: microMat.BatchNumber,
            ReceivingDate: microMat.ReceivingDate,
            ExpiryDate: microMat.ExpiryDate,
            Code: null,
            Location: "Shelf 9",
            QuantityReceived: 50m,
            Unit: MaterialUnit.Gram,
            MinimumStockLevel: null,
            AtccNumber: null,
            OrganismId: null,
            SectionId: otherSec.Id); // Intentionally attempting to change SectionId to otherSec

        await service.UpdateAsync(microMat.Id, updateReq, microUser.Id);

        var reloaded = await db.Materials.FindAsync(microMat.Id);
        Assert.Equal("Renamed Buffer", reloaded!.MaterialName);
        Assert.Equal(microSec.Id, reloaded.SectionId); // SectionId remains untouched!
    }

    [Fact]
    public async Task MaterialDocument_Upload_OutOfScopeMaterial_ThrowsUnauthorizedAccessException()
    {
        await using var db = NewDb();
        var (_, otherSec, microUser, _) = SeedBase(db);

        var otherMat = SeedMaterial(db, otherSec.Id, "Chem Solvent");
        var docService = BuildDocService(db);

        var uploadReq = new UploadMaterialDocumentRequest(
            DocumentType: MaterialDocumentType.SDS,
            OriginalFileName: "safety-sheet.pdf",
            DeclaredContentType: "application/pdf",
            Content: new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 });

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            docService.UploadAsync(otherMat.Id, uploadReq, microUser.Id));
        Assert.Equal("This material belongs to a laboratory section you are not assigned to.", ex.Message);
    }
}
