using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using Xunit;

using Base = MicroLIMS.Tests.UnitTests.HplcMethodServiceTests;

namespace MicroLIMS.Tests.IntegrationTests;

[Collection("PostgresDatabaseCollection")]
public class HplcMoisturePostgresTests
{
    private const string Password = "ValidPassword123!";
    private readonly PostgresTestFixture _fixture;

    public HplcMoisturePostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private sealed class HplcScenario
    {
        public required DocumentSection Section { get; init; }
        public required int UserId { get; init; }
        public required Item Item { get; init; }
        public required HplcMethod Method { get; init; }
        public required HplcMethodAnalyte Analyte { get; init; }
        public required TestDefinition TestDef { get; init; }
        public required Specification Spec { get; init; }
        public required Sample Sample { get; init; }
        public required TestOrder Order { get; init; }
        public required Equipment Equipment { get; init; }
        public required ChromatographyColumn Column { get; init; }
        public required HplcRun Run { get; init; }
        public required HplcRunSample RunSample { get; init; }
    }

    private static async Task<HplcScenario> SeedScenarioAsync(
        MicroLimsDbContext db, int userId, ResultBasis basis = ResultBasis.PercentDriedBasis)
    {
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var section = TestServiceFactory.EnsureMicroSection(db);
        TestServiceFactory.AssignUserToMicroSection(db, userId);

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);
        await db.SaveChangesAsync();

        var diluent = await Base.AddSolutionAsync(db, section.Id, "Diluent " + tag, SolutionType.Diluent);
        var mp = await Base.AddSolutionAsync(db, section.Id, "Mobile " + tag, SolutionType.MobilePhase);
        var standard = await Base.AddEntryAsync(db, section.Id, "STD-" + tag);

        var stage = await db.ProductionStages.FirstOrDefaultAsync(p => p.Role == ProductionStageRole.Bulk);
        if (stage == null)
        {
            stage = new ProductionStage { Name = "Bulk " + tag, Role = ProductionStageRole.Bulk, IsActive = true };
            db.ProductionStages.Add(stage);
            await db.SaveChangesAsync();
        }

        var item = new Item
        {
            Code = "RM-" + tag,
            Name = "Raw Material " + tag,
            Category = SampleCategory.RawMaterial,
            IsActive = true,
        };
        db.Items.Add(item);

        var method = new HplcMethod
        {
            Name = "HPLC Assay " + tag,
            Abbreviation = "H" + tag,
            SectionId = section.Id,
            DiluentSolutionId = diluent.Id,
            ResultMode = HplcResultMode.Assay,
            Technique = HplcTechnique.Hplc,
            IsActive = true,
        };
        method.MobilePhases.Add(new HplcMethodMobilePhase { Channel = "A", SolutionMasterId = mp.Id });
        db.HplcMethods.Add(method);

        var analyte = new HplcMethodAnalyte
        {
            HplcMethod = method,
            Name = "Active Ingredient",
            StandardEntryId = standard.Id,
            WavelengthNm = 254m,
            DisplayOrder = 1,
            TheoreticalWeightStdMg = 50m,
            TheoreticalWeightTestMg = 50m,
            StandardInjections = 3,
        };
        db.HplcMethodAnalytes.Add(analyte);

