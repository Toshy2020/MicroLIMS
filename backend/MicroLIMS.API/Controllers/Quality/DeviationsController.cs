using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.API.Authorization;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services.Quality;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers.Quality;

[ApiController]
[Route("api/quality/deviations")]
public sealed class DeviationsController(DeviationService service) : ControllerBase
{
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0", CultureInfo.InvariantCulture);

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost]
    [Authorize(Policy = PermissionConstants.QualityRaise)]
    public async Task<IActionResult> Raise(RaiseDeviationRequest request, CancellationToken ct = default) =>
        CreatedDeviation(await service.RaiseAsync(request, null, Actor, Ip, ct));

    [HttpPost("{id:int}/assess")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> Assess(int id, AssessDeviationRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<DeviationDto>.Ok(await service.AssessAsync(id, request, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> Reject(int id, DeviationReasonRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<DeviationDto>.Ok(await service.RejectAsync(id, request.Reason, request.Password, Version(), Actor, Ip, ct)));

    [HttpPut("{id:int}/investigation")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> SaveInvestigation(int id, SaveDeviationInvestigationRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<DeviationDto>.Ok(await service.SaveInvestigationAsync(id, request, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/lab-conclusion")]
    [Authorize(Policy = PermissionConstants.QualityLabConclude)]
    public async Task<IActionResult> SignLabConclusion(int id, DeviationPasswordRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<DeviationDto>.Ok(await service.SignLabConclusionAsync(id, request.Password, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> Approve(int id, ApproveDeviationRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<DeviationDto>.Ok(await service.ApproveAsync(id, request, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/return")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> Return(int id, ReturnDeviationRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<DeviationDto>.Ok(await service.ReturnAsync(id, request.Comment, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/close")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> Close(int id, DeviationPasswordRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<DeviationDto>.Ok(await service.CloseAsync(id, request.Password, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> Cancel(int id, DeviationReasonRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<DeviationDto>.Ok(await service.CancelAsync(id, request.Reason, request.Password, Version(), Actor, Ip, ct)));

    [HttpPost("from-oos-request/{id:int}")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> RaiseFromOosRequest(int id, CancellationToken ct = default) =>
        CreatedDeviation(await service.RaiseFromOosRequestAsync(id, Version(), Actor, Ip, ct));

    [HttpPost("from-monitoring-deviation/{id:int}")]
    [Authorize(Policy = PermissionPolicyProvider.QualityApproveOrInvestigate)]
    public async Task<IActionResult> EscalateMonitoringDeviation(int id, CancellationToken ct = default) =>
        CreatedDeviation(await service.EscalateMonitoringDeviationAsync(id, Version(), Actor, Ip, ct));

    [HttpPost("from-audit-finding/{id:int}")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> RaiseFromAuditFinding(int id, CancellationToken ct = default) =>
        CreatedDeviation(await service.RaiseFromAuditFindingAsync(id, Version(), Actor, Ip, ct));

    private IActionResult CreatedDeviation(DeviationDto deviation) =>
        CreatedAtAction(nameof(QualityRecordsController.GetById), "QualityRecords",
            new { id = deviation.Id }, ApiResponse<DeviationDto>.Ok(deviation));

    private uint Version() => RecordVersionMiddleware.ParseExpectedVersion(Request)
        ?? throw new BusinessRuleException(MessageCodes.QualityVersionRequired, "An If-Match quality record version is required.");
}
