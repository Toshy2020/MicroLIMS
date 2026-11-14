using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Characterisation of today's count-result save path
// (TestWorkflowEngine.RecordResultAsync -> IncubationStepRecorder.RecordCountTestAsync,
// the same call TestWorkflowController.record-result makes after it has built a
// CountTestPayload from the request). Every expected value below is what the code
// produced when this file was written; it pins current behaviour so the arithmetic
// can be moved into one calculator (Faster Data Entry, Task 8) without changing a
// saved result. A value here is NOT a statement that the rule is scientifically
// right - questionable ones are marked "QUESTION:" and reported to the owner.
[Collection("PostgresDatabaseCollection")]
public class CountResultCharacterisationPostgresTests
{
    private readonly PostgresTestFixture _fixture;

    public CountResultCharacterisationPostgresTests(PostgresTestFixture fixture) => _fixture = fixture;

    private static string UniqueCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    // What the sample is and what is configured for its test.
    private sealed record Scn(
        SampleCategory Category,
        decimal? SpecDf = null,
        string SpecUnit = "",
        string Alert = "",
        string Action = "",
        string Spec = "",
        bool WaterConfig = false,
        string WaterUnit = "",
        bool ItemlessNonDirect = false);

    private sealed record Snap(
        TestWorkflowResult Returned,
        CountTestReading Reading,
        Result ResultRow,
        Incubation Incubation,
        TestOrder Order,
        int ReadingAuditRows,
        int ResultAuditRows);

    private async Task<(int OrderId, string StepName)> ArrangeAsync(Scn scn)
    {
        var arranged = await ArrangeCoreAsync(scn, selectMedia: true);
        return (arranged.OrderId, "CountIncubation");
    }

    private sealed record Arranged(int OrderId, int MediaId, int IncubatorId);

    private async Task<Arranged> ArrangeCoreAsync(Scn scn, bool selectMedia)
    {
        await using var db = _fixture.CreateDbContext();
        var sectionId = _fixture.SeededSectionId;
        var testCode = UniqueCode("TAMCCH");

        var definition = new TestDefinition { Code = testCode, DisplayName = "Count test (characterisation)", WorkflowType = WorkflowType.CountTest, SectionId = sectionId };
        db.TestDefinitions.Add(definition);
        await db.SaveChangesAsync();

        var step = new TestWorkflowStep
        {
            TestDefinitionId = definition.Id, StepOrder = 1, StepName = "CountIncubation",
            IncubationMinHours = 72, IncubationMaxHours = 120, TemperatureMin = 30, TemperatureMax = 35,
            IsFinalStep = true, StepType = StepType.PlateCount
        };
        db.TestWorkflowSteps.Add(step);

        var material = new Material
        {
            MaterialType = MaterialType.DehydratedMedia, MaterialName = "TSA Powder", ManufacturerName = "Himedia",
            BatchNumber = UniqueCode("LOT-"), ReceivingDate = DateTime.UtcNow.AddDays(-10), Code = UniqueCode("TSA"),
            Location = "Micro Lab", QuantityReceived = 500, QuantityRemaining = 500, Unit = MaterialUnit.Gram, SectionId = sectionId
        };
        db.Materials.Add(material);
        await db.SaveChangesAsync();

        db.TestWorkflowStepMedias.Add(new TestWorkflowStepMedia { TestWorkflowStepId = step.Id, MediaProductId = MediaProductTestData.ProductIdFor(db, material.Id), TempMin = 30, TempMax = 35, IncubationMinHours = 72, IncubationMaxHours = 120 });
        var media = new Media { MaterialId = material.Id, LotNumber = UniqueCode("TSA/"), IsReleasedForUse = true, Status = MediaStatus.Active, ExpiryDate = DateTime.UtcNow.AddDays(30) };
        db.Media.Add(media);
        var incubator = new Equipment { Name = "Incubator", Code = UniqueCode("INC-"), Type = EquipmentType.Incubator, SetPointTemperature = 32, SectionId = sectionId };
        db.Equipment.Add(incubator);
        var cause = new CauseOfTesting { Name = UniqueCode("Routine ") };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            Category = scn.Category, ControlNumber = UniqueCode("CTRL-"), ReferenceNumber = UniqueCode("REF-"),
            CauseOfTestingId = cause.Id, ReceivedByUserId = _fixture.SeededUserId, Status = SampleStatus.Received
        };
        // Product / RM / PM / GPT must have their preparation confirmed before incubation can start
        // (a precondition of the save path, not under test). Direct-count samples are exempt.
        var needsPreparation = scn.Category is SampleCategory.FinishedProduct or SampleCategory.RawMaterial
            or SampleCategory.PackagingMaterial or SampleCategory.GPT;
        if (needsPreparation)
            sample.PreparationStatus = SamplePreparationStatus.Ready;

