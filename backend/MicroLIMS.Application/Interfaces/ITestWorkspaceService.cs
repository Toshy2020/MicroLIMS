using MicroLIMS.Application.DTOs;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Interfaces;

public interface ITestWorkspaceService
{
    Task<List<SampleDto>> GetActiveSamplesAsync();
    Task<PagedResult<SampleDto>> GetActiveSamplesAsync(TestingWorkspaceFilterDto filter, int? currentUserId = null);
    Task<WorkspaceTileCountsDto> GetWorkloadCountsAsync(int? currentUserId = null);
    Task<SampleDto?> GetSampleAsync(int sampleId);
}
