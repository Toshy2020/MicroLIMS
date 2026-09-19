using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class CalibrationCurveRunPostgresIntegrationTests
{
    private static readonly string YearStr = DateTime.UtcNow.ToString("yyyy", CultureInfo.InvariantCulture);
    private static readonly string Mm = DateTime.UtcNow.ToString("MM", CultureInfo.InvariantCulture);
    private readonly PostgresTestFixture _fixture;

    public CalibrationCurveRunPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    public async Task CreateCalibrationRun_CodeTakenAtTheSameMoment_TakesNextNumberAndSucceeds()
    {
        var methodAbbr = NewAbbr();
        var (test, equip, standard, analyte) = await SeedPrerequisitesAsync(methodAbbr);

        await using var db = _fixture.CreateDbContext(new TakeRunCodeBeforeSave(
            _fixture, test.Id, test.SectionId, equip.Id, standard.Id, methodAbbr, times: 1));

        var service = TestServiceFactory.CalibrationRun(db);
        var request = new CreateCalibrationRunRequest(
            TestDefinitionId: test.Id,
            EquipmentId: equip.Id,
            CalibrationStandardMaterialId: standard.Id,
            IcvStandardMaterialId: null,
            Password: "IntegrationPassword123!",
            Comment: "Postgres retry test",
            Analytes: new List<CreateCalibrationRunAnalyteRequest>
            {
                new(
                    TestAnalyteId: analyte.Id,
                    CorrelationValue: 0.9995m,
                    CorrelationType: CorrelationType.RSquared,
                    NumberOfStandards: 5,
                    LowestStandardMgPerL: 0.01m,
                    HighestStandardMgPerL: 10.0m,
                    Checks: new List<CreateCalibrationRunCheckRequest>
                    {
                        new(CalibrationCheckType.Blank, 1, null, 0.001m),
                        new(CalibrationCheckType.Icv, 2, 1.0m, 1.0m)
                    }
                )
            });

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 Dummy ICP Report"));
        var run = await service.CreateAsync(request, stream, "report.pdf", "application/pdf", _fixture.SeededUserId, "127.0.0.1");

        // The first code (01) was taken by the concurrent insert; this run retried and took 02
        Assert.Equal($"{methodAbbr} CAL 02/{Mm}{YearStr}", run.Code);

        await using var check = _fixture.CreateDbContext();
        var codes = await check.CalibrationRuns
            .Where(r => r.TestDefinitionId == test.Id)
            .OrderBy(r => r.Code)
            .Select(r => r.Code)
            .ToListAsync();

        Assert.Equal(new[] { $"{methodAbbr} CAL 01/{Mm}{YearStr}", $"{methodAbbr} CAL 02/{Mm}{YearStr}" }, codes);
    }

    private static string NewAbbr() => "C" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();

    private async Task<(TestDefinition test, Equipment equip, Material standard, TestAnalyte analyte)> SeedPrerequisitesAsync(string methodAbbr)
    {
        await using var db = _fixture.CreateDbContext();

        var section = await db.DocumentSections.FirstOrDefaultAsync(s => s.Code == "FP")
            ?? await db.DocumentSections.FirstAsync(s => s.Code == "MICRO");

        var user = await db.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationPassword123!");
        user.IsActive = true;

        var test = new TestDefinition
        {
            Code = $"{methodAbbr}_TEST",
            DisplayName = $"{methodAbbr} Integration Test",
            SectionId = section.Id,
            WorkflowType = WorkflowType.ElementalAssay,
            EquationType = EquationType.CalibrationCurve,
            MethodAbbreviation = methodAbbr,
            CalibrationEntryMode = CalibrationEntryMode.InstrumentReported,
            CalMinCorrelation = 0.999m,
            CalCorrelationType = CorrelationType.RSquared,
            CalMinStandards = 3,
            CalCheckRecoveryLowPercent = 90.0m,
            CalCheckRecoveryHighPercent = 110.0m,
            CalBlankMax = 0.005m,
            CalRequireBlank = true,
            CalRequireIcv = true,
            CalRequireCcv = false,
            CalRequireInternalStandard = false,
            ReportedConcentrationBasis = ReportedConcentrationBasis.SamplePpm,
            CalMaxRunAgeHours = 24
        };
        db.TestDefinitions.Add(test);
        await db.SaveChangesAsync();


        var analyte = new TestAnalyte
        {
            TestDefinitionId = test.Id,
            Element = "Zn",
            WavelengthNm = 213.856m,
            View = AnalyteView.Axial,
            LoqMgPerL = 0.005m,
            DisplayOrder = 1,
            IsActive = true
        };
        db.TestAnalytes.Add(analyte);

        var equip = new Equipment
        {
            Name = $"ICP {methodAbbr}",
            Code = $"EQ-{methodAbbr}",
            Type = EquipmentType.IcpOes,
            SectionId = section.Id,
            CdsSoftware = CdsSoftware.PerkinElmerSyngistix
        };
        db.Equipment.Add(equip);

        var standard = new Material
        {
            MaterialName = $"Standard {methodAbbr}",
            MaterialType = MaterialType.ReferenceStandard,
            ManufacturerName = "PerkinElmer",
            BatchNumber = $"LOT-{methodAbbr}",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Milliliter,
            Location = "Standards Storage",
            SectionId = section.Id,
            Purity = 99.5m,
            CreatedByUserId = _fixture.SeededUserId,
            LastModifiedByUserId = _fixture.SeededUserId
        };
        db.Materials.Add(standard);

        await db.SaveChangesAsync();
        return (test, equip, standard, analyte);
    }

    private sealed class TakeRunCodeBeforeSave : SaveChangesInterceptor
    {
        private readonly PostgresTestFixture _fixture;
        private readonly int _testId;
        private readonly int _sectionId;
        private readonly int _equipId;
        private readonly int _matId;
        private readonly string _abbr;
        private int _remaining;

        public TakeRunCodeBeforeSave(
            PostgresTestFixture fixture,
            int testId, int sectionId, int equipId, int matId, string abbr,
            int times)
        {
            _fixture = fixture;
            _testId = testId;
            _sectionId = sectionId;
            _equipId = equipId;
            _matId = matId;
            _abbr = abbr;
            _remaining = times;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var pending = eventData.Context!.ChangeTracker.Entries<CalibrationRun>()
                .FirstOrDefault(e => e.State == EntityState.Added);

            if (pending is not null && _remaining > 0)
            {
                _remaining--;
                await using var concurrentDb = _fixture.CreateDbContext();

                var sig = new ElectronicSignature
                {
                    UserId = _fixture.SeededUserId,
                    UserFullNameSnapshot = "Concurrent User",
                    UsernameSnapshot = "concurrent",
                    RoleSnapshot = "Analyst",
                    MeaningOfSignature = SignatureMeaning.CalibrationRunPerformed,
                    EntityType = "TestDefinition",
                    EntityId = _testId
                };
                concurrentDb.ElectronicSignatures.Add(sig);

                var concurrentRun = new CalibrationRun
                {
                    Code = pending.Entity.Code,
                    TestDefinitionId = _testId,
                    SectionId = _sectionId,
                    EquipmentId = _equipId,
                    CalibrationStandardMaterialId = _matId,
                    PerformedByUserId = _fixture.SeededUserId,
                    PerformedAt = DateTime.UtcNow,
                    Signature = sig,
                    AnalytesPassed = 1,
                    AnalytesTotal = 1,
                    Passed = true,
                    Document = new CalibrationRunDocument
                    {
                        StorageKey = "concurrent/doc.pdf",
                        OriginalFileName = "concurrent.pdf",
                        ContentType = "application/pdf",
                        SizeBytes = 100,
                        ContentSha256 = "DUMMYHASH",
                        UploadedByUserId = _fixture.SeededUserId,
                        UploadedAt = DateTime.UtcNow
                    }
                };
                concurrentDb.CalibrationRuns.Add(concurrentRun);
                await concurrentDb.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }
}
