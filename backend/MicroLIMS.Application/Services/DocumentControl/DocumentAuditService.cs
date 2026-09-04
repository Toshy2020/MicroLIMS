using System.Text;
using Microsoft.EntityFrameworkCore;
using MicroLIMS.Application.DTOs.DocumentControl;
using MicroLIMS.Application.Interfaces;
using MicroLIMS.Application.Interfaces.DocumentControl;
using MicroLIMS.Domain.Entities;
using MicroLIMS.Domain.Enums;
using MicroLIMS.Persistence.DbContext;

namespace MicroLIMS.Application.Services.DocumentControl;

public class DocumentAuditService : IDocumentAuditService
{
    private readonly MicroLimsDbContext _db;
    private readonly IAuditEventService _auditEventService;
    private readonly IDocumentAuthorizationService _authService;

    public DocumentAuditService(
        MicroLimsDbContext db,
        IAuditEventService auditEventService,
        IDocumentAuthorizationService authService)
    {
        _db = db;
        _auditEventService = auditEventService;
        _authService = authService;
    }

    public async Task<DocumentAuditResponse> SearchAuditLogsAsync(DocumentAuditFilterRequest filter, int userId)
    {
        var canQuery = await _authService.CanQueryGlobalAuditAsync(userId);
        if (!canQuery)
            throw new UnauthorizedAccessException("You do not have permission to query the global audit trail.");

        var query = BuildAuditQuery(filter);

        var totalCount = await query.CountAsync();
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var logs = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userIds = logs.Where(l => l.UserId.HasValue).Select(l => l.UserId!.Value).Distinct().ToList();
        var userMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        var items = logs.Select(l => MapToItemDto(l, userMap)).ToList();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new DocumentAuditResponse(items, totalCount, page, pageSize, totalPages);
    }

