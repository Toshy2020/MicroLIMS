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
        db.Equipment.Add(new Equipment
        {
            Id = 10, // Independent Equipment.Id
            Code = "INC-F-ML-F-01-002",
            Name = "INCUCELL",
            Type = EquipmentType.Incubator,
            SetPointTemperature = 32.5m
        });

        db.Incubations.Add(new Incubation
        {
            Id = 101,
            IncubatorEquipmentId = 10,
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
        db.Equipment.Add(new Equipment
        {
            Id = 20, // Independent Equipment.Id
            Code = "INC-MULTI-01",
            Name = "Multi Incubator",
            Type = EquipmentType.Incubator,
            SetPointTemperature = 32.5m
        });

        for (int i = 1; i <= 4; i++)
        {
            db.Incubations.Add(new Incubation
            {
                Id = 200 + i,
                IncubatorEquipmentId = 20,
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
        db.Equipment.Add(new Equipment
        {
            Id = 50, // Independent Equipment.Id
            Code = "INC-005",
            Name = "Incubator 5",
            Type = EquipmentType.Incubator,
            SetPointTemperature = 32.5m
        });

        var inc = new Incubation
        {
            Id = 501,
            IncubatorEquipmentId = 50,
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
        db.Equipment.Add(new Equipment
        {
            Id = 60, // Independent Equipment.Id
            Code = "INC-F-ML-F-01-002",
            Name = "Incubator 1 Master",
            Type = EquipmentType.Incubator,
            SetPointTemperature = 32.5m
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
            IncubatorEquipmentId = 60,
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
        db.Equipment.Add(new Equipment
        {
            Id = 70, // Independent Equipment.Id
            Code = "INC-007",
            Name = "Incubator 7",
            Type = EquipmentType.Incubator,
            SetPointTemperature = 32.5m
        });

        db.Incubations.AddRange(
            new Incubation
            {
                Id = 701, IncubatorEquipmentId = 70, StepName = "Test A",
                StartedAt = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc),
                IncubationStartUtc = new DateTime(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc),
                CompletedAt = new DateTime(2026, 8, 16, 10, 0, 0, DateTimeKind.Utc),
                StartedByUserId = 1
            },
            new Incubation
            {
                Id = 702, IncubatorEquipmentId = 70, StepName = "Test B",
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
        db.Equipment.Add(new Equipment
        {
            Id = 80, // Independent Equipment.Id
            Code = "INC-008",
            Name = "Incubator 8",
            Type = EquipmentType.Incubator,
            SetPointTemperature = 32.5m
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
            IncubatorEquipmentId = 80,
            StepName = "TAMC Incubation",
            StartedAt = DateTime.UtcNow.AddDays(-3),
            IncubationStartUtc = DateTime.UtcNow.AddDays(-3),
            IncubationEndUtc = DateTime.UtcNow.AddDays(2),
            CompletedAt = null,
            StartedByUserId = 1
        });
        await db.SaveChangesAsync();

        var service = new EquipmentInventoryService(db);

        var activeEq = await service.GetActiveEquipmentAsync();
        var inc008 = Assert.Single(activeEq, e => e.Code == "INC-008");
        Assert.Equal(0, inc008.ActiveItemCount);

        var activeActivities = await service.GetActiveActivitiesForEquipmentAsync(8);
        Assert.Empty(activeActivities);

        var history = await service.GetHistoricalActivitiesForEquipmentAsync(8);
        var histItem = Assert.Single(history);
        Assert.False(histItem.IsActive);
        Assert.NotNull(histItem.CompletedOn);
    }

    // =========================================================================
    // REGRESSION TESTS FOR CROSS-ENTITY ID CONFUSION (PRODUCTION SCENARIO)
    // =========================================================================

    [Fact]
    public async Task EquipmentResolution_DistinctInventoryAndMasterIds_NoCrossContamination()
    {
        // Mirrors exact production database:
        // INC-F-ML-F-01-003: EquipmentInventory.Id = 1, Equipment.Id = 3
        // INC-F-ML-F-01-007: EquipmentInventory.Id = 3, Equipment.Id = 5
        // AUT-F-ML-F-03-045: EquipmentInventory.Id = 6, Equipment.Id = 2
        using var db = NewDb();

        db.EquipmentInventories.AddRange(
            new EquipmentInventory
            {
                Id = 1,
                Code = "INC-F-ML-F-01-003",
                InstrumentType = "Incubator",
                Location = "Instruments room F-ML-F-01",
                Status = EquipmentOperationalStatus.InService
            },
            new EquipmentInventory
            {
                Id = 3,
                Code = "INC-F-ML-F-01-007",
                InstrumentType = "Incubator",
                Location = "Instruments room F-ML-F-01",
                Status = EquipmentOperationalStatus.InService
            },
            new EquipmentInventory
            {
                Id = 6,
                Code = "AUT-F-ML-F-03-045",
                InstrumentType = "Hirayama",
                Location = "Sterilization room F-ML-F-04",
                Status = EquipmentOperationalStatus.InService
            }
        );

        db.Equipment.AddRange(
            new Equipment
            {
                Id = 3,
                Code = "INC-F-ML-F-01-003",
                Name = "Qualitemp",
                Type = EquipmentType.Incubator,
                SetPointTemperature = 32.5m
            },
            new Equipment
            {
                Id = 5,
                Code = "INC-F-ML-F-01-007",
                Name = "Binder",
                Type = EquipmentType.Incubator,
                SetPointTemperature = 42m
            },
            new Equipment
            {
                Id = 2,
                Code = "AUT-F-ML-F-03-045",
                Name = "Hirayama",
                Type = EquipmentType.Autoclave
            }
        );

        // TestOrder 303 & 309 incubated in Qualitemp (Equipment.Id = 3)
        var s1 = new Sample { Id = 83, ReferenceNumber = "FP0926006" };
        var s2 = new Sample { Id = 84, ReferenceNumber = "FP0926007" };
        var s3 = new Sample { Id = 85, ReferenceNumber = "FP0926008" };
        db.Samples.AddRange(s1, s2, s3);

        var o303 = new TestOrder { Id = 303, SampleId = 83, TestCode = "P. aeruginosa" };
        var o309 = new TestOrder { Id = 309, SampleId = 84, TestCode = "P. aeruginosa" };
        var o290 = new TestOrder { Id = 290, SampleId = 85, TestCode = "E.coli" };
        db.TestOrders.AddRange(o303, o309, o290);

        // Incubations for INC-F-ML-F-01-003 (Equipment.Id = 3)
        db.Incubations.Add(new Incubation
        {
            Id = 587,
            TestOrderId = 303,
            StepName = "CAM",
            IncubatorEquipmentId = 3,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            IncubationStartUtc = DateTime.UtcNow.AddHours(-1),
            IncubationEndUtc = DateTime.UtcNow.AddHours(23),
            StartedByUserId = 1
        });
        db.Incubations.Add(new Incubation
        {
            Id = 588,
            TestOrderId = 309,
            StepName = "CAM",
            IncubatorEquipmentId = 3,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            IncubationStartUtc = DateTime.UtcNow.AddHours(-1),
            IncubationEndUtc = DateTime.UtcNow.AddHours(23),
            StartedByUserId = 1
        });

        // Incubation for INC-F-ML-F-01-007 (Equipment.Id = 5)
        db.Incubations.Add(new Incubation
        {
            Id = 591,
            TestOrderId = 290,
            StepName = "MBP",
            IncubatorEquipmentId = 5,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            IncubationStartUtc = DateTime.UtcNow.AddHours(-1),
            IncubationEndUtc = DateTime.UtcNow.AddHours(23),
            StartedByUserId = 1
        });

        await db.SaveChangesAsync();

        var service = new EquipmentInventoryService(db);

        // 1. Check Active Equipment Counts
        var activeEquipment = await service.GetActiveEquipmentAsync();

        var inc003 = Assert.Single(activeEquipment, e => e.Code == "INC-F-ML-F-01-003");
        var inc007 = Assert.Single(activeEquipment, e => e.Code == "INC-F-ML-F-01-007");
        var autoclave = Assert.Single(activeEquipment, e => e.Code == "AUT-F-ML-F-03-045");

        // INC-F-ML-F-01-003 must have exactly 2 active items (TestOrders 303 and 309)
        Assert.Equal(2, inc003.ActiveItemCount);

        // INC-F-ML-F-01-007 must have exactly 1 active item (TestOrder 290)
        // Under the old bug, it falsely matched IncubatorEquipmentId == 3 (eq.Id == 3) and had 3 items!
        Assert.Equal(1, inc007.ActiveItemCount);

        // Autoclave must have 0 active incubator items
        // Under the old bug, if an incubator had Equipment.Id = 6, autoclave (eq.Id = 6) picked it up!
        Assert.Equal(0, autoclave.ActiveItemCount);

        // 2. Check Active Activities per Equipment
        var inc003Activities = await service.GetActiveActivitiesForEquipmentAsync(1); // eq.Id = 1
        Assert.Equal(2, inc003Activities.Count);
        Assert.Contains(inc003Activities, a => a.ItemCode == "FP0926006" && a.ActivityType == "Media Incubation");
        Assert.Contains(inc003Activities, a => a.ItemCode == "FP0926007" && a.ActivityType == "Media Incubation");

        var inc007Activities = await service.GetActiveActivitiesForEquipmentAsync(3); // eq.Id = 3
        Assert.Single(inc007Activities);
        Assert.Equal("FP0926008", inc007Activities[0].ItemCode);
        // Crucial: TestOrders 303 and 309 must NOT appear in INC-F-ML-F-01-007!
        Assert.DoesNotContain(inc007Activities, a => a.ItemCode == "FP0926006");
        Assert.DoesNotContain(inc007Activities, a => a.ItemCode == "FP0926007");

        var autoclaveActivities = await service.GetActiveActivitiesForEquipmentAsync(6); // eq.Id = 6
        Assert.Empty(autoclaveActivities);

        // 3. Check "Where Is It?" resolution
        var whereResult303 = await service.WhereIsItAsync("FP0926006");
        Assert.Equal("INC-F-ML-F-01-003", whereResult303.CurrentEquipmentCode);
        Assert.Equal("Incubator", whereResult303.CurrentEquipmentName);
        Assert.NotNull(whereResult303.CurrentActivity);

        var whereResult290 = await service.WhereIsItAsync("FP0926008");
        Assert.Equal("INC-F-ML-F-01-007", whereResult290.CurrentEquipmentCode);

        // 4. Check Historical Activities
        var history003 = await service.GetHistoricalActivitiesForEquipmentAsync(1); // eq.Id = 1 -> Master 3
        Assert.Equal(2, history003.Count);

        var history007 = await service.GetHistoricalActivitiesForEquipmentAsync(3); // eq.Id = 3 -> Master 5
        Assert.Single(history007);
        Assert.DoesNotContain(history007, a => a.ItemCode == "FP0926006");
        Assert.DoesNotContain(history007, a => a.ItemCode == "FP0926007");
    }
}
