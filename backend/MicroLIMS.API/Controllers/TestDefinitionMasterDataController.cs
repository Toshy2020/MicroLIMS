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

// Master data: test definition. One of the controllers that share the
// api/masterdata route; the rules and data access live in the
// Application layer (TestDefinitionMasterDataService).
[ApiController]
[Route("api/masterdata")]
[Authorize]
public class TestDefinitionMasterDataController : ControllerBase
{
    private readonly TestDefinitionMasterDataService _service;

    public TestDefinitionMasterDataController(TestDefinitionMasterDataService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

    // ---- Test Master ----
    [HttpGet("test-definitions")]
    public async Task<IActionResult> GetTestDefinitions() =>
        Ok(ApiResponse<object>.Ok(await _service.GetTestDefinitionsAsync()));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("test-definitions")]
    public async Task<IActionResult> CreateTestDefinition(CreateTestDefinitionRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateTestDefinitionAsync(CurrentUserId, request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("test-definitions/{id}")]
    public async Task<IActionResult> UpdateTestDefinition(int id, UpdateTestDefinitionRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateTestDefinitionAsync(CurrentUserId, id, request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("test-definitions/{id}/freeze")]
    public async Task<IActionResult> FreezeTestDefinition(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.FreezeTestDefinitionAsync(id)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("test-definitions/{id}/unfreeze")]
    public async Task<IActionResult> UnfreezeTestDefinition(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.UnfreezeTestDefinitionAsync(id)));

    // ---- Equation Types (REQ-FP-030/042) ----
    [HttpGet("equation-types")]
    public IActionResult GetEquationTypes() =>
        Ok(ApiResponse<object>.Ok(_service.GetEquationTypes()));
}