        if (scn.Category == SampleCategory.Water)
        {
            var point = new WaterSamplingPoint { Code = UniqueCode("WP-"), Location = "Utility Room", AssignedTestCodes = new() { testCode } };
            db.WaterSamplingPoints.Add(point);
            await db.SaveChangesAsync();
            sample.WaterSamplingPointId = point.Id;
            if (scn.WaterConfig)
                db.SamplingConfigurations.Add(new SamplingConfiguration
                {
                    WaterSamplingPointId = point.Id, TestCode = testCode,
                    AlertLimit = scn.Alert, ActionLimit = scn.Action, SpecLimit = scn.Spec, Unit = scn.WaterUnit
                });
        }
        else if (scn.Category is SampleCategory.FinishedProduct or SampleCategory.RawMaterial or SampleCategory.PackagingMaterial
                 && !scn.ItemlessNonDirect)
        {
            var item = new Item { Code = UniqueCode("ITM-"), Name = "Characterisation item", Category = scn.Category };
            db.Items.Add(item);
            await db.SaveChangesAsync();
            sample.ItemId = item.Id;
            db.Specifications.Add(new Specification
            {
                ItemId = item.Id, TestCode = testCode, DilutionFactor = scn.SpecDf, Unit = scn.SpecUnit,
                AlertLimit = scn.Alert, ActionLimit = scn.Action, SpecLimit = scn.Spec
            });
        }
        // EnvironmentalMonitoring / AfterCleaning: no item, no water point -> no limits to read.
        // ItemlessNonDirect (e.g. GPT) : a non-direct sample with no item.

        var order = new TestOrder { TestCode = testCode, Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, SectionId = sectionId };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        if (sample.ItemId is not null)
        {
            db.SamplePreparations.Add(new SamplePreparation
            {
                SampleId = sample.Id, Amount = 10, Technique = "PourPlate", Diluent = "Diluent", Neutralizer = "None",
                PreparedByUserId = _fixture.SeededUserId, PreparedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Media selection is the precondition RecordResult enforces; it is not under test.
        if (selectMedia)
        {
            var engine = TestServiceFactory.TestWorkflow(db);
            await engine.SelectMediaAsync(order.Id, "CountIncubation", media.Id, incubator.Id, _fixture.SeededUserId);
        }

        return new Arranged(order.Id, media.Id, incubator.Id);
    }

    private async Task<TestWorkflowResult> ActAsync(int orderId, string step, List<string> readings, decimal? clientDilutionFactor, string? note = null)
    {
        await using var actDb = _fixture.CreateDbContext();
        var engine = TestServiceFactory.TestWorkflow(actDb);
        // Same payload shape the controller builds: DilutionFactor ?? 1.
        var payload = new CountTestPayload(readings, clientDilutionFactor ?? 1, note);
        return await engine.RecordResultAsync(orderId, step, payload, _fixture.SeededUserId);
    }

    private async Task<Snap> AssertReadAsync(int orderId, TestWorkflowResult returned)
    {
        await using var assertDb = _fixture.CreateDbContext();
        var reading = await assertDb.CountTestReadings.AsNoTracking().SingleAsync(r => r.TestOrderId == orderId);
        var result = await assertDb.Results.AsNoTracking().SingleAsync(r => r.TestOrderId == orderId);
        var incubation = await assertDb.Incubations.AsNoTracking().SingleAsync(i => i.TestOrderId == orderId);
        var order = await assertDb.TestOrders.AsNoTracking().SingleAsync(t => t.Id == orderId);
        // NewValue is JSON, so "<" and "²" are escaped in it: compare the parsed property, not a substring.
        var readingAuditRows = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(CountTestReading) && a.EntityId == reading.Id.ToString() && a.Action == "Create")
            .Select(a => a.NewValue).ToListAsync();
        var readingAudit = readingAuditRows.Count(v => v is not null
            && System.Text.Json.JsonDocument.Parse(v).RootElement.GetProperty("ReportedResult").GetString() == reading.ReportedResult);
        var resultAuditRows = await assertDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(Result) && a.EntityId == result.Id.ToString() && a.Action == "Create")
            .Select(a => a.NewValue).ToListAsync();
        var resultAudit = resultAuditRows.Count(v => v is not null
            && System.Text.Json.JsonDocument.Parse(v).RootElement.GetProperty("RawValue").GetString() == result.RawValue);
        return new Snap(returned, reading, result, incubation, order, readingAudit, resultAudit);
    }

    private async Task<Snap> RunAsync(Scn scn, string[] readings, decimal? clientDf, string? note = null)
    {
        var (orderId, step) = await ArrangeAsync(scn);
        var returned = await ActAsync(orderId, step, readings.ToList(), clientDf, note);
        return await AssertReadAsync(orderId, returned);
    }

