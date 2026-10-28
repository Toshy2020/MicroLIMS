using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.DTOs.Responses;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using Xunit;

namespace MicroLIMS.Tests.UnitTests;

public class HplcDissolutionStandardTests
{
    private static MicroLimsDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MicroLimsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { CurrentUserId = 1 };

    // Seeds a run (method snapshot with one analyte), its SST and one assigned
    // run sample for the test order. Returns the run so tests can alter it.
    // The id parameters default to InMemory placeholders; the Postgres tests
    // pass real rows (foreign keys are enforced there).
    internal static HplcRun SeedRun(MicroLimsDbContext db, int testOrderId = 100,
        HplcSstStatus sst = HplcSstStatus.Passed, HplcRunStatus runStatus = HplcRunStatus.Open,
        decimal? dilution = 2500m, int analyteCount = 1,
        int sectionId = 1, int equipmentId = 7, int columnId = 1, int methodId = 1, int userId = 1,
        int firstAnalyteId = 1, string codeSuffix = "", string? internalStandardName = null)
    {
        var analytes = Enumerable.Range(0, analyteCount).Select(i => new HplcMethodAnalyteResponse(
            firstAnalyteId + i, i + 1, $"Analyte {i + 1}", 290m, 1, "STD", 50m, 50m, 5,
            null, null, null, null, null, null, null, dilution)).ToList();
        var run = new HplcRun
        {
            SectionId = sectionId, Code = "SILD RUN 01/102026" + codeSuffix, EquipmentId = equipmentId, ChromatographyColumnId = columnId, HplcMethodId = methodId,
            MethodSnapshotJson = JsonSerializer.Serialize(new HplcMethodResponse { Id = methodId, Name = "M", Abbreviation = "SILD", ColumnDesignation = "L1", Analytes = analytes, InternalStandardName = internalStandardName }, SnapshotJson.Options),
            AnalystUserId = userId, StartedAt = DateTime.UtcNow, Status = runStatus,
            Sst = new HplcSstRecord
            {
                Code = "SILD S.S 01/102026" + codeSuffix, Status = sst,
                Analytes = analytes.Select(a => new HplcSstAnalyte
                {
                    HplcMethodAnalyteId = a.Id, AnalyteName = a.Name,
                    StandardWeightMg = 50m, StandardPurityPercent = 100m, StandardMoisturePercent = 0m, MeanResponse = 0.500m
                }).ToList()
            },
            Samples = { new HplcRunSample { TestOrderId = testOrderId, Status = HplcRunSampleStatus.Assigned, AssignedAt = DateTime.UtcNow, AssignedByUserId = userId } }
        };
        db.HplcRuns.Add(run);
        db.SaveChanges();
        return run;
    }

    [Fact]
    public async Task Standard_FromAssignedPassedRun()
    {
        await using var db = NewDb();
        var run = SeedRun(db);

        var result = await HplcDissolutionStandard.ResolveAsync(db, 100);

        Assert.Null(result.Problem);
        Assert.Equal(run.Id, result.Standard!.HplcRunId);
        Assert.Equal(7, result.Standard.EquipmentId);
        Assert.Equal(0.02m, result.Standard.Cs); // 50 x 1.0 x 1.0 / 2500
    }

    [Fact]
    public async Task Standard_NotAssigned_IsMissing()
    {
        await using var db = NewDb();
        Assert.Equal(HplcDissolutionStandard.NotAssigned, (await HplcDissolutionStandard.ResolveAsync(db, 100)).Problem);
    }

    [Fact]
    public async Task Standard_RemovedSample_IsMissing()
    {
        await using var db = NewDb();
        var run = SeedRun(db);
        run.Samples[0].Status = HplcRunSampleStatus.Removed;
        db.SaveChanges();
        Assert.Equal(HplcDissolutionStandard.NotAssigned, (await HplcDissolutionStandard.ResolveAsync(db, 100)).Problem);
    }

    [Fact]
    public async Task Standard_AbandonedRun_IsMissing()
    {
        await using var db = NewDb();
        SeedRun(db, runStatus: HplcRunStatus.Abandoned);
        Assert.Equal(HplcDissolutionStandard.NotAssigned, (await HplcDissolutionStandard.ResolveAsync(db, 100)).Problem);
    }

    [Fact]
    public async Task Standard_SstNotPassed_IsMissing()
    {
        await using var db = NewDb();
        SeedRun(db, sst: HplcSstStatus.Pending);
        Assert.Contains("has not passed", (await HplcDissolutionStandard.ResolveAsync(db, 100)).Problem);
    }

    [Fact]
    public async Task Standard_NoDilution_IsMissing()
    {
        await using var db = NewDb();
        SeedRun(db, dilution: null);
        Assert.Contains("standard dilution", (await HplcDissolutionStandard.ResolveAsync(db, 100)).Problem);
    }

    [Fact]
    public async Task Standard_WithInternalStandard_IsRefused()
    {
        await using var db = NewDb();
        SeedRun(db, internalStandardName: "Benzoic acid");
        var result = await HplcDissolutionStandard.ResolveAsync(db, 100);
        Assert.Equal("Dissolution does not support internal-standard methods.", result.Problem);
    }

