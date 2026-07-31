using MicroLIMS.Application.Workflows;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Services;

public class PathogenService
{
    private readonly IPathogenWorkflowEngine _engine;

    public PathogenService(IPathogenWorkflowEngine engine)
    {
        _engine = engine;
    }

    public Task<PathogenObservation> RecordObservationAsync(int testOrderId, string stepName, bool growthObserved, int userId) =>
        _engine.RecordObservationAsync(testOrderId, stepName, growthObserved, userId);

    public Task<string> InterpretAsync(int testOrderId) => _engine.InterpretAsync(testOrderId);
}