    // Everything that does not depend on the branch.
    private void AssertCommon(Snap s, string reported, ResultStatus status, string rawReadings)
    {
        Assert.Equal(reported, s.Reading.ReportedResult);
        Assert.Equal(status, s.Reading.Status);
        Assert.Equal(rawReadings, s.Reading.PlateReadings);
        Assert.Equal("CountIncubation", s.Reading.StepName);
        Assert.True(s.Reading.IsActive);
        Assert.Equal(_fixture.SeededUserId, s.Reading.EnteredByUserId);

        Assert.Equal(rawReadings, s.ResultRow.RawValue);
        Assert.Equal($"{reported} ({status})", s.ResultRow.InterpretedValue);
        Assert.Equal(ResultType.Numeric, s.ResultRow.Type);
        Assert.Equal(_fixture.SeededUserId, s.ResultRow.EnteredByUserId);

        // The value handed back to the caller is the persisted one.
        Assert.Equal(reported, s.Returned.OutcomeSummary);
        Assert.Equal(reported, s.Returned.FinalResult);
        Assert.Equal(status, s.Returned.Status);
        Assert.Equal(s.Reading.Average, s.Returned.Average);
        Assert.Equal(s.Reading.CalculatedResult, s.Returned.CalculatedResult);
        Assert.True(s.Returned.AllStepsComplete);

        // The incubation window is closed with the reported value, and the (single, final-step) order is Ready.
        Assert.NotNull(s.Incubation.CompletedAt);
        Assert.Equal(_fixture.SeededUserId, s.Incubation.CompletedByUserId);
        Assert.Equal(reported, s.Incubation.Outcome);
        Assert.Equal(WorkflowStep.Ready, s.Order.CurrentStep);

        // Rule 4.4: both regulated inserts are audited with the saved values (the test host has no current-user context, so the audit UserId is not asserted here).
        Assert.Equal(1, s.ReadingAuditRows);
        Assert.Equal(1, s.ResultAuditRows);
    }

    private void AssertNumeric(Snap s, string reported, decimal average, decimal calculated, ResultStatus status,
        decimal dilutionFactor, decimal? configuredDf, string rawReadings,
        bool overridden = false, string? note = null, string? alert = null, string? action = null, string? spec = null)
    {
        AssertCommon(s, reported, status, rawReadings);
        Assert.Equal(average, s.Reading.Average);
        Assert.Equal(calculated, s.Reading.CalculatedResult);
        Assert.Equal(dilutionFactor, s.Reading.DilutionFactor);
        Assert.Equal(configuredDf, s.Reading.ConfiguredDilutionFactor);
        Assert.Equal(overridden, s.Reading.DilutionFactorOverridden);
        Assert.Equal(note, s.Reading.DilutionFactorOverrideNote);
        Assert.Equal(alert, s.Reading.AlertLimit);
        Assert.Equal(action, s.Reading.ActionLimit);
        Assert.Equal(spec, s.Reading.SpecLimit);
        Assert.False(s.Reading.HasNonNumericReading);
        Assert.Null(s.Reading.NonNumericValue);
        Assert.False(s.Reading.RequiresReview);
    }

    private void AssertNonNumeric(Snap s, string reported, string rawReadings, decimal dilutionFactor, decimal? configuredDf,
        bool overridden = false, string? note = null)
    {
        AssertCommon(s, reported, ResultStatus.RequiresReview, rawReadings);
        Assert.Null(s.Reading.Average);
        Assert.Null(s.Reading.CalculatedResult);
        Assert.Equal(dilutionFactor, s.Reading.DilutionFactor);
        Assert.Equal(configuredDf, s.Reading.ConfiguredDilutionFactor);
        Assert.Equal(overridden, s.Reading.DilutionFactorOverridden);
        Assert.Equal(note, s.Reading.DilutionFactorOverrideNote);
        // Limits are never looked up on this branch, even when configured.
        Assert.Null(s.Reading.AlertLimit);
        Assert.Null(s.Reading.ActionLimit);
        Assert.Null(s.Reading.SpecLimit);
        Assert.True(s.Reading.HasNonNumericReading);
        Assert.Equal(reported, s.Reading.NonNumericValue);
        Assert.True(s.Reading.RequiresReview);
    }

    private async Task AssertNothingPersistedAsync(int orderId)
    {
        await using var db = _fixture.CreateDbContext();
        Assert.False(await db.CountTestReadings.AsNoTracking().AnyAsync(r => r.TestOrderId == orderId));
        Assert.False(await db.Results.AsNoTracking().AnyAsync(r => r.TestOrderId == orderId));
        Assert.Null((await db.Incubations.AsNoTracking().SingleAsync(i => i.TestOrderId == orderId)).CompletedAt);
        Assert.Equal(WorkflowStep.Incubating, (await db.TestOrders.AsNoTracking().SingleAsync(t => t.Id == orderId)).CurrentStep);
    }

    private static readonly Scn WaterNoLimits = new(SampleCategory.Water);
    private static readonly Scn WaterLimits = new(SampleCategory.Water, Alert: "10", Action: "50", Spec: "100", WaterConfig: true);

    // ===================================================================
    // Branch: direct count (Water / EM / After Cleaning), DF forced to 1
    // ===================================================================

    // IncubationStepRecorder.cs L784-L790 (force DF = 1) + L849-L850 (average x DF).
    [PostgresFact]
    public async Task Water_DirectCount_TwoPlates_AveragesAndReportsWholeNumberWithDefaultUnit()
    {
        var s = await RunAsync(WaterNoLimits, new[] { "12", "14" }, clientDf: 1);
        AssertNumeric(s, "13 CFU/mL", 13m, 13m, ResultStatus.LimitsNotConfigured, 1m, 1m, "12,14", alert: null, action: null, spec: null);
    }

