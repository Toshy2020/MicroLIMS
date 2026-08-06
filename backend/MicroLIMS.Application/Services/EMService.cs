using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Workflows;

namespace MicroLIMS.Application.Services;

public class EMService
{
    private readonly IEMWorkflowEngine _workflow;

    public EMService(IEMWorkflowEngine workflow)
    {
        _workflow = workflow;
    }

    public async Task<SampleDto> ReceiveAsync(EMReceiveRequest request)
    {
        var sample = await _workflow.ReceiveAsync(request);
        return TestingWorkspaceService.ToDto(sample);
    }

    public async Task<SampleDto> PrepareAsync(int sampleId, List<int> roomTestConfigurationIds, int userId)
    {
        var sample = await _workflow.PrepareAsync(sampleId, roomTestConfigurationIds, userId);
        return TestingWorkspaceService.ToDto(sample);
    }
}
