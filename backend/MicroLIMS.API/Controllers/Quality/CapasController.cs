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
[Route("api/quality/capas")]
public sealed class CapasController(CapaService service) : ControllerBase
{
    private int Actor => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0", CultureInfo.InvariantCulture);

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost]
    [Authorize(Policy = PermissionPolicyProvider.QualityApproveOrInvestigate)]
    public async Task<IActionResult> Create(CreateCapaRequest request, CancellationToken ct = default)
    {
        var capa = await service.CreateAsync(request, Actor, ct);
        return CreatedAtAction(nameof(QualityRecordsController.GetById), "QualityRecords",
            new { id = capa.Id }, ApiResponse<CapaDto>.Ok(capa));
    }

    [HttpPut("{id:int}/plan")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> SavePlan(int id, SaveCapaPlanRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<CapaDto>.Ok(await service.SavePlanAsync(id, request, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/approve-plan")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> ApprovePlan(int id, CapaPasswordRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<CapaDto>.Ok(await service.ApprovePlanAsync(id, request.Password, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/return-plan")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> ReturnPlan(int id, ReturnCapaPlanRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<CapaDto>.Ok(await service.ReturnPlanAsync(id, request.Comment, Version(), Actor, Ip, ct)));

    [HttpPost("actions/{actionId:int}/complete")]
    [Authorize(Policy = PermissionConstants.QualityView)]
    public async Task<IActionResult> CompleteAction(int actionId, CompleteCapaActionRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<CapaDto>.Ok(await service.CompleteActionAsync(actionId, request, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/verify-completion")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> VerifyCompletion(int id, CapaPasswordRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<CapaDto>.Ok(await service.VerifyCompletionAsync(id, request.Password, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/effectiveness")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> SignEffectiveness(int id, SignCapaEffectivenessRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<CapaDto>.Ok(await service.SignEffectivenessAsync(id, request, Version(), Actor, Ip, ct)));

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = PermissionConstants.QualityApprove)]
    public async Task<IActionResult> Cancel(int id, CapaReasonRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<CapaDto>.Ok(await service.CancelAsync(id, request.Reason, request.Password, Version(), Actor, Ip, ct)));

    private uint Version() => RecordVersionMiddleware.ParseExpectedVersion(Request)
        ?? throw new BusinessRuleException(MessageCodes.QualityVersionRequired, "An If-Match quality record version is required.");
}
