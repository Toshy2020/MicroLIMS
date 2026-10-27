using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers.DocumentControl;

// Succession for deactivated users: the queue of document responsibilities waiting for a new holder, and the
// audited hand-over. Authority (Documents.ConfigManage, or Documents.Reassign plus membership of the document's
// department) depends on the document's department, so the service decides it per item, not an attribute.
[ApiController]
[Authorize]
[Route("api/document-control/reassignments")]
public class DocumentReassignmentController : ControllerBase
{
    private readonly IDocumentReassignmentService _service;

    public DocumentReassignmentController(IDocumentReassignmentService service)
    {
        _service = service;
    }

    private int CurrentUserId =>
        int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetQueue([FromQuery] int? departmentId) =>
        Ok(ApiResponse<ReassignmentQueueDto>.Ok(await _service.GetQueueAsync(departmentId, CurrentUserId)));

    [HttpGet("count")]
    public async Task<IActionResult> GetCount() =>
        Ok(ApiResponse<ReassignmentCountDto>.Ok(await _service.GetCountAsync(CurrentUserId)));

    [HttpGet("candidates")]
    public async Task<IActionResult> GetCandidates([FromQuery] string itemType, [FromQuery] int itemId) =>
        Ok(ApiResponse<IReadOnlyList<ReassignmentCandidateDto>>.Ok(await _service.GetCandidatesAsync(itemType, itemId, CurrentUserId)));

    [HttpPost]
    public async Task<IActionResult> Reassign([FromBody] ReassignRequest request) =>
        Ok(ApiResponse<ReassignmentResultDto>.Ok(await _service.ReassignAsync(request, CurrentUserId)));
}
