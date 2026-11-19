using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs;
using MicroLIMS.Application.Services.TableViews;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

public sealed record CreateTableViewRequest(string TableKey, string Name, string StateJson, string? Scope, int? SectionId);
public sealed record UpdateTableViewRequest(string? Name, string? StateJson);
public sealed record SetDefaultTableViewRequest(string TableKey, int? ViewId);

// Saved table views. Any signed-in user may keep personal views; sharing with a section needs TableViews.Share
// and membership of that section, which TableViewService checks. The user is always the authenticated caller:
// no user id appears in a route or body. A view the caller cannot see answers 404, never 403.
[ApiController]
[Route("api/table-views")]
[Authorize]
public class TableViewsController : ControllerBase
{
    private readonly TableViewService _service;

    public TableViewsController(TableViewService service) => _service = service;

    private int CurrentUserId =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string tableKey, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<TableViewDto>>.Ok(await _service.ListAsync(CurrentUserId, tableKey, ct)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTableViewRequest request, CancellationToken ct)
    {
        var scope = Enum.TryParse<TableViewScope>(request.Scope ?? nameof(TableViewScope.Personal), ignoreCase: true, out var parsed)
            ? parsed
            : throw new Shared.Exceptions.BusinessRuleException("The scope must be Personal or Section.");
        var created = await _service.CreateAsync(CurrentUserId, request.TableKey, request.Name, request.StateJson, scope, request.SectionId, ct);
        return Ok(ApiResponse<TableViewDto>.Ok(created));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTableViewRequest request, CancellationToken ct) =>
        Ok(ApiResponse<TableViewDto>.Ok(await _service.UpdateAsync(CurrentUserId, id, request.Name, request.StateJson, ct)));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _service.DeleteAsync(CurrentUserId, id, ct);
        return Ok(ApiResponse<object?>.Ok(null));
    }

    [HttpPut("default")]
    public async Task<IActionResult> SetDefault([FromBody] SetDefaultTableViewRequest request, CancellationToken ct)
    {
        await _service.SetDefaultAsync(CurrentUserId, request.TableKey, request.ViewId, ct);
        return Ok(ApiResponse<object?>.Ok(null));
    }
}
