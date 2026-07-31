using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Workflows;

namespace MicroLIMS.Application.Interfaces;

public interface IReceivingService
{
    Task<SampleDto> ReceiveSampleAsync(ItemBasedReceiveRequest request);
}
