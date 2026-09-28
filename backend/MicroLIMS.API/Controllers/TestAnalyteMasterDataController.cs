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

// Master data: test analyte. One of the controllers that share the
// api/masterdata route; the rules and data access live in the
// Application layer (TestAnalyteMasterDataService).
[ApiController]
[Route("api/masterdata")]
[Authorize]
public class TestAnalyteMasterDataController : ControllerBase
{
    private readonly TestAnalyteMasterDataService _service;

    public TestAnalyteMasterDataController(TestAnalyteMasterDataService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0;

    // ---- Test Analytes (ICP-OES / Calibration Curve) ----
    [HttpGet("test-definitions/{id:int}/analytes")]
    public async Task<IActionResult> GetTestAnalytes(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.GetTestAnalytesAsync(id)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost("test-definitions/{id:int}/analytes")]
    public async Task<IActionResult> CreateTestAnalyte(int id, CreateTestAnalyteRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateTestAnalyteAsync(CurrentUserId, id, request)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("test-definitions/{id:int}/analytes/{analyteId:int}")]
    public async Task<IActionResult> UpdateTestAnalyte(int id, int analyteId, UpdateTestAnalyteRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateTestAnalyteAsync(CurrentUserId, id, analyteId, request)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpDelete("test-definitions/{id:int}/analytes/{analyteId:int}")]
    public async Task<IActionResult> DeleteTestAnalyte(int id, int analyteId) =>
        Ok(ApiResponse<object>.Ok(await _service.DeleteTestAnalyteAsync(CurrentUserId, id, analyteId)));
}