    [PostgresFact]
    public async Task Water_ClientDilutionFactorIsIgnored_ForcedToOne_NoOverrideRecorded()
    {
        var s = await RunAsync(WaterNoLimits, new[] { "12", "14" }, clientDf: 10m, note: "client says ten");
        // DF stored is 1, configured snapshot is 1, not flagged overridden, the note is dropped.
        AssertNumeric(s, "13 CFU/mL", 13m, 13m, ResultStatus.LimitsNotConfigured, 1m, 1m, "12,14");
    }

    // The "force DF = 1" rule also names EM and After Cleaning, but neither can reach this method:
    // their tests are refused before incubation starts until locations are assigned, and once
    // locations exist RecordResultAsync refuses the single-value path (batch endpoint only).
    // So today the rule is only reachable for Water.
    [PostgresFact]
    public async Task EnvironmentalMonitoringAndAfterCleaning_CannotStartIncubation_WithoutLocations()
    {
        await PostgresCases.RunAsync(new[] { SampleCategory.EnvironmentalMonitoring, SampleCategory.AfterCleaning }, async cat =>
        {
            var a = await ArrangeCoreAsync(new Scn(cat), selectMedia: false);
            await using var actDb = _fixture.CreateDbContext();
            var engine = TestServiceFactory.TestWorkflow(actDb);
            var ex = await Assert.ThrowsAsync<WorkflowStepException>(() =>
                engine.SelectMediaAsync(a.OrderId, "CountIncubation", a.MediaId, a.IncubatorId, _fixture.SeededUserId));
            Assert.Equal("Preparation not complete - no locations assigned to this test.", ex.Message);
        });
    }

