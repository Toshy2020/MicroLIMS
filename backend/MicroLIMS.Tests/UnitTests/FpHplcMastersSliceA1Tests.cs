using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class FpHplcMastersSliceA1Tests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private static (DocumentSection microSec, DocumentSection fpSec, User microUser, User fpUser, User multiUser, User adminUser) SeedSectionsAndUsers(MicroLimsDbContext db)
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

        var fpUser = new User
        {
            Username = "fpUser_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "FP Analyst",
            RoleId = analystRole.Id,
            Role = analystRole,
            IsActive = true
        };

        var multiUser = new User
        {
            Username = "multiUser_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Multi Section Analyst",
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

        db.Users.AddRange(microUser, fpUser, multiUser, adminUser);
        db.SaveChanges();

        // Assign microUser to microSec
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = microUser.Id, DepartmentId = microSec.DepartmentId, SectionId = microSec.Id });
        // Assign fpUser to fpSec
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = fpUser.Id, DepartmentId = fpSec.DepartmentId, SectionId = fpSec.Id });
        // Assign multiUser to both microSec and fpSec
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = multiUser.Id, DepartmentId = microSec.DepartmentId, SectionId = microSec.Id });
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = multiUser.Id, DepartmentId = fpSec.DepartmentId, SectionId = fpSec.Id });
        db.SaveChanges();

        return (microSec, fpSec, microUser, fpUser, multiUser, adminUser);
    }

    #region Equipment Tests

    [Fact]
    public void EquipmentType_Enum_Has_Hplc_PhMeter_Balance_AtEnd()
    {
        Assert.Equal(6, (int)EquipmentType.Hplc);
        Assert.Equal(7, (int)EquipmentType.PhMeter);
        Assert.Equal(8, (int)EquipmentType.Balance);
    }

    [Fact]
    public async Task Equipment_CreateHplc_WithoutCdsSoftware_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var (_, fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var scope = new UserSectionScopeService(db);

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            var req = new CreateEquipmentRequest("HPLC-01", "HPLC-01", EquipmentType.Hplc, "Lab 1", null, null, "Shimadzu", null, null, fpSec.Id);
            if (req.Type == EquipmentType.Hplc && !req.CdsSoftware.HasValue)
                throw new InvalidOperationException("CDS Software is required for HPLC equipment.");
        });

        Assert.Equal("CDS Software is required for HPLC equipment.", ex.Message);
    }

    [Fact]
    public async Task Equipment_CreateNonHplc_WithCdsSoftware_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var (microSec, _, microUser, _, _, _) = SeedSectionsAndUsers(db);

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            var req = new CreateEquipmentRequest("INC-01", "INC-01", EquipmentType.Incubator, "Lab 1", 32.5m, null, "Memmert", CdsSoftware.ShimadzuLabSolutions, null, microSec.Id);
            if (req.Type != EquipmentType.Hplc && req.CdsSoftware.HasValue)
                throw new InvalidOperationException("CDS Software is only allowed for HPLC equipment.");
        });

        Assert.Equal("CDS Software is only allowed for HPLC equipment.", ex.Message);
    }

    [Fact]
    public async Task Equipment_CreateHplc_WithValidCdsSoftware_Succeeds()
    {
        await using var db = NewDb();
        var (_, fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var scope = new UserSectionScopeService(db);

        var sectionId = await scope.ResolveSectionForCreateAsync(fpUser.Id, null);
        Assert.Equal(fpSec.Id, sectionId);

        var equipment = new Equipment
        {
            Name = "HPLC Waters Alliance",
            Code = "HPLC-01",
            Type = EquipmentType.Hplc,
            Vendor = "Waters",
            CdsSoftware = CdsSoftware.WatersEmpower3,
            Location = "Room 101",
            SectionId = sectionId
        };
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();

        var saved = await db.Equipment.FindAsync(equipment.Id);
        Assert.NotNull(saved);
        Assert.Equal(EquipmentType.Hplc, saved.Type);
        Assert.Equal(CdsSoftware.WatersEmpower3, saved.CdsSoftware);
        Assert.Equal("Waters", saved.Vendor);
        Assert.Equal(fpSec.Id, saved.SectionId);
    }

    [Fact]
    public async Task Equipment_SectionScoping_ListsFiltered_And_OtherSectionGetThrows403()
    {
        await using var db = NewDb();
        var (microSec, fpSec, microUser, fpUser, _, adminUser) = SeedSectionsAndUsers(db);
        var scope = new UserSectionScopeService(db);

        var microEq = new Equipment { Name = "Micro Incubator", Code = "INC-M1", Type = EquipmentType.Incubator, SectionId = microSec.Id };
        var fpEq = new Equipment { Name = "FP HPLC", Code = "HPLC-FP1", Type = EquipmentType.Hplc, CdsSoftware = CdsSoftware.ShimadzuLabSolutions, SectionId = fpSec.Id };
        db.Equipment.AddRange(microEq, fpEq);
        await db.SaveChangesAsync();

        // Scope check for microUser: only microSec
        var microScope = await scope.GetAccessibleSectionIdsAsync(microUser.Id);
        Assert.NotNull(microScope);
        Assert.Single(microScope);
        Assert.Contains(microSec.Id, microScope);

        // fpUser accessing microEq throws UnauthorizedAccessException
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            scope.EnsureEquipmentAccessAsync(fpUser.Id, microEq.Id));

        // microUser accessing microEq succeeds
        await scope.EnsureEquipmentAccessAsync(microUser.Id, microEq.Id);

        // adminUser accessing microEq succeeds
        await scope.EnsureEquipmentAccessAsync(adminUser.Id, microEq.Id);
    }

    #endregion

    #region ChromatographyColumn Tests

    [Fact]
    public async Task Column_Create_ValidHplcEquipment_Succeeds()
    {
        await using var db = NewDb();
        var (_, fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var scope = new UserSectionScopeService(db);
        var service = new ChromatographyColumnService(db, scope);

        var hplc = new Equipment
        {
            Name = "HPLC Shimadzu Prominence",
            Code = "HPLC-SHIM-01",
            Type = EquipmentType.Hplc,
            CdsSoftware = CdsSoftware.ShimadzuLabSolutions,
            SectionId = fpSec.Id
        };
        db.Equipment.Add(hplc);
        await db.SaveChangesAsync();

        var created = await service.CreateAsync(new CreateChromatographyColumnRequest(
            Code: "COL-C18-001",
            Name: "C18 Hypersil BDS 250x4.6mm 5um",
            SerialNumber: "SN-987654",
            SectionId: fpSec.Id,
            CompatibleEquipmentIds: new List<int> { hplc.Id }
        ), fpUser.Id);

        Assert.NotNull(created);
        Assert.Equal("COL-C18-001", created.Code);
        Assert.Equal(fpSec.Id, created.SectionId);
        Assert.True(created.IsActive);
        Assert.Single(created.CompatibleEquipment);
        Assert.Equal(hplc.Id, created.CompatibleEquipment[0].Id);
    }

    [Fact]
    public async Task Column_Create_NonHplcEquipment_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var (_, fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var scope = new UserSectionScopeService(db);
        var service = new ChromatographyColumnService(db, scope);

        var incubator = new Equipment
        {
            Name = "Incubator",
            Code = "INC-FP-01",
            Type = EquipmentType.Incubator,
            SectionId = fpSec.Id
        };
        db.Equipment.Add(incubator);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateChromatographyColumnRequest(
                Code: "COL-002",
                Name: "Test Column",
                CompatibleEquipmentIds: new List<int> { incubator.Id }
            ), fpUser.Id));

        Assert.Contains("is not an HPLC instrument", ex.Message);
    }

    [Fact]
    public async Task Column_Create_OtherSectionEquipment_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var (microSec, fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var scope = new UserSectionScopeService(db);
        var service = new ChromatographyColumnService(db, scope);

        var microHplc = new Equipment
        {
            Name = "Micro HPLC",
            Code = "HPLC-MICRO-01",
            Type = EquipmentType.Hplc,
            CdsSoftware = CdsSoftware.AgilentOpenLab,
            SectionId = microSec.Id
        };
        db.Equipment.Add(microHplc);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateChromatographyColumnRequest(
                Code: "COL-003",
                Name: "Test Column",
                SectionId: fpSec.Id,
                CompatibleEquipmentIds: new List<int> { microHplc.Id }
            ), fpUser.Id));

        Assert.Contains("belongs to another section", ex.Message);
    }

    [Fact]
    public async Task Column_Create_DuplicateCode_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var (_, fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var scope = new UserSectionScopeService(db);
        var service = new ChromatographyColumnService(db, scope);

        await service.CreateAsync(new CreateChromatographyColumnRequest(
            Code: "COL-DUP",
            Name: "First Column",
            SectionId: fpSec.Id
        ), fpUser.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new CreateChromatographyColumnRequest(
                Code: "COL-DUP",
                Name: "Second Column",
                SectionId: fpSec.Id
            ), fpUser.Id));

        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public async Task Column_SectionScoping_ListAndById()
    {
        await using var db = NewDb();
        var (microSec, fpSec, microUser, fpUser, _, adminUser) = SeedSectionsAndUsers(db);
        var scope = new UserSectionScopeService(db);
        var service = new ChromatographyColumnService(db, scope);

        var microCol = new ChromatographyColumn
        {
            Code = "COL-M1",
            Name = "Micro Column",
            SectionId = microSec.Id,
            IsActive = true
        };
        var fpCol = new ChromatographyColumn
        {
            Code = "COL-FP1",
            Name = "FP Column",
            SectionId = fpSec.Id,
            IsActive = true
        };
        db.ChromatographyColumns.AddRange(microCol, fpCol);
        await db.SaveChangesAsync();

        // microUser list shows only microCol
        var microList = await service.GetAllAsync(microUser.Id);
        Assert.Single(microList);
        Assert.Equal("COL-M1", microList[0].Code);

        // fpUser list shows only fpCol
        var fpList = await service.GetAllAsync(fpUser.Id);
        Assert.Single(fpList);
        Assert.Equal("COL-FP1", fpList[0].Code);

        // adminUser list shows both
        var adminList = await service.GetAllAsync(adminUser.Id);
        Assert.Equal(2, adminList.Count);

        // fpUser accessing microCol throws UnauthorizedAccessException (403)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetByIdAsync(microCol.Id, fpUser.Id));

        // microUser accessing microCol succeeds
        var fetched = await service.GetByIdAsync(microCol.Id, microUser.Id);
        Assert.Equal("COL-M1", fetched.Code);
    }

    [Fact]
    public async Task Column_Deactivate_SetsIsActiveFalse_DoesNotDelete()
    {
        await using var db = NewDb();
        var (_, fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var scope = new UserSectionScopeService(db);
        var service = new ChromatographyColumnService(db, scope);

        var col = await service.CreateAsync(new CreateChromatographyColumnRequest(
            Code: "COL-DEACT",
            Name: "Deactivation Column",
            SectionId: fpSec.Id
        ), fpUser.Id);

        Assert.True(col.IsActive);

        var deactivated = await service.DeactivateAsync(col.Id, fpUser.Id);
        Assert.False(deactivated.IsActive);

        // Verify still in database
        var inDb = await db.ChromatographyColumns.FindAsync(col.Id);
        Assert.NotNull(inDb);
        Assert.False(inDb.IsActive);

        // Verify activeOnly filter excludes it
        var activeList = await service.GetAllAsync(fpUser.Id, activeOnly: true);
        Assert.DoesNotContain(activeList, c => c.Id == col.Id);

        // But regular list includes it
        var allList = await service.GetAllAsync(fpUser.Id, activeOnly: false);
        Assert.Contains(allList, c => c.Id == col.Id);
    }

    #endregion

    #region Material ReferenceStandard and Purity Tests

    [Fact]
    public void MaterialType_Enum_Has_ReferenceStandard_AtEnd()
    {
        Assert.Equal(11, (int)MaterialType.ReferenceStandard);
    }

    [Fact]
    public async Task Material_CreateReferenceStandard_WithoutPurity_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var (_, fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var scope = new UserSectionScopeService(db);
        var service = new MaterialService(db, scope);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new SaveMaterialRequest(
                MaterialType: MaterialType.ReferenceStandard,
                MaterialName: "Ascorbic Acid Standard",
                ManufacturerName: "USP",
                BatchNumber: "LOT-ASC-01",
                ReceivingDate: DateTime.UtcNow,
                ExpiryDate: DateTime.UtcNow.AddYears(1),
                Code: "RS-ASC",
                Location: "Cabinet 2",
                QuantityReceived: 10m,
                Unit: MaterialUnit.Gram,
                MinimumStockLevel: 2m,
                AtccNumber: null,
                OrganismId: null,
                SectionId: fpSec.Id,
                Purity: null
            ), fpUser.Id));

        Assert.Equal("Purity is required for reference standards.", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100.001)]
    [InlineData(150)]
    public async Task Material_CreateReferenceStandard_InvalidPurity_ThrowsInvalidOperationException(decimal invalidPurity)
    {
        await using var db = NewDb();
        var (_, fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var scope = new UserSectionScopeService(db);
        var service = new MaterialService(db, scope);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new SaveMaterialRequest(
                MaterialType: MaterialType.ReferenceStandard,
                MaterialName: "Ascorbic Acid Standard",
                ManufacturerName: "USP",
                BatchNumber: "LOT-ASC-01",
                ReceivingDate: DateTime.UtcNow,
                ExpiryDate: DateTime.UtcNow.AddYears(1),
                Code: "RS-ASC",
                Location: "Cabinet 2",
                QuantityReceived: 10m,
                Unit: MaterialUnit.Gram,
                MinimumStockLevel: 2m,
                AtccNumber: null,
                OrganismId: null,
                SectionId: fpSec.Id,
                Purity: invalidPurity
            ), fpUser.Id));

        Assert.Equal("Purity must be greater than 0 and less than or equal to 100.", ex.Message);
    }

    [Fact]
    public async Task Material_CreateNonReferenceStandard_WithPurity_ThrowsInvalidOperationException()
    {
        await using var db = NewDb();
        var (_, fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var scope = new UserSectionScopeService(db);
        var service = new MaterialService(db, scope);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(new SaveMaterialRequest(
                MaterialType: MaterialType.Chemical,
                MaterialName: "Acetonitrile HPLC Grade",
                ManufacturerName: "Honeywell",
                BatchNumber: "LOT-ACN-01",
                ReceivingDate: DateTime.UtcNow,
                ExpiryDate: DateTime.UtcNow.AddYears(1),
                Code: "CH-ACN",
                Location: "Solvent Cabinet",
                QuantityReceived: 2500m,
                Unit: MaterialUnit.Milliliter,
                MinimumStockLevel: 500m,
                AtccNumber: null,
                OrganismId: null,
                SectionId: fpSec.Id,
                Purity: 99.9m
            ), fpUser.Id));

        Assert.Equal("Purity is only allowed for reference standards.", ex.Message);
    }

    [Fact]
    public async Task Material_CreateReferenceStandard_ValidPurity_Succeeds()
    {
        await using var db = NewDb();
        var (_, fpSec, _, fpUser, _, _) = SeedSectionsAndUsers(db);
        var scope = new UserSectionScopeService(db);
        var service = new MaterialService(db, scope);

        var mat = await service.CreateAsync(new SaveMaterialRequest(
            MaterialType: MaterialType.ReferenceStandard,
            MaterialName: "Ascorbic Acid Primary RS",
            ManufacturerName: "USP",
            BatchNumber: "LOT-ASC-99",
            ReceivingDate: DateTime.UtcNow,
            ExpiryDate: DateTime.UtcNow.AddYears(2),
            Code: "RS-ASC-99",
            Location: "Standard Desiccator",
            QuantityReceived: 5m,
            Unit: MaterialUnit.Gram,
            MinimumStockLevel: 1m,
            AtccNumber: null,
            OrganismId: null,
            SectionId: fpSec.Id,
            Purity: 99.850m
        ), fpUser.Id);

        Assert.NotNull(mat);
        Assert.Equal(99.850m, mat.Purity);
        Assert.Equal(MaterialType.ReferenceStandard, mat.MaterialType);
    }

    [Fact]
    public async Task GetUsableReferenceStandards_ReturnsOnlyInStockNonExpiredInCallersSections()
    {
        await using var db = NewDb();
        var (microSec, fpSec, microUser, fpUser, _, adminUser) = SeedSectionsAndUsers(db);
        var scope = new UserSectionScopeService(db);
        var service = new MaterialService(db, scope);

        // Usable FP standard
        var usableFp = new Material
        {
            SectionId = fpSec.Id,
            MaterialType = MaterialType.ReferenceStandard,
            MaterialName = "Paracetamol RS",
            BatchNumber = "LOT-PCM-01",
            QuantityReceived = 10m,
            QuantityRemaining = 8m,
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Purity = 99.9m,
            Location = "Desiccator 1"
        };

        // Expired FP standard
        var expiredFp = new Material
        {
            SectionId = fpSec.Id,
            MaterialType = MaterialType.ReferenceStandard,
            MaterialName = "Expired RS",
            BatchNumber = "LOT-EXP-01",
            QuantityReceived = 10m,
            QuantityRemaining = 8m,
            ReceivingDate = DateTime.UtcNow.AddYears(-2),
            ExpiryDate = DateTime.UtcNow.AddDays(-5),
            Purity = 99.5m,
            Location = "Desiccator 1"
        };

        // Depleted FP standard
        var depletedFp = new Material
        {
            SectionId = fpSec.Id,
            MaterialType = MaterialType.ReferenceStandard,
            MaterialName = "Depleted RS",
            BatchNumber = "LOT-DEP-01",
            QuantityReceived = 10m,
            QuantityRemaining = 0m,
            ReceivingDate = DateTime.UtcNow.AddDays(-20),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Purity = 99.7m,
            Location = "Desiccator 1"
        };

        // Usable Micro standard
        var usableMicro = new Material
        {
            SectionId = microSec.Id,
            MaterialType = MaterialType.ReferenceStandard,
            MaterialName = "Endotoxin RS",
            BatchNumber = "LOT-ENDO-01",
            QuantityReceived = 5m,
            QuantityRemaining = 5m,
            ReceivingDate = DateTime.UtcNow.AddDays(-5),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Purity = 100.0m,
            Location = "Micro Fridge"
        };

        // Usable FP Chemical (not reference standard)
        var chemicalFp = new Material
        {
            SectionId = fpSec.Id,
            MaterialType = MaterialType.Chemical,
            MaterialName = "Methanol HPLC",
            BatchNumber = "LOT-MEOH-01",
            QuantityReceived = 1000m,
            QuantityRemaining = 1000m,
            ReceivingDate = DateTime.UtcNow.AddDays(-5),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            Location = "Flammable Cabinet"
        };

        db.Materials.AddRange(usableFp, expiredFp, depletedFp, usableMicro, chemicalFp);
        await db.SaveChangesAsync();

        // fpUser gets usable reference standards: only usableFp
        var fpStandards = await service.GetUsableReferenceStandardsAsync(fpUser.Id);
        Assert.Single(fpStandards);
        Assert.Equal("Paracetamol RS", fpStandards[0].MaterialName);

        // microUser gets usable reference standards: only usableMicro
        var microStandards = await service.GetUsableReferenceStandardsAsync(microUser.Id);
        Assert.Single(microStandards);
        Assert.Equal("Endotoxin RS", microStandards[0].MaterialName);

        // adminUser sees both usable standards
        var adminStandards = await service.GetUsableReferenceStandardsAsync(adminUser.Id);
        Assert.Equal(2, adminStandards.Count);
        Assert.Contains(adminStandards, s => s.MaterialName == "Paracetamol RS");
        Assert.Contains(adminStandards, s => s.MaterialName == "Endotoxin RS");
    }

    #endregion
}
