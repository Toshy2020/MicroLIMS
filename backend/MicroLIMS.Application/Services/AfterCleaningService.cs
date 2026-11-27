using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services;

public class AfterCleaningService
{
    private readonly IAfterCleaningWorkflowEngine _workflow;
    private readonly TimeProvider _time;

    public AfterCleaningService(IAfterCleaningWorkflowEngine workflow, TimeProvider? timeProvider = null)
    {
        _workflow = workflow;
        _time = timeProvider ?? TimeProvider.System;
    }

    public List<FieldError> Validate(AfterCleaningReceiveRequest request, int? row = null)
    {
        var errors = new List<FieldError>();
        if (string.IsNullOrWhiteSpace(request.PreviousProductBatchNumber))
            errors.Add(new FieldError("previousProductBatchNumber", "Previous Product Batch Number is required for After Cleaning receiving.", row));
        if (request.SampledAt.HasValue && request.SampledAt.Value > _time.GetUtcNow().UtcDateTime)
            errors.Add(new FieldError("sampledAt", "The sampling time cannot be after the time of receipt.", row));
        return errors;
    }

    public async Task<SampleDto> ReceiveAsync(AfterCleaningReceiveRequest request)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            var code = errors.Count == 1
                ? (errors[0].Field == "sampledAt"
                    ? MessageCodes.MonitoringSamplingTimeAfterReceipt
                    : MessageCodes.MonitoringAfterCleaningBatchRequired)
                : MessageCodes.MonitoringAfterCleaningBatchAndSamplingTimeInvalid;
            throw new BusinessRuleException(code, string.Join(" ", errors.Select(e => e.Message)));
        }

        var sample = await _workflow.ReceiveAsync(request);
        return TestingWorkspaceService.ToDto(sample);
    }

    public async Task<SampleDto> PrepareAsync(int sampleId, List<int> machinePartConfigurationIds, int userId, IReadOnlyList<int>? monitoringOccurrenceIds = null, CancellationToken ct = default)
    {
        var sample = await _workflow.PrepareAsync(sampleId, machinePartConfigurationIds, userId, monitoringOccurrenceIds, ct);
        return TestingWorkspaceService.ToDto(sample);
    }
}
