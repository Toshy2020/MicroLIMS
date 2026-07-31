using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Validators;
using MicroLIMS.Application.Workflows;

namespace MicroLIMS.Application.Services;

// Thin orchestration layer: validate -> call the shared
// ProductWorkflowEngine (used for Product/RM/PM alike) -> map to DTO.
public class ReceivingService : IReceivingService
{
    private readonly IProductWorkflowEngine _workflow;
    private readonly ReceiveSampleValidator _validator;

    public ReceivingService(IProductWorkflowEngine workflow, ReceiveSampleValidator validator)
    {
        _workflow = workflow;
        _validator = validator;
    }

    public async Task<SampleDto> ReceiveSampleAsync(ItemBasedReceiveRequest request)
    {
        var errors = _validator.Validate(request);
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));

        var sample = await _workflow.ReceiveAsync(request);
        return TestingWorkspaceService.ToDto(sample);
    }
}
