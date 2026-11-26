using System.Text.Json;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Tests.Fixtures;
using MicroLIMS.Tests.UnitTests;
using MicroLIMS.Tests.WorkflowTests;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static MicroLIMS.Tests.IntegrationTests.ItemSopResolverPostgresTests;

namespace MicroLIMS.Tests.IntegrationTests;

// D5 Task 2: the SOP revision is captured on the test order at every first recorded step, at exactly the
// recording points the competency guard covers, before the step's first write and in the same transaction.
// Every test arranges with one context, acts with another and asserts from a third with AsNoTracking.
// A step that is not the one under test is arranged with a capture that does nothing, so the step under test
// is the first to capture.
[Collection("PostgresDatabaseCollection")]
public class SopRevisionCapturePointsPostgresTests(PostgresTestFixture fixture)
{
    private static readonly DateTimeOffset CapturedAt = new(2037, 2, 3, 4, 5, 6, TimeSpan.Zero);
    private static readonly DateTime RevisionEffective = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly string Password = TitrationScenario.Password;

    private sealed class NoCapture : ISopRevisionCapture
    {
        public Task CaptureIfNeededAsync(TestOrder order, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed record Sop(string Code, int MasterId, int RevisionId);
    private sealed record Ctx(TitrationScenario S, Sop Sop, int? OwnSectionId = null)
    {
        public int UserId => S.UserId;
        public int SampleId => S.Order.SampleId;
        public int SectionId => OwnSectionId ?? S.Section.Id;
    }

    // The gate is switched on per section, so a test that turns it on works in a section of its own.
    private async Task<Ctx> WithOwnSectionAsync(Ctx c)
    {
        await using var db = fixture.CreateDbContext();
        var section = new DocumentSection
        {
            Code = $"GT{Guid.NewGuid():N}"[..10], Name = $"SOP capture gate lab {Guid.NewGuid():N}", DepartmentId = fixture.SeededDepartmentId, IsActive = true,
        };
        db.DocumentSections.Add(section);
        await db.SaveChangesAsync();
        db.UserOrgMemberships.Add(new UserOrgMembership { UserId = c.UserId, SectionId = section.Id, DepartmentId = fixture.SeededDepartmentId });
        await db.SaveChangesAsync();
        return c with { OwnSectionId = section.Id };
    }

    private ISopRevisionCapture Capture(MicroLimsDbContext db, DateTimeOffset? at = null) =>
        new SopRevisionCapture(db, new ItemSopResolver(db), new FixedTimeProvider(at ?? CapturedAt));

    private TestWorkflowEngine Engine(MicroLimsDbContext db) =>
        TestServiceFactory.TestWorkflow(db, sopCapture: Capture(db));

    private TestWorkflowEngine EngineWithoutCapture(MicroLimsDbContext db) =>
        TestServiceFactory.TestWorkflow(db, sopCapture: new NoCapture());

    private PathogenSessionService Session(MicroLimsDbContext db) =>
        TestServiceFactory.PathogenSession(db, new FixedTimeProvider(CapturedAt));

    private async Task<Ctx> NewScenarioAsync(Action<TitrationScenario.Options>? options = null, bool link = true)
    {
        await using var db = fixture.CreateDbContext();
        var o = new TitrationScenario.Options { ExistingUserId = fixture.SeededUserId };
        options?.Invoke(o);
        var s = TitrationScenario.Seed(db, o);
        var itemId = await db.Samples.Where(x => x.Id == s.Order.SampleId).Select(x => x.ItemId!.Value).SingleAsync();
        return new Ctx(s, link ? await LinkSopAsync(db, itemId, effective: true) : new Sop(string.Empty, 0, 0));
    }

    // The item gets an SOP number that resolves to one document with an effective, current revision 3.
    private async Task<Sop> LinkSopAsync(MicroLimsDbContext db, int itemId, bool effective)
    {
        var code = NewCode();
        var item = await db.Items.SingleAsync(i => i.Id == itemId);
        item.SopNumber = code;
        await db.SaveChangesAsync();
        var master = await AddMasterAsync(db, fixture, code);
        var revision = await AddRevisionAsync(db, fixture, master, 3,
            effective ? DocumentRevisionStatus.Effective : DocumentRevisionStatus.Draft, current: true);
        return new Sop(code, master.Id, revision.Id);
    }

    // A newer revision becomes effective between two steps.
    private async Task PublishRevisionAsync(Sop sop, int sequence)
    {
        await using var db = fixture.CreateDbContext();
        var master = await db.DocumentMasters.SingleAsync(m => m.Id == sop.MasterId);
        await AddRevisionAsync(db, fixture, master, sequence, DocumentRevisionStatus.Effective, current: true);
    }

    private sealed record Added(int OrderId, int SpecId, int? EquipmentId, string TestCode);

    // One more test of the scenario's sample, in the state a recorder expects (running, in progress).
    private async Task<Added> AddOrderAsync(Ctx c, Action<TestDefinition> definition, Action<Specification>? spec = null,
        EquipmentType? equipment = null, Action<TestOrder>? order = null)
    {
        await using var db = fixture.CreateDbContext();
        return await AddOrderAsyncIn(db, c, definition, spec, equipment, order);
    }

    private async Task AssertCapturedAsync(int orderId, Sop sop, string revision = "3", DateTimeOffset? at = null)
    {
        await using var assert = fixture.CreateDbContext();
        var stored = await assert.TestOrders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Equal(SopCaptureStatus.Captured, stored.SopCaptureStatus);
        Assert.Equal(sop.MasterId, stored.SopDocumentMasterId);
        Assert.Equal(sop.Code, stored.SopDocumentCode);
        Assert.Equal(sop.Code, stored.SopDocumentTitle);
        Assert.Equal(sop.RevisionId, stored.SopRevisionId);
        Assert.Equal(revision, stored.SopRevisionNumber);
        Assert.Equal(RevisionEffective, stored.SopEffectiveDateUtc);
        Assert.Equal((at ?? CapturedAt).UtcDateTime, stored.SopCapturedAtUtc);
    }

    private async Task AssertNotCapturedAsync(int orderId)
    {
        await using var assert = fixture.CreateDbContext();
        var stored = await assert.TestOrders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        Assert.Null(stored.SopCaptureStatus);
        Assert.Null(stored.SopDocumentMasterId);
        Assert.Null(stored.SopDocumentCode);
        Assert.Null(stored.SopDocumentTitle);
        Assert.Null(stored.SopRevisionId);
        Assert.Null(stored.SopRevisionNumber);
        Assert.Null(stored.SopEffectiveDateUtc);
        Assert.Null(stored.SopCapturedAtUtc);
    }

    // ------------------------------------------------------------------ analysis recorders

    [PostgresFact]
    public async Task Measurement_result_captures_the_revision()
    {
        var c = await NewScenarioAsync();
        var a = await AddOrderAsync(c, d => { d.WorkflowType = WorkflowType.Measurement; d.EquationType = EquationType.Measurement; });

        await using (var act = fixture.CreateDbContext())
            await Engine(act).RecordMeasurementResultAsync(a.OrderId,
                new(DateTime.UtcNow, null, [new(a.SpecId, [5m])], Password), c.UserId);

        await AssertCapturedAsync(a.OrderId, c.Sop);
        await using var assert = fixture.CreateDbContext();
        Assert.True(await assert.TestAnalyses.AsNoTracking().AnyAsync(x => x.TestOrderId == a.OrderId && x.IsActive));
    }

    [PostgresFact]
    public async Task Gravimetric_result_captures_the_revision()
    {
        var c = await NewScenarioAsync();
        var a = await AddOrderAsync(c, d =>
        {
            d.WorkflowType = WorkflowType.Gravimetric; d.EquationType = EquationType.GravimetricLoss;
            d.UsesTare = true; d.ConditionFields = "Temperature (Â°C), Time (h)";
        }, s => { s.LimitType = LimitType.NotMoreThan; s.LowerLimit = null; s.UpperLimit = 5m; }, EquipmentType.Oven);

        await using (var act = fixture.CreateDbContext())
            await Engine(act).RecordGravimetricResultAsync(a.OrderId, new(DateTime.UtcNow, a.EquipmentId,
                new Dictionary<string, string> { ["Temperature (Â°C)"] = "105", ["Time (h)"] = "3" },
                [new(a.SpecId, [new(25.1234m, 27.1234m, 27.0334m)])], Password), c.UserId);

        await AssertCapturedAsync(a.OrderId, c.Sop);
    }

    [PostgresFact]
    public async Task Qualitative_result_captures_the_revision()
    {
        var c = await NewScenarioAsync();
        var a = await AddOrderAsync(c, d => { d.WorkflowType = WorkflowType.Qualitative; d.EquationType = EquationType.Qualitative; },
            s => { s.LimitType = LimitType.Qualitative; s.LowerLimit = null; s.UpperLimit = null; s.SpecLimit = "Clear colourless liquid"; });

        await using (var act = fixture.CreateDbContext())
            await Engine(act).RecordQualitativeResultAsync(a.OrderId,
                new(DateTime.UtcNow, null, [new(a.SpecId, true, "Clear colourless liquid")], Password), c.UserId);

        await AssertCapturedAsync(a.OrderId, c.Sop);
    }

    [PostgresFact]
    public async Task Titration_result_captures_the_revision()
    {
        var c = await NewScenarioAsync();

        await using (var act = fixture.CreateDbContext())
            await Engine(act).RecordTitrationResultAsync(c.S.Order.Id, TitrationRecorderTests.Payload(c.S), c.UserId);

        await AssertCapturedAsync(c.S.Order.Id, c.Sop);
    }

    [PostgresFact]
    public async Task Uv_result_captures_the_revision()
    {
        var c = await NewScenarioAsync();
        var a = await AddOrderAsync(c, d =>
        {
            d.WorkflowType = WorkflowType.UvSpectrophotometry; d.EquationType = EquationType.UvSpectrophotometry;
            d.UvCalculationMode = UvCalculationMode.SpecificAbsorbanceAssay; d.UvSpecificAbsorbance = 328m; d.UvTotalVolumeMl = 5000m;
        }, s => { s.LowerLimit = 73m; s.UpperLimit = 79m; }, EquipmentType.UvVis);

        await using (var act = fixture.CreateDbContext())
            await Engine(act).RecordUvResultAsync(a.OrderId, new(DateTime.UtcNow, a.EquipmentId, null, null, null,
                [new(a.SpecId, 444m, null, [new(0.1m, 0.49m)])], Password, "uv"), c.UserId);

        await AssertCapturedAsync(a.OrderId, c.Sop);
    }

    [PostgresFact]
    public async Task Disintegration_result_captures_the_revision()
    {
        var c = await NewScenarioAsync();
        var a = await AddOrderAsync(c, DisintegrationDefinition,
            s => { s.LimitType = LimitType.DisintegrationTime; s.LowerLimit = null; s.UpperLimit = 30m; s.Unit = "min"; },
            EquipmentType.DisintegrationTester);

        await using (var act = fixture.CreateDbContext())
            await Engine(act).RecordDisintegrationResultAsync(a.OrderId, new(DateTime.UtcNow, a.EquipmentId,
                new Dictionary<string, string> { ["Medium"] = "Purified Water", ["Temperature"] = "37" },
                [12m, 14m, 15m, 13m, 16m, 20m], Password, "stage 1"), c.UserId);

        await AssertCapturedAsync(a.OrderId, c.Sop);
    }

    private static void DisintegrationDefinition(TestDefinition d)
    {
        d.WorkflowType = WorkflowType.Disintegration; d.EquationType = EquationType.Disintegration;
        d.DisintegrationStage1Units = 6; d.DisintegrationStage2Units = 12; d.DisintegrationMaxStage1Failures = 2;
        d.DisintegrationMinPassTotal = 16; d.ConditionFields = "Medium, Temperature";
    }

    [PostgresFact]
    public async Task Weight_variation_result_captures_the_revision()
    {
        var c = await NewScenarioAsync();
        var a = await AddOrderAsync(c, WeightVariationDefinition,
            s => { s.LimitType = LimitType.WeightVariation; s.DosageForm = DosageForm.HardCapsule; s.LowerLimit = null; s.UpperLimit = null; s.Unit = "mg"; },
            EquipmentType.Balance);

        await using (var act = fixture.CreateDbContext())
            await Engine(act).RecordWeightVariationResultAsync(a.OrderId, new(DateTime.UtcNow, a.EquipmentId,
                new Dictionary<string, string> { ["Balance ID"] = "BAL-01", ["Temperature"] = "22" },
                Enumerable.Range(0, 20).Select(_ => new WeightVariationUnitPayload(GrossMg: 600m, ShellMg: 200m)).ToList(), Password, "stage 1"), c.UserId);

        await AssertCapturedAsync(a.OrderId, c.Sop);
    }

    private static void WeightVariationDefinition(TestDefinition d)
    {
        d.WorkflowType = WorkflowType.WeightVariation; d.EquationType = EquationType.WeightVariation;
        d.WvUnitCount = 20; d.WvTabletBand1MaxMg = 130m; d.WvTabletBand1Percent = 10m; d.WvTabletBand2MaxMg = 324m;
        d.WvTabletBand2Percent = 7.5m; d.WvTabletBand3Percent = 5m; d.WvTabletMaxOutside = 2; d.WvCapsuleInnerPercent = 10m;
        d.WvCapsuleOuterPercent = 25m; d.WvCapsuleS1MaxOutside = 2; d.WvCapsuleS1MaxForRetest = 6; d.WvCapsuleS2ExtraUnits = 40;
        d.WvCapsuleS2MaxOutside = 6; d.ConditionFields = "Balance ID, Temperature";
    }

    [PostgresFact]
    public async Task Dissolution_result_captures_the_revision()
    {
        var c = await NewScenarioAsync();
        var uid = Guid.NewGuid().ToString("N")[..6];
        int orderId, equipmentId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var hplc = new Equipment { Name = $"HPLC {uid}", Code = $"EQ-HPLC-{uid}", Type = EquipmentType.Hplc, SectionId = c.SectionId };
            var column = new ChromatographyColumn
            {
                Name = $"Column {uid}", Code = $"COL-{uid}", SerialNumber = $"SN-{uid}", SectionId = c.SectionId, IsActive = true,
                CreatedByUserId = c.UserId, LastModifiedByUserId = c.UserId,
            };
            var diluent = new SolutionMaster
            {
                SectionId = c.SectionId, Name = $"Diluent {uid}", Type = SolutionType.Diluent, ShelfLifeValue = 30,
                ShelfLifeUnit = ShelfLifeUnit.Days, StorageCondition = "Room", FinalVolumeMl = 1000m,
                CreatedByUserId = c.UserId, LastModifiedByUserId = c.UserId,
            };
            var standard = new MaterialMasterEntry
            {
                SectionId = c.SectionId, Code = $"STD-{uid}", Name = $"Standard {uid}", Category = MaterialMasterCategory.ReferenceStandard,
                BaseUnit = MaterialUnit.Gram, CreatedByUserId = c.UserId, LastModifiedByUserId = c.UserId,
            };
            arrange.AddRange(hplc, column, diluent, standard);
            await arrange.SaveChangesAsync();
            var method = new HplcMethod
            {
                SectionId = c.SectionId, Name = $"Dissolution HPLC {uid}", Abbreviation = $"D{uid}".ToUpperInvariant(),
                ColumnDesignation = "L1", DiluentSolutionId = diluent.Id, EffectiveDate = DateTime.UtcNow,
                CreatedByUserId = c.UserId, LastModifiedByUserId = c.UserId,
                Analytes =
                {
                    new HplcMethodAnalyte
                    {
                        DisplayOrder = 1, Name = "Analyte", WavelengthNm = 290m, StandardEntryId = standard.Id,
                        TheoreticalWeightStdMg = 50m, TheoreticalWeightTestMg = 50m, StandardInjections = 5, StandardDilution = 2500m,
                    },
                },
            };
            arrange.HplcMethods.Add(method);
            await arrange.SaveChangesAsync();
            var added = await AddOrderAsyncIn(arrange, c, d =>
            {
                d.WorkflowType = WorkflowType.Dissolution; d.EquationType = EquationType.Dissolution;
                d.RequiresSystemSuitability = true; d.MethodAbbreviation = "DIS"; d.SstMaxRsdPercent = 2m;
                d.DissolutionS1Offset = 5m; d.DissolutionS2MinOffset = 15m; d.DissolutionS3MinOffset = 25m;
                d.DissolutionS3MaxBelowS2Min = 2m; d.ConditionFields = "Medium, RPM";
            }, s =>
            {
                s.LimitType = LimitType.DissolutionQ; s.LowerLimit = 80m; s.UpperLimit = null; s.LabelClaim = 18m; s.LabelClaimUnit = "mg";
            }, EquipmentType.DissolutionTester);
            orderId = added.OrderId;
            equipmentId = added.EquipmentId!.Value;
            HplcDissolutionStandardTests.SeedRun(arrange, orderId, sectionId: c.SectionId, equipmentId: hplc.Id,
                columnId: column.Id, methodId: method.Id, userId: c.UserId, firstAnalyteId: method.Analytes[0].Id, codeSuffix: uid);
        }

        await using (var act = fixture.CreateDbContext())
            await Engine(act).RecordDissolutionResultAsync(orderId, new(DateTime.UtcNow, equipmentId,
                new Dictionary<string, string> { ["Medium"] = "0.01M HCl 900mL", ["RPM"] = "50" },
                900m, 1m, [0.45m, 0.45m, 0.45m, 0.45m, 0.45m, 0.45m], Password, "stage 1"), c.UserId);

        await AssertCapturedAsync(orderId, c.Sop);
    }

