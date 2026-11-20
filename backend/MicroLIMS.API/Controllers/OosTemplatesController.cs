using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.API.Middleware;
using MicroLIMS.Application.Services.Oos;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/oos-templates")]
[Authorize(Policy = PermissionConstants.OosManage)]
public sealed class OosTemplatesController(OosTemplateService service) : ControllerBase
{
    [HttpGet("{sectionId:int}/{kind}")]
    public async Task<IActionResult> Get(int sectionId, OosInvestigationKind kind, CancellationToken ct = default) =>
        Ok(ApiResponse<OosTemplateDto>.Ok(await service.GetAsync(sectionId, kind, ct)));

    [HttpPut("{sectionId:int}/{kind}")]
    public async Task<IActionResult> Save(int sectionId, OosInvestigationKind kind, SaveOosTemplateRequest request,
        CancellationToken ct = default) =>
        Ok(ApiResponse<OosTemplateDto>.Ok(await service.SaveAsync(sectionId, kind, request, ExpectedVersion(), Actor(), ct)));

    [HttpGet("categories")]
    public async Task<IActionResult> ListCategories(int page = 1, int pageSize = 50, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<OosCategoryDto>>.Ok(await service.ListAsync(page, pageSize, ct)));

    [HttpPost("categories")]
    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    public async Task<IActionResult> AddCategory(SaveOosCategoryRequest request, CancellationToken ct = default) =>
        Ok(ApiResponse<OosCategoryDto>.Ok(await service.SaveAsync(null, request, Actor(), ct)));

    [HttpPut("categories/{categoryId:int}")]
    [Authorize(Policy = PermissionConstants.MasterDataManage)]
    public async Task<IActionResult> SaveCategory(int categoryId, SaveOosCategoryRequest request, CancellationToken ct = default)
    {
        _ = ExpectedVersion();
        return Ok(ApiResponse<OosCategoryDto>.Ok(await service.SaveAsync(categoryId, request, Actor(), ct)));
    }

    private uint ExpectedVersion() => RecordVersionMiddleware.ParseExpectedVersion(Request) ?? throw OosApiErrors.MissingVersion();
    private int Actor() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
}
