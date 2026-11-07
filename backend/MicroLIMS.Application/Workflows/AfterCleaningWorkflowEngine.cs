using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Workflows;

public record AfterCleaningReceiveRequest(
    int MachineId,
    int CauseOfTestingId,
    string SampledBy,
    string ControlNumber,
    int ReceivedByUserId,
    string? PreviousProductName = null,
    string? PreviousProductBatchNumber = null,
    DateTime? SampledAt = null,
    int? LabSectionId = null,
    int? PreviousProductItemId = null,
    List<string>? Techniques = null
);

public interface IAfterCleaningWorkflowEngine : IStatefulWorkflowEngine
{
    Task<Sample> ReceiveAsync(AfterCleaningReceiveRequest request);

    // The checklist screen: selecting which machine parts are included
    // in this batch is what generates the TestOrders (one per distinct
    // TestCode across the selected parts' configurations) and the
    // SampleLocation rows (one per selected MachinePartConfiguration).
    Task<Sample> PrepareAsync(int sampleId, List<int> machinePartConfigurationIds, int userId, IReadOnlyList<int>? monitoringOccurrenceIds = null);
}

public class AfterCleaningWorkflowEngine : IAfterCleaningWorkflowEngine
{
    private readonly IMicroLimsDbContext _db;
    private readonly Application.Services.ReferenceNumberGenerator _refNumbers;
    private readonly IUserSectionScopeService? _scope;
    private readonly MonitoringLinkService? _monitoring;

    public AfterCleaningWorkflowEngine(IMicroLimsDbContext db, Application.Services.ReferenceNumberGenerator refNumbers, IUserSectionScopeService? scope = null, MonitoringLinkService? monitoring = null)
    {
        _db = db;
        _refNumbers = refNumbers;
        _scope = scope;
        _monitoring = monitoring;
    }

    public async Task<Sample> ReceiveAsync(AfterCleaningReceiveRequest request)
    {
        var machine = await _db.Machines.FirstOrDefaultAsync(m => m.Id == request.MachineId)
            ?? throw new InvalidOperationException($"Machine {request.MachineId} not found.");
        if (!machine.IsActive)
            throw new InvalidOperationException($"Machine '{machine.Name}' is inactive and cannot receive samples.");

        if (string.IsNullOrWhiteSpace(request.PreviousProductBatchNumber))
        {
            throw new InvalidOperationException("Previous Product Batch Number is required for After Cleaning receiving.");
        }

        int? labSectionId;
        if (_scope != null)
        {
            labSectionId = await _scope.ResolveSectionForCreateAsync(request.ReceivedByUserId, request.LabSectionId);
        }
        else
        {
            labSectionId = request.LabSectionId
                ?? await _db.DocumentSections
                    .Where(s => s.Code == LaboratoryCodes.Microbiology)
                    .Select(s => (int?)s.Id)
                    .FirstOrDefaultAsync();
        }

        string? sectionCode = null;
        if (labSectionId.HasValue)
        {
            sectionCode = await _db.DocumentSections
                .Where(s => s.Id == labSectionId.Value)
                .Select(s => s.Code)
                .FirstOrDefaultAsync();

            if (string.Equals(sectionCode, PhyschemAreas.SectionCode, StringComparison.OrdinalIgnoreCase))
            {
                if (_scope != null)
                {
                    var areas = await _scope.GetPhyschemAreasAsync(request.ReceivedByUserId);
                    if (!areas.Contains(PhyschemAreas.OfCategory(SampleCategory.AfterCleaning)))
                    {
                        throw new UnauthorizedAccessException("After-cleaning samples are received by the FP area.");
                    }
                }
            }
        }

        bool isPhyschem = string.Equals(sectionCode, PhyschemAreas.SectionCode, StringComparison.OrdinalIgnoreCase);

        int? previousProductItemId = null;
        string? previousProductName = null;
        string? cleaningTechniques = null;

        if (request.PreviousProductItemId.HasValue)
        {
            var item = await _db.Items.FirstOrDefaultAsync(i => i.Id == request.PreviousProductItemId.Value)
                ?? throw new InvalidOperationException($"Item {request.PreviousProductItemId.Value} not found.");
            if (!item.IsActive)
                throw new InvalidOperationException($"Item '{item.Name}' is inactive and cannot be selected.");
            if (item.Category != SampleCategory.FinishedProduct)
                throw new InvalidOperationException($"Item '{item.Name}' is not a Finished Product.");

            previousProductItemId = item.Id;
            previousProductName = item.Name;
        }

        if (isPhyschem)
        {
            if (!request.PreviousProductItemId.HasValue)
            {
                throw new InvalidOperationException("Previous product item is required for Physicochemical after-cleaning receiving.");
            }
            if (request.Techniques == null || !request.Techniques.Any(t => !string.IsNullOrWhiteSpace(t)))
            {
                throw new InvalidOperationException("At least one cleaning technique is required.");
            }
            cleaningTechniques = CleaningTechniques.Normalize(request.Techniques);
        }
        else
        {
            cleaningTechniques = null;
            if (!request.PreviousProductItemId.HasValue)
            {
                if (string.IsNullOrWhiteSpace(request.PreviousProductName))
                {
                    throw new InvalidOperationException("Previous Product is required for After Cleaning receiving.");
                }
                previousProductName = request.PreviousProductName.Trim();
            }
        }

        var sample = new Sample
        {
            ReferenceNumber = await _refNumbers.GenerateAsync(SampleCategory.AfterCleaning),
            Category = SampleCategory.AfterCleaning,
            MachineId = machine.Id,
            ItemId = null,
            LabSectionId = labSectionId,
            PreviousProductItemId = previousProductItemId,
            PreviousProductName = previousProductName,
            PreviousProductBatchNumber = request.PreviousProductBatchNumber.Trim(),
            BatchNumber = request.PreviousProductBatchNumber.Trim(),
            CleaningTechniques = cleaningTechniques,
            CauseOfTestingId = request.CauseOfTestingId,
            SampledBy = request.SampledBy,
            ControlNumber = request.ControlNumber,
            ReceivedByUserId = request.ReceivedByUserId,
            SampledAt = request.SampledAt,
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.NeedsPreparation
        };

        if (sample.SampledAt > sample.ReceivedAt)
            throw new InvalidOperationException("The sampling time cannot be after the time of receipt.");

        _db.Samples.Add(sample);

        // Two receipts at the same moment both pick the same next number.
        // The unique index rejects the second save, which then takes the
        // number after the one that won. A second clash in a row is
        // reported rather than retried again.
        if (!await _db.TrySaveChangesAsync(UniqueIndexNames.SampleReferenceNumber))
        {
            sample.ReferenceNumber = await _refNumbers.GenerateAsync(SampleCategory.AfterCleaning);
            if (!await _db.TrySaveChangesAsync(UniqueIndexNames.SampleReferenceNumber))
                throw new InvalidOperationException(
                    $"Reference number {sample.ReferenceNumber} was taken by another receipt at the same moment. Nothing was saved - receive the sample again.");
        }

        return sample;
    }