    // The order arranged on a context the caller owns (the dissolution run needs the order id in the same arrangement).
    private async Task<Added> AddOrderAsyncIn(MicroLimsDbContext db, Ctx c, Action<TestDefinition> definition,
        Action<Specification>? spec, EquipmentType? equipment, Action<TestOrder>? order = null)
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var def = new TestDefinition
        {
            Code = $"SC_{uid}", DisplayName = $"SOP capture {uid}", SectionId = c.SectionId, IsActive = true,
            PhyschemArea = PhyschemArea.Both, ReplicateCount = 1, EvaluationBasis = MeasurementEvaluationBasis.Mean,
        };
        definition(def);
        db.TestDefinitions.Add(def);
        var itemId = await db.Samples.Where(x => x.Id == c.SampleId).Select(x => x.ItemId!.Value).SingleAsync();
        db.SampleTests.Add(new SampleTest { ItemId = itemId, TestCode = def.Code, DisplayName = def.DisplayName });
        var sp = new Specification
        {
            ItemId = itemId, TestCode = def.Code, ParameterName = "Parameter", DisplayOrder = 1,
            LimitType = LimitType.Range, LowerLimit = 1m, UpperLimit = 10m, Unit = "%", ResultBasis = ResultBasis.PercentAsIs,
        };
        spec?.Invoke(sp);
        db.Specifications.Add(sp);
        Equipment? eq = null;
        if (equipment is not null)
        {
            eq = new Equipment { Name = $"Instrument {uid}", Code = $"EQ-{uid}", Type = equipment.Value, SectionId = c.SectionId };
            db.Equipment.Add(eq);
        }
        var testOrder = new TestOrder
        {
            SampleId = c.SampleId, TestCode = def.Code, SectionId = c.SectionId,
            CurrentStep = WorkflowStep.Running, Status = ApprovalStatus.InProgress,
        };
        order?.Invoke(testOrder);
        db.TestOrders.Add(testOrder);
        await db.SaveChangesAsync();
        return new Added(testOrder.Id, sp.Id, eq?.Id, def.Code);
    }

    [PostgresFact]
    public async Task Outsourced_result_captures_the_revision()
    {
        var c = await NewScenarioAsync();
        var a = await AddOrderAsync(c, d => { d.WorkflowType = WorkflowType.Measurement; d.EquationType = EquationType.Measurement; },
            order: o =>
            {
                o.OutsourcedToLab = "Acme Analytical"; o.OutsourcedAt = DateTime.UtcNow; o.OutsourcedByUserId = fixture.SeededControllerUserId;
            });
        int reportId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var report = new SampleDocument
            {
                SampleId = c.SampleId, TestOrderId = a.OrderId, DocumentType = SampleDocumentType.ExternalLabReport,
                OriginalFileName = "ExternalReport.pdf", StorageKey = $"sample-documents/{c.SampleId}/test_{Guid.NewGuid():N}.pdf",
                FileExtension = ".pdf", ContentType = "application/pdf", FileSizeBytes = 1024,
                ContentSha256 = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855",
                UploadedByUserId = c.UserId, UploadedAt = DateTime.UtcNow, Status = SampleDocumentStatus.Current,
            };
            arrange.SampleDocuments.Add(report);
            await arrange.SaveChangesAsync();
            reportId = report.Id;
        }

        await using (var act = fixture.CreateDbContext())
            await Engine(act).RecordOutsourcedResultAsync(a.OrderId,
                new(DateTime.UtcNow, "EXT-1001", reportId, [new(a.SpecId, 5m, null, null)], Password), c.UserId);

