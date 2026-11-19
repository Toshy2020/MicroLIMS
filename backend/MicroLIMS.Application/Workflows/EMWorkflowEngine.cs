using System.Globalization;
using MicroLIMS.Application.Services.Monitoring;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Application.Abstractions.Persistence;

namespace MicroLIMS.Application.Workflows;

public record EMReceiveRequest(int DepartmentId, int CauseOfTestingId, string SampledBy, string ControlNumber, int ReceivedByUserId, DateTime? SampledAt = null);

public interface IEMWorkflowEngine : IStatefulWorkflowEngine
{
    // Receiving only captures the Department - creates a "needs
    // preparation" shell with no TestOrders yet.
    Task<Sample> ReceiveAsync(EMReceiveRequest request);

    // The checklist screen: selecting which rooms are included in this
    // monitoring session is what generates the TestOrders (one per
    // distinct TestCode across the selected rooms' configurations) and
    // the SampleLocation rows (one per selected RoomTestConfiguration).
    Task<Sample> PrepareAsync(int sampleId, List<int> roomTestConfigurationIds, int userId, IReadOnlyList<int>? monitoringOccurrenceIds = null);
}

public class EMWorkflowEngine : IEMWorkflowEngine
{
    private readonly IMicroLimsDbContext _db;
    private readonly Application.Services.ReferenceNumberGenerator _refNumbers;
    private readonly IUserSectionScopeService? _scope;
    private readonly MonitoringLinkService? _monitoring;

    public EMWorkflowEngine(IMicroLimsDbContext db, Application.Services.ReferenceNumberGenerator refNumbers, IUserSectionScopeService? scope = null, MonitoringLinkService? monitoring = null)
    {
        _db = db;
        _refNumbers = refNumbers;
        _scope = scope;
        _monitoring = monitoring;
    }

    // Generating the reference number and saving the sample run as one
    // transaction, with the number's lock held (see ReferenceNumberGenerator).
    public Task<Sample> ReceiveAsync(EMReceiveRequest request) =>
        UnitOfWork.RunAsync(_db, () => ReceiveCoreAsync(request));

    private async Task<Sample> ReceiveCoreAsync(EMReceiveRequest request)
    {
        var department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == request.DepartmentId)
            ?? throw new BusinessRuleException(
                MessageCodes.MonitoringDepartmentNotFound,
                $"Department {request.DepartmentId} not found.",
                new Dictionary<string, string> { ["departmentId"] = request.DepartmentId.ToString(CultureInfo.InvariantCulture) });
        if (!department.IsActive)
            throw new BusinessRuleException(
                MessageCodes.MonitoringDepartmentInactive,
                $"Department '{department.Name}' is inactive and cannot receive samples.",
                new Dictionary<string, string> { ["department"] = department.Name });

        var sample = new Sample
        {
            ReferenceNumber = await _refNumbers.GenerateAsync(SampleCategory.EnvironmentalMonitoring),
            Category = SampleCategory.EnvironmentalMonitoring,
            DepartmentId = department.Id,
            CauseOfTestingId = request.CauseOfTestingId,
            SampledBy = request.SampledBy,
            ControlNumber = request.ControlNumber,
            ReceivedByUserId = request.ReceivedByUserId,
            SampledAt = request.SampledAt,
            Status = SampleStatus.Received,
            PreparationStatus = SamplePreparationStatus.NeedsPreparation
        };

        if (sample.SampledAt > sample.ReceivedAt)
            throw new BusinessRuleException(
                MessageCodes.MonitoringSamplingTimeAfterReceipt,
                "The sampling time cannot be after the time of receipt.");

        _db.Samples.Add(sample);

        // Two receipts at the same moment both pick the same next number.
        // The unique index rejects the second save, which then takes the
        // number after the one that won. A second clash in a row is
        // reported rather than retried again.
        if (!await _db.TrySaveChangesAsync(UniqueIndexNames.SampleReferenceNumber))
        {
            sample.ReferenceNumber = await _refNumbers.GenerateAsync(SampleCategory.EnvironmentalMonitoring);
            if (!await _db.TrySaveChangesAsync(UniqueIndexNames.SampleReferenceNumber))
                throw new BusinessRuleException(
                    MessageCodes.ReceivingReferenceNumberClash,
                    $"Reference number {sample.ReferenceNumber} was taken by another receipt at the same moment. Nothing was saved - receive the sample again.",
                    new Dictionary<string, string> { ["referenceNumber"] = sample.ReferenceNumber });
        }

