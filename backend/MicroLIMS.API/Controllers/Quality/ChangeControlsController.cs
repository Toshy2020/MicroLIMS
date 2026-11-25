using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services.Quality;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers.Quality;

[ApiController]
[Route("api/quality/change-controls")]
public sealed class ChangeControlsController(ChangeControlService service) : ControllerBase
{
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0", CultureInfo.InvariantCulture);

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost]
    [Authorize(Policy = PermissionConstants.QualityRaise)]
    public async Task<IActionResult> Propose(ProposeChangeControlRequest request, CancellationToken ct = default)
    {
        var cc = await service.ProposeAsync(request, Actor, ct);
        return CreatedAtAction(nameof(QualityRecordsController.GetById), "QualityRecords",
            new { id = cc.Id }, ApiResponse<ChangeControlDto>.Ok(cc));
    }

    [HttpPost("{id:int}/affected")]
    [Authorize(Policy = PermissionConstants.QualityRaise)]
    public async Task<IActionResult> AddAffected(int id, ChangeControlAffectedInput input, CancellationToken ct = default) =>
        Ok(ApiResponse<ChangeControlDto>.Ok(await service.AddAffectedAsync(id, input, Version(), Actor, ct)));

    [HttpPut("{id:int}/assessment")]
    [Authorize(Policy = PermissionConstants.QualityInvestigate)]
    public async Task<IActionResult> SaveAssessment(int id, SaveChangeControlAssessmentRequest request,
        CancellationToken ct = default) =>
        Ok(ApiResponse<ChangeControlDto>.Ok(await service.SaveAssessmentAsync(id, request, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/sign-assessment")]
    [Authorize(Policy = PermissionConstants.QualityInvestigate)]
    public async Task<IActionResult> SignAssessment(int id, ChangeControlPasswordRequest request,
        CancellationToken ct = default) =>
        Ok(ApiResponse<ChangeControlDto>.Ok(await service.SignAssessmentAsync(id, request.Password, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> Approve(int id, ChangeControlPasswordRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<ChangeControlDto>.Ok(await service.ApproveAsync(id, request.Password, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> Reject(int id, ChangeControlRejectRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<ChangeControlDto>.Ok(await service.RejectAsync(id, request.Reason, request.Password, Version(), Actor, Ip, ct)));

    [HttpPost("tasks/{actionId:int}/complete")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> CompleteTask(int actionId, CompleteChangeControlTaskRequest request,
        CancellationToken ct = default) =>
        Ok(ApiResponse<ChangeControlDto>.Ok(await service.CompleteTaskAsync(actionId, request, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/close")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> Close(int id, CloseChangeControlRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<ChangeControlDto>.Ok(await service.CloseAsync(id, request, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> Cancel(int id, ChangeControlRejectRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<ChangeControlDto>.Ok(await service.CancelAsync(id, request.Reason, request.Password, Version(), Actor, Ip, ct)));

    private uint Version() => RecordVersionMiddleware.ParseExpectedVersion(Request)
        ?? throw new BusinessRuleException(MessageCodes.QualityVersionRequired, "An If-Match quality record version is required.");
}
