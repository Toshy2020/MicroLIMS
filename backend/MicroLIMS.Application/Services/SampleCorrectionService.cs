using MicroLIMS.Application.Helpers;
using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Services;

// The complete corrected state of a received sample's editable details.
// Fields are sent in full - a null optional field clears it - so the change
// set is exactly what differs from the stored sample. Fields that do not
// apply to the sample's category are ignored.
public record SampleCorrectionRequest(
    string ControlNumber,
    string SampledBy,
    int CauseOfTestingId,
    string? BatchNumber = null,
    DateTime? MfgDate = null,
    DateTime? ExpDate = null,
    string? SampleQuantity = null,
    string? ProductionStage = null,
    string? PreviousProductName = null,
    string? PreviousProductBatchNumber = null,
    string? StorageCondition = null,
    int? StorageTimeHours = null,
    int? ItemId = null,
    int? WaterDepartmentId = null,
    int? DepartmentId = null,
    int? MachineId = null,
    string? Grade = null);

// Corrects a received sample's details, and voids samples. Both are signed
// (21 CFR 11.200 - the password is re-verified at the moment of signing),
// need a reason, and record a structured audit event holding every field's
// old and new value, plus a lifecycle event on the sample's timeline.
//
// Details are correctable until the sample is submitted for review (Received
// or InTesting) - review signs the data as it stands. The item / location
// decides which tests a sample has, so it can only change before any test
// work exists; the tests and preparation are then rebuilt from the new item /
// location. Once work has started it is locked.
public class SampleCorrectionService
{
    public const string CorrectedActionCode = "SampleCorrected";
    public const string VoidedActionCode = "SampleVoided";

    private static readonly SampleCategory[] ItemBasedCategories =
        { SampleCategory.FinishedProduct, SampleCategory.RawMaterial, SampleCategory.PackagingMaterial };

    private readonly IMicroLimsDbContext _db;
    private readonly IElectronicSignatureService _signatures;
    private readonly IAuditEventService _audit;
    private readonly TimeProvider _time;

    public SampleCorrectionService(IMicroLimsDbContext db, IElectronicSignatureService signatures, IAuditEventService audit, TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _db = db;
        _signatures = signatures;
        _audit = audit;
    }

    // Received or In Testing - once submitted for review the data is locked.
    public static bool IsCorrectable(SampleStatus status) => status is SampleStatus.Received or SampleStatus.InTesting;

    public Task<SampleDto> CorrectAsync(
        int sampleId, SampleCorrectionRequest request, string reason, string password, int actingUserId, string? ipAddress) =>
        UnitOfWork.RunAsync(_db, () => CorrectCoreAsync(sampleId, request, reason, password, actingUserId, ipAddress));

