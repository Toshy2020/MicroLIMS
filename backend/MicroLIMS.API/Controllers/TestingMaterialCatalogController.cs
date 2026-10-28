using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

// The Master Testing Materials page and the stock-receipt picker read the five groups
// through this one list. Read-only and open to any authenticated user (every lab sees
// every entry); each group is still edited through its own endpoints.
[ApiController]
[Route("api/masterdata/testing-materials")]
[Authorize]
public class TestingMaterialCatalogController : ControllerBase
{
    private readonly TestingMaterialCatalogService _service;
    public TestingMaterialCatalogController(TestingMaterialCatalogService service) => _service = service;
    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    // group: ReagentsIndicators | Standards | CultureMedia | ReferenceOrganisms | OtherMaterials.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? group = null, [FromQuery] string? search = null,
        [FromQuery] bool activeOnly = false, [FromQuery] int? sectionId = null, CancellationToken ct = default) =>
        Ok(ApiResponse<object>.Ok(await _service.GetAsync(CurrentUserId, group, search, activeOnly, sectionId, ct)));
}