    [Fact]
    public async Task MasterData_DissolutionWithInternalStandardMethod_IsRefused()
    {
        await using var db = NewDb();
        var section = TestServiceFactory.EnsureMicroSection(db);
        TestServiceFactory.AssignUserToMicroSection(db, 1);
        var method = new HplcMethod { Name = "M-IS", Abbreviation = "MIS", SectionId = section.Id, Technique = HplcTechnique.Hplc, ResultMode = HplcResultMode.Assay, InternalStandardName = "Benzoic acid", IsActive = true };
        db.HplcMethods.Add(method);
        await db.SaveChangesAsync();

        var master = TestServiceFactory.TestDefinitionMaster(db);
        var exCreate = await Assert.ThrowsAsync<InvalidOperationException>(() => master.CreateTestDefinitionAsync(1, new CreateTestDefinitionRequest(
            Code: "DISS-IS", DisplayName: "Dissolution IS", SectionId: section.Id, WorkflowType: WorkflowType.Dissolution, EquationType: EquationType.Dissolution, RequiresSystemSuitability: true, MethodAbbreviation: "MIS", HplcMethodId: method.Id)));
        Assert.Equal("Dissolution does not support internal-standard methods.", exCreate.Message);
    }

    [Fact]
    public async Task MasterData_UpdateDissolutionWithInternalStandardMethod_IsRefused()
    {
        await using var db = NewDb();
        var section = TestServiceFactory.EnsureMicroSection(db);
        TestServiceFactory.AssignUserToMicroSection(db, 1);
        var method = new HplcMethod { Name = "M-IS", Abbreviation = "MIS", SectionId = section.Id, Technique = HplcTechnique.Hplc, ResultMode = HplcResultMode.Assay, InternalStandardName = "Benzoic acid", IsActive = true };
        var testDef = new TestDefinition
        {
            Code = "DISS-1", DisplayName = "Diss 1", SectionId = section.Id,
            WorkflowType = WorkflowType.Dissolution, EquationType = EquationType.Dissolution,
            RequiresSystemSuitability = true, MethodAbbreviation = "MIS", IsActive = true,
            DissolutionS1Offset = 5m, DissolutionS2MinOffset = 15m, DissolutionS3MinOffset = 25m, DissolutionS3MaxBelowS2Min = 2m
        };
        db.HplcMethods.Add(method);
        db.TestDefinitions.Add(testDef);
        await db.SaveChangesAsync();

        var master = TestServiceFactory.TestDefinitionMaster(db);
        var exUpdate = await Assert.ThrowsAsync<InvalidOperationException>(() => master.UpdateTestDefinitionAsync(testDef.Id, 1, new UpdateTestDefinitionRequest(
            Code: "DISS-1", DisplayName: "Diss 1", RequiresSystemSuitability: true, MethodAbbreviation: "MIS", HplcMethodId: method.Id)));
        Assert.Equal("Dissolution does not support internal-standard methods.", exUpdate.Message);
    }

    [Fact]
    public async Task AssignSamples_DissolutionOrderToIsRun_IsRefused()
    {
        await using var db = NewDb();
        var section = TestServiceFactory.EnsureMicroSection(db);
        TestServiceFactory.AssignUserToMicroSection(db, 1);
        var method = new HplcMethod { Name = "M-IS2", Abbreviation = "MIS2", SectionId = section.Id, Technique = HplcTechnique.Hplc, ResultMode = HplcResultMode.Assay, InternalStandardName = "Benzoic acid", IsActive = true };
        db.HplcMethods.Add(method);
        await db.SaveChangesAsync();

        var testDef = new TestDefinition { Code = "DISS-2", DisplayName = "Diss 2", SectionId = section.Id, WorkflowType = WorkflowType.Dissolution, EquationType = EquationType.Dissolution, RequiresSystemSuitability = true, MethodAbbreviation = "MIS2", HplcMethodId = method.Id, IsActive = true };
        var sample = new Sample { ReferenceNumber = "SMP-DISS-1", ItemId = 1, Category = SampleCategory.FinishedProduct, BatchNumber = "B-01", ReceivedAt = DateTime.UtcNow, ReceivedByUserId = 1 };
        var eq = new Equipment { Id = 7, Name = "HPLC-07", Code = "HPLC-07", Type = EquipmentType.Hplc, SectionId = section.Id };
        var col = new ChromatographyColumn { Code = "COL-01", Name = "C18", SectionId = section.Id, IsActive = true, CreatedByUserId = 1, LastModifiedByUserId = 1 };
        col.CompatibleEquipment.Add(eq);
        db.Equipment.Add(eq);
        db.ChromatographyColumns.Add(col);
        db.TestDefinitions.Add(testDef);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        var order = new TestOrder { SampleId = sample.Id, TestCode = testDef.Code, SectionId = section.Id, Status = ApprovalStatus.InProgress, CurrentStep = WorkflowStep.Waiting };
        db.TestOrders.Add(order);
        await db.SaveChangesAsync();

        var run = SeedRun(db, testOrderId: 999, sectionId: section.Id, equipmentId: eq.Id, columnId: col.Id, methodId: method.Id, internalStandardName: "Benzoic acid");
        var runSvc = TestServiceFactory.HplcRun(db);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runSvc.AssignSamplesAsync(run.Id, new List<int> { order.Id }, 1));
        Assert.Equal("Dissolution does not support internal-standard methods.", ex.Message);
    }
    }
