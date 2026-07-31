using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;

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

    public async Task<SampleDto> PrepareAsync(int sampleId, List<EMPreparationSelection> selections, int userId)
    {
        var sample = await _workflow.PrepareAsync(sampleId, selections, userId);
        return TestingWorkspaceService.ToDto(sample);
    }

    public Task StartStep1Async(int testOrderId, int userId) => _workflow.StartStep1Async(testOrderId, userId);
    public Task StartStep2Async(int testOrderId, int userId) => _workflow.StartStep2Async(testOrderId, userId);

    public Task<RoomMonitoring> CompleteAsync(int testOrderId, int finalCount, int userId, int actionLimit) =>
        _workflow.CompleteAsync(testOrderId, finalCount, userId, actionLimit);
}
