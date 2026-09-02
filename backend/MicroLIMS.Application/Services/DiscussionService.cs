using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Infrastructure.Notifications;
using MicroLIMS.Infrastructure.Storage;
using MicroLIMS.Persistence.DbContext;
using MicroLIMS.Shared.Responses;

namespace MicroLIMS.Application.Services;

// ---- DTOs ----

public record DiscussionAttachmentDto(
    int Id,
    string FileName,
    string FileExtension,
    string ContentType,
    long FileSizeBytes,
    DateTime UploadedAt);

public record DiscussionCommentDto(
    int Id,
    int PostId,
    int AuthorUserId,
    string AuthorName,
    string? AuthorRole,
    string Content,
    bool IsEdited,
    DateTime? LastEditedAt,
    DateTime CreatedAt);

public record DiscussionPostSummaryDto(
    int Id,
    string Title,
    string ContentPreview,
    DiscussionCategory Category,
    string CategoryName,
    int AuthorUserId,
    string AuthorName,
    string? AuthorRole,
    bool IsImportant,
    int CurrentVersion,
    bool IsEdited,
    DateTime? LastEditedAt,
    DateTime CreatedAt,
    int CommentCount,
    int AttachmentCount,
    List<DiscussionAttachmentDto> Attachments);

public record DiscussionPostDetailDto(
    int Id,
    string Title,
    string Content,
    DiscussionCategory Category,
    string CategoryName,
    int AuthorUserId,
    string AuthorName,
    string? AuthorRole,
    bool IsImportant,
    int CurrentVersion,
    bool IsEdited,
    DateTime? LastEditedAt,
    DateTime CreatedAt,
    List<DiscussionAttachmentDto> Attachments,
    List<DiscussionCommentDto> Comments,
    int VersionCount);

public record DiscussionVersionDto(
    int Id,
    int VersionNumber,
    string Title,
    string Content,
    DiscussionCategory Category,
    string CategoryName,
    int ChangedByUserId,
    string ChangedByName,
    DateTime ChangedAt);

public record CreateDiscussionPostRequest(
    string Title,
    string Content,
    DiscussionCategory Category,
    bool IsImportant = false);

public record UpdateDiscussionPostRequest(
    string Title,
    string Content,
    DiscussionCategory Category,
    bool IsImportant = false);

public record CreateDiscussionCommentRequest(string Content);

public record UpdateDiscussionCommentRequest(string Content);

// ---- Service ----

public class DiscussionService
{
    private readonly MicroLimsDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DiscussionService> _logger;

    public DiscussionService(
        MicroLimsDbContext db,
        IFileStorageService storage,
        INotificationService notificationService,
        ILogger<DiscussionService> logger)
    {
        _db = db;
        _storage = storage;
        _notificationService = notificationService;
        _logger = logger;
    }

    public static string GetCategoryDisplayName(DiscussionCategory category) => category switch
    {
        DiscussionCategory.Water => "Water",
        DiscussionCategory.Equipment => "Equipment",
        DiscussionCategory.EnvironmentalMonitoring => "Environmental Monitoring (EM)",
        DiscussionCategory.Products => "Products",
        DiscussionCategory.MediaMaterials => "Media / Materials",
        DiscussionCategory.InternalDecisions => "Internal Decisions",
        DiscussionCategory.ManagementRequirements => "Management Requirements",
        DiscussionCategory.EdaRequirements => "EDA Requirements",
        DiscussionCategory.Iso17025 => "ISO 17025",
        DiscussionCategory.GmpRegulatory => "GMP / Regulatory",
        DiscussionCategory.Other => "Other",
        _ => category.ToString()
    };

