using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

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
    int? MachineId = null);

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

    private readonly MicroLimsDbContext _db;
    private readonly IElectronicSignatureService _signatures;
    private readonly IAuditEventService _audit;

    public SampleCorrectionService(MicroLimsDbContext db, IElectronicSignatureService signatures, IAuditEventService audit)
    {
        _db = db;
        _signatures = signatures;
        _audit = audit;
    }

    // Received or In Testing - once submitted for review the data is locked.
    public static bool IsCorrectable(SampleStatus status) => status is SampleStatus.Received or SampleStatus.InTesting;

    public async Task<SampleDto> CorrectAsync(
        int sampleId, SampleCorrectionRequest request, string reason, string password, int actingUserId, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("A reason for the correction is required.");
        var trimmedReason = reason.Trim();

        var sample = await LoadSampleAsync(sampleId);

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
            Track("Sample Quantity", Optional(sample.SampleQuantity), Optional(request.SampleQuantity), v => v, v => sample.SampleQuantity = v);
            if (category == SampleCategory.FinishedProduct)
                Track("Production Stage", Optional(sample.ProductionStage), Optional(request.ProductionStage), v => v, v => sample.ProductionStage = v);
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
                ? string.Join(", ", structural.NewItem.AssignedTests.Select(t => t.TestCode).OrderBy(c => c))
                : string.Empty; // location categories create their tests at preparation
            if (previousTests != newTests)
                changes.Add(new AuditFieldChange("Tests", Blank(previousTests), Blank(newTests)));
            if (sample.PreparationStatus != SamplePreparationStatus.NeedsPreparation)
                changes.Add(new AuditFieldChange("Preparation Status", sample.PreparationStatus.ToString(), nameof(SamplePreparationStatus.NeedsPreparation)));
            if (sample.StorageCondition is not null && category == SampleCategory.Water)
                changes.Add(new AuditFieldChange("Storage Condition", sample.StorageCondition, null));
        }

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
            await ApplyItemOrLocationChangeAsync(sample, structural);

        // Result projection rows copy the sample's identifiers at entry.
        var records = await _db.ResultRecords.Where(r => r.SampleId == sample.Id).ToListAsync();
        foreach (var record in records)
        {
            record.BatchNumber = sample.BatchNumber;
            record.ControlNumber = sample.ControlNumber;
            record.UpdatedAt = DateTime.UtcNow;
        }

        await ReviewEventLog.LogAsync(_db, ReviewEntityTypes.Sample, sample.Id, actingUserId,
            ReviewWorkflowEventType.SampleCorrected,
            $"{trimmedReason} (changed: {string.Join(", ", changes.Select(c => c.FieldName))})");

        await _audit.RecordUserEventAsync(CorrectedActionCode, AuditActionCategory.Other, ReviewEntityTypes.Sample,
            reason: trimmedReason, changes: changes, entityId: sample.Id.ToString(), sampleId: sample.Id);

        return TestingWorkspaceService.ToDto(sample);
    }

    // Voiding strikes the record - the sample was received in error or its
    // results cannot stand. It is deliberately NOT a rejection: rejection is a
    // Section Head's judgement that the material does not conform, so a void
    // records no approval decision, approver or decision time, and reports
    // and the certificate must never read it as one.
    public async Task<SampleDto> VoidAsync(int sampleId, string reason, string password, int actingUserId, string? ipAddress)
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

        // Reports read the sample status off each result projection row.
        var records = await _db.ResultRecords.Where(r => r.SampleId == sampleId).ToListAsync();
        foreach (var record in records)
        {
            record.SampleStatus = SampleStatus.Voided;
            record.UpdatedAt = DateTime.UtcNow;
        }

        await ReviewEventLog.LogAsync(_db, ReviewEntityTypes.Sample, sampleId, actingUserId,
            ReviewWorkflowEventType.SampleVoided, trimmedReason);

        var changes = new List<AuditFieldChange> { new("Status", previousStatus.ToString(), nameof(SampleStatus.Voided)) };
        if (voidedOrders.Count > 0)
            changes.Add(new AuditFieldChange("Tests Voided", null, TestCodes(voidedOrders)));

        await _audit.RecordUserEventAsync(VoidedActionCode, AuditActionCategory.Other, ReviewEntityTypes.Sample,
            reason: trimmedReason, changes: changes, entityId: sampleId.ToString(), sampleId: sampleId);

        return TestingWorkspaceService.ToDto(sample);
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
    private async Task ApplyItemOrLocationChangeAsync(Sample sample, ItemOrLocationChange change)
    {
        var assignedAnalystId = sample.TestOrders.FirstOrDefault(t => t.AssignedAnalystId != null)?.AssignedAnalystId;

        var requiredCodes = change.NewItem?.AssignedTests.Select(t => t.TestCode).ToHashSet() ?? new HashSet<string>();
        var removedOrders = sample.TestOrders.Where(t => !requiredCodes.Contains(t.TestCode)).ToList();
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
                .Where(t => sample.TestOrders.All(o => o.TestCode != t.TestCode))
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
                    AssignedAnalystId = assignedAnalystId
                });
            }
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
            .Include(s => s.Locations)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
        ?? throw new InvalidOperationException($"Sample {sampleId} not found.");

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
