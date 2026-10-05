using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// ICP method master (I1). Reads are open to any
// authenticated user - the Test Master picker and specification screens
// need it; writes require MasterDataManage.
[ApiController]
[Route("api/masterdata/icp-methods")]
[Authorize]
public class IcpMethodController : ControllerBase
{
    private readonly IcpMethodService _service;
    public IcpMethodController(IcpMethodService service) => _service = service;
    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false) =>
        Ok(ApiResponse<object>.Ok(await _service.GetAllAsync(CurrentUserId, activeOnly)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.GetByIdAsync(id, CurrentUserId)));

    [HttpGet("{id:int}/history")]
    public async Task<IActionResult> GetHistory(int id) =>
        Ok(ApiResponse<object>.Ok(await _service.GetHistoryAsync(id, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveIcpMethodRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateAsync(r, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveIcpMethodRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateAsync(id, r, CurrentUserId)));

    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    [HttpPut("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool value, [FromBody] SetActiveRequest body) =>
        Ok(ApiResponse<object>.Ok(await _service.SetActiveAsync(id, value, body.Reason, CurrentUserId)));
}
