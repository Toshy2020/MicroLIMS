using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.Responses;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Tests.Fixtures;
using Xunit;

namespace MicroLIMS.Tests.IntegrationTests;

// Workflow commands save several times - the result, its projection, the
// transition, the auto-submit for review. Without a transaction a failure
// part-way committed a result with none of what should follow it. These
// run on PostgreSQL because the InMemory provider has no transactions.
[Collection("PostgresDatabaseCollection")]
public class UnitOfWorkPostgresIntegrationTests
{
    private readonly PostgresTestFixture _fixture;

    public UnitOfWorkPostgresIntegrationTests(PostgresTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static string UniqueCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private async Task<bool> ItemExistsAsync(string code)
    {
        await using var check = _fixture.CreateDbContext();
        return await check.Items.AnyAsync(i => i.Code == code);
    }

    // ---- The helper ----

    [PostgresFact]
    public async Task CommandThatFailsAfterSaving_LeavesNothingBehind()
    {
        var first = UniqueCode("UOW-A-");
        var second = UniqueCode("UOW-B-");
        await using var db = _fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => UnitOfWork.RunAsync(db, async () =>
        {
            db.Items.Add(new Item { Code = first, Name = "first save" });
            await db.SaveChangesAsync();
            db.Items.Add(new Item { Code = second, Name = "second save" });
            await db.SaveChangesAsync();
            throw new InvalidOperationException("fails after two saves");
        }));

        Assert.False(await ItemExistsAsync(first));
        Assert.False(await ItemExistsAsync(second));
    }

    [PostgresFact]
    public async Task CommandThatSucceeds_CommitsEverySave()
    {
        var first = UniqueCode("UOW-C-");
        var second = UniqueCode("UOW-D-");
        await using var db = _fixture.CreateDbContext();

        await UnitOfWork.RunAsync(db, async () =>
        {
            db.Items.Add(new Item { Code = first, Name = "first save" });
            await db.SaveChangesAsync();
            db.Items.Add(new Item { Code = second, Name = "second save" });
            await db.SaveChangesAsync();
        });

        Assert.True(await ItemExistsAsync(first));
        Assert.True(await ItemExistsAsync(second));
    }

    [PostgresFact]
    public async Task NestedCommand_JoinsTheOuterTransaction_AndRollsBackWithIt()
    {
        var inner = UniqueCode("UOW-E-");
        await using var db = _fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => UnitOfWork.RunAsync(db, async () =>
        {
            await UnitOfWork.RunAsync(db, async () =>
            {
                db.Items.Add(new Item { Code = inner, Name = "inner command" });
                await db.SaveChangesAsync();
            });
            throw new InvalidOperationException("outer command fails after the inner one finished");
        }));

