using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Exceptions;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/admin/users/{id:int}")]
[Authorize(Policy = PermissionConstants.UsersManage)]
public class AdminUserMfaController : ControllerBase
{
    private readonly IMfaAdminService _mfaAdminService;

    public AdminUserMfaController(IMfaAdminService mfaAdminService)
    {
        _mfaAdminService = mfaAdminService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpPost("mfa/reset")]
    public async Task<IActionResult> Reset(int id, AdminResetMfaRequest request, CancellationToken ct)
    {
        await _mfaAdminService.ResetAsync(id, request.Reason, CurrentUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpPost("mfa/hardware-token")]
    public async Task<IActionResult> AssignHardwareToken(int id, AssignHardwareTokenRequest request, CancellationToken ct)
    {
        var seedIsHex = request.SeedFormat?.Trim().ToLowerInvariant() switch
        {
            "base32" => false,
            "hex" => true,
            _ => throw new BusinessRuleException("Seed format must be 'base32' or 'hex'.")
        };

        await _mfaAdminService.AssignHardwareTokenAsync(
            id,
            request.SerialNumber,
            request.Seed,
            seedIsHex,
            request.StepSeconds,
            request.Reason,
            CurrentUserId,
            ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }

    [HttpGet("trusted-devices")]
    public async Task<IActionResult> ListTrustedDevices(int id, CancellationToken ct)
    {
        var devices = await _mfaAdminService.ListTrustedDevicesAsync(id, ct);
        return Ok(ApiResponse<IReadOnlyList<TrustedDeviceInfo>>.Ok(devices));
    }

    [HttpDelete("trusted-devices/{deviceId:int}")]
    public async Task<IActionResult> RevokeTrustedDevice(int id, int deviceId, CancellationToken ct)
    {
        await _mfaAdminService.RevokeTrustedDeviceAsync(id, deviceId, CurrentUserId, ct);
        return Ok(ApiResponse<object>.Ok(new { }));
    }
}
