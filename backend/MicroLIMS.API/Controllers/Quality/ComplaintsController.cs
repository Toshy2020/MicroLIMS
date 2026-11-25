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
[Route("api/quality/complaints")]
public sealed class ComplaintsController(ComplaintService service) : ControllerBase
{
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0", CultureInfo.InvariantCulture);

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> Receive(ReceiveComplaintRequest request, CancellationToken ct = default)
    {
        var complaint = await service.ReceiveAsync(request, Actor, ct);
        return CreatedAtAction(nameof(QualityRecordsController.GetById), "QualityRecords",
            new { id = complaint.Id }, ApiResponse<ComplaintDto>.Ok(complaint));
    }

    [HttpPost("{id:int}/request-investigation")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> RequestInvestigation(int id, RequestComplaintInvestigationRequest request,
        CancellationToken ct = default) =>
        Ok(ApiResponse<ComplaintDto>.Ok(await service.RequestInvestigationAsync(id, request, Version(), Actor, ct)));

    [HttpPut("{id:int}/investigation")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> SaveInvestigation(int id, SaveComplaintInvestigationRequest request,
        CancellationToken ct = default) =>
        Ok(ApiResponse<ComplaintDto>.Ok(await service.SaveInvestigationAsync(id, request, Version(), Actor, ct)));

    [HttpPost("{id:int}/lab-report")]
    [Authorize(Policy = PermissionConstants.QualityLabConclude)]
    public async Task<IActionResult> SignLabReport(int id, ComplaintPasswordRequest request,
        CancellationToken ct = default) =>
        Ok(ApiResponse<ComplaintDto>.Ok(await service.SignLabReportAsync(id, request.Password, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/conclude")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> Conclude(int id, ConcludeComplaintRequest request,
        CancellationToken ct = default) =>
        Ok(ApiResponse<ComplaintDto>.Ok(await service.ConcludeAsync(id, request, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/close")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> Close(int id, ComplaintPasswordRequest request,
        CancellationToken ct = default) =>
        Ok(ApiResponse<ComplaintDto>.Ok(await service.CloseAsync(id, request.Password, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> Cancel(int id, ComplaintReasonRequest request,
        CancellationToken ct = default) =>
        Ok(ApiResponse<ComplaintDto>.Ok(await service.CancelAsync(id, request.Reason, request.Password, Version(), Actor, Ip, ct)));

    private uint Version() => RecordVersionMiddleware.ParseExpectedVersion(Request)
        ?? throw new BusinessRuleException(MessageCodes.QualityVersionRequired, "An If-Match quality record version is required.");
}
