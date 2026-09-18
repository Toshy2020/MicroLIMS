using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.API.Controllers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Constants;
using Xunit;

namespace MicroLIMS.Tests.WorkflowTests;

// Corrections to a received sample and voids are signed, need a reason, and
// leave a structured audit event (every field's old and new value) plus a
// timeline event - and a failed signature changes nothing.
public class SampleCorrectionTests
{
    private const string Password = "Correct-Horse-9";

    private static MicroLimsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MicroLimsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MicroLimsDbContext(options);
    }

    private sealed record Seeded(Sample Sample, User Signer, CauseOfTesting Cause);

    private static async Task<User> SeedSignerAsync(MicroLimsDbContext db)
    {
        var role = new Role { Type = RoleType.SectionHead, Name = "Section Head" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var user = new User { FullName = "Sam Head", Username = "samhead", RoleId = role.Id, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<CauseOfTesting> SeedCauseAsync(MicroLimsDbContext db, string name)
    {
        var cause = new CauseOfTesting { Name = name };
        db.CausesOfTesting.Add(cause);
        await db.SaveChangesAsync();
        return cause;
    }

    private static async Task<Item> SeedItemAsync(MicroLimsDbContext db, string name, SampleCategory category, params string[] testCodes)
    {
        var section = TestServiceFactory.EnsureMicroSection(db);
        var item = new Item { Name = name, Code = name.Replace(" ", "").ToUpperInvariant(), Category = category, IsActive = true };
        foreach (var code in testCodes)
        {
            item.AssignedTests.Add(new SampleTest { TestCode = code, DisplayName = code });
            if (!db.TestDefinitions.Any(td => td.Code == code))
            {
                db.TestDefinitions.Add(new TestDefinition { Code = code, DisplayName = code, SectionId = section.Id });
            }
        }
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    // A received Finished Product sample: tests TAMC and TYMC, nothing started.
    private static async Task<Seeded> SeedProductAsync(MicroLimsDbContext db)
    {
        var signer = await SeedSignerAsync(db);
        var cause = await SeedCauseAsync(db, "Routine");
        var item = await SeedItemAsync(db, "Paracetamol 500", SampleCategory.FinishedProduct, "TAMC", "TYMC");
        var section = TestServiceFactory.EnsureMicroSection(db);

        var sample = new Sample
        {
            ReferenceNumber = "FP0926001",
            Category = SampleCategory.FinishedProduct,
            ItemId = item.Id,
            CauseOfTestingId = cause.Id,
            BatchNumber = "OLD-BATCH",
            ControlNumber = "OLD-CTRL",
            SampledBy = "Operator A",
            SampleQuantity = "3 packs",
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.NeedsPreparation
        };
        sample.TestOrders.Add(new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, SectionId = section.Id });
        sample.TestOrders.Add(new TestOrder { TestCode = "TYMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, SectionId = section.Id });
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        return new Seeded(sample, signer, cause);
    }

    // The sample's details exactly as stored - each test changes what it needs with `with`.
    private static SampleCorrectionRequest AsStored(Sample s) => new(
        s.ControlNumber, s.SampledBy, s.CauseOfTestingId, s.BatchNumber, s.MfgDate, s.ExpDate, s.SampleQuantity,
        s.ProductionStage, s.PreviousProductName, s.PreviousProductBatchNumber, s.StorageCondition, s.StorageTimeHours,
        s.ItemId, s.WaterDepartmentId, s.DepartmentId, s.MachineId);

    private static Task<AuditLog?> AuditEventAsync(MicroLimsDbContext db, string actionCode, int sampleId) =>
        db.AuditLogs.Include(a => a.Changes)
            .FirstOrDefaultAsync(a => a.ActionCode == actionCode && a.EntityId == sampleId.ToString());

    [Fact]
    public async Task CorrectAsync_DescriptiveFields_AreSignedAuditedAndOnTheTimeline()
    {
        await using var db = NewDb();
        var (sample, signer, _) = await SeedProductAsync(db);
        var complaint = await SeedCauseAsync(db, "Complaint");
        var tamc = sample.TestOrders.Single(t => t.TestCode == "TAMC");
        db.ResultRecords.Add(new ResultRecord { SampleId = sample.Id, TestOrderId = tamc.Id, TestCode = "TAMC", BatchNumber = "OLD-BATCH", ControlNumber = "OLD-CTRL", SampleStatus = SampleStatus.Received });
        await db.SaveChangesAsync();
        var orderIds = sample.TestOrders.Select(t => t.Id).OrderBy(id => id).ToList();

        var request = AsStored(sample) with
        {
            BatchNumber = " NEW-BATCH ",
            ControlNumber = "NEW-CTRL",
            MfgDate = new DateTime(2026, 8, 1),
            CauseOfTestingId = complaint.Id
        };
        var dto = await TestServiceFactory.SampleCorrection(db).CorrectAsync(
            sample.Id, request, "  Typed from the wrong label  ", Password, signer.Id, "10.0.0.5");

        Assert.Equal("NEW-BATCH", dto.BatchNumber);
        Assert.Equal("Complaint", dto.CauseOfTesting);
        Assert.Equal(complaint.Id, dto.CauseOfTestingId);

        var reloaded = await db.Samples.Include(s => s.TestOrders).FirstAsync(s => s.Id == sample.Id);
        Assert.Equal("NEW-CTRL", reloaded.ControlNumber);
        Assert.Equal(new DateTime(2026, 8, 1), reloaded.MfgDate);
        Assert.Equal(complaint.Id, reloaded.CauseOfTestingId);
        // Descriptive corrections leave the tests as they are.
        Assert.Equal(orderIds, reloaded.TestOrders.Select(t => t.Id).OrderBy(id => id).ToList());

        var signature = Assert.Single(await db.ElectronicSignatures.ToListAsync());
        Assert.Equal(SignatureMeaning.SampleCorrected, signature.MeaningOfSignature);
        Assert.Equal(ReviewEntityTypes.Sample, signature.EntityType);
        Assert.Equal(sample.Id, signature.EntityId);
        Assert.Equal(signer.Id, signature.UserId);
        Assert.Equal("Typed from the wrong label", signature.Comment);
        Assert.Equal("10.0.0.5", signature.IpAddress);

        // The Sample's Audit History lists it: EntityName "Sample", EntityId the sample id.
        var audit = await AuditEventAsync(db, SampleCorrectionService.CorrectedActionCode, sample.Id);
        Assert.NotNull(audit);
        Assert.Equal(ReviewEntityTypes.Sample, audit!.EntityName);
        Assert.Equal(sample.Id, audit.SampleId);
        Assert.Equal(signer.Id, audit.UserId);
        Assert.Equal("Typed from the wrong label", audit.Reason);
        Assert.Equal(
            new[] { "Batch Number", "Cause of Testing", "Control Number", "Mfg Date" },
            audit.Changes.Select(c => c.FieldName).OrderBy(f => f, StringComparer.Ordinal).ToArray());
        var batch = audit.Changes.Single(c => c.FieldName == "Batch Number");
        Assert.Equal("OLD-BATCH", batch.PreviousValue);
        Assert.Equal("NEW-BATCH", batch.NewValue);
        var cause = audit.Changes.Single(c => c.FieldName == "Cause of Testing");
        Assert.Equal("Routine", cause.PreviousValue);
        Assert.Equal("Complaint", cause.NewValue);
        var mfg = audit.Changes.Single(c => c.FieldName == "Mfg Date");
        Assert.Null(mfg.PreviousValue);
        Assert.Equal("2026-08-01", mfg.NewValue);

        var timeline = Assert.Single(await db.ReviewWorkflowEvents
            .Where(e => e.EntityType == ReviewEntityTypes.Sample && e.EntityId == sample.Id)
            .ToListAsync());
        Assert.Equal(ReviewWorkflowEventType.SampleCorrected, timeline.EventType);
        Assert.StartsWith("Typed from the wrong label", timeline.Comment);

        // Result projection rows copy the identifiers - they follow the correction.
        var record = await db.ResultRecords.SingleAsync();
        Assert.Equal("NEW-BATCH", record.BatchNumber);
        Assert.Equal("NEW-CTRL", record.ControlNumber);
    }

    [Fact]
    public async Task CorrectAsync_WrongPassword_ChangesNothing()
    {
        await using var db = NewDb();
        var (sample, signer, _) = await SeedProductAsync(db);

        await Assert.ThrowsAsync<SignatureVerificationException>(() =>
            TestServiceFactory.SampleCorrection(db).CorrectAsync(
                sample.Id, AsStored(sample) with { BatchNumber = "NEW-BATCH" }, "Wrong batch", "not-my-password", signer.Id, null));

        Assert.Equal("OLD-BATCH", (await db.Samples.FirstAsync(s => s.Id == sample.Id)).BatchNumber);
        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
        Assert.Null(await AuditEventAsync(db, SampleCorrectionService.CorrectedActionCode, sample.Id));
        Assert.Empty(await db.ReviewWorkflowEvents.ToListAsync());
    }

    [Theory]
    [InlineData(SampleStatus.UnderReview)]
    [InlineData(SampleStatus.UnderApproval)]
    [InlineData(SampleStatus.Approved)]
    [InlineData(SampleStatus.Voided)]
    public async Task CorrectAsync_OnceSubmittedForReview_IsLocked(SampleStatus status)
    {
        await using var db = NewDb();
        var (sample, signer, _) = await SeedProductAsync(db);
        sample.Status = status;
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.SampleCorrection(db).CorrectAsync(
                sample.Id, AsStored(sample) with { BatchNumber = "NEW-BATCH" }, "Late correction", Password, signer.Id, null));

        Assert.Contains("submitted for review", ex.Message);
        Assert.Equal("OLD-BATCH", (await db.Samples.FirstAsync(s => s.Id == sample.Id)).BatchNumber);
        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task CorrectAsync_InTestingAfterIncubation_DescriptiveCorrectionIsStillAllowed()
    {
        await using var db = NewDb();
        var (sample, signer, _) = await SeedProductAsync(db);
        sample.Status = SampleStatus.InTesting;
        var tamc = sample.TestOrders.Single(t => t.TestCode == "TAMC");
        tamc.CurrentStep = WorkflowStep.Incubating;
        db.Incubations.Add(new Incubation { TestOrderId = tamc.Id, StepName = "CountIncubation" });
        await db.SaveChangesAsync();

        var dto = await TestServiceFactory.SampleCorrection(db).CorrectAsync(
            sample.Id, AsStored(sample) with { ControlNumber = "NEW-CTRL" }, "Transposed digits", Password, signer.Id, null);

        Assert.Equal("NEW-CTRL", dto.ControlNumber);
    }

    [Fact]
    public async Task CorrectAsync_WithoutReasonOrWithoutChanges_IsRefused()
    {
        await using var db = NewDb();
        var (sample, signer, _) = await SeedProductAsync(db);
        var service = TestServiceFactory.SampleCorrection(db);

        var noReason = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CorrectAsync(sample.Id, AsStored(sample) with { BatchNumber = "NEW-BATCH" }, "   ", Password, signer.Id, null));
        Assert.Equal("A reason for the correction is required.", noReason.Message);

        var noChanges = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CorrectAsync(sample.Id, AsStored(sample), "Checking", Password, signer.Id, null));
        Assert.Equal("No changes were made.", noChanges.Message);

        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task CorrectAsync_SettingTheRetestCause_IsRefused()
    {
        await using var db = NewDb();
        var (sample, signer, _) = await SeedProductAsync(db);
        var retest = await SeedCauseAsync(db, "Retest");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.SampleCorrection(db).CorrectAsync(
                sample.Id, AsStored(sample) with { CauseOfTestingId = retest.Id }, "Is a retest", Password, signer.Id, null));

        Assert.Contains("Retest", ex.Message);
    }

    [Fact]
    public async Task CorrectAsync_ItemChangeBeforeWork_RebuildsTestsAndResetsPreparation()
    {
        await using var db = NewDb();
        var (sample, signer, _) = await SeedProductAsync(db);
        var ibuprofen = await SeedItemAsync(db, "Ibuprofen 400", SampleCategory.FinishedProduct, "TAMC", "ECOLI");

        // Prepared and assigned, but no test has started - one start attempt was refused.
        sample.PreparationStatus = SamplePreparationStatus.Ready;
        foreach (var order in sample.TestOrders) order.AssignedAnalystId = 42;
        db.SamplePreparations.Add(new SamplePreparation { SampleId = sample.Id, Amount = 10, Technique = "PourPlate", Diluent = "SCDLP", Neutralizer = "None", PreparedByUserId = 42 });
        var tamc = sample.TestOrders.Single(t => t.TestCode == "TAMC");
        var tymc = sample.TestOrders.Single(t => t.TestCode == "TYMC");
        db.WorkflowHistories.Add(new WorkflowHistory { TestOrderId = tymc.Id, FromStep = WorkflowStep.Waiting, ToStep = WorkflowStep.Waiting, Note = "Transition refused", PerformedByUserId = 42 });
        await db.SaveChangesAsync();

        await TestServiceFactory.SampleCorrection(db).CorrectAsync(
            sample.Id, AsStored(sample) with { ItemId = ibuprofen.Id }, "Received against the wrong product", Password, signer.Id, null);

        var reloaded = await db.Samples.Include(s => s.TestOrders).FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(ibuprofen.Id, reloaded.ItemId);
        Assert.Equal(SamplePreparationStatus.NeedsPreparation, reloaded.PreparationStatus);
        Assert.False(await db.SamplePreparations.AnyAsync(p => p.SampleId == sample.Id));

        Assert.Equal(new[] { "ECOLI", "TAMC" }, reloaded.TestOrders.Select(t => t.TestCode).OrderBy(c => c, StringComparer.Ordinal).ToArray());
        // The test both items require is kept as it was; the new one inherits the analyst.
        Assert.Equal(tamc.Id, reloaded.TestOrders.Single(t => t.TestCode == "TAMC").Id);
        Assert.All(reloaded.TestOrders, t =>
        {
            Assert.Equal(42, t.AssignedAnalystId);
            Assert.Equal(WorkflowStep.Waiting, t.CurrentStep);
        });
        Assert.False(await db.TestOrders.AnyAsync(t => t.Id == tymc.Id));
        Assert.False(await db.WorkflowHistories.AnyAsync());

        var audit = await AuditEventAsync(db, SampleCorrectionService.CorrectedActionCode, sample.Id);
        var item = audit!.Changes.Single(c => c.FieldName == "Item");
        Assert.Equal("Paracetamol 500", item.PreviousValue);
        Assert.Equal("Ibuprofen 400", item.NewValue);
        var tests = audit.Changes.Single(c => c.FieldName == "Tests");
        Assert.Equal("TAMC, TYMC", tests.PreviousValue);
        Assert.Equal("ECOLI, TAMC", tests.NewValue);
        Assert.Contains(audit.Changes, c => c.FieldName == "Preparation Status" && c.NewValue == nameof(SamplePreparationStatus.NeedsPreparation));
    }

    [Fact]
    public async Task CorrectAsync_ItemChangeAfterIncubationStarted_IsRefused()
    {
        await using var db = NewDb();
        var (sample, signer, _) = await SeedProductAsync(db);
        var ibuprofen = await SeedItemAsync(db, "Ibuprofen 400", SampleCategory.FinishedProduct, "TAMC");
        var originalItemId = sample.ItemId;
        db.Incubations.Add(new Incubation { TestOrderId = sample.TestOrders.First().Id, StepName = "CountIncubation" });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.SampleCorrection(db).CorrectAsync(
                sample.Id, AsStored(sample) with { ItemId = ibuprofen.Id }, "Wrong product", Password, signer.Id, null));

        Assert.Contains("Testing has already started", ex.Message);
        var reloaded = await db.Samples.Include(s => s.TestOrders).FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(originalItemId, reloaded.ItemId);
        Assert.Equal(2, reloaded.TestOrders.Count);
        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
    }

    [Fact]
    public async Task CorrectAsync_ItemFromAnotherCategory_IsRefused()
    {
        await using var db = NewDb();
        var (sample, signer, _) = await SeedProductAsync(db);
        var rawMaterial = await SeedItemAsync(db, "Lactose", SampleCategory.RawMaterial, "TAMC");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.SampleCorrection(db).CorrectAsync(
                sample.Id, AsStored(sample) with { ItemId = rawMaterial.Id }, "Wrong item", Password, signer.Id, null));

        Assert.Contains("same category", ex.Message);
    }

    [Fact]
    public async Task CorrectAsync_RetestSample_CannotChangeItem()
    {
        await using var db = NewDb();
        var (sample, signer, cause) = await SeedProductAsync(db);
        var origin = new Sample { ReferenceNumber = "FP0926000", Category = SampleCategory.FinishedProduct, ItemId = sample.ItemId, CauseOfTestingId = cause.Id, Status = SampleStatus.RetestRequested };
        db.Samples.Add(origin);
        await db.SaveChangesAsync();
        sample.OriginSampleId = origin.Id;
        var ibuprofen = await SeedItemAsync(db, "Ibuprofen 400", SampleCategory.FinishedProduct, "TAMC");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestServiceFactory.SampleCorrection(db).CorrectAsync(
                sample.Id, AsStored(sample) with { ItemId = ibuprofen.Id }, "Wrong product", Password, signer.Id, null));

        Assert.Contains("retest sample", ex.Message);
    }

    [Fact]
    public async Task CorrectAsync_EmDepartmentChange_OnPreparedSample_RemovesLocationsAndTests()
    {
        await using var db = NewDb();
        var signer = await SeedSignerAsync(db);
        var cause = await SeedCauseAsync(db, "Routine");
        var packing = new Department { Name = "Packing" };
        var filling = new Department { Name = "Filling" };
        db.Departments.AddRange(packing, filling);
        await db.SaveChangesAsync();

        var sample = new Sample
        {
            ReferenceNumber = "EM0926001",
            Category = SampleCategory.EnvironmentalMonitoring,
            DepartmentId = packing.Id,
            CauseOfTestingId = cause.Id,
            ControlNumber = "EM-CTRL",
            SampledBy = "Operator B",
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.Ready
        };
        var order = new TestOrder { TestCode = "TAMC", Status = ApprovalStatus.Pending, CurrentStep = WorkflowStep.Waiting, AssignedAnalystId = 42 };
        sample.TestOrders.Add(order);
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        db.SampleLocations.Add(new SampleLocation { SampleId = sample.Id, TestOrderId = order.Id });
        await db.SaveChangesAsync();

        await TestServiceFactory.SampleCorrection(db).CorrectAsync(
            sample.Id, AsStored(sample) with { DepartmentId = filling.Id }, "Sampled in Filling, not Packing", Password, signer.Id, null);

        var reloaded = await db.Samples.Include(s => s.TestOrders).FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(filling.Id, reloaded.DepartmentId);
        Assert.Equal(SamplePreparationStatus.NeedsPreparation, reloaded.PreparationStatus);
        // A location's tests are created again when the sample is prepared for the new department.
        Assert.Empty(reloaded.TestOrders);
        Assert.False(await db.SampleLocations.AnyAsync());

        var audit = await AuditEventAsync(db, SampleCorrectionService.CorrectedActionCode, sample.Id);
        var department = audit!.Changes.Single(c => c.FieldName == "Department");
        Assert.Equal("Packing", department.PreviousValue);
        Assert.Equal("Filling", department.NewValue);
        var tests = audit.Changes.Single(c => c.FieldName == "Tests");
        Assert.Equal("TAMC", tests.PreviousValue);
        Assert.Null(tests.NewValue);
    }

    [Fact]
    public async Task CorrectAsync_AfterCleaning_PreviousProductBatchMirrorsBatchNumber()
    {
        await using var db = NewDb();
        var signer = await SeedSignerAsync(db);
        var cause = await SeedCauseAsync(db, "Routine");
        var machine = new Machine { Name = "Filler 1" };
        db.Machines.Add(machine);
        await db.SaveChangesAsync();
        var sample = new Sample
        {
            ReferenceNumber = "AC0926001",
            Category = SampleCategory.AfterCleaning,
            MachineId = machine.Id,
            CauseOfTestingId = cause.Id,
            ControlNumber = "AC-CTRL",
            SampledBy = "Operator C",
            PreviousProductName = "Syrup A",
            PreviousProductBatchNumber = "B-1",
            BatchNumber = "B-1",
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.NeedsPreparation
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();

        await TestServiceFactory.SampleCorrection(db).CorrectAsync(
            sample.Id, AsStored(sample) with { PreviousProductBatchNumber = "B-2" }, "Previous batch misread", Password, signer.Id, null);

        var reloaded = await db.Samples.FirstAsync(s => s.Id == sample.Id);
        Assert.Equal("B-2", reloaded.PreviousProductBatchNumber);
        Assert.Equal("B-2", reloaded.BatchNumber);
    }

    [Fact]
    public async Task CorrectAsync_PreparedWaterSample_RefrigeratedStorageNeedsHours()
    {
        await using var db = NewDb();
        var signer = await SeedSignerAsync(db);
        var cause = await SeedCauseAsync(db, "Routine");
        var department = new WaterDepartment { Name = "Utilities" };
        db.WaterDepartments.Add(department);
        await db.SaveChangesAsync();
        var sample = new Sample
        {
            ReferenceNumber = "WT0926001",
            Category = SampleCategory.Water,
            WaterDepartmentId = department.Id,
            CauseOfTestingId = cause.Id,
            ControlNumber = "WT-CTRL",
            SampledBy = "Operator D",
            SampleQuantity = "500 mL",
            StorageCondition = "RoomTemperature",
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.Ready
        };
        db.Samples.Add(sample);
        await db.SaveChangesAsync();
        var service = TestServiceFactory.SampleCorrection(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CorrectAsync(sample.Id, AsStored(sample) with { StorageCondition = "Refrigerator" }, "Was refrigerated", Password, signer.Id, null));
        Assert.Contains("Storage time is required", ex.Message);

        await service.CorrectAsync(
            sample.Id, AsStored(sample) with { StorageCondition = "Refrigerator", StorageTimeHours = 6 }, "Was refrigerated", Password, signer.Id, null);

        var reloaded = await db.Samples.FirstAsync(s => s.Id == sample.Id);
        Assert.Equal("Refrigerator", reloaded.StorageCondition);
        Assert.Equal(6, reloaded.StorageTimeHours);
        var audit = await AuditEventAsync(db, SampleCorrectionService.CorrectedActionCode, sample.Id);
        var hours = audit!.Changes.Single(c => c.FieldName == "Storage Time (h)");
        Assert.Null(hours.PreviousValue);
        Assert.Equal("6", hours.NewValue);
    }

    [Fact]
    public async Task VoidAsync_IsSignedAndAudited_AndRecordsNoApprovalDecision()
    {
        await using var db = NewDb();
        var (sample, signer, _) = await SeedProductAsync(db);
        var liveOrder = sample.TestOrders.Single(t => t.TestCode == "TAMC");
        var supersededOrder = new TestOrder { SampleId = sample.Id, TestCode = "ECOLI", Status = ApprovalStatus.Reviewed, CurrentStep = WorkflowStep.Reviewed, IsSuperseded = true };
        db.TestOrders.Add(supersededOrder);
        db.ResultRecords.Add(new ResultRecord { SampleId = sample.Id, TestOrderId = liveOrder.Id, TestCode = "TAMC", SampleStatus = SampleStatus.InTesting });
        await db.SaveChangesAsync();

        var dto = await TestServiceFactory.SampleCorrection(db).VoidAsync(
            sample.Id, "  Received against the wrong batch  ", Password, signer.Id, "10.0.0.7");

        var reloaded = await db.Samples.Include(s => s.TestOrders).FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(SampleStatus.Voided, reloaded.Status);
        Assert.Equal("Voided", dto.Status);

        // A void is not a decision about the material.
        Assert.Null(reloaded.ApprovalDecision);
        Assert.Null(reloaded.ApprovedByUserId);
        Assert.Null(reloaded.ApprovedAt);
        Assert.Equal("Voided: Received against the wrong batch", reloaded.CertificateRemarks);

        Assert.Equal(ApprovalStatus.Voided, reloaded.TestOrders.Single(t => t.Id == liveOrder.Id).Status);
        Assert.Equal(ApprovalStatus.Reviewed, reloaded.TestOrders.Single(t => t.Id == supersededOrder.Id).Status);
        Assert.Equal(SampleStatus.Voided, (await db.ResultRecords.SingleAsync()).SampleStatus);

        var signature = Assert.Single(await db.ElectronicSignatures.ToListAsync());
        Assert.Equal(SignatureMeaning.SampleVoided, signature.MeaningOfSignature);
        Assert.Equal(sample.Id, signature.EntityId);
        Assert.Equal("Received against the wrong batch", signature.Comment);
        Assert.Equal("10.0.0.7", signature.IpAddress);

        var audit = await AuditEventAsync(db, SampleCorrectionService.VoidedActionCode, sample.Id);
        Assert.NotNull(audit);
        Assert.Equal(sample.Id, audit!.SampleId);
        Assert.Equal("Received against the wrong batch", audit.Reason);
        var status = audit.Changes.Single(c => c.FieldName == "Status");
        Assert.Equal(nameof(SampleStatus.Received), status.PreviousValue);
        Assert.Equal(nameof(SampleStatus.Voided), status.NewValue);
        Assert.Equal("TAMC, TYMC", audit.Changes.Single(c => c.FieldName == "Tests Voided").NewValue);

        var voidEvent = Assert.Single(await db.ReviewWorkflowEvents
            .Where(e => e.EntityType == ReviewEntityTypes.Sample && e.EntityId == sample.Id)
            .ToListAsync());
        Assert.Equal(ReviewWorkflowEventType.SampleVoided, voidEvent.EventType);
        Assert.Equal(signer.Id, voidEvent.PerformedByUserId);
        Assert.Equal("Received against the wrong batch", voidEvent.Comment);
    }

    [Fact]
    public async Task VoidAsync_WrongPassword_VoidsNothing()
    {
        await using var db = NewDb();
        var (sample, signer, _) = await SeedProductAsync(db);

        await Assert.ThrowsAsync<SignatureVerificationException>(() =>
            TestServiceFactory.SampleCorrection(db).VoidAsync(sample.Id, "Duplicate registration", "not-my-password", signer.Id, null));

        var reloaded = await db.Samples.Include(s => s.TestOrders).FirstAsync(s => s.Id == sample.Id);
        Assert.Equal(SampleStatus.Received, reloaded.Status);
        Assert.All(reloaded.TestOrders, t => Assert.Equal(ApprovalStatus.Pending, t.Status));
        Assert.Empty(await db.ElectronicSignatures.ToListAsync());
        Assert.Null(await AuditEventAsync(db, SampleCorrectionService.VoidedActionCode, sample.Id));
    }

    [Fact]
    public async Task VoidAsync_AlreadyVoided_IsRefused()
    {
        await using var db = NewDb();
        var (sample, signer, _) = await SeedProductAsync(db);
        var service = TestServiceFactory.SampleCorrection(db);
        await service.VoidAsync(sample.Id, "Duplicate registration", Password, signer.Id, null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.VoidAsync(sample.Id, "Again", Password, signer.Id, null));

        Assert.Equal("Sample is already voided.", ex.Message);
    }

    // Analysts record the work; correcting or voiding the sample record is not theirs.
    [Theory]
    [InlineData(nameof(SampleController.Correct))]
    [InlineData(nameof(SampleController.Void))]
    public void SampleController_CorrectAndVoid_AreClosedToAnalysts(string action)
    {
        var method = typeof(SampleController).GetMethod(action)!;
        var attr = (AuthorizeAttribute)method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Single();

        var roles = attr.Roles!.Split(',');
        Assert.DoesNotContain(RoleConstants.Analyst, roles);
        Assert.Contains(RoleConstants.Reviewer, roles);
        Assert.Contains(RoleConstants.SectionHead, roles);
        Assert.Contains(RoleConstants.SystemAdministrator, roles);
    }
}