    public async Task<PagedResult<DiscussionPostSummaryDto>> GetFeedAsync(
        DiscussionCategory? category = null,
        string? search = null,
        bool importantOnly = false,
        int page = 1,
        int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = _db.DiscussionPosts
            .Where(p => !p.IsDeleted)
            .Include(p => p.AuthorUser)
                .ThenInclude(u => u!.Role)
            .Include(p => p.Attachments)
            .Include(p => p.Comments.Where(c => !c.IsDeleted))
            .AsNoTracking();

        if (category.HasValue)
        {
            query = query.Where(p => p.Category == category.Value);
        }

        if (importantOnly)
        {
            query = query.Where(p => p.IsImportant);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p => p.Title.ToLower().Contains(term) || p.Content.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();

        var posts = await query
            .OrderByDescending(p => p.IsImportant)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = posts.Select(p =>
        {
            var preview = p.Content.Length > 200 ? p.Content[..200] + "..." : p.Content;
            return new DiscussionPostSummaryDto(
                p.Id,
                p.Title,
                preview,
                p.Category,
                GetCategoryDisplayName(p.Category),
                p.AuthorUserId,
                p.AuthorUser?.FullName ?? "Unknown User",
                p.AuthorUser?.Role?.Name ?? "Staff",
                p.IsImportant,
                p.CurrentVersion,
                p.IsEdited,
                p.LastEditedAt,
                p.CreatedAt,
                p.Comments.Count,
                p.Attachments.Count,
                p.Attachments.Select(a => new DiscussionAttachmentDto(
                    a.Id,
                    a.OriginalFileName,
                    a.FileExtension,
                    a.ContentType,
                    a.FileSizeBytes,
                    a.UploadedAt
                )).ToList()
            );
        }).ToList();

        return new PagedResult<DiscussionPostSummaryDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<DiscussionPostDetailDto> GetPostByIdAsync(int id)
    {
        var post = await _db.DiscussionPosts
            .Include(p => p.AuthorUser)
                .ThenInclude(u => u!.Role)
            .Include(p => p.Attachments)
            .Include(p => p.Comments.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.AuthorUser)
                    .ThenInclude(u => u!.Role)
            .Include(p => p.Versions)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted)
            ?? throw new KeyNotFoundException($"Discussion post {id} not found.");

        return new DiscussionPostDetailDto(
            post.Id,
            post.Title,
            post.Content,
            post.Category,
            GetCategoryDisplayName(post.Category),
            post.AuthorUserId,
            post.AuthorUser?.FullName ?? "Unknown User",
            post.AuthorUser?.Role?.Name ?? "Staff",
            post.IsImportant,
            post.CurrentVersion,
            post.IsEdited,
            post.LastEditedAt,
            post.CreatedAt,
            post.Attachments.Select(a => new DiscussionAttachmentDto(
                a.Id,
                a.OriginalFileName,
                a.FileExtension,
                a.ContentType,
                a.FileSizeBytes,
                a.UploadedAt
            )).ToList(),
            post.Comments
                .OrderBy(c => c.CreatedAt)
                .Select(c => new DiscussionCommentDto(
                    c.Id,
                    c.PostId,
                    c.AuthorUserId,
                    c.AuthorUser?.FullName ?? "Unknown User",
                    c.AuthorUser?.Role?.Name ?? "Staff",
                    c.Content,
                    c.IsEdited,
                    c.LastEditedAt,
                    c.CreatedAt
                )).ToList(),
            post.Versions.Count
        );
    }

    public async Task<DiscussionPostDetailDto> CreatePostAsync(
        CreateDiscussionPostRequest request,
        List<(string FileName, string ContentType, byte[] Data)>? attachments,
        int authorUserId)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Title is required.");
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new InvalidOperationException("Content is required.");

        var post = new DiscussionPost
        {
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            Category = request.Category,
            AuthorUserId = authorUserId,
            IsImportant = request.IsImportant,
            CurrentVersion = 1,
            CreatedAt = DateTime.UtcNow
        };

        _db.DiscussionPosts.Add(post);
        await _db.SaveChangesAsync();

        if (attachments != null && attachments.Count > 0)
        {
            foreach (var file in attachments)
            {
                var cleanName = Path.GetFileName(file.FileName);
                var extension = Path.GetExtension(cleanName).ToLowerInvariant();
                var storageKey = $"discussions/{post.Id}/{Guid.NewGuid()}_{cleanName}";
                var hash = Convert.ToHexString(SHA256.HashData(file.Data));

                await _storage.SaveAsync(storageKey, file.Data);

                var attachment = new DiscussionAttachment
                {
                    PostId = post.Id,
                    OriginalFileName = cleanName,
                    StorageKey = storageKey,
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                    FileExtension = extension,
                    FileSizeBytes = file.Data.Length,
                    ContentSha256 = hash,
                    UploadedByUserId = authorUserId,
                    UploadedAt = DateTime.UtcNow
                };

                _db.DiscussionAttachments.Add(attachment);
            }

            await _db.SaveChangesAsync();
        }

        return await GetPostByIdAsync(post.Id);
    }

    public async Task<DiscussionPostDetailDto> UpdatePostAsync(
        int id,
        UpdateDiscussionPostRequest request,
        int actingUserId,
        bool canEditAny)
    {
        var post = await _db.DiscussionPosts
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted)
            ?? throw new KeyNotFoundException($"Discussion post {id} not found.");

