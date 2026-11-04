using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class HplcInternalStandardTests
{
    private const string Password = "ValidPassword123!";
    private static readonly DateTimeOffset SepFirst = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;
        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private static (FakeTimeProvider TimeProvider, ILabClock Clock) NewClock(DateTimeOffset utcNow)
    {
        var tz = LabClock.ResolveTimeZone("Africa/Cairo");
        var fake = new FakeTimeProvider(utcNow);
        return (fake, new LabClock(fake, tz));
    }

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MicroLimsDbContext(options);
        db.CurrentUserId = 1;
        return db;
    }

    private static async Task<(DocumentSection section, int userId)> SeedAsync(MicroLimsDbContext db)
    {
        var section = TestServiceFactory.EnsureMicroSection(db);
        var role = new Role { Name = "Analyst", Type = RoleType.Analyst, IsActive = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User
        {
            Username = "u_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Test Analyst",
            RoleId = role.Id,
            IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        TestServiceFactory.AssignUserToMicroSection(db, user.Id);
        return (section, user.Id);
    }

    private static async Task<MaterialMasterEntry> AddEntryAsync(
        MicroLimsDbContext db, int sectionId, string code,
        MaterialMasterCategory category = MaterialMasterCategory.ReferenceStandard)
    {
        var entry = new MaterialMasterEntry
        {
            SectionId = sectionId,
            Code = code,
            Name = code,
            Category = category,
            BaseUnit = category == MaterialMasterCategory.ReferenceStandard ? MaterialUnit.Gram : MaterialUnit.Milliliter,
            IsActive = true,
            CreatedByUserId = 1,
            CreatedAt = DateTime.UtcNow,
            LastModifiedByUserId = 1,
            LastModifiedAt = DateTime.UtcNow,
        };
        db.MaterialMasterEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    private static async Task<Material> AddStandardLotAsync(
        MicroLimsDbContext db, int sectionId, MaterialMasterEntry entry,
        decimal purity = 100m, decimal moisturePercent = 0m)
    {
        var lot = new Material
        {
            SectionId = sectionId,
            MaterialType = MaterialType.ReferenceStandard,
            MaterialMasterEntryId = entry.Id,
            MaterialName = entry.Name,
            BatchNumber = "STD-LOT-" + Guid.NewGuid().ToString("N")[..4],
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Gram,
            Purity = purity,
            MoisturePercent = moisturePercent,
            CreatedByUserId = 1,
            LastModifiedByUserId = 1,
        };
        db.Materials.Add(lot);
        await db.SaveChangesAsync();
        return lot;
    }

    private static async Task<SolutionMaster> AddSolutionAsync(
        MicroLimsDbContext db, int sectionId, string name, SolutionType type, MaterialMasterEntry componentEntry)
    {
        var service = TestServiceFactory.SolutionMaster(db);
        var created = await service.CreateAsync(new SaveSolutionMasterRequest(
            name, type, 7, ShelfLifeUnit.Days, "Room temperature", 1000m, "Mix and filter.",
            new List<SolutionComponentInput> { new(componentEntry.Id, 500m, SolutionComponentUnit.Milliliter) },
            SectionId: sectionId), 1);

        return await db.SolutionMasters.FirstAsync(s => s.Id == created.Id);
    }

    private static async Task<SolutionPreparationResponse> PrepareMobilePhaseAsync(
        MicroLimsDbContext db, ILabClock clock, int userId, SolutionMaster mobilePhase, HplcMethod method, MaterialMasterEntry entry)
    {
        var reagentLot = new Material
        {
            SectionId = method.SectionId,
            MaterialType = MaterialType.Chemical,
            MaterialMasterEntryId = entry.Id,
            MaterialName = entry.Name,
            BatchNumber = "LOT-REAG-01",
            ReceivingDate = DateTime.UtcNow.AddDays(-10),
            ExpiryDate = DateTime.UtcNow.AddYears(1),
            QuantityReceived = 100m,
            QuantityRemaining = 100m,
            Unit = MaterialUnit.Milliliter,
            CreatedByUserId = 1,
            LastModifiedByUserId = 1,
        };
        db.Materials.Add(reagentLot);
        await db.SaveChangesAsync();

        var service = TestServiceFactory.SolutionPreparation(db, clock: clock);
        var prep = await service.StartAsync(new StartPreparationRequest(mobilePhase.Id, method.Id), userId);
        await service.SaveAsync(prep.Id,
            new SavePreparationRequest(new List<PreparationComponentInput> { new(prep.Components[0].Id, reagentLot.Id, 5m) }, 1000m, null),
            userId);
        return await service.CompleteAsync(prep.Id, new CompletePreparationRequest(Password, null), userId, null);
    }

    private sealed class TestContext
    {
        public required DocumentSection Section;
        public required int UserId;
        public required ILabClock Clock;
        public required SolutionMaster Diluent;
        public required SolutionMaster MobilePhase;
        public required MaterialMasterEntry StandardEntry;
        public required MaterialMasterEntry ReagentEntry;
        public required Equipment Equipment;
        public required ChromatographyColumn Column;
    }

    private static async Task<TestContext> CreateContextAsync(MicroLimsDbContext db)
    {
        var (_, clock) = NewClock(SepFirst);
        var (section, userId) = await SeedAsync(db);
        var standardEntry = await AddEntryAsync(db, section.Id, "STD-01", MaterialMasterCategory.ReferenceStandard);
        var reagentEntry = await AddEntryAsync(db, section.Id, "REAG-01", MaterialMasterCategory.Reagent);

        var diluent = await AddSolutionAsync(db, section.Id, "Diluent Water", SolutionType.Diluent, reagentEntry);
        var mobilePhase = await AddSolutionAsync(db, section.Id, "Mobile Phase A", SolutionType.MobilePhase, reagentEntry);

        var equipment = new Equipment
        {
            Name = "HPLC-01",
            Code = "HPLC-01",
            Type = EquipmentType.Hplc,
            SectionId = section.Id,
            CalibrationDueDate = DateTime.UtcNow.AddYears(1),
        };
        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();

        var column = new ChromatographyColumn
        {
            Code = "COL-01",
            Name = "C18",
            UspDesignation = "L1",
            SectionId = section.Id,
            IsActive = true,
            CreatedByUserId = 1,
            LastModifiedByUserId = 1,
        };
        column.CompatibleEquipment.Add(equipment);
        db.ChromatographyColumns.Add(column);
        await db.SaveChangesAsync();

        return new TestContext
        {
            Section = section,
            UserId = userId,
            Clock = clock,
            Diluent = diluent,
            MobilePhase = mobilePhase,
            StandardEntry = standardEntry,
            ReagentEntry = reagentEntry,
            Equipment = equipment,
            Column = column
        };
    }

    private static SaveHplcMethodRequest CreateMethodRequest(
        TestContext ctx, string abbr, string? isName,
        HplcTechnique technique = HplcTechnique.Hplc,
        HplcResultMode resultMode = HplcResultMode.Assay,
        int standardInjections = 3, decimal thWtStd = 50m, decimal thWtTest = 100m,
        string? reason = null) =>
        new(
            Name: "Assay Method " + abbr,
            Abbreviation: abbr,
            EffectiveDate: DateTime.UtcNow,
            ColumnDesignation: "L1",
            ColumnLengthMm: 150m,
            ColumnInternalDiameterMm: 4.6m,
            ParticleSizeUm: technique == HplcTechnique.Hplc ? 5m : null,
            ColumnTemperatureC: technique == HplcTechnique.Hplc ? 30m : null,
            ElutionMode: ElutionMode.Isocratic,
            FlowRateMlPerMin: 1m,
            DetectorType: HplcDetectorType.UV,
            InjectionVolumeUl: 20m,
            RunTimeMin: 15m,
            DiluentSolutionId: ctx.Diluent.Id,
            MobilePhases: new List<HplcMobilePhaseInput> { new("A", ctx.MobilePhase.Id, null) },
            GradientSteps: new List<HplcGradientStepInput>(),
            Analytes: new List<HplcAnalyteInput>
            {
                new(null, "Analyte 1", 254m, ctx.StandardEntry.Id,
                    resultMode == HplcResultMode.RelatedSubstances ? 0m : thWtStd,
                    resultMode == HplcResultMode.RelatedSubstances ? 0m : thWtTest,
                    standardInjections, 2.0m, null)
            },
            SectionId: ctx.Section.Id,
            Reason: reason,
            Technique: technique,
            ResultMode: resultMode,
            InternalStandardName: isName,
            ReferenceSolutionPercent: resultMode == HplcResultMode.RelatedSubstances ? 0.1m : null,
            DisregardLimitPercent: resultMode == HplcResultMode.RelatedSubstances ? 0.05m : null,
            Impurities: resultMode == HplcResultMode.RelatedSubstances ? new List<HplcImpurityInput> { new(null, "Imp A", 0.5m, 1m) } : null,
            CarrierGas: technique == HplcTechnique.Gc ? CarrierGas.Nitrogen : null
        );

    [Fact]
    public async Task Method_InternalStandard_OnlyHplcAssay_AndFixedOnUpdate()
    {
        await using var db = NewDb();
        var ctx = await CreateContextAsync(db);
        var service = TestServiceFactory.HplcMethod(db);

        // 1. Refuses IS on GC technique
        var gcReq = CreateMethodRequest(ctx, "GC-IS", "Benzoic acid", technique: HplcTechnique.Gc);
        var exGc = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(gcReq, ctx.UserId));
        Assert.Equal("An internal standard applies to HPLC assay methods only.", exGc.Message);

        // 2. Refuses IS on Related Substances
        var rsReq = CreateMethodRequest(ctx, "RS-IS", "Benzoic acid", resultMode: HplcResultMode.RelatedSubstances);
        var exRs = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(rsReq, ctx.UserId));
        Assert.Equal("An internal standard applies to HPLC assay methods only.", exRs.Message);

        // 3. Name length > 100 chars refused
        var longName = new string('X', 101);
        var longReq = CreateMethodRequest(ctx, "LONG-IS", longName);
        var exLong = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(longReq, ctx.UserId));
        Assert.Equal("Internal standard name cannot exceed 100 characters.", exLong.Message);

        // 4. Success on HPLC Assay, trimmed
        var validReq = CreateMethodRequest(ctx, "HPLC-IS", "  Benzoic acid  ");
        var created = await service.CreateAsync(validReq, ctx.UserId);
        Assert.Equal("Benzoic acid", created.InternalStandardName);

        var stored = await db.HplcMethods.FirstAsync(m => m.Id == created.Id);
        Assert.Equal("Benzoic acid", stored.InternalStandardName);

        // 5. Update: changing internal standard is refused
        var updateWithChangedIs = CreateMethodRequest(ctx, "HPLC-IS", "Other IS", reason: "Change IS");
        var exUpdate = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, updateWithChangedIs, ctx.UserId));
        Assert.Equal("The internal standard can't be changed after the method is created.", exUpdate.Message);

        // Update removing IS is refused
        var updateWithNullIs = CreateMethodRequest(ctx, "HPLC-IS", null, reason: "Remove IS");
        var exNull = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(created.Id, updateWithNullIs, ctx.UserId));
        Assert.Equal("The internal standard can't be changed after the method is created.", exNull.Message);

        // Method created without IS cannot gain IS on update
        var withoutIsReq = CreateMethodRequest(ctx, "NO-IS", null);
        var createdWithoutIs = await service.CreateAsync(withoutIsReq, ctx.UserId);
        var updateAddIs = CreateMethodRequest(ctx, "NO-IS", "Benzoic acid", reason: "Add IS");
        var exAdd = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(createdWithoutIs.Id, updateAddIs, ctx.UserId));
        Assert.Equal("The internal standard can't be changed after the method is created.", exAdd.Message);

        // Minor 4.3: On update of non-HPLC Assay method, mode validator runs before fixed check
        var rsDto = await service.CreateAsync(CreateMethodRequest(ctx, "RS-MTH", null, resultMode: HplcResultMode.RelatedSubstances), ctx.UserId);
        var exUpdateRs = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(rsDto.Id, CreateMethodRequest(ctx, "RS-MTH", "Benzoic acid", resultMode: HplcResultMode.RelatedSubstances, reason: "Change IS"), ctx.UserId));
        Assert.Equal("An internal standard applies to HPLC assay methods only.", exUpdateRs.Message);
    }

    [Fact]
    public async Task Sst_WithInternalStandard_MeanAndRsdUseRatios()
    {
        await using var db = NewDb();
        var ctx = await CreateContextAsync(db);
        var methodSvc = TestServiceFactory.HplcMethod(db);

        var methodDto = await methodSvc.CreateAsync(CreateMethodRequest(ctx, "SST-IS", "Benzoic acid", standardInjections: 3, thWtStd: 50m), ctx.UserId);
        var method = await db.HplcMethods.Include(m => m.Analytes).FirstAsync(m => m.Id == methodDto.Id);
        var prep = await PrepareMobilePhaseAsync(db, ctx.Clock, ctx.UserId, ctx.MobilePhase, method, ctx.ReagentEntry);
        var stdLot = await AddStandardLotAsync(db, ctx.Section.Id, ctx.StandardEntry, purity: 100m, moisturePercent: 0m);

        var runSvc = TestServiceFactory.HplcRun(db, clock: ctx.Clock);
        var run = await runSvc.StartRunAsync(new StartHplcRunRequest(ctx.Equipment.Id, method.Id, ctx.Column.Id,
            new List<HplcMobilePhaseAssignmentInput> { new("A", prep.Id) }), ctx.UserId);

        var sstAnalyteId = run.Sst!.Analytes[0].Id;

        // Brief: responses [50000, 50500, 49500], IS [100000, 101000, 99000] → MeanResponse 0.5, computed RSD 0.0.
        var saveRequest = new SaveSstRequest(new List<SaveSstAnalyteInput>
        {
            new(sstAnalyteId, stdLot.Id, 50m,
                new List<decimal> { 50000m, 50500m, 49500m },
                null, null, null, null, null, null, null,
                new List<decimal> { 100000m, 101000m, 99000m })
        });

        var runAfterSave = await runSvc.SaveSstAsync(run.Id, saveRequest, ctx.UserId);
        var sstAnalyteAfterSave = runAfterSave.Sst!.Analytes[0];
        Assert.Equal(0.5m, sstAnalyteAfterSave.MeanResponse);
        Assert.Equal(0.0m, sstAnalyteAfterSave.ComputedRsdPercent);

        // Upload standard report so ConfirmSstAsync is allowed
        var reportContent = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        await runSvc.UploadEvidenceAsync(run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport,
            "report.pdf", "application/pdf", reportContent, ctx.UserId);

        var runAfterConfirm = await runSvc.ConfirmSstAsync(run.Id, new ConfirmSstRequest(Password, null), ctx.UserId, null);
        var sstAnalyteAfterConfirm = runAfterConfirm.Sst!.Analytes[0];
        Assert.True(sstAnalyteAfterConfirm.Passed);
        Assert.Equal(0.5m, sstAnalyteAfterConfirm.MeanResponse);
        Assert.Equal(0.0m, sstAnalyteAfterConfirm.ComputedRsdPercent);

        // Verify stored raw responses in DB
        var storedInjections = await db.HplcSstInjections
            .Where(i => i.HplcSstAnalyteId == sstAnalyteId)
            .OrderBy(i => i.InjectionNo)
            .ToListAsync();
        Assert.Equal(3, storedInjections.Count);
        Assert.Equal(50000m, storedInjections[0].Response);
        Assert.Equal(100000m, storedInjections[0].InternalStandardResponse);
        Assert.Equal(50500m, storedInjections[1].Response);
        Assert.Equal(101000m, storedInjections[1].InternalStandardResponse);
        Assert.Equal(49500m, storedInjections[2].Response);
        Assert.Equal(99000m, storedInjections[2].InternalStandardResponse);

        // Verify DTO returns stored IS responses
        Assert.Equal(100000m, sstAnalyteAfterConfirm.Injections[0].InternalStandardResponse);
        Assert.Equal(101000m, sstAnalyteAfterConfirm.Injections[1].InternalStandardResponse);
        Assert.Equal(99000m, sstAnalyteAfterConfirm.Injections[2].InternalStandardResponse);
    }

    [Fact]
    public async Task Sample_WithInternalStandard_AssayFromRatio()
    {
        await using var db = NewDb();
        var ctx = await CreateContextAsync(db);
        var methodSvc = TestServiceFactory.HplcMethod(db);

        // std weight = Th.Wt std (50 mg), purity 100, moisture 0, sample weight = Th.Wt test (100 mg), sample 49000 / IS 100000 → assay "98.0 %"
        var methodDto = await methodSvc.CreateAsync(CreateMethodRequest(ctx, "SMP-IS", "Benzoic acid",
            standardInjections: 3, thWtStd: 50m, thWtTest: 100m), ctx.UserId);
        var method = await db.HplcMethods.Include(m => m.Analytes).FirstAsync(m => m.Id == methodDto.Id);
        var analyte = method.Analytes[0];
        var prep = await PrepareMobilePhaseAsync(db, ctx.Clock, ctx.UserId, ctx.MobilePhase, method, ctx.ReagentEntry);
        var stdLot = await AddStandardLotAsync(db, ctx.Section.Id, ctx.StandardEntry, purity: 100m, moisturePercent: 0m);

        var runSvc = TestServiceFactory.HplcRun(db, clock: ctx.Clock);
        var run = await runSvc.StartRunAsync(new StartHplcRunRequest(ctx.Equipment.Id, method.Id, ctx.Column.Id,
            new List<HplcMobilePhaseAssignmentInput> { new("A", prep.Id) }), ctx.UserId);

        var sstAnalyteId = run.Sst!.Analytes[0].Id;
        await runSvc.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput>
        {
            new(sstAnalyteId, stdLot.Id, 50m,
                new List<decimal> { 50000m, 50500m, 49500m },
                null, null, null, null, null, null, null,
                new List<decimal> { 100000m, 101000m, 99000m })
        }), ctx.UserId);

        var reportContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        await runSvc.UploadEvidenceAsync(run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport,
            "sst-report.pdf", "application/pdf", reportContent, ctx.UserId);

        await runSvc.ConfirmSstAsync(run.Id, new ConfirmSstRequest(Password, null), ctx.UserId, null);

        // Add test definition and item
        var testDef = new TestDefinition
        {
            Code = "TEST-IS",
            DisplayName = "IS Assay Test",
            SectionId = ctx.Section.Id,
            WorkflowType = WorkflowType.HplcMethodAssay,
            EquationType = EquationType.HplcMethodAssay,
            RequiresSystemSuitability = true,
            HplcMethodId = method.Id,
            MethodAbbreviation = method.Abbreviation,
            IsActive = true,
        };
        db.TestDefinitions.Add(testDef);

        var item = new Item { Code = "ITM-IS", Name = "IS Test Item", Category = SampleCategory.FinishedProduct, IsActive = true };
        db.Items.Add(item);

        var stage = new ProductionStage { Name = "Finished", Role = ProductionStageRole.Finished, IsActive = true };
        db.ProductionStages.Add(stage);

        var spec = new Specification
        {
            ItemId = item.Id,
            TestCode = testDef.Code,
            ParameterName = analyte.Name,
            HplcMethodAnalyteId = analyte.Id,
            ResultBasis = ResultBasis.PercentLabelClaim,
            LimitType = LimitType.Range,
            LowerLimit = 95m,
            UpperLimit = 105m,
            Unit = "%",
            DisplayOrder = 1
        };
        db.Specifications.Add(spec);
        await db.SaveChangesAsync();

        db.TestDefinitionStageReplicates.Add(new TestDefinitionStageReplicate
        {
            TestDefinitionId = testDef.Id,
            Role = ProductionStageRole.Finished,
            SampleReplicates = 1
        });
        var sample = new Sample
        {
            ReferenceNumber = "SMP-IS-01",
            ItemId = item.Id,
            Category = SampleCategory.FinishedProduct,
            BatchNumber = "B-001",
            ReceivedAt = DateTime.UtcNow,
            CauseOfTesting = new CauseOfTesting { Name = "Release", IsActive = true },
            ReceivedByUserId = ctx.UserId,
            ProductionStageId = stage.Id,
            ProductionStageRef = stage,
            Status = SampleStatus.InTesting,
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var order = new TestOrder
        {
            SampleId = sample.Id,
            TestCode = testDef.Code,
            SectionId = ctx.Section.Id,
            Status = ApprovalStatus.InProgress,
            CurrentStep = WorkflowStep.Waiting,
            IsSuperseded = false,
        };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        // Assign sample to run
        await runSvc.AssignSamplesAsync(run.Id, new List<int> { order.Id }, ctx.UserId);
        var runSample = await db.HplcRunSamples.FirstAsync(s => s.HplcRunId == run.Id && s.TestOrderId == order.Id);

        // Save replicate: sample weight = Th.Wt test (100 mg), sample response = 49000, IS response = 100000
        await runSvc.SaveReplicatesAsync(runSample.Id, new SaveReplicatesRequest(new List<HplcReplicateInput>
        {
            new(100m, new List<HplcReplicateResponseInput> { new(analyte.Id, 49000m) }, null, 100000m)
        }), ctx.UserId);

        // Preview: should give "98.0 %"
        var entryDto = await runSvc.GetSampleEntryAsync(runSample.Id, ctx.UserId);
        Assert.False(entryDto.CanSubmit);
        Assert.Equal("Upload the sample report before sending for review.", entryDto.CanSubmitReason);
        var preview = Assert.Single(entryDto.Preview);
        Assert.Equal("98.0 %", preview.Display);
        Assert.Equal(98.0m, Math.Round(preview.Value, 1));
        Assert.Equal(ResultStatus.WithinLimits, preview.Status);

        // Raw values stored in DB unchanged
        var storedRep = await db.HplcSampleReplicates
            .Include(r => r.Responses)
            .FirstAsync(r => r.HplcRunSampleId == runSample.Id);
        Assert.Equal(100m, storedRep.ActualWeightMg);
        Assert.Equal(100000m, storedRep.InternalStandardResponse);
        Assert.Equal(49000m, storedRep.Responses.First().Response);

        // Upload sample report & submit
        await runSvc.UploadEvidenceAsync(run.Id, runSample.Id, HplcEvidenceContext.Sample, HplcEvidenceKind.SampleReport,
            "sample-report.pdf", "application/pdf", reportContent, ctx.UserId);

        entryDto = await runSvc.GetSampleEntryAsync(runSample.Id, ctx.UserId);
        Assert.True(entryDto.CanSubmit);
        Assert.Null(entryDto.CanSubmitReason);

        await TestServiceFactory.TestWorkflow(db, clock: ctx.Clock).SubmitHplcMethodAssayAsync(runSample.Id, Password, null, ctx.UserId, "127.0.0.1");

        var paramResult = await db.ParameterResults
            .Include(p => p.Readings)
            .FirstAsync(p => p.TestOrderId == order.Id);
        Assert.Equal("98.0 %", paramResult.ReportedDisplay);
        var reading = Assert.Single(paramResult.Readings);
        // Value1 is the effective response ratio (49000 / 100000 = 0.49)
        Assert.Equal(0.49m, reading.Value1);
        Assert.Equal(98.0m, Math.Round(reading.ComputedValue!.Value, 1));

        // Task 4 Fix 1: Signed CalculationJson contains IS name, ratio, raw responses, and standardMeanRatio
        using var doc = JsonDocument.Parse(paramResult.CalculationJson!);
        var root = doc.RootElement;
        Assert.Equal("Benzoic acid", root.GetProperty("internalStandardName").GetString());
        Assert.Equal(0.5m, root.GetProperty("standardMeanRatio").GetDecimal());
        Assert.False(root.TryGetProperty("standardMeanResponse", out _));

        var reps = root.GetProperty("replicates");
        Assert.Equal(1, reps.GetArrayLength());
        var rep0 = reps[0];
        Assert.Equal(49000m, rep0.GetProperty("response").GetDecimal());
        Assert.Equal(100000m, rep0.GetProperty("internalStandardResponse").GetDecimal());
        Assert.Equal(0.49m, rep0.GetProperty("ratio").GetDecimal());
    }

    [Fact]
    public async Task Refusals_InternalStandardValidations()
    {
        await using var db = NewDb();
        var ctx = await CreateContextAsync(db);
        var methodSvc = TestServiceFactory.HplcMethod(db);

        // Method WITH internal standard
        var methodDto = await methodSvc.CreateAsync(CreateMethodRequest(ctx, "REF-IS", "Benzoic acid", standardInjections: 3), ctx.UserId);
        var method = await db.HplcMethods.Include(m => m.Analytes).FirstAsync(m => m.Id == methodDto.Id);
        var analyte = method.Analytes[0];
        var prep = await PrepareMobilePhaseAsync(db, ctx.Clock, ctx.UserId, ctx.MobilePhase, method, ctx.ReagentEntry);
        var stdLot = await AddStandardLotAsync(db, ctx.Section.Id, ctx.StandardEntry);

        var runSvc = TestServiceFactory.HplcRun(db, clock: ctx.Clock);
        var run = await runSvc.StartRunAsync(new StartHplcRunRequest(ctx.Equipment.Id, method.Id, ctx.Column.Id,
            new List<HplcMobilePhaseAssignmentInput> { new("A", prep.Id) }), ctx.UserId);
        var sstAnalyteId = run.Sst!.Analytes[0].Id;

        // 1. IS responses missing at SST → "{analyte}: enter an internal standard response (above zero) for every injection."
        var exMissing = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runSvc.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput>
            {
                new(sstAnalyteId, stdLot.Id, 50m, new List<decimal> { 50000m, 50000m, 50000m }, null, null, null, null, null, null, null,
                    InternalStandardResponses: null)
            }), ctx.UserId));
        Assert.Equal($"{analyte.Name}: enter an internal standard response (above zero) for every injection.", exMissing.Message);

        // 2. IS responses wrong length at SST
        var exWrongLength = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runSvc.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput>
            {
                new(sstAnalyteId, stdLot.Id, 50m, new List<decimal> { 50000m, 50000m, 50000m }, null, null, null, null, null, null, null,
                    InternalStandardResponses: new List<decimal> { 100000m, 100000m })
            }), ctx.UserId));
        Assert.Equal($"{analyte.Name}: enter an internal standard response (above zero) for every injection.", exWrongLength.Message);

        // 3. IS response <= 0 at SST
        var exZero = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runSvc.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput>
            {
                new(sstAnalyteId, stdLot.Id, 50m, new List<decimal> { 50000m, 50000m, 50000m }, null, null, null, null, null, null, null,
                    InternalStandardResponses: new List<decimal> { 100000m, 0m, 100000m })
            }), ctx.UserId));
        Assert.Equal($"{analyte.Name}: enter an internal standard response (above zero) for every injection.", exZero.Message);

        // Minor 4.2: Empty injection list with IS method saves draft
        await runSvc.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput>
        {
            new(sstAnalyteId, stdLot.Id, 50m, new List<decimal>(), null, null, null, null, null, null, null)
        }), ctx.UserId);

        // Complete SST properly so we can test sample replicate entry
        await runSvc.SaveSstAsync(run.Id, new SaveSstRequest(new List<SaveSstAnalyteInput>
        {
            new(sstAnalyteId, stdLot.Id, 50m, new List<decimal> { 50000m, 50000m, 50000m }, null, null, null, null, null, null, null,
                InternalStandardResponses: new List<decimal> { 100000m, 100000m, 100000m })
        }), ctx.UserId);
        var reportContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        await runSvc.UploadEvidenceAsync(run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport,
            "report.pdf", "application/pdf", reportContent, ctx.UserId);
        await runSvc.ConfirmSstAsync(run.Id, new ConfirmSstRequest(Password, null), ctx.UserId, null);

        // Set up test definition, item, spec, sample, order
        var testDef = new TestDefinition
        {
            Code = "TEST-REF-IS", DisplayName = "Ref Test", SectionId = ctx.Section.Id,
            WorkflowType = WorkflowType.HplcMethodAssay, EquationType = EquationType.HplcMethodAssay,
            RequiresSystemSuitability = true, HplcMethodId = method.Id, MethodAbbreviation = method.Abbreviation, IsActive = true,
        };
        db.TestDefinitions.Add(testDef);
        var item = new Item { Code = "ITM-REF", Name = "Ref Item", Category = SampleCategory.FinishedProduct, IsActive = true };
        db.Items.Add(item);
        var stage = new ProductionStage { Name = "Finished", Role = ProductionStageRole.Finished, IsActive = true };
        db.ProductionStages.Add(stage);
        db.Specifications.Add(new Specification
        {
            ItemId = item.Id, TestCode = testDef.Code, ParameterName = analyte.Name, HplcMethodAnalyteId = analyte.Id,
            ResultBasis = ResultBasis.PercentLabelClaim, LimitType = LimitType.Range, LowerLimit = 95m, UpperLimit = 105m, Unit = "%", DisplayOrder = 1
        });
        await db.SaveChangesAsync();

        db.TestDefinitionStageReplicates.Add(new TestDefinitionStageReplicate { TestDefinitionId = testDef.Id, Role = ProductionStageRole.Finished, SampleReplicates = 1 });
        var sample = new Sample
        {
            ReferenceNumber = "SMP-REF-01", ItemId = item.Id, Category = SampleCategory.FinishedProduct, BatchNumber = "B-001",
            ReceivedAt = DateTime.UtcNow, CauseOfTesting = new CauseOfTesting { Name = "Release", IsActive = true },
            ReceivedByUserId = ctx.UserId, ProductionStageId = stage.Id, ProductionStageRef = stage, Status = SampleStatus.InTesting,
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        var order = new TestOrder { SampleId = sample.Id, TestCode = testDef.Code, SectionId = ctx.Section.Id, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Waiting, IsSuperseded = false };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        await runSvc.AssignSamplesAsync(run.Id, new List<int> { order.Id }, ctx.UserId);
        var runSample = await db.HplcRunSamples.FirstAsync(s => s.HplcRunId == run.Id && s.TestOrderId == order.Id);

        // 4. Replicate IS missing or <= 0 refused at save time
        var exNullRep = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runSvc.SaveReplicatesAsync(runSample.Id, new SaveReplicatesRequest(new List<HplcReplicateInput>
            {
                new(100m, new List<HplcReplicateResponseInput> { new(analyte.Id, 49000m) }, null, InternalStandardResponse: null)
            }), ctx.UserId));
        Assert.Equal("Replicate 1: the internal standard response is required.", exNullRep.Message);

        var exZeroRep = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runSvc.SaveReplicatesAsync(runSample.Id, new SaveReplicatesRequest(new List<HplcReplicateInput>
            {
                new(100m, new List<HplcReplicateResponseInput> { new(analyte.Id, 49000m) }, null, InternalStandardResponse: 0m)
            }), ctx.UserId));
        Assert.Equal("Replicate 1: the internal standard response is required.", exZeroRep.Message);

        await runSvc.AbandonRunAsync(run.Id, "Finished first part", ctx.UserId);

        // 5. IS values sent to a method without an IS → "This method has no internal standard."
        var noIsDto = await methodSvc.CreateAsync(CreateMethodRequest(ctx, "NO-IS-MTH", null), ctx.UserId);
        var noIsMethod = await db.HplcMethods.Include(m => m.Analytes).FirstAsync(m => m.Id == noIsDto.Id);
        var noIsPrep = await PrepareMobilePhaseAsync(db, ctx.Clock, ctx.UserId, ctx.MobilePhase, noIsMethod, ctx.ReagentEntry);

        var noIsRun = await runSvc.StartRunAsync(new StartHplcRunRequest(ctx.Equipment.Id, noIsMethod.Id, ctx.Column.Id,
            new List<HplcMobilePhaseAssignmentInput> { new("A", noIsPrep.Id) }), ctx.UserId);
        var noIsSstAnalyteId = noIsRun.Sst!.Analytes[0].Id;

        // Send IS responses at SST
        var exNoIsSst = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runSvc.SaveSstAsync(noIsRun.Id, new SaveSstRequest(new List<SaveSstAnalyteInput>
            {
                new(noIsSstAnalyteId, stdLot.Id, 50m, new List<decimal> { 50000m, 50000m, 50000m }, null, null, null, null, null, null, null,
                    InternalStandardResponses: new List<decimal> { 100000m, 100000m, 100000m })
            }), ctx.UserId));
        Assert.Equal("This method has no internal standard.", exNoIsSst.Message);

        // Complete SST for no-IS run
        await runSvc.SaveSstAsync(noIsRun.Id, new SaveSstRequest(new List<SaveSstAnalyteInput>
        {
            new(noIsSstAnalyteId, stdLot.Id, 50m, new List<decimal> { 50000m, 50000m, 50000m }, null, null, null, null, null, null, null)
        }), ctx.UserId);
        await runSvc.UploadEvidenceAsync(noIsRun.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport,
            "report2.pdf", "application/pdf", reportContent, ctx.UserId);
        await runSvc.ConfirmSstAsync(noIsRun.Id, new ConfirmSstRequest(Password, null), ctx.UserId, null);

        // Assign order to no-IS run and try to save replicate with IS response
        var testDef2 = new TestDefinition
        {
            Code = "TEST-NO-IS", DisplayName = "No IS Test", SectionId = ctx.Section.Id,
            WorkflowType = WorkflowType.HplcMethodAssay, EquationType = EquationType.HplcMethodAssay,
            RequiresSystemSuitability = true, HplcMethodId = noIsMethod.Id, MethodAbbreviation = noIsMethod.Abbreviation, IsActive = true,
        };
        db.TestDefinitions.Add(testDef2);
        await db.SaveChangesAsync();

        db.TestDefinitionStageReplicates.Add(new TestDefinitionStageReplicate { TestDefinitionId = testDef2.Id, Role = ProductionStageRole.Finished, SampleReplicates = 1 });
        var sample2 = new Sample
        {
            ReferenceNumber = "SMP-NOIS-01", ItemId = item.Id, Category = SampleCategory.FinishedProduct, BatchNumber = "B-002",
            ReceivedAt = DateTime.UtcNow, CauseOfTesting = new CauseOfTesting { Name = "Release", IsActive = true },
            ReceivedByUserId = ctx.UserId, ProductionStageId = stage.Id, ProductionStageRef = stage, Status = SampleStatus.InTesting,
        };
        db.Samples.Add(sample2);
        await db.SaveChangesAsync();
        var order2 = new TestOrder { SampleId = sample2.Id, TestCode = testDef2.Code, SectionId = ctx.Section.Id, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Waiting, IsSuperseded = false };
        db.TestOrders.Add(order2);
        await db.SaveChangesAsync();

        await runSvc.AssignSamplesAsync(noIsRun.Id, new List<int> { order2.Id }, ctx.UserId);
        var noIsRunSample = await db.HplcRunSamples.FirstAsync(s => s.HplcRunId == noIsRun.Id && s.TestOrderId == order2.Id);

        var exNoIsRep = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runSvc.SaveReplicatesAsync(noIsRunSample.Id, new SaveReplicatesRequest(new List<HplcReplicateInput>
            {
                new(100m, new List<HplcReplicateResponseInput> { new(noIsMethod.Analytes[0].Id, 49000m) }, null, InternalStandardResponse: 100000m)
            }), ctx.UserId));
        Assert.Equal("This method has no internal standard.", exNoIsRep.Message);
    }

    [Fact]
    public void HplcResponse_Effective_AndHelpers_ValidateInternalStandard()
    {
        // Non-IS method returns raw response
        Assert.Equal(50000m, HplcResponse.Effective(50000m, null, hasInternalStandard: false));
        Assert.Equal(50000m, HplcResponse.Effective(50000m, 0m, hasInternalStandard: false));

        // IS method computes ratio
        Assert.Equal(0.5m, HplcResponse.Effective(50000m, 100000m, hasInternalStandard: true));

        // IS method refuses null or <= 0
        var exNull = Assert.Throws<InvalidOperationException>(() =>
            HplcResponse.Effective(50000m, null, hasInternalStandard: true));
        Assert.NotNull(exNull.Message);

        var exZero = Assert.Throws<InvalidOperationException>(() =>
            HplcResponse.Effective(50000m, 0m, hasInternalStandard: true));
        Assert.NotNull(exZero.Message);

        var exNeg = Assert.Throws<InvalidOperationException>(() =>
            HplcResponse.Effective(50000m, -1m, hasInternalStandard: true));
        Assert.NotNull(exNeg.Message);

        // Helper for SST throws exact SST message
        var exSst = Assert.Throws<InvalidOperationException>(() =>
            HplcResponse.ForSst("Analyte 1", 50000m, null, hasInternalStandard: true));
        Assert.Equal("Analyte 1: enter an internal standard response (above zero) for every injection.", exSst.Message);

        // Helper for Replicate throws exact Replicate message
        var exRep = Assert.Throws<InvalidOperationException>(() =>
            HplcResponse.ForReplicate(2, 50000m, 0m, hasInternalStandard: true));
        Assert.Equal("Replicate 2: the internal standard response is required.", exRep.Message);
    }

    [Fact]
    public async Task Sst_MissingInternalStandardInjections_ReportedAndConfirmRefused()
    {
        await using var db = NewDb();
        var ctx = await CreateContextAsync(db);
        var methodSvc = TestServiceFactory.HplcMethod(db);

        var methodDto = await methodSvc.CreateAsync(CreateMethodRequest(ctx, "SST-IS-MISS", "Benzoic acid", standardInjections: 3, thWtStd: 50m), ctx.UserId);
        var method = await db.HplcMethods.Include(m => m.Analytes).FirstAsync(m => m.Id == methodDto.Id);
        var prep = await PrepareMobilePhaseAsync(db, ctx.Clock, ctx.UserId, ctx.MobilePhase, method, ctx.ReagentEntry);
        var stdLot = await AddStandardLotAsync(db, ctx.Section.Id, ctx.StandardEntry, purity: 100m, moisturePercent: 0m);

        var runSvc = TestServiceFactory.HplcRun(db, clock: ctx.Clock);
        var run = await runSvc.StartRunAsync(new StartHplcRunRequest(ctx.Equipment.Id, method.Id, ctx.Column.Id,
            new List<HplcMobilePhaseAssignmentInput> { new("A", prep.Id) }), ctx.UserId);

        var sstAnalyte = await db.HplcSstAnalytes.Include(a => a.Injections).FirstAsync(a => a.HplcSstRecordId == run.Sst!.Id);
        sstAnalyte.StandardMaterialId = stdLot.Id;
        sstAnalyte.StandardWeightMg = 50m;
        sstAnalyte.Injections.Add(new HplcSstInjection { InjectionNo = 1, Response = 50000m, InternalStandardResponse = null });
        sstAnalyte.Injections.Add(new HplcSstInjection { InjectionNo = 2, Response = 50000m, InternalStandardResponse = null });
        sstAnalyte.Injections.Add(new HplcSstInjection { InjectionNo = 3, Response = 50000m, InternalStandardResponse = null });
        await db.SaveChangesAsync();

        var runDto = await runSvc.GetRunAsync(run.Id, ctx.UserId);
        Assert.False(runDto.CanConfirmSst);
        Assert.Contains("internal standard", runDto.CanConfirmSstReason);

        var reportContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        await runSvc.UploadEvidenceAsync(run.Id, null, HplcEvidenceContext.Sst, HplcEvidenceKind.StandardReport,
            "report.pdf", "application/pdf", reportContent, ctx.UserId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runSvc.ConfirmSstAsync(run.Id, new ConfirmSstRequest(Password, null), ctx.UserId, null));
        Assert.Contains("internal standard", ex.Message);
    }

    private static bool entryDtoHasCanSubmit(HplcSampleEntryDto dto) => dto.CanSubmit;
}
