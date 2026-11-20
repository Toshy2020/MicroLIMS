using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Messages;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public sealed record OosAnalystStatementRequest(string Text);
public sealed record OosChecklistAnswerRequest(OosChecklistAnswer Answer, string? Comment);

[ApiController]
[Route("api/oos-investigations")]
public sealed class OosInvestigationsController(OosInvestigationService service) : ControllerBase
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
        await service.SignPhaseOneAsync(id, request, ExpectedVersion(), Actor(), HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
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
