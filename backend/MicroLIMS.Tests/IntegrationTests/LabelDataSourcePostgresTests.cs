using System.Text.Json;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Labels;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class LabelDataSourcePostgresTests
{
    private const string Password = "Correct-Horse-9";

    // 22:30 UTC on 2026-11-12 is 00:30 on 13 Nov 2026 in Africa/Cairo (UTC+2),
    // proving dates on labels convert to lab-local time.
    private static readonly DateTime FixedUtc = new(2026, 11, 12, 22, 30, 0, DateTimeKind.Utc);
    private static readonly TimeZoneInfo CairoZone = LabClock.ResolveTimeZone("Africa/Cairo");

    private readonly PostgresTestFixture _fixture;

    public LabelDataSourcePostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static ILabClock FixedClock(DateTime utcNow) =>
        new LabClock(new FixedTimeProvider(new DateTimeOffset(utcNow, TimeSpan.Zero)), CairoZone);

    private static ILabelDataSource CreateDataSource(MicroLimsDbContext db, DateTime? utcNow = null) =>
        new LabelDataSource(db, new UserSectionScopeService(db), FixedClock(utcNow ?? FixedUtc));

    private static ILabelFormatService CreateFormatService(MicroLimsDbContext db) =>
        new LabelFormatService(db);

    private static async Task<User> CreateUserInSectionAsync(
        MicroLimsDbContext db,
        string fullName,
        string username,
        int deptId,
        int sectionId,
        RoleType roleType = RoleType.Analyst)
    {
        var role = new Role { Type = roleType, Name = $"Role-{username}", IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User
        {
            FullName = fullName,
            Username = username.ToLowerInvariant(),
            PasswordHash = TestPasswords.Hash(Password),
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.UserOrgMemberships.Add(new UserOrgMembership
        {
            UserId = user.Id,
            DepartmentId = deptId,
            SectionId = sectionId
        });
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<User> CreateViewAllUserWithoutMembershipAsync(MicroLimsDbContext db, string uid)
    {
        var viewAllPerm = await db.Permissions.SingleAsync(p => p.Code == PermissionConstants.SectionsViewAll);
        var role = new Role { Type = RoleType.Qa, Name = $"QaViewAll-{uid}", IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = viewAllPerm.Id });
        var user = new User
        {
            FullName = $"QA Viewer {uid}",
            Username = $"qa_{uid}".ToLowerInvariant(),
            PasswordHash = TestPasswords.Hash(Password),
            RoleId = role.Id,
            IsActive = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private sealed record SeededWorld(
        int MicroUserId,
        int FpUserId,
        int QaViewAllUserId,
        int SampleId,
        string SampleCode,
        int MediaId,
        string MediaCode,
        int SolutionId,
        string SolutionCode,
        int CryovialId,
        string CryovialCode,
        int MaterialId,
        string MaterialStockId,
        int EquipmentInventoryId,
        string EquipmentCode,
        int LinkedEquipmentId);

    private async Task<SeededWorld> SeedAllSixTypesInMicroAsync()
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        await using var db = _fixture.CreateDbContext();

        var micro = await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");
        var fp = await db.DocumentSections.FirstAsync(s => s.Code == "FP");

        var microUser = await CreateUserInSectionAsync(db, "Alice Mary Doe", $"micro_{uid}", micro.DepartmentId, micro.Id);
        var fpUser = await CreateUserInSectionAsync(db, "Bob Smith", $"fp_{uid}", fp.DepartmentId, fp.Id);
        var qaUser = await CreateViewAllUserWithoutMembershipAsync(db, uid);

        // 1. Sample
        var testCode = $"TD-{uid}";
        db.TestDefinitions.Add(new TestDefinition { Code = testCode, DisplayName = $"Test {uid}", SectionId = micro.Id, IsActive = true });
        var item = new Item { Code = $"ITM-{uid}", Name = $"Product {uid}", Category = SampleCategory.FinishedProduct, IsActive = true };
        item.AssignedTests.Add(new SampleTest { TestCode = testCode, DisplayName = $"Test {uid}" });
        db.Items.Add(item);
        await db.SaveChangesAsync();

        var cause = await db.CausesOfTesting.FirstAsync();
        var sample = new Sample
        {
            ReferenceNumber = $"FP-{uid}",
            Category = SampleCategory.FinishedProduct,
            ItemId = item.Id,
            CauseOfTestingId = cause.Id,
            SampledBy = "Sampler",
            BatchNumber = $"BAT-{uid}",
            ControlNumber = $"CTRL-{uid}",
            ReceivedByUserId = microUser.Id,
            ReceivedAt = FixedUtc,
            LabSectionId = micro.Id,
            Status = SampleStatus.Received
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        // 5. MaterialStockLot (also parent for Media & Cryovial)
        var stockMaterial = new Material
        {
            SectionId = micro.Id,
            MaterialType = MaterialType.Chemical,
            MaterialName = $"Buffer Salt {uid}",
            ManufacturerName = "ChemCorp",
            BatchNumber = $"SUP-{uid}",
            ReceivingDate = FixedUtc,
            ExpiryDate = FixedUtc.AddDays(60),
            StockId = $"STK-2026-{uid}",
            Location = "Shelf A",
            StorageCondition = "2-8 C",
            QuantityReceived = 500m,
            QuantityRemaining = 250m,
            Unit = MaterialUnit.Gram,
            CreatedByUserId = microUser.Id,
            LastModifiedByUserId = microUser.Id
        };
        var mediaSource = new Material
        {
            SectionId = micro.Id,
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = $"Agar Base {uid}",
            ManufacturerName = "MediaCorp",
            BatchNumber = $"MB-{uid}",
            ReceivingDate = FixedUtc,
            ExpiryDate = FixedUtc.AddYears(1),
            StockId = $"STK-M-{uid}",
            Location = "Media Room",
            QuantityReceived = 500m,
            QuantityRemaining = 400m,
            Unit = MaterialUnit.Gram,
            CreatedByUserId = microUser.Id,
            LastModifiedByUserId = microUser.Id
        };
        var strainSource = new Material
        {
            SectionId = micro.Id,
            MaterialType = MaterialType.LyophilizedMicroorganism,
            MaterialName = $"Strain Lot {uid}",
            ManufacturerName = "CultureBank",
            BatchNumber = $"CRB-{uid}",
            ReceivingDate = FixedUtc,
            ExpiryDate = FixedUtc.AddYears(1),
            StockId = $"STK-C-{uid}",
            Location = "Freezer",
            QuantityReceived = 10m,
            QuantityRemaining = 8m,
            Unit = MaterialUnit.Vial,
            CreatedByUserId = microUser.Id,
            LastModifiedByUserId = microUser.Id
        };
        db.Materials.AddRange(stockMaterial, mediaSource, strainSource);
        await db.SaveChangesAsync();

        // 2. MediaLot
        var media = new Media
        {
            MaterialId = mediaSource.Id,
            LotNumber = $"MED-{uid}/01",
            ManufacturerLot = $"ML-{uid}",
            ManufacturerName = "MediaCorp",
            TotalWeight = 10m,
            TotalVolume = "500 ml",
            AutoclaveProgram = "P1",
            LoadType = "liquid",
            Temperature = 121m,
            CycleTime = 15,
            CycleNumber = 1,
            Ph = 7.2m,
            PreparedAt = FixedUtc,
            ExpiryDate = FixedUtc.AddDays(30),
            PreparedByUserId = microUser.Id,
            Status = MediaStatus.Active,
            ApprovalStatus = ApprovalGateStatus.Approved,
            IsReleasedForUse = true
        };
        db.Media.Add(media);

        // 3. SolutionPreparation
        var solMaster = new SolutionMaster
        {
            SectionId = micro.Id,
            Name = $"Diluent Master {uid}",
            Type = SolutionType.Diluent,
            ShelfLifeValue = 7,
            ShelfLifeUnit = ShelfLifeUnit.Days,
            StorageCondition = "15-25 C",
            FinalVolumeMl = 1000m,
            Instructions = "Dissolve",
            IsActive = true,
            CreatedByUserId = microUser.Id,
            CreatedAt = FixedUtc,
            LastModifiedByUserId = microUser.Id,
            LastModifiedAt = FixedUtc
        };
        db.SolutionMasters.Add(solMaster);
        await db.SaveChangesAsync();

        var snapshotJson = JsonSerializer.Serialize(SolutionMasterResponse.From(solMaster), SnapshotJson.Options);
        var solution = new SolutionPreparation
        {
            SectionId = micro.Id,
            Code = $"DL-{uid} 01",
            SolutionMasterId = solMaster.Id,
            Type = SolutionType.Diluent,
            RecipeSnapshotJson = snapshotJson,
            Status = SolutionPreparationStatus.Prepared,
            StartedByUserId = microUser.Id,
            StartedAt = FixedUtc,
            PreparedByUserId = microUser.Id,
            PreparedAt = FixedUtc,
            ExpiresAt = FixedUtc.AddDays(7),
            FinalVolumeMl = 1000m,
            ApprovalStatus = null
        };
        db.SolutionPreparations.Add(solution);

        // 4. Cryovial
        var organism = new Organism { ScientificName = $"Test Strain {uid}", AtccNumber = $"REF-{uid}" };
        db.Organisms.Add(organism);
        await db.SaveChangesAsync();

        var cryovial = new Cryovial
        {
            Code = $"CRY-{uid}/01",
            MaterialId = strainSource.Id,
            OrganismId = organism.Id,
            OrganismNameSnapshot = organism.ScientificName,
            ManufacturerName = "CultureBank",
            PreparedAt = FixedUtc,
            ExpiryDate = FixedUtc.AddDays(90),
            NumberOfVialsPrepared = 5,
            VialsRemaining = 4,
            StorageCondition = "-70 C",
            PhysicalCheckConfirmed = true,
            PhysicalCheckText = "Clear",
            PreparedByUserId = microUser.Id,
            ApprovalStatus = ApprovalGateStatus.Approved,
            IsDestroyed = false
        };
        db.Cryovials.Add(cryovial);

        // 6. EquipmentAsset + linked workflow Equipment
        var asset = new EquipmentInventory
        {
            Code = $"INC-{uid}",
            InstrumentType = "Incubator",
            ManufacturerName = "Binder",
            Location = "Lab 1",
            Status = EquipmentOperationalStatus.InService,
            SectionId = micro.Id,
            CreatedByUserId = microUser.Id,
            LastModifiedByUserId = microUser.Id
        };
        db.EquipmentInventories.Add(asset);
        await db.SaveChangesAsync();

        var workflowEquip = new Equipment
        {
            Name = $"Incubator {uid}",
            Code = $"WFEQ-{uid}",
            Type = EquipmentType.Incubator,
            SectionId = micro.Id,
            EquipmentInventoryId = asset.Id
        };
        db.Equipment.Add(workflowEquip);
        await db.SaveChangesAsync();

        return new SeededWorld(
            microUser.Id, fpUser.Id, qaUser.Id,
            sample.Id, sample.ReferenceNumber,
            media.Id, media.LotNumber,
            solution.Id, solution.Code!,
            cryovial.Id, cryovial.Code,
            stockMaterial.Id, stockMaterial.StockId!,
            asset.Id, asset.Code,
            workflowEquip.Id);
    }

    [PostgresFact]
    public async Task Finds_each_of_the_six_types_by_id_and_by_code()
    {
        var w = await SeedAllSixTypesInMicroAsync();

        await using var actDb = _fixture.CreateDbContext();
        var ds = CreateDataSource(actDb);

        // FixedUtc = 2026-11-12 22:30 UTC -> 13 Nov 2026 in Africa/Cairo
        const string expectedPrepDate = "13 Nov 2026";

        var sampleById = await ds.FindByIdAsync(LabelEntityType.Sample, w.SampleId, w.MicroUserId, CancellationToken.None);
        var sampleByCode = await ds.FindByCodeAsync(LabelEntityType.Sample, w.SampleCode, w.MicroUserId, CancellationToken.None);
        AssertSameLabelRecord(sampleById, sampleByCode);
        Assert.Equal(w.SampleCode, sampleById!.Code);
        Assert.Equal(w.SampleId, sampleById.SampleId);
        Assert.Equal(PermissionConstants.SamplesReceiveOwnLab, sampleById.CreatePermission);
        Assert.True(sampleById.Usable);
        Assert.Null(sampleById.BlockedReason);
        Assert.Contains(sampleById.Content!.StandardFields, f => f.Value == expectedPrepDate);
        Assert.Empty(sampleById.Content.SmallFields);

        var mediaById = await ds.FindByIdAsync(LabelEntityType.MediaLot, w.MediaId, w.MicroUserId, CancellationToken.None);
        var mediaByCode = await ds.FindByCodeAsync(LabelEntityType.MediaLot, w.MediaCode, w.MicroUserId, CancellationToken.None);
        AssertSameLabelRecord(mediaById, mediaByCode);
        Assert.Equal(PermissionConstants.MediaPrepare, mediaById!.CreatePermission);
        Assert.True(mediaById.Usable);
        Assert.Contains(mediaById.Content!.StandardFields, f => f.Value == "AMD");
        Assert.Contains(mediaById.Content.StandardFields, f => f.Value == expectedPrepDate);
        Assert.Single(mediaById.Content.SmallFields);

        var solById = await ds.FindByIdAsync(LabelEntityType.SolutionPreparation, w.SolutionId, w.MicroUserId, CancellationToken.None);
        var solByCode = await ds.FindByCodeAsync(LabelEntityType.SolutionPreparation, w.SolutionCode, w.MicroUserId, CancellationToken.None);
        AssertSameLabelRecord(solById, solByCode);
        Assert.Equal(PermissionConstants.SolutionsPrepare, solById!.CreatePermission);
        Assert.True(solById.Usable);
        Assert.Contains(solById.Content!.StandardFields, f => f.Value == "15-25 C");
        Assert.Contains(solById.Content.StandardFields, f => f.Value == "AMD");
        Assert.Single(solById.Content.SmallFields);

        var cryById = await ds.FindByIdAsync(LabelEntityType.Cryovial, w.CryovialId, w.MicroUserId, CancellationToken.None);
        var cryByCode = await ds.FindByCodeAsync(LabelEntityType.Cryovial, w.CryovialCode, w.MicroUserId, CancellationToken.None);
        AssertSameLabelRecord(cryById, cryByCode);
        Assert.Equal(PermissionConstants.CryovialsManage, cryById!.CreatePermission);
        Assert.True(cryById.Usable);
        Assert.Single(cryById.Content!.SmallFields);

        var matById = await ds.FindByIdAsync(LabelEntityType.MaterialStockLot, w.MaterialId, w.MicroUserId, CancellationToken.None);
        var matByCode = await ds.FindByCodeAsync(LabelEntityType.MaterialStockLot, w.MaterialStockId, w.MicroUserId, CancellationToken.None);
        AssertSameLabelRecord(matById, matByCode);
        Assert.Equal(PermissionConstants.MaterialsManage, matById!.CreatePermission);
        Assert.True(matById.Usable);
        Assert.Contains(matById.Content!.StandardFields, f => f.Value == "2-8 C");
        Assert.Single(matById.Content.SmallFields);

        var eqById = await ds.FindByIdAsync(LabelEntityType.EquipmentAsset, w.EquipmentInventoryId, w.MicroUserId, CancellationToken.None);
        var eqByCode = await ds.FindByCodeAsync(LabelEntityType.EquipmentAsset, w.EquipmentCode, w.MicroUserId, CancellationToken.None);
        AssertSameLabelRecord(eqById, eqByCode);
        Assert.Equal(PermissionConstants.EquipmentManage, eqById!.CreatePermission);
        Assert.Equal(w.LinkedEquipmentId, eqById.LinkedEquipmentId);
        Assert.True(eqById.Usable);
        Assert.Empty(eqById.Content!.SmallFields);
    }

    private static void AssertSameLabelRecord(LabelRecord? expected, LabelRecord? actual)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected!.Type, actual!.Type);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Code, actual.Code);
        Assert.Equal(expected.DisplayName, actual.DisplayName);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Usable, actual.Usable);
        Assert.Equal(expected.BlockedReason, actual.BlockedReason);
        Assert.Equal(expected.SampleId, actual.SampleId);
        Assert.Equal(expected.CreatePermission, actual.CreatePermission);
        Assert.Equal(expected.LinkedEquipmentId, actual.LinkedEquipmentId);
        Assert.Equal(expected.Content is null, actual.Content is null);
        if (expected.Content is not null && actual.Content is not null)
        {
            Assert.Equal(expected.Content.Type, actual.Content.Type);
            Assert.Equal(expected.Content.Code, actual.Content.Code);
            Assert.Equal(expected.Content.StandardFields, actual.Content.StandardFields);
            Assert.Equal(expected.Content.SmallFields, actual.Content.SmallFields);
        }
    }

    [PostgresFact]
    public async Task Returns_null_for_a_record_in_another_section()
    {
        var w = await SeedAllSixTypesInMicroAsync();

        await using var actDb = _fixture.CreateDbContext();
        var ds = CreateDataSource(actDb);

        var targets = new (LabelEntityType Type, int Id, string Code)[]
        {
            (LabelEntityType.Sample, w.SampleId, w.SampleCode),
            (LabelEntityType.MediaLot, w.MediaId, w.MediaCode),
            (LabelEntityType.SolutionPreparation, w.SolutionId, w.SolutionCode),
            (LabelEntityType.Cryovial, w.CryovialId, w.CryovialCode),
            (LabelEntityType.MaterialStockLot, w.MaterialId, w.MaterialStockId),
            (LabelEntityType.EquipmentAsset, w.EquipmentInventoryId, w.EquipmentCode)
        };

        foreach (var (type, id, code) in targets)
        {
            Assert.Null(await ds.FindByIdAsync(type, id, w.FpUserId, CancellationToken.None));
            Assert.Null(await ds.FindByCodeAsync(type, code, w.FpUserId, CancellationToken.None));

            // A QA-style user holding Sections.ViewAll with no section membership can read all sections.
            Assert.NotNull(await ds.FindByIdAsync(type, id, w.QaViewAllUserId, CancellationToken.None));
            Assert.NotNull(await ds.FindByCodeAsync(type, code, w.QaViewAllUserId, CancellationToken.None));
        }
    }

    [PostgresFact]
    public async Task Solution_in_progress_has_null_code()
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        int userId;
        int prepId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");
            var user = await CreateUserInSectionAsync(arrangeDb, "Sol User", $"sol_{uid}", micro.DepartmentId, micro.Id);
            userId = user.Id;

            var master = new SolutionMaster
            {
                SectionId = micro.Id,
                Name = $"InProg Master {uid}",
                Type = SolutionType.Reagent,
                ShelfLifeValue = 5,
                ShelfLifeUnit = ShelfLifeUnit.Days,
                StorageCondition = "RT",
                FinalVolumeMl = 100m,
                Instructions = "Mix",
                IsActive = true,
                CreatedByUserId = userId,
                CreatedAt = FixedUtc,
                LastModifiedByUserId = userId,
                LastModifiedAt = FixedUtc
            };
            arrangeDb.SolutionMasters.Add(master);
            await arrangeDb.SaveChangesAsync();

            var prep = new SolutionPreparation
            {
                SectionId = micro.Id,
                Code = null,
                SolutionMasterId = master.Id,
                Type = SolutionType.Reagent,
                RecipeSnapshotJson = JsonSerializer.Serialize(SolutionMasterResponse.From(master), SnapshotJson.Options),
                Status = SolutionPreparationStatus.InProgress,
                StartedByUserId = userId,
                StartedAt = FixedUtc
            };
            arrangeDb.SolutionPreparations.Add(prep);
            await arrangeDb.SaveChangesAsync();
            prepId = prep.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        var record = await CreateDataSource(actDb).FindByIdAsync(LabelEntityType.SolutionPreparation, prepId, userId, CancellationToken.None);

        Assert.NotNull(record);
        Assert.Null(record!.Code);
        Assert.Null(record.Content);
        Assert.False(record.Usable);
        Assert.Equal("In progress.", record.BlockedReason);
    }

    [PostgresFact]
    public async Task Legacy_stock_lot_without_stock_id_has_null_code()
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        int userId;
        int materialId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");
            var user = await CreateUserInSectionAsync(arrangeDb, "Mat User", $"mat_{uid}", micro.DepartmentId, micro.Id);
            userId = user.Id;

            var mat = new Material
            {
                SectionId = micro.Id,
                MaterialType = MaterialType.Chemical,
                MaterialName = $"Legacy Chemical {uid}",
                ManufacturerName = "ChemCorp",
                BatchNumber = $"LEG-{uid}",
                ReceivingDate = FixedUtc.AddYears(-2),
                ExpiryDate = FixedUtc.AddDays(10),
                StockId = null,
                Location = "Shelf B",
                QuantityReceived = 100m,
                QuantityRemaining = 0m,
                Unit = MaterialUnit.Gram,
                CreatedByUserId = userId,
                LastModifiedByUserId = userId
            };
            arrangeDb.Materials.Add(mat);
            await arrangeDb.SaveChangesAsync();
            materialId = mat.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        var record = await CreateDataSource(actDb).FindByIdAsync(LabelEntityType.MaterialStockLot, materialId, userId, CancellationToken.None);

        Assert.NotNull(record);
        Assert.Null(record!.Code);
        Assert.Null(record.Content);
        Assert.False(record.Usable);
        Assert.Equal("Depleted.", record.BlockedReason);
    }

    [PostgresFact]
    public async Task Code_lookup_is_exact_not_contains()
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var w = await SeedAllSixTypesInMicroAsync();

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");
            var src = new Material
            {
                SectionId = micro.Id,
                MaterialType = MaterialType.DehydratedMedia,
                MaterialName = $"Base {uid}",
                ManufacturerName = "MediaCorp",
                BatchNumber = $"EX-{uid}",
                ReceivingDate = FixedUtc,
                ExpiryDate = FixedUtc.AddYears(1),
                StockId = $"STK-EX-{uid}",
                Location = "Room",
                QuantityReceived = 100m,
                QuantityRemaining = 100m,
                Unit = MaterialUnit.Gram,
                CreatedByUserId = w.MicroUserId,
                LastModifiedByUserId = w.MicroUserId
            };
            arrangeDb.Materials.Add(src);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.Media.Add(new Media
            {
                MaterialId = src.Id,
                LotNumber = $"TSA-{uid}/03/26",
                ManufacturerLot = "ML",
                ManufacturerName = "MediaCorp",
                TotalWeight = 5m,
                TotalVolume = "100 ml",
                AutoclaveProgram = "P1",
                LoadType = "liquid",
                Temperature = 121m,
                CycleTime = 15,
                CycleNumber = 1,
                Ph = 7.2m,
                PreparedAt = FixedUtc,
                ExpiryDate = FixedUtc.AddDays(30),
                PreparedByUserId = w.MicroUserId,
                Status = MediaStatus.Active,
                ApprovalStatus = ApprovalGateStatus.Approved,
                IsReleasedForUse = true
            });
            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = _fixture.CreateDbContext();
        var ds = CreateDataSource(actDb);

        Assert.Null(await ds.FindByCodeAsync(LabelEntityType.MediaLot, $"TSA-{uid}/03/2", w.MicroUserId, CancellationToken.None));
        Assert.NotNull(await ds.FindByCodeAsync(LabelEntityType.MediaLot, $"TSA-{uid}/03/26", w.MicroUserId, CancellationToken.None));
    }

    [PostgresFact]
    public async Task Code_lookup_is_case_insensitive()
    {
        var w = await SeedAllSixTypesInMicroAsync();

        await using var actDb = _fixture.CreateDbContext();
        var ds = CreateDataSource(actDb);

        var found = await ds.FindByCodeAsync(LabelEntityType.Sample, w.SampleCode.ToLowerInvariant(), w.MicroUserId, CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal(w.SampleId, found!.Id);
        Assert.Equal(w.SampleCode, found.Code);
    }

    [PostgresFact]
    public async Task Code_lookup_treats_percent_and_underscore_literally()
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        int userId;
        var literalCode = $"EQ_{uid}%1";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");
            var user = await CreateUserInSectionAsync(arrangeDb, "Wildcard User", $"wld_{uid}", micro.DepartmentId, micro.Id);
            userId = user.Id;

            arrangeDb.EquipmentInventories.AddRange(
                new EquipmentInventory
                {
                    Code = $"EQX{uid}ABC1",
                    InstrumentType = "Balance",
                    ManufacturerName = "Mettler",
                    Location = "Lab 1",
                    Status = EquipmentOperationalStatus.InService,
                    SectionId = micro.Id,
                    CreatedByUserId = userId,
                    LastModifiedByUserId = userId
                },
                new EquipmentInventory
                {
                    Code = literalCode,
                    InstrumentType = "pH Meter",
                    ManufacturerName = "Mettler",
                    Location = "Lab 2",
                    Status = EquipmentOperationalStatus.InService,
                    SectionId = micro.Id,
                    CreatedByUserId = userId,
                    LastModifiedByUserId = userId
                });
            await arrangeDb.SaveChangesAsync();
        }

        await using var actDb = _fixture.CreateDbContext();
        var ds = CreateDataSource(actDb);

        // Querying with % and _ must not wildcard-match EQX{uid}ABC1 when only EQX{uid}ABC1 exists for a different pattern.
        Assert.Null(await ds.FindByCodeAsync(LabelEntityType.EquipmentAsset, $"EQ_{uid}%9", userId, CancellationToken.None));

        var exact = await ds.FindByCodeAsync(LabelEntityType.EquipmentAsset, literalCode.ToLowerInvariant(), userId, CancellationToken.None);
        Assert.NotNull(exact);
        Assert.Equal(literalCode, exact!.Code);
    }

    [PostgresFact]
    public async Task Asset_returns_linked_workflow_equipment_id()
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var w = await SeedAllSixTypesInMicroAsync();
        int unlinkedAssetId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");
            var unlinked = new EquipmentInventory
            {
                Code = $"UNL-{uid}",
                InstrumentType = "Pipette",
                ManufacturerName = "Eppendorf",
                Location = "Bench 2",
                Status = EquipmentOperationalStatus.InService,
                SectionId = micro.Id,
                CreatedByUserId = w.MicroUserId,
                LastModifiedByUserId = w.MicroUserId
            };
            arrangeDb.EquipmentInventories.Add(unlinked);
            await arrangeDb.SaveChangesAsync();
            unlinkedAssetId = unlinked.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        var ds = CreateDataSource(actDb);

        var linked = await ds.FindByIdAsync(LabelEntityType.EquipmentAsset, w.EquipmentInventoryId, w.MicroUserId, CancellationToken.None);
        Assert.NotNull(linked);
        Assert.Equal(w.LinkedEquipmentId, linked!.LinkedEquipmentId);

        var notLinked = await ds.FindByIdAsync(LabelEntityType.EquipmentAsset, unlinkedAssetId, w.MicroUserId, CancellationToken.None);
        Assert.NotNull(notLinked);
        Assert.Null(notLinked!.LinkedEquipmentId);
    }

    [PostgresFact]
    public async Task Usability_matches_the_workflow_gate_for_each_type()
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        int userId;
        int mediaId;
        int solutionId;
        int cryovialId;
        int materialId;
        int equipId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");
            var user = await CreateUserInSectionAsync(arrangeDb, "Gate User", $"gate_{uid}", micro.DepartmentId, micro.Id);
            userId = user.Id;

            var awaitingMat = new Material
            {
                SectionId = micro.Id,
                MaterialType = MaterialType.Chemical,
                MaterialName = $"Pending Mat {uid}",
                ManufacturerName = "ChemCorp",
                BatchNumber = $"PM-{uid}",
                ReceivingDate = FixedUtc,
                ExpiryDate = FixedUtc.AddDays(30),
                StockId = $"STK-PM-{uid}",
                Location = "Quarantine",
                QuantityReceived = 50m,
                QuantityRemaining = 50m,
                Unit = MaterialUnit.Gram,
                ApprovalStatus = ApprovalGateStatus.PendingReview,
                CreatedByUserId = userId,
                LastModifiedByUserId = userId
            };
            var org = new Organism { ScientificName = $"Gate Strain {uid}" };
            var solMaster = new SolutionMaster
            {
                SectionId = micro.Id,
                Name = $"Gate Sol {uid}",
                Type = SolutionType.Reagent,
                ShelfLifeValue = 5,
                ShelfLifeUnit = ShelfLifeUnit.Days,
                StorageCondition = "2-8 C",
                FinalVolumeMl = 250m,
                Instructions = "Mix",
                ReleaseRequired = true,
                IsActive = true,
                CreatedByUserId = userId,
                CreatedAt = FixedUtc,
                LastModifiedByUserId = userId,
                LastModifiedAt = FixedUtc
            };
            arrangeDb.Materials.Add(awaitingMat);
            arrangeDb.Organisms.Add(org);
            arrangeDb.SolutionMasters.Add(solMaster);
            await arrangeDb.SaveChangesAsync();

            // Media: released=true, status=Destroyed (blocked by workflow gate)
            var media = new Media
            {
                MaterialId = awaitingMat.Id,
                LotNumber = $"MD-DEST-{uid}",
                ManufacturerLot = "ML",
                ManufacturerName = "MediaCorp",
                TotalWeight = 1m,
                TotalVolume = "100 ml",
                AutoclaveProgram = "P1",
                LoadType = "liquid",
                Temperature = 121m,
                CycleTime = 15,
                CycleNumber = 1,
                Ph = 7.0m,
                PreparedAt = FixedUtc.AddDays(-5),
                ExpiryDate = FixedUtc.AddDays(30),
                PreparedByUserId = userId,
                Status = MediaStatus.Destroyed,
                ApprovalStatus = ApprovalGateStatus.Approved,
                IsReleasedForUse = true
            };
            arrangeDb.Media.Add(media);

            // Solution: Prepared, not expired, but ApprovalStatus = PendingReview (blocked by release gate)
            var sol = new SolutionPreparation
            {
                SectionId = micro.Id,
                Code = $"SP-PEND-{uid}",
                SolutionMasterId = solMaster.Id,
                Type = SolutionType.Reagent,
                RecipeSnapshotJson = JsonSerializer.Serialize(SolutionMasterResponse.From(solMaster), SnapshotJson.Options),
                Status = SolutionPreparationStatus.Prepared,
                StartedByUserId = userId,
                StartedAt = FixedUtc,
                PreparedByUserId = userId,
                PreparedAt = FixedUtc,
                ExpiresAt = FixedUtc.AddDays(5),
                ApprovalStatus = ApprovalGateStatus.PendingReview
            };
            arrangeDb.SolutionPreparations.Add(sol);

            // Cryovial: approved, vials remaining > 0, but IsDestroyed = true (blocked by CryovialThaw gate)
            var cry = new Cryovial
            {
                Code = $"CR-DEST-{uid}",
                MaterialId = awaitingMat.Id,
                OrganismId = org.Id,
                OrganismNameSnapshot = org.ScientificName,
                ManufacturerName = "Bank",
                PreparedAt = FixedUtc,
                ExpiryDate = FixedUtc.AddDays(30),
                NumberOfVialsPrepared = 5,
                VialsRemaining = 5,
                StorageCondition = "-70 C",
                PhysicalCheckConfirmed = true,
                PhysicalCheckText = "OK",
                PreparedByUserId = userId,
                ApprovalStatus = ApprovalGateStatus.Approved,
                IsDestroyed = true
            };
            arrangeDb.Cryovials.Add(cry);

            var eq = new EquipmentInventory
            {
                Code = $"EQ-OOS-{uid}",
                InstrumentType = "Balance",
                ManufacturerName = "Mettler",
                Location = "Lab 1",
                Status = EquipmentOperationalStatus.OutOfService,
                SectionId = micro.Id,
                CreatedByUserId = userId,
                LastModifiedByUserId = userId
            };
            arrangeDb.EquipmentInventories.Add(eq);
            await arrangeDb.SaveChangesAsync();

            mediaId = media.Id;
            solutionId = sol.Id;
            cryovialId = cry.Id;
            materialId = awaitingMat.Id;
            equipId = eq.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        var ds = CreateDataSource(actDb);

        var mediaRec = await ds.FindByIdAsync(LabelEntityType.MediaLot, mediaId, userId, CancellationToken.None);
        Assert.NotNull(mediaRec);
        Assert.False(mediaRec!.Usable);
        Assert.Equal("Destroyed.", mediaRec.BlockedReason);

        var solRec = await ds.FindByIdAsync(LabelEntityType.SolutionPreparation, solutionId, userId, CancellationToken.None);
        Assert.NotNull(solRec);
        Assert.False(solRec!.Usable);
        Assert.Equal("Awaiting approval.", solRec.BlockedReason);

        var cryRec = await ds.FindByIdAsync(LabelEntityType.Cryovial, cryovialId, userId, CancellationToken.None);
        Assert.NotNull(cryRec);
        Assert.False(cryRec!.Usable);
        Assert.Equal("Destroyed.", cryRec.BlockedReason);

        var matRec = await ds.FindByIdAsync(LabelEntityType.MaterialStockLot, materialId, userId, CancellationToken.None);
        Assert.NotNull(matRec);
        Assert.False(matRec!.Usable);
        Assert.Equal("Awaiting release.", matRec.BlockedReason);

        var eqRec = await ds.FindByIdAsync(LabelEntityType.EquipmentAsset, equipId, userId, CancellationToken.None);
        Assert.NotNull(eqRec);
        Assert.False(eqRec!.Usable);
        Assert.Equal("Out of service.", eqRec.BlockedReason);
    }

    [PostgresFact]
    public async Task Solution_label_uses_the_preparation_snapshot_storage_condition_not_the_current_master()
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        int userId;
        int prepWithSnapshotStorageId;
        int prepWithoutStorageId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");
            var user = await CreateUserInSectionAsync(arrangeDb, "Snap User", $"snp_{uid}", micro.DepartmentId, micro.Id);
            userId = user.Id;

            var master = new SolutionMaster
            {
                SectionId = micro.Id,
                Name = $"Snap Master {uid}",
                Type = SolutionType.Diluent,
                ShelfLifeValue = 5,
                ShelfLifeUnit = ShelfLifeUnit.Days,
                StorageCondition = "2-8 C",
                FinalVolumeMl = 500m,
                Instructions = "Mix",
                IsActive = true,
                CreatedByUserId = userId,
                CreatedAt = FixedUtc,
                LastModifiedByUserId = userId,
                LastModifiedAt = FixedUtc
            };
            arrangeDb.SolutionMasters.Add(master);
            await arrangeDb.SaveChangesAsync();

            var snapshotResponse = SolutionMasterResponse.From(master);
            var prepWithStorage = new SolutionPreparation
            {
                SectionId = micro.Id,
                Code = $"DL-SNAP-{uid}",
                SolutionMasterId = master.Id,
                Type = SolutionType.Diluent,
                RecipeSnapshotJson = JsonSerializer.Serialize(snapshotResponse, SnapshotJson.Options),
                Status = SolutionPreparationStatus.Prepared,
                StartedByUserId = userId,
                StartedAt = FixedUtc,
                PreparedByUserId = userId,
                PreparedAt = FixedUtc,
                ExpiresAt = FixedUtc.AddDays(5)
            };

            // Edit master AFTER preparation: label must still read "2-8 C" from the snapshot.
            master.StorageCondition = "15-25 C";

            // Preparation whose snapshot has no storage condition: field must be omitted (not read from master).
            var emptyStorageJson = JsonSerializer.Serialize(
                new SolutionMasterResponse
                {
                    Id = master.Id,
                    Name = master.Name,
                    Type = master.Type,
                    StorageCondition = ""
                },
                SnapshotJson.Options);
            var prepWithoutStorage = new SolutionPreparation
            {
                SectionId = micro.Id,
                Code = $"DL-NOSNAP-{uid}",
                SolutionMasterId = master.Id,
                Type = SolutionType.Diluent,
                RecipeSnapshotJson = emptyStorageJson,
                Status = SolutionPreparationStatus.Prepared,
                StartedByUserId = userId,
                StartedAt = FixedUtc,
                PreparedByUserId = userId,
                PreparedAt = FixedUtc,
                ExpiresAt = FixedUtc.AddDays(5)
            };

            arrangeDb.SolutionPreparations.AddRange(prepWithStorage, prepWithoutStorage);
            await arrangeDb.SaveChangesAsync();
            prepWithSnapshotStorageId = prepWithStorage.Id;
            prepWithoutStorageId = prepWithoutStorage.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        var ds = CreateDataSource(actDb);

        var recWithStorage = await ds.FindByIdAsync(LabelEntityType.SolutionPreparation, prepWithSnapshotStorageId, userId, CancellationToken.None);
        Assert.NotNull(recWithStorage);
        Assert.Contains(recWithStorage!.Content!.StandardFields, f => f.Name == "Storage" && f.Value == "2-8 C");
        Assert.DoesNotContain(recWithStorage.Content.StandardFields, f => f.Value == "15-25 C");

        var recWithoutStorage = await ds.FindByIdAsync(LabelEntityType.SolutionPreparation, prepWithoutStorageId, userId, CancellationToken.None);
        Assert.NotNull(recWithoutStorage);
        Assert.DoesNotContain(recWithoutStorage!.Content!.StandardFields, f => f.Name == "Storage");
    }

    [PostgresFact]
    public async Task Format_update_with_stale_version_is_rejected_and_nothing_changes()
    {
        LabelFormat original;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            original = await arrangeDb.LabelFormats.AsNoTracking()
                .SingleAsync(f => f.EntityType == LabelEntityType.Sample);
        }

        var staleVersion = original.Version + 1;

        await using (var actDb = _fixture.CreateDbContext())
        {
            var service = CreateFormatService(actDb);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.UpdateAsync(
                original.Id,
                new UpdateLabelFormatRequest(99m, 44m, LabelLayout.Small, 7),
                staleVersion,
                CancellationToken.None));
        }

        await using var assertDb = _fixture.CreateDbContext();
        var persisted = await assertDb.LabelFormats.AsNoTracking().SingleAsync(f => f.Id == original.Id);
        Assert.Equal(original.WidthMm, persisted.WidthMm);
        Assert.Equal(original.HeightMm, persisted.HeightMm);
        Assert.Equal(original.Layout, persisted.Layout);
        Assert.Equal(original.DefaultCopies, persisted.DefaultCopies);
        Assert.Equal(original.Version, persisted.Version);
    }

    [PostgresFact]
    public async Task Format_update_persists_and_writes_audit_row()
    {
        LabelFormat before;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            before = await arrangeDb.LabelFormats.AsNoTracking()
                .SingleAsync(f => f.EntityType == LabelEntityType.EquipmentAsset);
        }

        var updatedWidth = before.WidthMm == 60m ? 65m : 60m;
        var updatedHeight = before.HeightMm == 30m ? 35m : 30m;
        var updatedLayout = before.Layout == LabelLayout.Standard ? LabelLayout.Small : LabelLayout.Standard;
        var updatedCopies = before.DefaultCopies == 3 ? 4 : 3;

        await using (var actDb = _fixture.CreateDbContext())
        {
            actDb.CurrentUserId = _fixture.SeededUserId;
            await CreateFormatService(actDb).UpdateAsync(
                before.Id,
                new UpdateLabelFormatRequest(updatedWidth, updatedHeight, updatedLayout, updatedCopies),
                before.Version,
                CancellationToken.None);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var after = await assertDb.LabelFormats.AsNoTracking().SingleAsync(f => f.Id == before.Id);
        Assert.Equal(updatedWidth, after.WidthMm);
        Assert.Equal(updatedHeight, after.HeightMm);
        Assert.Equal(updatedLayout, after.Layout);
        Assert.Equal(updatedCopies, after.DefaultCopies);
        Assert.NotEqual(before.Version, after.Version);

        var expectedEntityId = before.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var audit = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(LabelFormat) && a.EntityId == expectedEntityId && a.Action == "Update")
            .OrderByDescending(a => a.Id)
            .FirstOrDefaultAsync();
        Assert.NotNull(audit);
        Assert.Equal(_fixture.SeededUserId, audit!.UserId);
        Assert.Contains("WidthMm", audit.PreviousValue);
        Assert.Contains("WidthMm", audit.NewValue);
    }

    // [PostgresFact] looping the cases, not a multi-row [PostgresTheory]: skipped without a database a
    // theory counts as one test, and CI's verify_test_results.py then sees different totals for its two runs.
    [PostgresFact]
    public async Task Format_update_out_of_range_is_rejected_and_nothing_changes()
    {
        var cases = new (decimal WidthMm, decimal HeightMm, LabelLayout Layout, int DefaultCopies)[]
        {
            (9.99m, 25m, LabelLayout.Standard, 1),
            (150.01m, 25m, LabelLayout.Standard, 1),
            (50m, 9.99m, LabelLayout.Standard, 1),
            (50m, 150.01m, LabelLayout.Standard, 1),
            (50m, 25m, LabelLayout.Standard, 0),
            (50m, 25m, LabelLayout.Standard, 51),
            (50m, 25m, (LabelLayout)999, 1)
        };

        await PostgresCases.RunAsync(cases, async c =>
        {
            LabelFormat before;
            await using (var arrangeDb = _fixture.CreateDbContext())
            {
                before = await arrangeDb.LabelFormats.AsNoTracking()
                    .SingleAsync(f => f.EntityType == LabelEntityType.Cryovial);
            }

            await using (var actDb = _fixture.CreateDbContext())
            {
                var service = CreateFormatService(actDb);
                await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(
                    before.Id,
                    new UpdateLabelFormatRequest(c.WidthMm, c.HeightMm, c.Layout, c.DefaultCopies),
                    before.Version,
                    CancellationToken.None));
            }

            await using var assertDb = _fixture.CreateDbContext();
            var after = await assertDb.LabelFormats.AsNoTracking().SingleAsync(f => f.Id == before.Id);
            Assert.Equal(before.WidthMm, after.WidthMm);
            Assert.Equal(before.HeightMm, after.HeightMm);
            Assert.Equal(before.Layout, after.Layout);
            Assert.Equal(before.DefaultCopies, after.DefaultCopies);
            Assert.Equal(before.Version, after.Version);
        });
    }
}
