using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services.Sso;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Reason is optional for a preview (nothing changes) and required for an import.
public record DirectoryLinkImportRequest(IReadOnlyList<DirectoryLinkRowRequest>? Rows, string? Reason = null);

// Company sign-in administration. ?mode= picks the mode that blockers, the last
// passed test and the connection test refer to (see SsoAdminService.ResolveTargetMode).
[ApiController]
[Route("api/admin/sso")]
[Authorize(Policy = PermissionConstants.UsersManage)]
public class AdminSsoController : ControllerBase
{
    private readonly SsoAdminService _ssoAdmin;

    public AdminSsoController(SsoAdminService ssoAdmin)
    {
        _ssoAdmin = ssoAdmin;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? mode, CancellationToken ct)
    {
        var overview = await _ssoAdmin.GetOverviewAsync(CurrentUserId, ParseMode(mode), ct);
        return Ok(ApiResponse<SsoAdminOverviewDto>.Ok(overview));
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test([FromQuery] string? mode, CancellationToken ct)
    {
        var result = await _ssoAdmin.TestConnectionAsync(CurrentUserId, ParseMode(mode), ct);
        return Ok(ApiResponse<SsoTestResultDto>.Ok(result));
    }

    [HttpPost("links/preview")]
    public async Task<IActionResult> PreviewLinks(DirectoryLinkImportRequest request, CancellationToken ct)
    {
        var result = await _ssoAdmin.PreviewLinksAsync(request.Rows, ct);
        return Ok(ApiResponse<DirectoryLinkImportResultDto>.Ok(result));
    }

    [HttpPost("links/import")]
    public async Task<IActionResult> ImportLinks(DirectoryLinkImportRequest request, CancellationToken ct)
    {
        var result = await _ssoAdmin.ImportLinksAsync(request.Rows, request.Reason, CurrentUserId, ct);
        return Ok(ApiResponse<DirectoryLinkImportResultDto>.Ok(result));
    }

    private static SsoMode? ParseMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
            return null;
        if (!SsoModeService.TryParseMode(mode, out var parsed))
            throw new BusinessRuleException("Mode must be 'Off', 'OpenIdConnect' or 'ActiveDirectory'.");
        return parsed;
    }
}