    public Task<Sample> PrepareAsync(int sampleId, List<int> machinePartConfigurationIds, int userId, IReadOnlyList<int>? monitoringOccurrenceIds = null) =>
        monitoringOccurrenceIds is { Count: > 0 }
            ? UnitOfWork.RunAsync(_db, () => PrepareCoreAsync(sampleId, machinePartConfigurationIds, userId, monitoringOccurrenceIds))
            : PrepareCoreAsync(sampleId, machinePartConfigurationIds, userId, null);

    private async Task<Sample> PrepareCoreAsync(int sampleId, List<int> machinePartConfigurationIds, int userId, IReadOnlyList<int>? monitoringOccurrenceIds)
    {
        var sample = await _db.Samples
            .Include(s => s.TestOrders)
            .Include(s => s.Locations)
            .Include(s => s.PreviousProductItem)
            .FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new NotFoundException($"Sample {sampleId} not found.");

        if (sample.PreparationStatus != SamplePreparationStatus.NeedsPreparation)
            throw new InvalidOperationException("This sample has already been prepared.");

        if (machinePartConfigurationIds.Count == 0)
            throw new InvalidOperationException("At least one machine part must be selected.");

        var configs = await _db.MachinePartConfigurations
            .Include(c => c.MachinePart)
            .Where(c => machinePartConfigurationIds.Contains(c.Id))
            .ToListAsync();

        var missing = machinePartConfigurationIds.Except(configs.Select(c => c.Id)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Machine part configuration(s) not found: {string.Join(", ", missing)}.");

        var inactive = configs.Where(c => !c.IsActive || c.MachinePart?.IsActive == false).ToList();
        if (inactive.Count > 0)
            throw new InvalidOperationException(
                $"Part(s) {string.Join(", ", inactive.Select(c => c.MachinePart?.Name))} are inactive and cannot be prepared.");

        var wrongMachine = configs.Where(c => c.MachinePart?.MachineId != sample.MachineId).ToList();
        if (wrongMachine.Count > 0)
            throw new InvalidOperationException(
                $"Part(s) {string.Join(", ", wrongMachine.Select(c => c.MachinePart?.Name))} do not belong to this sample's machine.");

        var microSection = await _db.DocumentSections
            .FirstOrDefaultAsync(s => s.Code == LaboratoryCodes.Microbiology);
        var labId = sample.LabSectionId ?? microSection?.Id ?? 0;
        var labSection = await _db.DocumentSections
            .FirstOrDefaultAsync(s => s.Id == labId);
        bool isPhyschem = string.Equals(labSection?.Code, PhyschemAreas.SectionCode, StringComparison.OrdinalIgnoreCase);

        var allCodes = configs.Select(c => c.TestCode).Distinct().ToList();
        var testDefs = await _db.TestDefinitions
            .Include(t => t.HplcMethod)
            .Where(t => allCodes.Contains(t.Code))
            .ToListAsync();

        var allowedDefs = testDefs.Where(t =>
            t.SectionId == labId &&
            (!isPhyschem || PhyschemAreas.Includes(t.PhyschemArea, PhyschemAreas.OfCategory(sample.Category)))
        ).ToList();
        var allowedCodes = allowedDefs.Select(t => t.Code).ToHashSet(StringComparer.Ordinal);

        foreach (var config in configs)
        {
            if (!allowedCodes.Contains(config.TestCode))
            {
                throw new InvalidOperationException($"Part test {config.TestCode} belongs to another laboratory.");
            }
        }

        var defsByCode = testDefs.ToDictionary(t => t.Code, StringComparer.Ordinal);

        if (isPhyschem)
        {
            var chosenTechniques = (sample.CleaningTechniques ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var config in configs)
            {
                if (defsByCode.TryGetValue(config.TestCode, out var testDef))
                {
                    var technique = CleaningTechniques.Of(testDef.WorkflowType);
                    if (!chosenTechniques.Contains(technique))
                    {
                        throw new InvalidOperationException($"Part test {config.TestCode} is {technique}, which was not chosen at receipt.");
                    }

                    if (testDef.WorkflowType == WorkflowType.HplcMethodAssay)
                    {
                        var method = testDef.HplcMethod;
                        if (method == null || method.ResultMode != HplcResultMode.CleaningResidue || method.ProductItemId != sample.PreviousProductItemId)
                        {
                            var productName = sample.PreviousProductItem?.Name
                                ?? (await _db.Items.Where(i => i.Id == sample.PreviousProductItemId).Select(i => i.Name).FirstOrDefaultAsync())
                                ?? "the previous product";
                            throw new InvalidOperationException($"No cleaning HPLC method for {productName} on test {testDef.Code}.");
                        }
                    }
                }
            }
        }

        if (_scope is not null)
            await _scope.EnsureSectionsAccessAsync(userId, new[] { labId });

        var testOrdersByCode = new Dictionary<string, TestOrder>();
        foreach (var config in configs)
        {
            var testDef = defsByCode.TryGetValue(config.TestCode, out var td) ? td : null;
            var isHplcOrIcp = isPhyschem && testDef != null &&
                (testDef.WorkflowType is WorkflowType.HplcMethodAssay or WorkflowType.IcpMethodAssay);

            if (isHplcOrIcp)
            {
                var order = new TestOrder
                {
                    TestCode = config.TestCode,
                    SectionId = labId,
                    Status = ApprovalStatus.Pending,
                    CurrentStep = WorkflowStep.Waiting,
                    AssignedAnalystId = userId
                };
                sample.TestOrders.Add(order);

                sample.Locations.Add(new SampleLocation
                {
                    TestOrder = order,
                    LocationType = LocationType.MachinePart,
                    MachinePartConfigurationId = config.Id
                });
            }
            else
            {
                if (!testOrdersByCode.TryGetValue(config.TestCode, out var order))
                {
                    order = new TestOrder
                    {
                        TestCode = config.TestCode,
                        SectionId = labId,
                        Status = ApprovalStatus.Pending,
                        CurrentStep = WorkflowStep.Waiting,
                        AssignedAnalystId = userId
                    };
                    sample.TestOrders.Add(order);
                    testOrdersByCode[config.TestCode] = order;
                }

                sample.Locations.Add(new SampleLocation
                {
                    TestOrder = order,
                    LocationType = LocationType.MachinePart,
                    MachinePartConfigurationId = config.Id
                });
            }
        }

        if (monitoringOccurrenceIds is { Count: > 0 })
        {
            if (_monitoring is null)
                throw new InvalidOperationException("Monitoring plan linking is not available.");
            await _monitoring.LinkAsync(sample, monitoringOccurrenceIds, userId);
        }

        sample.PreparationStatus = SamplePreparationStatus.Ready;
        await _db.SaveChangesAsync();
        return sample;
    }

    public async Task<WorkflowStep> AdvanceAsync(int testOrderId, int performedByUserId, string? note = null)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        var errors = await ValidateAsync(testOrderId);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));

        var next = order.CurrentStep switch
        {
            WorkflowStep.Ready => WorkflowStep.Reviewed,
            WorkflowStep.Reviewed => WorkflowStep.Approved,
            _ => order.CurrentStep
        };
        return await WorkflowStateMachine.TransitionAsync(_db, order, next, performedByUserId, note);
    }

    public async Task<List<string>> ValidateAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        var errors = new List<string>();
        if (order.CurrentStep is WorkflowStep.Waiting or WorkflowStep.Running or WorkflowStep.Incubating)
            errors.Add("Batch results must be entered for every location before this test order can proceed.");
        return errors;
    }

    public async Task<bool> IsCompleteAsync(int testOrderId)
    {
        var order = await WorkflowStateMachine.LoadOrThrowAsync(_db, testOrderId);
        return order.CurrentStep == WorkflowStep.Approved;
    }
}
