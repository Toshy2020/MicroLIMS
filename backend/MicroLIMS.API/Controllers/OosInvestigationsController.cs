using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.API.Authorization;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public sealed record SignRetestPlanRequest(string Password);
public sealed record OosAnalystStatementRequest(string Text);
public sealed record OosChecklistAnswerRequest(OosChecklistAnswer Answer, string? Comment);

public sealed record OosEntryRequest(OosEntryKind Kind, string Text, int? ParentEntryId);
public sealed record OosReopenRequest(string Reason, string Password);
public sealed record OosDeviationReasonRequest(string Reason);
public sealed record OosDeviationAddressRequest(string Comment);
public sealed record OpenManualExcursionRequest(int TestOrderId, string Reason);

[ApiController]
[Route("api/oos-investigations")]
public sealed class OosInvestigationsController(OosInvestigationService service, OosRetestPlanService retestPlans,
    IOosNotifier notifier, ILogger<OosInvestigationsController> logger) : ControllerBase
{
    [HttpGet("{id:int}/facts")]
    [Authorize(Policy = PermissionConstants.OosView)]
    public async Task<IActionResult> GetFacts(int id, CancellationToken ct = default) =>
        Ok(ApiResponse<OosChecklistFactsDto>.Ok(await service.GetFactsAsync(id, ct)));

    [HttpPost("{id:int}/phase-one/statements")]
    [Authorize(Policy = PermissionConstants.OosContribute)]
    public async Task<IActionResult> AddAnalystStatement(int id, OosAnalystStatementRequest request, CancellationToken ct = default)
    {
        _ = ExpectedVersion();
        await service.AddAnalystStatementAsync(id, request.Text, Actor(), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/checklist/{phase:int}/{itemKey}")]
    [Authorize(Policy = PermissionConstants.OosContribute)]
    public async Task<IActionResult> AnswerChecklist(int id, int phase, string itemKey, OosChecklistAnswerRequest request,
        CancellationToken ct = default)
    {
        _ = ExpectedVersion();
        await service.AnswerChecklistAsync(id, phase, itemKey, request.Answer, request.Comment, Actor(), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/phase-one/sign")]
    [Authorize(Policy = PermissionConstants.OosManage)]
    public async Task<IActionResult> SignPhaseOne(int id, SignPhaseOneRequest request, CancellationToken ct = default)
    {
        if (await service.SignPhaseOneAsync(id, request, ExpectedVersion(), Actor(), HttpContext.Connection.RemoteIpAddress?.ToString(), ct))
            await OosPostCommitNotification.SendAsync(notifier.PhaseTwoStartedAsync, id, logger, ct);
        return NoContent();
    }

    [HttpPut("{id:int}/retest-plan")]
    [Authorize(Policy = PermissionConstants.OosManage)]
    public async Task<IActionResult> SaveRetestPlan(int id, SaveRetestPlanRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<OosRetestPlanDto>.Ok(await retestPlans.DraftAsync(id, request, ExpectedVersion(), Actor(), ct)));

    [HttpPost("retest-plans/{planId:int}/sign")]
    [Authorize(Policy = PermissionConstants.OosManage)]
    public async Task<IActionResult> SignRetestPlan(int planId, SignRetestPlanRequest request, CancellationToken ct = default)
    {
        await retestPlans.SignAndCreateAsync(planId, request.Password, ExpectedVersion(), Actor(),
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/entries")]
    [Authorize(Policy = PermissionPolicyProvider.OosContributeOrManage)]
    public async Task<IActionResult> AddEntry(int id, OosEntryRequest request, CancellationToken ct = default)
    {
        _ = ExpectedVersion();
        await service.AddEntryAsync(id, request.Kind, request.Text, request.ParentEntryId, Actor(), ct);
        return NoContent();
    }

    [HttpPost("{id:int}/conclude")]
    [Authorize(Policy = PermissionConstants.OosManage)]
    public async Task<IActionResult> Conclude(int id, ConcludeRequest request, CancellationToken ct = default)
    {
        await service.ConcludeAsync(id, request, ExpectedVersion(), Actor(), HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        await OosPostCommitNotification.SendAsync(notifier.ConcludedAsync, id, logger, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/reopen")]
    [Authorize(Policy = PermissionConstants.OosManage)]
    public async Task<IActionResult> Reopen(int id, OosReopenRequest request, CancellationToken ct = default)
    {
        _ = ExpectedVersion();
        await service.ReopenAsync(id, request.Reason, request.Password, Actor(), HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        return NoContent();
    }

    [HttpPost("for-sample/{sampleId:int}/sections/{sectionId:int}/open")]
    [Authorize(Policy = PermissionConstants.OosManage)]
    public async Task<IActionResult> OpenManually(int sampleId, int sectionId, CancellationToken ct = default)
    {
        var investigation = await service.OpenManuallyAsync(sampleId, sectionId, ExpectedVersion(), Actor(), ct);
        await OosPostCommitNotification.SendAsync(notifier.OpenedAsync, investigation.Id, logger, ct);
        return Ok(ApiResponse<OosManualOpeningDto>.Ok(investigation));
    }

    [HttpPost("{id:int}/deviation-requests")]
    [Authorize(Policy = PermissionConstants.OosRequestDeviation)]
    public async Task<IActionResult> RequestDeviation(int id, OosDeviationReasonRequest request, CancellationToken ct = default)
    {
        _ = ExpectedVersion();
        var created = await service.RequestDeviationAsync(id, request.Reason, Actor(), ct);
        await OosPostCommitNotification.SendAsync(notifier.DeviationRequestedAsync, id, logger, ct);
        return Ok(ApiResponse<OosDeviationRequestDto>.Ok(created));
    }

    [HttpPost("deviation-requests/{requestId:int}/address")]
    [Authorize(Policy = PermissionConstants.OosManage)]
    public async Task<IActionResult> AddressDeviationRequest(int requestId, OosDeviationAddressRequest request,
        CancellationToken ct = default)
    {
        await service.AddressDeviationRequestAsync(requestId, request.Comment, ExpectedVersion(), Actor(), ct);
        return NoContent();
    }

    [HttpPost("excursions/open")]
    [Authorize(Policy = PermissionPolicyProvider.OosContributeOrManage)]
    public async Task<IActionResult> OpenManualExcursion(OpenManualExcursionRequest request, CancellationToken ct = default)
    {
        var investigation = await service.OpenManualExcursionAsync(request.TestOrderId, request.Reason, Actor(), ct);
        await OosPostCommitNotification.SendAsync(notifier.OpenedAsync, investigation.Id, logger, ct);
        return Ok(ApiResponse<OosManualOpeningDto>.Ok(investigation));
    }

    [HttpPost("{id:int}/excursion/close")]
    [Authorize(Policy = PermissionConstants.OosManage)]
    public async Task<IActionResult> CloseExcursion(int id, CloseExcursionRequest request, CancellationToken ct = default)
    {
        await service.CloseExcursionAsync(id, request, ExpectedVersion(), Actor(),
            HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        await OosPostCommitNotification.SendAsync(notifier.ConcludedAsync, id, logger, ct);
        return NoContent();
    }

    private uint ExpectedVersion() => RecordVersionMiddleware.ParseExpectedVersion(Request) ?? throw OosApiErrors.MissingVersion();
    private int Actor() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
}

internal static class OosApiErrors
{
    public static BusinessRuleException MissingVersion() =>
        new(MessageCodes.OosVersionRequired, "A record version is required. Reload the record and try again.");
}