        await AssertCapturedAsync(a.OrderId, c.Sop);
    }

    // ------------------------------------------------------------------ count, pathogen and preparation steps

    private sealed record Count(int OrderId, string StepName, int MediaId, int IncubatorId);

    // A count test of the scenario's sample whose preparation is confirmed, ready for its media selection.
    private async Task<Count> AddCountOrderAsync(Ctx c)
    {
        await using var db = fixture.CreateDbContext();
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var definition = new TestDefinition { Code = $"CT_{uid}", DisplayName = $"Count {uid}", WorkflowType = WorkflowType.CountTest, SectionId = c.SectionId };
        db.TestDefinitions.Add(definition);
        await db.SaveChangesAsync();
        var step = new TestWorkflowStep
        {
            TestDefinitionId = definition.Id, StepOrder = 1, StepName = "CountIncubation", IncubationMinHours = 72, IncubationMaxHours = 120,
            TemperatureMin = 30, TemperatureMax = 35, IsFinalStep = true, StepType = StepType.PlateCount,
        };
        db.TestWorkflowSteps.Add(step);
        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia", BatchNumber = $"LOT-{uid}",
            ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = $"TSA{uid}", Location = "Micro Lab", QuantityReceived = 500,
            QuantityRemaining = 500, Unit = MaterialUnit.Gram, SectionId = c.SectionId,
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();
        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia
        {
            TestWorkflowStepId = step.Id, MediaProductId = MediaProductTestData.ProductIdFor(db, material.Id),
            TempMin = 30, TempMax = 35, IncubationMinHours = 72, IncubationMaxHours = 120,
        });
        var media = new Media { MaterialId = material.Id, LotNumber = $"TSA/{uid}", IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        var incubator = new Equipment { Name = "Incubator", Code = $"INC-{uid}", Type = EquipmentType.Incubator, SetPointTemperature = 32, SectionId = c.SectionId };
        db.AddRange(media, incubator);
        var itemId = await db.Samples.Where(s => s.Id == c.SampleId).Select(s => s.ItemId!.Value).SingleAsync();
        db.Specifications.Add(new Specification { ItemId = itemId, TestCode = definition.Code, DilutionFactor = 1m, Unit = "CFU/g", AlertLimit = "", ActionLimit = "", SpecLimit = "" });
        await ConfirmPreparationAsync(db, c.SampleId);
        var order = new TestOrder
        {
            SampleId = c.SampleId, TestCode = definition.Code, Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, SectionId = c.SectionId,
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();
        return new Count(order.Id, "CountIncubation", media.Id, incubator.Id);
    }

    private async Task ConfirmPreparationAsync(MicroLimsDbContext db, int sampleId)
    {
        var sample = await db.Samples.SingleAsync(s => s.Id == sampleId);
        sample.PreparationStatus = SamplePreparationStatus.Ready;
        if (!await db.SamplePreparations.AnyAsync(p => p.SampleId == sampleId))
            db.SamplePreparations.Add(new SamplePreparation
            {
                SampleId = sampleId, Amount = 10, Technique = "PourPlate", Diluent = "Diluent", Neutralizer = "None",
                PreparedByUserId = fixture.SeededUserId, PreparedAt = DateTime.UtcNow,
            });
    }

    [PostgresFact]
    public async Task Media_selection_of_a_count_test_captures_the_revision()
    {
        var c = await NewScenarioAsync();
        var count = await AddCountOrderAsync(c);

        await using (var act = fixture.CreateDbContext())
            await Engine(act).SelectMediaAsync(count.OrderId, count.StepName, count.MediaId, count.IncubatorId, c.UserId);

        await AssertCapturedAsync(count.OrderId, c.Sop);
    }

    [PostgresFact]
    public async Task Count_reading_captures_the_revision()
    {
        var c = await NewScenarioAsync();
        var count = await AddCountOrderAsync(c);
        await using (var arrange = fixture.CreateDbContext())
            await EngineWithoutCapture(arrange).SelectMediaAsync(count.OrderId, count.StepName, count.MediaId, count.IncubatorId, c.UserId);
        await AssertNotCapturedAsync(count.OrderId);

        await using (var act = fixture.CreateDbContext())
            await Engine(act).RecordResultAsync(count.OrderId, count.StepName, new CountTestPayload(["12", "14"], 1), c.UserId);

        await AssertCapturedAsync(count.OrderId, c.Sop);
        await using var assert = fixture.CreateDbContext();
        Assert.True(await assert.CountTestReadings.AsNoTracking().AnyAsync(r => r.TestOrderId == count.OrderId));
    }

    private sealed record Pathogen(int[] OrderIds, int MediaId, int IncubatorId, int MaterialId);

    // The scenario's own order is left alone.
    private async Task<Pathogen> AddPathogenOrdersAsync(Ctx c, int tests = 2, bool confirmatory = false)
    {
        await using var db = fixture.CreateDbContext();
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var product = new MediaProduct { Code = $"P{uid}", Name = $"SOP capture medium {uid}" };
        var media = new Media
        {
            Material = new Material { SectionId = c.SectionId, BatchNumber = uid, MaterialName = product.Name, MediaProduct = product },
            LotNumber = $"SC-{uid}", IsReleasedForUse = true, ApprovalStatus = ApprovalGateStatus.Approved,
            PreparedAt = DateTime.UtcNow.AddDays(-1), ExpiryDate = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PreparedByUserId = c.UserId,
        };
        var incubator = new Equipment
        {
            Type = EquipmentType.Incubator, SectionId = c.SectionId, Code = $"SC-INC-{uid}", Name = $"Incubator {uid}", SetPointTemperature = 32,
        };
        db.AddRange(media, incubator);
        var orders = new List<TestOrder>();
        for (var i = 0; i < tests; i++)
        {
            var definition = new TestDefinition
            {
                Code = $"PA{i}_{uid}", DisplayName = $"Pathogen {i}", SectionId = c.SectionId, WorkflowType = WorkflowType.Observation,
                Steps =
                [
                    new TestWorkflowStep
                    {
                        StepName = confirmatory ? "Confirmatory Plating" : "Broth Enrichment",
                        StepOrder = 1,
                        StepType = confirmatory ? StepType.ConfirmatoryPlating : StepType.BrothEnrichment,
                        StepMedia =
                        {
                            new TestWorkflowStepMedia
                            {
                                MediaProduct = product, IsRequired = true, IncubationMinHours = 18, IncubationMaxHours = 24, TempMin = 30, TempMax = 35,
                            },
                        },
                    },
                ],
            };
            db.TestDefinitions.Add(definition);
            var order = new TestOrder
            {
                SampleId = c.SampleId, TestCode = definition.Code, SectionId = c.SectionId,
                Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting,
            };
            db.TestOrders.Add(order);
            orders.Add(order);
        }
        await ConfirmPreparationAsync(db, c.SampleId);
        await db.SaveChangesAsync();
        return new Pathogen(orders.Select(o => o.Id).ToArray(), media.Id, incubator.Id, media.MaterialId);
    }

    [PostgresFact]
    public async Task Pathogen_step_captures_the_revision()
    {
        var c = await NewScenarioAsync();
        var p = await AddPathogenOrdersAsync(c, 1);

        await using (var act = fixture.CreateDbContext())
            await Engine(act).SelectMediaAsync(p.OrderIds[0], "Broth Enrichment", p.MediaId, p.IncubatorId, c.UserId);

        await AssertCapturedAsync(p.OrderIds[0], c.Sop);
    }

    [PostgresFact]
    public async Task Shared_enrichment_start_captures_every_order_it_writes_for()
    {
        var c = await NewScenarioAsync();
        var p = await AddPathogenOrdersAsync(c, 2);

        await using (var act = fixture.CreateDbContext())
            await Session(act).StartSharedTsbAsync(c.SampleId, new(p.MediaId, p.IncubatorId, DateTime.UtcNow), c.UserId);

        foreach (var orderId in p.OrderIds)
            await AssertCapturedAsync(orderId, c.Sop);
        await using var assert = fixture.CreateDbContext();
        Assert.Equal(p.OrderIds.Length, await assert.Incubations.AsNoTracking().CountAsync(i => i.TestOrderId != null && p.OrderIds.Contains(i.TestOrderId.Value)));
    }

    private async Task<(int LocationId, int? ObservationId)> AddPathogenLocationAsync(
        Ctx c, int orderId, bool withGrowth = false)
    {
        await using var arrange = fixture.CreateDbContext();
        var location = new SampleLocation { SampleId = c.SampleId, TestOrderId = orderId };
        arrange.SampleLocations.Add(location);
        LocationPathogenObservation? observation = null;
        if (withGrowth)
        {
            observation = new LocationPathogenObservation
            {
                SampleLocation = location, TestOrderId = orderId,
                GrowthObservation = GrowthObservation.GrowthConforming, ObservedByUserId = c.UserId,
                ObservedAt = CapturedAt.UtcDateTime.AddHours(-24),
            };
            arrange.LocationPathogenObservations.Add(observation);
        }
        await arrange.SaveChangesAsync();
        return (location.Id, observation?.Id);
    }

    private Task<Added> AddObservationOrderAsync(Ctx c) =>
        AddOrderAsync(c, d => d.WorkflowType = WorkflowType.Observation);

    private async Task AssertPathogenCaptureAsync(Ctx c, int capturedId, params int[] uncapturedIds)
    {
        await AssertCapturedAsync(capturedId, c.Sop);
        foreach (var id in uncapturedIds)
            await AssertNotCapturedAsync(id);

        await using var assert = fixture.CreateDbContext();
        var orderId = capturedId.ToString();
        var updates = await assert.AuditLogs.AsNoTracking()
            .Where(log => log.EntityName == nameof(TestOrder) && log.EntityId == orderId && log.Action == "Update")
            .ToListAsync();
        var audit = Assert.Single(updates, IsCaptureTransition);
        Assert.Equal(c.UserId, audit.UserId);
        using var values = JsonDocument.Parse(audit.NewValue!);
        Assert.Equal(c.Sop.RevisionId, values.RootElement.GetProperty(nameof(TestOrder.SopRevisionId)).GetInt32());
    }

    [PostgresFact]
    public async Task Primary_observations_capture_only_valid_touched_orders_allowed_by_competency()
    {
        var c = await WithOwnSectionAsync(await NewScenarioAsync());
        var allowed = await AddObservationOrderAsync(c);
        var refused = await AddObservationOrderAsync(c);
        var invalid = await AddObservationOrderAsync(c);
        var (allowedLocation, _) = await AddPathogenLocationAsync(c, allowed.OrderId);
        var (refusedLocation, _) = await AddPathogenLocationAsync(c, refused.OrderId);
        await EnableGateAsync(c.SectionId);
        await QualifyAsync(c.UserId, allowed.TestCode, c.Sop);

        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = c.UserId;
            var response = await Session(act).SavePrimaryObservationsAsync(c.SampleId, new([
                new(allowedLocation, allowed.TestCode, GrowthObservation.NoGrowth),
                new(refusedLocation, refused.TestCode, GrowthObservation.NoGrowth),
                new(0, invalid.TestCode, GrowthObservation.NoGrowth),
            ]), c.UserId);
            Assert.Equal(refused.OrderId, Assert.Single(response.Skipped!).TestOrderId);
        }

        await AssertPathogenCaptureAsync(c, allowed.OrderId, refused.OrderId, invalid.OrderId, c.S.Order.Id);
        await using var assert = fixture.CreateDbContext();
        var observation = Assert.Single(await assert.LocationPathogenObservations.AsNoTracking()
            .Where(o => o.SampleLocationId == allowedLocation || o.SampleLocationId == refusedLocation).ToListAsync());
        Assert.Equal(allowed.OrderId, observation.TestOrderId);
        Assert.Equal(allowedLocation, observation.SampleLocationId);
        Assert.Equal(GrowthObservation.NoGrowth, observation.GrowthObservation);
        Assert.Equal(c.UserId, observation.ObservedByUserId);
        Assert.Equal(CapturedAt.UtcDateTime, observation.ObservedAt);
    }

    [PostgresFact]
    public async Task Shared_confirmatory_setup_captures_only_the_order_started()
    {
        var c = await NewScenarioAsync();
        var p = await AddPathogenOrdersAsync(c, confirmatory: true);
        var (location, _) = await AddPathogenLocationAsync(c, p.OrderIds[0], withGrowth: true);
        await AddPathogenLocationAsync(c, p.OrderIds[1], withGrowth: true);

        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = c.UserId;
            await Session(act).StartSharedConfirmatorySetupAsync(c.SampleId,
                new(p.OrderIds[0], [location], [p.MaterialId], [p.MediaId], p.IncubatorId, null), c.UserId);
        }

        await AssertPathogenCaptureAsync(c, p.OrderIds[0], p.OrderIds[1], c.S.Order.Id);
        await using var assert = fixture.CreateDbContext();
        var incubation = await assert.Incubations.AsNoTracking()
            .SingleAsync(i => i.TestOrderId == p.OrderIds[0] || i.TestOrderId == p.OrderIds[1]);
        Assert.Equal(p.OrderIds[0], incubation.TestOrderId);
        Assert.Equal(p.MediaId, incubation.MediaId);
        Assert.Equal(p.IncubatorId, incubation.IncubatorEquipmentId);
        Assert.Equal(c.UserId, incubation.StartedByUserId);
        Assert.Equal(CapturedAt.UtcDateTime, incubation.IncubationStartUtc);
        Assert.Equal(WorkflowStep.Incubating, await assert.TestOrders.AsNoTracking()
            .Where(o => o.Id == p.OrderIds[0]).Select(o => o.CurrentStep).SingleAsync());
    }

    [PostgresFact]
    public async Task Confirmatory_readings_capture_only_orders_with_submitted_plates()
    {
        var c = await NewScenarioAsync();
        var p = await AddPathogenOrdersAsync(c, confirmatory: true);
        var (location, observation) = await AddPathogenLocationAsync(c, p.OrderIds[0], withGrowth: true);
        var (untouchedLocation, _) = await AddPathogenLocationAsync(c, p.OrderIds[1], withGrowth: true);

        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = c.UserId;
            await Session(act).SaveBatchConfirmatoryPlateReadingsAsync(c.SampleId,
                new([new(observation!.Value, 0, p.MaterialId, GrowthObservation.NoGrowth)]), c.UserId);
        }

        await AssertPathogenCaptureAsync(c, p.OrderIds[0], p.OrderIds[1], c.S.Order.Id);
        await using var assert = fixture.CreateDbContext();
        var plate = await assert.ConfirmatoryPlateObservations.AsNoTracking()
            .SingleAsync(o => o.SampleLocationId == location || o.SampleLocationId == untouchedLocation);
        Assert.Equal(observation, plate.LocationPathogenObservationId);
        Assert.Equal(location, plate.SampleLocationId);
        Assert.Equal(p.MaterialId, plate.MaterialId);
        Assert.Equal(GrowthObservation.NoGrowth, plate.Observation);
        Assert.Equal(c.UserId, plate.RecordedByUserId);
        Assert.Equal(CapturedAt.UtcDateTime, plate.RecordedAtUtc);
    }

    [PostgresFact]
    public async Task Result_matrix_captures_only_orders_with_valid_submitted_cells()
    {
        var c = await NewScenarioAsync();
        var entered = await AddObservationOrderAsync(c);
        var invalid = await AddObservationOrderAsync(c);
        var (location, _) = await AddPathogenLocationAsync(c, entered.OrderId);

        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = c.UserId;
            await Session(act).SaveResultMatrixAsync(c.SampleId, new([
                new(location, entered.TestCode, "DETECTED", "Detected (+)", null, "Qualitative"),
                new(0, invalid.TestCode, "DETECTED", "Detected (+)", null, "Qualitative"),
            ]), c.UserId);
        }

        await AssertPathogenCaptureAsync(c, entered.OrderId, invalid.OrderId, c.S.Order.Id);
        await using var assert = fixture.CreateDbContext();
        var result = await assert.SampleLocations.AsNoTracking().SingleAsync(l => l.Id == location);
        Assert.Equal(ResultStatus.Detected, result.Status);
        Assert.Equal("Detected (+)", result.ReportedResult);
        Assert.Equal(c.UserId, result.EnteredByUserId);
        Assert.Equal(CapturedAt.UtcDateTime, result.EnteredAt);
    }

    [PostgresFact]
    public async Task Complete_session_captures_only_eligible_orders_after_results_are_complete()
    {
        var c = await NewScenarioAsync();
        var eligible = await AddObservationOrderAsync(c);
        var (location, _) = await AddPathogenLocationAsync(c, eligible.OrderId);
        await using (var arrange = fixture.CreateDbContext())
        {
            var closed = await arrange.TestOrders.SingleAsync(o => o.Id == c.S.Order.Id);
            closed.Status = ApprovalStatus.Cancelled;
            await arrange.SaveChangesAsync();
        }

        await using (var act = fixture.CreateDbContext())
        {
            var rejected = await Assert.ThrowsAsync<WorkflowStepException>(
                () => Session(act).CompleteSessionAsync(c.SampleId, c.UserId));
            Assert.Equal("IncompleteResults", rejected.ErrorCode);
        }
        await AssertNotCapturedAsync(eligible.OrderId);

        await using (var arrange = fixture.CreateDbContext())
        {
            var result = await arrange.SampleLocations.SingleAsync(l => l.Id == location);
            result.Status = ResultStatus.Absent;
            result.ReportedResult = "Not Detected (-)";
            result.EnteredByUserId = c.UserId;
            result.EnteredAt = CapturedAt.UtcDateTime.AddHours(-1);
            await arrange.SaveChangesAsync();
        }

        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = c.UserId;
            await Session(act).CompleteSessionAsync(c.SampleId, c.UserId);
        }

        await AssertPathogenCaptureAsync(c, eligible.OrderId, c.S.Order.Id);
        await using var assert = fixture.CreateDbContext();
        var orders = await assert.TestOrders.AsNoTracking().Where(o => o.SampleId == c.SampleId).ToDictionaryAsync(o => o.Id);
        Assert.Equal(ApprovalStatus.ResultEntered, orders[eligible.OrderId].Status);
        Assert.Equal(WorkflowStep.Ready, orders[eligible.OrderId].CurrentStep);
        Assert.Equal(ApprovalStatus.Cancelled, orders[c.S.Order.Id].Status);
        var history = await assert.WorkflowHistories.AsNoTracking()
            .SingleAsync(h => h.TestOrderId == eligible.OrderId && h.ToStep == WorkflowStep.Ready);
        Assert.Equal(c.UserId, history.PerformedByUserId);
        Assert.Equal(CapturedAt.UtcDateTime, history.Timestamp);
        Assert.Equal(SampleStatus.UnderReview, await assert.Samples.AsNoTracking()
            .Where(s => s.Id == c.SampleId).Select(s => s.Status).SingleAsync());
    }

    [PostgresFact]
    public async Task Preparation_start_captures_every_order_it_prepares()
    {
        var c = await NewScenarioAsync();
        int firstId, secondId, sampleId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var section = new DocumentSection
            {
                Code = $"PR{Guid.NewGuid():N}"[..10], Name = $"SOP capture preparation lab {Guid.NewGuid():N}", DepartmentId = fixture.SeededDepartmentId, IsActive = true,
            };
            arrange.DocumentSections.Add(section);
            await arrange.SaveChangesAsync();
            arrange.UserOrgMemberships.Add(new UserOrgMembership { UserId = c.UserId, SectionId = section.Id, DepartmentId = fixture.SeededDepartmentId });
            var source = await arrange.Samples.AsNoTracking().SingleAsync(s => s.Id == c.SampleId);
            var sample = new Sample
            {
                ReferenceNumber = $"PREP-{Guid.NewGuid():N}"[..14], Category = SampleCategory.FinishedProduct, ItemId = source.ItemId,
                CauseOfTestingId = source.CauseOfTestingId, ReceivedByUserId = c.UserId, Status = SampleStatus.Received,
                PreparationStatus = SamplePreparationStatus.NeedsPreparation,
            };
            TestOrder Order(string tag) => new()
            {
                Sample = sample, TestCode = $"PRT-{tag}-{Guid.NewGuid():N}"[..14], SectionId = section.Id,
                Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting,
            };
            var first = Order("A");
            var second = Order("B");
            arrange.TestOrders.AddRange(first, second);
            arrange.ItemPreparationConfigurations.Add(new ItemPreparationConfiguration
            {
                ItemId = source.ItemId!.Value, Amount = 1, Technique = "PourPlate", Diluent = "Configured diluent",
                Neutralizer = "Configured neutralizer", CreatedByUserId = c.UserId,
            });
            await arrange.SaveChangesAsync();
            (firstId, secondId, sampleId) = (first.Id, second.Id, sample.Id);
        }

        await using (var act = fixture.CreateDbContext())
            await TestServiceFactory.SamplePreparation(act, new FixedTimeProvider(CapturedAt))
                .ConfirmFromConfigurationAsync(new(sampleId, c.UserId, Password));

        await AssertCapturedAsync(firstId, c.Sop);
        await AssertCapturedAsync(secondId, c.Sop);
        await using var assert = fixture.CreateDbContext();
        Assert.Equal(SamplePreparationStatus.Ready, await assert.Samples.AsNoTracking().Where(s => s.Id == sampleId).Select(s => s.PreparationStatus).SingleAsync());
    }

    // ------------------------------------------------------------------ location based recorders

    private sealed record Located(int OrderId, int[] LocationIds);

    // A water-chemistry test with one location per sampling point. Water samples carry no item of their own, so the
    // sample is given the scenario's item here: the point under test is where the recorder calls the capture.
    private async Task<Located> AddLocationOrderAsync(Ctx c)
    {
        await using var db = fixture.CreateDbContext();
        var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var department = new WaterDepartment { Name = $"Chemistry {uid}" };
        db.WaterDepartments.Add(department);
        await db.SaveChangesAsync();
        var points = new[]
        {
            new WaterSamplingPoint { Code = $"W1-{uid}", Location = "Feed", WaterDepartmentId = department.Id },
            new WaterSamplingPoint { Code = $"W2-{uid}", Location = "Return", WaterDepartmentId = department.Id },
        };
        db.WaterSamplingPoints.AddRange(points);
        var definition = new TestDefinition { Code = $"PH_{uid}", DisplayName = $"pH {uid}", WorkflowType = WorkflowType.Measurement, SectionId = c.SectionId };
        db.TestDefinitions.Add(definition);
        await db.SaveChangesAsync();
        var configs = points.Select(p => new SamplingConfiguration
        {
            WaterSamplingPointId = p.Id, TestCode = definition.Code, LimitType = LimitType.Range, LowerLimit = 6.5m, UpperLimit = 8.5m, Unit = "pH",
        }).ToArray();
        db.SamplingConfigurations.AddRange(configs);
        var source = await db.Samples.AsNoTracking().SingleAsync(s => s.Id == c.SampleId);
        var sample = new Sample
        {
            ReferenceNumber = $"WL-{uid}", Category = SampleCategory.Water, WaterDepartmentId = department.Id, ItemId = source.ItemId,
            CauseOfTestingId = source.CauseOfTestingId, ReceivedByUserId = c.UserId, Status = SampleStatus.InTesting, LabSectionId = c.SectionId,
        };
        var order = new TestOrder
        {
            Sample = sample, TestCode = definition.Code, SectionId = c.SectionId, CurrentStep = WorkflowStep.Waiting, Status = ApprovalStatus.InProgress,
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();
        var locations = points.Select((p, i) => new SampleLocation
        {
            SampleId = sample.Id, TestOrderId = order.Id, LocationType = LocationType.WaterSamplingPoint,
            WaterSamplingPointId = p.Id, SamplingConfigurationId = configs[i].Id,
        }).ToList();
        db.SampleLocations.AddRange(locations);
        await db.SaveChangesAsync();
        return new Located(order.Id, locations.Select(l => l.Id).ToArray());
    }

    [PostgresFact]
    public async Task Location_results_capture_the_revision()
    {
        var c = await NewScenarioAsync();
        var located = await AddLocationOrderAsync(c);

        await using (var act = fixture.CreateDbContext())
            await Engine(act).RecordLocationResultsAsync(located.OrderId,
                [new(located.LocationIds[0], 7.2m, "7.2"), new(located.LocationIds[1], 7.4m, "7.4")], c.UserId);

        await AssertCapturedAsync(located.OrderId, c.Sop);
        await using var assert = fixture.CreateDbContext();
        Assert.All(await assert.SampleLocations.AsNoTracking().Where(l => l.TestOrderId == located.OrderId).ToListAsync(),
            l => Assert.Equal(ResultStatus.WithinLimits, l.Status));
    }

    // A prepared water count test whose media is selected, ready for its batch readings.
    private async Task<int> AddWaterBatchOrderAsync(Ctx c)
    {
        int orderId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var uid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var department = new WaterDepartment { Name = $"WTU {uid}" };
            arrange.WaterDepartments.Add(department);
            await arrange.SaveChangesAsync();
            var code = $"TW_{uid}";
            var pointA = new WaterSamplingPoint { Code = $"SPA-{uid}", WaterDepartmentId = department.Id, AssignedTestCodes = [code] };
            var pointB = new WaterSamplingPoint { Code = $"SPB-{uid}", WaterDepartmentId = department.Id, AssignedTestCodes = [code] };
            arrange.WaterSamplingPoints.AddRange(pointA, pointB);
            await arrange.SaveChangesAsync();
            arrange.SamplingConfigurations.AddRange(
                new SamplingConfiguration { WaterSamplingPointId = pointA.Id, TestCode = code, AlertLimit = "10", ActionLimit = "50", SpecLimit = "100" },
                new SamplingConfiguration { WaterSamplingPointId = pointB.Id, TestCode = code, AlertLimit = "10", ActionLimit = "50", SpecLimit = "100" });
            var micro = await arrange.DocumentSections.AsNoTracking().FirstAsync(s => s.Code == "MICRO");
            var definition = new TestDefinition { Code = code, DisplayName = code, WorkflowType = WorkflowType.CountTest, SectionId = micro.Id };
            arrange.TestDefinitions.Add(definition);
            await arrange.SaveChangesAsync();
            var step = new TestWorkflowStep
            {
                TestDefinitionId = definition.Id, StepOrder = 1, StepName = "CountIncubation", IncubationMinHours = 0, IncubationMaxHours = 24,
                TemperatureMin = 20, TemperatureMax = 25, IsFinalStep = true,
            };
            arrange.TestWorkflowSteps.Add(step);
            var material = new Material
            {
                MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia", BatchNumber = $"LOT-{uid}",
                ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = $"TSAW{uid}", Location = "Micro Lab", QuantityReceived = 500,
                QuantityRemaining = 500, Unit = MaterialUnit.Gram, SectionId = micro.Id,
            };
            arrange.Materials.Add(material);
            await arrange.SaveChangesAsync();
            arrange.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia
            {
                TestWorkflowStepId = step.Id, MediaProductId = MediaProductTestData.ProductIdFor(arrange, material.Id),
                TempMin = 20, TempMax = 25, IncubationMinHours = 18, IncubationMaxHours = 24,
            });
            var media = new Media { MaterialId = material.Id, LotNumber = $"TSAW/{uid}", IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
            var incubator = new Equipment { Name = "Incubator Water", Code = $"INCW-{uid}", Type = EquipmentType.Incubator, SetPointTemperature = 22, SectionId = micro.Id };
            arrange.AddRange(media, incubator);
            await arrange.SaveChangesAsync();

            var water = new WaterWorkflowEngine(arrange, new ReferenceNumberGenerator(arrange));
            var sample = await water.ReceiveAsync(new WaterReceiveRequest(department.Id, (await arrange.Samples.AsNoTracking().SingleAsync(s => s.Id == c.SampleId)).CauseOfTestingId, "500ml", "Analyst", $"CTRL-{uid}", c.UserId));
            var prepared = await water.PrepareAsync(sample.Id, [pointA.Id, pointB.Id], c.UserId, "RoomTemperature");
            orderId = prepared.TestOrders.Single().Id;
            var source = await arrange.Samples.AsNoTracking().SingleAsync(s => s.Id == c.SampleId);
            await EngineWithoutCapture(arrange).SelectMediaAsync(orderId, "CountIncubation", media.Id, incubator.Id, c.UserId);
            await IncubationTestClock.ElapseOpenIncubationsAsync(arrange, orderId);
            var stored = await arrange.Samples.SingleAsync(s => s.Id == sample.Id);
            stored.ItemId = source.ItemId;
            await arrange.SaveChangesAsync();
        }
        return orderId;
    }

    [PostgresFact]
    public async Task Water_batch_readings_capture_the_revision()
    {
        var c = await NewScenarioAsync();
        var orderId = await AddWaterBatchOrderAsync(c);
        await AssertNotCapturedAsync(orderId);
        List<int> locationIds;
        await using (var read = fixture.CreateDbContext())
            locationIds = await read.SampleLocations.AsNoTracking().Where(l => l.TestOrderId == orderId).Select(l => l.Id).ToListAsync();

        await using (var act = fixture.CreateDbContext())
            await Engine(act).RecordWaterBatchReadingsAsync(orderId,
                [new(locationIds[0], [12m, 14m]), new(locationIds[1], [5m, 5m])], c.UserId);

        await AssertCapturedAsync(orderId, c.Sop);
        await using var assert = fixture.CreateDbContext();
        Assert.All(await assert.SampleLocations.AsNoTracking().Where(l => l.TestOrderId == orderId).ToListAsync(),
            l => Assert.NotNull(l.CalculatedResult));
    }

    // ------------------------------------------------------------------ the second step, the audit trail, statuses

    [PostgresFact]
    public async Task Second_step_does_not_recapture()
    {
        var c = await NewScenarioAsync();
        var count = await AddCountOrderAsync(c);
        var laterAt = CapturedAt.AddDays(3);
        await using (var act = fixture.CreateDbContext())
            await Engine(act).SelectMediaAsync(count.OrderId, count.StepName, count.MediaId, count.IncubatorId, c.UserId);
        await AssertCapturedAsync(count.OrderId, c.Sop);

        await PublishRevisionAsync(c.Sop, 4);
        await using (var act = fixture.CreateDbContext())
            await TestServiceFactory.TestWorkflow(act, sopCapture: Capture(act, laterAt))
                .RecordResultAsync(count.OrderId, count.StepName, new CountTestPayload(["12", "14"], 1), c.UserId);

        await AssertCapturedAsync(count.OrderId, c.Sop, revision: "3");
        await using var assert = fixture.CreateDbContext();
        Assert.True(await assert.CountTestReadings.AsNoTracking().AnyAsync(r => r.TestOrderId == count.OrderId));
        var current = await assert.DocumentMasters.AsNoTracking().Where(m => m.Id == c.Sop.MasterId)
            .Select(m => m.CurrentEffectiveRevision!.RevisionNumber).SingleAsync();
        Assert.Equal("4", current);
    }

    [PostgresFact]
    public async Task Audit_row_of_first_step_contains_captured_values()
    {
        var c = await NewScenarioAsync();
        var a = await AddOrderAsync(c, d => { d.WorkflowType = WorkflowType.Measurement; d.EquationType = EquationType.Measurement; });

        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = c.UserId;
            await Engine(act).RecordMeasurementResultAsync(a.OrderId, new(DateTime.UtcNow, null, [new(a.SpecId, [5m])], Password), c.UserId);
        }

        await using var assert = fixture.CreateDbContext();
        var id = a.OrderId.ToString();
        // every update of the order carries its full row, so the first step's row is the one that takes the capture from empty
        var rows = (await assert.AuditLogs.AsNoTracking()
            .Where(x => x.EntityName == nameof(TestOrder) && x.EntityId == id && x.Action == "Update").ToListAsync())
            .Where(IsCaptureTransition).ToList();
        var audit = Assert.Single(rows);
        Assert.Equal(c.UserId, audit.UserId);
        using var newValues = JsonDocument.Parse(audit.NewValue!);
        var root = newValues.RootElement;
        Assert.Equal((int)SopCaptureStatus.Captured, root.GetProperty("SopCaptureStatus").GetInt32());
        Assert.Equal(c.Sop.Code, root.GetProperty("SopDocumentCode").GetString());
        Assert.Equal(c.Sop.Code, root.GetProperty("SopDocumentTitle").GetString());
        Assert.Equal(c.Sop.MasterId, root.GetProperty("SopDocumentMasterId").GetInt32());
        Assert.Equal(c.Sop.RevisionId, root.GetProperty("SopRevisionId").GetInt32());
        Assert.Equal("3", root.GetProperty("SopRevisionNumber").GetString());
        Assert.True(root.TryGetProperty("SopEffectiveDateUtc", out _));
        Assert.True(root.TryGetProperty("SopCapturedAtUtc", out _));
    }

    private static bool IsCaptureTransition(AuditLog audit)
    {
        static bool Captured(string? json)
        {
            if (json is null) return false;
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("SopCaptureStatus", out var status) && status.ValueKind != JsonValueKind.Null;
        }
        return Captured(audit.NewValue) && !Captured(audit.PreviousValue);
    }

    [PostgresFact]
    public async Task Each_resolver_status_is_stored_and_never_blocks_the_recording()
    {
        var cases = new (string Name, SopCaptureStatus Status)[]
        {
            ("blank", SopCaptureStatus.NoSopNumber),
            ("missing", SopCaptureStatus.NotFound),
            ("draft", SopCaptureStatus.NoEffectiveRevision),
        };
        await PostgresCases.RunAsync(cases, async scenario =>
        {
            var c = await NewScenarioAsync(link: false);
            await using (var arrange = fixture.CreateDbContext())
            {
                var itemId = await arrange.Samples.Where(s => s.Id == c.SampleId).Select(s => s.ItemId!.Value).SingleAsync();
                var item = await arrange.Items.SingleAsync(i => i.Id == itemId);
                var code = NewCode();
                item.SopNumber = scenario.Name == "blank" ? "   " : code;
                await arrange.SaveChangesAsync();
                if (scenario.Name == "draft")
                    await AddRevisionAsync(arrange, fixture, await AddMasterAsync(arrange, fixture, code), 1, DocumentRevisionStatus.Draft, current: true);
            }
            var a = await AddOrderAsync(c, d => { d.WorkflowType = WorkflowType.Measurement; d.EquationType = EquationType.Measurement; });

            await using (var act = fixture.CreateDbContext())
                await Engine(act).RecordMeasurementResultAsync(a.OrderId, new(DateTime.UtcNow, null, [new(a.SpecId, [5m])], Password), c.UserId);

            await using var assert = fixture.CreateDbContext();
            var stored = await assert.TestOrders.AsNoTracking().SingleAsync(o => o.Id == a.OrderId);
            Assert.Equal(scenario.Status, stored.SopCaptureStatus);
            Assert.Equal(CapturedAt.UtcDateTime, stored.SopCapturedAtUtc);
            Assert.Null(stored.SopDocumentMasterId);
            Assert.Null(stored.SopRevisionId);
            Assert.Null(stored.SopRevisionNumber);
            Assert.Equal(WorkflowStep.Ready, stored.CurrentStep);
            Assert.True(await assert.TestAnalyses.AsNoTracking().AnyAsync(x => x.TestOrderId == a.OrderId && x.IsActive));
        });
    }

    // ------------------------------------------------------------------ the competency gate is unchanged

    private async Task QualifyAsync(int userId, string testCode, Sop sop)
    {
        await using var db = fixture.CreateDbContext();
        var definition = await db.TestDefinitions.AsNoTracking().SingleAsync(d => d.Code == testCode);
        var id = await fixture.CreateSequenceHelper(db).ReserveIdAsync<AnalystQualification>();
        db.AnalystQualifications.Add(new AnalystQualification
        {
            Id = id, UserId = userId, TestDefinitionId = definition.Id, Basis = QualificationBasis.ObservedPractice,
            Evidence = "Observed the method", QualifiedOn = new DateOnly(2026, 1, 1), SignedByUserId = fixture.SeededControllerUserId,
            Signature = new ElectronicSignature
            {
                UserId = fixture.SeededControllerUserId, EntityType = nameof(AnalystQualification), EntityId = id,
                MeaningOfSignature = SignatureMeaning.AnalystQualified,
            },
        });
        db.DocumentTrainingAssignments.Add(new DocumentTrainingAssignment
        {
            DocumentMasterId = sop.MasterId, DocumentRevisionId = sop.RevisionId, AssignedUserId = userId,
            Status = TrainingAssignmentStatus.Acknowledged, DueDateUtc = CapturedAt.UtcDateTime, AssignedDateUtc = CapturedAt.UtcDateTime.AddDays(-1),
            CreatedAtUtc = CapturedAt.UtcDateTime.AddDays(-1), CreatedByUserId = fixture.SeededControllerUserId,
        });
        await db.SaveChangesAsync();
    }

    private async Task EnableGateAsync(int sectionId)
    {
        await using var db = fixture.CreateDbContext();
        var existing = await db.CompetencySectionSettings.SingleOrDefaultAsync(s => s.SectionId == sectionId);
        if (existing is null)
            db.CompetencySectionSettings.Add(new CompetencySectionSetting { SectionId = sectionId, GateEnabled = true });
        else
            existing.GateEnabled = true;
        await db.SaveChangesAsync();
    }

    [PostgresFact]
    public async Task Competency_refusal_captures_nothing_and_a_qualified_recording_captures()
    {
        var c = await WithOwnSectionAsync(await NewScenarioAsync());
        var a = await AddOrderAsync(c, d => { d.WorkflowType = WorkflowType.Measurement; d.EquationType = EquationType.Measurement; });
        await EnableGateAsync(c.SectionId);

        await using (var act = fixture.CreateDbContext())
        {
            var refused = await Assert.ThrowsAsync<MicroLIMS.Shared.Exceptions.BusinessRuleException>(() => Engine(act)
                .RecordMeasurementResultAsync(a.OrderId, new(DateTime.UtcNow, null, [new(a.SpecId, [5m])], Password), c.UserId));
            Assert.Equal("Competency.NotQualified", refused.Code);
            Assert.StartsWith($"You are not qualified for {a.TestCode}.", refused.Message);
        }
        await AssertNotCapturedAsync(a.OrderId);
        await using (var check = fixture.CreateDbContext())
            Assert.False(await check.TestAnalyses.AsNoTracking().AnyAsync(x => x.TestOrderId == a.OrderId));

        await QualifyAsync(c.UserId, a.TestCode, c.Sop);
        await using (var act = fixture.CreateDbContext())
            await Engine(act).RecordMeasurementResultAsync(a.OrderId, new(DateTime.UtcNow, null, [new(a.SpecId, [5m])], Password), c.UserId);
        await AssertCapturedAsync(a.OrderId, c.Sop);
    }

    [PostgresFact]
    public async Task Shared_enrichment_start_captures_only_the_orders_the_actor_is_qualified_for()
    {
        var c = await WithOwnSectionAsync(await NewScenarioAsync());
        var p = await AddPathogenOrdersAsync(c, 2);
        await EnableGateAsync(c.SectionId);
        string firstCode;
        await using (var read = fixture.CreateDbContext())
            firstCode = await read.TestOrders.AsNoTracking().Where(o => o.Id == p.OrderIds[0]).Select(o => o.TestCode).SingleAsync();
        await QualifyAsync(c.UserId, firstCode, c.Sop);

        await using (var act = fixture.CreateDbContext())
        {
            var response = await Session(act).StartSharedTsbAsync(c.SampleId, new(p.MediaId, p.IncubatorId, DateTime.UtcNow), c.UserId);
            var skip = Assert.Single(response.Skipped!);
            Assert.Equal(p.OrderIds[1], skip.TestOrderId);
        }

        await AssertCapturedAsync(p.OrderIds[0], c.Sop);
        await AssertNotCapturedAsync(p.OrderIds[1]);
    }

    // ------------------------------------------------------------------ a retest captures its own revision

    [PostgresFact]
    public async Task Retest_captures_its_own_revision()
    {
        var scenarios = new ActionGuardScenarios(fixture);
        var section = await scenarios.SectionAsync();
        var analyst = await scenarios.AnalystAsync(section);
        var reviewer = await scenarios.ReviewerAsync(section);
        var head = await scenarios.HeadAsync(section);
        var code = await scenarios.TestDefinitionAsync(section, WorkflowType.Measurement);
        var seed = await scenarios.UnderApprovalAsync(section, reviewer.Id, 1, analyst.Id, code);
        var originOrderId = seed.Orders[0].Id;
        var originSampleId = seed.Sample.Id;

        Sop sop;
        int specId;
        await using (var arrange = fixture.CreateDbContext())
        {
            var definition = await arrange.TestDefinitions.SingleAsync(d => d.Code == code);
            definition.EquationType = EquationType.Measurement;
            definition.ReplicateCount = 1;
            definition.EvaluationBasis = MeasurementEvaluationBasis.Mean;
            var item = new Item { Code = NewCode(), Name = "Retest SOP item", Category = SampleCategory.FinishedProduct, IsActive = true };
            arrange.Items.Add(item);
            await arrange.SaveChangesAsync();
            var spec = new Specification
            {
                ItemId = item.Id, TestCode = code, ParameterName = "Parameter", DisplayOrder = 1, LimitType = LimitType.Range,
                LowerLimit = 1m, UpperLimit = 10m, Unit = "%", ResultBasis = ResultBasis.PercentAsIs,
            };
            arrange.Specifications.Add(spec);
            var sample = await arrange.Samples.SingleAsync(s => s.Id == originSampleId);
            sample.ItemId = item.Id;
            if (!await arrange.CausesOfTesting.AnyAsync(x => x.Name == "Retest"))
                arrange.CausesOfTesting.Add(new CauseOfTesting { Name = "Retest", IsActive = true });
            await arrange.SaveChangesAsync();
            specId = spec.Id;
            sop = await LinkSopAsync(arrange, item.Id, effective: true);
        }
        // the origin's first step captured revision 3 in the past (nothing changes once it is set)
        var originCaptured = new DateTime(2036, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        await scenarios.UpdateAsync<TestOrder>(originOrderId, o =>
        {
            o.SopCaptureStatus = SopCaptureStatus.Captured; o.SopDocumentMasterId = sop.MasterId; o.SopDocumentCode = sop.Code;
            o.SopDocumentTitle = sop.Code; o.SopRevisionId = sop.RevisionId; o.SopRevisionNumber = "3";
            o.SopEffectiveDateUtc = RevisionEffective; o.SopCapturedAtUtc = originCaptured;
        });
        await PublishRevisionAsync(sop, 4);

        await using (var act = fixture.CreateDbContext())
            await TestServiceFactory.SampleApproval(act).DecideAsync(originSampleId, head.Id, ActionGuardScenarios.Password,
                ApprovalDecision.RetestRetainedSample, "Retest", null, selectedTestOrderIds: [originOrderId]);

        int retestSampleId, retestOrderId;
        await using (var read = fixture.CreateDbContext())
        {
            var retest = await read.Samples.AsNoTracking().SingleAsync(s => s.OriginSampleId == originSampleId);
            retestSampleId = retest.Id;
            var order = await read.TestOrders.AsNoTracking().SingleAsync(o => o.SampleId == retest.Id);
            retestOrderId = order.Id;
            // the origin's capture is not copied onto the new order
            Assert.Null(order.SopCaptureStatus);
            Assert.Null(order.SopDocumentMasterId);
            Assert.Null(order.SopRevisionId);
            Assert.Null(order.SopRevisionNumber);
            Assert.Null(order.SopEffectiveDateUtc);
            Assert.Null(order.SopCapturedAtUtc);
        }
        await using (var arrange = fixture.CreateDbContext())
        {
            var retest = await arrange.Samples.SingleAsync(s => s.Id == retestSampleId);
            retest.PreparationStatus = SamplePreparationStatus.Ready;
            arrange.SamplePreparations.Add(new SamplePreparation
            {
                SampleId = retestSampleId, Amount = 1, Technique = "PourPlate", Diluent = "Diluent", Neutralizer = "None",
                PreparedByUserId = analyst.Id, PreparedAt = DateTime.UtcNow,
            });
            var order = await arrange.TestOrders.SingleAsync(o => o.Id == retestOrderId);
            order.CurrentStep = WorkflowStep.Running;
            order.Status = ApprovalStatus.InProgress;
            order.AssignedAnalystId = analyst.Id;
            await arrange.SaveChangesAsync();
            var user = await arrange.Users.SingleAsync(u => u.Id == analyst.Id);
            Assert.NotNull(user.PasswordHash);
        }

        await using (var act = fixture.CreateDbContext())
            await Engine(act).RecordMeasurementResultAsync(retestOrderId,
                new(DateTime.UtcNow, null, [new(specId, [5m])], ActionGuardScenarios.Password), analyst.Id);

        await using var assert = fixture.CreateDbContext();
        var retestOrder = await assert.TestOrders.AsNoTracking().SingleAsync(o => o.Id == retestOrderId);
        Assert.Equal(SopCaptureStatus.Captured, retestOrder.SopCaptureStatus);
        Assert.Equal("4", retestOrder.SopRevisionNumber);
        Assert.Equal(CapturedAt.UtcDateTime, retestOrder.SopCapturedAtUtc);
        Assert.NotEqual(sop.RevisionId, retestOrder.SopRevisionId);
        var origin = await assert.TestOrders.AsNoTracking().SingleAsync(o => o.Id == originOrderId);
        Assert.Equal("3", origin.SopRevisionNumber);
        Assert.Equal(sop.RevisionId, origin.SopRevisionId);
        Assert.Equal(originCaptured, origin.SopCapturedAtUtc);
    }

    // ------------------------------------------------------------------ two first steps at the same time

    [PostgresFact]
    public async Task Concurrent_first_steps_capture_once()
    {
        var c = await NewScenarioAsync();
        var a = await AddOrderAsync(c, d => { d.WorkflowType = WorkflowType.Measurement; d.EquationType = EquationType.Measurement; });
        var firstAt = CapturedAt;
        var secondAt = CapturedAt.AddHours(5);

        await using var first = fixture.CreateDbContext();
        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        var firstOrder = await first.TestOrders.SingleAsync(o => o.Id == a.OrderId);
        await Capture(first, firstAt).CaptureIfNeededAsync(firstOrder);
        await first.SaveChangesAsync();

        // the second writer loaded the order while it was still uncaptured, then tries to capture
        Task second = Task.Run(async () =>
        {
            await using var db = fixture.CreateDbContext();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var order = await db.TestOrders.SingleAsync(o => o.Id == a.OrderId);
            Assert.Null(order.SopCaptureStatus);
            await Capture(db, secondAt).CaptureIfNeededAsync(order);
            order.AssignedAnalystId = c.UserId;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        });

        await WaitUntilBlockedOnALockAsync(second);
        await firstTransaction.CommitAsync();
        await second.WaitAsync(TimeSpan.FromSeconds(30));

        await AssertCapturedAsync(a.OrderId, c.Sop, at: firstAt);
        await using var assert = fixture.CreateDbContext();
        var stored = await assert.TestOrders.AsNoTracking().SingleAsync(o => o.Id == a.OrderId);
        Assert.Equal(c.UserId, stored.AssignedAnalystId);
        var id = a.OrderId.ToString();
        var captures = (await assert.AuditLogs.AsNoTracking()
            .Where(x => x.EntityName == nameof(TestOrder) && x.EntityId == id && x.Action == "Update").ToListAsync())
            .Count(IsCaptureTransition);
        Assert.Equal(1, captures);
    }

    [PostgresFact]
    public async Task Adopting_a_capture_preserves_concurrency_for_overlapping_assignment_changes()
    {
        await PostgresCases.RunAsync(new[] { true, false }, async editBeforeAdoption =>
        {
            var c = await NewScenarioAsync();
            var a = await AddObservationOrderAsync(c);

            await using (var second = fixture.CreateDbContext())
            {
                var stale = await second.TestOrders.SingleAsync(o => o.Id == a.OrderId);
                Assert.Null(stale.SopCaptureStatus);
                Assert.Null(stale.AssignedAnalystId);
                await using (var first = fixture.CreateDbContext())
                {
                    first.CurrentUserId = c.UserId;
                    var winner = await first.TestOrders.SingleAsync(o => o.Id == a.OrderId);
                    await Capture(first).CaptureIfNeededAsync(winner);
                    winner.AssignedAnalystId = c.UserId;
                    await first.SaveChangesAsync();
                }

                await using var transaction = await second.Database.BeginTransactionAsync();
                if (editBeforeAdoption)
                    stale.AssignedAnalystId = fixture.SeededControllerUserId;

                await Capture(second, CapturedAt.AddHours(1)).CaptureIfNeededAsync(stale);
                Assert.Equal(c.Sop.RevisionId, stale.SopRevisionId);
                if (!editBeforeAdoption)
                    stale.AssignedAnalystId = fixture.SeededControllerUserId;

                await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
            }

            await AssertPathogenCaptureAsync(c, a.OrderId);
            await using var assert = fixture.CreateDbContext();
            var persisted = await assert.TestOrders.AsNoTracking().SingleAsync(o => o.Id == a.OrderId);
            Assert.Equal(c.UserId, persisted.AssignedAnalystId);
            var orderId = a.OrderId.ToString();
            Assert.Equal(1, await assert.AuditLogs.AsNoTracking()
                .CountAsync(log => log.EntityName == nameof(TestOrder) && log.EntityId == orderId && log.Action == "Update"));
        });
    }

    private async Task WaitUntilBlockedOnALockAsync(Task second)
    {
        await using var watcher = fixture.CreateDbContext();
        var connection = watcher.Database.GetDbConnection();
        await connection.OpenAsync();
        for (var attempt = 0; attempt < 100 && !second.IsCompleted; attempt++)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND pid <> pg_backend_pid()";
            if ((long)(await command.ExecuteScalarAsync())! > 0)
                return;
            await Task.Delay(100);
        }
        Assert.False(second.IsFaulted, "The second writer failed before it could wait for the first.");
    }

    [PostgresFact]
    public async Task Icp_method_assay_submission_captures_the_revision()
    {
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        int userId;
        int orderId;
        int runSampleId;
        Sop sop;
        await using (var arrange = fixture.CreateDbContext())
        {
            var section = TestServiceFactory.EnsureMicroSection(arrange);
            var role = new Role { Name = "Analyst " + tag, Type = RoleType.Analyst, IsActive = true };
            arrange.Roles.Add(role);
            await arrange.SaveChangesAsync();
            var user = new User
            {
                Username = "icp_sop_" + tag,
                FullName = "ICP SOP Analyst",
                RoleId = role.Id,
                IsActive = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
            };
            arrange.Users.Add(user);
            await arrange.SaveChangesAsync();
            TestServiceFactory.AssignUserToMicroSection(arrange, user.Id);
            arrange.CurrentUserId = user.Id;
            userId = user.Id;

            var calEntry = new MaterialMasterEntry
            {
                SectionId = section.Id, Code = "ICP-CAL-" + tag, Name = "ICP cal " + tag,
                Category = MaterialMasterCategory.ReferenceStandard, BaseUnit = MaterialUnit.Milliliter,
                IsActive = true, CreatedByUserId = userId, CreatedAt = DateTime.UtcNow,
                LastModifiedByUserId = userId, LastModifiedAt = DateTime.UtcNow,
            };
            arrange.MaterialMasterEntries.Add(calEntry);
            await arrange.SaveChangesAsync();
            var lot = new Material
            {
                SectionId = section.Id, MaterialType = MaterialType.ReferenceStandard, MaterialMasterEntryId = calEntry.Id,
                MaterialName = calEntry.Name, ManufacturerName = "Acme", BatchNumber = "LOT-" + tag,
                ReceivingDate = DateTime.UtcNow.AddDays(-10), ExpiryDate = DateTime.UtcNow.AddYears(1), Location = "Shelf",
                QuantityReceived = 100m, QuantityRemaining = 100m, Unit = MaterialUnit.Milliliter,
                Purity = 99.5m, MoisturePercent = 0m, CreatedByUserId = userId, LastModifiedByUserId = userId,
            };
            var equipment = new Equipment
            {
                Name = "ICP-OES " + tag, Code = "ICP-" + tag, Type = EquipmentType.IcpOes, SectionId = section.Id,
                CalibrationDueDate = DateTime.UtcNow.AddYears(1),
            };
            var def = new TestDefinition
            {
                Code = "ICPT_" + tag, DisplayName = "ICP " + tag, SectionId = section.Id,
                WorkflowType = WorkflowType.IcpMethodAssay, EquationType = EquationType.IcpMethodAssay, IsActive = true,
                IcpMode = IcpTestMode.Assay, IcpSampleForm = IcpSampleForm.Solid, IcpTheoreticalAmount = 0.5m,
                IcpElements = { new TestDefinitionIcpElement { Symbol = "Ca", DisplayOrder = 0, TargetPpm = 20m } },
            };
            var item = new Item { Code = "CA-" + tag, Name = "Calcium tablet " + tag, Category = SampleCategory.FinishedProduct, IsActive = true };
            arrange.Materials.Add(lot);
            arrange.Equipment.Add(equipment);
            arrange.TestDefinitions.Add(def);
            arrange.Items.Add(item);
            await arrange.SaveChangesAsync();

            var caEl = def.IcpElements.Single();
            arrange.Specifications.Add(new Specification
            {
                ItemId = item.Id, TestCode = def.Code, ParameterName = caEl.Symbol, IcpTestElementId = caEl.Id,
                LimitType = LimitType.Range, LowerLimit = 90m, UpperLimit = 110m, Unit = "%", DisplayOrder = 1,
            });
            var cause = await arrange.CausesOfTesting.FirstOrDefaultAsync() ?? new CauseOfTesting { Name = "Release", IsActive = true };
            if (cause.Id == 0) arrange.CausesOfTesting.Add(cause);
            var sample = new Sample
            {
                ReferenceNumber = "FP-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), ItemId = item.Id,
                Category = SampleCategory.FinishedProduct, BatchNumber = "B-CA", ReceivedAt = DateTime.UtcNow,
                CauseOfTesting = cause, ReceivedByUserId = userId, Status = SampleStatus.InTesting,
            };
            arrange.Samples.Add(sample);
            await arrange.SaveChangesAsync();
            var order = new TestOrder
            {
                SampleId = sample.Id, TestCode = def.Code, SectionId = section.Id,
                CurrentStep = WorkflowStep.Running, Status = ApprovalStatus.InProgress,
            };
            arrange.TestOrders.Add(order);
            await arrange.SaveChangesAsync();
            orderId = order.Id;

            sop = await LinkSopAsync(arrange, item.Id, effective: true);

            var icpSvc = TestServiceFactory.IcpRun(arrange);
            var run = await icpSvc.StartRunAsync(new StartIcpRunRequest(equipment.Id), userId);
            await icpSvc.SaveCalibrationAsync(run.Id, new SaveIcpCalibrationRequest(lot.Id, new()
            {
                new SaveIcpCalibrationElementInput("Ca", 0.999386m),
            }), userId);
            var pdf = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };
            await icpSvc.UploadEvidenceAsync(run.Id, null, IcpEvidenceContext.Calibration, IcpEvidenceKind.CalibrationReport,
                "cal.pdf", "application/pdf", new MemoryStream(pdf), userId);
            await icpSvc.ConfirmCalibrationAsync(run.Id, new ConfirmIcpCalibrationRequest(Password, null), userId, null);
            run = await icpSvc.AssignSamplesAsync(run.Id, new AssignIcpSamplesRequest(new List<int> { orderId }, Password, null), userId, null);
            runSampleId = run.Samples.Single(x => x.TestOrderId == orderId).Id;
            await icpSvc.SaveEntryAsync(runSampleId, new SaveIcpSampleEntryRequest(0.5m, new() { new IcpConcentrationInput(caEl.Id, 19m) }), userId);
            await icpSvc.UploadEvidenceAsync(run.Id, runSampleId, IcpEvidenceContext.Sample, IcpEvidenceKind.SampleReport,
                "s.pdf", "application/pdf", new MemoryStream(pdf), userId);
        }

        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = userId;
            await Engine(act).SubmitIcpMethodAssayAsync(runSampleId, Password, "ok", userId, "127.0.0.1");
        }

        await AssertCapturedAsync(orderId, sop);
    }

    [PostgresFact]
    public async Task Hplc_method_assay_submission_captures_the_revision()
    {
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        int userId;
        int orderId;
        int runSampleId;
        Sop sop;
        await using (var arrange = fixture.CreateDbContext())
        {
            var section = TestServiceFactory.EnsureMicroSection(arrange);
            userId = fixture.SeededUserId;
            TestServiceFactory.AssignUserToMicroSection(arrange, userId);
            var user = await arrange.Users.SingleAsync(u => u.Id == userId);
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);
            await arrange.SaveChangesAsync();

            var diluent = await HplcMethodServiceTests.AddSolutionAsync(arrange, section.Id, "Diluent " + tag, SolutionType.Diluent);
            var mp = await HplcMethodServiceTests.AddSolutionAsync(arrange, section.Id, "Mobile " + tag, SolutionType.MobilePhase);
            var standard = await HplcMethodServiceTests.AddEntryAsync(arrange, section.Id, "STD-" + tag);
            var stage = await arrange.ProductionStages.FirstOrDefaultAsync(p => p.Role == ProductionStageRole.Bulk);
            if (stage == null)
            {
                stage = new ProductionStage { Name = ProductionStageRole.Bulk + " " + tag, Role = ProductionStageRole.Bulk, IsActive = true };
                arrange.ProductionStages.Add(stage);
                await arrange.SaveChangesAsync();
            }

            var item = new Item { Code = "RM-" + tag, Name = "Raw Material " + tag, Category = SampleCategory.RawMaterial, IsActive = true };
            arrange.Items.Add(item);
            var method = new HplcMethod
            {
                Name = "HPLC Assay " + tag, Abbreviation = "H" + tag, SectionId = section.Id,
                DiluentSolutionId = diluent.Id, ResultMode = HplcResultMode.Assay, Technique = HplcTechnique.Hplc, IsActive = true,
            };
            method.MobilePhases.Add(new HplcMethodMobilePhase { Channel = "A", SolutionMasterId = mp.Id });
            arrange.HplcMethods.Add(method);
            var analyte = new HplcMethodAnalyte
            {
                HplcMethod = method, Name = "Active Ingredient", StandardEntryId = standard.Id, WavelengthNm = 254m,
                DisplayOrder = 1, TheoreticalWeightStdMg = 50m, TheoreticalWeightTestMg = 50m, StandardInjections = 3,
            };
            arrange.HplcMethodAnalytes.Add(analyte);
            var testDef = new TestDefinition
            {
                Code = "HPLC-" + tag, DisplayName = "HPLC Content Assay " + tag, SectionId = section.Id,
                WorkflowType = WorkflowType.HplcMethodAssay, EquationType = EquationType.HplcMethodAssay,
                RequiresSystemSuitability = true, HplcMethod = method, IsActive = true,
            };
            arrange.TestDefinitions.Add(testDef);
            await arrange.SaveChangesAsync();

            arrange.SampleTests.Add(new SampleTest { ItemId = item.Id, TestCode = testDef.Code });
            arrange.Specifications.Add(new Specification
            {
                ItemId = item.Id, TestCode = testDef.Code, ParameterName = "Active Ingredient",
                HplcMethodAnalyteId = analyte.Id, LimitType = LimitType.Range, LowerLimit = 95m, UpperLimit = 105m,
                Unit = "%", ResultBasis = ResultBasis.PercentAsIs, DisplayOrder = 1,
            });
            arrange.TestDefinitionStageReplicates.Add(new TestDefinitionStageReplicate
            {
                TestDefinitionId = testDef.Id, Role = ProductionStageRole.Bulk, SampleReplicates = 1,
            });
            var cause = await arrange.CausesOfTesting.FirstOrDefaultAsync()
                ?? arrange.CausesOfTesting.Add(new CauseOfTesting { Name = "Routine Testing", IsActive = true }).Entity;
            var sample = new Sample
            {
                ItemId = item.Id, Category = SampleCategory.RawMaterial, ReferenceNumber = "SMP-" + tag,
                BatchNumber = "B-001", ProductionStageId = stage.Id, ProductionStage = stage.Name,
                CauseOfTesting = cause, ReceivedAt = DateTime.UtcNow, ReceivedByUserId = userId, Status = SampleStatus.InTesting,
            };
            arrange.Samples.Add(sample);
            await arrange.SaveChangesAsync();
            var order = new TestOrder
            {
                SampleId = sample.Id, TestCode = testDef.Code, SectionId = section.Id,
                CurrentStep = WorkflowStep.Running, Status = ApprovalStatus.InProgress,
            };
            arrange.TestOrders.Add(order);
            var equipment = new Equipment
            {
                Code = "EQ-" + tag, Name = "HPLC System " + tag, Type = EquipmentType.Hplc,
                SectionId = section.Id, CalibrationDueDate = DateTime.UtcNow.AddYears(1),
            };
            var column = new ChromatographyColumn
            {
                Code = "COL-" + tag, Name = "C18 Column " + tag, UspDesignation = "L1",
                SectionId = section.Id, IsActive = true, CreatedByUserId = userId, LastModifiedByUserId = userId,
            };
            column.CompatibleEquipment.Add(equipment);
            arrange.Equipment.Add(equipment);
            arrange.ChromatographyColumns.Add(column);
            await arrange.SaveChangesAsync();

            var run = new HplcRun
            {
                Code = "RUN-" + tag, SectionId = section.Id, EquipmentId = equipment.Id,
                HplcMethodId = method.Id, ChromatographyColumnId = column.Id, Status = HplcRunStatus.Open,
                MethodSnapshotJson = JsonSerializer.Serialize(HplcMethodResponse.From(method), SnapshotJson.Options),
            };
            arrange.HplcRuns.Add(run);
            await arrange.SaveChangesAsync();

            arrange.HplcSstRecords.Add(new HplcSstRecord
            {
                HplcRunId = run.Id, Code = "SST-" + tag, Status = HplcSstStatus.Passed,
                Analytes =
                {
                    new HplcSstAnalyte
                    {
                        HplcMethodAnalyteId = analyte.Id, MeanResponse = 1000m,
                        StandardWeightMg = 50m, StandardPurityPercent = 100m, StandardMoisturePercent = 0m,
                    }
                }
            });
            var runSample = new HplcRunSample
            {
                HplcRunId = run.Id, TestOrderId = order.Id, Status = HplcRunSampleStatus.Assigned,
                Replicates =
                {
                    new HplcSampleReplicate
                    {
                        ReplicateNo = 1, ActualWeightMg = 50m,
                        Responses = { new HplcReplicateResponse { HplcMethodAnalyteId = analyte.Id, Response = 980m } }
                    }
                }
            };
            arrange.HplcRunSamples.Add(runSample);
            await arrange.SaveChangesAsync();
            arrange.HplcEvidences.Add(new HplcEvidence
            {
                HplcRunId = run.Id, HplcRunSampleId = runSample.Id, Kind = HplcEvidenceKind.SampleReport,
                Context = HplcEvidenceContext.Sample, FilePath = "report.pdf", FileName = "report.pdf",
                ContentType = "application/pdf", UploadedByUserId = userId, UploadedAt = DateTime.UtcNow,
            });
            await arrange.SaveChangesAsync();

            orderId = order.Id;
            runSampleId = runSample.Id;
            sop = await LinkSopAsync(arrange, item.Id, effective: true);
        }

        await using (var act = fixture.CreateDbContext())
        {
            act.CurrentUserId = userId;
            await Engine(act).SubmitHplcMethodAssayAsync(runSampleId, Password, "ok", userId, "127.0.0.1");
        }

        await AssertCapturedAsync(orderId, sop);
    }
}