        var testDef = new TestDefinition
        {
            Code = "HPLC-" + tag,
            DisplayName = "HPLC Content Assay " + tag,
            SectionId = section.Id,
            WorkflowType = WorkflowType.HplcMethodAssay,
            EquationType = EquationType.HplcMethodAssay,
            RequiresSystemSuitability = true,
            HplcMethod = method,
            IsActive = true,
        };
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = testDef.Code });

        // If dried basis, also assign an LOD test definition to the item so the prerequisite check succeeds
        if (basis == ResultBasis.PercentDriedBasis)
        {
            var lodDef = new TestDefinition
            {
                Code = "LOD-" + tag,
                DisplayName = "Loss on Drying " + tag,
                SectionId = section.Id,
                WorkflowType = WorkflowType.Gravimetric,
                EquationType = EquationType.GravimetricLoss,
                MoistureRole = MoistureRole.LossOnDrying,
                IsActive = true,
            };
            db.TestDefinitions.Add(lodDef);
            await db.SaveChangesAsync();
            db.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = lodDef.Code });
        }

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = "Active Ingredient",
            HplcMethodAnalyteId = analyte.Id,
            LimitType = LimitType.Range,
            LowerLimit = 95m,
            UpperLimit = 105m,
            Unit = "%",
            ResultBasis = basis,
            DisplayOrder = 1,
        };
        db.Specifications.Add(spec);

        db.TestDefinitionStageReplicates.Add(new TestDefinitionStageReplicate
        {
            TestDefinitionId = testDef.Id,
            Role = ProductionStageRole.Bulk,
            SampleReplicates = 1,
        });

        var cause = await db.CausesOfTesting.FirstOrDefaultAsync()
            ?? db.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine Testing", IsActive = true }).Entity;

        var sample = new Sample
        {
            ItemId = item.Id,
            Category = SampleCategory.RawMaterial,
            ReferenceNumber = "SMP-" + tag,
            BatchNumber = "B-001",
            ProductionStageId = stage.Id,
            ProductionStage = stage.Name,
            CauseOfTesting = cause,
            ReceivedAt = DateTime.UtcNow,
            ReceivedByUserId = userId,
            Status = SampleStatus.InTesting,
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var order = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = testDef.Code,
            SectionId = section.Id,
            CurrentStep = WorkflowStep.Running,
            Status = ApprovalStatus.InProgress,
        };
        db.TestOrders.Add(order);

        var equipment = new Equipment
        {
            Code = "EQ-" + tag,
            Name = "HPLC System " + tag,
            Type = EquipmentType.Hplc,
            SectionId = section.Id,
            CalibrationDueDate = DateTime.UtcNow.AddYears(1),
        };
        db.Equipment.Add(equipment);

        var column = new ChromatographyColumn
        {
            Code = "COL-" + tag,
            Name = "C18 Column " + tag,
            UspDesignation = "L1",
            SectionId = section.Id,
            IsActive = true,
            CreatedByUserId = userId,
            LastModifiedByUserId = userId,
        };
        column.CompatibleEquipment.Add(equipment);
        db.ChromatographyColumns.Add(column);
        await db.SaveChangesAsync();

        var snapshotJson = JsonSerializer.Serialize(HplcMethodResponse.From(method), SnapshotJson.Options);

        var run = new HplcRun
        {
            Code = "RUN-" + tag,
            SectionId = section.Id,
            EquipmentId = equipment.Id,
            HplcMethodId = method.Id,
            ChromatographyColumnId = column.Id,
            Status = HplcRunStatus.Open,
            MethodSnapshotJson = snapshotJson,
        };
        db.HplcRuns.Add(run);
        await db.SaveChangesAsync();

        var sst = new HplcSstRecord
        {
            HplcRunId = run.Id,
            Code = "SST-" + tag,
            Status = HplcSstStatus.Passed,
            Analytes =
            {
                new HplcSstAnalyte
                {
                    HplcMethodAnalyteId = analyte.Id,
                    MeanResponse = 1000m,
                    StandardWeightMg = 50m,
                    StandardPurityPercent = 100m,
                    StandardMoisturePercent = 0m,
                }
            }
        };
        db.HplcSstRecords.Add(sst);
        await db.SaveChangesAsync();

        // One replicate: weight 50, response 980 -> as-is 98.0 %
        var runSample = new HplcRunSample
        {
            HplcRunId = run.Id,
            TestOrderId = order.Id,
            Status = HplcRunSampleStatus.Assigned,
            Replicates =
            {
                new HplcSampleReplicate
                {
                    ReplicateNo = 1,
                    ActualWeightMg = 50m,
                    Responses =
                    {
                        new HplcReplicateResponse
                        {
                            HplcMethodAnalyteId = analyte.Id,
                            Response = 980m,
                        }
                    }
                }
            }
        };
        db.HplcRunSamples.Add(runSample);
        await db.SaveChangesAsync();

        var evidence = new HplcEvidence
        {
            HplcRunId = run.Id,
            HplcRunSampleId = runSample.Id,
            Kind = HplcEvidenceKind.SampleReport,
            Context = HplcEvidenceContext.Sample,
            FilePath = "report.pdf",
            FileName = "report.pdf",
            ContentType = "application/pdf",
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
        };
        db.HplcEvidences.Add(evidence);
        await db.SaveChangesAsync();

        return new HplcScenario
        {
            Section = section,
            UserId = userId,
            Item = item,
            Method = method,
            Analyte = analyte,
            TestDef = testDef,
            Spec = spec,
            Sample = sample,
            Order = order,
            Equipment = equipment,
            Column = column,
            Run = run,
            RunSample = runSample,
        };
    }

    private static (TestOrder Order, ParameterResult Result) AddMoistureResult(
        MicroLimsDbContext db, int sampleId, MoistureRole role, decimal percent, int userId, int sectionId)
    {
        var uid = Guid.NewGuid().ToString("N")[..6];
        var def = new TestDefinition
        {
            Code = $"{(role == MoistureRole.LossOnDrying ? "LOD" : "KF")}_{uid}",
            DisplayName = $"Moisture {uid}",
            SectionId = sectionId,
            IsActive = true,
            MoistureRole = role,
            ReplicateCount = 1,
            WorkflowType = role == MoistureRole.LossOnDrying ? WorkflowType.Gravimetric : WorkflowType.Titration,
            EquationType = role == MoistureRole.LossOnDrying ? EquationType.GravimetricLoss : EquationType.Titration,
        };
        db.TestDefinitions.Add(def);

        var sample = db.Samples.First(s => s.Id == sampleId);
        int? specId = null;
        if (sample.ItemId is int itemId)
        {
            db.SampleTests.Add(new SampleTest { ItemId = itemId, TestCode = def.Code });
            var spec = new Specification
            {
                ItemId = itemId,
                TestCode = def.Code,
                ParameterName = def.DisplayName,
                LimitType = LimitType.NotMoreThan,
                UpperLimit = 5m,
                Unit = "%",
                ResultBasis = ResultBasis.PercentAsIs,
            };
            db.Specifications.Add(spec);
            db.SaveChanges();
            specId = spec.Id;
        }

        var moistureOrder = new TestOrder
        {
            SampleId = sampleId,
            TestCode = def.Code,
            SectionId = sectionId,
            CurrentStep = WorkflowStep.Ready,
            Status = ApprovalStatus.ResultEntered,
        };
        db.TestOrders.Add(moistureOrder);
        db.SaveChanges();

        var user = db.Users.First(u => u.Id == userId);
        var sig = new ElectronicSignature
        {
            UserId = userId,
            UserFullNameSnapshot = user.FullName,
            UsernameSnapshot = user.Username,
            RoleSnapshot = "Analyst",
            MeaningOfSignature = SignatureMeaning.ResultRecorded,
            EntityType = "TestOrder",
            EntityId = moistureOrder.Id,
            SignedAt = DateTime.UtcNow,
        };
        db.ElectronicSignatures.Add(sig);

        var analysis = new TestAnalysis
        {
            TestOrderId = moistureOrder.Id,
            AnalysisType = def.WorkflowType,
            AnalysedAt = DateTime.UtcNow,
            IsActive = true,
            EnteredByUserId = userId,
            EnteredAt = DateTime.UtcNow,
            Signature = sig,
        };
        db.TestAnalyses.Add(analysis);
        db.SaveChanges();

        var pr = new ParameterResult
        {
            TestOrderId = moistureOrder.Id,
            SpecificationId = specId!.Value,
            TestAnalysisId = analysis.Id,
            ParameterName = def.DisplayName,
            ReportedValue = percent,
            ReportedDisplay = $"{percent:0.0} %",
            Unit = "%",
            IsActive = true,
            ComparisonStatus = ResultStatus.WithinLimits,
        };
        db.ParameterResults.Add(pr);
        db.SaveChanges();

        return (moistureOrder, pr);
    }

    [PostgresFact]
    public async Task HplcDriedAssay_BeforeLod_Refused()
    {
        await using var db = _fixture.CreateDbContext();
        var s = await SeedScenarioAsync(db, _fixture.SeededUserId, ResultBasis.PercentDriedBasis);

        // Preview checks
        var runService = TestServiceFactory.HplcRun(db);
        var entry = await runService.GetSampleEntryAsync(s.RunSample.Id, s.UserId);
        Assert.False(entry.CanSubmit);
        Assert.Contains("Record the sample's loss on drying result", entry.CanSubmitReason);
        Assert.Equal(MoistureRole.LossOnDrying, entry.MoistureRole);

        // Submit checks
        var workflow = TestServiceFactory.TestWorkflow(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => workflow.SubmitHplcMethodAssayAsync(s.RunSample.Id, Password, "ok", s.UserId, "127.0.0.1"));
        Assert.Contains("Record the sample's loss on drying result", ex.Message);
    }

    [PostgresFact]
    public async Task HplcDriedAssay_UsesLod()
    {
        await using var db = _fixture.CreateDbContext();
        var s = await SeedScenarioAsync(db, _fixture.SeededUserId, ResultBasis.PercentDriedBasis);

        var (lodOrder, lodResult) = AddMoistureResult(db, s.Sample.Id, MoistureRole.LossOnDrying, 0.5m, s.UserId, s.Section.Id);

        // Preview should calculate as-is 98.0 with LOD 0.5 -> 98.49, display "98.5 %"
        var runService = TestServiceFactory.HplcRun(db);
        var entry = await runService.GetSampleEntryAsync(s.RunSample.Id, s.UserId);
        Assert.True(entry.CanSubmit);
        Assert.Equal(MoistureRole.LossOnDrying, entry.MoistureRole);
        var preview = Assert.Single(entry.Preview);
        Assert.Equal(98.49m, preview.Value);
        Assert.Equal("98.5 %", preview.Display);

        // Submit
        var workflow = TestServiceFactory.TestWorkflow(db);
        var result = await workflow.SubmitHplcMethodAssayAsync(s.RunSample.Id, Password, "ok", s.UserId, "127.0.0.1");
        Assert.True(result.AllStepsComplete);

        await using var check = _fixture.CreateDbContext();
        var analysis = await check.TestAnalyses.Include(a => a.ParameterResults).SingleAsync(a => a.TestOrderId == s.Order.Id);
        Assert.True(analysis.IsActive);
        var pr = Assert.Single(analysis.ParameterResults);
        Assert.Equal(98.49m, pr.ReportedValue);
        Assert.Equal("98.5 %", pr.ReportedDisplay);

        Assert.NotNull(analysis.ConditionsJson);
        using var doc = JsonDocument.Parse(analysis.ConditionsJson);
        var moisture = doc.RootElement.GetProperty("moisture");
        Assert.Equal(lodResult.Id, moisture.GetProperty("parameterResultId").GetInt32());
        Assert.Equal(lodOrder.Id, moisture.GetProperty("testOrderId").GetInt32());
        Assert.Equal(0.5m, moisture.GetProperty("percent").GetDecimal());
    }

    [PostgresFact]
    public async Task ReturningLod_ReturnsHplcAssay()
    {
        await using var db = _fixture.CreateDbContext();
        var s = await SeedScenarioAsync(db, _fixture.SeededUserId, ResultBasis.PercentDriedBasis);

        var (lodOrder, _) = AddMoistureResult(db, s.Sample.Id, MoistureRole.LossOnDrying, 0.5m, s.UserId, s.Section.Id);

        var workflow = TestServiceFactory.TestWorkflow(db);
        await workflow.SubmitHplcMethodAssayAsync(s.RunSample.Id, Password, "ok", s.UserId, "127.0.0.1");

        // Verify HPLC order is in ResultEntered
        var orderBefore = await db.TestOrders.FindAsync(s.Order.Id);
        Assert.Equal(ApprovalStatus.ResultEntered, orderBefore!.Status);

        // Return the LOD order
        var review = TestServiceFactory.SampleReview(db);
        await review.ReturnToAnalystAsync(lodOrder.Id, s.UserId, "Re-drying required");

        await using var check = _fixture.CreateDbContext();
        // The LOD order was returned
        var lodCheck = await check.TestOrders.FindAsync(lodOrder.Id);
        Assert.Equal(WorkflowStep.Running, lodCheck!.CurrentStep);

        // The dependent HPLC assay was returned too
        var hplcCheck = await check.TestOrders.FindAsync(s.Order.Id);
        Assert.Equal(WorkflowStep.Running, hplcCheck!.CurrentStep);
        Assert.False((await check.TestAnalyses.SingleAsync(a => a.TestOrderId == s.Order.Id)).IsActive);
        Assert.All(await check.ParameterResults.Where(p => p.TestOrderId == s.Order.Id).ToListAsync(), p => Assert.False(p.IsActive));

        var events = await check.TestReturnEvents.Where(e => e.TestOrderId == s.Order.Id).ToListAsync();
        var retEvent = Assert.Single(events);
        Assert.Equal("Recalculate: the loss on drying result was returned.", retEvent.Reason);
    }

    [PostgresFact]
    public async Task AsIsAssay_NoMoistureNeeded()
    {
        await using var db = _fixture.CreateDbContext();
        var s = await SeedScenarioAsync(db, _fixture.SeededUserId, ResultBasis.PercentAsIs);

        var runService = TestServiceFactory.HplcRun(db);
        var entry = await runService.GetSampleEntryAsync(s.RunSample.Id, s.UserId);
        Assert.True(entry.CanSubmit);
        Assert.Null(entry.MoistureRole);
        var preview = Assert.Single(entry.Preview);
        Assert.Equal(98.0m, preview.Value);
        Assert.Equal("98.0 %", preview.Display);

        var workflow = TestServiceFactory.TestWorkflow(db);
        var result = await workflow.SubmitHplcMethodAssayAsync(s.RunSample.Id, Password, "ok", s.UserId, "127.0.0.1");
        Assert.True(result.AllStepsComplete);

        await using var check = _fixture.CreateDbContext();
        var analysis = await check.TestAnalyses.Include(a => a.ParameterResults).SingleAsync(a => a.TestOrderId == s.Order.Id);
        Assert.True(analysis.IsActive);
        var pr = Assert.Single(analysis.ParameterResults);
        Assert.Equal(98.0m, pr.ReportedValue);
        Assert.Equal("98.0 %", pr.ReportedDisplay);
        Assert.Null(analysis.ConditionsJson);
    }
}