    private async Task<SampleDto> CorrectCoreAsync(
        int sampleId, SampleCorrectionRequest request, string reason, string password, int actingUserId, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason for the correction is required.");
        var trimmedReason = reason.Trim();

        var sample = await LoadSampleAsync(sampleId);
        RecordVersion.EnsureCurrent(_db, sample);

        if (!IsCorrectable(sample.Status))
            throw new InvalidOperationException(
                "Sample details can only be corrected until the sample is submitted for review - this record is locked.");

        var changes = new List<AuditFieldChange>();
        var applies = new List<Action>();

        void Track<T>(string field, T current, T proposed, Func<T, string?> display, Action<T> apply)
        {
            if (EqualityComparer<T>.Default.Equals(current, proposed)) return;
            changes.Add(new AuditFieldChange(field, display(current), display(proposed)));
            applies.Add(() => apply(proposed));
        }

        var category = sample.Category;
        var isItemBased = ItemBasedCategories.Contains(category);
        var structural = await ResolveItemOrLocationChangeAsync(sample, request);
        if (sample.AwaitingTestSelection && (structural is not null || request.CauseOfTestingId != sample.CauseOfTestingId))
            throw new InvalidOperationException(
                "The item and cause of testing cannot change while the tests are still to be chosen. Void the sample and receive it again.");
        GradeContext? gradeContext = null;
        GradeOrderPlan? gradePlan = null;

        var controlNumber = Required(request.ControlNumber, "Control number");
        var sampledBy = Required(request.SampledBy, "Sampled by");
        Track("Control Number", Optional(sample.ControlNumber), controlNumber, v => v, v => sample.ControlNumber = v!);
        Track("Sampled By", Optional(sample.SampledBy), sampledBy, v => v, v => sample.SampledBy = v!);

        if (request.CauseOfTestingId != sample.CauseOfTestingId)
        {
            var newCause = await _db.CausesOfTesting.FirstOrDefaultAsync(c => c.Id == request.CauseOfTestingId)
                ?? throw new InvalidOperationException("The selected cause of testing does not exist.");
            // "Retest" is assigned by the system to OOS retest samples only.
            if (IsRetest(sample.CauseOfTesting) || IsRetest(newCause))
                throw new InvalidOperationException(
                    "The \"Retest\" cause of testing is assigned by the system to retest samples and cannot be set or changed here.");
            changes.Add(new AuditFieldChange("Cause of Testing", sample.CauseOfTesting?.Name, newCause.Name));
            applies.Add(() =>
            {
                sample.CauseOfTestingId = newCause.Id;
                sample.CauseOfTesting = newCause;
            });
        }

        if (isItemBased)
        {
            var batch = Required(request.BatchNumber, "Batch number");
            var mfg = AsDate(request.MfgDate);
            var exp = AsDate(request.ExpDate);
            if (mfg is not null && exp is not null && exp < mfg)
                throw new InvalidOperationException("The expiry date cannot be before the manufacturing date.");

            Track("Batch Number", Optional(sample.BatchNumber), batch, v => v, v => sample.BatchNumber = v);
            Track("Mfg Date", AsDate(sample.MfgDate), mfg, FormatDate, v => sample.MfgDate = v);
            Track("Exp Date", AsDate(sample.ExpDate), exp, FormatDate, v => sample.ExpDate = v);
            // Grade stated on the supplier's CoA: decides which tests apply.
            var effectiveItem = structural?.NewItem ?? sample.Item;
            // an omitted grade on an unchanged item means "unchanged"; a new item always
            // needs a grade of its own (the old one is not carried over).
            if (effectiveItem is not null && (structural is not null || request.Grade is not null))
            {
                var itemSpecs = await _db.Specifications.AsNoTracking().Where(s => s.ItemId == effectiveItem.Id).ToListAsync();
                var newGrade = ProductWorkflowEngine.ResolveGrade(
                    itemSpecs, structural is null ? request.Grade ?? sample.Grade : request.Grade);
                gradeContext = new GradeContext(itemSpecs, newGrade);
                if (newGrade != sample.Grade)
                {
                    // A new item rebuilds every test (and is locked once work exists);
                    // a grade change alone re-derives the set, only while nothing has started.
                    // A periodic retest awaiting its tests has none: the new grade is
                    // simply used when the tests are chosen.
                    if (structural is null && !sample.AwaitingTestSelection)
                    {
                        if (sample.TestOrders.Any(o => o.Status != ApprovalStatus.Pending))
                            throw new InvalidOperationException(
                                "The grade can only be corrected while every test is still pending. Void the sample and receive it again.");
                        gradePlan = await PlanGradeOrdersAsync(sample, gradeContext);
                    }
                    Track("Grade", sample.Grade, newGrade, v => v, v => sample.Grade = v);
                }
            }
            Track("Sample Quantity", Optional(sample.SampleQuantity), Optional(request.SampleQuantity), v => v, v => sample.SampleQuantity = v);
            if (category == SampleCategory.FinishedProduct)
            {
                // Required and resolved as at receipt: stage-dependent tests
                // read the id, so a corrected name must carry it.
                var stageName = Required(request.ProductionStage, "Production stage");
                int? stageId = await _db.ProductionStages
                    .Where(p => p.Name.ToLower() == stageName.ToLower())
                    .Select(p => (int?)p.Id)
                    .FirstOrDefaultAsync()
                    ?? throw new InvalidOperationException($"Production stage '{stageName}' is not a known stage.");
                Track("Production Stage", Optional(sample.ProductionStage), stageName, v => v, v =>
                {
                    sample.ProductionStage = v;
                    sample.ProductionStageId = stageId;
                });
            }
        }
        else if (category == SampleCategory.Water)
        {
            Track("Sample Quantity", Optional(sample.SampleQuantity), Optional(request.SampleQuantity), v => v, v => sample.SampleQuantity = v);

            // Storage is captured at preparation - it can be corrected once it
            // exists. A department change discards the preparation, storage included.
            if (sample.StorageCondition is not null && structural is null)
            {
                var condition = Optional(request.StorageCondition)
                    ?? throw new InvalidOperationException("Storage condition is required for a prepared water sample.");
                if (condition is not ("Refrigerator" or "RoomTemperature"))
                    throw new InvalidOperationException("Storage condition must be Refrigerator or RoomTemperature.");
                int? hours = null;
                if (condition == "Refrigerator")
                {
                    hours = request.StorageTimeHours
                        ?? throw new InvalidOperationException("Storage time is required when a water sample was refrigerated.");
                    if (hours < 0)
                        throw new InvalidOperationException("Storage time cannot be negative.");
                }

                Track("Storage Condition", sample.StorageCondition, condition, v => v, v => sample.StorageCondition = v);
                Track("Storage Time (h)", sample.StorageTimeHours, hours, v => v?.ToString(), v => sample.StorageTimeHours = v);
            }
        }
        else if (category == SampleCategory.AfterCleaning)
        {
            var previousProduct = Required(request.PreviousProductName, "Previous product");
            var previousBatch = Required(request.PreviousProductBatchNumber, "Previous product batch number");
            Track("Previous Product", Optional(sample.PreviousProductName), previousProduct, v => v, v => sample.PreviousProductName = v);
            // The After Cleaning batch number mirrors the previous product's batch (see AfterCleaningWorkflowEngine).
            Track("Previous Product Batch", Optional(sample.PreviousProductBatchNumber), previousBatch, v => v, v =>
            {
                sample.PreviousProductBatchNumber = v;
                sample.BatchNumber = v;
            });
        }

        if (structural is not null)
        {
            if (sample.OriginSampleId is not null)
                throw new InvalidOperationException(
                    "A retest sample must test the same item or location as its original sample, so it cannot be changed.");
            await EnsureNoTestWorkAsync(sample);

            changes.Add(new AuditFieldChange(structural.Field, structural.PreviousName, structural.NewName));

            var previousTests = TestCodes(sample.TestOrders);
            var newTests = structural.NewItem is not null
                ? string.Join(", ", structural.NewItem.AssignedTests.Where(t => gradeContext?.Applies(t.TestCode) ?? true).Select(t => t.TestCode).OrderBy(c => c))
                : string.Empty; // location categories create their tests at preparation
            if (previousTests != newTests)
                changes.Add(new AuditFieldChange("Tests", Blank(previousTests), Blank(newTests)));
            var newPreparationStatus = await PreparationStatusAfterChangeAsync(structural.NewItem, gradeContext);
            if (sample.PreparationStatus != newPreparationStatus)
                changes.Add(new AuditFieldChange("Preparation Status", sample.PreparationStatus.ToString(), newPreparationStatus.ToString()));
            if (sample.StorageCondition is not null && category == SampleCategory.Water)
                changes.Add(new AuditFieldChange("Storage Condition", sample.StorageCondition, null));
        }

        if (gradePlan is not null)
            changes.AddRange(gradePlan.Changes);

        if (changes.Count == 0)
            throw new InvalidOperationException("No changes were made.");

        // Signs first - a failed verification writes nothing about the sample
        // (the signature, the changes and the audit event commit together in
        // the single save inside RecordUserEventAsync below).
        _db.CurrentUserId = actingUserId;
        await _signatures.SignAsync(actingUserId, password, SignatureMeaning.SampleCorrected,
            ReviewEntityTypes.Sample, sample.Id, trimmedReason, ipAddress);

        foreach (var apply in applies) apply();
        if (structural is not null)
            await ApplyItemOrLocationChangeAsync(sample, structural, gradeContext);
        else if (gradePlan is not null)
            await ApplyGradeChangeAsync(sample, gradePlan);

        // Result projection rows copy the sample's identifiers at entry.
        var records = await _db.ResultRecords.Where(r => r.SampleId == sample.Id).ToListAsync();
        foreach (var record in records)
        {
            record.BatchNumber = sample.BatchNumber;
            record.ControlNumber = sample.ControlNumber;
            record.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        }

        await ReviewEventLog.LogAsync(_db, ReviewEntityTypes.Sample, sample.Id, actingUserId,
            ReviewWorkflowEventType.SampleCorrected,
            $"{trimmedReason} (changed: {string.Join(", ", changes.Select(c => c.FieldName))})");

        await _audit.RecordUserEventAsync(CorrectedActionCode, AuditActionCategory.Other, ReviewEntityTypes.Sample,
            reason: trimmedReason, changes: changes, entityId: sample.Id.ToString(), sampleId: sample.Id);

        return TestingWorkspaceService.ToDto(sample, nowUtc: _time.GetUtcNow().UtcDateTime);
    }

