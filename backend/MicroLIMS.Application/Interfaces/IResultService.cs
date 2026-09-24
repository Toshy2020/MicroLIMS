using MicroLIMS.Application.DTOs;

namespace MicroLIMS.Application.Interfaces;

// Read-only. Results are recorded only through TestWorkflowEngine, which
// gates entry on the test order's workflow step and writes the reporting
// projection alongside each Result. The former SaveResultAsync / POST and
// PUT /api/results path bypassed both (any role, any order state - it
// could return an Approved order to ResultEntered) and had no callers.
public interface IResultService
{
    Task<List<ResultDto>> GetResultsForTestOrderAsync(int testOrderId);
}
