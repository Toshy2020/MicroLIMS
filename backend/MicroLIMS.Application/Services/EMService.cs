using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services;

public class EMService
{
    private readonly IEMWorkflowEngine _workflow;
    private readonly TimeProvider _time;

    public EMService(IEMWorkflowEngine workflow, TimeProvider? timeProvider = null)
    {
        _workflow = workflow;
        _time = timeProvider ?? TimeProvider.System;
    }

    public List<FieldError> Validate(EMReceiveRequest request, int? row = null)
    {
        var errors = new List<FieldError>();
        if (request.SampledAt.HasValue && request.SampledAt.Value > _time.GetUtcNow().UtcDateTime)
            errors.Add(new FieldError("sampledAt", "The sampling time cannot be after the time of receipt.", row));
        return errors;
    }

    public async Task<SampleDto> ReceiveAsync(EMReceiveRequest request)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
            throw new BusinessRuleException(
                MessageCodes.MonitoringSamplingTimeAfterReceipt,
                string.Join(" ", errors.Select(e => e.Message)));

        var sample = await _workflow.ReceiveAsync(request);
        return TestingWorkspaceService.ToDto(sample);
    }

    public async Task<SampleDto> PrepareAsync(int sampleId, List<int> roomTestConfigurationIds, int userId, IReadOnlyList<int>? monitoringOccurrenceIds = null, CancellationToken ct = default)
    {
        var sample = await _workflow.PrepareAsync(sampleId, roomTestConfigurationIds, userId, monitoringOccurrenceIds, ct);
        return TestingWorkspaceService.ToDto(sample);
    }
}