    [PostgresFact]
    public async Task EnvironmentalMonitoring_RecordResultWithoutAnIncubation_IsRefusedBeforeAnyCountMaths()
    {
        var a = await ArrangeCoreAsync(new Scn(SampleCategory.EnvironmentalMonitoring), selectMedia: false);
        await using var actDb = _fixture.CreateDbContext();
        var engine = TestServiceFactory.TestWorkflow(actDb);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            engine.RecordResultAsync(a.OrderId, "CountIncubation", new CountTestPayload(new List<string> { "7" }, 1m), _fixture.SeededUserId));
        await using var assertDb = _fixture.CreateDbContext();
        Assert.False(await assertDb.CountTestReadings.AsNoTracking().AnyAsync(r => r.TestOrderId == a.OrderId));
    }

    // ===================================================================
    // Branch: item-based (Product / RM / PM) - dilution applied
    // ===================================================================

    // L792-L828 (configured DF read from the item's Specification) + L851.
    [PostgresFact]
    public async Task Product_DilutionApplied_ConfiguredDf_ClientMatches_NoOverride()
    {
        var scn = new Scn(SampleCategory.FinishedProduct, SpecDf: 10m, SpecUnit: "g", Alert: "200", Action: "500", Spec: "1000");
        var s = await RunAsync(scn, new[] { "12", "14" }, clientDf: 10m);
        AssertNumeric(s, "130 CFU/g", 13m, 130m, ResultStatus.WithinLimits, 10m, 10m, "12,14", alert: "200", action: "500", spec: "1000");
    }

    [PostgresFact]
    public async Task RawMaterialAndPackagingMaterial_BehaveAsProduct()
    {
        await PostgresCases.RunAsync(new[] { SampleCategory.RawMaterial, SampleCategory.PackagingMaterial }, async cat =>
        {
            var scn = new Scn(cat, SpecDf: 10m, SpecUnit: "ml", Spec: "1000");
            var s = await RunAsync(scn, new[] { "12", "14" }, clientDf: 10m);
            AssertNumeric(s, "130 CFU/ml", 13m, 130m, ResultStatus.WithinLimits, 10m, 10m, "12,14", alert: "", action: "", spec: "1000");
        });
    }

    [PostgresFact]
    public async Task Product_ClientDfDiffers_WithNote_Overridden_NoteAndConfiguredSnapshotPersisted()
    {
        var scn = new Scn(SampleCategory.FinishedProduct, SpecDf: 10m, SpecUnit: "g", Spec: "100000");
        var s = await RunAsync(scn, new[] { "12", "14" }, clientDf: 100m, note: "extra dilution after spill");
        AssertNumeric(s, "1300 CFU/g", 13m, 1300m, ResultStatus.WithinLimits, 100m, 10m, "12,14",
            overridden: true, note: "extra dilution after spill", alert: "", action: "", spec: "100000");
    }

    [PostgresFact]
    public async Task Product_ClientDfDiffers_NoNote_Rejected_NothingPersisted()
    {
        var scn = new Scn(SampleCategory.FinishedProduct, SpecDf: 10m, SpecUnit: "g");
        var (orderId, step) = await ArrangeAsync(scn);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => ActAsync(orderId, step, new List<string> { "12" }, clientDilutionFactor: 100m));
        Assert.Equal(WorkflowErrorCodes.DilutionFactorJustificationRequired, ex.ErrorCode);
        await AssertNothingPersistedAsync(orderId);
    }

    [PostgresFact]
    public async Task Product_ClientDfDiffers_BlankNote_Rejected()
    {
        var scn = new Scn(SampleCategory.FinishedProduct, SpecDf: 10m, SpecUnit: "g");
        var (orderId, step) = await ArrangeAsync(scn);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => ActAsync(orderId, step, new List<string> { "12" }, 100m, note: "   "));
        Assert.Equal(WorkflowErrorCodes.DilutionFactorJustificationRequired, ex.ErrorCode);
        await AssertNothingPersistedAsync(orderId);
    }

    [PostgresFact]
    public async Task Product_NoConfiguredDf_Rejected_NothingPersisted()
    {
        var scn = new Scn(SampleCategory.FinishedProduct, SpecDf: null, SpecUnit: "g");
        var (orderId, step) = await ArrangeAsync(scn);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => ActAsync(orderId, step, new List<string> { "12" }, 1m));
        Assert.Equal(WorkflowErrorCodes.DilutionFactorNotConfigured, ex.ErrorCode);
        await AssertNothingPersistedAsync(orderId);
    }

    [PostgresFact]
    public async Task Product_NoConfiguredUnit_Rejected_NothingPersisted()
    {
        var scn = new Scn(SampleCategory.FinishedProduct, SpecDf: 10m, SpecUnit: "  ");
        var (orderId, step) = await ArrangeAsync(scn);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => ActAsync(orderId, step, new List<string> { "12" }, 10m));
        Assert.Equal(WorkflowErrorCodes.CfuUnitNotConfigured, ex.ErrorCode);
        await AssertNothingPersistedAsync(orderId);
    }

    // The configured-DF / unit guards run before the non-numeric branch, so they apply to TNTC too.
    [PostgresFact]
    public async Task Product_Tntc_WithNoConfiguredDf_StillRejected()
    {
        var scn = new Scn(SampleCategory.FinishedProduct, SpecDf: null, SpecUnit: "g");
        var (orderId, step) = await ArrangeAsync(scn);

        var ex = await Assert.ThrowsAsync<WorkflowStepException>(() => ActAsync(orderId, step, new List<string> { "TNTC" }, 1m));
        Assert.Equal(WorkflowErrorCodes.DilutionFactorNotConfigured, ex.ErrorCode);
        await AssertNothingPersistedAsync(orderId);
    }

    // ===================================================================
    // Branch: input guards
    // ===================================================================

    // L767-L768.
    [PostgresFact]
    public async Task NoPlateReadings_Rejected_NothingPersisted()
    {
        var (orderId, step) = await ArrangeAsync(WaterNoLimits);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => ActAsync(orderId, step, new List<string>(), 1m));
        Assert.Equal("At least one plate reading is required to calculate an average.", ex.Message);
        await AssertNothingPersistedAsync(orderId);
    }

    // ===================================================================
    // Branch: TNTC / Uncountable -> RequiresReview
    // ===================================================================

    // L842-L879.
    [PostgresFact]
    public async Task Tntc_RequiresReview_NoNumbers_NoLimitsLookedUp_EvenWhenLimitsConfigured()
    {
        var s = await RunAsync(WaterLimits, new[] { "TNTC" }, clientDf: 1m);
        AssertNonNumeric(s, "TNTC", "TNTC", 1m, 1m);
    }

    [PostgresFact]
    public async Task Uncountable_RequiresReview()
    {
        var s = await RunAsync(WaterLimits, new[] { "Uncountable" }, clientDf: 1m);
        AssertNonNumeric(s, "Uncountable", "Uncountable", 1m, 1m);
    }

    [PostgresFact]
    public async Task Tntc_IsCaseInsensitive_ReportedInCanonicalCase_RawReadingKeptAsTyped()
    {
        var s = await RunAsync(WaterNoLimits, new[] { "tntc" }, clientDf: 1m);
        AssertNonNumeric(s, "TNTC", "tntc", 1m, 1m);
    }

    [PostgresFact]
    public async Task OneNonNumericAmongNumbers_WholeResultIsNonNumeric_NumbersIgnored()
    {
        var s = await RunAsync(WaterNoLimits, new[] { "12", "TNTC", "14" }, clientDf: 1m);
        AssertNonNumeric(s, "TNTC", "12,TNTC,14", 1m, 1m);
    }

    // QUESTION: with both words entered, the first one in plate order wins; the other is silently dropped.
    [PostgresFact]
    public async Task TntcAndUncountableTogether_FirstEnteredWins()
    {
        var first = await RunAsync(WaterNoLimits, new[] { "Uncountable", "TNTC" }, clientDf: 1m);
        AssertNonNumeric(first, "Uncountable", "Uncountable,TNTC", 1m, 1m);

        var second = await RunAsync(WaterNoLimits, new[] { "TNTC", "Uncountable" }, clientDf: 1m);
        AssertNonNumeric(second, "TNTC", "TNTC,Uncountable", 1m, 1m);
    }

    // The non-numeric branch still snapshots the configured DF and the override note.
    [PostgresFact]
    public async Task Product_Tntc_KeepsDilutionOverrideSnapshotAndNote()
    {
        var scn = new Scn(SampleCategory.FinishedProduct, SpecDf: 10m, SpecUnit: "g", Alert: "100", Action: "500", Spec: "1000");
        var s = await RunAsync(scn, new[] { "TNTC" }, clientDf: 100m, note: "plates overgrown at 1:100");
        AssertNonNumeric(s, "TNTC", "TNTC", 100m, 10m, overridden: true, note: "plates overgrown at 1:100");
    }

    // QUESTION: any other non-numeric word (typo, "TNTC " with a trailing space, "N/A") is not
    // recognised as non-numeric and falls into decimal.Parse, which throws a raw FormatException
    // (not a BusinessRuleException) - the save fails with an unhandled error rather than a field error.
    [PostgresFact]
    public async Task UnrecognisedWord_ThrowsFormatException_NothingPersisted()
    {
        await PostgresCases.RunAsync(new[] { "abc", "TNTC ", "N/A" }, async word =>
        {
            var (orderId, step) = await ArrangeAsync(WaterNoLimits);
            await Assert.ThrowsAsync<FormatException>(() => ActAsync(orderId, step, new List<string> { word }, 1m));
            await AssertNothingPersistedAsync(orderId);
        });
    }

    // ===================================================================
    // Branch: rounding of the reported whole number (MidpointRounding.AwayFromZero)
    // ===================================================================

    // L897-L899. Midpoint with an even integer below (banker's rounding would give 12).
    [PostgresFact]
    public async Task Rounding_MidpointOnEvenBelow_RoundsUp_NotBankers()
    {
        var s = await RunAsync(WaterNoLimits, new[] { "12", "13" }, clientDf: 1m);
        AssertNumeric(s, "13 CFU/mL", 12.5m, 12.5m, ResultStatus.LimitsNotConfigured, 1m, 1m, "12,13");
    }

    // Midpoint with an odd integer below (banker's and away-from-zero agree).
    [PostgresFact]
    public async Task Rounding_MidpointOnOddBelow_RoundsUp()
    {
        var s = await RunAsync(WaterNoLimits, new[] { "13", "14" }, clientDf: 1m);
        AssertNumeric(s, "14 CFU/mL", 13.5m, 13.5m, ResultStatus.LimitsNotConfigured, 1m, 1m, "13,14");
    }

    [PostgresFact]
    public async Task Rounding_JustBelowAndJustAboveMidpoint()
    {
        var below = await RunAsync(WaterNoLimits, new[] { "12.4" }, clientDf: 1m);
        AssertNumeric(below, "12 CFU/mL", 12.4m, 12.4m, ResultStatus.LimitsNotConfigured, 1m, 1m, "12.4");

        var above = await RunAsync(WaterNoLimits, new[] { "12.6" }, clientDf: 1m);
        AssertNumeric(above, "13 CFU/mL", 12.6m, 12.6m, ResultStatus.LimitsNotConfigured, 1m, 1m, "12.6");
    }

    // The midpoint arises from the dilution multiplication, not from the average.
    [PostgresFact]
    public async Task Rounding_MidpointAfterDilution_RoundsUp()
    {
        var scn = new Scn(SampleCategory.FinishedProduct, SpecDf: 0.5m, SpecUnit: "g", Spec: "1000");
        var s = await RunAsync(scn, new[] { "5" }, clientDf: 0.5m);
        // 5 x 0.5 = 2.5 -> 3 (banker's would give 2).
        AssertNumeric(s, "3 CFU/g", 5m, 2.5m, ResultStatus.WithinLimits, 0.5m, 0.5m, "5", alert: "", action: "", spec: "1000");
    }

    // The stored values keep full precision; only the reported text is rounded.
    [PostgresFact]
    public async Task Rounding_RepeatingAverage_StoredAtFullPrecision_ReportedRoundedToWholeNumber()
    {
        var scn = new Scn(SampleCategory.FinishedProduct, SpecDf: 10m, SpecUnit: "g", Spec: "1000");
        var s = await RunAsync(scn, new[] { "1", "1", "2" }, clientDf: 10m);
        Assert.Equal("13 CFU/g", s.Reading.ReportedResult);
        // Average 4/3 and calculated 40/3 are persisted unrounded (28 significant digits of System.Decimal).
        Assert.Equal(4m / 3m, s.Reading.Average);
        Assert.Equal(4m / 3m * 10m, s.Reading.CalculatedResult);
        Assert.Equal(ResultStatus.WithinLimits, s.Reading.Status);
    }

    // QUESTION: fractional plate counts are accepted and averaged (a colony count is whole-number).
    [PostgresFact]
    public async Task FractionalPlateReading_IsAccepted()
    {
        var s = await RunAsync(WaterNoLimits, new[] { "12.5" }, clientDf: 1m);
        AssertNumeric(s, "13 CFU/mL", 12.5m, 12.5m, ResultStatus.LimitsNotConfigured, 1m, 1m, "12.5");
    }

    // QUESTION: a negative reading is accepted and produces a "<1" result.
    [PostgresFact]
    public async Task NegativePlateReading_IsAccepted_ReportedBelowOne()
    {
        var s = await RunAsync(WaterNoLimits, new[] { "-3" }, clientDf: 1m);
        AssertNumeric(s, "<1 CFU/mL", -3m, -3m, ResultStatus.LimitsNotConfigured, 1m, 1m, "-3");
    }

    // QUESTION: decimal.Parse default styles accept thousands separators, so "1,000" is read as 1000.
    // (The controller splits nothing on commas; PlateReadings are stored joined by "," so a stored
    // "1,000" is also ambiguous with two readings.)
    [PostgresFact]
    public async Task ThousandsSeparatorReading_IsParsedAsOneThousand()
    {
        var s = await RunAsync(WaterNoLimits, new[] { "1,000" }, clientDf: 1m);
        AssertNumeric(s, "1000 CFU/mL", 1000m, 1000m, ResultStatus.LimitsNotConfigured, 1m, 1m, "1,000");
    }

    // ===================================================================
    // Branch: below-limit reporting ("<DF")
    // ===================================================================

    // L897 (calculated < lowerLimit, lowerLimit = DF).
    [PostgresFact]
    public async Task BelowLimit_ZeroColonies_Water_ReportsLessThanOne_StatusStillCompared()
    {
        var s = await RunAsync(WaterLimits, new[] { "0" }, clientDf: 1m);
        AssertNumeric(s, "<1 CFU/mL", 0m, 0m, ResultStatus.WithinLimits, 1m, 1m, "0", alert: "10", action: "50", spec: "100");
    }

    [PostgresFact]
    public async Task BelowLimit_AverageBelowOne_Water_ReportsLessThanOne()
    {
        var s = await RunAsync(WaterNoLimits, new[] { "0", "1" }, clientDf: 1m);
        AssertNumeric(s, "<1 CFU/mL", 0.5m, 0.5m, ResultStatus.LimitsNotConfigured, 1m, 1m, "0,1");
    }

    [PostgresFact]
    public async Task BelowLimit_Product_ReportsLessThanTheDilutionFactor()
    {
        var scn = new Scn(SampleCategory.FinishedProduct, SpecDf: 10m, SpecUnit: "g", Spec: "1000");
        var s = await RunAsync(scn, new[] { "0", "0" }, clientDf: 10m);
        AssertNumeric(s, "<10 CFU/g", 0m, 0m, ResultStatus.WithinLimits, 10m, 10m, "0,0", alert: "", action: "", spec: "1000");
    }

    // QUESTION: an average of 0.5 colonies x DF 10 = 5, reported "<10" - the threshold is the DF, not 10.
    [PostgresFact]
    public async Task BelowLimit_ProductAverageHalfAColony_ReportsLessThanDf()
    {
        var scn = new Scn(SampleCategory.FinishedProduct, SpecDf: 10m, SpecUnit: "g", Spec: "1000");
        var s = await RunAsync(scn, new[] { "0", "1" }, clientDf: 10m);
        AssertNumeric(s, "<10 CFU/g", 0.5m, 5m, ResultStatus.WithinLimits, 10m, 10m, "0,1", alert: "", action: "", spec: "1000");
    }

    // calculated == DF is NOT below the limit.
    [PostgresFact]
    public async Task BelowLimit_Boundary_CalculatedEqualToDf_IsReportedAsNumber()
    {
        var scn = new Scn(SampleCategory.FinishedProduct, SpecDf: 10m, SpecUnit: "g", Spec: "1000");
        var s = await RunAsync(scn, new[] { "1" }, clientDf: 10m);
        AssertNumeric(s, "10 CFU/g", 1m, 10m, ResultStatus.WithinLimits, 10m, 10m, "1", alert: "", action: "", spec: "1000");
    }

    // QUESTION: the "<" threshold is the DF rounded with the same midpoint rule, so a configured
    // DF of 2.5 reports "<3" (a limit stronger than the real detection threshold of 2.5).
    [PostgresFact]
    public async Task BelowLimit_FractionalDf_ThresholdTextIsRounded()
    {
        var scn = new Scn(SampleCategory.FinishedProduct, SpecDf: 2.5m, SpecUnit: "g", Spec: "1000");
        var s = await RunAsync(scn, new[] { "0" }, clientDf: 2.5m);
        AssertNumeric(s, "<3 CFU/g", 0m, 0m, ResultStatus.WithinLimits, 2.5m, 2.5m, "0", alert: "", action: "", spec: "1000");
    }

    // ===================================================================
    // Branch: unit selection
    // ===================================================================

    // L834-L840 + L890-L892.
    [PostgresFact]
    public async Task Unit_ConfiguredWithoutCfuPrefix_GetsPrefix_WithPrefix_KeptAsIs_CaseInsensitive()
    {
        await PostgresCases.RunAsync(new[]
        {
            ("100mL", "13 CFU/100mL"),
            ("CFU/100mL", "13 CFU/100mL"),
            ("cfu/g", "13 cfu/g"),
        }, async c =>
        {
            var scn = new Scn(SampleCategory.Water, WaterConfig: true, WaterUnit: c.Item1);
            var s = await RunAsync(scn, new[] { "13" }, clientDf: 1m);
            Assert.Equal(c.Item2, s.Reading.ReportedResult);
        });
    }

    // Product unit comes from the item's Specification (required there).
    [PostgresFact]
    public async Task Unit_Product_FromSpecification_WithPrefixHandling()
    {
        var scn = new Scn(SampleCategory.FinishedProduct, SpecDf: 1m, SpecUnit: "CFU/25cm2");
        var s = await RunAsync(scn, new[] { "13" }, clientDf: 1m);
        Assert.Equal("13 CFU/25cm2", s.Reading.ReportedResult);
    }

    // ===================================================================
    // Branch: status against limits (Spec -> Action -> Alert precedence), limit source
    // ===================================================================

    [PostgresFact]
    public async Task Status_AgainstWaterSamplingConfiguration_EachTier()
    {
        await PostgresCases.RunAsync(new[]
        {
            ("5", ResultStatus.WithinLimits),
            ("10", ResultStatus.WithinLimits),          // exactly the alert limit is not "exceeded"
            ("12", ResultStatus.AlertLimitExceeded),
            ("60", ResultStatus.ActionLimitExceeded),
            ("150", ResultStatus.OutOfSpecification),
        }, async c =>
        {
            var s = await RunAsync(WaterLimits, new[] { c.Item1 }, clientDf: 1m);
            Assert.Equal(c.Item2, s.Reading.Status);
            Assert.Equal($"{c.Item1} CFU/mL ({c.Item2})", s.ResultRow.InterpretedValue);
            Assert.Equal("10", s.Reading.AlertLimit);
            Assert.Equal("50", s.Reading.ActionLimit);
            Assert.Equal("100", s.Reading.SpecLimit);
        });
    }

    // QUESTION: status compares the UNROUNDED calculated value, so 100.4 is reported "100 CFU/mL"
    // (a whole number at the limit) but is saved as OutOfSpecification.
    [PostgresFact]
    public async Task Status_UsesUnroundedValue_ReportedRoundsDownToTheLimitButStatusIsOos()
    {
        var s = await RunAsync(WaterLimits, new[] { "100.4" }, clientDf: 1m);
        AssertNumeric(s, "100 CFU/mL", 100.4m, 100.4m, ResultStatus.OutOfSpecification, 1m, 1m, "100.4", alert: "10", action: "50", spec: "100");
    }

    [PostgresFact]
    public async Task Status_Product_AgainstItemSpecification_TierPrecedenceAndSnapshotOfLimits()
    {
        var scn = new Scn(SampleCategory.FinishedProduct, SpecDf: 10m, SpecUnit: "g", Alert: "NMT 100", Action: "500", Spec: "1000");
        var action = await RunAsync(scn, new[] { "60" }, clientDf: 10m);   // 600 > action 500, < spec 1000
        AssertNumeric(action, "600 CFU/g", 60m, 600m, ResultStatus.ActionLimitExceeded, 10m, 10m, "60", alert: "NMT 100", action: "500", spec: "1000");

        var oos = await RunAsync(scn, new[] { "101" }, clientDf: 10m);     // 1010 > spec 1000
        AssertNumeric(oos, "1010 CFU/g", 101m, 1010m, ResultStatus.OutOfSpecification, 10m, 10m, "101", alert: "NMT 100", action: "500", spec: "1000");
    }

    // Water sample whose point has no SamplingConfiguration for this test: no limits, default unit.
    [PostgresFact]
    public async Task Water_NoSamplingConfiguration_LimitsNotConfigured_SnapshotLimitsNull()
    {
        var s = await RunAsync(WaterNoLimits, new[] { "500" }, clientDf: 1m);
        AssertNumeric(s, "500 CFU/mL", 500m, 500m, ResultStatus.LimitsNotConfigured, 1m, 1m, "500", alert: null, action: null, spec: null);
    }

    // Item with a Specification row whose limits are blank: blank limits are snapshotted as "" and not enforced.
    [PostgresFact]
    public async Task Product_BlankLimits_LimitsNotConfigured_BlankLimitsSnapshotted()
    {
        var scn = new Scn(SampleCategory.FinishedProduct, SpecDf: 10m, SpecUnit: "g");
        var s = await RunAsync(scn, new[] { "50" }, clientDf: 10m);
        AssertNumeric(s, "500 CFU/g", 50m, 500m, ResultStatus.LimitsNotConfigured, 10m, 10m, "50", alert: "", action: "", spec: "");
    }

    // ===================================================================
    // Branch: non-direct sample with no item (e.g. GPT)
    // ===================================================================

    // L797 skipped (ItemId null): no configured DF, no override check, client DF used as typed,
    // no limits, unit falls to GetCfuUnit's default "CFU/unit".
    // QUESTION: is a GPT (or any item-less non-direct) count meant to take the client's DF unchecked?
    [PostgresFact]
    public async Task ItemlessNonDirectSample_ClientDfUsedAsTyped_NoLimits_DefaultUnit()
    {
        var s = await RunAsync(new Scn(SampleCategory.GPT), new[] { "12", "14" }, clientDf: 10m);
        AssertNumeric(s, "130 CFU/unit", 13m, 130m, ResultStatus.LimitsNotConfigured, 10m, null, "12,14");
    }
}