        if (post.AuthorUserId != actingUserId && !canEditAny)
            throw new UnauthorizedAccessException("You are not authorized to edit this discussion post.");

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Title is required.");
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new InvalidOperationException("Content is required.");

        // Preserve previous version in DiscussionPostVersion before updating
        var versionSnapshot = new DiscussionPostVersion
        {
            PostId = post.Id,
            VersionNumber = post.CurrentVersion,
            Title = post.Title,
            Content = post.Content,
            Category = post.Category,
            ChangedByUserId = actingUserId,
            ChangedAt = DateTime.UtcNow
        };
        _db.DiscussionPostVersions.Add(versionSnapshot);

        post.Title = request.Title.Trim();
        post.Content = request.Content.Trim();
        post.Category = request.Category;
        post.IsImportant = request.IsImportant;
        post.CurrentVersion += 1;
        post.IsEdited = true;
        post.LastEditedAt = DateTime.UtcNow;
        post.LastEditedByUserId = actingUserId;

        await _db.SaveChangesAsync();

        // Requirement: "Add a notification when an existing discussion post is updated. Notify users who have participated in that discussion."
        await NotifyPostUpdatedAsync(post, actingUserId);

        return await GetPostByIdAsync(post.Id);
    }

    public async Task<bool> ToggleImportantAsync(int id, int actingUserId, bool canEditAny)
    {
        var post = await _db.DiscussionPosts
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted)
            ?? throw new KeyNotFoundException($"Discussion post {id} not found.");

        if (post.AuthorUserId != actingUserId && !canEditAny)
            throw new UnauthorizedAccessException("You are not authorized to modify this discussion post.");

        post.IsImportant = !post.IsImportant;
        await _db.SaveChangesAsync();
        return post.IsImportant;
    }

    public async Task DeletePostAsync(int id, int actingUserId, bool canEditAny)
    {
        var post = await _db.DiscussionPosts
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted)
            ?? throw new KeyNotFoundException($"Discussion post {id} not found.");

        if (post.AuthorUserId != actingUserId && !canEditAny)
            throw new UnauthorizedAccessException("You are not authorized to delete this discussion post.");

        post.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    public async Task<List<DiscussionVersionDto>> GetPostHistoryAsync(int postId)
    {
        var versions = await _db.DiscussionPostVersions
            .Where(v => v.PostId == postId)
            .Include(v => v.ChangedByUser)
            .OrderByDescending(v => v.VersionNumber)
            .AsNoTracking()
            .ToListAsync();

        return versions.Select(v => new DiscussionVersionDto(
            v.Id,
            v.VersionNumber,
            v.Title,
            v.Content,
            v.Category,
            GetCategoryDisplayName(v.Category),
            v.ChangedByUserId,
            v.ChangedByUser?.FullName ?? "Unknown User",
            v.ChangedAt
        )).ToList();
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> GetAttachmentContentAsync(int postId, int attachmentId)
    {
        var attachment = await _db.DiscussionAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.PostId == postId)
            ?? throw new KeyNotFoundException($"Attachment {attachmentId} not found.");

        var data = await _storage.ReadAsync(attachment.StorageKey);

        // Verify SHA-256 integrity
        var calculatedHash = Convert.ToHexString(SHA256.HashData(data));
        if (!string.Equals(calculatedHash, attachment.ContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("Checksum mismatch for attachment {Id}. Expected {Exp}, calculated {Calc}",
                attachment.Id, attachment.ContentSha256, calculatedHash);
            throw new InvalidOperationException("File integrity verification failed.");
        }

        return (data, attachment.ContentType, attachment.OriginalFileName);
    }

    // ---- Comments ----

    public async Task<DiscussionCommentDto> AddCommentAsync(int postId, CreateDiscussionCommentRequest request, int authorUserId)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new InvalidOperationException("Comment content cannot be empty.");

        var post = await _db.DiscussionPosts
            .FirstOrDefaultAsync(p => p.Id == postId && !p.IsDeleted)
            ?? throw new KeyNotFoundException($"Discussion post {postId} not found.");

        var comment = new DiscussionComment
        {
            PostId = postId,
            AuthorUserId = authorUserId,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _db.DiscussionComments.Add(comment);
        await _db.SaveChangesAsync();

        var author = await _db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == authorUserId);

        // Notify post author and participants
        await NotifyCommentAddedAsync(post, comment, author?.FullName ?? "Someone");

        return new DiscussionCommentDto(
            comment.Id,
            comment.PostId,
            comment.AuthorUserId,
            author?.FullName ?? "Unknown User",
            author?.Role?.Name ?? "Staff",
            comment.Content,
            comment.IsEdited,
            comment.LastEditedAt,
            comment.CreatedAt
        );
    }

    public async Task<DiscussionCommentDto> UpdateCommentAsync(
        int postId,
        int commentId,
        UpdateDiscussionCommentRequest request,
        int actingUserId,
        bool canEditAny)
    {
        var comment = await _db.DiscussionComments
            .Include(c => c.AuthorUser)
                .ThenInclude(u => u!.Role)
            .FirstOrDefaultAsync(c => c.Id == commentId && c.PostId == postId && !c.IsDeleted)
            ?? throw new KeyNotFoundException($"Comment {commentId} not found.");

        if (comment.AuthorUserId != actingUserId && !canEditAny)
            throw new UnauthorizedAccessException("You are not authorized to edit this comment.");

        if (string.IsNullOrWhiteSpace(request.Content))
            throw new InvalidOperationException("Comment content cannot be empty.");

        comment.Content = request.Content.Trim();
        comment.IsEdited = true;
        comment.LastEditedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new DiscussionCommentDto(
            comment.Id,
            comment.PostId,
            comment.AuthorUserId,
            comment.AuthorUser?.FullName ?? "Unknown User",
            comment.AuthorUser?.Role?.Name ?? "Staff",
            comment.Content,
            comment.IsEdited,
            comment.LastEditedAt,
            comment.CreatedAt
        );
    }

    public async Task DeleteCommentAsync(int postId, int commentId, int actingUserId, bool canEditAny)
    {
        var comment = await _db.DiscussionComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.PostId == postId && !c.IsDeleted)
            ?? throw new KeyNotFoundException($"Comment {commentId} not found.");

        if (comment.AuthorUserId != actingUserId && !canEditAny)
            throw new UnauthorizedAccessException("You are not authorized to delete this comment.");

        comment.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    // ---- Notifications Integration ----

    private async Task NotifyPostUpdatedAsync(DiscussionPost post, int editorUserId)
    {
        try
        {
            var editor = await _db.Users.FirstOrDefaultAsync(u => u.Id == editorUserId);
            var editorName = editor?.FullName ?? "Someone";

            // Find all participants: post author + all comment authors on this post
            var participantUserIds = await _db.DiscussionComments
                .Where(c => c.PostId == post.Id && !c.IsDeleted)
                .Select(c => c.AuthorUserId)
                .Distinct()
                .ToListAsync();

            if (!participantUserIds.Contains(post.AuthorUserId))
            {
                participantUserIds.Add(post.AuthorUserId);
            }

            // Exclude the person who edited the post
            participantUserIds.Remove(editorUserId);

            foreach (var userId in participantUserIds)
            {
                var message = $"Discussion '{post.Title}' was updated by {editorName}.";
                var notif = new NotificationLog
                {
                    UserId = userId,
                    Type = "DiscussionPostUpdated",
                    Message = message,
                    Severity = "info",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                _db.NotificationLogs.Add(notif);
                await _notificationService.NotifyAsync(userId, message);
            }

            if (participantUserIds.Count > 0)
            {
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send update notifications for post {PostId}", post.Id);
        }
    }

    private async Task NotifyCommentAddedAsync(DiscussionPost post, DiscussionComment comment, string commenterName)
    {
        try
        {
            // Collect target users: post author + previous commenters, excluding current commenter
            var targetUserIds = await _db.DiscussionComments
                .Where(c => c.PostId == post.Id && !c.IsDeleted && c.AuthorUserId != comment.AuthorUserId)
                .Select(c => c.AuthorUserId)
                .Distinct()
                .ToListAsync();

            if (post.AuthorUserId != comment.AuthorUserId && !targetUserIds.Contains(post.AuthorUserId))
            {
                targetUserIds.Add(post.AuthorUserId);
            }

            foreach (var userId in targetUserIds)
            {
                var isAuthor = userId == post.AuthorUserId;
                var message = isAuthor
                    ? $"{commenterName} commented on your discussion: '{post.Title}'."
                    : $"{commenterName} also commented on discussion: '{post.Title}'.";

                var notif = new NotificationLog
                {
                    UserId = userId,
                    Type = "DiscussionComment",
                    Message = message,
                    Severity = "info",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                _db.NotificationLogs.Add(notif);
                await _notificationService.NotifyAsync(userId, message);
            }

            if (targetUserIds.Count > 0)
            {
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send comment notifications for post {PostId}", post.Id);
        }
    }
}
