using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MicroLIMS.Application.Services;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.API.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessageController : ControllerBase
{
    private readonly MessageService _messageService;

    public MessageController(MessageService messageService)
    {
        _messageService = messageService;
    }

    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var result = await _messageService.GetConversationsAsync(CurrentUserId);
        return Ok(ApiResponse<List<ConversationSummaryDto>>.Ok(result));
    }

    [HttpGet("conversations/{id}")]
    public async Task<IActionResult> GetConversationById(int id)
    {
        try
        {
            var result = await _messageService.GetConversationByIdAsync(id, CurrentUserId);
            return Ok(ApiResponse<ConversationSummaryDto>.Ok(result));
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

    [HttpPost("conversations")]
    public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest request)
    {
        try
        {
            var result = await _messageService.CreateConversationAsync(request, CurrentUserId);
            return Ok(ApiResponse<ConversationSummaryDto>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("conversations/{id}/messages")]
    public async Task<IActionResult> GetMessages(int id, [FromQuery] int take = 50)
    {
        try
        {
            var messages = await _messageService.GetMessagesAsync(id, CurrentUserId, take);
            return Ok(ApiResponse<List<DirectMessageDto>>.Ok(messages));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpPost("conversations/{id}/messages")]
    public async Task<IActionResult> SendMessage(int id, [FromBody] SendMessageRequest request)
    {
        try
        {
            var message = await _messageService.SendMessageAsync(id, request, CurrentUserId);
            return Ok(ApiResponse<DirectMessageDto>.Ok(message));
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

    [HttpPost("conversations/{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        await _messageService.MarkAsReadAsync(id, CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { message = "Marked as read." }));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetTotalUnreadCount()
    {
        var count = await _messageService.GetTotalUnreadCountAsync(CurrentUserId);
        return Ok(ApiResponse<object>.Ok(new { unreadCount = count }));
    }
}
