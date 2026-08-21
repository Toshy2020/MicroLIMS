using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

public class EquipmentConfigurationTests
{
    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);

        if (!db.Users.Any())
        {
            db.Users.Add(new User
            {
                Id = 1,
                FullName = "Sara Ahmed",
                Username = "sahmed",
                PasswordHash = "hash",
                RoleId = 1,
                IsActive = true
            });
            db.SaveChanges();
        }

        return db;
    }

    [Fact]
    public void ReconciliationQuery_IsSqlTranslatable_AndSeedingExecutesCleanly()
    {
        using var db = NewDb();

        // Seed initial data
        MicroLIMS.Persistence.Seed.DbSeeder.Seed(db);

        // Verify INCUCELL
        var incucellMaster = db.Equipment.FirstOrDefault(e => e.Code == "INC-F-ML-F-01-002");
        Assert.NotNull(incucellMaster);
        Assert.Equal("INCUCELL", incucellMaster.Name);

        var incucellInv = db.EquipmentInventories.FirstOrDefault(i => i.Code == "INC-F-ML-F-01-002");
        Assert.NotNull(incucellInv);
        Assert.Equal("D,141445", incucellInv.SerialNumber);

        // Verify Hirayama
        var hirayamaMaster = db.Equipment.FirstOrDefault(e => e.Code == "AUT-F-ML-F-03-045");
        Assert.NotNull(hirayamaMaster);
        Assert.Equal("Hirayama", hirayamaMaster.Name);

        var hirayamaInv = db.EquipmentInventories.FirstOrDefault(i => i.Code == "AUT-F-ML-F-03-045");
        Assert.NotNull(hirayamaInv);
        Assert.Equal("30317012128", hirayamaInv.SerialNumber);
    }

    [Fact]
    public async Task RealInventoryEquipment_IncucellAndHirayama_SummaryIncludesSerialNumbers()
    {
        using var db = NewDb();

        db.Equipment.AddRange(
            new Equipment { Id = 10, Name = "INCUCELL", Code = "INC-F-ML-F-01-002", Type = EquipmentType.Incubator, SetPointTemperature = 36.5m },
            new Equipment { Id = 20, Name = "Hirayama", Code = "AUT-F-ML-F-03-045", Type = EquipmentType.Autoclave }
        );

        db.EquipmentInventories.AddRange(
            new EquipmentInventory { Id = 10, Code = "INC-F-ML-F-01-002", InstrumentType = "INCUCELL", ManufacturerName = "INCUCELL", SerialNumber = "D,141445", Location = "Instruments room F-ML-F-01", Status = EquipmentOperationalStatus.InService },
            new EquipmentInventory { Id = 20, Code = "AUT-F-ML-F-03-045", InstrumentType = "Hirayama", ManufacturerName = "Hirayama", SerialNumber = "30317012128", Location = "Sterilization room F-ML-F-04", Status = EquipmentOperationalStatus.InService }
        );

        await db.SaveChangesAsync();

        var service = new EquipmentConfigurationService(db);
        var summary = await service.GetConfiguredEquipmentSummaryAsync();

        var incucell = Assert.Single(summary, e => e.Name == "INCUCELL");
        Assert.Equal("D,141445", incucell.SerialNumber);
        Assert.Equal("INC-F-ML-F-01-002", incucell.Code);

        var hirayama = Assert.Single(summary, e => e.Name == "Hirayama");
        Assert.Equal("30317012128", hirayama.SerialNumber);
        Assert.Equal("AUT-F-ML-F-03-045", hirayama.Code);
    }

    [Fact]
    public async Task RealInventoryIncubator_IsUsedByLaboratoryConfiguration()
    {
        using var db = NewDb();
        db.Equipment.Add(new Equipment { Id = 1, Name = "INCUCELL", Code = "INC-F-ML-F-01-002", Type = EquipmentType.Incubator, SetPointTemperature = 36.5m });
        db.EquipmentInventories.Add(new EquipmentInventory { Id = 10, Code = "INC-F-ML-F-01-002", InstrumentType = "INCUCELL", ManufacturerName = "INCUCELL", SerialNumber = "D,141445", Status = EquipmentOperationalStatus.InService });
        await db.SaveChangesAsync();

        var service = new EquipmentConfigurationService(db);
        var summary = await service.GetConfiguredEquipmentSummaryAsync();

        var incucell = Assert.Single(summary, e => e.Code == "INC-F-ML-F-01-002");
        Assert.Equal("INCUCELL", incucell.Name);
        Assert.Equal("D,141445", incucell.SerialNumber);
        Assert.Equal(10, incucell.EquipmentInventoryId);
    }

    [Fact]
    public async Task RealInventoryAutoclave_IsUsedByLaboratoryConfiguration()
    {
        using var db = NewDb();
        db.Equipment.Add(new Equipment { Id = 2, Name = "Hirayama", Code = "AUT-F-ML-F-03-045", Type = EquipmentType.Autoclave });
        db.EquipmentInventories.Add(new EquipmentInventory { Id = 20, Code = "AUT-F-ML-F-03-045", InstrumentType = "Hirayama", ManufacturerName = "Hirayama", SerialNumber = "30317012128", Status = EquipmentOperationalStatus.InService });
        await db.SaveChangesAsync();

        var service = new EquipmentConfigurationService(db);
        var summary = await service.GetConfiguredEquipmentSummaryAsync();

        var hirayama = Assert.Single(summary, e => e.Code == "AUT-F-ML-F-03-045");
        Assert.Equal("Hirayama", hirayama.Name);
        Assert.Equal("30317012128", hirayama.SerialNumber);
        Assert.Equal(20, hirayama.EquipmentInventoryId);
    }

    [Fact]
    public async Task PlaceholderEquipment_DoesNotCrossMatchBySeparateAutoIncrementId()
    {
        using var db = NewDb();
        // Equipment Id = 1 is placeholder INC-03
        db.Equipment.Add(new Equipment { Id = 1, Name = "Incubator 03", Code = "INC-03", Type = EquipmentType.Incubator });

        // EquipmentInventory Id = 1 is Qualitemp (Serial 61631/02) with Code INC-F-ML-F-01-003
        db.EquipmentInventories.Add(new EquipmentInventory { Id = 1, Code = "INC-F-ML-F-01-003", InstrumentType = "Qualitemp", SerialNumber = "61631/02", Status = EquipmentOperationalStatus.InService });

        await db.SaveChangesAsync();

        var service = new EquipmentConfigurationService(db);
        var summary = await service.GetConfiguredEquipmentSummaryAsync();

        var inc03 = Assert.Single(summary, e => e.Code == "INC-03");
        // Because Id matching across separate tables is removed, INC-03 does NOT falsely match Qualitemp (Serial 61631/02)
        Assert.Null(inc03.SerialNumber);
        Assert.Null(inc03.EquipmentInventoryId);
    }

    [Fact]
    public async Task NoDuplicatePhysicalEquipment()
    {
        using var db = NewDb();
        db.EquipmentInventories.AddRange(
            new EquipmentInventory { Id = 1, Code = "INC-F-ML-F-01-002", InstrumentType = "INCUCELL", SerialNumber = "D,141445", Status = EquipmentOperationalStatus.InService },
            new EquipmentInventory { Id = 2, Code = "AUT-F-ML-F-03-045", InstrumentType = "Hirayama", SerialNumber = "30317012128", Status = EquipmentOperationalStatus.InService }
        );
        await db.SaveChangesAsync();

        var incucellList = await db.EquipmentInventories.Where(i => i.SerialNumber == "D,141445").ToListAsync();
        var hirayamaList = await db.EquipmentInventories.Where(i => i.SerialNumber == "30317012128").ToListAsync();

        Assert.Single(incucellList);
        Assert.Single(hirayamaList);
    }

    [Fact]
    public async Task UpdateIncubatorSetPoint_ValidatesReason_UpdatesSetPoint_AndLogsHistory()
    {
        using var db = NewDb();
        var incubator = new Equipment
        {
            Id = 1,
            Name = "INCUCELL",
            Code = "INC-F-ML-F-01-002",
            Type = EquipmentType.Incubator,
            SetPointTemperature = 31.0m
        };
        db.Equipment.Add(incubator);
        await db.SaveChangesAsync();

        var service = new EquipmentConfigurationService(db);

        // 1. Missing reason throws
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateIncubatorSetPointAsync(1, new UpdateIncubatorSetPointRequest(32.5m, ""), 1));

        // 2. Valid update succeeds
        var updated = await service.UpdateIncubatorSetPointAsync(1, new UpdateIncubatorSetPointRequest(32.5m, "Routine adjustment"), 1);
        Assert.Equal(32.5m, updated.SetPointTemperature);

        // 3. History recorded
        var history = await service.GetIncubatorSetPointHistoryAsync(1);
        var entry = Assert.Single(history);
        Assert.Equal(31.0m, entry.PreviousSetPoint);
        Assert.Equal(32.5m, entry.NewSetPoint);
        Assert.Equal("Routine adjustment", entry.Reason);
        Assert.Equal("Sara Ahmed", entry.ChangedByName);
    }

    [Fact]
    public async Task NonIncubatorSetPointUpdate_ThrowsException()
    {
        using var db = NewDb();
        var autoclave = new Equipment
        {
            Id = 2,
            Name = "Hirayama",
            Code = "AUT-F-ML-F-03-045",
            Type = EquipmentType.Autoclave
        };
        db.Equipment.Add(autoclave);
        await db.SaveChangesAsync();

        var service = new EquipmentConfigurationService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateIncubatorSetPointAsync(2, new UpdateIncubatorSetPointRequest(121.0m, "Attempt invalid setpoint"), 1));
    }

    [Fact]
    public async Task AutoclaveProgram_CreateEditStatus_LogsHistory()
    {
        using var db = NewDb();
        var autoclave = new Equipment
        {
            Id = 3,
            Name = "Hirayama",
            Code = "AUT-F-ML-F-03-045",
            Type = EquipmentType.Autoclave
        };
        db.Equipment.Add(autoclave);
        await db.SaveChangesAsync();

        var service = new EquipmentConfigurationService(db);

        // 1. Create Program P01
        var p01 = await service.SaveAutoclaveProgramAsync(new SaveAutoclaveProgramRequest(
            Id: null,
            EquipmentId: 3,
            ProgramCode: "P01",
            ProgramName: "Prepared Media",
            LoadType: "Media",
            Temperature: 121m,
            CycleTimeMinutes: 15,
            IsActive: true,
            Comment: "Initial program setup"
        ), 1);

        Assert.NotNull(p01);
        Assert.Equal("P01", p01.ProgramCode);

        // 2. Edit Program P01 (Change cycle time to 20 min)
        var edited = await service.SaveAutoclaveProgramAsync(new SaveAutoclaveProgramRequest(
            Id: p01.Id,
            EquipmentId: 3,
            ProgramCode: "P01",
            ProgramName: "Prepared Media",
            LoadType: "Media",
            Temperature: 121m,
            CycleTimeMinutes: 20,
            IsActive: true,
            Comment: "Extended sterilization cycle"
        ), 1);

        Assert.Equal(20, edited.CycleTimeMinutes);

        // 3. Deactivate Program P01
        await service.SetAutoclaveProgramStatusAsync(p01.Id, false, "Deactivating for maintenance", 1);

        // 4. Verify Program History
        var history = await service.GetAutoclaveProgramHistoryAsync(p01.Id);
        Assert.Equal(3, history.Count); // Created, Updated, StatusChanged

        var statusChange = history.First(h => h.Action == "StatusChanged");
        Assert.False(statusChange.NewIsActive);
        Assert.Equal("Deactivating for maintenance", statusChange.Comment);
        Assert.Equal("Sara Ahmed", statusChange.ChangedByName);
    }

    [Fact]
    public async Task GetAutoclaveProgramsAsync_ActiveOnly_FiltersByEquipmentAndActiveStatus()
    {
        using var db = NewDb();
        var hirayama = new Equipment { Id = 3, Name = "Hirayama", Code = "AUT-F-ML-F-03-045", Type = EquipmentType.Autoclave };
        var autoclave2 = new Equipment { Id = 4, Name = "Autoclave 2", Code = "AUT-02", Type = EquipmentType.Autoclave };
        db.Equipment.AddRange(hirayama, autoclave2);
        await db.SaveChangesAsync();

        var service = new EquipmentConfigurationService(db);
        await service.SaveAutoclaveProgramAsync(new SaveAutoclaveProgramRequest(null, 3, "P01", "Prepared Media", "Media", 121m, 15, true, "Active media program"), 1);
        await service.SaveAutoclaveProgramAsync(new SaveAutoclaveProgramRequest(null, 3, "P02", "Glassware", "Glass", 121m, 30, true, "Active glass program"), 1);
        await service.SaveAutoclaveProgramAsync(new SaveAutoclaveProgramRequest(null, 3, "P03", "Waste", "Waste", 121m, 45, false, "Inactive waste program"), 1);
        await service.SaveAutoclaveProgramAsync(new SaveAutoclaveProgramRequest(null, 4, "P01", "Autoclave 2 Program", "Media", 121m, 15, true, "Other autoclave program"), 1);

        var activeHirayamaPrograms = await service.GetAutoclaveProgramsAsync(3, activeOnly: true);
        Assert.Equal(2, activeHirayamaPrograms.Count);
        Assert.All(activeHirayamaPrograms, p => Assert.Equal(3, p.EquipmentId));
        Assert.All(activeHirayamaPrograms, p => Assert.True(p.IsActive));
        Assert.Contains(activeHirayamaPrograms, p => p.ProgramCode == "P01");
        Assert.Contains(activeHirayamaPrograms, p => p.ProgramCode == "P02");
        Assert.DoesNotContain(activeHirayamaPrograms, p => p.ProgramCode == "P03");

        var allHirayamaPrograms = await service.GetAutoclaveProgramsAsync(3, activeOnly: false);
        Assert.Equal(3, allHirayamaPrograms.Count);
    }

    [Fact]
    public async Task LinkInventoryEquipmentToMasterAsync_IsSqlTranslatable_AndMatchesCaseInsensitivelyWithoutDuplicates()
    {
        using var db = NewDb();

        // Existing master equipment with UPPERCASE code
        var incucellMaster = new Equipment
        {
            Id = 1,
            Name = "INCUCELL",
            Code = "INC-F-ML-F-01-002",
            Type = EquipmentType.Incubator,
            SetPointTemperature = 36.5m
        };
        db.Equipment.Add(incucellMaster);

        // Inventory item with lowercase code
        var incucellInv = new EquipmentInventory
        {
            Id = 10,
            Code = "inc-f-ml-f-01-002",
            InstrumentType = "INCUCELL Incubator",
            ManufacturerName = "INCUCELL",
            SerialNumber = "D,141445",
            Status = EquipmentOperationalStatus.InService
        };

        // Another distinct inventory item that has similar prefix but should NOT match
        var incucellOldInv = new EquipmentInventory
        {
            Id = 11,
            Code = "INC-F-ML-F-01-002-OLD",
            InstrumentType = "INCUCELL Old",
            SerialNumber = "D,000000",
            Status = EquipmentOperationalStatus.Retired
        };

        // New autoclave inventory item to link
        var hirayamaInv = new EquipmentInventory
        {
            Id = 20,
            Code = "AUT-F-ML-F-03-045",
            InstrumentType = "Hirayama Autoclave",
            ManufacturerName = "Hirayama",
            SerialNumber = "30317012128",
            Status = EquipmentOperationalStatus.InService
        };

        db.EquipmentInventories.AddRange(incucellInv, incucellOldInv, hirayamaInv);
        await db.SaveChangesAsync();

        var service = new EquipmentConfigurationService(db);

        // 1. Linking lowercase code returns existing master without throwing translation exception
        var linkedIncucell = await service.LinkInventoryEquipmentToMasterAsync(10, 1);
        Assert.NotNull(linkedIncucell);
        Assert.Equal(1, linkedIncucell.Id);
        Assert.Equal("INC-F-ML-F-01-002", linkedIncucell.Code);

        // Verify count of master equipment remains 1 (no duplicate created)
        var masterCount = await db.Equipment.CountAsync();
        Assert.Equal(1, masterCount);

        // 2. Linking Hirayama creates new master equipment record with correct type and code
        var linkedHirayama = await service.LinkInventoryEquipmentToMasterAsync(20, 1);
        Assert.NotNull(linkedHirayama);
        Assert.Equal("AUT-F-ML-F-03-045", linkedHirayama.Code);
        Assert.Equal(EquipmentType.Autoclave, linkedHirayama.Type);

        // 3. Linking the distinct code INC-F-ML-F-01-002-OLD creates a separate master record
        var linkedOld = await service.LinkInventoryEquipmentToMasterAsync(11, 1);
        Assert.NotNull(linkedOld);
        Assert.Equal("INC-F-ML-F-01-002-OLD", linkedOld.Code);
        Assert.NotEqual(linkedIncucell.Id, linkedOld.Id);
    }
}
