using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Infrastructure.Notifications;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services;

// ---- DTOs ----

public record ConversationParticipantDto(
    int UserId,
    string FullName,
    string Username,
    string? JobTitle,
    string RoleName,
    DateTime? LastReadAt);

public record DirectMessageDto(
    int Id,
    int ConversationId,
    int SenderUserId,
    string SenderName,
    string? SenderRole,
    string Content,
    DateTime CreatedAt);

public record ConversationSummaryDto(
    int Id,
    string? Title,
    bool IsGroup,
    int CreatedByUserId,
    DateTime LastMessageAt,
    List<ConversationParticipantDto> Participants,
    DirectMessageDto? LastMessage,
    int UnreadCount);

public record CreateConversationRequest(
    string? Title,
    bool IsGroup,
    List<int> ParticipantUserIds,
    string InitialMessage);

public record SendMessageRequest(string Content);

// ---- Service ----

public class MessageService
{
    private readonly MicroLimsDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        MicroLimsDbContext db,
        INotificationService notificationService,
        ILogger<MessageService> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<List<ConversationSummaryDto>> GetConversationsAsync(int userId)
    {
        var conversationIds = await _db.ConversationParticipants
            .Where(cp => cp.UserId == userId)
            .Select(cp => cp.ConversationId)
            .ToListAsync();

        var conversations = await _db.Conversations
            .Where(c => conversationIds.Contains(c.Id))
            .Include(c => c.Participants)
                .ThenInclude(cp => cp.User)
                    .ThenInclude(u => u!.Role)
            .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
                .ThenInclude(m => m.SenderUser)
                    .ThenInclude(u => u!.Role)
            .OrderByDescending(c => c.LastMessageAt)
            .AsNoTracking()
            .ToListAsync();

        var summaries = new List<ConversationSummaryDto>();

        foreach (var c in conversations)
        {
            var myParticipant = c.Participants.FirstOrDefault(p => p.UserId == userId);
            var lastReadId = myParticipant?.LastReadMessageId ?? 0;

            var unreadCount = await _db.DirectMessages
                .CountAsync(m => m.ConversationId == c.Id && m.Id > lastReadId && m.SenderUserId != userId && !m.IsDeleted);

            var lastMessage = c.Messages.FirstOrDefault();
            DirectMessageDto? lastMessageDto = null;
            if (lastMessage != null)
            {
                lastMessageDto = new DirectMessageDto(
                    lastMessage.Id,
                    lastMessage.ConversationId,
                    lastMessage.SenderUserId,
                    lastMessage.SenderUser?.FullName ?? "Unknown User",
                    lastMessage.SenderUser?.Role?.Name,
                    lastMessage.Content,
                    lastMessage.CreatedAt
                );
            }

            var participantsDto = c.Participants.Select(p => new ConversationParticipantDto(
                p.UserId,
                p.User?.FullName ?? "Unknown User",
                p.User?.Username ?? "",
                p.User?.JobTitle,
                p.User?.Role?.Name ?? "Staff",
                p.LastReadAt
            )).ToList();

            // Format default title for 1-on-1 if title is empty
            var displayTitle = c.Title;
            if (string.IsNullOrWhiteSpace(displayTitle) && !c.IsGroup)
            {
                var other = participantsDto.FirstOrDefault(p => p.UserId != userId);
                displayTitle = other?.FullName ?? "Direct Message";
            }

            summaries.Add(new ConversationSummaryDto(
                c.Id,
                displayTitle,
                c.IsGroup,
                c.CreatedByUserId,
                c.LastMessageAt,
                participantsDto,
                lastMessageDto,
                unreadCount
            ));
        }

        return summaries;
    }

