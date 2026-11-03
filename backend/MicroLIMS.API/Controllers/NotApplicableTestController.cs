using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Authorize]
public class NotApplicableTestController : ControllerBase
{
    private readonly NotApplicableTestService _service;

    public NotApplicableTestController(NotApplicableTestService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    private string? ClientIpAddress => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet("api/items/{itemId:int}/not-applicable-tests")]
    public async Task<IActionResult> ListForItem(int itemId) =>
        Ok(ApiResponse<object>.Ok(await _service.ListForItemAsync(itemId)));

    [HttpPost("api/items/{itemId:int}/not-applicable-tests")]
    [Authorize(Policy = PermissionConstants.NotApplicableTestsManage)]
    public async Task<IActionResult> Create(int itemId, [FromBody] CreateNotApplicableTestRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.CreateAsync(CurrentUserId, r with { ItemId = itemId })));

    [HttpPost("api/not-applicable-tests/{id:int}/head-approve")]
    [Authorize(Policy = PermissionConstants.NotApplicableTestsManage)]
    public async Task<IActionResult> HeadApprove(int id, [FromBody] NotApplicableSignRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.HeadApproveAsync(CurrentUserId, id, r.Password, r.Comment, ClientIpAddress)));

    [HttpPost("api/not-applicable-tests/{id:int}/final-approve")]
    [Authorize(Policy = PermissionConstants.NotApplicableTestsFinalApprove)]
    public async Task<IActionResult> FinalApprove(int id, [FromBody] NotApplicableSignRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.FinalApproveAsync(CurrentUserId, id, r.Password, r.Comment, ClientIpAddress)));

    [HttpPost("api/not-applicable-tests/{id:int}/withdraw")]
    [Authorize(Policy = PermissionConstants.NotApplicableTestsManage)]
    public async Task<IActionResult> Withdraw(int id, [FromBody] NotApplicableWithdrawRequest r) =>
        Ok(ApiResponse<object>.Ok(await _service.WithdrawAsync(CurrentUserId, id, r.Password, r.Reason)));
}
