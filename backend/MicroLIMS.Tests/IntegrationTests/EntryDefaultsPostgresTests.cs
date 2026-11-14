using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.EntryDefaults;
using MicroLIMS.Application.Services.SystemSettings;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class EntryDefaultsPostgresTests
{
    private static readonly DateTime NowUtc = new(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);

    private readonly PostgresTestFixture _fixture;

    public EntryDefaultsPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTime utcNow) => _utcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class StubSettings : ISystemSettingsReader
    {
        private readonly bool _enabled;
        public StubSettings(bool enabled = true) => _enabled = enabled;
        public Task<int> GetIntAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> GetBoolAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(key == SystemSettingKeys.LastChoiceDefaultsEnabled ? _enabled : true);
        public Task<string> GetStringAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
    }

    private static EntryDefaultsService BuildService(
        MicroLimsDbContext db,
        ISystemSettingsReader? settings = null,
        DateTime? nowUtc = null)
    {
        var clock = new FixedTimeProvider(nowUtc ?? NowUtc);
        return new EntryDefaultsService(
            db,
            new UserSectionScopeService(db),
            new IncubatorEligibilityService(db, clock),
            settings ?? new StubSettings(true),
            clock);
    }

    private static EntryDefaultsController BuildController(EntryDefaultsService service, int userId)
    {
        var controller = new EntryDefaultsController(service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                    "test"))
            }
        };
        return controller;
    }

    private sealed record World(DocumentSection Micro, DocumentSection Fp, int CauseId);

    private static async Task<World> WorldAsync(MicroLimsDbContext db)
    {
        var micro = await db.DocumentSections.AsNoTracking().SingleAsync(s => s.Code == "MICRO");
        var fp = await db.DocumentSections.AsNoTracking().SingleAsync(s => s.Code == "FP");
        var cause = await db.CausesOfTesting.FirstOrDefaultAsync();
        if (cause is null)
        {
            cause = new CauseOfTesting { Name = "Routine Testing", IsActive = true };
            db.CausesOfTesting.Add(cause);
            await db.SaveChangesAsync();
        }

        return new World(micro, fp, cause.Id);
    }

    private static async Task<User> NewAnalystAsync(MicroLimsDbContext db, string label, params DocumentSection[] memberOf)
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var role = await db.Roles.AsNoTracking().FirstAsync(r => r.Type == RoleType.Analyst);
        var user = new User
        {
            Username = $"{label}_{uid}",
            FullName = $"{label} {uid}",
            PasswordHash = "x",
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        foreach (var section in memberOf)
        {
            db.UserOrgMemberships.Add(new UserOrgMembership
            {
                UserId = user.Id,
                DepartmentId = section.DepartmentId,
                SectionId = section.Id
            });
        }
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<(Equipment Equipment, EquipmentInventory Asset)> NewEquipmentWithAssetAsync(
        MicroLimsDbContext db,
        int sectionId,
        int userId,
        EquipmentType type,
        decimal? setPointTemp = 32.5m,
        EquipmentOperationalStatus status = EquipmentOperationalStatus.InService,
        DateTime? calibrationDueDate = null)
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var code = $"{(type == EquipmentType.Incubator ? "INC" : "AC")}-{uid}";
        var asset = new EquipmentInventory
        {
            InstrumentType = type.ToString(),
            ManufacturerName = "Binder",
            Code = code,
            Location = "Lab 1",
            Status = status,
            CalibrationDueDate = calibrationDueDate,
            SectionId = sectionId,
            CreatedByUserId = userId,
            CreatedAt = NowUtc.AddDays(-30),
            LastModifiedByUserId = userId,
            LastModifiedAt = NowUtc.AddDays(-30)
        };
        db.EquipmentInventories.Add(asset);
        await db.SaveChangesAsync();

        var equipment = new Equipment
        {
            Name = $"{type} {uid}",
            Code = code,
            Type = type,
            Location = "Lab 1",
            SetPointTemperature = setPointTemp,
            CalibrationDueDate = calibrationDueDate,
            SectionId = sectionId,
            EquipmentInventoryId = asset.Id
        };
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();

        return (equipment, asset);
    }

    private static async Task<(MediaProduct Product, Material Source, Media Lot)> NewMediaLotAsync(
        MicroLimsDbContext db,
        int sectionId,
        int userId,
        bool isReleased = true,
        MediaStatus status = MediaStatus.Active,
        DateTime? expiryDate = null,
        int? autoclaveEquipmentId = null,
        DateTime? preparedAt = null)
    {
        var uid = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var product = new MediaProduct
        {
            Code = $"MP{uid}",
            Name = $"Media Product {uid}",
            IsActive = true
        };
        db.MediaProducts.Add(product);
        await db.SaveChangesAsync();

        var source = new Material
        {
            SectionId = sectionId,
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = $"Powder {uid}",
            ManufacturerName = "Oxoid",
            BatchNumber = $"B-{uid}",
            ReceivingDate = NowUtc.AddDays(-60),
            ExpiryDate = NowUtc.AddDays(365),
            StockId = $"STK-{uid}",
            Location = "Shelf A",
            MediaProductId = product.Id,
            QuantityReceived = 500m,
            QuantityRemaining = 400m,
            Unit = MaterialUnit.Gram,
            CreatedByUserId = userId,
            LastModifiedByUserId = userId
        };
        db.Materials.Add(source);
        await db.SaveChangesAsync();

        var lot = new Media
        {
            MaterialId = source.Id,
            LotNumber = $"LOT-{uid}/01",
            ManufacturerLot = $"ML-{uid}",
            ManufacturerName = "Oxoid",
            TotalWeight = 20m,
            TotalVolume = "1000 ml",
            AutoclaveEquipmentId = autoclaveEquipmentId,
            AutoclaveProgram = "P1",
            LoadType = "Liquid",
            Temperature = 121m,
            CycleTime = 15,
            CycleNumber = 1,
            Ph = 7.2m,
            PreparedAt = preparedAt ?? NowUtc.AddDays(-5),
            ExpiryDate = expiryDate ?? NowUtc.AddDays(30),
            PreparedByUserId = userId,
            Status = status,
            ApprovalStatus = isReleased ? ApprovalGateStatus.Approved : ApprovalGateStatus.PendingReview,
            IsReleasedForUse = isReleased
        };
        db.Media.Add(lot);
        await db.SaveChangesAsync();

        return (product, source, lot);
    }

    private static async Task<(TestOrder Order, TestWorkflowStepMedia StepMedia)> NewTestOrderWithStepMediaAsync(
        MicroLimsDbContext db,
        World w,
        int userId,
        DocumentSection section,
        int mediaProductId,
        decimal tempMin = 30m,
        decimal tempMax = 35m)
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var testCode = $"TC-{uid}";
        var definition = new TestDefinition
        {
            Code = testCode,
            DisplayName = $"Test {uid}",
            WorkflowType = WorkflowType.CountTest,
            SectionId = section.Id,
            IsActive = true
        };
        db.TestDefinitions.Add(definition);
        await db.SaveChangesAsync();

        var step = new TestWorkflowStep
        {
            TestDefinitionId = definition.Id,
            StepOrder = 1,
            StepName = "TAMC Incubation",
            StepType = StepType.PlateCount,
            IncubationMinHours = 72,
            IncubationMaxHours = 120,
            TemperatureMin = tempMin,
            TemperatureMax = tempMax,
            IsFinalStep = true
        };
        db.TestWorkflowSteps.Add(step);
        await db.SaveChangesAsync();

        var stepMedia = new TestWorkflowStepMedia
        {
            TestWorkflowStepId = step.Id,
            MediaProductId = mediaProductId,
            TempMin = tempMin,
            TempMax = tempMax,
            IncubationMinHours = 72,
            IncubationMaxHours = 120,
            IsRequired = true,
            DisplayOrder = 1
        };
        db.TestWorkflowStepMedias.Add(stepMedia);

        var sample = new Sample
        {
            ReferenceNumber = $"SMP-{uid}",
            Category = SampleCategory.FinishedProduct,
            CauseOfTestingId = w.CauseId,
            ReceivedAt = NowUtc.AddHours(-3),
            ReceivedByUserId = userId,
            LabSectionId = section.Id,
            Status = SampleStatus.InTesting
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var order = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = testCode,
            SectionId = section.Id,
            Status = ApprovalStatus.InProgress,
            CurrentStep = WorkflowStep.Waiting,
            AssignedAnalystId = userId
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        return (order, stepMedia);
    }

    private static async Task<Incubation> NewIncubationRecordAsync(
        MicroLimsDbContext db,
        int testOrderId,
        int startedByUserId,
        int? mediaId,
        int? incubatorEquipmentId,
        DateTime startedAt)
    {
        var inc = new Incubation
        {
            TestOrderId = testOrderId,
            StepNumber = 1,
            StepName = "TAMC Incubation",
            MediaId = mediaId,
            IncubatorEquipmentId = incubatorEquipmentId,
            StartedByUserId = startedByUserId,
            StartedAt = startedAt
        };
        db.Incubations.Add(inc);
        await db.SaveChangesAsync();
        return inc;
    }

    [PostgresFact]
    public async Task ReturnsMyLastIncubator_NotAnotherUsers()
    {
        int myUserId, testOrderId, stepMediaId, mediaProductId, myIncubatorId, myLotId;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            var me = await NewAnalystAsync(arrange, "me", w.Micro);
            var other = await NewAnalystAsync(arrange, "other", w.Micro);
            myUserId = me.Id;

            var (product, _, myLot) = await NewMediaLotAsync(arrange, w.Micro.Id, me.Id);
            var (_, _, otherLot) = await NewMediaLotAsync(arrange, w.Micro.Id, other.Id);
            mediaProductId = product.Id;
            myLotId = myLot.Id;

            var (myInc, _) = await NewEquipmentWithAssetAsync(arrange, w.Micro.Id, me.Id, EquipmentType.Incubator, setPointTemp: 32.5m);
            var (otherInc, _) = await NewEquipmentWithAssetAsync(arrange, w.Micro.Id, other.Id, EquipmentType.Incubator, setPointTemp: 32.5m);
            myIncubatorId = myInc.Id;

            var (order, stepMedia) = await NewTestOrderWithStepMediaAsync(arrange, w, me.Id, w.Micro, product.Id, 30m, 35m);
            testOrderId = order.Id;
            stepMediaId = stepMedia.Id;

            await NewIncubationRecordAsync(arrange, order.Id, me.Id, myLot.Id, myInc.Id, NowUtc.AddHours(-2));
            // Another user started an incubation MORE recently with a different incubator & lot:
            await NewIncubationRecordAsync(arrange, order.Id, other.Id, otherLot.Id, otherInc.Id, NowUtc.AddMinutes(-15));
        }

        EntryDefaultsResponse response;
        await using (var act = _fixture.CreateDbContext())
        {
            response = await BuildService(act).GetAsync(
                myUserId,
                "incubation",
                testOrderId: testOrderId,
                stepMediaId: stepMediaId,
                mediaProductId: mediaProductId);
        }

        var incDefault = Assert.Single(response.Defaults, d => d.Field == "incubatorEquipmentId");
        Assert.Equal(myIncubatorId, incDefault.Id);

        var lotDefault = Assert.Single(response.Defaults, d => d.Field == "mediaLotId");
        Assert.Equal(myLotId, lotDefault.Id);
    }

    [PostgresFact]
    public async Task EquipmentCalibrationOverdueToday_NotOffered()
    {
        int userId, testOrderId, stepMediaId, mediaProductId;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            var me = await NewAnalystAsync(arrange, "overdue", w.Micro);
            userId = me.Id;

            var (product, _, lot) = await NewMediaLotAsync(arrange, w.Micro.Id, me.Id);
            mediaProductId = product.Id;

            // Calibration due yesterday on the EquipmentInventory asset -> overdue today (Review Focus 4)
            var (overdueInc, _) = await NewEquipmentWithAssetAsync(
                arrange,
                w.Micro.Id,
                me.Id,
                EquipmentType.Incubator,
                setPointTemp: 32.5m,
                status: EquipmentOperationalStatus.InService,
                calibrationDueDate: NowUtc.Date.AddDays(-1));
            overdueInc.CalibrationDueDate = null;
            await arrange.SaveChangesAsync();

            var (order, stepMedia) = await NewTestOrderWithStepMediaAsync(arrange, w, me.Id, w.Micro, product.Id, 30m, 35m);
            testOrderId = order.Id;
            stepMediaId = stepMedia.Id;

            await NewIncubationRecordAsync(arrange, order.Id, me.Id, lot.Id, overdueInc.Id, NowUtc.AddHours(-1));
        }

        EntryDefaultsResponse response;
        await using (var act = _fixture.CreateDbContext())
        {
            response = await BuildService(act).GetAsync(
                userId,
                "incubation",
                testOrderId: testOrderId,
                stepMediaId: stepMediaId,
                mediaProductId: mediaProductId);
        }

        Assert.DoesNotContain(response.Defaults, d => d.Field == "incubatorEquipmentId");
    }

    [PostgresFact]
    public async Task EquipmentOutOfService_NotOffered()
    {
        int userId, testOrderId, stepMediaId, mediaProductId;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            var me = await NewAnalystAsync(arrange, "oos", w.Micro);
            userId = me.Id;

            var (product, _, lot) = await NewMediaLotAsync(arrange, w.Micro.Id, me.Id);
            mediaProductId = product.Id;

            var (oosInc, _) = await NewEquipmentWithAssetAsync(
                arrange,
                w.Micro.Id,
                me.Id,
                EquipmentType.Incubator,
                setPointTemp: 32.5m,
                status: EquipmentOperationalStatus.OutOfService,
                calibrationDueDate: NowUtc.Date.AddDays(30));

            var (order, stepMedia) = await NewTestOrderWithStepMediaAsync(arrange, w, me.Id, w.Micro, product.Id, 30m, 35m);
            testOrderId = order.Id;
            stepMediaId = stepMedia.Id;

            await NewIncubationRecordAsync(arrange, order.Id, me.Id, lot.Id, oosInc.Id, NowUtc.AddHours(-1));
        }

        EntryDefaultsResponse response;
        await using (var act = _fixture.CreateDbContext())
        {
            response = await BuildService(act).GetAsync(
                userId,
                "incubation",
                testOrderId: testOrderId,
                stepMediaId: stepMediaId,
                mediaProductId: mediaProductId);
        }

        Assert.DoesNotContain(response.Defaults, d => d.Field == "incubatorEquipmentId");
    }

    [PostgresFact]
    public async Task IncubatorOutsideStepTemperatureRange_NotOffered()
    {
        int userId, testOrderId, stepMediaId, mediaProductId;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            var me = await NewAnalystAsync(arrange, "temp", w.Micro);
            userId = me.Id;

            var (product, _, lot) = await NewMediaLotAsync(arrange, w.Micro.Id, me.Id);
            mediaProductId = product.Id;

            // Step requires 20-25 C, but last incubator is set to 32.5 C
            var (warmInc, _) = await NewEquipmentWithAssetAsync(
                arrange,
                w.Micro.Id,
                me.Id,
                EquipmentType.Incubator,
                setPointTemp: 32.5m);

            var (order, stepMedia) = await NewTestOrderWithStepMediaAsync(arrange, w, me.Id, w.Micro, product.Id, 20m, 25m);
            testOrderId = order.Id;
            stepMediaId = stepMedia.Id;

            await NewIncubationRecordAsync(arrange, order.Id, me.Id, lot.Id, warmInc.Id, NowUtc.AddHours(-1));
        }

        EntryDefaultsResponse response;
        await using (var act = _fixture.CreateDbContext())
        {
            response = await BuildService(act).GetAsync(
                userId,
                "incubation",
                testOrderId: testOrderId,
                stepMediaId: stepMediaId,
                mediaProductId: mediaProductId);
        }

        Assert.DoesNotContain(response.Defaults, d => d.Field == "incubatorEquipmentId");
    }

    [PostgresFact]
    public async Task LotForDifferentMediaProduct_NotOffered()
    {
        int userId, testOrderId, stepMediaId, requiredProductId;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            var me = await NewAnalystAsync(arrange, "diffprod", w.Micro);
            userId = me.Id;

            var (otherProduct, _, otherLot) = await NewMediaLotAsync(arrange, w.Micro.Id, me.Id);
            var (requiredProduct, _, _) = await NewMediaLotAsync(arrange, w.Micro.Id, me.Id);
            requiredProductId = requiredProduct.Id;

            var (inc, _) = await NewEquipmentWithAssetAsync(arrange, w.Micro.Id, me.Id, EquipmentType.Incubator, setPointTemp: 32.5m);
            var (order, stepMedia) = await NewTestOrderWithStepMediaAsync(arrange, w, me.Id, w.Micro, requiredProduct.Id, 30m, 35m);
            testOrderId = order.Id;
            stepMediaId = stepMedia.Id;

            // User's last incubation used a lot of otherProduct, not requiredProduct
            await NewIncubationRecordAsync(arrange, order.Id, me.Id, otherLot.Id, inc.Id, NowUtc.AddHours(-1));
        }

        EntryDefaultsResponse response;
        await using (var act = _fixture.CreateDbContext())
        {
            response = await BuildService(act).GetAsync(
                userId,
                "incubation",
                testOrderId: testOrderId,
                stepMediaId: stepMediaId,
                mediaProductId: requiredProductId);
        }

        Assert.DoesNotContain(response.Defaults, d => d.Field == "mediaLotId");
    }

    [PostgresFact]
    public async Task LotExpired_NotOffered()
    {
        int userId, testOrderId, stepMediaId, mediaProductId;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            var me = await NewAnalystAsync(arrange, "explot", w.Micro);
            userId = me.Id;

            var (product, _, expiredLot) = await NewMediaLotAsync(
                arrange,
                w.Micro.Id,
                me.Id,
                isReleased: true,
                status: MediaStatus.Active,
                expiryDate: NowUtc.AddMinutes(-1));
            mediaProductId = product.Id;

            var (inc, _) = await NewEquipmentWithAssetAsync(arrange, w.Micro.Id, me.Id, EquipmentType.Incubator, setPointTemp: 32.5m);
            var (order, stepMedia) = await NewTestOrderWithStepMediaAsync(arrange, w, me.Id, w.Micro, product.Id, 30m, 35m);
            testOrderId = order.Id;
            stepMediaId = stepMedia.Id;

            await NewIncubationRecordAsync(arrange, order.Id, me.Id, expiredLot.Id, inc.Id, NowUtc.AddHours(-2));
        }

        EntryDefaultsResponse response;
        await using (var act = _fixture.CreateDbContext())
        {
            response = await BuildService(act).GetAsync(
                userId,
                "incubation",
                testOrderId: testOrderId,
                stepMediaId: stepMediaId,
                mediaProductId: mediaProductId);
        }

        Assert.DoesNotContain(response.Defaults, d => d.Field == "mediaLotId");
    }

    [PostgresFact]
    public async Task LotNotReleased_NotOffered()
    {
        int userId, testOrderId, stepMediaId, mediaProductId;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            var me = await NewAnalystAsync(arrange, "unrellot", w.Micro);
            userId = me.Id;

            var (product, _, unreleasedLot) = await NewMediaLotAsync(
                arrange,
                w.Micro.Id,
                me.Id,
                isReleased: false,
                status: MediaStatus.Prepared);
            mediaProductId = product.Id;

            var (inc, _) = await NewEquipmentWithAssetAsync(arrange, w.Micro.Id, me.Id, EquipmentType.Incubator, setPointTemp: 32.5m);
            var (order, stepMedia) = await NewTestOrderWithStepMediaAsync(arrange, w, me.Id, w.Micro, product.Id, 30m, 35m);
            testOrderId = order.Id;
            stepMediaId = stepMedia.Id;

            await NewIncubationRecordAsync(arrange, order.Id, me.Id, unreleasedLot.Id, inc.Id, NowUtc.AddHours(-2));
        }

        EntryDefaultsResponse response;
        await using (var act = _fixture.CreateDbContext())
        {
            response = await BuildService(act).GetAsync(
                userId,
                "incubation",
                testOrderId: testOrderId,
                stepMediaId: stepMediaId,
                mediaProductId: mediaProductId);
        }

        Assert.DoesNotContain(response.Defaults, d => d.Field == "mediaLotId");
    }

    [PostgresFact]
    public async Task LotOfAnotherSection_NotOffered()
    {
        int userId, testOrderId, stepMediaId, mediaProductId;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            var me = await NewAnalystAsync(arrange, "othersec", w.Micro);
            userId = me.Id;

            // Lot belongs to FP section, while test order is in MICRO section
            var (product, _, fpLot) = await NewMediaLotAsync(arrange, w.Fp.Id, me.Id);
            mediaProductId = product.Id;

            var (inc, _) = await NewEquipmentWithAssetAsync(arrange, w.Micro.Id, me.Id, EquipmentType.Incubator, setPointTemp: 32.5m);
            var (order, stepMedia) = await NewTestOrderWithStepMediaAsync(arrange, w, me.Id, w.Micro, product.Id, 30m, 35m);
            testOrderId = order.Id;
            stepMediaId = stepMedia.Id;

            await NewIncubationRecordAsync(arrange, order.Id, me.Id, fpLot.Id, inc.Id, NowUtc.AddHours(-2));
        }

        EntryDefaultsResponse response;
        await using (var act = _fixture.CreateDbContext())
        {
            response = await BuildService(act).GetAsync(
                userId,
                "incubation",
                testOrderId: testOrderId,
                stepMediaId: stepMediaId,
                mediaProductId: mediaProductId);
        }

        Assert.DoesNotContain(response.Defaults, d => d.Field == "mediaLotId");
    }

    [PostgresFact]
    public async Task EquipmentInSectionINoLongerBelongTo_NotOffered()
    {
        int userId, testOrderId, stepMediaId, mediaProductId;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            // User belongs only to MICRO, but equipment is in FP
            var me = await NewAnalystAsync(arrange, "movedsec", w.Micro);
            userId = me.Id;

            var (product, _, lot) = await NewMediaLotAsync(arrange, w.Micro.Id, me.Id);
            mediaProductId = product.Id;

            var (fpInc, _) = await NewEquipmentWithAssetAsync(arrange, w.Fp.Id, me.Id, EquipmentType.Incubator, setPointTemp: 32.5m);
            var (order, stepMedia) = await NewTestOrderWithStepMediaAsync(arrange, w, me.Id, w.Micro, product.Id, 30m, 35m);
            testOrderId = order.Id;
            stepMediaId = stepMedia.Id;

            await NewIncubationRecordAsync(arrange, order.Id, me.Id, lot.Id, fpInc.Id, NowUtc.AddHours(-2));
        }

        EntryDefaultsResponse response;
        await using (var act = _fixture.CreateDbContext())
        {
            response = await BuildService(act).GetAsync(
                userId,
                "incubation",
                testOrderId: testOrderId,
                stepMediaId: stepMediaId,
                mediaProductId: mediaProductId);
        }

        Assert.DoesNotContain(response.Defaults, d => d.Field == "incubatorEquipmentId");
    }

    [PostgresFact]
    public async Task SettingOff_ReturnsEmpty()
    {
        int userId, testOrderId, stepMediaId, mediaProductId;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            var me = await NewAnalystAsync(arrange, "setoff", w.Micro);
            userId = me.Id;

            var (product, _, lot) = await NewMediaLotAsync(arrange, w.Micro.Id, me.Id);
            mediaProductId = product.Id;

            var (inc, _) = await NewEquipmentWithAssetAsync(arrange, w.Micro.Id, me.Id, EquipmentType.Incubator, setPointTemp: 32.5m);
            var (order, stepMedia) = await NewTestOrderWithStepMediaAsync(arrange, w, me.Id, w.Micro, product.Id, 30m, 35m);
            testOrderId = order.Id;
            stepMediaId = stepMedia.Id;

            await NewIncubationRecordAsync(arrange, order.Id, me.Id, lot.Id, inc.Id, NowUtc.AddHours(-1));
        }

        EntryDefaultsResponse response;
        await using (var act = _fixture.CreateDbContext())
        {
            response = await BuildService(act, new StubSettings(enabled: false)).GetAsync(
                userId,
                "incubation",
                testOrderId: testOrderId,
                stepMediaId: stepMediaId,
                mediaProductId: mediaProductId);
        }

        Assert.Empty(response.Defaults);
    }

    [PostgresFact]
    public async Task NoHistory_ReturnsEmpty()
    {
        int userId, testOrderId, stepMediaId, mediaProductId;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            var me = await NewAnalystAsync(arrange, "nohist", w.Micro);
            userId = me.Id;

            var (product, _, _) = await NewMediaLotAsync(arrange, w.Micro.Id, me.Id);
            mediaProductId = product.Id;

            var (order, stepMedia) = await NewTestOrderWithStepMediaAsync(arrange, w, me.Id, w.Micro, product.Id, 30m, 35m);
            testOrderId = order.Id;
            stepMediaId = stepMedia.Id;
        }

        EntryDefaultsResponse response;
        await using (var act = _fixture.CreateDbContext())
        {
            response = await BuildService(act).GetAsync(
                userId,
                "incubation",
                testOrderId: testOrderId,
                stepMediaId: stepMediaId,
                mediaProductId: mediaProductId);
        }

        Assert.Empty(response.Defaults);
    }

    [PostgresFact]
    public async Task Receiving_ReturnsSampledByAndCause_ForSameCategoryOnly()
    {
        int userId, causeId;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            var me = await NewAnalystAsync(arrange, "recvcat", w.Micro);
            userId = me.Id;

            var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var cause = new CauseOfTesting { Name = $"Routine {uid}", IsActive = true };
            arrange.CausesOfTesting.Add(cause);
            await arrange.SaveChangesAsync();
            causeId = cause.Id;

            arrange.Samples.Add(new Sample
            {
                ReferenceNumber = $"FP-CAT-{uid}",
                Category = SampleCategory.FinishedProduct,
                CauseOfTestingId = cause.Id,
                SampledBy = "Sampler Sam",
                ReceivedByUserId = me.Id,
                ReceivedAt = NowUtc.AddHours(-1),
                LabSectionId = w.Micro.Id,
                Status = SampleStatus.Received
            });
            await arrange.SaveChangesAsync();
        }

        EntryDefaultsResponse sameCategory;
        EntryDefaultsResponse differentCategory;
        await using (var act = _fixture.CreateDbContext())
        {
            var service = BuildService(act);
            sameCategory = await service.GetAsync(userId, "receiving", category: "product");
            differentCategory = await service.GetAsync(userId, "receiving", category: "water");
        }

        var sampledBy = Assert.Single(sameCategory.Defaults, d => d.Field == "sampledBy");
        Assert.Equal("Sampler Sam", sampledBy.Text);
        var causeDefault = Assert.Single(sameCategory.Defaults, d => d.Field == "causeOfTestingId");
        Assert.Equal(causeId, causeDefault.Id);

        Assert.Empty(differentCategory.Defaults);
    }

    [PostgresFact]
    public async Task InactiveCause_NotOffered()
    {
        int userId;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            var me = await NewAnalystAsync(arrange, "inactcause", w.Micro);
            userId = me.Id;

            var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var inactiveCause = new CauseOfTesting { Name = $"Retired {uid}", IsActive = false };
            arrange.CausesOfTesting.Add(inactiveCause);
            await arrange.SaveChangesAsync();

            arrange.Samples.Add(new Sample
            {
                ReferenceNumber = $"WT-CAUSE-{uid}",
                Category = SampleCategory.Water,
                CauseOfTestingId = inactiveCause.Id,
                SampledBy = "Water Sampler",
                ReceivedByUserId = me.Id,
                ReceivedAt = NowUtc.AddHours(-1),
                LabSectionId = w.Micro.Id,
                Status = SampleStatus.Received
            });
            await arrange.SaveChangesAsync();
        }

        EntryDefaultsResponse response;
        await using (var act = _fixture.CreateDbContext())
        {
            response = await BuildService(act).GetAsync(userId, "receiving", category: "water");
        }

        Assert.DoesNotContain(response.Defaults, d => d.Field == "causeOfTestingId");
        var sampledBy = Assert.Single(response.Defaults, d => d.Field == "sampledBy");
        Assert.Equal("Water Sampler", sampledBy.Text);
    }

    [PostgresFact]
    public async Task MediaPreparation_And_SolutionPreparation_ReturnMyLastAutoclave()
    {
        int userId, mediaAutoclaveId, solutionAutoclaveId, solutionMasterId;
        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            var me = await NewAnalystAsync(arrange, "autoclave", w.Micro);
            userId = me.Id;

            var (mediaAutoclave, _) = await NewEquipmentWithAssetAsync(
                arrange,
                w.Micro.Id,
                me.Id,
                EquipmentType.Autoclave,
                setPointTemp: null,
                status: EquipmentOperationalStatus.InService,
                calibrationDueDate: NowUtc.Date.AddDays(15));
            mediaAutoclaveId = mediaAutoclave.Id;

            var (solAutoclave, _) = await NewEquipmentWithAssetAsync(
                arrange,
                w.Micro.Id,
                me.Id,
                EquipmentType.Autoclave,
                setPointTemp: null,
                status: EquipmentOperationalStatus.InService,
                calibrationDueDate: NowUtc.Date.AddDays(15));
            solutionAutoclaveId = solAutoclave.Id;

            await NewMediaLotAsync(
                arrange,
                w.Micro.Id,
                me.Id,
                autoclaveEquipmentId: mediaAutoclave.Id,
                preparedAt: NowUtc.AddHours(-3));

            var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var solMaster = new SolutionMaster
            {
                SectionId = w.Micro.Id,
                Name = $"Diluent {uid}",
                Type = SolutionType.Diluent,
                ShelfLifeValue = 7,
                ShelfLifeUnit = ShelfLifeUnit.Days,
                StorageCondition = "Room temp",
                FinalVolumeMl = 1000m,
                Instructions = "Mix",
                SterilizationRequired = true,
                IsActive = true,
                CreatedByUserId = me.Id,
                CreatedAt = NowUtc.AddDays(-10),
                LastModifiedByUserId = me.Id,
                LastModifiedAt = NowUtc.AddDays(-10)
            };
            arrange.SolutionMasters.Add(solMaster);
            await arrange.SaveChangesAsync();
            solutionMasterId = solMaster.Id;

            var snapshotJson = JsonSerializer.Serialize(SolutionMasterResponse.From(solMaster), SnapshotJson.Options);
            arrange.SolutionPreparations.Add(new SolutionPreparation
            {
                SectionId = w.Micro.Id,
                Code = $"DL-{uid}-01",
                SolutionMasterId = solMaster.Id,
                Type = SolutionType.Diluent,
                RecipeSnapshotJson = snapshotJson,
                Status = SolutionPreparationStatus.Prepared,
                StartedByUserId = me.Id,
                StartedAt = NowUtc.AddHours(-2),
                PreparedByUserId = me.Id,
                PreparedAt = NowUtc.AddHours(-1),
                ExpiresAt = NowUtc.AddDays(7),
                FinalVolumeMl = 1000m,
                AutoclaveEquipmentId = solAutoclave.Id
            });
            await arrange.SaveChangesAsync();
        }

        EntryDefaultsResponse mediaResp;
        EntryDefaultsResponse solResp;
        await using (var act = _fixture.CreateDbContext())
        {
            var service = BuildService(act);
            mediaResp = await service.GetAsync(userId, "mediaPreparation");
            solResp = await service.GetAsync(userId, "solutionPreparation", solutionMasterId: solutionMasterId);
        }

        var mediaAc = Assert.Single(mediaResp.Defaults, d => d.Field == "autoclaveEquipmentId");
        Assert.Equal(mediaAutoclaveId, mediaAc.Id);

        var solAc = Assert.Single(solResp.Defaults, d => d.Field == "autoclaveEquipmentId");
        Assert.Equal(solutionAutoclaveId, solAc.Id);
    }

    [PostgresFact]
    public async Task EndpointSavesNothing()
    {
        int userId, testOrderId, stepMediaId, mediaProductId;
        int initialIncubations, initialSamples, initialMedia, initialAudits;

        await using (var arrange = _fixture.CreateDbContext())
        {
            var w = await WorldAsync(arrange);
            var me = await NewAnalystAsync(arrange, "nosave", w.Micro);
            userId = me.Id;

            var (product, _, lot) = await NewMediaLotAsync(arrange, w.Micro.Id, me.Id);
            mediaProductId = product.Id;

            var (inc, _) = await NewEquipmentWithAssetAsync(arrange, w.Micro.Id, me.Id, EquipmentType.Incubator, setPointTemp: 32.5m);
            var (order, stepMedia) = await NewTestOrderWithStepMediaAsync(arrange, w, me.Id, w.Micro, product.Id, 30m, 35m);
            testOrderId = order.Id;
            stepMediaId = stepMedia.Id;

            await NewIncubationRecordAsync(arrange, order.Id, me.Id, lot.Id, inc.Id, NowUtc.AddHours(-1));

            initialIncubations = await arrange.Incubations.AsNoTracking().CountAsync();
            initialSamples = await arrange.Samples.AsNoTracking().CountAsync();
            initialMedia = await arrange.Media.AsNoTracking().CountAsync();
            initialAudits = await arrange.AuditLogs.AsNoTracking().CountAsync();
        }

        await using (var act = _fixture.CreateDbContext())
        {
            var controller = BuildController(BuildService(act), userId);
            var result = await controller.Get(
                context: "incubation",
                testOrderId: testOrderId,
                stepMediaId: stepMediaId,
                mediaProductId: mediaProductId);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var apiResponse = Assert.IsType<ApiResponse<EntryDefaultsResponse>>(ok.Value);
            Assert.True(apiResponse.Success);
            Assert.Equal(2, apiResponse.Data!.Defaults.Count);
        }

        await using (var assertDb = _fixture.CreateDbContext())
        {
            Assert.Equal(initialIncubations, await assertDb.Incubations.AsNoTracking().CountAsync());
            Assert.Equal(initialSamples, await assertDb.Samples.AsNoTracking().CountAsync());
            Assert.Equal(initialMedia, await assertDb.Media.AsNoTracking().CountAsync());
            Assert.Equal(initialAudits, await assertDb.AuditLogs.AsNoTracking().CountAsync());
        }
    }
}
