using MicroLIMS.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Helpers;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;
using MicroLIMS.Application.Services.MasterData;

namespace MicroLIMS.API.Controllers;

// Master data: test workflow step. One of the controllers that share the
// api/masterdata route; the rules and data access live in the
// Application layer (TestWorkflowStepMasterDataService).
[ApiController]
[Route("api/masterdata")]
[Authorize]
public class TestWorkflowStepMasterDataController : ControllerBase
{
    private readonly TestWorkflowStepMasterDataService _service;

    public TestWorkflowStepMasterDataController(TestWorkflowStepMasterDataService service)
    {
        _service = service;
    }

    // ---- Test Workflow Steps (TestWorkflowEngine's configurable
    // template - replaces what used to be hardcoded chains in
    // PathogenWorkflowEngine/CountTestWorkflowEngine) ----
    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("test-definitions/{id}/workflow-type")]
    public async Task<IActionResult> UpdateWorkflowType(int id, UpdateWorkflowTypeRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateWorkflowTypeAsync(id, request)));

    [HttpGet("test-definitions/{id}/steps")]
    public async Task<IActionResult> GetTestWorkflowSteps(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.GetTestWorkflowStepsAsync(id)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("test-definitions/{id}/steps")]
    public async Task<IActionResult> CreateTestWorkflowStep(int id, CreateTestWorkflowStepRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateTestWorkflowStepAsync(id, request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("test-definitions/steps/{stepId}")]
    public async Task<IActionResult> UpdateTestWorkflowStep(int stepId, UpdateTestWorkflowStepRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateTestWorkflowStepAsync(stepId, request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("test-definitions/steps/{stepId}/move")]
    public async Task<IActionResult> MoveTestWorkflowStep(int stepId, MoveTestWorkflowStepRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.MoveTestWorkflowStepAsync(stepId, request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("test-definitions/steps/{stepId}")]
    public async Task<IActionResult> DeleteTestWorkflowStep(int stepId) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteTestWorkflowStepAsync(stepId)));
}
