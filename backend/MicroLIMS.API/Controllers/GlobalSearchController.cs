using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs.Search;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/search")]
[Authorize]
public class GlobalSearchController : ControllerBase
{
    private readonly IGlobalSearchService _searchService;

    public GlobalSearchController(IGlobalSearchService searchService)
    {
        _searchService = searchService;
    }

    private int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    [HttpGet("global")]
    public async Task<IActionResult> Search(
        [FromQuery] string query,
        [FromQuery] string? category = null,
        [FromQuery] int limit = 5)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return Ok(ApiResponse<GlobalSearchResultDto>.Ok(new GlobalSearchResultDto(query ?? string.Empty, 0, 0, new List<GlobalSearchCategoryDto>())));
        }

        var result = await _searchService.SearchAsync(new GlobalSearchRequest(query.Trim(), category, limit), CurrentUserId);
        return Ok(ApiResponse<GlobalSearchResultDto>.Ok(result));
    }
}