    public async Task<List<DocumentAuditItemDto>> GetRecordAuditHistoryAsync(int documentMasterId, int? revisionId, int userId)
    {
        var canView = await _authService.CanViewRecordAuditAsync(documentMasterId, userId);
        if (!canView)
            throw new UnauthorizedAccessException("You do not have permission to view audit history for this document.");

        var query = _db.AuditLogs
            .Include(a => a.Changes)
            .AsNoTracking()
            .Where(a => a.DocumentMasterId == documentMasterId);

        if (revisionId.HasValue)
        {
            query = query.Where(a => a.DocumentRevisionId == revisionId.Value);
        }

        var logs = await query
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();

        var userIds = logs.Where(l => l.UserId.HasValue).Select(l => l.UserId!.Value).Distinct().ToList();
        var userMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        return logs.Select(l => MapToItemDto(l, userMap)).ToList();
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> ExportAuditTrailAsync(DocumentAuditFilterRequest filter, int userId)
    {
        var canQuery = await _authService.CanQueryGlobalAuditAsync(userId);
        if (!canQuery)
            throw new UnauthorizedAccessException("You do not have permission to export the audit trail.");

        var query = BuildAuditQuery(filter);
        var logs = await query
            .OrderByDescending(a => a.Timestamp)
            .Take(5000)
            .ToListAsync();

        var userIds = logs.Where(l => l.UserId.HasValue).Select(l => l.UserId!.Value).Distinct().ToList();
        var userMap = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        var sb = new StringBuilder();
        sb.AppendLine("EventUid,TimestampUTC,ActorType,User,SystemProcess,ActionCode,ActionCategory,RecordType,EntityId,DocumentMasterId,DocumentRevisionId,Reason,Changes");

        foreach (var log in logs)
        {
            string userName = "";
            if (log.UserId.HasValue && userMap.TryGetValue(log.UserId.Value, out var name))
                userName = name;
            else if (log.UserId.HasValue)
                userName = $"User #{log.UserId}";

            var userStr = EscapeCsv(userName);
            var changesStr = EscapeCsv(string.Join("; ", log.Changes.Select(c => $"{c.FieldName}: '{c.PreviousValue}' -> '{c.NewValue}'")));

            sb.AppendLine($"{log.EventUid},{log.Timestamp:O},{log.ActorType},{userStr},{EscapeCsv(log.SystemProcessName)},{log.ActionCode},{log.ActionCategory},{EscapeCsv(log.EntityName)},{EscapeCsv(log.EntityId)},{log.DocumentMasterId},{log.DocumentRevisionId},{EscapeCsv(log.Reason)},{changesStr}");
        }

        // Record export in audit trail (DC-URS-131: audit trail export is itself an audited action)
        await _auditEventService.RecordUserEventAsync(
            actionCode: "AuditTrailExported",
            actionCategory: AuditActionCategory.Security,
            recordType: nameof(AuditLog),
            reason: $"Exported {logs.Count} audit records to CSV.",
            changes: new List<AuditFieldChange>
            {
                new("ExportedRecordCount", null, logs.Count.ToString())
            });

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"audit_trail_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return (bytes, "text/csv", fileName);
    }

    private IQueryable<AuditLog> BuildAuditQuery(DocumentAuditFilterRequest filter)
    {
        var query = _db.AuditLogs
            .Include(a => a.Changes)
            .AsNoTracking()
            .AsQueryable();

        if (filter.DateFrom.HasValue)
            query = query.Where(a => a.Timestamp >= filter.DateFrom.Value);

        if (filter.DateTo.HasValue)
            query = query.Where(a => a.Timestamp <= filter.DateTo.Value);

        if (filter.UserId.HasValue)
            query = query.Where(a => a.UserId == filter.UserId.Value);

        if (filter.ActionCategory.HasValue)
            query = query.Where(a => a.ActionCategory == filter.ActionCategory.Value);

        if (!string.IsNullOrWhiteSpace(filter.ActionCode))
            query = query.Where(a => a.ActionCode == filter.ActionCode.Trim());

        if (!string.IsNullOrWhiteSpace(filter.RecordType))
            query = query.Where(a => a.EntityName == filter.RecordType.Trim());

        if (filter.DocumentMasterId.HasValue)
            query = query.Where(a => a.DocumentMasterId == filter.DocumentMasterId.Value);

        if (filter.DocumentRevisionId.HasValue)
            query = query.Where(a => a.DocumentRevisionId == filter.DocumentRevisionId.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(a => (a.EventUid != null && a.EventUid.ToLower().Contains(term)) ||
                                     (a.ActionCode != null && a.ActionCode.ToLower().Contains(term)) ||
                                     (a.Action != null && a.Action.ToLower().Contains(term)) ||
                                     (a.Reason != null && a.Reason.ToLower().Contains(term)) ||
                                     (a.EntityName != null && a.EntityName.ToLower().Contains(term)) ||
                                     (a.EntityId != null && a.EntityId.ToLower().Contains(term)));
        }

        return query;
    }

    private static DocumentAuditItemDto MapToItemDto(AuditLog log, Dictionary<int, string> userMap)
    {
        string? userName = null;
        if (log.UserId.HasValue && userMap.TryGetValue(log.UserId.Value, out var name))
            userName = name;
        else if (log.UserId.HasValue)
            userName = $"User #{log.UserId}";

        return new DocumentAuditItemDto(
            log.Id,
            log.EventUid,
            log.Timestamp,
            log.ActorType,
            log.UserId,
            userName,
            log.SystemProcessName,
            log.Action,
            log.ActionCode,
            log.ActionCategory,
            log.EntityName,
            log.EntityId,
            log.Reason,
            log.SourceContext,
            log.CorrelationId,
            log.DocumentMasterId,
            log.DocumentRevisionId,
            log.Changes.Select(c => new AuditEventChangeDto(c.FieldName, c.PreviousValue, c.NewValue)).ToList()
        );
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