    public async Task<ConversationSummaryDto> GetConversationByIdAsync(int conversationId, int userId)
    {
        var conversation = await _db.Conversations
            .Include(c => c.Participants)
                .ThenInclude(cp => cp.User)
                    .ThenInclude(u => u!.Role)
            .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
                .ThenInclude(m => m.SenderUser)
                    .ThenInclude(u => u!.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId)
            ?? throw new KeyNotFoundException($"Conversation {conversationId} not found.");

        var isMember = conversation.Participants.Any(p => p.UserId == userId);
        if (!isMember)
            throw new UnauthorizedAccessException("You are not a participant in this conversation.");

        var myParticipant = conversation.Participants.FirstOrDefault(p => p.UserId == userId);
        var lastReadId = myParticipant?.LastReadMessageId ?? 0;

        var unreadCount = await _db.DirectMessages
            .CountAsync(m => m.ConversationId == conversation.Id && m.Id > lastReadId && m.SenderUserId != userId && !m.IsDeleted);

        var lastMessage = conversation.Messages.FirstOrDefault();
        DirectMessageDto? lastMessageDto = null;
        if (lastMessage != null)
        {
            lastMessageDto = new DirectMessageDto(
                lastMessage.Id,
                lastMessage.ConversationId,
                lastMessage.SenderUserId,
                lastMessage.SenderUser?.FullName ?? "Unknown User",
                lastMessage.SenderUser?.Role?.Name,
                lastMessage.Content,
                lastMessage.CreatedAt
            );
        }

        var participantsDto = conversation.Participants.Select(p => new ConversationParticipantDto(
            p.UserId,
            p.User?.FullName ?? "Unknown User",
            p.User?.Username ?? "",
            p.User?.JobTitle,
            p.User?.Role?.Name ?? "Staff",
            p.LastReadAt
        )).ToList();

        var displayTitle = conversation.Title;
        if (string.IsNullOrWhiteSpace(displayTitle) && !conversation.IsGroup)
        {
            var other = participantsDto.FirstOrDefault(p => p.UserId != userId);
            displayTitle = other?.FullName ?? "Direct Message";
        }

        return new ConversationSummaryDto(
            conversation.Id,
            displayTitle,
            conversation.IsGroup,
            conversation.CreatedByUserId,
            conversation.LastMessageAt,
            participantsDto,
            lastMessageDto,
            unreadCount
        );
    }

    public async Task<ConversationSummaryDto> CreateConversationAsync(CreateConversationRequest request, int creatorUserId)
    {
        if (string.IsNullOrWhiteSpace(request.InitialMessage))
            throw new InvalidOperationException("Initial message is required.");

        var allParticipantIds = request.ParticipantUserIds ?? new List<int>();
        if (!allParticipantIds.Contains(creatorUserId))
        {
            allParticipantIds.Add(creatorUserId);
        }

        allParticipantIds = allParticipantIds.Distinct().ToList();

        if (allParticipantIds.Count < 2)
            throw new InvalidOperationException("A conversation must have at least one other participant.");

        // For 1-on-1 conversations, check if one already exists between these 2 users
        if (!request.IsGroup && allParticipantIds.Count == 2)
        {
            var otherUserId = allParticipantIds.First(id => id != creatorUserId);

            var existingConvId = await _db.Conversations
                .Where(c => !c.IsGroup)
                .Where(c => c.Participants.Any(p => p.UserId == creatorUserId) &&
                            c.Participants.Any(p => p.UserId == otherUserId))
                .Select(c => c.Id)
                .FirstOrDefaultAsync();

            if (existingConvId != 0)
            {
                await SendMessageAsync(existingConvId, new SendMessageRequest(request.InitialMessage), creatorUserId);
                return await GetConversationByIdAsync(existingConvId, creatorUserId);
            }
        }

        var now = DateTime.UtcNow;
        var conversation = new Conversation
        {
            Title = request.Title?.Trim(),
            IsGroup = request.IsGroup,
            CreatedByUserId = creatorUserId,
            CreatedAt = now,
            LastMessageAt = now
        };

        foreach (var participantId in allParticipantIds)
        {
            conversation.Participants.Add(new ConversationParticipant
            {
                UserId = participantId,
                JoinedAt = now
            });
        }

        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync();

        var message = new DirectMessage
        {
            ConversationId = conversation.Id,
            SenderUserId = creatorUserId,
            Content = request.InitialMessage.Trim(),
            CreatedAt = now
        };

        _db.DirectMessages.Add(message);
        await _db.SaveChangesAsync();

        // Update creator's last read
        var creatorParticipant = conversation.Participants.FirstOrDefault(p => p.UserId == creatorUserId);
        if (creatorParticipant != null)
        {
            creatorParticipant.LastReadMessageId = message.Id;
            creatorParticipant.LastReadAt = now;
            await _db.SaveChangesAsync();
        }

        // Notify other participants
        var creator = await _db.Users.FirstOrDefaultAsync(u => u.Id == creatorUserId);
        await NotifyMessageSentAsync(conversation.Id, allParticipantIds, creatorUserId, creator?.FullName ?? "Someone", message.Content);

        return await GetConversationByIdAsync(conversation.Id, creatorUserId);
    }