    // Voiding strikes the record - the sample was received in error or its
    // results cannot stand. It is deliberately NOT a rejection: rejection is a
    // Section Head's judgement that the material does not conform, so a void
    // records no approval decision, approver or decision time, and reports
    // and the certificate must never read it as one.
    public Task<SampleDto> VoidAsync(int sampleId, string reason, string password, int actingUserId, string? ipAddress) =>
        UnitOfWork.RunAsync(_db, () => VoidCoreAsync(sampleId, reason, password, actingUserId, ipAddress));

    private async Task<SampleDto> VoidCoreAsync(int sampleId, string reason, string password, int actingUserId, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason for voiding is required.");
        var trimmedReason = reason.Trim();

        var sample = await LoadSampleAsync(sampleId);

        if (sample.Status == SampleStatus.Voided)
            throw new InvalidOperationException("Sample is already voided.");

        // Signs first - a failed verification voids nothing.
        _db.CurrentUserId = actingUserId;
        await _signatures.SignAsync(actingUserId, password, SignatureMeaning.SampleVoided,
            ReviewEntityTypes.Sample, sample.Id, trimmedReason, ipAddress);

        var previousStatus = sample.Status;
        sample.Status = SampleStatus.Voided;
        // Kept so the reason still shows wherever the sample's remarks are displayed.
        sample.CertificateRemarks = $"Voided: {trimmedReason}";

        // Superseded orders are already history - their retest lives on another sample.
        var voidedOrders = sample.TestOrders.Where(t => !t.IsSuperseded).ToList();
        foreach (var order in voidedOrders)
        {
            order.Status = ApprovalStatus.Voided;
        }

        // Voiding strikes the whole sample, so every section's sign-off
        // closes with it (who reviewed/approved it stays on the row).
        foreach (var sectionId in SampleSectionRollup.SectionIds(sample))
        {
            SampleSectionRollup.GetOrAdd(sample, sectionId).Status = SectionSignoffStatus.Voided;
        }

        // Reports read the sample status off each result projection row.
        var records = await _db.ResultRecords.Where(r => r.SampleId == sampleId).ToListAsync();
        foreach (var record in records)
        {
            record.SampleStatus = SampleStatus.Voided;
            record.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        }

        await ReviewEventLog.LogAsync(_db, ReviewEntityTypes.Sample, sampleId, actingUserId,
            ReviewWorkflowEventType.SampleVoided, trimmedReason);

        var changes = new List<AuditFieldChange> { new("Status", previousStatus.ToString(), nameof(SampleStatus.Voided)) };
        if (voidedOrders.Count > 0)
            changes.Add(new AuditFieldChange("Tests Voided", null, TestCodes(voidedOrders)));

        await _audit.RecordUserEventAsync(VoidedActionCode, AuditActionCategory.Other, ReviewEntityTypes.Sample,
            reason: trimmedReason, changes: changes, entityId: sampleId.ToString(), sampleId: sampleId);

        return TestingWorkspaceService.ToDto(sample, nowUtc: _time.GetUtcNow().UtcDateTime);
    }

