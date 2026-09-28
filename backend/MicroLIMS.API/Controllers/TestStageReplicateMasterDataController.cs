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

// Master data: test stage replicate. One of the controllers that share the
// api/masterdata route; the rules and data access live in the
// Application layer (TestStageReplicateMasterDataService).
[ApiController]
[Route("api/masterdata")]
[Authorize]
public class TestStageReplicateMasterDataController : ControllerBase
{
    private readonly TestStageReplicateMasterDataService _service;

    public TestStageReplicateMasterDataController(TestStageReplicateMasterDataService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

    // ---- Test Definition Stage Replicates (FP Standard-Comparison Assay -
    // per-ProductionStageRole standard/sample replicate counts) ----
    [HttpGet("test-definitions/{id:int}/stage-replicates")]
    public async Task<IActionResult> GetTestDefinitionStageReplicates(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.GetTestDefinitionStageReplicatesAsync(id)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPost("test-definitions/{id:int}/stage-replicates")]
    public async Task<IActionResult> CreateTestDefinitionStageReplicate(int id, CreateTestDefinitionStageReplicateRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateTestDefinitionStageReplicateAsync(CurrentUserId, id, request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpPut("test-definitions/{id:int}/stage-replicates/{replicateId:int}")]
    public async Task<IActionResult> UpdateTestDefinitionStageReplicate(int id, int replicateId, UpdateTestDefinitionStageReplicateRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateTestDefinitionStageReplicateAsync(CurrentUserId, id, replicateId, request)));

    [Authorize(Roles = RoleConstants.SectionHead + "," + RoleConstants.SystemAdministrator)]
    [HttpDelete("test-definitions/{id:int}/stage-replicates/{replicateId:int}")]
    public async Task<IActionResult> DeleteTestDefinitionStageReplicate(int id, int replicateId) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteTestDefinitionStageReplicateAsync(CurrentUserId, id, replicateId)));
}
