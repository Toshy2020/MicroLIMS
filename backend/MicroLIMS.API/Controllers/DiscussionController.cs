using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Shared.Constants;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/discussions")]
[Authorize]
public class DiscussionController : ControllerBase
{
    private readonly DiscussionService _discussionService;

    public DiscussionController(DiscussionService discussionService)
    {
        _discussionService = discussionService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    private bool CanEditAny => User.HasClaim("permission", PermissionConstants.DiscussionsEditAny) ||
                               User.IsInRole(RoleConstants.SystemAdministrator) ||
                               User.IsInRole(RoleConstants.SectionHead);

    [HttpGet]
    public async Task<IActionResult> GetFeed(
        [FromQuery] int? categoryId = null,
        [FromQuery] string? search = null,
        [FromQuery] bool importantOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        DiscussionCategory? cat = categoryId.HasValue ? (DiscussionCategory)categoryId.Value : null;
        var result = await _discussionService.GetFeedAsync(cat, search, importantOnly, page, pageSize);
        return Ok(ApiResponse<PagedResult<DiscussionPostSummaryDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _discussionService.GetPostByIdAsync(id);
            return Ok(ApiResponse<DiscussionPostDetailDto>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    public class CreatePostForm
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int Category { get; set; } = 11; // Default to Other
        public bool IsImportant { get; set; }
        public List<IFormFile>? Files { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> CreatePost([FromForm] CreatePostForm form)
    {
        try
        {
            var attachments = new List<(string FileName, string ContentType, byte[] Data)>();
            if (form.Files != null)
            {
                foreach (var file in form.Files)
                {
                    if (file.Length > 0)
                    {
                        using var ms = new MemoryStream();
                        await file.CopyToAsync(ms);
                        attachments.Add((file.FileName, file.ContentType, ms.ToArray()));
                    }
                }
            }

            var request = new CreateDiscussionPostRequest(
                form.Title,
                form.Content,
                (DiscussionCategory)form.Category,
                form.IsImportant
            );

            var created = await _discussionService.CreatePostAsync(request, attachments, CurrentUserId);
            return Ok(ApiResponse<DiscussionPostDetailDto>.Ok(created));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePost(int id, [FromBody] UpdateDiscussionPostRequest request)
    {
        try
        {
            var updated = await _discussionService.UpdatePostAsync(id, request, CurrentUserId, CanEditAny);
            return Ok(ApiResponse<DiscussionPostDetailDto>.Ok(updated));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPatch("{id}/important")]
    public async Task<IActionResult> ToggleImportant(int id)
    {
        try
        {
            var isImportant = await _discussionService.ToggleImportantAsync(id, CurrentUserId, CanEditAny);
            return Ok(ApiResponse<object>.Ok(new { isImportant }));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePost(int id)
    {
        try
        {
            await _discussionService.DeletePostAsync(id, CurrentUserId, CanEditAny);
            return Ok(ApiResponse<object>.Ok(new { message = "Post deleted successfully." }));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetPostHistory(int id)
    {
        var history = await _discussionService.GetPostHistoryAsync(id);
        return Ok(ApiResponse<List<DiscussionVersionDto>>.Ok(history));
    }

    [HttpGet("{id}/attachments/{attachmentId}/download")]
    public async Task<IActionResult> DownloadAttachment(int id, int attachmentId)
    {
        try
        {
            var (data, contentType, fileName) = await _discussionService.GetAttachmentContentAsync(id, attachmentId);
            return File(data, contentType, fileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ---- Comments ----

    [HttpPost("{id}/comments")]
    public async Task<IActionResult> AddComment(int id, [FromBody] CreateDiscussionCommentRequest request)
    {
        try
        {
            var comment = await _discussionService.AddCommentAsync(id, request, CurrentUserId);
            return Ok(ApiResponse<DiscussionCommentDto>.Ok(comment));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPut("{id}/comments/{commentId}")]
    public async Task<IActionResult> UpdateComment(int id, int commentId, [FromBody] UpdateDiscussionCommentRequest request)
    {
        try
        {
            var comment = await _discussionService.UpdateCommentAsync(id, commentId, request, CurrentUserId, CanEditAny);
            return Ok(ApiResponse<DiscussionCommentDto>.Ok(comment));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpDelete("{id}/comments/{commentId}")]
    public async Task<IActionResult> DeleteComment(int id, int commentId)
    {
        try
        {
            await _discussionService.DeleteCommentAsync(id, commentId, CurrentUserId, CanEditAny);
            return Ok(ApiResponse<object>.Ok(new { message = "Comment deleted successfully." }));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
    }
}
