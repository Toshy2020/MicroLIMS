using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// Periodic access review for the admin panel (EU GMP Annex 11 s.12, 21 CFR
// 11.10(d)(g)). Read-and-attest: it reports who holds which access and records a
// signed conclusion; it never changes anyone's access (a requested change is made
// in Users / Roles with their own reason and signature). Held by the System
// Administrator and grantable to a reviewer such as QA through Roles.
[ApiController]
[Route("api/admin/access-reviews")]
[Authorize(Policy = PermissionConstants.AccessReview)]
public class AdminAccessReviewsController : ControllerBase
{
    private readonly IAccessReviewService _service;

    public AdminAccessReviewsController(IAccessReviewService service)
    {
        _service = service;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(ApiResponse<object>.Ok(await _service.ListAsync(ct)));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(ApiResponse<object>.Ok(await _service.GetAsync(id, ct)));

    [HttpPost]
    public async Task<IActionResult> Generate(GenerateAccessReviewRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.GenerateAsync(request.PeriodLabel, request.Reason, CurrentUserId)));

    [HttpPost("{id:int}/findings")]
    public async Task<IActionResult> AddFinding(int id, SaveAccessReviewFindingRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.AddFindingAsync(id, request, CurrentUserId)));

    [HttpPut("{id:int}/findings/{findingId:int}")]
    public async Task<IActionResult> UpdateFinding(int id, int findingId, SaveAccessReviewFindingRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.UpdateFindingAsync(id, findingId, request, CurrentUserId)));

    [HttpPost("{id:int}/sign")]
    public async Task<IActionResult> Sign(int id, SignAccessReviewRequest request) =>
        Ok(ApiResponse<object>.Ok(await _service.SignAsync(id, request.Password, request.Conclusion, CurrentUserId, ClientIp)));

    // CSV only; recorded as an ACCESS_REVIEW_EXPORTED audit event.
    [HttpGet("{id:int}/export")]
    public async Task<IActionResult> Export(int id)
    {
        var file = await _service.ExportCsvAsync(id, CurrentUserId);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
