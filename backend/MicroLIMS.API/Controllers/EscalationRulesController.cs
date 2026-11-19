using System.Security.Claims;
using MicroLIMS.Application.Services.Notifications;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroLIMS.API.Controllers;

public sealed record CreateEscalationRuleRequest(
    int SectionId,
    EscalationTrigger Trigger,
    decimal ThresholdWorkingHours,
    RoleType NotifyRoleType);

public sealed record ReplaceEscalationRuleRequest(
    decimal ThresholdWorkingHours,
    RoleType NotifyRoleType);

[ApiController]
[Route("api/escalation-rules")]
[Authorize(Policy = PermissionConstants.NotificationsConfigure)]
public class EscalationRulesController : ControllerBase
{
    private readonly EscalationService _service;

    public EscalationRulesController(EscalationService service)
    {
        _service = service;
    }

    private int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id
            : throw new UnauthorizedAccessException("User ID claim not found");

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EscalationRuleDto>>>> List(
        [FromQuery] int? sectionId = null,
        [FromQuery] bool includeRetired = false,
        CancellationToken ct = default)
    {
        var rows = await _service.ListAsync(CurrentUserId, sectionId, includeRetired, ct);
        return Ok(ApiResponse<IReadOnlyList<EscalationRuleDto>>.Ok(rows));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<EscalationRuleDto>>> Create(
        [FromBody] CreateEscalationRuleRequest request,
        CancellationToken ct = default)
    {
        var created = await _service.CreateAsync(
            CurrentUserId,
            request.SectionId,
            request.Trigger,
            request.ThresholdWorkingHours,
            request.NotifyRoleType,
            ct);
        return Ok(ApiResponse<EscalationRuleDto>.Ok(created));
    }

    [HttpPost("{id:int}/replace")]
    public async Task<ActionResult<ApiResponse<EscalationRuleDto>>> Replace(
        int id,
        [FromBody] ReplaceEscalationRuleRequest request,
        CancellationToken ct = default)
    {
        var replaced = await _service.ReplaceAsync(
            CurrentUserId,
            id,
            request.ThresholdWorkingHours,
            request.NotifyRoleType,
            ct);
        return Ok(ApiResponse<EscalationRuleDto>.Ok(replaced));
    }

    [HttpPost("{id:int}/retire")]
    public async Task<ActionResult<ApiResponse<object>>> Retire(
        int id,
        CancellationToken ct = default)
    {
        await _service.RetireAsync(CurrentUserId, id, ct);
        return Ok(ApiResponse<object>.Ok(new { retired = true }));
    }
}
