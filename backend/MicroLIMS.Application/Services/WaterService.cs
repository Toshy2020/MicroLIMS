using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services;

public class WaterService
{
    private readonly IWaterWorkflowEngine _workflow;
    private readonly TimeProvider _time;

    public WaterService(IWaterWorkflowEngine workflow, TimeProvider? timeProvider = null)
    {
        _workflow = workflow;
        _time = timeProvider ?? TimeProvider.System;
    }

    public List<FieldError> Validate(WaterReceiveRequest request, int? row = null)
    {
        var errors = new List<FieldError>();
        if (request.SampledAt.HasValue && request.SampledAt.Value > _time.GetUtcNow().UtcDateTime)
            errors.Add(new FieldError("sampledAt", "The sampling time cannot be after the time of receipt.", row));
        return errors;
    }

    public async Task<SampleDto> ReceiveAsync(WaterReceiveRequest request)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
            throw new BusinessRuleException(
                MessageCodes.MonitoringSamplingTimeAfterReceipt,
                string.Join(" ", errors.Select(e => e.Message)));

        var sample = await _workflow.ReceiveAsync(request);
        return TestingWorkspaceService.ToDto(sample);
    }

    public async Task<SampleDto> PrepareAsync(int sampleId, List<int> waterSamplingPointIds, int userId, string? storageCondition = null, int? storageTimeHours = null, IReadOnlyList<int>? monitoringOccurrenceIds = null, CancellationToken ct = default)
    {
        var sample = await _workflow.PrepareAsync(sampleId, waterSamplingPointIds, userId, storageCondition, storageTimeHours, monitoringOccurrenceIds, ct);
        return TestingWorkspaceService.ToDto(sample);
    }

    public Task<List<WaterComparisonResult>> GetDailyAggregateAsync(DateTime date, IReadOnlyCollection<int>? sectionIds = null) =>
        _workflow.GetDailyAggregateAsync(date, sectionIds);
}
