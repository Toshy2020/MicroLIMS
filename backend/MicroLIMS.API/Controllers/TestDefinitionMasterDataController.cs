using MicroLIMS.API.Helpers;
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

    // Null-safe so the controller can be exercised without an HTTP context.
    private CancellationToken Ct => HttpContext?.RequestAborted ?? CancellationToken.None;

    private int CurrentUserId => int.TryParse(User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

    // ---- Test Master ----
    [HttpGet("test-definitions")]
    public async Task<IActionResult> GetTestDefinitions() =>
        Ok(ApiResponse<object>.Ok(await _service.GetTestDefinitionsAsync()));

    // Items that use the test, paged; any signed-in user who can read the Test Master.
    [HttpGet("test-definitions/{id:int}/items")]
    public async Task<IActionResult> GetItemsForTest(int id, [FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 25) =>
        Ok(ApiResponse<object>.Ok(await _service.GetItemsForTestAsync(id, search, page, pageSize, CurrentUserId, Ct)));

    [HttpGet("test-definitions/editable-codes")]
    public async Task<IActionResult> GetEditableCodes() =>
        Ok(ApiResponse<object>.Ok(await _service.GetEditableCodesAsync(CurrentUserId)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("test-definitions")]
    public async Task<IActionResult> CreateTestDefinition(CreateTestDefinitionRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateTestDefinitionAsync(CurrentUserId, request)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("test-definitions/validate")]
    public async Task<IActionResult> ValidateTestDefinition(
        ValidateTestDefinitionRequest request,
        [FromQuery] int? id = null) =>
        Ok(ApiResponse<IReadOnlyList<FieldError>>.Ok(await _service.ValidateTestDefinitionAsync(CurrentUserId, request, id, Ct)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("test-definitions/{id}")]
    public async Task<IActionResult> UpdateTestDefinition(int id, UpdateTestDefinitionRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateTestDefinitionAsync(CurrentUserId, id, request, ChangeReasonHeader.Read(Request), Ct)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("test-definitions/{id}/freeze")]
    public async Task<IActionResult> FreezeTestDefinition(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.FreezeTestDefinitionAsync(CurrentUserId, id, Ct, ChangeReasonHeader.Read(Request))));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("test-definitions/{id}/unfreeze")]
    public async Task<IActionResult> UnfreezeTestDefinition(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.UnfreezeTestDefinitionAsync(CurrentUserId, id, Ct, ChangeReasonHeader.Read(Request))));

    // ---- Equation Types (REQ-FP-030/042) ----
    [HttpGet("equation-types")]
    public IActionResult GetEquationTypes() =>
        Ok(ApiResponse<object>.Ok(_service.GetEquationTypes()));
}
