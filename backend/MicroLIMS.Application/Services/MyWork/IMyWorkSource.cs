using MicroLIMS.Application.DTOs;

namespace MicroLIMS.Application.Services.MyWork;

// One kind of task waiting for the caller. Sources run one after another on the request's DbContext.
public interface IMyWorkSource
{
    string Key { get; }                                  // shown in UnavailableSources
    bool AppliesTo(MyWorkContext ctx);                   // permission gate; false = not run, not reported
    Task<MyWorkSourceResult> GetAsync(MyWorkContext ctx, int take, CancellationToken ct);
    Task<IReadOnlyDictionary<MyWorkUrgencyLevel, int>> CountAsync(MyWorkContext ctx, CancellationToken ct);
}

public interface IMyWorkService
{
    Task<MyWorkResponse> GetAsync(int userId, DashboardLabScope? lab, CancellationToken ct = default);
    Task<MyWorkSummary> GetSummaryAsync(int userId, CancellationToken ct = default);
}
