using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.InventoryTests;

public class ActiveEquipmentTraceabilityTests
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
    public async Task EmptyIncubator_AppearsWithZeroActiveItems()
    {
        // Idle equipment still needs to be selectable in the Active
        // Equipment view (to browse its traceability history) even when
        // nothing is currently in progress - it just carries a 0 count
        // rather than vanishing from the list entirely.
        using var db = NewDb();
        db.EquipmentInventories.Add(new EquipmentInventory
        {
            Id = 10,
            Code = "INC-EMPTY-01",
            InstrumentType = "Incubator",
            Location = "Instruments room F-ML-F-01",
            Status = EquipmentOperationalStatus.InService
        });
        await db.SaveChangesAsync();

        var service = new EquipmentInventoryService(db);
        var activeList = await service.GetActiveEquipmentAsync();

        var entry = Assert.Single(activeList, e => e.Code == "INC-EMPTY-01");
        Assert.Equal(0, entry.ActiveItemCount);
    }

    [Fact]
    public async Task IncubatorWithActiveActivity_AppearsInActiveEquipment()
    {
        using var db = NewDb();
        db.EquipmentInventories.Add(new EquipmentInventory
        {
            Id = 1,
            Code = "INC-F-ML-F-01-002",
            InstrumentType = "Incubator",
            Location = "Instruments room F-ML-F-01",
            Status = EquipmentOperationalStatus.InService
        });

        db.Incubations.Add(new Incubation
        {
            Id = 101,
            IncubatorEquipmentId = 1,
            StepName = "Pathogen Test",
            StartedAt = DateTime.UtcNow.AddHours(-2),
            IncubationStartUtc = DateTime.UtcNow.AddHours(-2),
            IncubationEndUtc = DateTime.UtcNow.AddHours(22),
            CompletedAt = null,
            StartedByUserId = 1
        });
        await db.SaveChangesAsync();

        var service = new EquipmentInventoryService(db);
        var activeList = await service.GetActiveEquipmentAsync();

        var activeEq = Assert.Single(activeList, e => e.Code == "INC-F-ML-F-01-002");
        Assert.Equal(1, activeEq.ActiveItemCount);
        Assert.Equal("Incubation", activeEq.PrimaryActivityCategory);
    }

    [Fact]
    public async Task MultipleActivities_CorrectItemCount()
    {
        using var db = NewDb();
        db.EquipmentInventories.Add(new EquipmentInventory
        {
            Id = 2,
            Code = "INC-MULTI-01",
            InstrumentType = "Incubator",
            Location = "Room 101",
            Status = EquipmentOperationalStatus.InService
        });

        for (int i = 1; i <= 4; i++)
        {
            db.Incubations.Add(new Incubation
            {
                Id = 200 + i,
                IncubatorEquipmentId = 2,
                StepName = $"Media Incubation {i}",
                StartedAt = DateTime.UtcNow.AddHours(-1),
                IncubationStartUtc = DateTime.UtcNow.AddHours(-1),
                IncubationEndUtc = DateTime.UtcNow.AddHours(24),
                CompletedAt = null,
                StartedByUserId = 1
            });
        }
        await db.SaveChangesAsync();

        var service = new EquipmentInventoryService(db);
        var activeList = await service.GetActiveEquipmentAsync();

        var activeEq = Assert.Single(activeList, e => e.Code == "INC-MULTI-01");
        Assert.Equal(4, activeEq.ActiveItemCount);

        var currentActivities = await service.GetActiveActivitiesForEquipmentAsync(2);
        Assert.Equal(4, currentActivities.Count);
        Assert.All(currentActivities, a => Assert.Equal("Sara Ahmed", a.StartedBy));
    }

    [Fact]
    public async Task RefrigeratorWithActiveMedia_AppearsInActiveEquipment()
    {
        using var db = NewDb();
        db.EquipmentInventories.Add(new EquipmentInventory
        {
            Id = 3,
            Code = "REF-F-ML-F-01-001",
            InstrumentType = "Refrigerator",
            Location = "Media Prep room",
            Status = EquipmentOperationalStatus.InService
        });

        var mat = new Material { Id = 30, MaterialName = "TSB Powder", Code = "TSB-MAT" };
        db.Materials.Add(mat);

        db.Media.Add(new Media
        {
            Id = 301,
            MaterialId = 30,
            AutoclaveEquipmentId = 3,
            LotNumber = "TSB/08/26",
            ManufacturerLot = "MFG-001",
            ManufacturerName = "Merck",
            ExpiryDate = DateTime.UtcNow.AddMonths(1),
            PreparedAt = DateTime.UtcNow.AddDays(-2),
            PreparedByUserId = 1,
            Status = MediaStatus.Active
        });
        await db.SaveChangesAsync();

        var service = new EquipmentInventoryService(db);
        var activeList = await service.GetActiveEquipmentAsync();

        var activeEq = Assert.Single(activeList, e => e.Code == "REF-F-ML-F-01-001");
        Assert.Equal("Media Storage", activeEq.PrimaryActivityCategory);

        var activities = await service.GetActiveActivitiesForEquipmentAsync(3);
        var act = Assert.Single(activities);
        Assert.Equal("Media Storage", act.ActivityType);
        Assert.Equal("TSB/08/26", act.ItemCode);
    }

    [Fact]
    public async Task DeepFreezerWithCryovials_AppearsInActiveEquipment()
    {
        using var db = NewDb();
        db.EquipmentInventories.Add(new EquipmentInventory
        {
            Id = 4,
            Code = "COD-F-ML-D-06-071",
            InstrumentType = "Deep Freezer",
            Location = "Strain Room",
            Status = EquipmentOperationalStatus.InService
        });

        var mat = new Material { Id = 40, MaterialName = "S. aureus Lyophilized", Code = "SA-MAT" };
        var org = new Organism { Id = 40, ScientificName = "Staphylococcus aureus", AtccNumber = "6538" };
        db.Materials.Add(mat);
        db.Organisms.Add(org);

        db.Cryovials.Add(new Cryovial
        {
            Id = 401,
            MaterialId = 40,
            OrganismId = 40,
            Code = "CRYO-SA-08-26",
            OrganismNameSnapshot = "Staphylococcus aureus",
            ManufacturerName = "ATCC",
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            NumberOfVialsPrepared = 10,
            VialsRemaining = 8,
            StorageCondition = "Deep Freezer",
            PreparedByUserId = 1,
            IsDestroyed = false
        });
        await db.SaveChangesAsync();

        var service = new EquipmentInventoryService(db);
        var activeList = await service.GetActiveEquipmentAsync();

        var activeEq = Assert.Single(activeList, e => e.Code == "COD-F-ML-D-06-071");
        Assert.Equal("Cryovial Storage", activeEq.PrimaryActivityCategory);

        var activities = await service.GetActiveActivitiesForEquipmentAsync(4);
        var act = Assert.Single(activities);
        Assert.Equal("Cryovial Storage", act.ActivityType);
        Assert.Equal("CRYO-SA-08-26", act.ItemCode);
    }

    [Fact]
    public async Task ActivityCompletion_ItemLeavesCurrentActivities_RemainsInHistory()
    {
        using var db = NewDb();
        db.EquipmentInventories.Add(new EquipmentInventory
        {
            Id = 5,
            Code = "INC-005",
            InstrumentType = "Incubator",
            Location = "Lab 1",
            Status = EquipmentOperationalStatus.InService
        });

        var inc = new Incubation
        {
            Id = 501,
            IncubatorEquipmentId = 5,
            StepName = "E. coli test",
            StartedAt = DateTime.UtcNow.AddHours(-12),
            IncubationStartUtc = DateTime.UtcNow.AddHours(-12),
            IncubationEndUtc = DateTime.UtcNow.AddHours(12),
            CompletedAt = null,
            StartedByUserId = 1
        };
        db.Incubations.Add(inc);
        await db.SaveChangesAsync();

        var service = new EquipmentInventoryService(db);

        // Before completion:
        var currentBefore = await service.GetActiveActivitiesForEquipmentAsync(5);
        Assert.Single(currentBefore);

        // Complete incubation:
        inc.CompletedAt = DateTime.UtcNow.AddHours(-1);
        inc.IncubationEndUtc = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();

        // After completion:
        var currentAfter = await service.GetActiveActivitiesForEquipmentAsync(5);
        Assert.Empty(currentAfter);

        // Exists in history:
        var history = await service.GetHistoricalActivitiesForEquipmentAsync(5);
        Assert.Single(history);
        Assert.Equal("Sara Ahmed", history[0].StartedBy);
    }

    [Fact]
    public async Task WhereIsIt_SearchByItemCodeAndHistory()
    {
        using var db = NewDb();
        db.EquipmentInventories.Add(new EquipmentInventory
        {
            Id = 6,
            Code = "INC-F-ML-F-01-002",
            InstrumentType = "Incubator 1",
            Location = "Room F-01",
            Status = EquipmentOperationalStatus.InService
        });

        var sample = new Sample
        {
            Id = 600,
            ReferenceNumber = "PT-0021",
            ControlNumber = "CTRL-001"
        };
        db.Samples.Add(sample);

        var testOrder = new TestOrder
        {
            Id = 601,
            SampleId = 600,
            TestCode = "PAT-ECOLI"
        };
        db.TestOrders.Add(testOrder);

        db.Incubations.Add(new Incubation
        {
            Id = 602,
            TestOrderId = 601,
            IncubatorEquipmentId = 6,
            StepName = "Pathogen Test - Pre-Enrichment",
            StartedAt = DateTime.UtcNow.AddHours(-5),
            IncubationStartUtc = DateTime.UtcNow.AddHours(-5),
            IncubationEndUtc = DateTime.UtcNow.AddHours(19),
            CompletedAt = null,
            StartedByUserId = 1
        });
        await db.SaveChangesAsync();

        var service = new EquipmentInventoryService(db);
        var result = await service.WhereIsItAsync("PT-0021");

        Assert.Equal("PT-0021", result.SearchTerm);
        Assert.NotNull(result.CurrentActivity);
        Assert.Equal("INC-F-ML-F-01-002", result.CurrentEquipmentCode);
        Assert.Equal("Incubator 1", result.CurrentEquipmentName);
        Assert.Single(result.History);
    }

    [Fact]
    public async Task HistoricalSearch_DateRangeAndItemCodeFilter()
    {
        using var db = NewDb();
        db.EquipmentInventories.Add(new EquipmentInventory
        {
            Id = 7,
            Code = "INC-007",
            InstrumentType = "Incubator",
            Location = "Room A",
            Status = EquipmentOperationalStatus.InService
        });

        db.Incubations.AddRange(
            new Incubation
            {
                Id = 701, IncubatorEquipmentId = 7, StepName = "Test A",
                StartedAt = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc),
                IncubationStartUtc = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc),
                CompletedAt = new DateTime(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc),
                StartedByUserId = 1
            },
            new Incubation
            {
                Id = 702, IncubatorEquipmentId = 7, StepName = "Test B",
                StartedAt = new DateTime(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc),
                IncubationStartUtc = new DateTime(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc),
                CompletedAt = new DateTime(2026, 8, 19, 10, 0, 0, DateTimeKind.Utc),
                StartedByUserId = 1
            }
        );
        await db.SaveChangesAsync();

        var service = new EquipmentInventoryService(db);

        var searchFiltered = await service.GetHistoricalActivitiesForEquipmentAsync(
            7, fromDate: new DateTime(2026, 8, 17), toDate: new DateTime(2026, 8, 20));

        var act = Assert.Single(searchFiltered);
        Assert.Equal("Test B", act.ItemName);
    }

    [Fact]
    public async Task IncubationCompletedViaTestOrderOrSample_ZeroActiveCountNotExcluded()
    {
        using var db = NewDb();
        db.EquipmentInventories.Add(new EquipmentInventory
        {
            Id = 8,
            Code = "INC-008",
            InstrumentType = "Incubator",
            Location = "Room B",
            Status = EquipmentOperationalStatus.InService
        });

        var sample = new Sample
        {
            Id = 800,
            ReferenceNumber = "SMP-800",
            ControlNumber = "CTRL-800",
            Status = SampleStatus.Approved
        };
        db.Samples.Add(sample);

        var testOrder = new TestOrder
        {
            Id = 801,
            SampleId = 800,
            TestCode = "TAMC",
            CurrentStep = WorkflowStep.Approved,
            Status = ApprovalStatus.Approved
        };
        db.TestOrders.Add(testOrder);

        db.Incubations.Add(new Incubation
        {
            Id = 802,
            TestOrderId = 801,
            IncubatorEquipmentId = 8,
            StepName = "TAMC Incubation",
            StartedAt = DateTime.UtcNow.AddDays(-3),
            IncubationStartUtc = DateTime.UtcNow.AddDays(-3),
            IncubationEndUtc = DateTime.UtcNow.AddDays(2), // Date is in future, but test & sample are approved/completed
            CompletedAt = null,
            StartedByUserId = 1
        });
        await db.SaveChangesAsync();

        var service = new EquipmentInventoryService(db);

        // INC-008 stays listed (idle equipment is still selectable for
        // history) but must carry a 0 active count, since its only
        // Incubation row belongs to an already-Approved TestOrder.
        var activeEq = await service.GetActiveEquipmentAsync();
        var inc008 = Assert.Single(activeEq, e => e.Code == "INC-008");
        Assert.Equal(0, inc008.ActiveItemCount);

        // Active activities for INC-008 should be empty
        var activeActivities = await service.GetActiveActivitiesForEquipmentAsync(8);
        Assert.Empty(activeActivities);

        // History should still show the record with isActive = false
        var history = await service.GetHistoricalActivitiesForEquipmentAsync(8);
        var histItem = Assert.Single(history);
        Assert.False(histItem.IsActive);
        Assert.NotNull(histItem.CompletedOn);
    }
}