    private sealed record ItemOrLocationChange(string Field, string? PreviousName, string NewName, Item? NewItem, Action Apply);

    private async Task<ItemOrLocationChange?> ResolveItemOrLocationChangeAsync(Sample sample, SampleCorrectionRequest request)
    {
        switch (sample.Category)
        {
            case SampleCategory.FinishedProduct or SampleCategory.RawMaterial or SampleCategory.PackagingMaterial
                when request.ItemId is int itemId && itemId != sample.ItemId:
            {
                var item = await _db.Items.Include(i => i.AssignedTests).FirstOrDefaultAsync(i => i.Id == itemId)
                    ?? throw new InvalidOperationException("The selected item does not exist.");
                // The reference number encodes the category, so the item must stay in it.
                if (item.Category != sample.Category)
                    throw new InvalidOperationException("The new item must belong to the same category as the sample.");
                if (!item.IsActive)
                    throw new InvalidOperationException($"Item '{item.Name}' is frozen and cannot be used.");
                if (item.AssignedTests.Count == 0)
                    throw new InvalidOperationException($"Item '{item.Name}' has no assigned tests.");
                return new ItemOrLocationChange("Item", sample.Item?.Name, item.Name, item, () =>
                {
                    sample.ItemId = item.Id;
                    sample.Item = item;
                });
            }
            case SampleCategory.Water when request.WaterDepartmentId is int waterDepartmentId && waterDepartmentId != sample.WaterDepartmentId:
            {
                var department = await _db.WaterDepartments.FirstOrDefaultAsync(d => d.Id == waterDepartmentId)
                    ?? throw new InvalidOperationException("The selected water department does not exist.");
                var previous = sample.WaterDepartmentId is int previousId
                    ? await _db.WaterDepartments.Where(d => d.Id == previousId).Select(d => d.Name).FirstOrDefaultAsync()
                    : null;
                return new ItemOrLocationChange("Water Department", previous, department.Name, null, () => sample.WaterDepartmentId = department.Id);
            }
            case SampleCategory.EnvironmentalMonitoring when request.DepartmentId is int departmentId && departmentId != sample.DepartmentId:
            {
                var department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == departmentId)
                    ?? throw new InvalidOperationException("The selected department does not exist.");
                return new ItemOrLocationChange("Department", sample.Department?.Name, department.Name, null, () =>
                {
                    sample.DepartmentId = department.Id;
                    sample.Department = department;
                });
            }
            case SampleCategory.AfterCleaning when request.MachineId is int machineId && machineId != sample.MachineId:
            {
                var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Id == machineId)
                    ?? throw new InvalidOperationException("The selected machine does not exist.");
                return new ItemOrLocationChange("Machine", sample.Machine?.Name, machine.Name, null, () =>
                {
                    sample.MachineId = machine.Id;
                    sample.Machine = machine;
                });
            }
            default:
                return null;
        }
    }

    // Tests and results belong to the item / location they were created for.
    // Any recorded work locks the item / location - a wrong one then means a
    // void and a new receipt.
    private async Task EnsureNoTestWorkAsync(Sample sample)
    {
        var orderIds = sample.TestOrders.Select(t => t.Id).ToList();

        var workStarted = sample.Status != SampleStatus.Received
            || sample.TestOrders.Any(t => t.CurrentStep != WorkflowStep.Waiting)
            || await _db.Incubations.AnyAsync(i => i.TestOrderId != null && orderIds.Contains(i.TestOrderId.Value))
            || await _db.Results.AnyAsync(r => orderIds.Contains(r.TestOrderId))
            || await _db.CountTestReadings.AnyAsync(r => orderIds.Contains(r.TestOrderId))
            || await _db.PathogenObservations.AnyAsync(p => orderIds.Contains(p.TestOrderId))
            || await _db.WorkflowStepResults.AnyAsync(w => orderIds.Contains(w.TestOrderId))
            || await _db.LocationPathogenObservations.AnyAsync(o => orderIds.Contains(o.TestOrderId))
            || await _db.MediaUsages.AnyAsync(m => orderIds.Contains(m.TestOrderId))
            || await _db.RoomMonitorings.AnyAsync(r => orderIds.Contains(r.TestOrderId))
            || await _db.TestReturnEvents.AnyAsync(e => orderIds.Contains(e.TestOrderId))
            || await _db.SampleLocations.AnyAsync(l => l.SampleId == sample.Id && l.EnteredAt != null);

        if (workStarted)
            throw new InvalidOperationException(
                "Testing has already started for this sample, so its item or location can no longer change. Void the sample and receive it again.");
    }

    // Rebuilds what the item / location decided. None of it has been worked
    // (EnsureNoTestWorkAsync), so tests the new item still requires are kept
    // as they are, the others are removed with their locations, and the
    // sample returns to preparation. A location's tests are created again at
    // preparation. Rows are removed through the change tracker - never left to
    // the database cascade - so the audit capture records every one.
    private async Task ApplyItemOrLocationChangeAsync(Sample sample, ItemOrLocationChange change, GradeContext? grade)
    {
        // A new test inherits the analyst already on its laboratory section's
        // tests - never one from another section, who may not belong to it.
        var analystBySection = sample.TestOrders
            .Where(t => t.AssignedAnalystId != null)
            .GroupBy(t => t.SectionId)
            .ToDictionary(g => g.Key, g => g.First().AssignedAnalystId);

        var requiredCodes = change.NewItem?.AssignedTests.Select(t => t.TestCode).Where(c => grade?.Applies(c) ?? true).ToHashSet() ?? new HashSet<string>();
        await RemoveOrdersAsync(sample, sample.TestOrders.Where(t => !requiredCodes.Contains(t.TestCode)).ToList());

        var preparation = await _db.SamplePreparations.FirstOrDefaultAsync(p => p.SampleId == sample.Id);
        if (preparation is not null)
            _db.SamplePreparations.Remove(preparation);
        sample.PreparationStatus = SamplePreparationStatus.NeedsPreparation;

        if (sample.Category == SampleCategory.Water)
        {
            // Captured at preparation, which has to be done again.
            sample.StorageCondition = null;
            sample.StorageTimeHours = null;
        }

        change.Apply();

        if (change.NewItem is not null)
        {
            var newTests = change.NewItem.AssignedTests
                .Where(t => (grade?.Applies(t.TestCode) ?? true) && sample.TestOrders.All(o => o.TestCode != t.TestCode))
                .ToList();
            var testSections = await TestSectionLookup.ResolveAsync(_db, newTests.Select(t => t.TestCode));
            foreach (var test in newTests)
            {
                sample.TestOrders.Add(new TestOrder
                {
                    TestCode = test.TestCode,
                    SectionId = testSections[test.TestCode],
                    Status = ApprovalStatus.Pending,
                    CurrentStep = WorkflowStep.Waiting,
                    AssignedAnalystId = analystBySection.GetValueOrDefault(testSections[test.TestCode])
                });
            }
        }

        sample.PreparationStatus = await PreparationStatusAfterChangeAsync(change.NewItem, grade);
    }

    // Location categories create their tests at preparation, so they always
    // need it; an item's tests decide (FP-only items have no preparation).
    private async Task<SamplePreparationStatus> PreparationStatusAfterChangeAsync(Item? newItem, GradeContext? grade)
    {
        if (newItem is null)
            return SamplePreparationStatus.NeedsPreparation;
        var sections = await TestSectionLookup.ResolveAsync(_db,
            newItem.AssignedTests.Select(t => t.TestCode).Where(c => grade?.Applies(c) ?? true));
        return await PreparationRules.InitialStatusAsync(_db, sections.Values);
    }

    // The item's specifications read for one grade (see SpecificationLookup).
    private sealed record GradeContext(IReadOnlyList<Specification> Specs, string? Grade)
    {
        public bool Applies(string testCode) => ProductWorkflowEngine.AppliesToGrade(Specs, testCode, Grade);
    }

    // What a grade change does to the test orders (nothing has started):
    // tests that no longer apply go, newly applicable assigned tests come in
    // for the sample's laboratories, as at receiving.
    private sealed record GradeOrderPlan(
        List<TestOrder> Removed,
        List<(string TestCode, int SectionId)> Added,
        SamplePreparationStatus? PreparationStatus,
        List<AuditFieldChange> Changes);

    private async Task<GradeOrderPlan> PlanGradeOrdersAsync(Sample sample, GradeContext grade)
    {
        var labs = SampleSectionRollup.SectionIds(sample);
        var removed = sample.TestOrders.Where(o => !grade.Applies(o.TestCode)).ToList();
        var missing = (await _db.SampleTests.Where(t => t.ItemId == sample.ItemId).Select(t => t.TestCode).ToListAsync())
            .Where(c => grade.Applies(c) && sample.TestOrders.All(o => o.TestCode != c))
            .ToList();
        var sections = await TestSectionLookup.ResolveAsync(_db, missing);
        var added = missing.Where(c => labs.Contains(sections[c])).Select(c => (c, sections[c])).ToList();

        // Receiving never leaves a lab (or the sample) without tests, so neither may a correction.
        var remainingSections = sample.TestOrders.Except(removed).Select(o => o.SectionId)
            .Concat(added.Select(a => a.Item2)).ToHashSet();
        var emptiedLab = labs.Where(l => !remainingSections.Contains(l)).Cast<int?>().FirstOrDefault();
        if (emptiedLab is int emptied)
        {
            var labName = await _db.DocumentSections.Where(d => d.Id == emptied).Select(d => d.Name).FirstOrDefaultAsync() ?? $"section {emptied}";
            throw new InvalidOperationException($"This grade leaves {labName} with no tests; void the sample and receive it again.");
        }

        var changes = new List<AuditFieldChange>();
        var before = TestCodes(sample.TestOrders);
        var after = string.Join(", ", sample.TestOrders.Except(removed).Select(o => o.TestCode)
            .Concat(added.Select(a => a.Item1)).OrderBy(c => c));
        if (before != after)
            changes.Add(new AuditFieldChange("Tests", Blank(before), Blank(after)));

        // Preparation is kept once it has been done; otherwise the remaining
        // tests decide whether the sample needs it.
        SamplePreparationStatus? prep = null;
        if (before != after && !await _db.SamplePreparations.AnyAsync(p => p.SampleId == sample.Id))
        {
            var status = await PreparationRules.InitialStatusAsync(_db,
                sample.TestOrders.Except(removed).Select(o => o.SectionId).Concat(added.Select(a => a.Item2)));
            if (status != sample.PreparationStatus)
            {
                prep = status;
                changes.Add(new AuditFieldChange("Preparation Status", sample.PreparationStatus.ToString(), status.ToString()));
            }
        }
        return new GradeOrderPlan(removed, added, prep, changes);
    }

    private async Task ApplyGradeChangeAsync(Sample sample, GradeOrderPlan plan)
    {
        // A new test inherits the analyst already on its laboratory's tests.
        var analystBySection = sample.TestOrders
            .Where(t => t.AssignedAnalystId != null)
            .GroupBy(t => t.SectionId)
            .ToDictionary(g => g.Key, g => g.First().AssignedAnalystId);

        await RemoveOrdersAsync(sample, plan.Removed);
        foreach (var (testCode, sectionId) in plan.Added)
        {
            sample.TestOrders.Add(new TestOrder
            {
                TestCode = testCode,
                SectionId = sectionId,
                Status = ApprovalStatus.Pending,
                CurrentStep = WorkflowStep.Waiting,
                AssignedAnalystId = analystBySection.GetValueOrDefault(sectionId)
            });
        }
        if (plan.PreparationStatus is { } status)
            sample.PreparationStatus = status;
    }

    // Removes orders with their history and locations through the change
    // tracker, so the audit capture records every row.
    private async Task RemoveOrdersAsync(Sample sample, List<TestOrder> removedOrders)
    {
        var removedIds = removedOrders.Select(t => t.Id).ToList();

        // e.g. a "start refused - not prepared" entry on a test that never ran.
        var history = await _db.WorkflowHistories.Where(h => removedIds.Contains(h.TestOrderId)).ToListAsync();
        _db.WorkflowHistories.RemoveRange(history);

        foreach (var location in sample.Locations.Where(l => removedIds.Contains(l.TestOrderId)).ToList())
        {
            _db.SampleLocations.Remove(location);
            sample.Locations.Remove(location);
        }
        foreach (var order in removedOrders)
        {
            _db.TestOrders.Remove(order);
            sample.TestOrders.Remove(order);
        }
    }

    private async Task<Sample> LoadSampleAsync(int sampleId) =>
        await _db.Samples
            .Include(s => s.OriginSample)
            .Include(s => s.Item)
            .Include(s => s.WaterSamplingPoint)
            .Include(s => s.Department)
            .Include(s => s.Machine)
            .Include(s => s.CauseOfTesting)
            .Include(s => s.TestOrders)
            .Include(s => s.SectionSignoffs)
            .Include(s => s.Locations)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
        ?? throw new NotFoundException($"Sample {sampleId} not found.");

    private static string Required(string? value, string label) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"{label} is required.") : value.Trim();

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime? AsDate(DateTime? value) =>
        value is null ? null : DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc);

    private static string? FormatDate(DateTime? value) => value?.ToString("yyyy-MM-dd");

    private static bool IsRetest(CauseOfTesting? cause) =>
        string.Equals(cause?.Name, "Retest", StringComparison.OrdinalIgnoreCase);

    private static string TestCodes(IEnumerable<TestOrder> orders) =>
        string.Join(", ", orders.Select(t => t.TestCode).OrderBy(c => c));

    private static string? Blank(string value) => value.Length == 0 ? null : value;
}
