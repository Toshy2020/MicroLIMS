using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Services.MasterData;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class SpecificationRetirementPostgresTests
{
    private static readonly DateTimeOffset FixedUtc = new(2031, 6, 15, 10, 20, 30, TimeSpan.Zero);
    private const string ValidReason = "Retired under change control CC-2026-09";
    private const string ReinstateReason = "Reinstated per QA review QA-2026-11";

    private readonly PostgresTestFixture _fixture;

    public SpecificationRetirementPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static string Tag() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static SpecificationMasterDataService BuildSpecService(MicroLimsDbContext db, TimeProvider? clock = null) =>
        new(db, new UserSectionScopeService(db), timeProvider: clock ?? new FixedTimeProvider(FixedUtc));

    private sealed record SeededSpecScenario(
        int SectionId,
        int ItemId,
        int SpecId,
        string TestCode,
        int CauseId,
        string StageName,
        int MediaId,
        int IncubatorId);

    private async Task<SeededSpecScenario> SeedCountItemWithSpecAsync(bool isCountTest = false, bool seedSampleWithResult = false)
    {
        var tag = Tag();
        var testCode = (isCountTest ? "TAMC_" : "PHYS_") + tag;
        await using var arrangeDb = _fixture.CreateDbContext();

        var section = TestServiceFactory.EnsureMicroSection(arrangeDb);
        var stage = TestServiceFactory.EnsureProductionStage(arrangeDb, "F.P");
        var td = new TestDefinition
        {
            Code = testCode,
            DisplayName = (isCountTest ? "Total Count " : "Assay ") + tag,
            SectionId = section.Id,
            WorkflowType = isCountTest ? WorkflowType.CountTest : WorkflowType.Measurement
        };
        arrangeDb.TestDefinitions.Add(td);

        var item = new Item
        {
            Code = "RET-" + tag,
            Name = "Retired Item " + tag,
            Category = SampleCategory.FinishedProduct,
            AssignedTests = new List<SampleTest>
            {
                new() { TestCode = testCode, DisplayName = td.DisplayName }
            }
        };
        arrangeDb.Items.Add(item);

        var cause = new CauseOfTesting { Name = "Release-" + tag };
        arrangeDb.CausesOfTesting.Add(cause);

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia,
            MaterialName = "TSA Powder " + tag,
            ManufacturerName = "Himedia",
            BatchNumber = "LOT-" + tag,
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            Code = "TSA-" + tag,
            Location = "Micro Lab",
            QuantityReceived = 500,
            QuantityRemaining = 500,
            Unit = MaterialUnit.Gram,
            SectionId = section.Id
        };
        arrangeDb.Materials.Add(material);
        await arrangeDb.SaveChangesAsync();

        if (isCountTest)
        {
            var step = new TestWorkflowStep
            {
                TestDefinitionId = td.Id,
                StepOrder = 1,
                StepName = "CountIncubation",
                IncubationMinHours = 72,
                IncubationMaxHours = 120,
                TemperatureMin = 30,
                TemperatureMax = 35,
                IsFinalStep = true,
                StepType = StepType.PlateCount
            };
            arrangeDb.TestWorkflowSteps.Add(step);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia
            {
                TestWorkflowStepId = step.Id,
                MediaProductId = MediaProductTestData.ProductIdFor(arrangeDb, material.Id),
                TempMin = 30,
                TempMax = 35,
                IncubationMinHours = 72,
                IncubationMaxHours = 120
            });
        }

        var media = new Media
        {
            MaterialId = material.Id,
            LotNumber = "MLOT-" + tag,
            IsReleasedForUse = true,
            Status = MediaStatus.Active,
            ExpiryDate = DateTime.UtcNow.AddDays(30)
        };
        arrangeDb.Media.Add(media);

        var incubator = new Equipment { Code = "INC-" + tag, Name = "Incubator " + tag, Type = EquipmentType.Incubator, SetPointTemperature = 32, SectionId = section.Id };
        arrangeDb.Equipment.Add(incubator);

        await arrangeDb.SaveChangesAsync();

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testCode,
            ParameterName = isCountTest ? "" : "Active Content",
            LimitType = isCountTest ? LimitType.CountTiered : LimitType.Range,
            LowerLimit = isCountTest ? null : 95m,
            UpperLimit = isCountTest ? null : 105m,
            LowerInclusive = true,
            UpperInclusive = true,
            DilutionFactor = isCountTest ? 10m : null,
            ActionLimit = isCountTest ? "100" : "",
            SpecLimit = isCountTest ? "200" : "",
            Unit = isCountTest ? "CFU/g" : "%"
        };
        arrangeDb.Specifications.Add(spec);
        await arrangeDb.SaveChangesAsync();

        if (seedSampleWithResult)
        {
            var sample = new Sample
            {
                Category = SampleCategory.FinishedProduct,
                ItemId = item.Id,
                ProductionStage = stage.Name,
                ProductionStageId = stage.Id,
                ControlNumber = "CTL-" + tag,
                ReferenceNumber = "REF-" + tag,
                CauseOfTestingId = cause.Id,
                ReceivedByUserId = _fixture.SeededUserId,
                Status = SampleStatus.Approved,
                PreparationStatus = SamplePreparationStatus.Ready
            };
            var order = new TestOrder
            {
                TestCode = testCode,
                Status = ApprovalStatus.Pending,
                CurrentStep = WorkflowStep.Approved,
                SectionId = section.Id
            };
            sample.TestOrders.Add(order);
            arrangeDb.Samples.Add(sample);
            await arrangeDb.SaveChangesAsync();

            var sig = new ElectronicSignature
            {
                UserId = _fixture.SeededUserId,
                UserFullNameSnapshot = "Seeded User",
                UsernameSnapshot = "seeded",
                MeaningOfSignature = SignatureMeaning.Approved,
                EntityType = nameof(TestAnalysis),
                EntityId = order.Id,
                Comment = "Initial entry"
            };
            arrangeDb.ElectronicSignatures.Add(sig);
            await arrangeDb.SaveChangesAsync();

            var analysis = new TestAnalysis
            {
                TestOrderId = order.Id,
                AnalysisType = td.WorkflowType,
                AnalysedAt = DateTime.UtcNow,
                EnteredByUserId = _fixture.SeededUserId,
                EnteredAt = DateTime.UtcNow,
                SignatureId = sig.Id,
                IsActive = true
            };
            arrangeDb.TestAnalyses.Add(analysis);
            await arrangeDb.SaveChangesAsync();

            arrangeDb.ParameterResults.Add(new ParameterResult
            {
                TestAnalysisId = analysis.Id,
                TestOrderId = order.Id,
                SpecificationId = spec.Id,
                ParameterName = spec.ParameterName ?? "Count",
                ReportedDisplay = "99.4",
                ReportedValue = 99.4m,
                Unit = spec.Unit,
                ComparisonStatus = ResultStatus.WithinLimits
            });
            await arrangeDb.SaveChangesAsync();
        }

        return new SeededSpecScenario(section.Id, item.Id, spec.Id, testCode, cause.Id, stage.Name, media.Id, incubator.Id);
    }

    [PostgresFact]
    public async Task DeleteUnusedSpec_WithReason_Deleted()
    {
        var scn = await SeedCountItemWithSpecAsync(isCountTest: false, seedSampleWithResult: false);

        SpecificationDeleteOutcome outcome;
        await using (var actDb = _fixture.CreateDbContext())
        {
            outcome = await BuildSpecService(actDb).DeleteSpecificationAsync(_fixture.SeededUserId, scn.SpecId, ValidReason);
        }

        Assert.Equal("deleted", outcome.Outcome);
        Assert.Equal(scn.SpecId, outcome.Id);

        await using var assertDb = _fixture.CreateDbContext();
        Assert.Null(await assertDb.Specifications.AsNoTracking().FirstOrDefaultAsync(s => s.Id == scn.SpecId));
        var audit = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(Specification) && a.EntityId == scn.SpecId.ToString() && a.Action == "Delete")
            .OrderByDescending(a => a.Id)
            .FirstAsync();
        Assert.Equal(ValidReason, audit.Reason);
    }

    [PostgresFact]
    public async Task DeleteUnusedSpec_NoReason_Refused()
    {
        var scn = await SeedCountItemWithSpecAsync(isCountTest: false, seedSampleWithResult: false);

        await using (var actDb = _fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
                BuildSpecService(actDb).DeleteSpecificationAsync(_fixture.SeededUserId, scn.SpecId, changeReason: null));
            Assert.Contains(ex.FieldErrors, e => e.Field == ChangeReasonGuard.FieldName);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.NotNull(await assertDb.Specifications.AsNoTracking().FirstOrDefaultAsync(s => s.Id == scn.SpecId));
    }

    [PostgresFact]
    public async Task DeleteSpecWithResults_IsRetired_NotDeleted_ResultsUnchanged()
    {
        var scn = await SeedCountItemWithSpecAsync(isCountTest: false, seedSampleWithResult: true);

        ParameterResult beforeResult;
        await using (var preDb = _fixture.CreateDbContext())
        {
            beforeResult = await preDb.ParameterResults.AsNoTracking().SingleAsync(p => p.SpecificationId == scn.SpecId);
        }

        SpecificationDeleteOutcome outcome;
        await using (var actDb = _fixture.CreateDbContext())
        {
            outcome = await BuildSpecService(actDb).DeleteSpecificationAsync(_fixture.SeededUserId, scn.SpecId, ValidReason);
        }

        Assert.Equal("retired", outcome.Outcome);
        Assert.Equal(scn.SpecId, outcome.Id);

        await using var assertDb = _fixture.CreateDbContext();
        var spec = await assertDb.Specifications.AsNoTracking().SingleAsync(s => s.Id == scn.SpecId);
        Assert.True(spec.IsRetired);
        Assert.Equal(FixedUtc.UtcDateTime, spec.RetiredAt);
        Assert.Equal(_fixture.SeededUserId, spec.RetiredByUserId);

        var afterResult = await assertDb.ParameterResults.AsNoTracking().SingleAsync(p => p.SpecificationId == scn.SpecId);
        Assert.Equal(beforeResult.Id, afterResult.Id);
        Assert.Equal(beforeResult.ReportedValue, afterResult.ReportedValue);
        Assert.Equal(beforeResult.ReportedDisplay, afterResult.ReportedDisplay);
        Assert.Equal(beforeResult.ComparisonStatus, afterResult.ComparisonStatus);
        Assert.Equal(beforeResult.SpecificationId, afterResult.SpecificationId);

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(Specification) && a.EntityId == scn.SpecId.ToString())
            .OrderByDescending(a => a.Id)
            .FirstAsync();
        Assert.Equal(ValidReason, audit.Reason);
    }

    [PostgresFact]
    public async Task RetiredSpec_NotUsedForNewSampleAssignmentOrEvaluation()
    {
        var scn = await SeedCountItemWithSpecAsync(isCountTest: false, seedSampleWithResult: true);

        // Give the retired spec a Grade so if receipt still read retired specs it would assign the grade to new samples.
        await using (var prepDb = _fixture.CreateDbContext())
        {
            var spec = await prepDb.Specifications.SingleAsync(s => s.Id == scn.SpecId);
            spec.Grade = "EP";
            await prepDb.SaveChangesAsync();
            await BuildSpecService(prepDb).DeleteSpecificationAsync(_fixture.SeededUserId, scn.SpecId, ValidReason);
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var grades = await new ItemService(actDb).GetGradesAsync(scn.ItemId);
            Assert.Empty(grades);

            var sample = await new ProductWorkflowEngine(actDb, new ReferenceNumberGenerator(actDb)).ReceiveAsync(
                new ItemBasedReceiveRequest(
                    ItemId: scn.ItemId,
                    CauseOfTestingId: scn.CauseId,
                    SampleQuantity: "10",
                    SampledBy: "QA Sampler",
                    BatchNumber: "B1",
                    ControlNumber: "C-" + Tag(),
                    MfgDate: null,
                    ExpDate: null,
                    ProductionStage: scn.StageName,
                    ReceivedByUserId: _fixture.SeededUserId,
                    Grade: null));

            Assert.Null(sample.Grade);
            var lookupSpecs = await SpecificationLookup.ForSampleAsync(actDb, sample.Id, scn.ItemId, scn.TestCode);
            Assert.Empty(lookupSpecs);
        }
    }

    [PostgresFact]
    public async Task ResultEntryAgainstRetiredSpec_ThrowsClearMessage()
    {
        var scn = await SeedCountItemWithSpecAsync(isCountTest: true, seedSampleWithResult: true);

        // Create a second sample that is currently in testing BEFORE the specification is retired.
        int openOrderId;
        await using (var prepDb = _fixture.CreateDbContext())
        {
            var sample = await new ProductWorkflowEngine(prepDb, new ReferenceNumberGenerator(prepDb)).ReceiveAsync(
                new ItemBasedReceiveRequest(
                    ItemId: scn.ItemId,
                    CauseOfTestingId: scn.CauseId,
                    SampleQuantity: "10",
                    SampledBy: "QA Sampler",
                    BatchNumber: "B1",
                    ControlNumber: "C-" + Tag(),
                    MfgDate: null,
                    ExpDate: null,
                    ProductionStage: scn.StageName,
                    ReceivedByUserId: _fixture.SeededUserId));

            var order = await prepDb.TestOrders.SingleAsync(o => o.SampleId == sample.Id && o.TestCode == scn.TestCode);
            sample.PreparationStatus = SamplePreparationStatus.Ready;
            prepDb.SamplePreparations.Add(new SamplePreparation
            {
                SampleId = sample.Id,
                Amount = 10m,
                Technique = "PourPlate",
                Diluent = "Diluent",
                Neutralizer = "None",
                PreparedByUserId = _fixture.SeededUserId,
                PreparedAt = DateTime.UtcNow
            });
            await prepDb.SaveChangesAsync();

            var engine = TestServiceFactory.TestWorkflow(prepDb);
            await engine.SelectMediaAsync(order.Id, "CountIncubation", scn.MediaId, scn.IncubatorId, _fixture.SeededUserId);
            openOrderId = order.Id;

            // Now retire the specification while the sample is in testing.
            await BuildSpecService(prepDb).DeleteSpecificationAsync(_fixture.SeededUserId, scn.SpecId, ValidReason);
        }

        await using var actDb = _fixture.CreateDbContext();
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            TestServiceFactory.TestWorkflow(actDb).RecordResultAsync(
                openOrderId,
                "CountIncubation",
                new CountTestPayload(new List<string> { "12", "14" }, 10m),
                _fixture.SeededUserId));

        Assert.Contains("has been retired. Ask the Section Head to reinstate it or configure a new one.", ex.Message);
    }

    [PostgresFact]
    public async Task Reinstate_WithReason_Works_Audited()
    {
        var scn = await SeedCountItemWithSpecAsync(isCountTest: false, seedSampleWithResult: true);

        await using (var retireDb = _fixture.CreateDbContext())
        {
            await BuildSpecService(retireDb).DeleteSpecificationAsync(_fixture.SeededUserId, scn.SpecId, ValidReason);
        }

        // Reinstating without a reason must fail.
        await using (var noReasonDb = _fixture.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<FieldValidationException>(() =>
                BuildSpecService(noReasonDb).ReinstateSpecificationAsync(_fixture.SeededUserId, scn.SpecId, changeReason: "short"));
            Assert.Contains(ex.FieldErrors, e => e.Field == ChangeReasonGuard.FieldName);
        }

        SpecificationResponse reinstated;
        await using (var actDb = _fixture.CreateDbContext())
        {
            reinstated = await BuildSpecService(actDb).ReinstateSpecificationAsync(_fixture.SeededUserId, scn.SpecId, ReinstateReason);
        }

        Assert.False(reinstated.IsRetired);
        Assert.Null(reinstated.RetiredAt);
        Assert.Null(reinstated.RetiredByUserId);

        await using var assertDb = _fixture.CreateDbContext();
        var spec = await assertDb.Specifications.AsNoTracking().SingleAsync(s => s.Id == scn.SpecId);
        Assert.False(spec.IsRetired);
        Assert.Null(spec.RetiredAt);
        Assert.Null(spec.RetiredByUserId);

        var audit = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(Specification) && a.EntityId == scn.SpecId.ToString())
            .OrderByDescending(a => a.Id)
            .FirstAsync();
        Assert.Equal(ReinstateReason, audit.Reason);
    }

    [PostgresFact]
    public async Task RetiredSpec_ListedInConfigurationWithRetiredMark()
    {
        var scn = await SeedCountItemWithSpecAsync(isCountTest: false, seedSampleWithResult: true);

        await using (var retireDb = _fixture.CreateDbContext())
        {
            await BuildSpecService(retireDb).DeleteSpecificationAsync(_fixture.SeededUserId, scn.SpecId, ValidReason);
        }

        await using var assertDb = _fixture.CreateDbContext();
        var rowsObj = await BuildSpecService(assertDb).GetSpecificationsAsync(_fixture.SeededUserId, scn.ItemId);
        var rows = Assert.IsAssignableFrom<List<SpecificationRowDto>>(rowsObj);
        var row = Assert.Single(rows, r => r.Id == scn.SpecId);
        Assert.True(row.IsRetired);
        Assert.Equal(FixedUtc.UtcDateTime, row.RetiredAt);
        Assert.Equal(_fixture.SeededUserId, row.RetiredByUserId);
    }

    [PostgresFact]
    public async Task AnalysisResultEntry_AgainstRetiredSpec_ThrowsClearMessage_NothingRecorded()
    {
        var tag = Tag();
        int orderId;
        int specId;
        int equipId;
        long baselineMaxAuditId;

        await using (var arrangeDb = _fixture.CreateDbContext())
        {
            var fpSection = await arrangeDb.DocumentSections.FirstAsync(s => s.Code == "FP");
            var user = await arrangeDb.Users.FirstAsync(u => u.Id == _fixture.SeededUserId);
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("IntegrationPassword123!");
            user.IsActive = true;

            var testDef = new TestDefinition
            {
                Code = "PH_RET_" + tag,
                DisplayName = "pH Retired Spec " + tag,
                SectionId = fpSection.Id,
                WorkflowType = WorkflowType.Measurement,
                EquationType = EquationType.Measurement,
                ReplicateCount = 3,
                EvaluationBasis = MeasurementEvaluationBasis.Mean,
                IsActive = true
            };
            arrangeDb.TestDefinitions.Add(testDef);

            var equip = new Equipment
            {
                Name = "pH Meter " + tag,
                Code = "EQ-RET-" + tag,
                Type = EquipmentType.PhMeter,
                SectionId = fpSection.Id
            };
            arrangeDb.Equipment.Add(equip);

            var item = new Item
            {
                Name = "Retired Spec Tablet " + tag,
                Code = "ITEM-RET-" + tag,
                Category = SampleCategory.FinishedProduct,
                IsActive = true
            };
            arrangeDb.Items.Add(item);
            await arrangeDb.SaveChangesAsync();

            var spec = new Specification
            {
                ItemId = item.Id,
                TestCode = testDef.Code,
                ParameterName = "pH",
                LimitType = LimitType.TargetWithTolerance,
                Target = 6.0m,
                Tolerance = 0.5m,
                ToleranceMode = ToleranceMode.Absolute,
                SpecLimit = "6.0 ± 0.5",
                DisplayOrder = 1,
                IsRetired = true,
                RetiredAt = FixedUtc.UtcDateTime,
                RetiredByUserId = _fixture.SeededUserId
            };
            arrangeDb.Specifications.Add(spec);

            var cause = await arrangeDb.CausesOfTesting.FirstOrDefaultAsync()
                ?? arrangeDb.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine " + tag, IsActive = true }).Entity;

            var sample = new Sample
            {
                ReferenceNumber = "SMP-RET-" + tag,
                Category = SampleCategory.FinishedProduct,
                ItemId = item.Id,
                ReceivedAt = DateTime.UtcNow,
                CauseOfTesting = cause,
                ReceivedByUserId = _fixture.SeededUserId,
                Status = SampleStatus.InTesting
            };
            arrangeDb.Samples.Add(sample);
            await arrangeDb.SaveChangesAsync();

            var order = new TestOrder
            {
                SampleId = sample.Id,
                TestCode = testDef.Code,
                SectionId = fpSection.Id,
                CurrentStep = WorkflowStep.Running,
                Status = ApprovalStatus.InProgress
            };
            arrangeDb.TestOrders.Add(order);
            await arrangeDb.SaveChangesAsync();

            orderId = order.Id;
            specId = spec.Id;
            equipId = equip.Id;
            baselineMaxAuditId = await arrangeDb.AuditLogs.AsNoTracking()
                .Select(a => (long?)a.Id)
                .MaxAsync() ?? 0L;
        }

        await using (var actDb = _fixture.CreateDbContext())
        {
            var payload = new MeasurementPayload(
                AnalysedAt: DateTime.UtcNow,
                EquipmentId: equipId,
                Parameters: new List<MeasurementParameterInput>
                {
                    new(specId, new List<decimal> { 6.01m, 6.02m, 5.99m })
                },
                Password: "IntegrationPassword123!",
                Comment: "Attempt against retired spec");

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                TestServiceFactory.TestWorkflow(actDb).RecordMeasurementResultAsync(
                    orderId, payload, _fixture.SeededUserId, "127.0.0.1"));

            Assert.Contains("has been retired. Ask the Section Head to reinstate it or configure a new one.", ex.Message);
        }

        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.TestAnalyses.AsNoTracking().AnyAsync(a => a.TestOrderId == orderId));
        Assert.False(await assertDb.ParameterResults.AsNoTracking().AnyAsync(p => p.TestOrderId == orderId));
        Assert.False(await assertDb.AuditLogs.AsNoTracking().AnyAsync(a => a.Id > baselineMaxAuditId));
    }
}
