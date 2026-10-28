using MicroLIMS.Application.DTOs;

namespace MicroLIMS.Application.Interfaces;

public interface IDatabaseOverviewService
{
    Task<DatabaseOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}