    public async Task<List<DirectMessageDto>> GetMessagesAsync(int conversationId, int userId, int take = 50)
    {
        var isParticipant = await _db.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId);
        if (!isParticipant)
            throw new UnauthorizedAccessException("You are not a participant in this conversation.");

        var messages = await _db.DirectMessages
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
            .Include(m => m.SenderUser)
                .ThenInclude(u => u!.Role)
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .AsNoTracking()
            .ToListAsync();

        return messages
            .OrderBy(m => m.CreatedAt)
            .Select(m => new DirectMessageDto(
                m.Id,
                m.ConversationId,
                m.SenderUserId,
                m.SenderUser?.FullName ?? "Unknown User",
                m.SenderUser?.Role?.Name,
                m.Content,
                m.CreatedAt
            ))
            .ToList();
    }

    public async Task<DirectMessageDto> SendMessageAsync(int conversationId, SendMessageRequest request, int senderUserId)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new InvalidOperationException("Message content cannot be empty.");

        var conversation = await _db.Conversations
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == conversationId)
            ?? throw new KeyNotFoundException($"Conversation {conversationId} not found.");

        var senderParticipant = conversation.Participants.FirstOrDefault(p => p.UserId == senderUserId);
        if (senderParticipant == null)
            throw new UnauthorizedAccessException("You are not a participant in this conversation.");

        var now = DateTime.UtcNow;
        var message = new DirectMessage
        {
            ConversationId = conversationId,
            SenderUserId = senderUserId,
            Content = request.Content.Trim(),
            CreatedAt = now
        };

        _db.DirectMessages.Add(message);
        conversation.LastMessageAt = now;

        await _db.SaveChangesAsync();

        senderParticipant.LastReadMessageId = message.Id;
        senderParticipant.LastReadAt = now;
        await _db.SaveChangesAsync();

        var sender = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == senderUserId);

        var recipientUserIds = conversation.Participants
            .Select(p => p.UserId)
            .ToList();

        await NotifyMessageSentAsync(conversationId, recipientUserIds, senderUserId, sender?.FullName ?? "Someone", message.Content);

        return new DirectMessageDto(
            message.Id,
            message.ConversationId,
            message.SenderUserId,
            sender?.FullName ?? "Unknown User",
            sender?.Role?.Name,
            message.Content,
            message.CreatedAt
        );
    }

    public async Task MarkAsReadAsync(int conversationId, int userId)
    {
        var participant = await _db.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId);

        if (participant == null) return;

        var latestMessageId = await _db.DirectMessages
            .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
            .OrderByDescending(m => m.Id)
            .Select(m => (int?)m.Id)
            .FirstOrDefaultAsync();

        if (latestMessageId.HasValue)
        {
            participant.LastReadMessageId = latestMessageId.Value;
            participant.LastReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<int> GetTotalUnreadCountAsync(int userId)
    {
        var memberships = await _db.ConversationParticipants
            .Where(cp => cp.UserId == userId)
            .Select(cp => new { cp.ConversationId, cp.LastReadMessageId })
            .ToListAsync();

        var total = 0;
        foreach (var m in memberships)
        {
            var lastRead = m.LastReadMessageId ?? 0;
            var unreadInConv = await _db.DirectMessages
                .CountAsync(msg => msg.ConversationId == m.ConversationId && msg.Id > lastRead && msg.SenderUserId != userId && !msg.IsDeleted);
            total += unreadInConv;
        }

        return total;
    }

    private async Task NotifyMessageSentAsync(int conversationId, List<int> allParticipants, int senderUserId, string senderName, string content)
    {
        try
        {
            var otherParticipants = allParticipants.Where(id => id != senderUserId).ToList();
            var snippet = content.Length > 80 ? content[..80] + "..." : content;

            foreach (var recipientId in otherParticipants)
            {
                var message = $"New message from {senderName}: \"{snippet}\"";
                var notif = new NotificationLog
                {
                    UserId = recipientId,
                    Type = "DirectMessage",
                    Message = message,
                    Severity = "info",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                _db.NotificationLogs.Add(notif);
                await _notificationService.NotifyAsync(recipientId, message);
            }

            if (otherParticipants.Count > 0)
            {
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message notification for conversation {ConversationId}", conversationId);
        }
    }
}
