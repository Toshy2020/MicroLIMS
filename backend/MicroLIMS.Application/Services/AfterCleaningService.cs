using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;

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

    public async Task<SampleDto> PrepareAsync(int sampleId, List<AfterCleaningPreparationSelection> selections, int userId)
    {
        var sample = await _workflow.PrepareAsync(sampleId, selections, userId);
        return TestingWorkspaceService.ToDto(sample);
    }

    public Task StartStep1Async(int testOrderId, int userId) => _workflow.StartStep1Async(testOrderId, userId);
    public Task StartStep2Async(int testOrderId, int userId) => _workflow.StartStep2Async(testOrderId, userId);

    public Task<Result> CompleteAsync(int testOrderId, int finalCount, int userId) =>
        _workflow.CompleteAsync(testOrderId, finalCount, userId);
}
