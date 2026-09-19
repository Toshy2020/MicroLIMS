using MicroLIMS.Application.DTOs;
using MicroLIMS.Domain.Entities;

namespace MicroLIMS.Application.Interfaces;

public interface ISystemSuitabilityService
{
    Task<SystemSuitabilityRun> CreateAsync(CreateSystemSuitabilityRunRequest request, int userId, string? ipAddress, CancellationToken ct = default);
    Task<List<SystemSuitabilityRun>> GetAllAsync(SystemSuitabilityRunFilter filter, int userId, CancellationToken ct = default);
    Task<SystemSuitabilityRun?> GetByIdAsync(int id, int userId, CancellationToken ct = default);
    Task<List<SystemSuitabilityRun>> GetSelectableRunsForTestOrderAsync(int testOrderId, int userId, CancellationToken ct = default);
    Task<SystemSuitabilityRun?> GetLinkedRunForTestOrderAsync(int testOrderId, int userId, CancellationToken ct = default);
    Task<SuitabilityRunReportDetailsDto> GetReportDetailsAsync(int runId, int userId, CancellationToken ct = default);
    Task LinkTestOrdersAsync(int runId, IEnumerable<int> testOrderIds, int userId, CancellationToken ct = default);
}
