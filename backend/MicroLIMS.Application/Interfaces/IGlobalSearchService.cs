using MicroLIMS.Application.DTOs.Search;

namespace MicroLIMS.Application.Interfaces;

public interface IGlobalSearchService
{
    Task<GlobalSearchResultDto> SearchAsync(GlobalSearchRequest request, int userId, CancellationToken ct = default);
}
