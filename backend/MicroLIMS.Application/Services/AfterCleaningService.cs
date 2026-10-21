using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Workflows;

namespace MicroLIMS.Application.Services;

public class AfterCleaningService
{
    private readonly IAfterCleaningWorkflowEngine _workflow;

    public AfterCleaningService(IAfterCleaningWorkflowEngine workflow)
    {
        _workflow = workflow;
    }

    public async Task<SampleDto> ReceiveAsync(AfterCleaningReceiveRequest request)
    {
        var sample = await _workflow.ReceiveAsync(request);
        return TestingWorkspaceService.ToDto(sample);
    }

    public async Task<SampleDto> PrepareAsync(int sampleId, List<int> machinePartConfigurationIds, int userId, IReadOnlyList<int>? monitoringOccurrenceIds = null)
    {
        var sample = await _workflow.PrepareAsync(sampleId, machinePartConfigurationIds, userId, monitoringOccurrenceIds);
        return TestingWorkspaceService.ToDto(sample);
    }
}