        return sample;
    }

    public Task<Sample> PrepareAsync(int sampleId, List<int> roomTestConfigurationIds, int userId, IReadOnlyList<int>? monitoringOccurrenceIds = null) =>
        monitoringOccurrenceIds is { Count: > 0 }
            ? UnitOfWork.RunAsync(_db, () => PrepareCoreAsync(sampleId, roomTestConfigurationIds, userId, monitoringOccurrenceIds))
            : PrepareCoreAsync(sampleId, roomTestConfigurationIds, userId, null);

    private async Task<Sample> PrepareCoreAsync(int sampleId, List<int> roomTestConfigurationIds, int userId, IReadOnlyList<int>? monitoringOccurrenceIds)
    {
        var sample = await _db.Samples.Include(s => s.TestOrders).Include(s => s.Locations).FirstOrDefaultAsync(s => s.Id == sampleId)
            ?? throw new NotFoundException(
                MessageCodes.SampleNotFound,
                $"Sample {sampleId} not found.",
                new Dictionary<string, string> { ["sampleId"] = sampleId.ToString(CultureInfo.InvariantCulture) });

        if (sample.PreparationStatus != SamplePreparationStatus.NeedsPreparation)
            throw new BusinessRuleException(
                MessageCodes.SampleAlreadyPrepared,
                "This sample has already been prepared.");

        if (roomTestConfigurationIds.Count == 0)
            throw new BusinessRuleException(
                MessageCodes.MonitoringRoomRequired,
                "At least one room must be selected.");

        var configs = await _db.RoomTestConfigurations
            .Include(c => c.Room)
            .Where(c => roomTestConfigurationIds.Contains(c.Id))
            .ToListAsync();

        var missing = roomTestConfigurationIds.Except(configs.Select(c => c.Id)).ToList();
        if (missing.Count > 0)
        {
            var ids = string.Join(", ", missing);
            throw new BusinessRuleException(
                MessageCodes.MonitoringRoomTestConfigsNotFound,
                $"Room test configuration(s) not found: {ids}.",
                new Dictionary<string, string> { ["ids"] = ids });
        }

        var inactive = configs.Where(c => !c.IsActive || c.Room?.IsActive == false).ToList();
        if (inactive.Count > 0)
        {
            var rooms = string.Join(", ", inactive.Select(c => c.Room?.Name));
            throw new BusinessRuleException(
                MessageCodes.MonitoringRoomsInactiveCannotPrepare,
                $"Room(s) {rooms} are inactive and cannot be prepared.",
                new Dictionary<string, string> { ["rooms"] = rooms });
        }

        var wrongDepartment = configs.Where(c => c.Room?.DepartmentId != sample.DepartmentId).ToList();
        if (wrongDepartment.Count > 0)
        {
            var rooms = string.Join(", ", wrongDepartment.Select(c => c.Room?.Name));
            throw new BusinessRuleException(
                MessageCodes.MonitoringRoomsWrongDepartment,
                $"Room(s) {rooms} do not belong to this sample's department.",
                new Dictionary<string, string> { ["rooms"] = rooms });
        }

        // One TestOrder per distinct TestCode across every selected room -
        // the whole batch shares a single incubation setup per test type.
        var testSections = await TestSectionLookup.ResolveAsync(_db, configs.Select(c => c.TestCode));
        if (_scope is not null)
            await _scope.EnsureSectionsAccessAsync(userId, testSections.Values);
        var testOrdersByCode = new Dictionary<string, TestOrder>();
        foreach (var config in configs)
        {
            if (!testOrdersByCode.TryGetValue(config.TestCode, out var order))
            {
                order = new TestOrder
                {
                    TestCode = config.TestCode,
                    SectionId = testSections[config.TestCode],
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
                LocationType = LocationType.Room,
                RoomTestConfigurationId = config.Id
            });
        }

        if (monitoringOccurrenceIds is { Count: > 0 })
        {
            if (_monitoring is null)
                throw new BusinessRuleException(
                    MessageCodes.MonitoringPlanLinkingNotAvailable,
                    "Monitoring plan linking is not available.");
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
        if (errors.Count > 0)
        {
            var message = string.Join(" ", errors);
            throw new BusinessRuleException(
                MessageCodes.MonitoringAdvanceValidationFailed,
                message,
                new Dictionary<string, string> { ["message"] = message });
        }

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
