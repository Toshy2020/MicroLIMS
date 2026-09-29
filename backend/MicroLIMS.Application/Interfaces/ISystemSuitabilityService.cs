using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Interfaces;

public interface ISystemSuitabilityService
{
    Task<SystemSuitabilityRunView> CreateAsync(CreateSystemSuitabilityRunRequest request, int userId, string? ipAddress, CancellationToken ct = default);
    Task<List<SystemSuitabilityRunView>> GetAllAsync(SystemSuitabilityRunFilter filter, int userId, CancellationToken ct = default);
    Task<SystemSuitabilityRunView?> GetByIdAsync(int id, int userId, CancellationToken ct = default);
    Task<List<SystemSuitabilityRunView>> GetSelectableRunsForTestOrderAsync(int testOrderId, int userId, CancellationToken ct = default);
    Task<SystemSuitabilityRunView?> GetLinkedRunForTestOrderAsync(int testOrderId, int userId, CancellationToken ct = default);
    Task<SuitabilityRunReportDetailsDto> GetReportDetailsAsync(int runId, int userId, CancellationToken ct = default);
    Task LinkTestOrdersAsync(int runId, IEnumerable<int> testOrderIds, int userId, CancellationToken ct = default);
}
