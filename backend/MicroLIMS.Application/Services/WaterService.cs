using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Workflows;

namespace MicroLIMS.Application.Services;

public class WaterService
{
    private readonly IWaterWorkflowEngine _workflow;

    public WaterService(IWaterWorkflowEngine workflow)
    {
        _workflow = workflow;
    }

    public async Task<SampleDto> ReceiveAsync(WaterReceiveRequest request)
    {
        var sample = await _workflow.ReceiveAsync(request);
        return TestingWorkspaceService.ToDto(sample);
    }

    public async Task<SampleDto> PrepareAsync(int sampleId, List<int> waterSamplingPointIds, int userId, string? storageCondition = null, int? storageTimeHours = null)
    {
        var sample = await _workflow.PrepareAsync(sampleId, waterSamplingPointIds, userId, storageCondition, storageTimeHours);
        return TestingWorkspaceService.ToDto(sample);
    }

    public Task<WaterComparisonResult> CalculateAsync(int testOrderId, List<decimal> readings) =>
        _workflow.CalculateAndCompareAsync(testOrderId, readings);

    public Task<List<WaterComparisonResult>> GetDailyAggregateAsync(DateTime date) =>
        _workflow.GetDailyAggregateAsync(date);
}
