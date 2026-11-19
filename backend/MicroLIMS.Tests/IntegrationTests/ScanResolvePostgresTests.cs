using System.Security.Claims;
using System.Text;
using System.Text.Json;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Labels;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class ScanResolvePostgresTests
{
    private const string Password = "Correct-Horse-9";
    private static readonly DateTime FixedUtc = new(2026, 11, 12, 22, 30, 0, DateTimeKind.Utc);
    private static readonly TimeZoneInfo CairoZone = LabClock.ResolveTimeZone("Africa/Cairo");

    private static readonly IReadOnlyDictionary<char, char> LatinToArabic101 = new Dictionary<char, char>
    {
        ['q'] = '\u0636',
        ['w'] = '\u0635',
        ['e'] = '\u062B',
        ['r'] = '\u0642',
        ['t'] = '\u0641',
        ['y'] = '\u063A',
        ['u'] = '\u0639',
        ['i'] = '\u0647',
        ['o'] = '\u062E',
        ['p'] = '\u062D',
        ['a'] = '\u0634',
        ['s'] = '\u0633',
        ['d'] = '\u064A',
        ['f'] = '\u0628',
        ['g'] = '\u0644',
        ['h'] = '\u0627',
        ['j'] = '\u062A',
        ['k'] = '\u0646',
        ['l'] = '\u0645',
        ['z'] = '\u0626',
        ['x'] = '\u0621',
        ['c'] = '\u0624',
        ['v'] = '\u0631',
        ['n'] = '\u0649',
        ['m'] = '\u0629',
        ['0'] = '\u0660',
        ['1'] = '\u0661',
        ['2'] = '\u0662',
        ['3'] = '\u0663',
        ['4'] = '\u0664',
        ['5'] = '\u0665',
        ['6'] = '\u0666',
        ['7'] = '\u0667',
        ['8'] = '\u0668',
        ['9'] = '\u0669'
    };

    private readonly PostgresTestFixture _fixture;

    public ScanResolvePostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static ILabClock FixedClock(DateTime utcNow) =>
        new LabClock(new FixedTimeProvider(new DateTimeOffset(utcNow, TimeSpan.Zero)), CairoZone);

    private static IScanResolveService CreateService(MicroLimsDbContext db, DateTime? utcNow = null)
    {
        var dataSource = new LabelDataSource(db, new UserSectionScopeService(db), FixedClock(utcNow ?? FixedUtc));
        return new ScanResolveService(dataSource);
    }

    private static ScanController CreateController(IScanResolveService service, int userId)
    {
        var controller = new ScanController(service);
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

    private static string ToArabic101(string latin)
    {
        var sb = new StringBuilder(latin.Length);
        foreach (var ch in latin.ToLowerInvariant())
        {
            if (ch == 'b')
            {
                sb.Append("\u0644\u0627");
            }
            else if (LatinToArabic101.TryGetValue(ch, out var arabic))
            {
                sb.Append(arabic);
            }
            else
            {
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }

    private static async Task<User> CreateUserInSectionAsync(
        MicroLimsDbContext db,
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
            FullName = $"User {username}",
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

    private sealed record SeededWorld(
        int MicroUserId,
        int FpUserId,
        int MicroSectionId,
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

    private static async Task<SeededWorld> SeedAllSixTypesInMicroAsync(MicroLimsDbContext db)
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var micro = await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");
        var fp = await db.DocumentSections.FirstAsync(s => s.Code == "FP");

        var microUser = await CreateUserInSectionAsync(db, $"sr_micro_{uid}", micro.DepartmentId, micro.Id);
        var fpUser = await CreateUserInSectionAsync(db, $"sr_fp_{uid}", fp.DepartmentId, fp.Id);

        // 1. Sample
        var testCode = $"TD-SR-{uid}";
        db.TestDefinitions.Add(new TestDefinition { Code = testCode, DisplayName = $"Test {uid}", SectionId = micro.Id, IsActive = true });
        var item = new Item { Code = $"ITM-SR-{uid}", Name = $"Product {uid}", Category = SampleCategory.FinishedProduct, IsActive = true };
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

        // 5. MaterialStockLot (+ source materials for Media & Cryovial)
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
            StockId = $"STK-M-SR-{uid}",
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
            StockId = $"STK-C-SR-{uid}",
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
            Code = $"WFEQ-SR-{uid}",
            Type = EquipmentType.Incubator,
            SectionId = micro.Id,
            EquipmentInventoryId = asset.Id
        };
        db.Equipment.Add(workflowEquip);
        await db.SaveChangesAsync();

        return new SeededWorld(
            microUser.Id,
            fpUser.Id,
            micro.Id,
            sample.Id,
            sample.ReferenceNumber,
            media.Id,
            media.LotNumber,
            solution.Id,
            solution.Code!,
            cryovial.Id,
            cryovial.Code,
            stockMaterial.Id,
            stockMaterial.StockId!,
            asset.Id,
            asset.Code,
            workflowEquip.Id);
    }

    [PostgresFact]
    public async Task Prefixed_payload_resolves_each_of_the_six_types()
    {
        SeededWorld w;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            w = await SeedAllSixTypesInMicroAsync(arrangeDb);
        }

        await using var actDb = _fixture.CreateDbContext();
        var service = CreateService(actDb);

        var cases = new (LabelEntityType Type, int Id, string Code, string ExpectedTarget, int? ExpectedLinkedEquipId)[]
        {
            (LabelEntityType.Sample, w.SampleId, w.SampleCode, $"/receiving-testing?sampleId={w.SampleId}", null),
            (LabelEntityType.MediaLot, w.MediaId, w.MediaCode, "/laboratory-configuration/media", null),
            (LabelEntityType.SolutionPreparation, w.SolutionId, w.SolutionCode, $"/preparation/{w.SolutionId}", null),
            (LabelEntityType.Cryovial, w.CryovialId, w.CryovialCode, $"/cryovials/{w.CryovialId}/report", null),
            (LabelEntityType.MaterialStockLot, w.MaterialId, w.MaterialStockId, "/inventory/materials", null),
            (LabelEntityType.EquipmentAsset, w.EquipmentInventoryId, w.EquipmentCode, "/inventory/equipment", w.LinkedEquipmentId)
        };

        foreach (var c in cases)
        {
            var payload = LabelPayload.Build(c.Type, c.Code);
            var response = await service.ResolveAsync(payload, w.MicroUserId, CancellationToken.None);

            Assert.Equal(ScanOutcome.Found, response.Outcome);
            Assert.False(response.LayoutCorrected);
            Assert.Equal(payload, response.NormalizedValue);
            Assert.NotNull(response.Match);
            Assert.Empty(response.Candidates);
            Assert.Null(response.Hint);

            Assert.Equal(c.Type, response.Match!.EntityType);
            Assert.Equal(c.Id, response.Match.Id);
            Assert.Equal(c.Code, response.Match.Code);
            Assert.True(response.Match.Usable);
            Assert.Null(response.Match.BlockedReason);
            Assert.Equal(c.ExpectedTarget, response.Match.NavigationTarget);
            Assert.Equal(c.ExpectedLinkedEquipId, response.Match.LinkedEquipmentId);
        }
    }

    [PostgresFact]
    public async Task Bare_code_with_one_match_resolves()
    {
        SeededWorld w;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            w = await SeedAllSixTypesInMicroAsync(arrangeDb);
        }

        await using var actDb = _fixture.CreateDbContext();
        var service = CreateService(actDb);

        var response = await service.ResolveAsync(w.SampleCode.ToLowerInvariant(), w.MicroUserId, CancellationToken.None);

        Assert.Equal(ScanOutcome.Found, response.Outcome);
        Assert.False(response.LayoutCorrected);
        Assert.NotNull(response.Match);
        Assert.Equal(LabelEntityType.Sample, response.Match!.EntityType);
        Assert.Equal(w.SampleId, response.Match.Id);
        Assert.Equal(w.SampleCode, response.Match.Code);
        Assert.Empty(response.Candidates);
        Assert.Null(response.Hint);
    }

    [PostgresFact]
    public async Task Bare_code_matching_two_types_is_ambiguous_with_both_candidates()
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var sharedCode = $"SHARED-{uid}";
        int microUserId;
        int sampleId;
        int materialId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");
            var microUser = await CreateUserInSectionAsync(arrangeDb, $"sr_amb_{uid}", micro.DepartmentId, micro.Id);
            microUserId = microUser.Id;

            var testCode = $"TD-AMB-{uid}";
            arrangeDb.TestDefinitions.Add(new TestDefinition { Code = testCode, DisplayName = $"Test {uid}", SectionId = micro.Id, IsActive = true });
            var item = new Item { Code = $"ITM-AMB-{uid}", Name = $"Product {uid}", Category = SampleCategory.FinishedProduct, IsActive = true };
            item.AssignedTests.Add(new SampleTest { TestCode = testCode, DisplayName = $"Test {uid}" });
            arrangeDb.Items.Add(item);
            await arrangeDb.SaveChangesAsync();

            var cause = await arrangeDb.CausesOfTesting.FirstAsync();
            var sample = new Sample
            {
                ReferenceNumber = sharedCode,
                Category = SampleCategory.FinishedProduct,
                ItemId = item.Id,
                CauseOfTestingId = cause.Id,
                SampledBy = "Sampler",
                BatchNumber = $"BAT-AMB-{uid}",
                ControlNumber = $"CTRL-AMB-{uid}",
                ReceivedByUserId = microUser.Id,
                ReceivedAt = FixedUtc,
                LabSectionId = micro.Id,
                Status = SampleStatus.Received
            };
            arrangeDb.Samples.Add(sample);

            var material = new Material
            {
                SectionId = micro.Id,
                MaterialType = MaterialType.Chemical,
                MaterialName = $"Reagent {uid}",
                ManufacturerName = "ChemCorp",
                BatchNumber = $"B-AMB-{uid}",
                ReceivingDate = FixedUtc,
                ExpiryDate = FixedUtc.AddDays(60),
                StockId = sharedCode,
                Location = "Shelf B",
                QuantityReceived = 100m,
                QuantityRemaining = 100m,
                Unit = MaterialUnit.Gram,
                CreatedByUserId = microUser.Id,
                LastModifiedByUserId = microUser.Id
            };
            arrangeDb.Materials.Add(material);
            await arrangeDb.SaveChangesAsync();

            sampleId = sample.Id;
            materialId = material.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        var service = CreateService(actDb);

        var response = await service.ResolveAsync(sharedCode, microUserId, CancellationToken.None);

        Assert.Equal(ScanOutcome.Ambiguous, response.Outcome);
        Assert.False(response.LayoutCorrected);
        Assert.Equal(sharedCode, response.NormalizedValue);
        Assert.Null(response.Match);
        Assert.Null(response.Hint);
        Assert.Equal(2, response.Candidates.Count);
        Assert.Contains(response.Candidates, c =>
            c.EntityType == LabelEntityType.Sample
            && c.Id == sampleId
            && c.Code == sharedCode
            && c.NavigationTarget == $"/receiving-testing?sampleId={sampleId}");
        Assert.Contains(response.Candidates, c =>
            c.EntityType == LabelEntityType.MaterialStockLot
            && c.Id == materialId
            && c.Code == sharedCode
            && c.NavigationTarget == "/inventory/materials");
    }

    [PostgresFact]
    public async Task Record_in_another_section_is_not_found()
    {
        SeededWorld w;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            w = await SeedAllSixTypesInMicroAsync(arrangeDb);
        }

        await using var actDb = _fixture.CreateDbContext();
        var service = CreateService(actDb);

        var probeCode = $"S:{w.SampleCode}";
        var inOtherSection = await service.ResolveAsync(probeCode, w.FpUserId, CancellationToken.None);
        var unknown = await service.ResolveAsync($"S:UNKNOWN-{Guid.NewGuid():N}", w.FpUserId, CancellationToken.None);

        Assert.Equal(ScanOutcome.NotFound, inOtherSection.Outcome);
        Assert.Equal(unknown.Outcome, inOtherSection.Outcome);
        Assert.Equal(unknown.LayoutCorrected, inOtherSection.LayoutCorrected);
        Assert.Equal(unknown.Match, inOtherSection.Match);
        Assert.Equal(unknown.Candidates, inOtherSection.Candidates);
        Assert.Equal(unknown.Hint, inOtherSection.Hint);
        Assert.Equal(probeCode, inOtherSection.NormalizedValue);

        // Also check bare lookup in another section is indistinguishable from an unknown bare code
        var bareOtherSection = await service.ResolveAsync(w.MediaCode, w.FpUserId, CancellationToken.None);
        var bareUnknown = await service.ResolveAsync($"UNKNOWN-{Guid.NewGuid():N}", w.FpUserId, CancellationToken.None);

        Assert.Equal(bareUnknown.Outcome, bareOtherSection.Outcome);
        Assert.Equal(bareUnknown.LayoutCorrected, bareOtherSection.LayoutCorrected);
        Assert.Equal(bareUnknown.Match, bareOtherSection.Match);
        Assert.Equal(bareUnknown.Candidates, bareOtherSection.Candidates);
        Assert.Equal(bareUnknown.Hint, bareOtherSection.Hint);
    }

    [PostgresFact]
    public async Task Partial_code_is_not_found()
    {
        SeededWorld w;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            w = await SeedAllSixTypesInMicroAsync(arrangeDb);
        }

        await using var actDb = _fixture.CreateDbContext();
        var service = CreateService(actDb);

        var partial = w.SampleCode[..^2];
        var response = await service.ResolveAsync(partial, w.MicroUserId, CancellationToken.None);

        Assert.Equal(ScanOutcome.NotFound, response.Outcome);
        Assert.False(response.LayoutCorrected);
        Assert.Equal(partial, response.NormalizedValue);
        Assert.Null(response.Match);
        Assert.Empty(response.Candidates);
        Assert.Null(response.Hint);
    }

    [PostgresFact]
    public async Task Arabic_layout_input_resolves_and_sets_layout_corrected()
    {
        SeededWorld w;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            w = await SeedAllSixTypesInMicroAsync(arrangeDb);
        }

        await using var actDb = _fixture.CreateDbContext();
        var service = CreateService(actDb);

        var expectedPayload = LabelPayload.Build(LabelEntityType.Sample, w.SampleCode);
        var arabicInput = ToArabic101(expectedPayload);

        var response = await service.ResolveAsync(arabicInput, w.MicroUserId, CancellationToken.None);

        Assert.Equal(ScanOutcome.Found, response.Outcome);
        Assert.True(response.LayoutCorrected);
        Assert.Equal(expectedPayload.ToLowerInvariant(), response.NormalizedValue);
        Assert.NotNull(response.Match);
        Assert.Equal(LabelEntityType.Sample, response.Match!.EntityType);
        Assert.Equal(w.SampleId, response.Match.Id);
        Assert.Equal(w.SampleCode, response.Match.Code);
        Assert.Empty(response.Candidates);
        Assert.Null(response.Hint);
    }

    [PostgresFact]
    public async Task Unknown_arabic_layout_input_returns_hint()
    {
        SeededWorld w;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            w = await SeedAllSixTypesInMicroAsync(arrangeDb);
        }

        await using var actDb = _fixture.CreateDbContext();
        var service = CreateService(actDb);

        var arabicUnknown = ToArabic101($"S:NOPE-{Guid.NewGuid():N}"[..16]);
        var response = await service.ResolveAsync(arabicUnknown, w.MicroUserId, CancellationToken.None);

        Assert.Equal(ScanOutcome.NotFound, response.Outcome);
        Assert.True(response.LayoutCorrected);
        Assert.Null(response.Match);
        Assert.Empty(response.Candidates);
        Assert.Equal("The keyboard was in Arabic. Switch to English and scan again.", response.Hint);
    }

    [PostgresFact]
    public async Task Expired_media_lot_is_found_but_not_usable_with_reason()
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        int microUserId;
        int mediaId;
        var lotNumber = $"MED-EXP-{uid}";

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");
            var microUser = await CreateUserInSectionAsync(arrangeDb, $"sr_exp_{uid}", micro.DepartmentId, micro.Id);
            microUserId = microUser.Id;

            var mediaSource = new Material
            {
                SectionId = micro.Id,
                MaterialType = MaterialType.DehydratedMedia,
                MaterialName = $"Agar Expired {uid}",
                ManufacturerName = "MediaCorp",
                BatchNumber = $"MB-EXP-{uid}",
                ReceivingDate = FixedUtc.AddDays(-60),
                ExpiryDate = FixedUtc.AddYears(1),
                StockId = $"STK-EXP-{uid}",
                Location = "Media Room",
                QuantityReceived = 500m,
                QuantityRemaining = 400m,
                Unit = MaterialUnit.Gram,
                CreatedByUserId = microUser.Id,
                LastModifiedByUserId = microUser.Id
            };
            arrangeDb.Materials.Add(mediaSource);
            await arrangeDb.SaveChangesAsync();

            var media = new Media
            {
                MaterialId = mediaSource.Id,
                LotNumber = lotNumber,
                ManufacturerLot = $"ML-EXP-{uid}",
                ManufacturerName = "MediaCorp",
                TotalWeight = 10m,
                TotalVolume = "500 ml",
                AutoclaveProgram = "P1",
                LoadType = "liquid",
                Temperature = 121m,
                CycleTime = 15,
                CycleNumber = 1,
                Ph = 7.2m,
                PreparedAt = FixedUtc.AddDays(-40),
                ExpiryDate = FixedUtc.AddDays(-1),
                PreparedByUserId = microUser.Id,
                Status = MediaStatus.Active,
                ApprovalStatus = ApprovalGateStatus.Approved,
                IsReleasedForUse = true
            };
            arrangeDb.Media.Add(media);
            await arrangeDb.SaveChangesAsync();
            mediaId = media.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        var service = CreateService(actDb);

        var response = await service.ResolveAsync(LabelPayload.Build(LabelEntityType.MediaLot, lotNumber), microUserId, CancellationToken.None);

        Assert.Equal(ScanOutcome.Found, response.Outcome);
        Assert.NotNull(response.Match);
        Assert.Equal(mediaId, response.Match!.Id);
        Assert.False(response.Match.Usable);
        Assert.Equal("Expired.", response.Match.BlockedReason);
    }

    [PostgresFact]
    public async Task Asset_scan_returns_linked_equipment_id()
    {
        SeededWorld w;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            w = await SeedAllSixTypesInMicroAsync(arrangeDb);
        }

        await using var actDb = _fixture.CreateDbContext();
        var service = CreateService(actDb);

        var response = await service.ResolveAsync(LabelPayload.Build(LabelEntityType.EquipmentAsset, w.EquipmentCode), w.MicroUserId, CancellationToken.None);

        Assert.Equal(ScanOutcome.Found, response.Outcome);
        Assert.NotNull(response.Match);
        Assert.Equal(LabelEntityType.EquipmentAsset, response.Match!.EntityType);
        Assert.Equal(w.EquipmentInventoryId, response.Match.Id);
        Assert.Equal(w.LinkedEquipmentId, response.Match.LinkedEquipmentId);
    }

    [PostgresFact]
    public async Task Blank_value_is_bad_request()
    {
        int microUserId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");
            var user = await CreateUserInSectionAsync(arrangeDb, $"sr_blank_{Guid.NewGuid():N}"[..16], micro.DepartmentId, micro.Id);
            microUserId = user.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        var service = CreateService(actDb);
        var controller = CreateController(service, microUserId);

        foreach (var blank in new string?[] { null, "", "   \r\n\t" })
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.ResolveAsync(blank, microUserId, CancellationToken.None));

            var actionResult = await controller.Resolve(blank, CancellationToken.None);
            var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
            var payload = Assert.IsType<ApiResponse<object>>(badRequest.Value);
            Assert.False(payload.Success);
            Assert.False(string.IsNullOrWhiteSpace(payload.Message));
        }
    }

    [PostgresFact]
    public async Task Value_over_300_characters_is_bad_request()
    {
        int microUserId;
        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");
            var user = await CreateUserInSectionAsync(arrangeDb, $"sr_len_{Guid.NewGuid():N}"[..16], micro.DepartmentId, micro.Id);
            microUserId = user.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        var service = CreateService(actDb);
        var controller = CreateController(service, microUserId);

        var overLimit = new string('A', 301);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ResolveAsync(overLimit, microUserId, CancellationToken.None));

        var badAction = await controller.Resolve(overLimit, CancellationToken.None);
        var badRequest = Assert.IsType<BadRequestObjectResult>(badAction);
        var badPayload = Assert.IsType<ApiResponse<object>>(badRequest.Value);
        Assert.False(badPayload.Success);

        // Exactly 300 characters is accepted (200 with NotFound outcome)
        var atLimit = new string('A', 300);
        var okAction = await controller.Resolve(atLimit, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(okAction);
        var okPayload = Assert.IsType<ApiResponse<ScanResolveResponse>>(okResult.Value);
        Assert.True(okPayload.Success);
        Assert.Equal(ScanOutcome.NotFound, okPayload.Data!.Outcome);
    }

    [PostgresFact]
    public async Task Prefixed_payload_for_a_code_containing_a_colon_resolves()
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var codeWithColon = $"MP-A:B {uid}";
        int microUserId;
        int solutionId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");
            var user = await CreateUserInSectionAsync(arrangeDb, $"sr_colon_{uid}", micro.DepartmentId, micro.Id);
            microUserId = user.Id;

            var master = new SolutionMaster
            {
                SectionId = micro.Id,
                Name = $"Sol Colon {uid}",
                Type = SolutionType.Diluent,
                ShelfLifeValue = 7,
                ShelfLifeUnit = ShelfLifeUnit.Days,
                FinalVolumeMl = 100m,
                Instructions = "Mix",
                IsActive = true,
                CreatedByUserId = user.Id,
                CreatedAt = FixedUtc,
                LastModifiedByUserId = user.Id,
                LastModifiedAt = FixedUtc
            };
            arrangeDb.SolutionMasters.Add(master);
            await arrangeDb.SaveChangesAsync();

            var snapshotJson = JsonSerializer.Serialize(SolutionMasterResponse.From(master), SnapshotJson.Options);
            var solution = new SolutionPreparation
            {
                SectionId = micro.Id,
                Code = codeWithColon,
                SolutionMasterId = master.Id,
                Type = SolutionType.Diluent,
                RecipeSnapshotJson = snapshotJson,
                Status = SolutionPreparationStatus.Prepared,
                StartedByUserId = user.Id,
                StartedAt = FixedUtc,
                PreparedByUserId = user.Id,
                PreparedAt = FixedUtc,
                ExpiresAt = FixedUtc.AddDays(7),
                FinalVolumeMl = 100m
            };
            arrangeDb.SolutionPreparations.Add(solution);
            await arrangeDb.SaveChangesAsync();
            solutionId = solution.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        var service = CreateService(actDb);

        var response = await service.ResolveAsync($"SP:{codeWithColon}", microUserId, CancellationToken.None);

        Assert.Equal(ScanOutcome.Found, response.Outcome);
        Assert.NotNull(response.Match);
        Assert.Equal(LabelEntityType.SolutionPreparation, response.Match!.EntityType);
        Assert.Equal(solutionId, response.Match.Id);
        Assert.Equal(codeWithColon, response.Match.Code);
    }

    [PostgresFact]
    public async Task Bare_code_that_looks_like_an_unknown_prefix_is_treated_as_a_bare_code()
    {
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var unknownPrefixCode = $"XX:{uid}";
        int microUserId;
        int materialId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var micro = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "MICRO");
            var user = await CreateUserInSectionAsync(arrangeDb, $"sr_unkp_{uid}", micro.DepartmentId, micro.Id);
            microUserId = user.Id;

            var material = new Material
            {
                SectionId = micro.Id,
                MaterialType = MaterialType.Chemical,
                MaterialName = $"Salt {uid}",
                ManufacturerName = "ChemCorp",
                BatchNumber = $"B-{uid}",
                ReceivingDate = FixedUtc,
                ExpiryDate = FixedUtc.AddDays(60),
                StockId = unknownPrefixCode,
                Location = "Shelf C",
                QuantityReceived = 100m,
                QuantityRemaining = 100m,
                Unit = MaterialUnit.Gram,
                CreatedByUserId = user.Id,
                LastModifiedByUserId = user.Id
            };
            arrangeDb.Materials.Add(material);
            await arrangeDb.SaveChangesAsync();
            materialId = material.Id;
        }

        await using var actDb = _fixture.CreateDbContext();
        var service = CreateService(actDb);

        var response = await service.ResolveAsync(unknownPrefixCode, microUserId, CancellationToken.None);

        Assert.Equal(ScanOutcome.Found, response.Outcome);
        Assert.NotNull(response.Match);
        Assert.Equal(LabelEntityType.MaterialStockLot, response.Match!.EntityType);
        Assert.Equal(materialId, response.Match.Id);
        Assert.Equal(unknownPrefixCode, response.Match.Code);
    }
}
