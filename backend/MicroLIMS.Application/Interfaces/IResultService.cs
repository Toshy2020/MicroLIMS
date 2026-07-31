using MicroLIMS.Application.DTOs;

namespace MicroLIMS.Application.Interfaces;

public interface IResultService
{
    Task<ResultDto> SaveResultAsync(int testOrderId, string rawValue, int enteredByUserId);
    Task<List<ResultDto>> GetResultsForTestOrderAsync(int testOrderId);
}