        Assert.False(await ItemExistsAsync(inner));
    }

    // A caller that handles the failure keeps using the same context: what
    // the command changed matches the database again, what it created is
    // gone, and what it never touched is still tracked and still saves.
    [PostgresFact]
    public async Task AfterARolledBackCommand_TheCallerCanKeepUsingTheContext()
    {
        await using var db = _fixture.CreateDbContext();
        var changedByCommand = new Item { Code = UniqueCode("UOW-G-"), Name = "original" };
        var untouched = new Item { Code = UniqueCode("UOW-H-"), Name = "untouched" };
        db.Items.AddRange(changedByCommand, untouched);
        await db.SaveChangesAsync();
        var createdByCommand = new Item { Code = UniqueCode("UOW-I-"), Name = "created" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => UnitOfWork.RunAsync(db, async () =>
        {
            changedByCommand.Name = "changed then rolled back";
            db.Items.Add(createdByCommand);
            await db.SaveChangesAsync();
            throw new InvalidOperationException("fails after saving");
        }));

        Assert.Equal("original", changedByCommand.Name);
        Assert.Equal(EntityState.Detached, db.Entry(createdByCommand).State);
        Assert.Equal(EntityState.Unchanged, db.Entry(untouched).State);

        untouched.Name = "saved after the failure";
        await db.SaveChangesAsync();

        await using var check = _fixture.CreateDbContext();
        Assert.Equal("saved after the failure", (await check.Items.SingleAsync(i => i.Id == untouched.Id)).Name);
        Assert.Equal("original", (await check.Items.SingleAsync(i => i.Id == changedByCommand.Id)).Name);
        Assert.False(await check.Items.AnyAsync(i => i.Code == createdByCommand.Code));
    }

    // A wrong password is recorded, then thrown. Rolling the command back
    // must not erase that record, or the attempt escapes the throttle.
    [PostgresFact]
    public async Task RefusedSignature_IsStillRecorded_WhenTheCommandIsRolledBack()
    {
        var code = UniqueCode("UOW-F-");
        var entityId = Random.Shared.Next(1_000_000, int.MaxValue);
        await using var db = _fixture.CreateDbContext();
        var roleId = await db.Users.Where(u => u.Id == _fixture.SeededUserId).Select(u => u.RoleId).SingleAsync();
        var signer = new User { FullName = "Signer", Username = UniqueCode("signer-"), PasswordHash = TestPasswords.Hash("Right-Password-1!"), RoleId = roleId };
        db.Users.Add(signer);
        await db.SaveChangesAsync();
        var signatures = new ElectronicSignatureService(db);

        await Assert.ThrowsAsync<SignatureVerificationException>(() => UnitOfWork.RunAsync(db, async () =>
        {
            db.Items.Add(new Item { Code = code, Name = "saved before signing" });
            await db.SaveChangesAsync();
            await signatures.SignAsync(signer.Id, "wrong password", SignatureMeaning.Approved, "UnitOfWorkProbe", entityId, null, null);
        }));

        Assert.False(await ItemExistsAsync(code));

        await using var check = _fixture.CreateDbContext();
        var refused = await check.AuditLogs
            .Where(a => a.EntityName == "ElectronicSignature" && a.Action == "SignatureFailed" && a.NewValue!.Contains($"UnitOfWorkProbe #{entityId}"))
            .CountAsync();
        Assert.Equal(1, refused);
    }

    // ---- A real workflow command ----

    // Fails the save that writes the ResultRecord projection - the second
    // of RecordResultAsync's three saves, after the reading is saved.
    private sealed class FailResultProjectionSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<ResultRecord>().Any(e => e.State == EntityState.Added))
                throw new InvalidOperationException("projection save failed");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [PostgresFact]
    public async Task RecordResult_FailingAtTheProjection_RollsBackTheReadingAndTheIncubation()
    {
        var failing = new FailResultProjectionSave();
        int orderId, incubationId;
        await using (var db = _fixture.CreateDbContext(failing))
        {
            (orderId, var mediaId, var incubatorId) = await SeedCountTestOrderAsync(db);
            var engine = TestServiceFactory.TestWorkflow(db);
            var incubation = await engine.SelectMediaAsync(orderId, "CountIncubation", mediaId, incubatorEquipmentId: incubatorId, userId: _fixture.SeededUserId);
            incubationId = incubation.Id;

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                engine.RecordResultAsync(orderId, "CountIncubation", new CountTestPayload(new List<decimal> { 12, 14 }, 1), _fixture.SeededUserId));
        }

        await using var check = _fixture.CreateDbContext();
        Assert.False(await check.CountTestReadings.AnyAsync(r => r.TestOrderId == orderId));
        Assert.Null((await check.Incubations.SingleAsync(i => i.Id == incubationId)).CompletedAt);
        Assert.Equal(WorkflowStep.Incubating, (await check.TestOrders.SingleAsync(t => t.Id == orderId)).CurrentStep);
    }

    private async Task<(int OrderId, int MediaId, int IncubatorId)> SeedCountTestOrderAsync(MicroLIMS.Persistence.DbContext.MicroLimsDbContext db)
    {
        var sectionId = _fixture.SeededSectionId;
        var testCode = UniqueCode("TAMCUOW");

        var definition = new TestDefinition { Code = testCode, DisplayName = "Count test (unit of work)", WorkflowType = WorkflowType.CountTest, SectionId = sectionId };
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
        var point = new WaterSamplingPoint { Code = UniqueCode("WP-"), Location = "Utility Room", AssignedTestCodes = new() { testCode } };
        db.WaterSamplingPoints.Add(point);
        var cause = new CauseOfTesting { Name = UniqueCode("Routine ") };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();

        db.SamplingConfigurations.Add(new SamplingConfiguration { WaterSamplingPointId = point.Id, TestCode = testCode, AlertLimit = "10", ActionLimit = "50", SpecLimit = "100" });

        var sample = new Sample
        {
            Category = SampleCategory.Water, WaterSamplingPointId = point.Id, ControlNumber = UniqueCode("CTRL-"), ReferenceNumber = UniqueCode("REF-"),
            CauseOfTestingId = cause.Id, ReceivedByUserId = _fixture.SeededUserId, Status = SampleStatus.Received
        };
        var order = new TestOrder { TestCode = testCode, Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, SectionId = sectionId };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        return (order.Id, media.Id, incubator.Id);
    }

    private sealed class FailReplicateInsertSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<HplcSampleReplicate>().Any(e => e.State == EntityState.Added))
                throw new InvalidOperationException("replicate insert failed");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    [PostgresFact]
    public async Task ReplaceReplicates_FailingAtInsert_RollsBackTheDeletion()
    {
        var sectionId = _fixture.SeededSectionId;
        var userId = _fixture.SeededUserId;
        int runSampleId;
        int originalRepId;

        await using (var db = _fixture.CreateDbContext())
        {
            var equip = new Equipment { Name = "HPLC UOW", Code = UniqueCode("HPLCUOW-"), Type = EquipmentType.Hplc, SectionId = sectionId };
            db.Equipment.Add(equip);

            var col = new ChromatographyColumn
            {
                Name = "Column UOW", Code = UniqueCode("COL-"), SerialNumber = UniqueCode("SN-"),
                SectionId = sectionId, IsActive = true, CreatedByUserId = userId, LastModifiedByUserId = userId
            };
            db.ChromatographyColumns.Add(col);

            var diluent = new SolutionMaster
            {
                SectionId = sectionId, Name = "Diluent UOW", Type = SolutionType.Diluent,
                ShelfLifeValue = 30, ShelfLifeUnit = ShelfLifeUnit.Days, StorageCondition = "Room", FinalVolumeMl = 1000m,
                CreatedByUserId = userId, LastModifiedByUserId = userId
            };
            var stdEntry = new MaterialMasterEntry
            {
                SectionId = sectionId, Code = UniqueCode("STD-"), Name = "Standard UOW",
                Category = MaterialMasterCategory.ReferenceStandard, BaseUnit = MaterialUnit.Gram,
                CreatedByUserId = userId, LastModifiedByUserId = userId
            };
            db.SolutionMasters.Add(diluent);
            db.MaterialMasterEntries.Add(stdEntry);
            await db.SaveChangesAsync();

            var method = new HplcMethod
            {
                SectionId = sectionId, Name = "Method RS UOW", Abbreviation = UniqueCode("M"),
                ColumnDesignation = "L1", DiluentSolutionId = diluent.Id, EffectiveDate = DateTime.UtcNow,
                ResultMode = HplcResultMode.RelatedSubstances, ReferenceSolutionPercent = 0.1m, DisregardLimitPercent = 0.05m,
                CreatedByUserId = userId, LastModifiedByUserId = userId,
                Analytes =
                {
                    new HplcMethodAnalyte
                    {
                        DisplayOrder = 1, Name = "Analyte", WavelengthNm = 290m, StandardEntryId = stdEntry.Id,
                        TheoreticalWeightStdMg = 50m, TheoreticalWeightTestMg = 50m, StandardInjections = 5,
                    }
                }
            };
            db.HplcMethods.Add(method);
            await db.SaveChangesAsync();

            var run = new HplcRun
            {
                Code = UniqueCode("RUN-"),
                EquipmentId = equip.Id,
                ChromatographyColumnId = col.Id,
                HplcMethodId = method.Id,
                SectionId = sectionId,
                Status = HplcRunStatus.Open,
                AnalystUserId = userId,
                MethodSnapshotJson = JsonSerializer.Serialize(HplcMethodResponse.From(method), SnapshotJson.Options),
            };
            db.HplcRuns.Add(run);
            await db.SaveChangesAsync();

            var cause = new CauseOfTesting { Name = UniqueCode("Routine ") };
            db.CausesOfTesting.Add(cause);
            await db.SaveChangesAsync();

            var sample = new Sample
            {
                Category = SampleCategory.RawMaterial,
                ControlNumber = UniqueCode("CTRL-"),
                ReferenceNumber = UniqueCode("REF-"),
                CauseOfTestingId = cause.Id,
                ReceivedByUserId = userId,
                Status = SampleStatus.Received,
            };
            var order = new TestOrder
            {
                TestCode = UniqueCode("TST-"),
                Status = ApprovalStatus.Pending,
                CurrentStep = WorkflowStep.Waiting,
                SectionId = sectionId,
            };
            sample.TestOrders.Add(order);
            db.Samples.Add(sample);
            await db.SaveChangesAsync();

            var runSample = new HplcRunSample
            {
                HplcRunId = run.Id,
                TestOrderId = order.Id,
                Status = HplcRunSampleStatus.Assigned,
                AssignedByUserId = userId,
                AssignedAt = DateTime.UtcNow,
            };
            var rep = new HplcSampleReplicate
            {
                ReplicateNo = 1,
                ActualWeightMg = 0m,
                Responses = new List<HplcReplicateResponse> { new() { HplcMethodAnalyteId = method.Analytes[0].Id, Response = 10000m } },
                Peaks = new List<HplcReplicatePeak> { new() { Area = 5000m } },
            };
            runSample.Replicates.Add(rep);
            db.HplcRunSamples.Add(runSample);
            await db.SaveChangesAsync();

            runSampleId = runSample.Id;
            originalRepId = rep.Id;
        }

        var failing = new FailReplicateInsertSave();
        await using (var db = _fixture.CreateDbContext(failing))
        {
            var service = TestServiceFactory.HplcRun(db);
            var analyteId = (await db.HplcSampleReplicates.Include(r => r.Responses).FirstAsync(r => r.Id == originalRepId)).Responses[0].HplcMethodAnalyteId;
            var newInputs = new SaveReplicatesRequest(new List<HplcReplicateInput>
            {
                new(0m, new List<HplcReplicateResponseInput> { new(analyteId, 12000m) }, new List<HplcReplicatePeakInput> { new(null, null, 8000m) }),
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SaveReplicatesAsync(runSampleId, newInputs, userId));
        }

        await using (var check = _fixture.CreateDbContext())
        {
            var reps = await check.HplcSampleReplicates.Include(r => r.Peaks).Where(r => r.HplcRunSampleId == runSampleId).ToListAsync();
            var rep = Assert.Single(reps);
            Assert.Equal(originalRepId, rep.Id);
            Assert.Equal(1, rep.ReplicateNo);
            Assert.Equal(5000m, Assert.Single(rep.Peaks).Area);
        }
    }
}
