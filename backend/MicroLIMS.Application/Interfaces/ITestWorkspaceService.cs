using MicroLIMS.Application.DTOs;

namespace MicroLIMS.Application.Interfaces;

public interface ITestWorkspaceService
{
    Task<List<SampleDto>> GetActiveSamplesAsync();
    Task<SampleDto?> GetSampleAsync(int sampleId);
}
