using Microsoft.EntityFrameworkCore;
using MicroLIMS.Shared.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Abstractions.Persistence;
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
    private readonly IServiceProvider? _provider;
    private readonly IUserSectionScopeService? _scope;

    public TestDefinitionMasterDataController(
        TestDefinitionMasterDataService service,
        IServiceProvider? provider = null,
        IUserSectionScopeService? scope = null)
    {
        _service = service;
        _provider = provider;
        _scope = scope;
    }

    private IMicroLimsDbContext ResolveDb() =>
        (IMicroLimsDbContext)(_provider?.GetService(typeof(IMicroLimsDbContext))
            ?? HttpContext?.RequestServices.GetRequiredService(typeof(IMicroLimsDbContext))
            ?? throw new InvalidOperationException("DbContext not available."));

    private IUserSectionScopeService ResolveScope() =>
        _scope
        ?? (IUserSectionScopeService?)(_provider?.GetService(typeof(IUserSectionScopeService)))
        ?? (IUserSectionScopeService)(HttpContext?.RequestServices.GetRequiredService(typeof(IUserSectionScopeService))
            ?? throw new InvalidOperationException("UserSectionScopeService not available."));

    private int CurrentUserId => int.TryParse(User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

    // ---- Test Master ----
    [HttpGet("test-definitions")]
    public async Task<IActionResult> GetTestDefinitions() =>
        Ok(ApiResponse<object>.Ok(await _service.GetTestDefinitionsAsync()));

    [HttpGet("test-definitions/editable-codes")]
    public async Task<IActionResult> GetEditableCodes()
    {
        var db = ResolveDb();
        var scope = ResolveScope();
        var codes = await TestCodeOwnership.GetEditableCodesOrAllActiveAsync(db, scope, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(codes));
    }

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("test-definitions")]
    public async Task<IActionResult> CreateTestDefinition(CreateTestDefinitionRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateTestDefinitionAsync(CurrentUserId, request)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("test-definitions/{id}")]
    public async Task<IActionResult> UpdateTestDefinition(int id, UpdateTestDefinitionRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateTestDefinitionAsync(CurrentUserId, id, request)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("test-definitions/{id}/freeze")]
    public async Task<IActionResult> FreezeTestDefinition(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.FreezeTestDefinitionAsync(id)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("test-definitions/{id}/unfreeze")]
    public async Task<IActionResult> UnfreezeTestDefinition(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.UnfreezeTestDefinitionAsync(id)));

    // ---- Equation Types (REQ-FP-030/042) ----
    [HttpGet("equation-types")]
    public IActionResult GetEquationTypes() =>
        Ok(ApiResponse<object>.Ok(_service.GetEquationTypes()));
}
